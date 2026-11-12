using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>A reminder the pet says in a speech bubble.</summary>
	public sealed class Reminder
	{
		public enum Kinds { Interval, Daily, Once }

		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		public string Text { get; set; } = "";
		public bool Enabled { get; set; } = true;
		public Kinds Kind { get; set; } = Kinds.Interval;

		/// <summary>Interval: every N minutes, only between FromHour and ToHour (ToHour excluded), on the chosen days.</summary>
		public int Minutes { get; set; } = 60;
		public int FromHour { get; set; } = 9;
		public int ToHour { get; set; } = 18;

		/// <summary>Daily: time of the day ("HH:mm") on the chosen days.</summary>
		public string Time { get; set; } = "09:00";

		/// <summary>Days of the week, bit 0 = Sunday … bit 6 = Saturday. 127 = every day.</summary>
		public int Days { get; set; } = 127;

		/// <summary>Once: date and time.</summary>
		public DateTime At { get; set; } = DateTime.Now.AddMinutes(10);

		public DateTime LastFired { get; set; } = DateTime.MinValue;
		public DateTime SnoozedUntil { get; set; } = DateTime.MinValue;

		public bool OnDay(DateTime t) => (Days & (1 << (int)t.DayOfWeek)) != 0;

		/// <summary>Inside FromHour..ToHour (a range like 22..6 goes over midnight; equal hours = all day).</summary>
		bool InHours(DateTime t) => FromHour == ToHour || (FromHour < ToHour ? t.Hour >= FromHour && t.Hour < ToHour : t.Hour >= FromHour || t.Hour < ToHour);

		TimeSpan TimeOfDay => TimeSpan.TryParseExact(Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out var ts) ? ts : new TimeSpan(9, 0, 0);

		/// <summary>Next moment this reminder is due, after <paramref name="since"/> (null = never).</summary>
		public DateTime? Next(DateTime since)
		{
			if (!Enabled) return null;
			if (SnoozedUntil > since) return SnoozedUntil;
			switch (Kind)
			{
				case Kinds.Once:
					return LastFired >= At ? (DateTime?)null : At;
				case Kinds.Daily:
					for (int d = 0; d < 8; d++)
					{
						var t = since.Date.AddDays(d) + TimeOfDay;
						if (t > since && t > LastFired && OnDay(t)) return t;
					}
					return null;
				default:
					var next = since.AddMinutes(Math.Max(1, Minutes));
					// outside the active hours or days: the next full hour that is inside
					for (int i = 0; i < 8 * 24; i++)
					{
						if (InHours(next) && OnDay(next)) return next;
						next = next.Date.AddHours(next.Hour + 1);
					}
					return null;
			}
		}

		/// <summary>"Every 60 min, 9h–18h, Mon–Fri" in the current language.</summary>
		public string Summary()
		{
			string days = DaysText();
			switch (Kind)
			{
				case Kinds.Once: return Lang.T("rem.sumOnce", At.ToString("g", CultureInfo.CurrentCulture));
				case Kinds.Daily: return Lang.T("rem.sumDaily", Time, days);
				default: return Lang.T("rem.sumInterval", Minutes, FromHour, ToHour, days);
			}
		}

		string DaysText()
		{
			if (Days == 127) return Lang.T("rem.everyDay");
			if (Days == 62) return Lang.T("rem.weekdays");
			if (Days == 65) return Lang.T("rem.weekend");
			var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
			return string.Join(", ", Enumerable.Range(0, 7).Where(i => (Days & (1 << i)) != 0).Select(i => names[i]));
		}
	}

	/// <summary>
	/// The reminders: kept in %LocalAppData%\DesktopPet\reminders.json and checked every 15 seconds.
	/// When one is due, the pet says it in a bubble with "Done" and "Snooze 10 min".
	/// </summary>
	public static class Reminders
	{
		internal static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "reminders.json");
		static List<Reminder> list;
		internal static void Reload() { list = null; Changed?.Invoke(); }
		static Timer timer;
		static readonly DateTime started = DateTime.Now;

		/// <summary>The list changed (the settings page follows it).</summary>
		public static event Action Changed;

		public static List<Reminder> All
		{
			get
			{
				if (list == null) Load();
				return list;
			}
		}

		static void Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					list = JsonText.Deserialize<List<Reminder>>(File.ReadAllText(FilePath)) ?? new List<Reminder>();
					return;
				}
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "reminders: " + ex.Message); }
			// first run: ready-made ideas, turned off (the user picks what they want)
			list = new List<Reminder>
			{
				new Reminder { Text = Lang.T("rem.presetWater"), Minutes = 60, FromHour = 9, ToHour = 18, Days = 62, Enabled = false },
				new Reminder { Text = Lang.T("rem.presetStretch"), Minutes = 90, FromHour = 9, ToHour = 18, Days = 62, Enabled = false },
				new Reminder { Text = Lang.T("rem.presetEyes"), Minutes = 30, FromHour = 9, ToHour = 18, Days = 62, Enabled = false },
				new Reminder { Text = Lang.T("rem.presetLunch"), Kind = Reminder.Kinds.Daily, Time = "12:00", Days = 62, Enabled = false },
			};
		}

		public static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				// once-reminders that already happened are cleaned up after a day
				All.RemoveAll(r => r.Kind == Reminder.Kinds.Once && r.LastFired >= r.At && r.LastFired < DateTime.Now.AddDays(-1));
				File.WriteAllText(FilePath, JsonText.SerializeIndented(All));
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "reminders: " + ex.Message); }
			Changed?.Invoke();
		}

		public static void Add(Reminder r) { All.Add(r); Save(); }

		public static void Remove(Reminder r) { All.Remove(r); Save(); }

		/// <summary>Start checking (once, from the UI thread).</summary>
		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 15000 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		/// <summary>"Remind me in N minutes" (tray menu).</summary>
		public static void In(int minutes, string text = null)
		{
			Add(new Reminder
			{
				Kind = Reminder.Kinds.Once,
				At = DateTime.Now.AddMinutes(minutes),
				Text = string.IsNullOrWhiteSpace(text) ? Lang.T("rem.timerDone", minutes) : text,
			});
			SpeechBubble.Say(Lang.T("rem.timerSet", minutes), 5);
		}

		static void Check()
		{
			var now = DateTime.Now;
			bool changed = false;
			foreach (var r in All.ToList())
			{
				// what was due while the program was closed is not said at start (except a one-time reminder)
				var from = r.Kind == Reminder.Kinds.Once ? DateTime.MinValue
					: r.LastFired > started.AddDays(-1) ? r.LastFired : started;
				var due = r.Next(from);
				if (due == null || due > now) continue;
				r.LastFired = now;
				r.SnoozedUntil = DateTime.MinValue;
				if (r.Kind == Reminder.Kinds.Once) r.Enabled = false;
				changed = true;
				Fire(r, true);
			}
			if (changed) Save();
		}

		/// <summary>Say the reminder now (also the "Test" button of the settings page).</summary>
		public static void Fire(Reminder r) => Fire(r, false);

		/// <summary>Say the reminder; when it came by itself (automatic) it waits during quiet time.</summary>
		public static void Fire(Reminder r, bool automatic)
		{
			var choices = new[]
			{
				new SpeechBubble.Choice(Lang.T("rem.done"), null, true),
				new SpeechBubble.Choice(Lang.T("rem.snooze"), () =>
				{
					r.SnoozedUntil = DateTime.Now.AddMinutes(10);
					if (r.Kind == Reminder.Kinds.Once) { r.Enabled = true; r.At = r.SnoozedUntil; }
					Save();
				}),
			};
			if (automatic) SpeechBubble.Notify(r.Text, 0, choices);
			else SpeechBubble.Say(r.Text, 0, choices);
		}
	}
}
