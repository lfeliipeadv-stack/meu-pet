using System;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Focus timer (Pomodoro): focus N minutes, short break, and a long break every 4 focus blocks.
	/// The pet announces each change in a bubble. Durations are set in Settings → Extras.
	/// </summary>
	public static class Pomodoro
	{
		public enum Phase { Off, Focus, ShortBreak, LongBreak }

		public static Phase Current { get; private set; } = Phase.Off;
		public static DateTime EndsAt { get; private set; }
		public static int Completed { get; private set; }
		public static bool Paused { get; private set; }
		static TimeSpan pausedLeft;
		static Timer timer;

		public static event Action Changed;

		public static int FocusMinutes => Program.MyData.GetSettingInt("PomodoroFocus", 25);
		public static int BreakMinutes => Program.MyData.GetSettingInt("PomodoroBreak", 5);
		public static int LongBreakMinutes => Program.MyData.GetSettingInt("PomodoroLong", 15);

		public static TimeSpan Left => Paused ? pausedLeft : (Current == Phase.Off ? TimeSpan.Zero : EndsAt - DateTime.Now);

		public static void StartFocus()
		{
			Begin(Phase.Focus, FocusMinutes);
			SpeechBubble.Say(Lang.T("pomo.startFocus", FocusMinutes), 6);
		}

		public static void Stop()
		{
			if (Current == Phase.Off) return;
			Current = Phase.Off;
			Paused = false;
			Completed = 0;
			timer?.Stop();
			SpeechBubble.Say(Lang.T("pomo.stopped"), 5);
			Changed?.Invoke();
		}

		public static void TogglePause()
		{
			if (Current == Phase.Off) return;
			if (Paused) { EndsAt = DateTime.Now + pausedLeft; Paused = false; }
			else { pausedLeft = EndsAt - DateTime.Now; Paused = true; }
			Changed?.Invoke();
		}

		/// <summary>"Focus: 12:34 left" for menus and bubbles.</summary>
		public static string Status()
		{
			if (Current == Phase.Off) return Lang.T("pomo.off");
			var l = Left; if (l < TimeSpan.Zero) l = TimeSpan.Zero;
			string key = Current == Phase.Focus ? "pomo.statusFocus" : "pomo.statusBreak";
			return Lang.T(key, $"{(int)l.TotalMinutes:00}:{l.Seconds:00}") + (Paused ? " " + Lang.T("pomo.paused") : "");
		}

		static void Begin(Phase phase, int minutes)
		{
			Current = phase;
			Paused = false;
			EndsAt = DateTime.Now.AddMinutes(Math.Max(1, minutes));
			if (timer == null)
			{
				timer = new Timer { Interval = 1000 };
				timer.Tick += (s, e) => Tick();
			}
			timer.Start();
			Changed?.Invoke();
		}

		static void Tick()
		{
			if (Current == Phase.Off || Paused || DateTime.Now < EndsAt) return;
			if (Current == Phase.Focus)
			{
				Completed++;
				bool longBreak = Completed % 4 == 0;
				Begin(longBreak ? Phase.LongBreak : Phase.ShortBreak, longBreak ? LongBreakMinutes : BreakMinutes);
				SpeechBubble.Say(Lang.T(longBreak ? "pomo.longBreak" : "pomo.break", longBreak ? LongBreakMinutes : BreakMinutes), 0,
					new SpeechBubble.Choice(Lang.T("pomo.ok"), null, true),
					new SpeechBubble.Choice(Lang.T("pomo.skipBreak"), StartFocus));
				Program.Mainthread?.ReactAll("cheer", "happy", "dance", "jump");
			}
			else
			{
				Begin(Phase.Focus, FocusMinutes);
				SpeechBubble.Say(Lang.T("pomo.backToFocus", FocusMinutes), 0,
					new SpeechBubble.Choice(Lang.T("pomo.ok"), null, true),
					new SpeechBubble.Choice(Lang.T("pomo.stop"), Stop));
			}
		}
	}
}
