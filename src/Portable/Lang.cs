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
			try { Use("auto"); } catch (Exception) { }
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
						catch (Exception) { }
						list.Add(new Entry { Code = code, Name = name });
					}
				}
			}
			catch (Exception) { }
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
			}
			catch (Exception) { }
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
			catch (Exception) { }
			return "en";
		}

		private static Dictionary<string, string> Read(string file)
		{
			// The file is data, never code: a bad or hostile file can only give us strings.
			return JsonText.Deserialize<Dictionary<string, string>>(File.ReadAllText(file, System.Text.Encoding.UTF8));
		}

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
				["nav.extras"] = "Extras",
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
				["opt.monitor.desc"] = "Shows CPU, memory and free disk space next to the pet, and the pet comments when the computer is very busy.",
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
				["menu.animations"] = "Choose an animation…",
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
				["page.application.subtitle"] = "Language, theme, startup, size and maintenance.",
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
				["opt.export.title"] = "Export my pets",
				["opt.export.desc"] = "Save your downloaded pets in one file, to take them to another computer.",
				["opt.export.button"] = "Export…",
				["opt.import.title"] = "Import pets",
				["opt.import.desc"] = "Bring the pets of an exported file to this computer.",
				["opt.import.button"] = "Import…",
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
