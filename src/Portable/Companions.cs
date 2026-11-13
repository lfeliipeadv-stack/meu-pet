using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Pets that meet on screen greet each other, play or show love (Settings → Extras → "Pets greet each other").
	/// </summary>
	public static class PetsMeet
	{
		static Timer timer;
		static readonly Dictionary<string, DateTime> lastMeet = new Dictionary<string, DateTime>();
		static readonly Random rnd = new Random();

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("PetsMeet", true);
			set => Program.MyData.SetSetting("PetsMeet", value);
		}

		static readonly string[][] Moods =
		{
			new[] { "wave", "hello", "happy" },
			new[] { "dance", "jump", "cheer", "spin" },
			new[] { "love", "in love", "hearts", "blow kiss", "wink" },
		};

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 700 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		/// <summary>
		/// A tiny conversation: one pet says a line, the other answers a moment later (pairs of lines in the language
		/// in use). Not during quiet time or while a bubble is talking to the user. False if it did not happen.
		/// </summary>
		static DateTime chatUntil = DateTime.MinValue;
		internal static void ResetChat() => chatUntil = DateTime.MinValue;

		internal static bool Talk(FormPet a, FormPet b)
		{
			if (SpeechBubble.QuietNow || SpeechBubble.Talking || DateTime.Now < chatUntil) return false;
			var first = Lang.T("chat.first").Split('|');
			var answer = Lang.T("chat.answer").Split('|');
			int n = Math.Min(first.Length, answer.Length);
			if (n == 0) return false;
			int i = rnd.Next(n);
			if (SpeechBubble.Chat(a, first[i].Trim(), 4) == null) return false;
			chatUntil = DateTime.Now.AddSeconds(8);   // one conversation at a time
			a.React("wave", "hello", "talk");
			var later = new Timer { Interval = 2200 };
			later.Tick += (s, e) =>
			{
				later.Stop(); later.Dispose();
				if (b.IsDisposed || !b.Visible) return;
				SpeechBubble.Chat(b, answer[i].Trim(), 4);
				b.React("happy", "laugh", "wave", "talk");
			};
			later.Start();
			StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.info, "pets chat: " + first[i]);
			return true;
		}

		/// <summary>Two pets standing side by side on the same ground (self-test checks it with rectangles).</summary>
		public static bool Close(Rectangle a, Rectangle b)
		{
			int ca = a.Left + a.Width / 2, cb = b.Left + b.Width / 2;
			return Math.Abs(ca - cb) < (a.Width + b.Width) / 2 * 0.8 && Math.Abs(a.Bottom - b.Bottom) < 24 && Math.Abs(ca - cb) > 4;
		}

		static void Check()
		{
			if (!Enabled || HideHotkey.Hidden) return;
			var pets = Application.OpenForms.OfType<FormPet>().Where(p => p.Visible && !p.IsChildWindow && !p.Busy && p.Opacity > 0).ToList();
			if (pets.Count < 2) return;
			for (int i = 0; i < pets.Count; i++)
				for (int j = i + 1; j < pets.Count; j++)
				{
					var a = pets[i]; var b = pets[j];
					if (!Close(a.Bounds, b.Bounds)) continue;
					string key = a.Handle + "-" + b.Handle;
					if (lastMeet.TryGetValue(key, out var t) && (DateTime.Now - t).TotalSeconds < 90) continue;
					lastMeet[key] = DateTime.Now;
					// sometimes they say a few words to each other, otherwise they wave, dance or show love
					if (rnd.Next(100) < 40 && Talk(a, b)) continue;
					var mood = Moods[rnd.Next(Moods.Length)];
					a.React(mood);
					b.React(mood);
					StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.info, "pets meet: " + mood[0]);
				}
		}
	}

	/// <summary>
	/// The pet notices what happens on the computer: low battery, a finished download, a good morning and a late night
	/// (Settings → Extras → "Reactions to the computer").
	/// </summary>
	public static class ComputerEvents
	{
		static Timer timer;
		static FileSystemWatcher downloads;
		static readonly Dictionary<string, DateTime> pending = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
		static DateTime lastDownloadSaid = DateTime.MinValue;
		static bool lowSaid, criticalSaid, wasCharging = true;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("PcEvents", true);
			set { Program.MyData.SetSetting("PcEvents", value); Apply(); }
		}

		/// <summary>Battery level (%) of the "low battery" notice.</summary>
		public static int BatteryLow
		{
			get => Program.MyData.GetSettingInt("BatteryLow", 20);
			set => Program.MyData.SetSetting("BatteryLow", value);
		}

		/// <summary>Also say when the charger is plugged in or out.</summary>
		public static bool PlugNotices
		{
			get => Program.MyData.GetSettingBool("PlugNotices", false);
			set => Program.MyData.SetSetting("PlugNotices", value);
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 15000 }; timer.Tick += (s, e) => Tick(); }
			if (Enabled)
			{
				timer.Start();
				WatchDownloads();
				// good morning, a little after the start
				var hello = new Timer { Interval = 8000 };
				hello.Tick += (s, e) => { hello.Stop(); hello.Dispose(); Greetings(); };
				hello.Start();
			}
			else
			{
				timer.Stop();
				downloads?.Dispose();
				downloads = null;
			}
		}

		static void Tick()
		{
			Battery();
			Greetings();
			FinishedDownloads();
		}

		// ------------------------------------------------------------------ battery

		static void Battery()
		{
			var ps = SystemInformation.PowerStatus;
			if (ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) || ps.BatteryChargeStatus == BatteryChargeStatus.Unknown) return;
			int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
			bool charging = ps.PowerLineStatus == PowerLineStatus.Online;
			int low = BatteryLow, critical = Math.Min(10, low / 2);
			if (charging)
			{
				if (!wasCharging && (lowSaid || PlugNotices)) { Program.Mainthread?.ReactAll("happy", "cheer", "jump"); SpeechBubble.Notify(Lang.T("pc.charging"), 5); }
				lowSaid = criticalSaid = false;
			}
			else if (wasCharging && PlugNotices && pct > low)
			{
				Program.Mainthread?.ReactAll("look around", "think", "wave");
				SpeechBubble.Notify(Lang.T("pc.unplugged", pct), 6);
			}
			else if (pct <= critical && !criticalSaid)
			{
				criticalSaid = lowSaid = true;
				Program.Mainthread?.ReactAll("scared", "shocked", "tired", "dizzy");
				SpeechBubble.Notify(Lang.T("pc.batteryCritical", pct), 0, new SpeechBubble.Choice("OK", null, true));
			}
			else if (pct <= low && !lowSaid)
			{
				lowSaid = true;
				Program.Mainthread?.ReactAll("tired", "yawn", "sleepy");
				SpeechBubble.Notify(Lang.T("pc.batteryLow", pct), 10);
			}
			wasCharging = charging;
		}

		// ------------------------------------------------------------------ good morning / late night

		static void Greetings()
		{
			var now = DateTime.Now;
			string today = now.ToString("yyyy-MM-dd");
			if (now.Hour >= 5 && now.Hour < 11 && Program.MyData.GetSetting("SaidMorning") != today && !(Weather.Enabled && Weather.Morning))   // the weather says good morning then
			{
				Program.MyData.SetSetting("SaidMorning", today);
				Program.Mainthread?.ReactAll("wave", "hello", "stretch", "happy");
				SpeechBubble.Notify(Lang.T("pc.morning"), 8);
			}
			// the night belongs to the day it started (00:30 is still "last night")
			string night = (now.Hour < 5 ? now.AddDays(-1) : now).ToString("yyyy-MM-dd");
			if ((now.Hour >= 23 || now.Hour < 4) && Program.MyData.GetSetting("SaidNight") != night)
			{
				Program.MyData.SetSetting("SaidNight", night);
				Program.Mainthread?.ReactAll("yawn", "sleepy", "tired", "sleep");
				SpeechBubble.Notify(Lang.T("pc.night"), 10);
			}
		}

		// ------------------------------------------------------------------ downloads

		[DllImport("shell32.dll")] static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

		public static string DownloadsFolder()
		{
			try
			{
				if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out IntPtr p) == 0)
				{
					string s = Marshal.PtrToStringUni(p);
					Marshal.FreeCoTaskMem(p);
					if (Directory.Exists(s)) return s;
				}
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		}

		/// <summary>A file a browser is still writing (.crdownload, .part…) or a hidden/temporary one.</summary>
		public static bool IsUnfinished(string name)
		{
			if (string.IsNullOrEmpty(name) || name.StartsWith("~") || name.StartsWith(".")) return true;
			string ext = Path.GetExtension(name).ToLowerInvariant();
			return ext == ".crdownload" || ext == ".part" || ext == ".partial" || ext == ".tmp" || ext == ".download"
				|| ext == ".opdownload" || ext == ".!ut" || ext == ".!qb" || ext == ".ini";
		}

		static void WatchDownloads()
		{
			if (downloads != null) return;
			try
			{
				downloads = new FileSystemWatcher(DownloadsFolder()) { IncludeSubdirectories = false, EnableRaisingEvents = true };
				void Seen(string path) { if (!IsUnfinished(Path.GetFileName(path))) lock (pending) pending[path] = DateTime.Now; }
				downloads.Created += (s, e) => Seen(e.FullPath);
				downloads.Renamed += (s, e) => Seen(e.FullPath);
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "downloads watcher: " + ex.Message); }
		}

		/// <summary>Self-test: watch another folder (null = the real Downloads again) and check it now.</summary>
		internal static void TestWatch(string folder)
		{
			downloads?.Dispose();
			downloads = null;
			lock (pending) pending.Clear();
			if (folder == null) { if (Enabled) WatchDownloads(); return; }
			downloads = new FileSystemWatcher(folder) { IncludeSubdirectories = false, EnableRaisingEvents = true };
			void Seen(string path) { if (!IsUnfinished(Path.GetFileName(path))) lock (pending) pending[path] = DateTime.Now; }
			downloads.Created += (s, e) => Seen(e.FullPath);
			downloads.Renamed += (s, e) => Seen(e.FullPath);
		}

		internal static void TestCheckDownloads() { lastDownloadSaid = DateTime.MinValue; FinishedDownloads(); }

		/// <summary>Files that stopped growing for a few seconds are announced (several at once in one bubble).</summary>
		static void FinishedDownloads()
		{
			List<string> done;
			lock (pending)
			{
				done = pending.Where(p => (DateTime.Now - p.Value).TotalSeconds > 4).Select(p => p.Key).ToList();
				foreach (var d in done) pending.Remove(d);
			}
			done = done.Where(File.Exists).ToList();
			if (done.Count == 0 || (DateTime.Now - lastDownloadSaid).TotalSeconds < 10) return;
			lastDownloadSaid = DateTime.Now;
			Program.Mainthread?.ReactAll("cheer", "happy", "jump", "thumbs up");
			string first = done[0];
			string text = done.Count == 1 ? Lang.T("pc.downloaded", Path.GetFileName(first)) : Lang.T("pc.downloadedMany", done.Count);
			SpeechBubble.Notify(text, 10, new SpeechBubble.Choice(Lang.T("pc.showFile"), () =>
			{
				try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + first + "\"") { UseShellExecute = false }); } catch (Exception skipped) { LogFile.Ignored(skipped); }
			}, true));
		}
	}
}
