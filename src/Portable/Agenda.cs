using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>One meeting.</summary>
	public sealed class AgendaEvent
	{
		public string Uid, Title;
		public DateTime Start;   // local time
	}

	/// <summary>
	/// "Your next meeting is in 10 minutes": reads the private iCal link of a Google or Outlook calendar
	/// (no sign-in needed), every 15 minutes, and the pet tells you before each meeting.
	/// Supports the usual cases of those calendars: time zones, all-day events (ignored), daily/weekly repeats,
	/// removed and moved occurrences, cancelled events.
	/// </summary>
	public static class Agenda
	{
		static Timer timer;
		static List<AgendaEvent> events = new List<AgendaEvent>();
		static DateTime lastFetch = DateTime.MinValue;
		static readonly HashSet<string> told = new HashSet<string>();

		public static string Url
		{
			get => Program.MyData.GetSetting("AgendaUrl");
			set { Program.MyData.SetSetting("AgendaUrl", value ?? ""); events.Clear(); lastFetch = DateTime.MinValue; }
		}

		public static int LeadMinutes
		{
			get => Program.MyData.GetSettingInt("AgendaLead", 10);
			set => Program.MyData.SetSetting("AgendaLead", value);
		}

		public static bool Configured => Url.Length > 0;

		/// <summary>The next meeting today or tomorrow (for the pet menu), or null.</summary>
		public static AgendaEvent Next => events.Where(e => e.Start > DateTime.Now).OrderBy(e => e.Start).FirstOrDefault();

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 30000 };
			timer.Tick += async (s, e) => await Tick();
			timer.Start();
		}

		static async Task Tick()
		{
			if (!Configured) return;
			if ((DateTime.Now - lastFetch).TotalMinutes >= 15)
			{
				lastFetch = DateTime.Now;
				try { events = await Load(Url); }
				catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "agenda: " + ex.Message); }
			}
			var now = DateTime.Now;
			foreach (var e in events)
			{
				var before = e.Start - now;
				string key = e.Uid + "|" + e.Start.ToString("o");
				if (before.TotalMinutes <= LeadMinutes && before.TotalMinutes > -1 && told.Add(key))
				{
					int min = Math.Max(0, (int)Math.Round(before.TotalMinutes));
					Program.Mainthread?.ReactAll("wave", "look around", "think");
					SpeechBubble.Notify(min == 0 ? Lang.T("agenda.now", e.Title) : Lang.T("agenda.soon", min, e.Title, e.Start.ToString("t", CultureInfo.CurrentCulture)), 0,
						new SpeechBubble.Choice("OK", null, true));
				}
			}
		}

		/// <summary>Download and read the calendar: events of the next 24 hours.</summary>
		public static async Task<List<AgendaEvent>> Load(string url)
		{
			url = (url ?? "").Trim();
			if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Substring(9);
			if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(Lang.T("agenda.httpsOnly"));
			using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
			{
				http.DefaultRequestHeaders.Add("User-Agent", "DesktopPet");
				string ics = await http.GetStringAsync(url);
				if (!ics.Contains("BEGIN:VCALENDAR")) throw new FormatException(Lang.T("agenda.notCalendar"));
				return Parse(ics, DateTime.Now, DateTime.Now.AddHours(24));
			}
		}

		// ------------------------------------------------------------------ iCalendar (RFC 5545), the parts calendars use

		sealed class Raw
		{
			public string Uid = "", Summary = "", Status = "", Rrule = "", RecurrenceId = "", DtStart = "", DtStartTz = "";
			public bool AllDay;
			public List<(string value, string tz)> ExDates = new List<(string, string)>();
		}

		/// <summary>Events starting between <paramref name="from"/> and <paramref name="to"/> (local time), sorted.</summary>
		public static List<AgendaEvent> Parse(string ics, DateTime from, DateTime to)
		{
			// unfold: a line starting with a space or tab continues the previous one
			var lines = new List<string>();
			foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
			{
				if ((raw.StartsWith(" ") || raw.StartsWith("\t")) && lines.Count > 0) lines[lines.Count - 1] += raw.Substring(1);
				else lines.Add(raw.TrimEnd('\r'));
			}

			var all = new List<Raw>();
			Raw cur = null;
			foreach (var line in lines)
			{
				if (line == "BEGIN:VEVENT") { cur = new Raw(); continue; }
				if (line == "END:VEVENT") { if (cur != null) all.Add(cur); cur = null; continue; }
				if (cur == null) continue;
				int colon = line.IndexOf(':');
				if (colon < 0) continue;
				string head = line.Substring(0, colon), value = line.Substring(colon + 1);
				string name = head.Split(';')[0].ToUpperInvariant();
				string tz = Param(head, "TZID");
				bool date = Param(head, "VALUE").Equals("DATE", StringComparison.OrdinalIgnoreCase) || (value.Length == 8 && !value.Contains("T"));
				switch (name)
				{
					case "UID": cur.Uid = value; break;
					case "SUMMARY": cur.Summary = Unescape(value); break;
					case "STATUS": cur.Status = value.ToUpperInvariant(); break;
					case "RRULE": cur.Rrule = value; break;
					case "RECURRENCE-ID": cur.RecurrenceId = ToLocal(value, tz)?.Ticks.ToString(CultureInfo.InvariantCulture) ?? ""; break;
					case "DTSTART": cur.DtStart = value; cur.DtStartTz = tz; cur.AllDay = date; break;
					case "EXDATE": foreach (var v in value.Split(',')) cur.ExDates.Add((v, tz)); break;
				}
			}

			// moved or changed occurrences replace the original one
			var moved = new HashSet<string>(all.Where(e => e.RecurrenceId.Length > 0).Select(e => e.Uid + "|" + e.RecurrenceId));
			var result = new List<AgendaEvent>();
			foreach (var e in all)
			{
				if (e.AllDay || e.Status == "CANCELLED") continue;
				var start = ToLocal(e.DtStart, e.DtStartTz);
				if (start == null) continue;
				string title = string.IsNullOrWhiteSpace(e.Summary) ? Lang.T("agenda.untitled") : e.Summary;
				if (e.Rrule.Length == 0 || e.RecurrenceId.Length > 0)
				{
					if (start >= from && start <= to) result.Add(new AgendaEvent { Uid = e.Uid, Title = title, Start = start.Value });
					continue;
				}
				var skip = new HashSet<DateTime>(e.ExDates.Select(x => ToLocal(x.value, string.IsNullOrEmpty(x.tz) ? e.DtStartTz : x.tz)).Where(x => x != null).Select(x => x.Value));
				foreach (var t in Occurrences(start.Value, e.Rrule, from, to))
				{
					if (skip.Contains(t) || moved.Contains(e.Uid + "|" + t.Ticks.ToString(CultureInfo.InvariantCulture))) continue;
					result.Add(new AgendaEvent { Uid = e.Uid, Title = title, Start = t });
				}
			}
			return result.OrderBy(r => r.Start).ToList();
		}

		/// <summary>DAILY / WEEKLY (with BYDAY) / MONTHLY (same day) repeats, with INTERVAL, COUNT and UNTIL.</summary>
		static IEnumerable<DateTime> Occurrences(DateTime start, string rrule, DateTime from, DateTime to)
		{
			var p = rrule.Split(';').Select(x => x.Split('=')).Where(x => x.Length == 2).ToDictionary(x => x[0].ToUpperInvariant(), x => x[1]);
			string freq = p.TryGetValue("FREQ", out var f) ? f.ToUpperInvariant() : "";
			int interval = p.TryGetValue("INTERVAL", out var iv) && int.TryParse(iv, out int n) && n > 0 ? n : 1;
			int count = p.TryGetValue("COUNT", out var c) && int.TryParse(c, out int k) ? k : int.MaxValue;
			DateTime until = p.TryGetValue("UNTIL", out var u) ? (ToLocal(u, null) ?? DateTime.MaxValue) : DateTime.MaxValue;
			var days = p.TryGetValue("BYDAY", out var bd)
				? bd.Split(',').Select(d => d.Length >= 2 ? d.Substring(d.Length - 2) : d).Select(DayOf).Where(d => d != null).Select(d => d.Value).ToList()
				: new List<DayOfWeek>();
			int made = 0;
			if (freq == "DAILY")
			{
				for (var t = start; t <= to && t <= until && made < count; t = t.AddDays(interval))
				{
					made++;
					if (t >= from) yield return t;
				}
			}
			else if (freq == "WEEKLY")
			{
				if (days.Count == 0) days.Add(start.DayOfWeek);
				// weeks counted from the week of the first event (weeks start on Monday, the iCalendar default)
				var weekStart = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7));
				for (var w = weekStart; w <= to && made < count; w = w.AddDays(7 * interval))
				{
					foreach (var d in days.OrderBy(d => ((int)d + 6) % 7))
					{
						var t = w.AddDays(((int)d + 6) % 7) + start.TimeOfDay;
						if (t < start) continue;
						if (t > until || made >= count) yield break;
						made++;
						if (t >= from && t <= to) yield return t;
					}
				}
			}
			else if (freq == "MONTHLY")
			{
				for (var t = start; t <= to && t <= until && made < count; t = t.AddMonths(interval))
				{
					made++;
					if (t >= from) yield return t;
				}
			}
			else if (start >= from && start <= to) yield return start;   // unknown repeat: at least the first one
		}

		static DayOfWeek? DayOf(string two)
		{
			switch (two.ToUpperInvariant())
			{
				case "MO": return DayOfWeek.Monday; case "TU": return DayOfWeek.Tuesday; case "WE": return DayOfWeek.Wednesday;
				case "TH": return DayOfWeek.Thursday; case "FR": return DayOfWeek.Friday; case "SA": return DayOfWeek.Saturday;
				case "SU": return DayOfWeek.Sunday; default: return null;
			}
		}

		static string Param(string head, string name)
		{
			foreach (var part in head.Split(';').Skip(1))
			{
				int eq = part.IndexOf('=');
				if (eq > 0 && part.Substring(0, eq).Equals(name, StringComparison.OrdinalIgnoreCase)) return part.Substring(eq + 1).Trim('"');
			}
			return "";
		}

		/// <summary>"20261112T140000Z" (UTC), "20261112T140000" with a time zone (Windows or IANA name), or floating (local).</summary>
		public static DateTime? ToLocal(string value, string tz)
		{
			value = (value ?? "").Trim();
			bool utc = value.EndsWith("Z");
			string v = value.TrimEnd('Z');
			if (!DateTime.TryParseExact(v, new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm", "yyyyMMdd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
			if (utc) return DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime();
			if (!string.IsNullOrEmpty(tz))
			{
				try
				{
					var zone = TimeZoneInfo.FindSystemTimeZoneById(tz);
					return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), zone, TimeZoneInfo.Local);
				}
				catch (Exception skipped) { LogFile.Ignored(skipped); }
			}
			return t;
		}

		static string Unescape(string s) => s.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
	}
}
