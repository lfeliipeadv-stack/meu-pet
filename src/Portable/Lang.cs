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
				["nav.addPet"] = "＋  Add a pet",
				["nav.subtitle"] = "Desktop Pet",

				["page.pets.title"] = "Pets",
				["page.pets.subtitle"] = "Pick a mate for your desktop. Click a card to see details, double-click to adopt it.",
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
				["pets.section.builtIn"] = "Included in the program ({0})",
				["pets.section.gallery"] = "From the gallery ({0})",
				["pets.adopt"] = "Adopt this pet",
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
				["status.downloadError"] = "Error downloading {0}: {1}",
				["status.restored"] = "Default pet restored.",
				["status.enabled"] = "{0} enabled",
				["status.disabled"] = "{0} disabled",
				["status.setTo"] = "{0}: {1}",

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

				// ---- easter egg animation menu
				["pet.animMenu.title"] = "Animations",
				["pet.animMenu.quick"] = "Quick picks",
				["pet.animMenu.all"] = "All animations",
				["pet.animMenu.spawns"] = "Spawns",
				["pet.anim.dance"] = "Dance",
				["pet.anim.wave"] = "Wave",
				["pet.anim.sleep"] = "Sleep",
				["pet.anim.jump"] = "Jump",
				["pet.anim.spin"] = "Spin",
				["pet.anim.eat"] = "Eat",
				["pet.anim.flower"] = "Flower",
				["pet.anim.think"] = "Think",
				["pet.anim.scared"] = "Scared",
				["pet.anim.angry"] = "Angry",
				["pet.anim.sneak"] = "Sneak",
				["pet.anim.stretch"] = "Stretch",
			};
		}
	}
}
