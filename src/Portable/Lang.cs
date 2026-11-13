using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DesktopPet
{
	/// <summary>
	/// Very small translation system.
	/// <para>
	/// Texts live in plain JSON files in the <c>lang</c> folder next to the executable
	/// (<c>lang\en.json</c>, <c>lang\pt-BR.json</c>, …), so anybody can fix a text or add a language
	/// without recompiling the application. English is also built in, so the app still works
	/// if the folder is missing.
	/// </para>
	/// </summary>
	public static class Lang
	{
		private static Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
		private static Dictionary<string, string> fallback = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Code of the language in use ("en", "pt-BR", …).</summary>
		public static string Current { get; private set; } = "en";

		/// <summary>Raised after <see cref="Use"/> changed the language.</summary>
		public static event Action Changed;

		/// <summary>Folder holding the language files.</summary>
		public static string Folder => Path.Combine(AppContext.BaseDirectory, "lang");

		/// <summary>A language the user can pick: file code plus the name to show.</summary>
		public sealed class Entry
		{
			public string Code { get; set; }
			public string Name { get; set; }
			public override string ToString() => Name;
		}

		static Lang()
		{
			fallback = BuiltInEnglish();
			strings = fallback;
			// Until the saved preference is read, follow the Windows language.
			try { Use("auto"); } catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		/// <summary>
		/// Developer helper: write the built-in English texts as a JSON template for translators.
		/// </summary>
		public static void ExportEnglish(string path)
		{
			File.WriteAllText(path, JsonText.SerializeIndented(BuiltInEnglish()), new System.Text.UTF8Encoding(false));
		}

		/// <summary>Every key the application uses (to check translation files).</summary>
		public static IEnumerable<string> Keys => fallback.Keys;

		/// <summary>The built-in English text of a key (self-test: compare translations).</summary>
		public static string English(string key) => fallback.TryGetValue(key, out var v) ? v : key;

		/// <summary>
		/// Languages available: the built-in English plus every <c>lang\*.json</c> file found.
		/// </summary>
		public static List<Entry> Available()
		{
			var list = new List<Entry> { new Entry { Code = "en", Name = "English" } };
			try
			{
				if (Directory.Exists(Folder))
				{
					foreach (var file in Directory.GetFiles(Folder, "*.json"))
					{
						string code = Path.GetFileNameWithoutExtension(file);
						if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)) continue;
						string name = code;
						try
						{
							var dict = Read(file);
							if (dict != null && dict.TryGetValue("language.name", out string n) && !string.IsNullOrWhiteSpace(n)) name = n;
						}
						catch (Exception skipped) { LogFile.Ignored(skipped); }
						list.Add(new Entry { Code = code, Name = name });
					}
				}
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			// languages inside the exe that the folder does not have
			foreach (var code in EmbeddedCodes())
			{
				if (list.Any(e => e.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) continue;
				string name = code;
				try { var d = Read(EmbeddedPrefix + code); if (d != null && d.TryGetValue("language.name", out string n) && !string.IsNullOrWhiteSpace(n)) name = n; } catch (Exception skipped) { LogFile.Ignored(skipped); }
				list.Add(new Entry { Code = code, Name = name });
			}
			return list;
		}

		/// <summary>
		/// Load a language. Pass an empty string (or "auto") to follow the Windows display language.
		/// Unknown codes fall back to English.
		/// </summary>
		public static void Use(string code)
		{
			if (string.IsNullOrWhiteSpace(code) || code.Equals("auto", StringComparison.OrdinalIgnoreCase))
				code = MatchSystemLanguage();

			var merged = new Dictionary<string, string>(fallback, StringComparer.Ordinal);
			string file = FindFile(code, out string resolved);
			if (file != null)
			{
				try
				{
					var dict = Read(file);
					if (dict != null)
						foreach (var kv in dict)
							if (kv.Value != null) merged[kv.Key] = kv.Value;
					code = resolved;
				}
				catch (Exception)
				{
					// A broken file must never stop the application: keep English.
					merged = new Dictionary<string, string>(fallback, StringComparer.Ordinal);
					code = "en";
				}
			}
			else if (!code.Equals("en", StringComparison.OrdinalIgnoreCase))
			{
				code = "en";
			}

			strings = merged;
			Current = code;
			Changed?.Invoke();
		}

		/// <summary>Find lang\{code}.json, or lang\{language}.json ("pt-BR" → "pt") as a second choice.</summary>
		private static string FindFile(string code, out string resolved)
		{
			resolved = code;
			try
			{
				string file = Path.Combine(Folder, code + ".json");
				if (File.Exists(file)) return file;
				string shortCode = code.Split('-')[0];
				file = Path.Combine(Folder, shortCode + ".json");
				if (File.Exists(file)) { resolved = shortCode; return file; }
				// no lang folder (only the exe was downloaded): the copy inside the exe
				var embedded = EmbeddedCodes();
				string match = embedded.FirstOrDefault(c => c.Equals(code, StringComparison.OrdinalIgnoreCase))
					?? embedded.FirstOrDefault(c => c.Split('-')[0].Equals(shortCode, StringComparison.OrdinalIgnoreCase));
				if (match != null) { resolved = match; return EmbeddedPrefix + match; }
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			return null;
		}

		/// <summary>Pick the language file that best matches the Windows display language.</summary>
		private static string MatchSystemLanguage()
		{
			try
			{
				var culture = CultureInfo.CurrentUICulture;
				var codes = Available().Select(a => a.Code).ToList();
				var exact = codes.FirstOrDefault(c => string.Equals(c, culture.Name, StringComparison.OrdinalIgnoreCase));
				if (exact != null) return exact;
				var neutral = codes.FirstOrDefault(c => string.Equals(c.Split('-')[0], culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));
				if (neutral != null) return neutral;
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			return "en";
		}

		private static Dictionary<string, string> Read(string file)
		{
			// The file is data, never code: a bad or hostile file can only give us strings.
			if (file.StartsWith(EmbeddedPrefix, StringComparison.Ordinal))
				using (var s = typeof(Lang).Assembly.GetManifestResourceStream("lang/" + file.Substring(EmbeddedPrefix.Length) + ".json"))
				using (var r = new StreamReader(s, System.Text.Encoding.UTF8))
					return JsonText.Deserialize<Dictionary<string, string>>(r.ReadToEnd());
			return JsonText.Deserialize<Dictionary<string, string>>(File.ReadAllText(file, System.Text.Encoding.UTF8));
		}

		/// <summary>Languages inside the exe ("embedded:pt-BR"): used when the lang folder does not have them.</summary>
		private const string EmbeddedPrefix = "embedded:";

		private static List<string> EmbeddedCodes() => typeof(Lang).Assembly.GetManifestResourceNames()
			.Where(n => n.StartsWith("lang/") && n.EndsWith(".json")).Select(n => n.Substring(5, n.Length - 10)).ToList();

		/// <summary>Translated text for a key. Unknown keys return the key itself (visible, easy to spot).</summary>
		public static string T(string key)
		{
			return strings.TryGetValue(key, out string value) ? value : key;
		}

		/// <summary>Translated text with <c>{0}</c>, <c>{1}</c>… placeholders filled in.</summary>
		public static string T(string key, params object[] args)
		{
			string text = T(key);
			try { return string.Format(CultureInfo.CurrentCulture, text, args); }
			catch (FormatException) { return text; }
		}

		/// <summary>
		/// English texts, built into the application so it never shows raw keys,
		/// and used as the base every other language overrides.
		/// </summary>
		private static Dictionary<string, string> BuiltInEnglish()
		{
			return new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["language.name"] = "English",

				// ---- tray menu
				["tray.add"] = "&Add new {0}",
				["tray.addDefault"] = "&Add new pet",
				["tray.options"] = "&Options",
				["tray.panel"] = "Open &Panel",
				["tray.install"] = "&Install application...",
				["tray.repair"] = "Repair/&Uninstall...",
				["tray.sound"] = "&Play FX",
				["tray.about"] = "A&bout",
				["tray.help"] = "&Help",
				["tray.exit"] = "&Remove {0} and Close",
				["tray.exitDefault"] = "&Remove pet and Close",
				["tray.tooltip"] = "{0} Desktop Pet",

				// ---- settings window
				["win.title"] = "desktopPet",
				["nav.pets"] = "Pets",
				["nav.behaviour"] = "Behaviour",
				["nav.application"] = "Settings",
				["nav.about"] = "About",
				["nav.reminders"] = "Reminders",
				["page.reminders.title"] = "Reminders",
				["page.reminders.subtitle"] = "Your pet reminds you to drink water, stretch, take a break… in a speech bubble.",
				["tray.reminders"] = "&Reminders",
				["tray.remindIn"] = "Remind me in {0} min",
				["tray.manageReminders"] = "Manage reminders…",
				["bubble.hello"] = "Hi! I'm {0}. Nice to meet you!",
				["rem.new"] = "+ New reminder",
				["rem.editTitle"] = "Edit reminder",
				["rem.quick"] = "Remind me in:",
				["rem.minutesShort"] = "{0} min",
				["rem.empty"] = "No reminders yet. Create one with \"New reminder\".",
				["rem.test"] = "Test",
				["rem.edit"] = "Edit",
				["rem.delete"] = "Delete",
				["rem.deleteConfirm"] = "Delete the reminder \"{0}\"?",
				["rem.saved"] = "Reminder saved.",
				["rem.next"] = "next: {0}",
				["rem.today"] = "today {0}",
				["rem.tomorrow"] = "tomorrow {0}",
				["rem.sumInterval"] = "Every {0} min, {1}h–{2}h, {3}",
				["rem.sumDaily"] = "Every day at {0}, {1}",
				["rem.sumOnce"] = "Once, {0}",
				["rem.everyDay"] = "every day",
				["rem.weekdays"] = "Mon–Fri",
				["rem.weekend"] = "weekends",
				["rem.done"] = "Done",
				["rem.snooze"] = "In 10 min",
				["rem.timerDone"] = "Time's up! {0} minutes have passed.",
				["rem.timerSet"] = "OK! I'll let you know in {0} minutes.",
				["rem.presetWater"] = "Time to drink some water! 💧",
				["rem.presetStretch"] = "Let's stretch a little? Stand up and move!",
				["rem.presetEyes"] = "Rest your eyes: look at something far away for 20 seconds.",
				["rem.presetLunch"] = "Lunch time! Take a real break.",
				["rem.whatSays"] = "What should the pet say?",
				["rem.when"] = "When",
				["rem.kindInterval"] = "Repeat",
				["rem.kindDaily"] = "Every day at a time",
				["rem.kindOnce"] = "Only once",
				["rem.every"] = "every",
				["rem.minutesFrom"] = "minutes, from",
				["rem.hoursTo"] = "h to",
				["rem.hoursEnd"] = "h",
				["rem.at"] = "at",
				["rem.on"] = "on",
				["rem.days"] = "Days of the week",
				["rem.save"] = "Save",
				["rem.cancel"] = "Cancel",
				["rem.smartMenu"] = "Reminder from a sentence…",
				["rem.smartAsk"] = "Write the reminder as you would say it:\n\n\"call Ana at 3pm\", \"in 20 minutes drink water\", \"tomorrow at 9 meeting\", \"every day at 8 take the medicine\", \"every 30 minutes stretch\"",
				["rem.smartOk"] = "Got it! I will remind you: \"{0}\" — {1} ⏰",
				["rem.smartUndo"] = "Undo",
				["rem.smartUndone"] = "OK, reminder removed.",
				["rem.smartFail"] = "I did not understand when. Try \"at 3pm\", \"in 20 minutes\", \"tomorrow at 9\" or \"every day at 8\".",
				["rem.smartDefault"] = "Here is your reminder!",
				["rem.smartEvery"] = "every {0} min",
				["rem.smartDaily"] = "every day at {0}",
				["nav.extras"] = "Extras",
				["nav.ai"] = "Assistant",
				["nav.home"] = "Home",
				["nav.plugins"] = "Plugins",
				["nav.integrations"] = "Integrations",
				["plug.care.title"] = "Look after you",
				["int.claude.desc"] = "The pet reacts while Claude Code works (thinking, editing, done) and Claude can send you short messages in the pet's bubble.",
				["int.mcpOn"] = "messages on",
				["int.mcpOff"] = "messages off",
				["int.secConnection"] = "Connection",
				["int.cli.title"] = "Claude Code on this computer",
				["int.cliFound"] = "Found: {0}",
				["int.cliMissing"] = "Not found. Install Claude Code, or use the manual command below.",
				["int.refresh"] = "Refresh",
				["int.listener.title"] = "The pet's listener",
				["int.listening"] = "Listening to Claude Code on port {0} (this computer only).",
				["int.notListening"] = "Off: turn the reactions on below.",
				["int.secReactions"] = "Reactions (hooks)",
				["int.secMcp"] = "Messages (MCP)",
				["int.copyCmd.title"] = "Manual command",
				["int.copyCmd.desc"] = "Copies the command that adds the pet to Claude Code, to paste in a terminal.",
				["int.copyCmd.button"] = "Copy",
				["int.copied"] = "Command copied.",
				["int.secAdvanced"] = "Advanced",
				["int.folder.title"] = "Claude settings folder",
				["int.folder.desc"] = "Opens the folder with settings.json and the backup the pet made before changing it.",
				["ai.providers"] = "Configured providers ({0})",
				["ai.whereLocal"] = "on this computer",
				["ai.whereInternet"] = "on the internet",
				["ai.keySaved"] = "key saved",
				["ai.use"] = "Use",
				["ai.edit"] = "Edit",
				["ai.removeBtn"] = "Remove",
				["ai.removeConfirm"] = "Remove {0}? Its saved key is deleted too.",
				["ai.inUse"] = "In use",
				["ai.add.title"] = "Add provider",
				["ai.add.desc"] = "Save another AI service (for example Ollama, to work without internet) and switch whenever you want.",
				["ai.addFirst.desc"] = "Choose a free AI service: Ollama (on your computer), Google Gemini, Groq or OpenRouter.",
				["ai.add.button"] = "Add…",
				["ai.secChat"] = "Chat",
				["int.claude.title"] = "Claude Code",
				["int.mcp.title"] = "Claude Code: messages (MCP)",
				["int.connected"] = "connected: {0}",
				["page.home.title"] = "Home",
				["page.home.subtitle"] = "Your pet, your day and what comes next.",
				["page.plugins.title"] = "Plugins",
				["page.plugins.subtitle"] = "Turn on what you want the pet to do. Everything comes with the program and works on this computer.",
				["page.integrations.title"] = "Integrations",
				["page.integrations.subtitle"] = "Connect the pet to the tools you use.",
				["set.petSection"] = "Pet",
				["set.appSection"] = "Program",
				["plugins.configure"] = "Configure",
				["plugins.open"] = "Open",
				["plugins.always"] = "Always available",
				["plugins.on"] = "On",
				["plugins.off"] = "Off",
				["plugins.cat.all"] = "All",
				["plugins.cat.day"] = "My day",
				["plugins.cat.care"] = "Wellbeing",
				["plugins.cat.fun"] = "Fun",
				["plugins.cat.pc"] = "Computer",
				["plug.weather.title"] = "Weather",
				["plug.weather.desc"] = "In the morning the pet tells the weather of the day and warns before rain. Uses Open-Meteo (free): only your city leaves this computer.",
				["plug.weatherCity.title"] = "City",
				["plug.weatherCity.desc"] = "Where you are, for the forecast.",
				["plug.weatherCity.button"] = "Choose…",
				["plug.weatherMorning.title"] = "Weather of the day",
				["plug.weatherMorning.desc"] = "In the morning, the forecast with a tip (umbrella, heat, cold).",
				["plug.weatherRain.title"] = "Rain warning",
				["plug.weatherRain.desc"] = "When rain is likely in the next 2 hours.",
				["plug.weatherNow.title"] = "Weather now",
				["plug.weatherNow.desc"] = "The pet says the weather in its bubble.",
				["plug.weatherNow.button"] = "Show",
				["plug.reminders.title"] = "Reminders",
				["plug.reminders.desc"] = "Water, stretching, breaks and your own reminders, also written as a sentence (\"remind me at 3pm…\").",
				["plug.reminders.count"] = "{0} reminders",
				["plug.focus.title"] = "Focus (Pomodoro)",
				["plug.focus.desc"] = "Focus blocks with breaks. During focus the pet stays quiet and keeps the notices for later.",
				["plug.games.title"] = "Games",
				["plug.games.desc"] = "Fortune cookie, magic 8 ball and jokes, from the pet menu.",
				["plug.shortcuts.count"] = "{0} shortcuts",
				["int.ai.title"] = "AI service",
				["int.ai.desc"] = "Ask the pet anything with a free AI (Ollama on this computer, Gemini, Groq, OpenRouter…).",
				["int.notSet"] = "not set up",
				["home.yourPet"] = "Your pet",
				["home.changePet"] = "Change pet",
				["home.addPet"] = "Add another",
				["home.focus"] = "Focus today",
				["home.focusFoot"] = "finished Pomodoro blocks",
				["home.breaks"] = "Breaks",
				["home.breaksFoot"] = "pauses of 5 minutes or more",
				["home.reminders"] = "Reminders",
				["home.remindersFoot"] = "still to come today",
				["home.mood"] = "Mood",
				["home.moodFoot"] = "how you are today",
				["home.next"] = "Coming up",
				["home.nothing"] = "Nothing else today. 🎉",
				["home.system"] = "The program",
				["home.pets"] = "Pets",
				["home.petsValue"] = "{0} on this computer",
				["home.plugins"] = "Plugins",
				["home.pluginsValue"] = "{0} of {1} on",
				["home.updates"] = "Updates",
				["home.updatesPets"] = "{0} pet(s) to update",
				["home.updatesNone"] = "all up to date",
				["home.version"] = "Version",
				["home.managePlugins"] = "Manage plugins",
				["home.hidden"] = "Hidden (Ctrl+Alt+P to show).",
				["home.ready"] = "Ready to keep you company.",
				["home.tomorrow"] = "tomorrow",
				["home.today"] = "Today",
				["weather.clear"] = "clear sky",
				["weather.partly"] = "some clouds",
				["weather.cloudy"] = "cloudy",
				["weather.fog"] = "fog",
				["weather.drizzle"] = "drizzle",
				["weather.rain"] = "rain",
				["weather.snow"] = "snow",
				["weather.storm"] = "thunderstorm",
				["weather.today"] = "Today in {0}: {1} {2}, {3}° to {4}°.",
				["weather.tipUmbrella"] = "{0}% chance of rain: take the umbrella! ☔",
				["weather.tipHot"] = "It will be hot: drink plenty of water! 🥤",
				["weather.tipCold"] = "It will be cold: grab a coat! 🧥",
				["weather.morning"] = "Good morning! ☀️",
				["weather.rainSoon"] = "Rain is coming in {0} soon ({1}%). Take the umbrella! ☔",
				["weather.noCity"] = "Tell me your city and I will tell you the weather!",
				["weather.choose"] = "Choose city",
				["weather.offline"] = "I could not see the weather now (no internet?).",
				["weather.nowTemp"] = "Now: {0}°.",
				["weather.askCity"] = "Which city? (for example: São Paulo, Lisbon, Madrid)",
				["weather.searching"] = "Looking for the city…",
				["weather.notFound"] = "I did not find \"{0}\". Try the name of the city only.",
				["weather.cityOk"] = "Weather on: {0}.",
				["weather.error"] = "Could not find the city: {0}",
				["menu.weather"] = "How is the weather?",
				["hover.ask"] = "Ask",
				["tour.hello"] = "Hi! I'm {0}. Can I show you what I can do? It takes one minute.",
				["tour.show"] = "Show me",
				["tour.later"] = "Not now",
				["tour.next"] = "Next ({0}/{1})",
				["tour.skip"] = "Skip",
				["tour.finish"] = "Got it!",
				["tour.menu"] = "Right-click me to open my menu: games, focus timer, reminders, shortcuts and more.",
				["tour.drag"] = "You can drag me with the mouse and let go. The middle button (wheel) chooses one of my animations.",
				["tour.focus"] = "I remind you to drink water and take breaks (Reminders), and keep you company while you focus (Pomodoro).",
				["tour.hide"] = "In a meeting? Ctrl+Alt+P hides me and keeps me quiet. Press it again to bring me back.",
				["tour.ai"] = "Hover over me and click \"Ask\" to chat: reminders, weather and your agenda already work. For free questions, set up the AI (it is free).",
				["tour.aiReady"] = "Hover over me and click \"Ask\" (or press {0}) to ask me anything.",
				["tour.openAi"] = "Set up the AI",
				["opt.tour.title"] = "Introduction",
				["opt.tour.desc"] = "The pet shows again what it can do.",
				["opt.tour.button"] = "Show",
				["whatsnew.title"] = "What's new in version {0}:",
				["whatsnew.more"] = "See everything",
				["whatsnew.2.5.0"] = "ask me anything (AI), hover button, quiet focus time, Outlook e-mails, I stand still when talking, create your own pet, Spanish and an installer that checks .NET.",
				["whatsnew.2.5.1"] = "colourful emoji, I arrive from the sky the first time and I look at you while I talk.",
				["whatsnew.2.6.0"] = "a new panel with Home and plugins in cards, a chat box on me, the weather (Open-Meteo), reminders written as a sentence, I suggest breaks and pets chat with each other.",
				["opt.aiStyle.title"] = "Personality",
				["opt.aiStyle.desc"] = "How the pet answers.",
				["opt.aiStyle.friendly"] = "Friendly",
				["opt.aiStyle.direct"] = "Direct",
				["opt.aiStyle.kids"] = "For kids",
				["opt.aiStyle.funny"] = "Funny",
				["opt.hoverAsk.title"] = "\"Ask\" button on the pet",
				["opt.hoverAsk.desc"] = "When the mouse is over the pet, a button appears to ask a question with one click.",
				["opt.askHotkey.title"] = "Shortcut to ask",
				["opt.askHotkey.desc"] = "Opens the question box from any program. If another program already uses it, choose another one.",
				["key.space"] = "Space",
				["ask.keyBusy"] = "The shortcut {0} is already used by another program, so it does not open my questions. Choose another one?",
				["ask.keyChoose"] = "Choose another",
				["ask.voice"] = "Speak",
				["ask.continuing"] = "The pet remembers this conversation (\"and then?\" works).",
				["ask.newChat"] = "New conversation",
				["chat.title"] = "Chat",
				["chat.placeholder"] = "Message {0}…",
				["chat.ready"] = "Ready",
				["chat.thinking"] = "Thinking…",
				["chat.noAi"] = "No AI: reminders, weather, agenda",
				["chat.hello"] = "Hi! How can I help?",
				["chat.helloSub"] = "Ask anything, create a reminder or check the weather and your meetings.",
				["chat.helloSubNoAi"] = "Create reminders, check the weather and your meetings. For free questions, set up the AI.",
				["chat.sugHelp"] = "What can you do?",
				["chat.sugWeather"] = "How is the weather?",
				["chat.sugRemind"] = "Remind me to…",
				["chat.sugRemindFill"] = "remind me to  at 3pm",
				["chat.sugMeeting"] = "When is my next meeting?",
				["chat.needAi"] = "I can answer free questions once an AI service is set up (it is free). Reminders, weather and meetings already work here.",
				["chat.privacyNo"] = "OK, nothing was sent.",
				["chat.noAgenda"] = "Connect your Google or Outlook calendar and I will tell you your meetings.",
				["chat.nextMeeting"] = "Your next meeting: {0} at {1}. 📅",
				["chat.system"] = "The computer: CPU at {0}%, memory at {1}%. You have {2} reminder(s) on. 💻",
				["chat.help"] = "I can: remind you of things (\"remind me at 3pm to…\"), tell the weather, tell your next meeting, keep you company in focus time, play games and, with the AI on, answer any question. 🐾",
				["page.ai.title"] = "Assistant",
				["page.ai.subtitle"] = "Connect an AI service and ask your pet anything. Free options ready to use.",
				["opt.ai.title"] = "AI service",
				["opt.ai.desc"] = "Choose Ollama (on this computer, free and private), Google Gemini, Groq, OpenRouter or another compatible service. The key is kept encrypted, only for your Windows user.",
				["opt.ai.button"] = "Set up…",
				["opt.aiAsk.title"] = "Chat with the pet",
				["opt.aiAsk.desc"] = "Opens the chat box above the pet. Reminders, weather and agenda work even without an AI.",
				["opt.aiAsk.button"] = "Open chat",
				["menu.ask"] = "Ask the pet…",
				["ai.title"] = "AI of the pet",
				["ai.addTitle"] = "Add an AI provider",
				["ai.editTitle"] = "Edit AI provider",
				["ai.step1"] = "1. Choose the service",
				["ai.step2"] = "2. Connect",
				["ai.tag.ollama"] = "Free · on your computer",
				["ai.tag.gemini"] = "Free · Google",
				["ai.tag.groq"] = "Free · very fast",
				["ai.tag.openrouter"] = "Free models",
				["ai.tag.custom"] = "OpenAI compatible",
				["ai.customShort"] = "Other",
				["ai.keyHelp"] = "Paste the key here. It is saved encrypted, only for your Windows user.",
				["ai.modelAuto"] = "Chosen by itself when you test (one that answers); you can change it.",
				["ai.showKey"] = "Show",
				["ai.hideKey"] = "Hide",
				["ai.intro"] = "The pet answers your questions with an AI service. Ollama runs on this computer (free, private, nothing leaves it). The others are on the internet with free plans: you only need a free key.",
				["ai.service"] = "Service",
				["ai.address"] = "Address",
				["ai.key"] = "Key (API key)",
				["ai.model"] = "Model",
				["ai.custom"] = "Other (OpenAI compatible)",
				["ai.test"] = "Test connection",
				["ai.save"] = "Save",
				["ai.remove"] = "Remove",
				["ai.getKey"] = "Get a free key",
				["ai.getOllama"] = "Download Ollama",
				["ai.infoLocal"] = "Install Ollama, then run in a terminal: ollama pull llama3.2 — and click Test.",
				["ai.infoCloud"] = "Your questions are sent to {0}. Create a free key, paste it above and click Test.",
				["ai.testing"] = "Connecting…",
				["ai.testOk"] = "Connected! {0} model(s) available. Using: {1}",
				["ai.testFail"] = "It did not work: {0}",
				["ai.needKey"] = "paste the key first",
				["ai.noModels"] = "the service did not list any model",
				["ai.noModelsOllama"] = "Ollama has no model yet. Run in a terminal: ollama pull llama3.2",
				["ai.ollamaOff"] = "Ollama is not running on this computer. Install it from ollama.com (or open it) and try again.",
				["ai.timeout"] = "the service took too long to answer",
				["ai.httpsOnly"] = "the address must start with https:// (or be on this computer)",
				["ai.errKey"] = "the key was refused: check it",
				["ai.errModel"] = "this model does not exist there",
				["ai.errLimit"] = "the free limit of the service was reached for now: try again later",
				["ai.errBusy"] = "the service is overloaded right now: try again in a moment",
				["ai.testingModels"] = "Connected! Finding a model that answers…",
				["ai.saved"] = "AI set up: {0} ({1}).",
				["ai.removed"] = "AI removed.",
				["ai.ready"] = "Now you can ask me things! Right-click me → Ask the pet. 🤓",
				["ask.title"] = "Ask {0}",
				["ask.prompt"] = "What do you want to ask {0}?",
				["ask.send"] = "Ask",
				["ask.thinking"] = "Hmm, let me think… 🤔",
				["ask.empty"] = "Hmm… I have no answer for that.",
				["ask.seeAll"] = "See all",
				["ask.again"] = "Ask again",
				["ask.settings"] = "AI settings",
				["ask.error"] = "I couldn't answer: {0}",
				["ask.privacy"] = "Your questions will be sent to {0} to be answered.\n\nDo not write passwords or private data. Continue?",
				["opt.mail.title"] = "New e-mails (Outlook)",
				["opt.mail.desc"] = "While the classic Outlook is open, the pet tells you when a new e-mail arrives. Only the number of messages is read, never the sender or the text. The new Outlook is not supported.",
				["mail.newOne"] = "You have a new e-mail in Outlook. Open it? ✉️",
				["mail.newMany"] = "You have {0} new e-mails in Outlook. Open them? ✉️",
				["mail.open"] = "Open",
				["mail.later"] = "Later",
				["quiet.heldOne"] = "While you were busy, 1 notice arrived.",
				["quiet.heldMany"] = "While you were busy, {0} notices arrived.",
				["quiet.show"] = "Show",
				["quiet.dismiss"] = "Dismiss",
				["quiet.next"] = "Next ({0})",
				["opt.petsMeet.title"] = "Pets greet each other",
				["opt.petsMeet.desc"] = "When two pets meet on the screen they chat for a moment, wave, dance or show some love.",
				["chat.first"] = "Hi! Nice day, isn't it?|Have you seen a cookie around here?|Race you to the other side!|Shh… the human is working.|I just had the best nap.|Do you like this wallpaper?|Want to play tag?|I counted 37 open tabs today!",
				["chat.answer"] = "Perfect for a walk on the taskbar!|No… but I would share it with you!|You are on! 🏁|Oops, I will walk on tiptoe.|Lucky you! I want one too. 💤|Yes, it is great for jumping!|Tag, you are it! 😄|Wow… and they say WE are messy!",
				["opt.pcEvents.title"] = "Reactions to the computer",
				["opt.pcEvents.desc"] = "The pet says good morning and good night, warns you when the battery is low and celebrates finished downloads.",
				["opt.activity.title"] = "Notice how I use the computer",
				["opt.activity.desc"] = "The pet suggests a break after a long time without one, is happy when you come back, and celebrates when you close a program used for hours. Only the time of the last key or mouse movement and the name of the program in front are used: nothing you type or see.",
				["act.break"] = "You have been at the computer for {0} without a break. How about standing up, stretching and drinking some water? 🚶",
				["act.breakOk"] = "I will take a break",
				["act.breakLater"] = "Not now",
				["act.welcome"] = "Good to have you back! 👋",
				["act.appDone"] = "Phew! {0} of {1} today. Good job! 🎉",
				["opt.agenda.title"] = "Calendar (Google or Outlook)",
				["opt.agenda.desc"] = "The pet tells you before each meeting. Uses the private iCal link of your calendar: no sign-in needed.",
				["opt.agenda.button"] = "Set up…",
				["opt.agendaLead.title"] = "Meeting warning",
				["opt.agendaLead.desc"] = "How long before a meeting the pet tells you.",
				["agenda.ask"] = "Paste the private iCal link of your calendar (empty to remove):\n\nGoogle Calendar: Settings → your calendar → \"Secret address in iCal format\".\nOutlook.com: Settings → Calendar → Shared calendars → Publish a calendar → ICS link.\n\nThe link stays only on this computer.",
				["agenda.checking"] = "Reading the calendar…",
				["agenda.connected"] = "Calendar connected: {0} event(s) in the next 24 hours.",
				["agenda.connectedNext"] = "Calendar connected! Next: {0} at {1}.",
				["agenda.connectedNone"] = "Calendar connected! Nothing in the next 24 hours.",
				["agenda.removed"] = "Calendar removed.",
				["agenda.error"] = "Could not read the calendar: {0}",
				["agenda.httpsOnly"] = "the link must start with https:// or webcal://",
				["agenda.notCalendar"] = "the link is not an iCal calendar",
				["agenda.untitled"] = "(no title)",
				["agenda.soon"] = "In {0} min: {1} ({2}) 📅",
				["agenda.now"] = "It's time: {0} 📅",
				["menu.agendaNext"] = "Next: {0} {1}",
				["menu.agendaNone"] = "No meetings in the next 24 h",
				["pc.morning"] = "Good morning! ☀️ Have a great day.",
				["pc.night"] = "It's late… how about getting some rest? 🌙",
				["pc.batteryLow"] = "Battery at {0}%… plug in the charger? 🔌",
				["pc.batteryCritical"] = "Battery almost empty ({0}%)! Save your work and plug in the charger. 🪫",
				["pc.charging"] = "Thanks! Charging now. ⚡",
				["pc.downloaded"] = "Download finished: {0} 🎉",
				["pc.downloadedMany"] = "{0} downloads finished 🎉",
				["pc.showFile"] = "Show",
				["agent.hello"] = "Hi! Let's code together? 👋",
				["agent.thinking"] = "Claude is thinking… 🤔",
				["agent.reading"] = "Reading the code… 📖",
				["agent.editing"] = "Editing files… ✏️",
				["agent.running"] = "Running a command… ⚙️",
				["agent.searching"] = "Searching the web… 🔎",
				["agent.delegating"] = "Calling a helper agent…",
				["agent.working"] = "Working on it…",
				["agent.waiting"] = "Claude needs you! 👀",
				["agent.done"] = "Done! ✅ Claude finished.",
				["agent.error"] = "Oops, something failed. Claude is on it.",
				["opt.claude.title"] = "React to Claude Code",
				["opt.claude.desc"] = "The pet thinks, types, runs and celebrates while Claude Code works, and calls you when it needs you. Adds hooks to your Claude Code settings (a backup is kept).",
				["opt.claudeBubbles.title"] = "Claude Code progress bubbles",
				["opt.claudeBubbles.desc"] = "Also say what Claude is doing (thinking, editing, running…). The end and \"needs you\" are always said.",
				["opt.claudeTest.title"] = "Test the Claude Code reactions",
				["opt.claudeTest.desc"] = "Plays a short example: thinking, editing, running and done.",
				["opt.claudeTest.button"] = "Test",
				["opt.claudeMcp.title"] = "Let Claude talk through the pet (MCP)",
				["opt.claudeMcp.desc"] = "Adds the \"desktoppet\" tools to Claude Code: Claude can send you a short message in the pet's bubble (for example when a long task ends). Paths, links and code are never shown.",
				["mcp.on"] = "Done! Claude can now talk through the pet (in new Claude Code sessions).",
				["mcp.off"] = "Claude Code no longer talks through the pet.",
				["mcp.error"] = "Claude Code answered with an error: {0}",
				["mcp.manual"] = "Claude Code was not found on this computer. Run this command in a terminal (it is already copied):\n\n{0}",
				["claude.confirmOn"] = "This adds hooks to your Claude Code settings:\n{0}\n\nA copy of the file is saved first ({1}). The hooks only tell this program which kind of thing Claude is doing; nothing of your code or messages is shown.\n\nNew Claude Code sessions will use it. Continue?",
				["claude.on"] = "Done! The pet now reacts to Claude Code (open a new Claude Code session).",
				["claude.off"] = "The Claude Code hooks were removed.",
				["claude.error"] = "Could not change the Claude Code settings: {0}",
				["claude.portBusy"] = "The port {0} is used by another program: the pet cannot listen to Claude Code.",
				["page.extras.title"] = "Extras",
				["page.extras.subtitle"] = "Focus timer, virtual pet, PC monitor, mood and shortcuts. Right-click the pet to use them.",
				["opt.pomoFocus.title"] = "Focus time (Pomodoro)",
				["opt.pomoFocus.desc"] = "How long each focus block lasts. Start it from the pet menu (right click).",
				["opt.pomoBreak.title"] = "Break time",
				["opt.pomoBreak.desc"] = "Short break between focus blocks. Every 4 blocks the break is 15 minutes.",
				["opt.vpet.title"] = "Virtual pet",
				["opt.vpet.desc"] = "Your pet gets hungry, wants affection and gets tired. Feed it, pet it, play and let it sleep from the pet menu.",
				["opt.monitor.title"] = "PC monitor",
				["opt.monitor.desc"] = "Hover the mouse over the pet to see CPU, memory and free disk space. The pet also comments when the computer is very busy.",
				["opt.mood.title"] = "Daily mood check-in",
				["opt.mood.desc"] = "Once a day the pet asks how you are doing.",
				["opt.moodHour.title"] = "Mood check-in time",
				["opt.moodHour.desc"] = "From what time of the day the pet asks.",
				["opt.moodHistory.title"] = "My mood",
				["opt.moodHistory.desc"] = "Your answers of the last two weeks.",
				["opt.moodHistory.button"] = "View",
				["opt.shortcuts.title"] = "Shortcuts",
				["opt.shortcuts.desc"] = "Folders, programs and websites to open from the pet menu.",
				["opt.shortcuts.button"] = "Manage…",
				["menu.games"] = "Games",
				["menu.fortune"] = "Fortune cookie",
				["menu.ball"] = "Magic 8-ball",
				["menu.joke"] = "Tell me a joke",
				["menu.focus"] = "Focus (Pomodoro)",
				["menu.focusStart"] = "Start focusing ({0} min)",
				["menu.focusPause"] = "Pause",
				["menu.focusResume"] = "Resume",
				["menu.focusStop"] = "Stop",
				["menu.shortcuts"] = "Shortcuts",
				["menu.shortcutsManage"] = "Manage shortcuts…",
				["menu.mood"] = "How am I today?",
				["menu.monitor"] = "Show CPU and memory",
				["menu.sendAway"] = "Send this pet away",
				["pomo.startFocus"] = "Focus time! {0} minutes. I'll keep quiet. 🤫",
				["pomo.stopped"] = "Focus timer stopped.",
				["pomo.off"] = "Off",
				["pomo.statusFocus"] = "Focus: {0} left",
				["pomo.statusBreak"] = "Break: {0} left",
				["pomo.paused"] = "(paused)",
				["pomo.break"] = "Great job! Take a {0}-minute break. Stretch, drink water. 🎉",
				["pomo.longBreak"] = "4 focus blocks done! Take a long {0}-minute break. 🏆",
				["pomo.backToFocus"] = "Break is over. Back to focus for {0} minutes?",
				["pomo.ok"] = "OK",
				["pomo.skipBreak"] = "Skip the break",
				["pomo.stop"] = "Stop",
				["vpet.feed"] = "Feed",
				["vpet.pet"] = "Pet",
				["vpet.play"] = "Play",
				["vpet.sleep"] = "Let it sleep",
				["vpet.later"] = "Later",
				["vpet.hungry"] = "I'm hungry… 🍖",
				["vpet.lonely"] = "I miss you… can I have some affection? ❤️",
				["vpet.sleepy"] = "I'm so tired… can I take a nap? 💤",
				["vpet.fed"] = "Yum! Thank you!",
				["vpet.petted"] = "Hehe, I love that!",
				["vpet.played"] = "That was fun!",
				["vpet.slept"] = "Zzz… I feel much better!",
				["mon.diskFree"] = "{0} {1:0} GB free",
				["mon.busy"] = "Phew, the computer is working hard: CPU at {0}%!",
				["mood.ask"] = "How are you today?",
				["mood.reply0"] = "That's wonderful! Let's keep this energy! ✨",
				["mood.reply1"] = "Nice! Have a good day.",
				["mood.reply2"] = "OK. Maybe a short walk or some water helps?",
				["mood.reply3"] = "I'm sorry. Take it easy, one thing at a time. I'm here.",
				["mood.reply4"] = "I'm sorry you're feeling bad. Talk to someone you trust, and be kind to yourself. ❤️",
				["mood.history"] = "Last 14 days (oldest first):\n\n{0}",
				["launch.title"] = "Shortcuts",
				["launch.intro"] = "These appear in the pet menu (right click) → Shortcuts.",
				["launch.addFolder"] = "Add folder",
				["launch.addFile"] = "Add program/file",
				["launch.addSite"] = "Add website",
				["launch.remove"] = "Remove",
				["launch.close"] = "Close",
				["launch.siteAsk"] = "Website address:",
				["launch.nameAsk"] = "Name in the menu:",
				["launch.opening"] = "Opening {0}…",
				["launch.error"] = "I couldn't open {0}: {1}",
				["launch.missing"] = "it does not exist anymore",
				["launch.badLink"] = "invalid address",
				["fun.ballAsk"] = "Think of a yes-or-no question… then shake the ball!",
				["fun.ballShake"] = "Shake",
				["fun.ballAnswers"] = "Yes!|No.|Definitely.|Better not.|Ask again later.|Without a doubt.|Hmm… unlikely.|The signs say yes.|I can't tell now.|Count on it.|My answer is no.|Very likely.",
				["fun.fortunes"] = "A small step today is a big step tomorrow.|Something good is about to happen.|Take a break: your best idea may come during it.|Your kindness will come back to you.|Today is a good day to learn something new.|Drink some water. Seriously.|A finished task is better than a perfect plan.|Someone is happy to have you around.|Patience is also progress.|The bug is in the line you're sure is right.",
				["fun.jokes"] = "Why did the computer go to the doctor? It had a virus!|What does a cloud wear under its clothes? Thunderwear.|Why don't skeletons fight? They don't have the guts.|I told my computer I needed a break… it gave me a KitKat error.|Why was the math book sad? Too many problems.|What do you call a fish without eyes? A fsh.",
				["nav.addPet"] = "＋  Add a pet",
				["nav.subtitle"] = "Desktop Pet",

				["page.pets.title"] = "Pets",
				["page.pets.subtitle"] = "Pick a mate for your desktop. Click a card to see details. Download a pet, then adopt it.",
				["page.behaviour.title"] = "Behaviour",
				["page.behaviour.subtitle"] = "How your pet moves and interacts with windows and screens.",
				["page.application.title"] = "Settings",
				["page.application.subtitle"] = "How the pet moves, language, theme, start-up, size and maintenance.",
				["page.about.title"] = "About",
				["page.about.subtitle"] = "desktopPet {0}",

				["pets.search"] = "Search pets or authors…",
				["pets.loading"] = "Loading pets…",
				["pets.noMatch"] = "No pet matches \"{0}\".",
				["pets.empty"] = "No pets found. Check your internet connection.",
				["pets.badgeInUse"] = "In use",
				["pets.badgeNew"] = "New",
				["pets.sortBy"] = "Sort by",
				["pets.sort.recent"] = "Recent",
				["pets.sort.name"] = "Name",
				["pets.sort.author"] = "Author",
				["pets.adopt"] = "Adopt this pet",
				["pets.download"] = "Download",
				["pets.downloadUpdate"] = "Download the update",
				["pets.downloadSize"] = "Download ({0})",
				["pets.downloadUpdateSize"] = "Download the update ({0})",
				["pets.downloadingPct"] = "Downloading… {0}%",
				["pets.retry"] = "Try again",
				["pets.removeLink"] = "Remove from this computer",
				["pets.remove"] = "Remove pet",
				["pets.removeConfirm"] = "Remove {0} from this computer?\n\nYou can download it again whenever you want.",
				["pets.errIncomplete"] = "the download was interrupted. Try again.",
				["pets.errMismatch"] = "the file does not match the gallery (different size). Try again in a few minutes.",
				["pets.errNotPet"] = "the file is not a valid pet.",
				["pets.notInGallery"] = "This pet is no longer in the gallery. It stays here while it is in use.",
				["pets.offline"] = "No internet connection. Only the pets already on this computer are shown: connect to see the pets you can download.",
				["pets.offlineRetry"] = "Try again",
				["pets.badgeDownloaded"] = "Downloaded",
				["pets.adoptAnother"] = "In use – add another one",
				["pets.by"] = "by {0}",
				["pets.updated"] = "Updated {0}",
				["pets.statAnimations"] = "animations",
				["pets.statSounds"] = "sounds",
				["pets.statSkins"] = "skins",
				["pets.loadingDetails"] = "Loading details…",
				["pets.color"] = "Color: {0}",
				["pets.statColors"] = "colors",
				["color.blue"] = "Blue",
				["color.green"] = "Green",
				["color.yellow"] = "Yellow",
				["color.orange"] = "Orange",
				["color.red"] = "Red",
				["color.pink"] = "Pink",
				["color.purple"] = "Purple",

				["status.loadingList"] = "Loading pet list…",
				["status.loadingListCount"] = "Loading pet list… {0}",
				["status.available"] = "{0} pets available",
				["status.listError"] = "Could not load the pet list: {0}",
				["status.downloading"] = "Downloading {0}…",
				["status.enjoy"] = "Enjoy {0}!",
				["status.downloaded"] = "{0} downloaded. Now you can adopt it!",
				["status.downloadCanceled"] = "Download of {0} canceled.",
				["status.removed"] = "{0} was removed from this computer.",
				["status.removeError"] = "Could not remove {0}: {1}",
				["status.offline"] = "No internet: showing only the pets on this computer.",
				["status.downloadError"] = "Error downloading {0}: {1}",
				["status.restored"] = "Default pet restored.",
				["status.startupError"] = "Windows did not allow changing the startup setting.",
				["status.enabled"] = "{0} enabled",
				["status.disabled"] = "{0} disabled",
				["status.setTo"] = "{0}: {1}",

				["tray.soundPet"] = "&Sounds of {0}",
				["tray.hidePets"] = "&Hide pets",
				["tray.showPets"] = "&Show pets",
				["tray.panelUpdateOne"] = "Open &Panel (1 update)",
				["tray.panelUpdates"] = "Open &Panel ({0} updates)",
				["pets.sounds"] = "Sounds of this pet",
				["pets.tabMine"] = "My pets",
				["pets.tabDownload"] = "Download pets",
				["pets.allDownloaded"] = "You already have every pet of the gallery.",
				["pets.badgeUpdate"] = "Update",
				["status.soundOn"] = "Sounds of {0} on",
				["status.soundOff"] = "Sounds of {0} off",
				["status.exportNone"] = "There is no downloaded pet to export yet.",
				["status.exported"] = "{0} pets saved in {1}.",
				["status.exportError"] = "Could not export: {0}",
				["status.imported"] = "{0} pets imported.",
				["status.importNone"] = "The file has no pets.",
				["status.importError"] = "Could not import: {0}",
				["status.hotkeyBusy"] = "{0} is already used by another program.",
				["dlg.petsFile"] = "DesktopPet pets",
				["dlg.petsFileName"] = "my-pets",
				["dlg.importAdopt"] = "\"{0}\" was the pet in use on the other computer. Adopt it now?",
				["opt.hotkey.title"] = "Hide pets with Ctrl+Alt+P",
				["opt.hotkey.desc"] = "The shortcut hides every pet (and its sounds) or shows them again, from any program. Handy in meetings.",
				["opt.export.title"] = "Back up my pets and settings",
				["opt.export.desc"] = "One file with your pets, reminders, shortcuts, mood history and settings, to take them to another computer. It also has your calendar link: keep it private.",
				["dlg.importMyThings"] = "This file also has reminders, shortcuts, mood history and settings.\n\nReplace the ones of this computer with them?",
				["status.restoredMyThings"] = "{0} pets and {1} items restored.",
				["opt.logs.title"] = "Log files",
				["opt.logs.desc"] = "Warnings and errors of the last 7 days. If something goes wrong, send the latest file to whoever helps you.",
				["opt.logs.button"] = "Open folder",
				["opt.export.button"] = "Export…",
				["opt.import.title"] = "Import pets",
				["opt.import.desc"] = "Bring the pets of an exported file to this computer.",
				["opt.import.button"] = "Import…",
				["opt.create.title"] = "Create my pet",
				["opt.create.desc"] = "Turn your own pictures (PNG, GIF, JPG) into a pet that walks, sleeps and falls like the others.",
				["opt.create.button"] = "Create…",
				["create.title"] = "Create my pet",
				["create.intro"] = "Choose the pictures of each movement, in order (frame_01, frame_02…). Only \"Walking\" is needed; the others use it when missing. Pictures with a transparent background look best.",
				["create.name"] = "Pet name",
				["create.walk"] = "Walking",
				["create.idle"] = "Standing still",
				["create.sleep"] = "Sleeping",
				["create.fall"] = "Falling",
				["create.drag"] = "Being dragged",
				["create.none"] = "none",
				["create.needed"] = "required: choose the pictures",
				["create.optional"] = "optional",
				["launch.empty"] = "No shortcuts yet. Add a folder, a program or a site below.",
				["create.count"] = "{0} pictures",
				["create.pick"] = "Choose…",
				["create.images"] = "Pictures",
				["create.facingRight"] = "My pictures look to the right",
				["create.create"] = "Create pet",
				["create.needName"] = "Give your pet a name.",
				["create.needWalk"] = "Choose at least one picture for \"Walking\".",
				["create.info"] = "Created on this computer with Desktop Pet.",
				["pets.createdHere"] = "You created this pet on this computer. It is kept in your backups.",
				["status.created"] = "\"{0}\" was created and is in My pets.",
				["dlg.createdAdopt"] = "\"{0}\" is ready. Adopt it now?",
				["opt.debug.title"] = "Debug window",
				["opt.debug.desc"] = "See what the pet is doing, live. It also opens if you hold CTRL while the program starts.",
				["opt.debug.button"] = "Open",
				["update.petTitleOne"] = "Pet update available",
				["update.petTitleMany"] = "{0} pet updates available",
				["update.petText"] = "{0}. Click to open the panel.",
				["debug.title"] = "Debug",
				["debug.subtitle"] = "Pet: {0}  ·  on screen: {1}  ·  {2} lines",
				["debug.search"] = "Search the log (Ctrl+F)",
				["debug.info"] = "Info",
				["debug.warnings"] = "Warnings",
				["debug.errors"] = "Errors",
				["debug.pause"] = "Pause",
				["debug.resume"] = "Resume",
				["debug.copy"] = "Copy",
				["debug.clear"] = "Clear",
				["debug.xml"] = "View XML",
				["debug.dot"] = "Graph (DOT)",
				["debug.empty"] = "Nothing to show.",
				["opt.on"] = "On",
				["opt.off"] = "Off",

				["opt.multiscreen.title"] = "Multiple screens",
				["opt.multiscreen.desc"] = "Let the pet spawn and walk on every monitor, not only on the primary one.",
				["opt.smooth.title"] = "Smooth movements",
				["opt.smooth.desc"] = "Interpolate the position between frames. Smoother, but less like the original pixel-stepping.",
				["opt.taskbar.title"] = "Stay above the taskbar",
				["opt.taskbar.desc"] = "Prevent the taskbar from covering the pet. Clicking the tray icon can become harder.",
				["opt.foreground.title"] = "Bring windows to front",
				["opt.foreground.desc"] = "Like the original eSheep, bring a window to front when the pet lands on it. Distracting, so off by default.",
				["opt.volume.title"] = "Sound volume",
				["opt.volume.desc"] = "Volume of the sound effects, if the pet has any.",
				["opt.volume.value"] = "{0} %",

				["opt.language.title"] = "Language",
				["opt.language.desc"] = "Language of the application. \"Auto\" follows Windows. Files live in the lang folder next to the program.",
				["opt.language.auto"] = "Auto",
				["opt.theme.title"] = "Theme",
				["opt.theme.desc"] = "Light or dark colors. \"Automatic\" follows the Windows setting.",
				["opt.theme.auto"] = "Automatic",
				["opt.theme.light"] = "Light",
				["opt.theme.dark"] = "Dark",
				["opt.startup.title"] = "Start with Windows",
				["opt.startup.desc"] = "Open the program (and your pets) automatically when you sign in to Windows.",
				["opt.quantity.title"] = "Pets at startup",
				["opt.quantity.desc"] = "How many pets appear when the application starts.",
				["opt.quantity.one"] = "1 pet",
				["opt.quantity.many"] = "{0} pets",
				["opt.scale.title"] = "Pet size",
				["opt.scale.desc"] = "Scale pixel-art pets on high resolution monitors. The application restarts to apply it.",
				["opt.restore.title"] = "Restore default pet",
				["opt.restore.desc"] = "Go back to the original sheep included in the application.",
				["opt.restore.button"] = "Restore",
				["opt.install.title"] = "Install on this computer",
				["opt.install.titleInstalled"] = "Repair or uninstall",
				["opt.install.desc"] = "Copy the application to your user folder, add shortcuts and optionally start with Windows.",
				["opt.install.button"] = "Open…",

				["dlg.restore.text"] = "Replace the current pet with the original eSheep?",
				["dlg.restore.title"] = "Restore pet",
				["dlg.yes"] = "Yes",
				["dlg.no"] = "No",
				["dlg.restart.text"] = "The application restarts to apply the change. Continue?",

				["about.currentPet"] = "CURRENT PET",
				["about.credits"] = "CREDITS",
				["about.links"] = "LINKS",
				["about.petLine"] = "by {0}   ·   version {1}",
				["about.credit.art"] = "Original concept and artwork",
				["about.credit.app"] = "C# application",
				["about.credit.tray"] = "System tray implementation",
				["about.credit.toss"] = "Pet tossing and window/pet collisions",
				["about.credit.audio"] = "Sound playback (open source)",
				["about.credit.fork"] = ".NET 10 version, translation and new pets",
				["about.link.github"] = "Source code on GitHub",
				["about.link.help"] = "Help and documentation",
				["about.link.gallery"] = "Pet gallery (web)",
				["about.link.original"] = "Original project by Adriano Petrucci",

				// ---- animation picker (middle-click on the pet)
				["picker.title"] = "{0} – animations",
				["picker.count"] = "{0} animations · click one to play it",
				["picker.search"] = "Search animation…",
				["picker.playing"] = "playing",
				["picker.empty"] = "No animation found",
				["picker.hint"] = "Middle-click the pet to open this window  ·  Esc to close",

				// ---- messages
				["msg.alreadyRunning"] = "Application is already running! Only 2 instances are allowed.",
				["msg.information"] = "Information",
				["msg.cantExecute"] = "Can't execute application: {0}",
				["msg.settingsError"] = "Error opening settings: {0}",
				["msg.settings"] = "Settings",
				["msg.xmlError"] = "XML error",
				["msg.xmlParseError"] = "Error parsing animation XML: {0}",
				["msg.xmlLoadError"] = "Error, can't load animations file. The original pet will be loaded",
				["msg.fatalError"] = "Fatal error: {0}\n----------\nPress Cancel for more info",
				["msg.appError"] = "Application error",

				// ---- installer
				["inst.title"] = "Install {0}",
				["inst.header"] = "Install {0}",
				["inst.headerApp"] = "Install desktopPet",
				["inst.desc"] = "Copies the program to your user folder and creates shortcuts. No administrator needed; your pets and settings stay where they are.",
				["inst.accept"] = "Accept",
				["inst.install"] = "Install",
				["inst.installed"] = "Installed",
				["inst.update"] = "Update",
				["inst.uninstall"] = "Uninstall",
				["inst.startMenu"] = "Add shortcut to Start Menu",
				["inst.desktop"] = "Add shortcut to Desktop",
				["inst.autostart"] = "Start with Windows",
				["inst.error"] = "Error: {0}",
				["inst.newVersion"] = "A newer version was found on the web: {0}\n==========================\nCHANGELOG:\n{1}\n==========================\nDo you want to install it now?",
				["inst.versionTitle"] = "{0} version: {1}",
				["inst.updateFailed"] = "Unable to update: {0}",
				["inst.updated"] = "Application was updated.",
				["inst.incompleteRelease"] = "version {0} was published without DesktopPet.exe or SHA256SUMS",
				["inst.hashMissing"] = "{0} has no SHA-256 in the release, the download was refused",
				["inst.hashMismatch"] = "{0} does not match its SHA-256, the download was refused",
			};
		}
	}
}
