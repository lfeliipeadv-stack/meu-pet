using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// The pet notices how you use the computer (Settings → Extras → "Notice how I use the computer"):
	/// a long time without a break, coming back after being away (sleep, lock or just out), and closing a program
	/// used for hours that day. Only the time of the last key or mouse movement and the name of the program in front
	/// are looked at, never what is typed or shown; nothing leaves this computer.
	/// </summary>
	public static class Activity
	{
		[StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
		[DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
		[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

		/// <summary>Minutes of use without a 5-minute break before the pet suggests one.</summary>
		public const int BreakAfter = 90;
		const int BreakGap = 5, AwayLong = 30, RepeatBreak = 45, LongApp = 120;

		static Timer timer;
		static DateTime lastActive = DateTime.MinValue, streakStart = DateTime.MinValue, lastBreakSaid = DateTime.MinValue;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("ActivityCare", true);
			set { Program.MyData.SetSetting("ActivityCare", value); Apply(); }
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 30000 }; timer.Tick += (s, e) => Tick(); }
			if (Enabled) timer.Start(); else timer.Stop();
		}

		static double IdleSeconds()
		{
			var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
			if (!GetLastInputInfo(ref info)) return 0;
			return Math.Max(0, unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0);
		}

		static void Tick()
		{
			try
			{
				var now = DateTime.Now;
				double idle = IdleSeconds();
				Say(Step(now, idle, Pomodoro.Current != Pomodoro.Phase.Off), now);
				if (idle < 60) SampleForeground(now);
				Say(EndedApp(now, pid => { try { return !Process.GetProcessById(pid).HasExited; } catch (Exception) { return false; } }), now);
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "activity: " + ex.Message); }
		}

		static void Say(string what, DateTime now)
		{
			if (what == null) return;
			if (what == "break")
			{
				Program.Mainthread?.ReactAll("stretch", "yawn", "wave");
				SpeechBubble.Notify(Lang.T("act.break", Duration(now - streakStart)), 0,
					new SpeechBubble.Choice(Lang.T("act.breakOk"), () => { streakStart = DateTime.Now; Program.Mainthread?.ReactAll("happy", "cheer"); }, true),
					new SpeechBubble.Choice(Lang.T("act.breakLater"), null));
			}
			else if (what == "welcome")
			{
				Program.Mainthread?.ReactAll("wave", "hello", "happy", "jump");
				SpeechBubble.Notify(Lang.T("act.welcome"), 6);
			}
			else if (what.StartsWith("app|"))
			{
				var parts = what.Split('|');
				Program.Mainthread?.ReactAll("cheer", "dance", "happy", "jump");
				SpeechBubble.Notify(Lang.T("act.appDone", parts[2], parts[1]), 8);
			}
		}

		/// <summary>"1h30", "45 min".</summary>
		internal static string Duration(TimeSpan t) =>
			t.TotalMinutes < 60 ? $"{(int)t.TotalMinutes} min" : $"{(int)t.TotalHours}h{(t.Minutes > 0 ? t.Minutes.ToString("00") : "")}";

		// ------------------------------------------------------------------ breaks and coming back (testable: the clock is given)

		/// <summary>One look: "break", "welcome" or null. A 5-minute pause starts a new streak; 30 minutes away is "coming back".</summary>
		internal static string Step(DateTime now, double idleSeconds, bool pomodoro)
		{
			bool active = idleSeconds < 60;
			if (!active) return null;
			string said = null;
			if (lastActive != DateTime.MinValue && (now - lastActive).TotalMinutes >= AwayLong) said = "welcome";
			if (streakStart == DateTime.MinValue || lastActive == DateTime.MinValue || (now - lastActive).TotalMinutes >= BreakGap) {
				if (lastActive != DateTime.MinValue && !testing) DayStats.Add("breaks", 1);   // a pause of 5 minutes or more
				streakStart = now;
			}
			lastActive = now;
			if (said == null && !pomodoro && (now - streakStart).TotalMinutes >= BreakAfter && (now - lastBreakSaid).TotalMinutes >= RepeatBreak)
			{
				lastBreakSaid = now;
				said = "break";
			}
			return said;
		}

		/// <summary>Self-test: start again from nothing.</summary>
		/// <summary>Self-test runs: the day counters are not touched.</summary>
		static bool testing;

		internal static void Reset() { testing = true; lastActive = streakStart = lastBreakSaid = DateTime.MinValue; used.Clear(); celebrated.Clear(); }

		// ------------------------------------------------------------------ programs used for a long time

		sealed class Use { public string Name; public int Pid; public double Seconds; public DateTime Day; }
		static readonly Dictionary<int, Use> used = new Dictionary<int, Use>();
		static readonly HashSet<string> celebrated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		static DateTime lastSample = DateTime.MinValue;

		static readonly string[] Ignore = { "explorer", "desktoppet", "searchhost", "startmenuexperiencehost", "shellexperiencehost", "applicationframehost", "lockapp", "textinputhost", "systemsettings" };

		static void SampleForeground(DateTime now)
		{
			double seconds = lastSample == DateTime.MinValue ? 30 : Math.Min(60, (now - lastSample).TotalSeconds);
			lastSample = now;
			GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
			if (pid == 0) return;
			string name;
			try
			{
				var p = Process.GetProcessById((int)pid);
				if (Ignore.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase)) return;
				name = p.ProcessName;
				try { string d = p.MainModule?.FileVersionInfo.FileDescription; if (!string.IsNullOrWhiteSpace(d) && d.Length <= 40) name = d.Trim(); } catch (Exception skipped) { LogFile.Ignored(skipped); }
			}
			catch (Exception) { return; }
			Track((int)pid, name, seconds, now);
		}

		/// <summary>Add time to a program in front (self-test feeds it directly).</summary>
		internal static void Track(int pid, string name, double seconds, DateTime now)
		{
			if (!used.TryGetValue(pid, out var u) || u.Day != now.Date) used[pid] = u = new Use { Name = name, Pid = pid, Day = now.Date };
			u.Seconds += seconds;
		}

		/// <summary>A program used for 2 hours or more today was closed: "app|name|duration" (once a day per program).</summary>
		internal static string EndedApp(DateTime now, Func<int, bool> alive)
		{
			foreach (var u in used.Values.ToList())
			{
				if (alive(u.Pid)) continue;
				used.Remove(u.Pid);
				string key = now.Date.ToString("yyyyMMdd") + "|" + u.Name;
				if (u.Seconds >= LongApp * 60 && u.Day == now.Date && celebrated.Add(key))
					return "app|" + u.Name + "|" + Duration(TimeSpan.FromSeconds(u.Seconds));
			}
			return null;
		}
	}
}

namespace DesktopPet
{
	/// <summary>Counters of today (minutes of focus, breaks taken…) for the Home page; they start again each day.</summary>
	public static class DayStats
	{
		static string Key(string name) => "Day." + name;

		public static int Get(string name)
		{
			var p = Program.MyData.GetSetting(Key(name)).Split('|');
			return p.Length == 2 && p[0] == DateTime.Today.ToString("yyyy-MM-dd") && int.TryParse(p[1], out int n) ? n : 0;
		}

		public static void Add(string name, int n)
		{
			try { Program.MyData.SetSetting(Key(name), DateTime.Today.ToString("yyyy-MM-dd") + "|" + (Get(name) + n)); }
			catch (Exception skipped) { LogFile.Ignored(skipped); }
		}
	}
}
