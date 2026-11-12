using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DesktopPet
{
	/// <summary>
	/// Reminders written as a sentence: "me lembra de ligar para a Ana às 15h", "daqui a 20 minutos beber água",
	/// "amanhã às 9 reunião", "todo dia às 8 tomar o remédio", "a cada 30 minutos alongar" (also in English and Spanish).
	/// Understood on this computer, without the AI. When the sentence is different and the AI is set up, the AI only
	/// rewrites it in one of these forms, and it is read again here (the AI never decides the time on its own).
	/// </summary>
	public static class SmartReminder
	{
		const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

		/// <summary>The sentence asks for a reminder ("me lembra…", "remind me…", "recuérdame…").</summary>
		public static bool LooksLikeReminder(string s) =>
			Regex.IsMatch(s ?? "", @"\b(me\s+lembr[ae]|lembre-?me|lembra-?me|me\s+avis[ae]|cria(r)?\s+(um\s+)?lembrete|lembrete\b|remind\s+me|set\s+a\s+reminder|recu[eé]rdame|recordarme|av[ií]same)", I);

		/// <summary>Read the sentence. Null when it has no time that can be understood.</summary>
		public static Reminder Parse(string sentence, DateTime now)
		{
			if (string.IsNullOrWhiteSpace(sentence)) return null;
			string s = " " + sentence.Trim().TrimEnd('.', '!', '?') + " ";
			var r = new Reminder();
			Match m;

			// every N minutes / hours
			if ((m = Regex.Match(s, @"\b(a\s+cada|every|cada)\s+(\d{1,3})\s*(min|minutos?|minutes?|h|horas?|hours?)\b", I)).Success)
			{
				int n = int.Parse(m.Groups[2].Value);
				r.Kind = Reminder.Kinds.Interval;
				r.Minutes = Math.Max(5, IsHour(m.Groups[3].Value) ? n * 60 : n);
				r.FromHour = 8; r.ToHour = 20; r.Days = 127;   // awake hours (can be changed in the reminder)
				s = Cut(s, m);
			}
			// in N minutes / hours
			else if ((m = Regex.Match(s, @"\b(em|daqui\s+a|dentro\s+de|in|en)\s+(\d{1,4}|uma?|one|an?|media|meia)\s*(min|minutos?|minutes?|h|horas?|hours?)\b", I)).Success)
			{
				string num = m.Groups[2].Value.ToLowerInvariant();
				double n = num == "meia" || num == "media" ? 0.5 : int.TryParse(num, out int k) ? k : 1;
				r.Kind = Reminder.Kinds.Once;
				r.At = now.AddMinutes(IsHour(m.Groups[3].Value) ? n * 60 : n);
				s = Cut(s, m);
			}
			else
			{
				// a time: "às 15h", "as 15:30", "at 3pm", "a las 9", "15h30"
				m = Regex.Match(s, @"(?:\b(?:[àa]s|at|a\s+las?|las)\s+)(\d{1,2})(?:(?:[:h\.])(\d{2}))?\s*(h|horas?|hrs?)?\s*(am|pm|da\s+manh[ãa]|da\s+tarde|da\s+noite|de\s+la\s+ma[ñn]ana|de\s+la\s+tarde|de\s+la\s+noche)?", I);
				if (!m.Success) m = Regex.Match(s, @"\b(\d{1,2})(?:[:h])(\d{2})?\b\s*(h)?()", I);
				if (!m.Success) return null;
				int hour = int.Parse(m.Groups[1].Value), min = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 0;
				string part = m.Groups[4].Value.ToLowerInvariant();
				if ((part == "pm" || part.Contains("tarde") || part.Contains("noite") || part.Contains("noche")) && hour < 12) hour += 12;
				if (part == "am" && hour == 12) hour = 0;
				if (hour > 23 || min > 59) return null;
				s = Cut(s, m);

				// every day at…
				var daily = Regex.Match(s, @"\b(todo\s+dia|todos\s+os\s+dias|every\s+day|daily|cada\s+d[ií]a|todos\s+los\s+d[ií]as)\b", I);
				if (daily.Success)
				{
					r.Kind = Reminder.Kinds.Daily;
					r.Time = $"{hour:00}:{min:00}";
					r.Days = 127;
					s = Cut(s, daily);
				}
				else
				{
					var day = now.Date;
					var tomorrow = Regex.Match(s, @"\b(amanh[ãa]|tomorrow|ma[ñn]ana)\b", I);
					if (tomorrow.Success) { day = day.AddDays(1); s = Cut(s, tomorrow); }
					var today = Regex.Match(s, @"\b(hoje|today|hoy)\b", I);
					if (today.Success) s = Cut(s, today);
					var at = day.AddHours(hour).AddMinutes(min);
					// "às 3" said at 14h means 15h; a time already gone today is tomorrow
					if (!tomorrow.Success && at <= now && hour < 12 && part.Length == 0 && day.AddHours(hour + 12).AddMinutes(min) > now) at = at.AddHours(12);
					else if (!tomorrow.Success && at <= now) at = at.AddDays(1);
					r.Kind = Reminder.Kinds.Once;
					r.At = at;
				}
			}

			r.Text = What(s);
			if (r.Text.Length == 0) r.Text = Lang.T("rem.smartDefault");
			return r;
		}

		static bool IsHour(string unit) => unit.StartsWith("h", StringComparison.OrdinalIgnoreCase);

		static string Cut(string s, Match m) => s.Substring(0, m.Index) + " " + s.Substring(m.Index + m.Length);

		/// <summary>What to remind: the sentence without "remind me", the time and the little linking words.</summary>
		static string What(string s)
		{
			s = Regex.Replace(s, @"\b(por\s+favor|please|por\s+favor,?)\b", " ", I);
			s = Regex.Replace(s, @"\b(pet|bichinho)\b,?", " ", I);
			s = Regex.Replace(s, @"\b(voc[eê]\s+pode\s+|pode\s+|can\s+you\s+|could\s+you\s+|puedes\s+)", " ", I);
			s = Regex.Replace(s, @"\b(me\s+lembr[ae]r?|lembre-?me|lembra-?me|me\s+avis[ae]r?|cria(r)?\s+(um\s+)?lembrete|lembrete|remind\s+me|set\s+a\s+reminder|recu[eé]rdame|recordarme|av[ií]same)\b", " ", I);
			s = Regex.Replace(s, @"\s+", " ").Trim(' ', ',', '.', ':', ';', '-');
			// leading linking words: "de", "que", "para", "to", "of", "que", "de que"
			for (int i = 0; i < 3; i++)
				s = Regex.Replace(s, @"^(de\s+que|de|que|para|pra|to|about|of|a|con|del?)\s+", "", I).Trim();
			// trailing ones left by the time ("… para", "… de")
			s = Regex.Replace(s, @"\s+(de|para|pra|to|at|em|on|el|la)$", "", I).Trim();
			if (s.Length > 0) s = char.ToUpper(s[0], CultureInfo.CurrentCulture) + s.Substring(1);
			return s;
		}

		/// <summary>When the reminder will be said, for the confirmation ("hoje às 15:00", "a cada 30 min").</summary>
		public static string When(Reminder r, DateTime now)
		{
			switch (r.Kind)
			{
				case Reminder.Kinds.Interval: return Lang.T("rem.smartEvery", r.Minutes);
				case Reminder.Kinds.Daily: return Lang.T("rem.smartDaily", r.Time);
				default:
					string time = r.At.ToString("HH:mm");
					return r.At.Date == now.Date ? Lang.T("rem.today", time)
						: r.At.Date == now.Date.AddDays(1) ? Lang.T("rem.tomorrow", time)
						: r.At.ToString("g", CultureInfo.CurrentCulture);
			}
		}

		/// <summary>
		/// Read it here; if that fails and the AI is set up, ask the AI to rewrite it in a simple form and read that.
		/// </summary>
		public static async Task<Reminder> Understand(string sentence)
		{
			var r = Parse(sentence, DateTime.Now);
			if (r != null || !AiAssistant.Configured) return r;
			try
			{
				bool pt = Lang.Current.StartsWith("pt", StringComparison.OrdinalIgnoreCase), es = Lang.Current.StartsWith("es", StringComparison.OrdinalIgnoreCase);
				string ask = (pt
					? "Reescreva este pedido de lembrete usando EXATAMENTE uma destas formas, e responda só com a frase: \"<tarefa> às HH:MM\", \"<tarefa> amanhã às HH:MM\", \"<tarefa> daqui a N minutos\", \"<tarefa> todo dia às HH:MM\" ou \"<tarefa> a cada N minutos\". Agora são "
					: es
					? "Reescribe esta petición de recordatorio usando EXACTAMENTE una de estas formas, y responde solo con la frase: \"<tarea> a las HH:MM\", \"<tarea> mañana a las HH:MM\", \"<tarea> en N minutos\", \"<tarea> todos los días a las HH:MM\" o \"<tarea> cada N minutos\". Ahora son las "
					: "Rewrite this reminder request using EXACTLY one of these forms, and answer only with the sentence: \"<task> at HH:MM\", \"<task> tomorrow at HH:MM\", \"<task> in N minutes\", \"<task> every day at HH:MM\" or \"<task> every N minutes\". It is now ")
					+ DateTime.Now.ToString("HH:mm") + ". " + sentence;
				string simple = await AiAssistant.Ask(AiAssistant.BaseUrl, AiAssistant.Model, AiAssistant.Key, ask, "Pet", 60);
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.info, "smart reminder rewritten by the AI: " + simple);
				return Parse(simple.Trim().Trim('"'), DateTime.Now);
			}
			catch (Exception ex)
			{
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "smart reminder: " + ex.Message);
				return null;
			}
		}

		/// <summary>Create it and say so in the bubble, with a way to undo.</summary>
		public static void AddAndConfirm(Reminder r)
		{
			Reminders.Add(r);
			Program.Mainthread?.ReactAll("happy", "wave", "nod");
			SpeechBubble.Say(Lang.T("rem.smartOk", r.Text, When(r, DateTime.Now)), 10,
				new SpeechBubble.Choice("OK", null, true),
				new SpeechBubble.Choice(Lang.T("rem.smartUndo"), () => { Reminders.Remove(r); SpeechBubble.Say(Lang.T("rem.smartUndone"), 4); }));
		}

		/// <summary>Menu "Reminder from a sentence…": type it, the pet understands it.</summary>
		public static async void AskAndAdd()
		{
			string text = FormLaunchers.Prompt(Lang.T("rem.smartAsk"), "");
			if (string.IsNullOrWhiteSpace(text)) return;
			var r = await Understand(text);
			if (r == null) SpeechBubble.Say(Lang.T("rem.smartFail"), 10);
			else AddAndConfirm(r);
		}
	}
}
