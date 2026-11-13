using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Developer check of the features that have no window of their own to look at:
	/// "DesktopPet.exe --selftest=C:\folder\result.txt" runs every check with the real code, writes PASS/FAIL lines
	/// and closes the program (exit code = number of failures). The user's reminders, needs, mood and shortcuts
	/// files are not touched; settings changed by a check are put back.
	/// </summary>
	internal static class SelfTest
	{
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);

		static readonly List<string> lines = new List<string>();
		static int failures;
		static string resultFile = "";

		static void Check(string name, bool ok, string detail = "")
		{
			if (!ok) failures++;
			lines.Add((ok ? "PASS  " : "FAIL  ") + name + (ok || detail.Length == 0 ? "" : "  →  " + detail));
		}

		/// <summary>Developer: "--only=name" runs only the tests whose name contains it.</summary>
		public static string Only;

		static void Try(string name, Action test)
		{
			if (Only != null && name.IndexOf(Only, StringComparison.OrdinalIgnoreCase) < 0) return;
			try { test(); }
			catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
		}

		/// <summary>The self-test is running: the plugins that react to the computer on their own (music, visits, full screen) wait.</summary>
		public static bool Running;
		/// <summary>The live part of the self-test: those plugins run for real (a butterfly, a pushed window, full screen).</summary>
		public static bool Live;

		public static async void Run(string outFile)
		{
			Running = true;
			resultFile = outFile;
			await Task.Delay(3000);   // pets on screen, menus created

			Try("reminder schedule", Reminders_Next);
			Try("reminder json", Reminders_Json);
			Try("virtual pet decay", VirtualPet_Decay);
			Try("per-pet sound", PetSound);
			Try("languages", Languages);
			Try("games texts", Games);
			Try("mood history text", () => Check("mood: 14 days shown", Mood.Recent(14).Split(' ').Length == 14, Mood.Recent(14)));
			Try("start with Windows (read)", () => { bool _ = WindowsStartup.Enabled; Check("start with Windows: readable", true); });
			Try("backup export", Backup_Export);
			Try("backup import safety", Backup_ImportSafety);
			await TryAsync("bubble", Bubble);
			await TryAsync("hide hotkey", Hide);
			await TryAsync("pet menu", Menu);
			await TryAsync("pomodoro", PomodoroCycle);
			await TryAsync("shortcut error", ShortcutError);
			Try("agenda", AgendaParse);
			Try("pets meet", () =>
			{
				Check("pets meet: side by side", PetsMeet.Close(new System.Drawing.Rectangle(100, 500, 80, 80), new System.Drawing.Rectangle(150, 502, 80, 80)));
				Check("pets meet: far apart", !PetsMeet.Close(new System.Drawing.Rectangle(100, 500, 80, 80), new System.Drawing.Rectangle(400, 500, 80, 80)));
				Check("pets meet: one on a window above", !PetsMeet.Close(new System.Drawing.Rectangle(100, 500, 80, 80), new System.Drawing.Rectangle(120, 300, 80, 80)));
			});
			Try("downloads", () =>
			{
				Check("downloads: unfinished files ignored", ComputerEvents.IsUnfinished("Unconfirmed 1234.crdownload") && ComputerEvents.IsUnfinished("video.mp4.part") && ComputerEvents.IsUnfinished("desktop.ini"));
				Check("downloads: finished file noticed", !ComputerEvents.IsUnfinished("relatorio.pdf") && !ComputerEvents.IsUnfinished("setup.exe"));
				Check("downloads: folder found", Directory.Exists(ComputerEvents.DownloadsFolder()), ComputerEvents.DownloadsFolder());
			});
			await TryAsync("ask the pet", AskThePet);
			Try("pet actions", PetActionNames);
			Try("create pet", PetCreatorBuild);
			await TryAsync("dialogs hold the pets", DialogsHold);
			Try("colour emoji", ColorEmoji);
			Try("sheep talk", SheepTalk);
			Try("smart reminders", SmartReminders);
			Try("activity", ActivityCare);
			Try("settings pages", SettingsPages);
			Try("ai providers", AiProviders);
			await TryAsync("chat", ChatBox);
			await TryAsync("weather", WeatherCheck);
			await TryAsync("pets chat", PetsChat);
			await TryAsync("sky arrival", SkyArrival);
			await TryAsync("tour", Onboard);
			await TryAsync("talking pet", TalkingPet);
			await TryAsync("quiet time", QuietTime);
			Try("outlook", () =>
			{
				var c = OutlookMail.Counts();
				if (c == null) lines.Add("SKIP  outlook: the classic Outlook is not open (the check did not start it)");
				else Check("outlook: inbox numbers read", c.Value.total >= 0 && c.Value.unread >= 0, c.ToString());
				Check("outlook: never started by the pet", c != null || !System.Diagnostics.Process.GetProcessesByName("OUTLOOK").Any());
			});
			await TryAsync("agenda online", AgendaOnline);
			await TryAsync("downloads watcher", DownloadsWatcher);
			Try("mcp", Mcp);
			Try("backup of my things", BackupMyThings);
			Try("log file", () =>
			{
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "selftest log line");
				Check("log: warnings saved to disk", File.Exists(LogFile.Today) && File.ReadAllText(LogFile.Today).Contains("selftest log line"), LogFile.Today);
			});
			Try("claude hooks file", ClaudeHooksFile);
			Try("claude events", ClaudeEvents);
			await TryAsync("claude endpoint", ClaudeEndpoint);
			Try("new plugins", NewPlugins);
			await TryAsync("live plugins", LivePlugins);

			lines.Insert(0, $"DesktopPet self-test {DateTime.Now:yyyy-MM-dd HH:mm:ss}  —  {failures} failure(s)");
			try { File.WriteAllLines(outFile, lines, new UTF8Encoding(false)); } catch (Exception) { }
			Environment.Exit(failures);
		}

		/// <summary>
		/// Developer: "--ai-check=result.txt" with the key in the environment variable DESKTOPPET_AI_KEY (never in a file)
		/// and the service in DESKTOPPET_AI_PROVIDER (default gemini): the real service, the real code, no settings changed.
		/// </summary>
		public static async void AiCheck(string outFile)
		{
			var o = new List<string>();
			try
			{
				string key = Environment.GetEnvironmentVariable("DESKTOPPET_AI_KEY") ?? "";
				var p = AiAssistant.Preset(Environment.GetEnvironmentVariable("DESKTOPPET_AI_PROVIDER") ?? "gemini");
				var models = await AiAssistant.ListModels(p.BaseUrl, key);
				o.Add($"models: {models.Count}");
				var sw = System.Diagnostics.Stopwatch.StartNew();
				string m = await AiAssistant.FindWorkingModel(p, p.BaseUrl, key, p.Model, models);
				o.Add($"working model: {m} ({sw.ElapsedMilliseconds} ms)");
				// what happened before the fix: an old saved model the service no longer serves
				string old = await AiAssistant.FindWorkingModel(p, p.BaseUrl, key, "gemini-2.5-flash", models);
				o.Add($"from an old saved model it moves to: {old}");
				sw.Restart();
				string answer = await AiAssistant.Ask(p.BaseUrl, m, key, "Em uma frase: por que o céu é azul?", "Rex");
				o.Add($"answer ({sw.ElapsedMilliseconds} ms): {answer}");
			}
			catch (Exception ex) { o.Add("ERROR " + ex.GetType().Name + ": " + ex.Message); }
			try { File.WriteAllLines(outFile, o, new UTF8Encoding(false)); } catch (Exception) { }
			Environment.Exit(0);
		}

		static async Task TryAsync(string name, Func<Task> test)
		{
			if (Only != null && name.IndexOf(Only, StringComparison.OrdinalIgnoreCase) < 0) return;
			try { await test(); }
			catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
		}

		// ------------------------------------------------------------------ reminders

		static void Reminders_Next()
		{
			// a fixed Wednesday, so the days of the week are known
			var wed = new DateTime(2026, 11, 11);
			var fri = new DateTime(2026, 11, 13);
			var r = new Reminder { Kind = Reminder.Kinds.Interval, Minutes = 60, FromHour = 9, ToHour = 18, Days = 127 };
			Check("interval: +60 min inside hours", r.Next(wed.AddHours(10)) == wed.AddHours(11), r.Next(wed.AddHours(10)).ToString());
			Check("interval: after hours → next day 9h", r.Next(wed.AddHours(17.5)) == wed.AddDays(1).AddHours(9), r.Next(wed.AddHours(17.5)).ToString());
			r.Days = 62;   // Mon–Fri
			Check("interval: Friday evening → Monday 9h", r.Next(fri.AddHours(17.5)) == fri.AddDays(3).AddHours(9), r.Next(fri.AddHours(17.5)).ToString());
			var night = new Reminder { Kind = Reminder.Kinds.Interval, Minutes = 60, FromHour = 22, ToHour = 6, Days = 127 };
			Check("interval: over midnight", night.Next(wed.AddHours(23)) == wed.AddDays(1), night.Next(wed.AddHours(23)).ToString());
			var allDay = new Reminder { Kind = Reminder.Kinds.Interval, Minutes = 30, FromHour = 0, ToHour = 0, Days = 127 };
			Check("interval: 0h–0h is all day", allDay.Next(wed.AddHours(3)) == wed.AddHours(3.5), allDay.Next(wed.AddHours(3)).ToString());

			var daily = new Reminder { Kind = Reminder.Kinds.Daily, Time = "12:00", Days = 62 };
			Check("daily: later today", daily.Next(wed.AddHours(11)) == wed.AddHours(12));
			daily.LastFired = wed.AddHours(12);
			Check("daily: fired today → tomorrow", daily.Next(wed.AddHours(12)) == wed.AddDays(1).AddHours(12), daily.Next(wed.AddHours(12)).ToString());
			daily.LastFired = DateTime.MinValue;
			Check("daily: Saturday → Monday", daily.Next(fri.AddDays(1).AddHours(8)) == fri.AddDays(3).AddHours(12), daily.Next(fri.AddDays(1).AddHours(8)).ToString());

			var once = new Reminder { Kind = Reminder.Kinds.Once, At = wed.AddHours(15) };
			Check("once: due at its time", once.Next(DateTime.MinValue) == wed.AddHours(15));
			once.LastFired = wed.AddHours(15);
			Check("once: never again after firing", once.Next(DateTime.MinValue) == null);

			var snoozed = new Reminder { Kind = Reminder.Kinds.Interval, Minutes = 60, Days = 127, FromHour = 0, ToHour = 0, SnoozedUntil = wed.AddHours(10.25) };
			Check("snooze wins", snoozed.Next(wed.AddHours(10)) == wed.AddHours(10.25));
			Check("disabled → never", new Reminder { Enabled = false }.Next(wed) == null);
			Check("summary text", !string.IsNullOrEmpty(r.Summary()) && !r.Summary().StartsWith("rem."), r.Summary());
		}

		static void Reminders_Json()
		{
			var list = new List<Reminder> { new Reminder { Text = "água", Kind = Reminder.Kinds.Daily, Time = "08:30", Days = 65, At = new DateTime(2026, 12, 1, 10, 0, 0) } };
			var back = JsonText.Deserialize<List<Reminder>>(JsonText.SerializeIndented(list));
			Check("reminder survives save/load", back.Count == 1 && back[0].Text == "água" && back[0].Kind == Reminder.Kinds.Daily
				&& back[0].Time == "08:30" && back[0].Days == 65 && back[0].At == list[0].At);
		}

		// ------------------------------------------------------------------ virtual pet

		static void VirtualPet_Decay()
		{
			var n = new PetNeeds { Food = 80, Love = 80, Energy = 80, Updated = DateTime.Now.AddMinutes(-60) };
			VirtualPet.Decay(n);
			Check("needs: 1 hour", Math.Abs(n.Food - 72) < 0.5 && Math.Abs(n.Love - 74) < 0.5 && Math.Abs(n.Energy - 75) < 0.5, $"{n.Food:0.0} {n.Love:0.0} {n.Energy:0.0}");
			var away = new PetNeeds { Food = 80, Love = 5, Energy = 80, Updated = DateTime.Now.AddHours(-48) };
			VirtualPet.Decay(away);
			Check("needs: program closed 2 days → not below 10 (or the value it had)", Math.Abs(away.Food - 10) < 0.5 && Math.Abs(away.Love - 5) < 0.5, $"{away.Food:0.0} {away.Love:0.0}");
			Check("needs: bars text", VirtualPet.Bars(n).Contains("%"));
		}

		// ------------------------------------------------------------------ settings

		static void PetSound()
		{
			const string f = "selftest_pet";
			Program.MyData.SetPetMuted(f, true);
			bool muted = Program.MyData.IsPetMuted(f);
			Program.MyData.SetPetMuted(f, false);
			Check("per-pet sound: mute and unmute", muted && !Program.MyData.IsPetMuted(f));
			Check("per-pet sound: empty folder means eSheep", Program.MyData.IsPetMuted("") == Program.MyData.IsPetMuted("esheep64"));
		}

		static void Languages()
		{
			foreach (string code in new[] { "pt-BR", "es" })
			{
				string file = Path.Combine(Lang.Folder, code + ".json");
				if (!File.Exists(file)) { Check($"{code}: file next to the program", false, file); continue; }
				var pt = JsonText.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
				var missing = Lang.Keys.Where(k => !pt.ContainsKey(k)).ToList();
				Check($"{code} has every text", missing.Count == 0, string.Join(", ", missing.Take(15)) + (missing.Count > 15 ? $" … ({missing.Count})" : ""));
				var extra = pt.Keys.Where(k => !Lang.Keys.Contains(k)).ToList();
				Check($"{code} has no unknown text", extra.Count == 0, string.Join(", ", extra.Take(15)));
				// placeholders: the same {n} in both languages
				var bad = Lang.Keys.Where(pt.ContainsKey).Where(k => Placeholders(pt[k]) != Placeholders(Lang.English(k))).ToList();
				Check($"{code} placeholders match", bad.Count == 0, string.Join(", ", bad.Take(10)));
			}
			Check("es: listed among the languages", Lang.Available().Any(a => a.Code == "es"));
		}

		/// <summary>"Create my pet": a few drawn pictures become a pet the program can read, with every frame in the sheet.</summary>
		static void PetCreatorBuild()
		{
			System.Drawing.Bitmap Frame(System.Drawing.Color c, int w, int h)
			{
				var b = new System.Drawing.Bitmap(w, h);
				using (var g = System.Drawing.Graphics.FromImage(b)) { g.Clear(System.Drawing.Color.Transparent); using (var br = new System.Drawing.SolidBrush(c)) g.FillEllipse(br, 0, 0, w, h); }
				return b;
			}
			var input = new PetCreator.Input { Name = "Teste Ção", FacingRight = true };
			for (int i = 0; i < 4; i++) input.Walk.Add(Frame(System.Drawing.Color.Orange, 60 + i, 50));
			input.Sleep.Add(Frame(System.Drawing.Color.Blue, 300, 240));   // bigger than the rest: reduced
			input.Sleep.Add(Frame(System.Drawing.Color.Navy, 60, 40));
			string xml = PetCreator.Build(input);
			var ser = new System.Xml.Serialization.XmlSerializer(typeof(XmlData.RootNode));
			XmlData.RootNode root;
			using (var r = new StringReader(xml)) root = (XmlData.RootNode)ser.Deserialize(r);
			Check("create pet: readable by the program", root?.Animations?.Animation?.Length >= 10, root?.Animations?.Animation?.Length.ToString());
			int cells = root.Image.TilesX * root.Image.TilesY;
			Check("create pet: every frame is in the picture", root.Animations.Animation.All(a => a.Sequence.Frame.All(f => f >= 0 && f < cells)), cells.ToString());
			var ids = root.Animations.Animation.Select(a => a.Id).ToHashSet();
			Check("create pet: every 'next' exists", root.Animations.Animation.All(a => a.Sequence.Next == null || a.Sequence.Next.All(n => ids.Contains(n.Value))));
			Check("create pet: walk, fall, drag, sleep and kill", new[] { "walk", "fall", "drag", "sleep", "kill" }.All(n => root.Animations.Animation.Any(a => a.Name == n)));
			Check("create pet: spawns", root.Spawns?.Spawn?.Length >= 1);
			using (var ms = new MemoryStream(Convert.FromBase64String(root.Image.Png)))
			using (var sheet = System.Drawing.Image.FromStream(ms))
				Check("create pet: picture at most 160 px per row", sheet.Height / root.Image.TilesY <= 160 && sheet.Width % root.Image.TilesX == 0, $"{sheet.Width}x{sheet.Height}");
			using (var ms = new MemoryStream(Convert.FromBase64String(root.Header.Icon)))
			using (var ico = new System.Drawing.Icon(ms, 64, 64))
				Check("create pet: icon", ico.Width == 64);
			Check("create pet: folder name", PetCreator.FolderFor("Teste Ção!") == "meu-teste-cao", PetCreator.FolderFor("Teste Ção!"));
			bool refused = false;
			try { PetCreator.Build(new PetCreator.Input { Name = "x" }); } catch (ArgumentException) { refused = true; }
			Check("create pet: walking pictures needed", refused);
			foreach (var img in input.Walk.Concat(input.Sleep)) img.Dispose();
		}

		/// <summary>The plugins of 2.7: links, quiet hours, levels, the agents' command and their settings files.</summary>
		static void NewPlugins()
		{
			Check("clipboard: a link is recognized", ClipboardWatch.IsLink("https://example.com/a?b=1", out var u) && u.Host == "example.com");
			Check("clipboard: plain text is not a link", !ClipboardWatch.IsLink("olá mundo https://x.com", out _));
			Check("clipboard: off unless turned on", !ClipboardWatch.Enabled || Program.MyData.GetSetting("ClipWatch").Length > 0);

			int from = NoticeOptions.QuietFrom, to = NoticeOptions.QuietTo;
			try
			{
				int h = DateTime.Now.Hour;
				NoticeOptions.QuietFrom = h; NoticeOptions.QuietTo = (h + 1) % 24;
				Check("quiet hours: now is inside", NoticeOptions.InQuietHours && SpeechBubble.QuietNow);
				NoticeOptions.QuietFrom = (h + 2) % 24; NoticeOptions.QuietTo = (h + 3) % 24;
				Check("quiet hours: now is outside", !NoticeOptions.InQuietHours);
				NoticeOptions.QuietFrom = -1;
				Check("quiet hours: off", !NoticeOptions.InQuietHours);
			}
			finally { NoticeOptions.QuietFrom = from; NoticeOptions.QuietTo = to; }

			Check("level: 1 at the start, 2 after 3 tasks, 3 after 8", AgentLevel.LevelFor(0) == 1 && AgentLevel.LevelFor(3) == 2 && AgentLevel.LevelFor(8) == 3 && AgentLevel.LevelFor(7) == 2);
			Check("agents: unknown event refused", OtherAgents.Signal("nonsense") == 2);
			Check("agents: command calls this program", OtherAgents.Command("done").Contains("--agent=done"));

			string dir = Path.Combine(Path.GetTempPath(), "desktoppet-agents-" + Guid.NewGuid().ToString("N"));
			OtherAgents.HomeOverride = dir;
			try
			{
				// Codex: notify before the first table, removed again, and never over someone else's notify
				Directory.CreateDirectory(Path.GetDirectoryName(OtherAgents.CodexConfig));
				File.WriteAllText(OtherAgents.CodexConfig, "model = \"o3\"\n\n[profiles.x]\nmodel = \"y\"\n");
				Check("codex: added", OtherAgents.SetCodex(true) == null && OtherAgents.CodexInstalled);
				var lines = File.ReadAllLines(OtherAgents.CodexConfig).ToList();
				Check("codex: notify before the first table", lines.FindIndex(l => l.StartsWith("notify")) < lines.FindIndex(l => l.StartsWith("[")));
				Check("codex: removed", OtherAgents.SetCodex(false) == null && !OtherAgents.CodexInstalled && File.ReadAllText(OtherAgents.CodexConfig).Contains("[profiles.x]"));
				// someone else's notify (like the Codex app's own): run after the pet, put back exactly when turned off
				string theirs = "notify = [ \"C:\\\\Program Files\\\\Other\\\\codex-computer-use.exe\", \"turn-ended\" ]";
				File.WriteAllText(OtherAgents.CodexConfig, theirs + "\nmodel = \"o3\"\n");
				Check("codex: their notify read", string.Join("|", OtherAgents.TomlStrings(theirs)) == "C:\\Program Files\\Other\\codex-computer-use.exe|turn-ended");
				Check("codex: chained with their notify", OtherAgents.SetCodex(true) == null && OtherAgents.CodexInstalled);
				var chained = OtherAgents.TomlStrings(File.ReadAllLines(OtherAgents.CodexConfig).First(l => l.StartsWith("notify")));
				Check("codex: the pet first, then their program with its arguments", chained != null && chained.Count == 5 && chained[1] == "--agent=done" && chained[2] == "--then"
					&& chained[3] == "C:\\Program Files\\Other\\codex-computer-use.exe" && chained[4] == "turn-ended", chained == null ? "?" : string.Join("|", chained));
				Check("codex: only one notify line", File.ReadAllLines(OtherAgents.CodexConfig).Count(l => l.StartsWith("notify")) == 1);
				OtherAgents.SetCodex(false);
				Check("codex: their notify put back exactly", File.ReadAllText(OtherAgents.CodexConfig) == theirs + "\r\nmodel = \"o3\"\r\n" || File.ReadAllText(OtherAgents.CodexConfig) == theirs + "\nmodel = \"o3\"\n",
					File.ReadAllText(OtherAgents.CodexConfig));
				// Cursor: hooks merged with the user's own
				Directory.CreateDirectory(Path.GetDirectoryName(OtherAgents.CursorHooks));
				File.WriteAllText(OtherAgents.CursorHooks, "{\"version\":1,\"hooks\":{\"stop\":[{\"command\":\"mine.exe\"}]}}");
				Check("cursor: added", OtherAgents.SetCursor(true) == null && OtherAgents.CursorInstalled);
				Check("cursor: the user's hook is kept", File.ReadAllText(OtherAgents.CursorHooks).Contains("mine.exe"));
				Check("cursor: removed", OtherAgents.SetCursor(false) == null && !OtherAgents.CursorInstalled && File.ReadAllText(OtherAgents.CursorHooks).Contains("mine.exe"));
			}
			finally
			{
				OtherAgents.HomeOverride = null;
				try { Directory.Delete(dir, true); } catch (Exception) { }
				AgentBridge.Apply();
			}

			Check("notes: tasks saved", Notes.Tasks != null);
			Check("api: example uses the secret file", LocalApi.Example.Contains("agent.json") && LocalApi.Example.Contains("X-DesktopPet"));
		}

		/// <summary>
		/// The plugins for real: another process calling "--agent" like Codex does (with a program chained after it),
		/// a butterfly flying to the pet, a window pushed by the walking pet, a full-screen window hiding the pets,
		/// a new song making the pet react.
		/// </summary>
		static async Task LivePlugins()
		{
			Live = true;
			try
			{
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
				await Task.Delay(300);

				// ---- agents: a second DesktopPet process, called exactly like Codex calls its notify program
				bool apiWas = LocalApi.Enabled, levelWas = AgentLevel.Enabled;
				int tasks = AgentLevel.Tasks;
				try
				{
					LocalApi.Enabled = true; AgentLevel.Enabled = true;
					await Task.Delay(300);
					Check("live agents: the bridge listens for the API", AgentBridge.Listening);
					var psi = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, CreateNoWindow = true };
					foreach (var a in new[] { "--agent=done", "--then", Environment.ProcessPath, "--agent=thinking", "{\"type\":\"agent-turn-complete\"}" }) psi.ArgumentList.Add(a);
					int exit = -1;
					await Task.Run(() => { using (var p = System.Diagnostics.Process.Start(psi)) { if (p.WaitForExit(20000)) exit = p.ExitCode; else p.Kill(); } });
					await Task.Delay(800);
					Check("live agents: the command ends well", exit == 0, "exit " + exit);
					Check("live agents: the pet counted the finished task", AgentLevel.Tasks == tasks + 1, $"{tasks} -> {AgentLevel.Tasks}");
					Check("live agents: the chained program ran after (thinking)", AgentBridge.Last == AgentBridge.Activity.Thinking, AgentBridge.Last.ToString());
				}
				finally
				{
					Program.MyData.SetSetting("AgentTasks", tasks);
					LocalApi.Enabled = apiWas; AgentLevel.Enabled = levelWas;
					foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
				}

				var pet = Pet();
				if (pet == null) { lines.Add("SKIP  live plugins: no pet on screen"); return; }

				// ---- music: a new song (Spotify's title) makes the pet react; a paused player does not count
				MusicWatch.FakeTitle = "Spotify Premium";
				Check("live music: paused Spotify is not a song", MusicWatch.Playing() == "");
				int react = pet.FindAnimation("dance", "party", "happy", "cheer", "jump");
				string before = pet.CurrentAnimationName;
				MusicWatch.FakeTitle = "Daft Punk - One More Time";
				MusicWatch.Tick();
				await Task.Delay(400);
				if (react >= 0) Check("live music: the pet reacts to a new song", pet.CurrentAnimationName != before || new[] { "dance", "party", "happy", "cheer", "jump" }.Contains(pet.CurrentAnimationName), pet.CurrentAnimationName);
				else lines.Add("SKIP  live music: this pet has no dance or happy animation");
				MusicWatch.FakeTitle = null;
				string real = MusicWatch.Playing();
				lines.Add("INFO  live music: Spotify now: " + (real.Length > 0 ? real : "nothing playing"));
				pet.React("walk");
				await Task.Delay(1500);

				// ---- butterfly: flies to the pet, around it, and away
				Visitors.Visit();
				await Task.Delay(300);
				var fly = Visitors.Flying;
				Check("live butterfly: appears", fly != null);
				if (fly != null)
				{
					var start = fly.Location;
					double nearest = double.MaxValue;
					var clock = System.Diagnostics.Stopwatch.StartNew();
					while (Visitors.Flying != null && clock.Elapsed.TotalSeconds < 60)
					{
						var c = new Point(fly.Left + fly.Width / 2, fly.Top + fly.Height / 2);
						var head = new Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 3);
						nearest = Math.Min(nearest, Math.Sqrt(Math.Pow(c.X - head.X, 2) + Math.Pow(c.Y - head.Y, 2)));
						await Task.Delay(100);
					}
					Check("live butterfly: reaches the pet", nearest < 80, $"closest {nearest:0} px");
					Check("live butterfly: flies away and closes", Visitors.Flying == null, $"{clock.Elapsed.TotalSeconds:0} s");
				}

				// ---- pushing: a small window standing on the pet's floor, just in front of it
				bool pushWas = WindowPush.Enabled;
				using (var box = new Form { FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.Manual, Size = new Size(220, 160), Text = "push test", ShowInTaskbar = false })
				{
					try
					{
						// nothing may hold the pets still (a bubble or a window left open by an earlier check)
						foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
						for (int i = 0; i < 30 && FormPet.Held; i++) await Task.Delay(100);
						int dir = 0;
						for (int tries = 0; tries < 3 && dir == 0; tries++)
						{
							pet.React("walk");
							await Task.Delay(800);
							int x0 = pet.Left; await Task.Delay(500);
							dir = Math.Sign(pet.Left - x0);
						}
						if (dir == 0) { lines.Add($"SKIP  live push: the pet did not walk (held {FormPet.Held}, {pet.CurrentAnimationName})"); }
						else
						{
							int front = pet.Left + pet.Width / 2 + dir * pet.Width / 4;
							box.Location = new Point(dir > 0 ? front + 25 : front - 25 - box.Width, pet.Bottom + 2 - box.Height);
							box.Show();
							WindowPush.TestTarget = box.Handle;
							WindowPush.Enabled = true;
							int bx = box.Left;
							var clock = System.Diagnostics.Stopwatch.StartNew();
							while (clock.Elapsed.TotalSeconds < 8 && Math.Abs(box.Left - bx) < 6)
							{
								if (!pet.OnGround || pet.CurrentAnimationName == null || !pet.CurrentAnimationName.Contains("walk")) pet.React("walk");
								await Task.Delay(200);
							}
							Check("live push: the walking pet pushes the window", (box.Left - bx) * dir >= 6, $"window moved {box.Left - bx} px, pet going {(dir > 0 ? "right" : "left")}");
						}
					}
					finally { WindowPush.Enabled = pushWas; WindowPush.TestTarget = IntPtr.Zero; box.Close(); }
				}

				// ---- full screen: a window covering the screen hides the pets, closing it brings them back
				bool fsWas = FullScreenWatch.Enabled;
				FullScreenWatch.Enabled = true;
				using (var full = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = Screen.PrimaryScreen.Bounds, BackColor = Color.Black, Text = "full screen test" })
				{
					try
					{
						full.Show(); full.Activate();
						for (int i = 0; i < 10 && !FullScreenWatch.Active; i++) { await Task.Delay(300); FullScreenWatch.Tick(); }
						if (!FullScreenWatch.Active && GetForegroundWindow() != full.Handle)
							lines.Add("SKIP  live full screen: Windows did not let the test window come to the front (another program has the focus)");
						else
							Check("live full screen: the pets hide", FullScreenWatch.Active && HideHotkey.Hidden && !pet.Visible);
					}
					finally { full.Close(); }
					for (int i = 0; i < 10 && FullScreenWatch.Active; i++) { await Task.Delay(300); FullScreenWatch.Tick(); }
					Check("live full screen: the pets are shown after", !FullScreenWatch.Active && !HideHotkey.Hidden && pet.Visible);
				}
				FullScreenWatch.Enabled = fsWas;
				if (HideHotkey.Hidden) HideHotkey.SetHidden(false);
			}
			finally { Live = false; MusicWatch.FakeTitle = null; }
		}

		/// <summary>Every window with a text box keeps the pets still while it is open.</summary>
		static async Task DialogsHold()
		{
			foreach (var t in new[] { typeof(FormAsk), typeof(FormAiSettings), typeof(FormLaunchers), typeof(FormReminder), typeof(FormPetCreator) })
				Check($"dialog holds the pets: {t.Name}", typeof(PetDialog).IsAssignableFrom(t));
			var pet = Pet();
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			await Task.Delay(200);
			pet?.React("walk");
			await Task.Delay(1700);
			using (var d = new FormPetCreator())
			{
				d.Show();
				await Task.Delay(600);
				Check("dialog open: pets held", FormPet.Held);
				if (pet != null)
				{
					int x1 = pet.Left;
					await Task.Delay(500);
					Check("dialog open: the pet does not walk", pet.Left == x1 || pet.StillAnimation < 0, $"{pet.CurrentAnimationName}, moved {pet.Left - x1}px");
				}
				d.Close();
			}
			await Task.Delay(200);
			Check("dialog closed: pets free again", !FormPet.Held);
		}

		/// <summary>Falling from a window that went away: a pet in the air cannot stand still until it lands.</summary>
		static bool InAir(FormPet pet) => (pet.CurrentAnimationName ?? "").IndexOf("fall", StringComparison.OrdinalIgnoreCase) >= 0;

		/// <summary>The chat above the pet: opens, holds the pets, answers here (reminder, help, meeting) and closes.</summary>
		static async Task ChatBox()
		{
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			FormChat.Open();
			await Task.Delay(400);
			var chat = Application.OpenForms.OfType<FormChat>().FirstOrDefault();
			Check("chat: opens", chat != null && chat.Visible);
			if (chat == null) return;
			Check("chat: the pets wait while it is open", FormPet.Held);
			var pet = Pet();
			if (pet != null) Check("chat: next to the pet", Math.Abs((chat.Left + chat.Width / 2) - (pet.Left + pet.Width / 2)) < chat.Width && (chat.Bottom <= pet.Top + 2 || chat.Top >= pet.Bottom - 2), $"chat {chat.Bounds}, pet {pet.Bounds}");
			chat.Refresh();
			Check("chat: suggestions to tap", FormChat.Messages.Count > 0 || chat.TestClickable >= 4, chat.TestClickable.ToString());

			int before = Reminders.All.Count;
			var r1 = await FormChat.Answer("me lembra de regar as plantas às 23h");
			var added = Reminders.All.Skip(before).ToList();
			Check("chat: a reminder written as a sentence is created", added.Count == 1 && added[0].Text == "Regar as plantas" && r1.ActionText != null, r1.Text);
			foreach (var r in added) Reminders.Remove(r);
			var help = await FormChat.Answer("o que você sabe fazer?");
			Check("chat: says what it can do (no AI)", help.Text == Lang.T("chat.help"));
			var sys = await FormChat.Answer("como está a memória do computador?");
			Check("chat: the computer, answered here", sys.Text.Contains("%"), sys.Text);
			if (!AiAssistant.Configured)
			{
				var free = await FormChat.Answer("qual a capital da França?");
				Check("chat: free question without AI offers to set it up", free.Text == Lang.T("chat.needAi") && free.Action != null);
			}
			else lines.Add("SKIP  chat: free question without AI (an AI is set up here)");

			chat.Send("o que você sabe fazer?");
			for (int i = 0; i < 30 && chat.TestBusy; i++) await Task.Delay(100);
			Check("chat: the conversation shows question and answer", FormChat.Messages.Count >= 2 && FormChat.Messages[FormChat.Messages.Count - 2].User && !FormChat.Messages.Last().User);
			chat.Close();
			await Task.Delay(200);
			Check("chat: closed, pets free again", !FormChat.IsOpen && !FormPet.Held);
		}

		/// <summary>Configured AI providers: saved, switched and removed; the user's own are put back afterwards.</summary>
		static void AiProviders()
		{
			string savedList = Program.MyData.GetSetting("AiProfiles"), savedActive = AiProfiles.ActiveId;
			bool savedDone = Program.MyData.GetSettingBool("AiProfilesDone", false);
			var savedNow = AiProfiles.Snapshot();
			try
			{
				Program.MyData.SetSetting("AiProfiles", ""); AiProfiles.ActiveId = "";
				Program.MyData.SetSetting("AiProfilesDone", true);
				AiProfiles.Restore(("", "", "", ""));
				Check("ai providers: none at first", AiProfiles.All.Count == 0 && !AiAssistant.Configured);
				AiAssistant.ProviderId = "ollama"; AiAssistant.BaseUrl = "http://localhost:11434/v1"; AiAssistant.Model = "llama3.2"; AiAssistant.Key = "";
				AiProfiles.SaveCurrent(asNew: true);
				AiAssistant.ProviderId = "groq"; AiAssistant.BaseUrl = "https://api.groq.com/openai/v1"; AiAssistant.Model = "llama-3.3-70b-versatile"; AiAssistant.Key = "chave-de-teste";
				AiProfiles.SaveCurrent(asNew: true);
				var all = AiProfiles.All;
				Check("ai providers: two saved, the last in use", all.Count == 2 && AiProfiles.ActiveId == all[1].Id && AiAssistant.ProviderId == "groq");
				Check("ai providers: the key stays encrypted in the list", !Program.MyData.GetSetting("AiProfiles").Contains("chave-de-teste") && all[1].KeyBlob.Length > 0);
				AiProfiles.Use(all[0]);
				Check("ai providers: switching uses the other one", AiAssistant.ProviderId == "ollama" && AiAssistant.Model == "llama3.2" && AiAssistant.Key == "");
				AiProfiles.Use(all[1]);
				Check("ai providers: and back, with its key", AiAssistant.Key == "chave-de-teste");
				AiProfiles.Remove(all[1]);
				Check("ai providers: removing the one in use falls back to the other", AiProfiles.All.Count == 1 && AiAssistant.ProviderId == "ollama");
				AiProfiles.Remove(AiProfiles.All[0]);
				Check("ai providers: none left, nothing in use", AiProfiles.All.Count == 0 && !AiAssistant.Configured);
			}
			finally
			{
				Program.MyData.SetSetting("AiProfiles", savedList); AiProfiles.ActiveId = savedActive;
				Program.MyData.SetSetting("AiProfilesDone", savedDone);
				AiProfiles.Restore(savedNow);
			}
		}

		/// <summary>The settings window: every page draws, the cards switch features, old pages lead to new ones.</summary>
		static void SettingsPages()
		{
			foreach (var (name, ok, detail) in FormSettings.SelfCheck()) Check("settings: " + name, ok, detail);
			int before = DayStats.Get("selftest");
			DayStats.Add("selftest", 2);
			Check("today counters: added", DayStats.Get("selftest") == before + 2);
		}

		/// <summary>Weather: texts from the codes, and the real Open-Meteo (skipped without internet).</summary>
		static async Task WeatherCheck()
		{
			Check("weather: codes have texts", new[] { 0, 2, 3, 45, 53, 63, 73, 81, 95 }.All(c => Lang.T(Weather.Describe(c).key) != Weather.Describe(c).key));
			var t = new Weather.Today { Code = 63, Min = 18, Max = 26, RainChance = 80 };
			string s = Weather.Summary(t, new Weather.Place { Name = "São Paulo, SP, BR" });
			Check("weather: summary with the umbrella tip", s.Contains("São Paulo") && s.Contains("18") && s.Contains("26") && s.Contains("80") && !s.Contains("{"), s);
			try
			{
				var place = await Weather.FindCity("Curitiba");
				Check("weather online: city found", place != null && Math.Abs(place.Lat + 25.4) < 1 && Math.Abs(place.Lon + 49.3) < 1, place?.Name + " " + place?.Lat + "," + place?.Lon);
				if (place != null)
				{
					var w = await Weather.Load(place);
					Check("weather online: forecast read", w != null && w.Max >= w.Min && w.Max < 60 && w.Min > -40 && w.RainChance >= 0 && w.RainChance <= 100, w == null ? "" : $"{w.Min}..{w.Max} code {w.Code} rain {w.RainChance}% soon {w.RainSoon}%");
				}
			}
			catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
			{
				lines.Add("SKIP  weather online: no internet (" + ex.Message + ")");
			}
		}

		/// <summary>Two pets chat: the first says a line, the other answers in its own bubble a moment later.</summary>
		static async Task PetsChat()
		{
			bool meetWas = PetsMeet.Enabled;
			PetsMeet.Enabled = false;   // no meeting on its own during the check
			try
			{
				var first = Lang.T("chat.first").Split('|');
				Check("chat: lines and answers match", first.Length >= 5 && first.Length == Lang.T("chat.answer").Split('|').Length, first.Length.ToString());
				foreach (var x in Application.OpenForms.OfType<SpeechBubble>().ToList()) x.Close();
				var before = Program.Mainthread.sheeps.Where(p => p != null && !p.IsDisposed).ToList();
				Program.Mainthread.AddSheep();
				var b = Program.Mainthread.sheeps.FirstOrDefault(p => p != null && !p.IsDisposed && !before.Contains(p));
				var a = Pet();
				if (a == null || b == null) { lines.Add("SKIP  chat: no room for a second pet"); return; }
				for (int i = 0; i < 40 && b.Opacity <= 0; i++) await Task.Delay(100);
				await Task.Delay(1500);
				foreach (var x in Application.OpenForms.OfType<SpeechBubble>().ToList()) x.Close();   // the new pet's "hello"
				await Task.Delay(200);
				PetsMeet.ResetChat();
				bool talked = PetsMeet.Talk(a, b);
				Check("chat: the first pet says something", talked && Application.OpenForms.OfType<SpeechBubble>().Count(x => x.Visible) == 1);
				Check("chat: the pets stop while they chat", FormPet.Held);
				Check("chat: one conversation at a time", !PetsMeet.Talk(b, a));
				await Task.Delay(2600);
				Check("chat: the other pet answers in its own bubble", Application.OpenForms.OfType<SpeechBubble>().Count(x => x.Visible) == 2,
					string.Join(" / ", Application.OpenForms.OfType<SpeechBubble>().Where(x => x.Visible).Select(x => x.Said)));
				await Task.Delay(4500);
				Check("chat: the bubbles go away by themselves", !Application.OpenForms.OfType<SpeechBubble>().Any(x => x.Visible) && !FormPet.Held);
				SpeechBubble.Say("teste", 5);
				PetsMeet.ResetChat();
				Check("chat: not over a bubble talking to the user", !PetsMeet.Talk(a, b));
				foreach (var x in Application.OpenForms.OfType<SpeechBubble>().ToList()) x.Close();
				if (!Program.Mainthread.KillSheep(b)) b.Close();
				await Task.Delay(1500);
			}
			finally { PetsMeet.Enabled = meetWas; PetsMeet.ResetChat(); }
		}

		/// <summary>Breaks, coming back and long programs, with a made-up clock (every 30 s, like the real timer).</summary>
		static void ActivityCare()
		{
			Activity.Reset();
			var t = new DateTime(2026, 11, 11, 9, 0, 0);
			string said = null;
			int when = -1;
			// 100 minutes of work without a pause: the break is suggested once, at 90 minutes
			for (int i = 0; i <= 200; i++)
			{
				string s = Activity.Step(t.AddSeconds(i * 30), 5, false);
				if (s != null && said == null) { said = s; when = i / 2; }
				else if (s != null) { Check("activity: break said only once", false, s + " at " + i / 2 + " min"); break; }
			}
			Check("activity: break after 90 minutes without a pause", said == "break" && when == Activity.BreakAfter, said + " at " + when + " min");

			// a 6-minute pause starts again: no break 80 minutes later
			Activity.Reset();
			bool breakSaid = false;
			for (int i = 0; i <= 120; i++) breakSaid |= Activity.Step(t.AddMinutes(i), 5, false) == "break";
			for (int i = 0; i < 6; i++) Activity.Step(t.AddMinutes(121 + i), 600, false);   // away: idle
			Activity.Reset();
			var t2 = t.AddMinutes(200);
			for (int i = 0; i < 60; i++) Activity.Step(t2.AddMinutes(i), 5, false);
			for (int i = 0; i < 6; i++) Activity.Step(t2.AddMinutes(60 + i), 400, false);
			bool afterPause = false;
			for (int i = 0; i <= 80; i++) afterPause |= Activity.Step(t2.AddMinutes(66 + i), 5, false) == "break";
			Check("activity: a 5-minute pause counts as a break", !afterPause);
			Check("activity: during Pomodoro the pet does not suggest breaks", Enumerable.Range(0, 200).All(i => { if (i == 0) Activity.Reset(); return Activity.Step(t.AddMinutes(i), 5, true) != "break"; }));

			// coming back after 40 minutes away (sleep, lock or just out)
			Activity.Reset();
			Activity.Step(t, 5, false);
			Check("activity: back after 40 min away", Activity.Step(t.AddMinutes(40), 5, false) == "welcome");
			Check("activity: back after 10 min is nothing special", Activity.Step(t.AddMinutes(50), 5, false) == null);

			// a program used 2h10 today and then closed: celebrated once; a short one: nothing
			Activity.Reset();
			Activity.Track(111, "Excel", 130 * 60, t);
			Activity.Track(222, "Bloco de notas", 10 * 60, t);
			Check("activity: still open, nothing yet", Activity.EndedApp(t, pid => true) == null);
			string ended = Activity.EndedApp(t, pid => pid != 111);
			Check("activity: long program closed is celebrated", ended == "app|Excel|2h10", ended);
			Check("activity: short program closed is not", Activity.EndedApp(t, pid => false) == null);
			Activity.Reset();
		}

		/// <summary>Reminders written as a sentence, read on this computer (fixed "now": Wednesday 14:10).</summary>
		static void SmartReminders()
		{
			var now = new DateTime(2026, 11, 11, 14, 10, 0);
			void T(string sentence, Reminder.Kinds kind, string when, string text)
			{
				var r = SmartReminder.Parse(sentence, now);
				string got = r == null ? "(not understood)"
					: r.Kind + " " + (r.Kind == Reminder.Kinds.Once ? r.At.ToString("dd HH:mm") : r.Kind == Reminder.Kinds.Daily ? r.Time : r.Minutes + "min") + " \"" + r.Text + "\"";
				string want = kind + " " + when + " \"" + text + "\"";
				Check("smart reminder: " + sentence, got == want, got + "  (wanted " + want + ")");
			}
			T("me lembra de ligar para a Ana às 15h", Reminder.Kinds.Once, "11 15:00", "Ligar para a Ana");
			T("Me lembre daqui a 20 minutos de tirar o bolo do forno", Reminder.Kinds.Once, "11 14:30", "Tirar o bolo do forno");
			T("lembrete amanhã às 9:30 reunião com o time", Reminder.Kinds.Once, "12 09:30", "Reunião com o time");
			T("me lembra às 3 de buscar as crianças", Reminder.Kinds.Once, "11 15:00", "Buscar as crianças");
			T("me lembra às 10 de pagar a conta", Reminder.Kinds.Once, "11 22:00", "Pagar a conta");
			T("todo dia às 8 tomar o remédio", Reminder.Kinds.Daily, "08:00", "Tomar o remédio");
			T("a cada 30 minutos me lembra de alongar", Reminder.Kinds.Interval, "30min", "Alongar");
			T("me lembra em meia hora de beber água", Reminder.Kinds.Once, "11 14:40", "Beber água");
			T("remind me to call mom at 6pm", Reminder.Kinds.Once, "11 18:00", "Call mom");
			T("remind me in 2 hours to stretch", Reminder.Kinds.Once, "11 16:10", "Stretch");
			T("recuérdame mañana a las 8 llamar al médico", Reminder.Kinds.Once, "12 08:00", "Llamar al médico");
			Check("smart reminder: no time, not understood", SmartReminder.Parse("me lembra de comprar pão", now) == null);
			Check("smart reminder: a question is not a reminder", !SmartReminder.LooksLikeReminder("por que o céu é azul?") && SmartReminder.LooksLikeReminder("me lembra de comprar pão às 18h"));
		}

		/// <summary>The eSheep that comes with the program turns to face you while it talks (and turns back after).</summary>
		static void SheepTalk()
		{
			var ser = new System.Xml.Serialization.XmlSerializer(typeof(XmlData.RootNode));
			XmlData.RootNode root;
			using (var r = new StringReader(BuiltInPets.GetXml("esheep64"))) root = (XmlData.RootNode)ser.Deserialize(r);
			var talk = root.Animations.Animation.FirstOrDefault(a => a.Name == "talk");
			Check("eSheep: has a talk animation", talk != null);
			if (talk == null) return;
			Check("eSheep talk: stays in place", talk.Start.X == "0" && talk.End.X == "0" && talk.Start.Y == "0" && talk.End.Y == "0", $"{talk.Start.X},{talk.Start.Y}");
			// counted like the program does: frames + (frames - repeatfrom) × repeat; the repeated frames are repeatfrom…end
			int f = talk.Sequence.Frame.Length, rf = talk.Sequence.RepeatFromFrame, rep = int.TryParse(talk.Sequence.RepeatCount, out int rr) ? rr : 0;
			int steps = f + (f - rf) * rep;
			var held = talk.Sequence.Frame.Skip(rf).Distinct().ToList();
			Check("eSheep talk: stays looking at you (the front frame repeats)", steps >= 50 && held.Count == 1 && held[0] == 10, $"{steps} steps, repeats {string.Join(",", held)}");
			var back = root.Animations.Animation.FirstOrDefault(a => talk.Sequence.Next != null && a.Id == talk.Sequence.Next[0].Value);
			Check("eSheep talk: then turns back", back?.Name == "rotate1b", back?.Name);
		}

		/// <summary>Emoji in colour: the same drawing call as the bubbles and the panel, on a picture; saved next to the result.</summary>
		static void ColorEmoji()
		{
			Check("emoji: found in text", ColorText.HasEmoji("Hora de beber água! 💧") && ColorText.HasEmoji("Novo e-mail ✉️") && !ColorText.HasEmoji("Fechar ✕") && !ColorText.HasEmoji("Olá"));
			using (var font = Theme.CreateFont(16f, System.Drawing.FontStyle.Regular))
			using (var bmp = new System.Drawing.Bitmap(360, 120))
			using (var g = System.Drawing.Graphics.FromImage(bmp))
			{
				g.Clear(System.Drawing.Color.White);
				string text = "Mandou bem! 🎉 Hora de beber água 💧 ❤️ ☀️ ✅";
				var size = Theme.MeasureText(g, text, font, 340, TextFormatFlags.WordBreak);
				Check("emoji: text measured", size.Width > 50 && size.Height > 10, size.ToString());
				Theme.DrawText(g, text, font, new System.Drawing.Rectangle(10, 10, 340, size.Height), System.Drawing.Color.Black, TextFormatFlags.WordBreak);
				Theme.DrawButton(g, new System.Drawing.Rectangle(10, 70, 160, 34), "😄 Ótimo", font, true, false);
				int colourful = 0;
				for (int y = 0; y < bmp.Height; y++)
					for (int x = 0; x < bmp.Width; x++)
					{
						var c = bmp.GetPixel(x, y);
						if (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) > 80 && y < 60) colourful++;
					}
				Check("emoji: drawn in colour", colourful > 100, colourful + " coloured pixels");
				try { bmp.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultFile)) ?? ".", "emoji.png")); } catch (Exception) { }
			}
		}

		/// <summary>The first run: the pet comes down from the sky and lands, then the introduction can start.</summary>
		static async Task SkyArrival()
		{
			var before = Program.Mainthread.sheeps.Where(p => p != null && !p.IsDisposed).ToList();
			Onboarding.ForceSky = true;
			Program.Mainthread.AddSheep();
			var pet = Program.Mainthread.sheeps.FirstOrDefault(p => p != null && !p.IsDisposed && !before.Contains(p));
			if (pet == null) { lines.Add("SKIP  sky: no room for one more pet"); Onboarding.ForceSky = false; return; }
			var area = Screen.FromControl(pet).WorkingArea;
			Check("sky: the new pet starts above the screen", pet.Top < area.Top, $"top {pet.Top}, screen top {area.Top}");
			bool landed = false;
			for (int i = 0; i < 100 && !landed; i++) { await Task.Delay(150); landed = pet.OnGround && pet.Top > area.Top; }
			Check("sky: it lands", landed, $"{pet.CurrentAnimationName} at {pet.Bounds}");
			FormPet.Hold(true);
			// a landing bounce may still move it a little: once it stands, it must stay
			for (int i = 0; i < 20 && !pet.StandingStill && pet.StillAnimation >= 0; i++) await Task.Delay(150);
			int x = pet.Left, y = pet.Top;
			await Task.Delay(1500);
			Check("sky: held after landing, it neither walks nor sinks", pet.Left == x && pet.Top == y && (pet.StandingStill || pet.StillAnimation < 0), $"{pet.CurrentAnimationName}, moved {pet.Left - x},{pet.Top - y}");
			FormPet.Hold(false);
			if (!Program.Mainthread.KillSheep(pet)) pet.Close();
			await Task.Delay(1500);
		}

		static async Task Onboard()
		{
			bool done = Program.MyData.GetSettingBool("TourDone", false);
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			Onboarding.Tour();
			Check("tour: the pet offers to show what it does", Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
			Check("tour: the pets stay still during the tour", Onboarding.Holding && FormPet.Held);
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			Check("tour: closing it does not mark it as seen", Program.MyData.GetSettingBool("TourDone", false) == done);
			await Task.Delay(2600);
			Check("tour: bubbles gone, the pets are let go", !Onboarding.Holding);
			Onboarding.StopHolding();
			// the text of this version, or of the next one (the version number goes up when it is released)
			var cur = Updater.CurrentVersion;
			Check("what's new: text for this (or the next) version", Lang.Keys.Any(k => k.StartsWith("whatsnew.") && Version.TryParse(k.Substring(9), out var v) && v >= new Version(cur.Major, cur.Minor, cur.Build)), cur.ToString(3));
			// the ask shortcut: when one is taken by another program, another one can be chosen and works
			bool wasOn = HideHotkey.AskEnabled; int wasKey = HideHotkey.AskKey;
			try
			{
				Program.MyData.SetSetting("AskHotkey", true);
				var free = Enumerable.Range(0, HideHotkey.AskKeyCount).Where(k => { HideHotkey.AskKey = k; return !HideHotkey.AskBusy; }).ToList();
				var taken = Enumerable.Range(0, HideHotkey.AskKeyCount).Except(free).Select(HideHotkey.AskKeyText).ToList();
				Check("hotkey: at least one ask shortcut is free", free.Count > 0, "taken: " + string.Join(", ", taken));
				if (taken.Count > 0) lines.Add("INFO  hotkey: taken by another program here: " + string.Join(", ", taken));
				Check("hotkey: names in the language", HideHotkey.AskKeyText(0).EndsWith(Lang.T("key.space")), HideHotkey.AskKeyText(0));
			}
			finally { Program.MyData.SetSetting("AskHotkey", wasOn); HideHotkey.AskKey = wasKey; }
		}

		static string Placeholders(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s ?? "", @"\{(\d+)").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));

		static void Games()
		{
			foreach (var key in new[] { "fun.fortunes", "fun.ballAnswers", "fun.jokes" })
				Check("games: " + key, Lang.T(key).Split('|').Count(s => s.Trim().Length > 0) >= 5);
		}

		// ------------------------------------------------------------------ backup

		static void Backup_Export()
		{
			string file = Path.Combine(Path.GetTempPath(), "selftest" + PetBackup.Extension);
			int n = PetBackup.Export(file);
			using (var zip = ZipFile.OpenRead(file))
			{
				int xmls = zip.Entries.Count(e => e.FullName.EndsWith("/animations.xml"));
				Check("export: every downloaded pet", xmls == n && n == PetStore.Folders().Count, $"{xmls} in zip, {n} exported, {PetStore.Folders().Count} on disk");
				Check("export: settings inside", zip.GetEntry("settings.txt") != null);
			}
			File.Delete(file);
		}

		static void Backup_ImportSafety()
		{
			string file = Path.Combine(Path.GetTempPath(), "selftest-bad" + PetBackup.Extension);
			if (File.Exists(file)) File.Delete(file);
			using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
			{
				void Add(string name, string text) { using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write(text); }
				Add("pets/../evil/animations.xml", "<animations><image/></animations>");
				Add("pets/not a name!/animations.xml", "<animations><image/></animations>");
				Add("pets/broken/animations.xml", "this is not xml");
				Add("pets/selftest_ok/animations.xml", "<animations xmlns=\"https://esheep.petrucci.ch/\"><image/><animations/></animations>");
				Add("settings.txt", "current=selftest_ok\nmuted=selftest_ok;esheep64_not_imported");
			}
			bool esheepMutedBefore = Program.MyData.IsPetMuted("esheep64");
			var (imported, current) = PetBackup.Import(file);
			Check("import: only the valid pet, nothing outside the store", imported.Count == 1 && imported[0] == "selftest_ok"
				&& !Directory.Exists(Path.Combine(Path.GetTempPath(), "evil")), string.Join(",", imported));
			Check("import: pet in use of the other computer", current == "selftest_ok");
			Check("import: sound setting of the imported pet", Program.MyData.IsPetMuted("selftest_ok"));
			// clean up
			PetStore.Delete("selftest_ok");
			Program.MyData.SetPetMuted("selftest_ok", false);
			Check("import: other settings untouched", Program.MyData.IsPetMuted("esheep64") == esheepMutedBefore);
			File.Delete(file);
		}

		// ------------------------------------------------------------------ windows

		static FormPet Pet() => Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow);

		static async Task Bubble()
		{
			var pet = Pet();
			Check("bubble: a pet is on screen", pet != null);
			if (pet == null) return;
			var before = GetForegroundWindow();
			SpeechBubble.Say("self-test", 3);
			await Task.Delay(300);
			Check("bubble: the window in front stays in front", GetForegroundWindow() == before);
			var b = Application.OpenForms.OfType<SpeechBubble>().FirstOrDefault();
			Check("bubble: shown", b != null);
			if (b == null) return;
			var wa = Screen.FromControl(pet).WorkingArea;
			Check("bubble: inside the screen", wa.Contains(b.Bounds), $"{b.Bounds} / {wa}");
			Check("bubble: next to the pet", b.Bottom <= pet.Top + 2 || b.Top >= pet.Bottom - 2, $"bubble {b.Bounds}, pet {pet.Bounds}");
			Check("bubble: is not the active window", GetForegroundWindow() != b.Handle);
			Check("bubble: always on top", (GetWindowLong(b.Handle, -20) & 0x8) != 0);
			await Task.Delay(3600);
			Check("bubble: closes by itself", b.IsDisposed || !b.Visible);
		}

		static async Task Hide()
		{
			HideHotkey.SetHidden(true);
			await Task.Delay(200);
			Check("hide: no pet window visible", !Application.OpenForms.OfType<FormPet>().Any(p => p.Visible));
			SpeechBubble.Say("hidden", 2);
			await Task.Delay(200);
			Check("hide: no bubble while hidden", !Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
			// a pet that opens a second window (balloon, plane…) while hidden: that window stays hidden too
			var hiddenPet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => !p.IsChildWindow);
			int childAnim = hiddenPet?.TestChildAnimation() ?? -1;
			if (childAnim >= 0)
			{
				hiddenPet.TestPlay(childAnim);
				await Task.Delay(800);
				Check("hide: a new balloon/plane window stays hidden", !Application.OpenForms.OfType<FormPet>().Any(p => p.Visible),
					string.Join(", ", Application.OpenForms.OfType<FormPet>().Where(p => p.Visible).Select(p => p.Name)));
			}
			else lines.Add("SKIP  hide: the pet in use opens no second window (adopt Boo or Rex to check it)");
			HideHotkey.SetHidden(false);
			await Task.Delay(300);
			Check("hide: pets back", Pet() != null);
		}

		static async Task Menu()
		{
			var pet = Pet();
			if (pet == null) { Check("menu: a pet is on screen", false); return; }
			var cm = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
			await Task.Delay(200);
			var texts = cm.Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();
			foreach (var key in new[] { "menu.games", "tray.reminders", "menu.shortcuts", "menu.mood", "menu.monitor", "menu.sendAway" })
				Check("menu: " + key, texts.Contains(Lang.T(key)), string.Join(" | ", texts));
			var games = cm.Items.OfType<ToolStripMenuItem>().First(i => i.Text == Lang.T("menu.games"));
			Check("menu: 3 games", games.DropDownItems.Count == 3);
			Check("menu: inside the screen", Screen.FromControl(pet).Bounds.Contains(cm.Bounds), cm.Bounds.ToString());
			cm.Close();

			// click every game: each one makes the pet say something
			foreach (var key in new[] { "menu.fortune", "menu.ball", "menu.joke" })
			{
				var menu = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
				var g = menu.Items.OfType<ToolStripMenuItem>().First(i => i.Text == Lang.T("menu.games"));
				var item = g.DropDownItems.OfType<ToolStripMenuItem>().First(i => i.Text == Lang.T(key));
				menu.Close();
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
				item.PerformClick();
				await Task.Delay(300);
				Check("menu click: " + key + " → the pet talks", Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
			}
			// the magic ball: its "Shake" button gives an answer
			var ball = Application.OpenForms.OfType<SpeechBubble>().FirstOrDefault();
			ball?.Close();

			// focus from the menu
			var m2 = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
			var focusItem = m2.Items.OfType<ToolStripMenuItem>().First(i => i.Text == Lang.T("menu.focus"));
			var start = focusItem.DropDownItems.OfType<ToolStripMenuItem>().First();
			m2.Close();
			start.PerformClick();
			Check("menu click: start focusing", Pomodoro.Current == Pomodoro.Phase.Focus);
			var m3 = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
			var running = m3.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == Pomodoro.Status());
			Check("menu: shows the time left while focusing", running != null, string.Join(" | ", m3.Items.OfType<ToolStripMenuItem>().Select(i => i.Text)));
			var stop = running?.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == Lang.T("menu.focusStop"));
			m3.Close();
			stop?.PerformClick();
			Check("menu click: stop focusing", Pomodoro.Current == Pomodoro.Phase.Off);

			// the mood question shows the 5 faces
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			Mood.Ask();
			await Task.Delay(300);
			Check("mood: question shown", Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();

			// PC monitor on and off from the menu (the setting is put back)
			bool monitorWas = PcMonitor.Enabled;
			PcMonitor.Enabled = true;
			await Task.Delay(2500);
			// shown only with the mouse on the pet: move the mouse away, then onto the pet, then back where it was
			var mouse = Cursor.Position;
			var area = Screen.FromControl(pet).WorkingArea;
			Cursor.Position = new System.Drawing.Point(pet.Left > area.Left + 200 ? area.Left + 20 : area.Right - 20, area.Top + 20);
			await Task.Delay(300);
			Check("monitor: hidden while the mouse is away", !Application.OpenForms.Cast<Form>().Any(f => f.GetType().Name == "Hud" && f.Visible));
			// a pet still walking in from the edge of the screen cannot be pointed at: let it come in first
			for (int i = 0; i < 40 && !(pet.Left >= area.Left && pet.Right <= area.Right); i++) await Task.Delay(150);
			bool plate = false;
			for (int i = 0; i < 8 && !plate; i++)   // the pet may be walking or falling: follow it
			{
				Cursor.Position = new System.Drawing.Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2);
				await Task.Delay(150);
				plate = Application.OpenForms.Cast<Form>().Any(f => f.GetType().Name == "Hud" && f.Visible);
			}
			Check("monitor: plate when the mouse is on the pet", plate, $"pet at {pet.Bounds}, mouse at {Cursor.Position}");
			Cursor.Position = mouse;
			Check("monitor: real numbers", PcMonitor.Cpu >= 0 && PcMonitor.Cpu <= 100 && PcMonitor.Ram > 0 && PcMonitor.Ram <= 100, $"CPU {PcMonitor.Cpu} RAM {PcMonitor.Ram}");
			PcMonitor.Enabled = false;
			Check("monitor: plate gone when off", !Application.OpenForms.Cast<Form>().Any(f => f.GetType().Name == "Hud" && f.Visible));
			PcMonitor.Enabled = monitorWas;
		}

		static async Task PomodoroCycle()
		{
			Pomodoro.StartFocus();
			Check("pomodoro: focus started", Pomodoro.Current == Pomodoro.Phase.Focus && Pomodoro.Left.TotalMinutes > Pomodoro.FocusMinutes - 1);
			Pomodoro.TogglePause();
			var left = Pomodoro.Left;
			await Task.Delay(1200);
			Check("pomodoro: paused time stands still", Pomodoro.Paused && Pomodoro.Left == left);
			Pomodoro.TogglePause();
			for (int i = 1; i <= 4; i++)
			{
				Pomodoro.TestFinishPhase();   // focus → break
				var expected = i == 4 ? Pomodoro.Phase.LongBreak : Pomodoro.Phase.ShortBreak;
				Check($"pomodoro: break after focus {i}", Pomodoro.Current == expected, Pomodoro.Current.ToString());
				Pomodoro.TestFinishPhase();   // break → focus
				Check($"pomodoro: focus again after break {i}", Pomodoro.Current == Pomodoro.Phase.Focus, Pomodoro.Current.ToString());
			}
			Check("pomodoro: status text", Pomodoro.Status().Contains(":"));
			Pomodoro.Stop();
			Check("pomodoro: stopped", Pomodoro.Current == Pomodoro.Phase.Off);
			await Task.Delay(300);
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
		}

		// ------------------------------------------------------------------ agenda

		static void AgendaParse()
		{
			string ics = string.Join("\r\n", new[]
			{
				"BEGIN:VCALENDAR", "VERSION:2.0",
				"BEGIN:VEVENT", "UID:utc", "DTSTART:20261116T130000Z", "SUMMARY:Café\\, conversa", "END:VEVENT",
				"BEGIN:VEVENT", "UID:tz", "DTSTART;TZID=America/Sao_Paulo:20261117T100000", "SUMMARY:Reunião com", "  o time", "END:VEVENT",
				"BEGIN:VEVENT", "UID:allday", "DTSTART;VALUE=DATE:20261118", "SUMMARY:Feriado", "END:VEVENT",
				"BEGIN:VEVENT", "UID:weekly", "DTSTART:20261102T090000", "RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR", "EXDATE:20261118T090000", "SUMMARY:Daily", "END:VEVENT",
				"BEGIN:VEVENT", "UID:weekly", "RECURRENCE-ID:20261120T090000", "DTSTART:20261120T110000", "SUMMARY:Daily (movida)", "END:VEVENT",
				"BEGIN:VEVENT", "UID:cancel", "DTSTART:20261119T150000", "STATUS:CANCELLED", "SUMMARY:Cancelada", "END:VEVENT",
				"BEGIN:VEVENT", "UID:count", "DTSTART:20261115T080000", "RRULE:FREQ=DAILY;COUNT=2", "SUMMARY:Duas vezes", "END:VEVENT",
				"END:VCALENDAR",
			});
			var list = Agenda.Parse(ics, new DateTime(2026, 11, 16), new DateTime(2026, 11, 23));
			string all = string.Join(" | ", list.Select(e => e.Start.ToString("ddd dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " " + e.Title));
			Check("agenda: 5 events (all-day, cancelled, removed and moved ones handled)", list.Count == 5, all);
			Check("agenda: UTC time converted", list.Any(e => e.Uid == "utc" && e.Start == new DateTime(2026, 11, 16, 13, 0, 0, DateTimeKind.Utc).ToLocalTime()), all);
			var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
			Check("agenda: time zone converted", list.Any(e => e.Uid == "tz" && e.Start == TimeZoneInfo.ConvertTime(new DateTime(2026, 11, 17, 10, 0, 0), sp, TimeZoneInfo.Local)), all);
			Check("agenda: long line joined, escapes removed", list.Any(e => e.Title == "Reunião com o time") && list.Any(e => e.Title == "Café, conversa"), all);
			Check("agenda: weekly repeat, removed day skipped", list.Count(e => e.Uid == "weekly") == 2 && list.Any(e => e.Uid == "weekly" && e.Start == new DateTime(2026, 11, 16, 9, 0, 0)), all);
			Check("agenda: moved occurrence at its new time", list.Any(e => e.Title == "Daily (movida)" && e.Start.Hour == 11) && !list.Any(e => e.Uid == "weekly" && e.Start == new DateTime(2026, 11, 20, 9, 0, 0)), all);
			Check("agenda: COUNT respected", list.Count(e => e.Uid == "count") == 1, all);
			Check("agenda: no all-day or cancelled events", !list.Any(e => e.Uid == "allday" || e.Uid == "cancel"), all);
		}

		/// <summary>
		/// "Ask the pet" against a fake AI service on this computer that speaks the OpenAI format (like Ollama,
		/// Gemini, Groq and OpenRouter do): models, the right and the wrong key, the answer in the bubble.
		/// The real AI settings are put back afterwards.
		/// </summary>
		static async Task AskThePet()
		{
			var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
			listener.Start();
			int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
			string lastBody = "";
			var cts = new System.Threading.CancellationTokenSource();
			_ = Task.Run(async () =>
			{
				while (!cts.IsCancellationRequested)
				{
					System.Net.Sockets.TcpClient c;
					try { c = await listener.AcceptTcpClientAsync(cts.Token); } catch (Exception) { return; }
					using (c)
					{
						var s = c.GetStream();
						var buf = new byte[65536];
						int n = await s.ReadAsync(buf, 0, buf.Length);
						string req = Encoding.UTF8.GetString(buf, 0, n);
						int headEnd = req.IndexOf("\r\n\r\n");
						var len = System.Text.RegularExpressions.Regex.Match(req, @"Content-Length: (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
						int want = len.Success ? int.Parse(len.Groups[1].Value) : 0;
						while (headEnd >= 0 && Encoding.UTF8.GetByteCount(req) - (headEnd + 4) < want)
						{
							int m = await s.ReadAsync(buf, 0, buf.Length);
							if (m == 0) break;
							req += Encoding.UTF8.GetString(buf, 0, m);
						}
						bool keyOk = req.Contains("Authorization: Bearer chave-certa");
						string status = "200 OK", body;
						if (!keyOk) { status = "401 Unauthorized"; body = "{\"error\":{\"message\":\"invalid key\"}}"; }
						else if (req.StartsWith("GET /v1/models")) body = "{\"data\":[{\"id\":\"modelo-a\"},{\"id\":\"llama3.2\"}]}";
						else { lastBody = req.Substring(headEnd + 4); body = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"<think>hmm</think>**Oi!** Eu sou o pet de teste.\\n# Fim\"}}]}"; }
						var bytes = Encoding.UTF8.GetBytes(body);
						var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
						await s.WriteAsync(head, 0, head.Length);
						await s.WriteAsync(bytes, 0, bytes.Length);
					}
				}
			});

			string url = $"http://127.0.0.1:{port}/v1";
			// keep the real settings
			string pId = AiAssistant.ProviderId, pUrl = AiAssistant.BaseUrl, pModel = AiAssistant.Model, pKey = AiAssistant.Key;
			try
			{
				var models = await AiAssistant.ListModels(url, "chave-certa");
				Check("ai: models listed", models.Count == 2, string.Join(",", models));
				Check("ai: preferred model chosen (llama3.2 for Ollama)", AiAssistant.PickModel(AiAssistant.Preset("ollama"), "nao-existe", models) == "llama3.2");
				Check("ai: saved model kept when it exists", AiAssistant.PickModel(AiAssistant.Preset("ollama"), "modelo-a", models) == "modelo-a");

				string answer = await AiAssistant.Ask(url, "modelo-a", "chave-certa", "quem é você?", "Rex");
				Check("ai: answer cleaned for the bubble", answer == "Oi! Eu sou o pet de teste.\nFim", answer);
				var sent = System.Text.Json.Nodes.JsonNode.Parse(lastBody);
				Check("ai: the pet's name and language go in the instructions",
					sent?["messages"]?[0]?["content"]?.GetValue<string>().Contains("Rex") == true && sent?["model"]?.GetValue<string>() == "modelo-a", lastBody.Length.ToString());

				bool refused = false;
				try { await AiAssistant.Ask(url, "modelo-a", "chave-errada", "oi", "Rex"); }
				catch (InvalidOperationException ex) { refused = ex.Message == Lang.T("ai.errKey"); }
				Check("ai: a wrong key gives a clear message", refused);

				bool https = false;
				try { await AiAssistant.ListModels("http://example.com/v1", "x"); } catch (ArgumentException) { https = true; }
				Check("ai: internet services only over https", https);

				// the key is stored encrypted, never in plain text
				AiAssistant.Key = "minha-chave-secreta";
				Check("ai: key stored encrypted", AiAssistant.Key == "minha-chave-secreta" && !Program.MyData.GetSetting("AiKey").Contains("minha-chave"));

				// the whole flow: menu item, thinking, answer in the bubble
				AiAssistant.ProviderId = "custom"; AiAssistant.BaseUrl = url; AiAssistant.Model = "modelo-a"; AiAssistant.Key = "chave-certa";
				Check("ai: configured", AiAssistant.Configured);
				var pet = Pet();
				if (pet != null)
				{
					var cm = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
					Check("ai: 'Ask the pet' in the pet menu once set up", cm.Items.OfType<ToolStripMenuItem>().Any(i => i.Text == Lang.T("menu.ask")));
					cm.Close();
				}
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
				await FormAsk.AskNow("quem é você?");
				await Task.Delay(300);
				Check("ai: the answer appears in the pet's bubble", Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();

				// the conversation: the second question carries the first one and its answer
				AiAssistant.NewConversation();
				await AiAssistant.Ask("primeira pergunta");
				await AiAssistant.Ask("e depois?");
				var conv = System.Text.Json.Nodes.JsonNode.Parse(lastBody)?["messages"]?.AsArray();
				Check("ai: remembers the conversation", conv != null && conv.Count == 4 && conv[1]?["content"]?.GetValue<string>() == "primeira pergunta", conv?.Count.ToString());
				AiAssistant.NewConversation();
				await AiAssistant.Ask("outra conversa");
				conv = System.Text.Json.Nodes.JsonNode.Parse(lastBody)?["messages"]?.AsArray();
				Check("ai: 'new conversation' forgets it", conv != null && conv.Count == 2, conv?.Count.ToString());
				AiAssistant.NewConversation();

				// hovering the pet shows the "Ask" button (like the CPU plate); leaving hides it
				if (pet != null && HoverAsk.Enabled)
				{
					var mouse = Cursor.Position;
					pet.React("walk");
					await Task.Delay(300);
					bool shown = false;
					for (int i = 0; i < 8 && !shown; i++)
					{
						Cursor.Position = new System.Drawing.Point(pet.Left + pet.Width / 2, pet.Top + pet.Height / 2);
						await Task.Delay(150);
						shown = HoverAsk.Showing;
					}
					Check("hover ask: the button appears over the pet", shown, $"pet at {pet.Bounds}, mouse at {Cursor.Position}, busy {pet.Busy}");
					Cursor.Position = new System.Drawing.Point(pet.Left - 400 < 0 ? pet.Right + 400 : pet.Left - 400, Math.Max(0, pet.Top - 300));
					await Task.Delay(1200);
					Check("hover ask: it goes away when the mouse leaves", !HoverAsk.Showing);
					Cursor.Position = mouse;
				}
			}
			finally
			{
				AiAssistant.ProviderId = pId; AiAssistant.BaseUrl = pUrl; AiAssistant.Model = pModel; AiAssistant.Key = pKey;
				cts.Cancel();
				listener.Stop();
			}
		}

		/// <summary>Every name of the action lists exists in that pet's animations (Pets folder of the repository).</summary>
		static void PetActionNames()
		{
			string dir = AppContext.BaseDirectory;
			while (dir != null && !File.Exists(Path.Combine(dir, "Pets", "pets.json"))) dir = Path.GetDirectoryName(dir);
			if (dir == null) { lines.Add("SKIP  pet actions: Pets folder not found"); return; }
			// every pet of the gallery has its own animation to stand still while it talks (not just any animation)
			var noTalk = new List<string>();
			foreach (var petDir in Directory.GetDirectories(Path.Combine(dir, "Pets")))
			{
				string file = Path.Combine(petDir, "animations.xml");
				if (!File.Exists(file)) continue;
				var ser = new System.Xml.Serialization.XmlSerializer(typeof(XmlData.RootNode));
				XmlData.RootNode root;
				using (var r = File.OpenRead(file)) root = (XmlData.RootNode)ser.Deserialize(r);
				bool has = root.Animations.Animation.Any(a => FormPet.StillNames.Contains(a.Name, StringComparer.OrdinalIgnoreCase)
					&& a.Start.X == "0" && a.End.X == "0" && a.Start.Y == "0" && a.End.Y == "0");
				if (!has) noTalk.Add(Path.GetFileName(petDir));
			}
			Check("talk: every gallery pet has a still animation for talking", noTalk.Count == 0, string.Join(", ", noTalk));
			foreach (var folder in PetActions.KnownPets)
			{
				string file = Path.Combine(dir, "Pets", folder, "animations.xml");
				if (!File.Exists(file)) { Check("pet actions: " + folder + " exists", false); continue; }
				var doc = new System.Xml.XmlDocument();
				doc.Load(file);
				var names = new HashSet<string>(doc.SelectNodes("//*[local-name()='animation']/*[local-name()='name']").Cast<System.Xml.XmlNode>().Select(n => n.InnerText), StringComparer.OrdinalIgnoreCase);
				foreach (PetActions.Act act in Enum.GetValues(typeof(PetActions.Act)))
				{
					var own = PetActions.Own(folder, act);
					var missing = own.Where(n => !names.Contains(n)).ToList();
					Check($"pet actions: {folder} {act}", own.Length > 0 && own.Any(names.Contains) && missing.Count == 0, "missing: " + string.Join(", ", missing));
				}
			}
		}

		static async Task TalkingPet()
		{
			var pet = Pet();
			if (pet == null) { Check("talk: a pet is on screen", false); return; }
			void CloseBubbles() { foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close(); }
			CloseBubbles();

			// the bug: the sleep animation was replaced by a wave as soon as the bubble opened
			int sleep = pet.FindAnimation(PetActions.Names(PetActions.Act.Sleep));
			if (sleep >= 0)
			{
				pet.React(PetActions.Names(PetActions.Act.Sleep));
				string sleeping = pet.CurrentAnimationName;
				SpeechBubble.Say("teste", 3);
				await Task.Delay(400);
				Check("talk: a bubble does not cut the sleep animation", pet.CurrentAnimationName == sleeping, pet.CurrentAnimationName);
				CloseBubbles();
			}

			// a pet with a "talk" animation (the eSheep turns to face you) uses it while it talks
			if (pet.FindAnimation("talk") >= 0) Check("talk: the pet faces you while talking (talk animation)", pet.StillAnimation == pet.FindAnimation("talk"), pet.StillAnimation.ToString());
			else lines.Add("SKIP  talk: the pet in use has no talk animation");

			// talking: the pet stops walking
			pet.React("walk");
			await Task.Delay(1700);   // walking on its own (not an action that just started, which would finish first)
			SpeechBubble.Say("parado enquanto falo", 4);
			await Task.Delay(500);
			Check("talk: the pet stands still while talking", pet.StillAnimation < 0 || pet.StandingStill || pet.Busy || InAir(pet), pet.CurrentAnimationName);
			CloseBubbles();

			// the games of the pet menu: the pet stays in place while it says the fortune / answer / joke
			foreach (var (name, game) in new (string, Action)[] { ("fortune cookie", Fun.FortuneCookie), ("joke", Fun.Joke), ("magic ball", Fun.MagicBall) })
			{
				pet.React("walk");
				await Task.Delay(1700);
				game();
				await Task.Delay(700);
				var x1 = pet.Left;
				await Task.Delay(600);
				Check($"talk: {name} — the pet does not walk away", pet.Left == x1 && (pet.StandingStill || pet.StillAnimation < 0 || InAir(pet)), $"{pet.CurrentAnimationName}, moved {pet.Left - x1}px");
				CloseBubbles();
			}

			// the pet menu open: every pet stays still; closed: back to normal
			pet.React("walk");
			await Task.Delay(1700);
			var menu = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0));
			Check("menu open: pets held", FormPet.Held);
			await Task.Delay(300);
			// (in a test the menu may close by itself, as it has no focus; then the pet simply goes on)
			if (menu.Visible) Check("menu open: the pet stops", pet.StandingStill || pet.StillAnimation < 0, pet.CurrentAnimationName);
			else Check("menu open: the pet stopped when the menu opened", pet.StandingStill || pet.StillAnimation < 0, pet.CurrentAnimationName);
			menu.Close();
			await Task.Delay(200);
			CloseBubbles();
			await Task.Delay(200);
			Check("all closed: nothing keeps the pets still", !FormPet.Held);

			// bubble after the animation: not at once, but when it is over
			bool done = false;
			var start = DateTime.Now;
			double after = -1;
			if (pet.ReactThen(() => { done = true; after = (DateTime.Now - start).TotalSeconds; }, "turn", "wave", "blink", "jump", "rotate1a"))
			{
				Check("after animation: not at once", !done);
				for (int i = 0; i < 100 && !done; i++) await Task.Delay(100);
				Check("after animation: runs when the animation ends", done && after > 0.1, after.ToString("0.0") + " s");
			}
			else lines.Add("SKIP  after animation: the pet in use has no short animation to try");
		}

		static async Task QuietTime()
		{
			void CloseBubbles() { foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close(); }
			bool AnyBubble() => Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible);

			// focus: notices wait
			Pomodoro.StartFocus();
			await Task.Delay(200);
			CloseBubbles();
			SpeechBubble.Notify("lembrete de teste", 5);
			SpeechBubble.Notify("reunião de teste", 5);
			await Task.Delay(300);
			Check("focus: notices wait (no bubble)", !AnyBubble() && SpeechBubble.HeldCount == 2, SpeechBubble.HeldCount.ToString());
			Check("focus: pet sounds off", SpeechBubble.QuietNow);
			SpeechBubble.Say("pedido do usuário", 3);
			await Task.Delay(200);
			Check("focus: what the user asks still answers", AnyBubble());
			CloseBubbles();
			Pomodoro.Stop();
			await Task.Delay(300);
			CloseBubbles();
			await Task.Delay(7500);
			var summary = Application.OpenForms.OfType<SpeechBubble>().FirstOrDefault(b => b.Visible);
			Check("focus over: summary of what arrived", summary != null);
			CloseBubbles();

			// Ctrl+Alt+P: silent too (before: the notices became Windows notifications)
			HideHotkey.SetHidden(true);
			SpeechBubble.Notify("aviso escondido", 5);
			await Task.Delay(200);
			Check("hidden: notices wait, nothing on screen", SpeechBubble.HeldCount >= 1 && !AnyBubble());
			HideHotkey.SetHidden(false);
			await Task.Delay(7600);
			Check("shown again: summary of what arrived", AnyBubble());
			CloseBubbles();
		}

		static async Task AgendaOnline()
		{
			// a real public Google calendar (Brazilian holidays): only all-day events, so nothing to warn about
			const string url = "https://calendar.google.com/calendar/ical/pt.brazilian%23holiday%40group.v.calendar.google.com/public/basic.ics";
			try
			{
				var list = await Agenda.Load(url);
				Check("agenda online: a real Google calendar is read", list != null && list.All(e => e.Title.Length > 0), list?.Count.ToString());
			}
			catch (System.Net.Http.HttpRequestException ex) { lines.Add("SKIP  agenda online: no internet (" + ex.Message + ")"); }
			bool refused = false;
			try { await Agenda.Load("http://example.com/cal.ics"); } catch (ArgumentException) { refused = true; }
			Check("agenda: plain http refused", refused);
		}

		static async Task DownloadsWatcher()
		{
			string dir = Path.Combine(Path.GetTempPath(), "selftest-downloads");
			if (Directory.Exists(dir)) Directory.Delete(dir, true);
			Directory.CreateDirectory(dir);
			try
			{
				ComputerEvents.TestWatch(dir);
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
				// like a browser: first the unfinished file, then renamed to the final name
				File.WriteAllText(Path.Combine(dir, "relatorio.pdf.crdownload"), "x");
				await Task.Delay(300);
				File.Move(Path.Combine(dir, "relatorio.pdf.crdownload"), Path.Combine(dir, "relatorio.pdf"));
				await Task.Delay(4600);
				ComputerEvents.TestCheckDownloads();
				await Task.Delay(300);
				Check("downloads: finished file announced", Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible));
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
			}
			finally
			{
				ComputerEvents.TestWatch(null);
				try { Directory.Delete(dir, true); } catch (Exception) { }
			}
		}

		static void Mcp()
		{
			System.Text.Json.Nodes.JsonNode Ask(string json) => McpServer.Handle(System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject);
			var init = Ask("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}");
			Check("mcp: initialize", init?["result"]?["serverInfo"]?["name"]?.GetValue<string>() == "desktoppet");
			Check("mcp: notification gets no answer", Ask("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}") == null);
			var tools = Ask("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}")?["result"]?["tools"]?.AsArray();
			Check("mcp: 3 tools", tools != null && tools.Count == 3 && tools.Any(t => t["name"]?.GetValue<string>() == "pet_say"));
			var bad = Ask("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"pet_say\",\"arguments\":{\"text\":\"veja C:\\\\Users\\\\x\\\\senha.txt\"}}}");
			Check("mcp: a path is never said", bad?["result"]?["isError"]?.GetValue<bool>() == true);
			Check("mcp: unknown method", Ask("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"nada\"}")?["error"] != null);
			Check("say filter: normal sentence ok", AgentBridge.IsSafeToSay("Terminei os testes, 2 falharam."));
			Check("say filter: link and key refused", !AgentBridge.IsSafeToSay("veja https://x.com") && !AgentBridge.IsSafeToSay("token sk_" + "live_" + "abcdefghijklmnopqrstuvwxyz123456"));
		}

		static void BackupMyThings()
		{
			string file = Path.Combine(Path.GetTempPath(), "selftest-full" + PetBackup.Extension);
			PetBackup.Export(file);
			using (var zip = ZipFile.OpenRead(file))
			{
				var settings = new StreamReader(zip.GetEntry("settings.txt").Open()).ReadToEnd();
				Check("backup: settings inside", settings.Contains("set."), settings.Length.ToString());
				Check("backup: no Claude Code secret", !settings.Contains("AgentToken") && !settings.Contains(AgentBridge.Token));
				Check("backup: reminders inside (when there are any)", !File.Exists(Reminders.FilePath) || zip.GetEntry("data/reminders.json") != null);
			}
			Check("backup: recognized as full", PetBackup.HasMyThings(file));
			File.Delete(file);
		}

		// ------------------------------------------------------------------ Claude Code

		static void ClaudeHooksFile()
		{
			string dir = Path.Combine(Path.GetTempPath(), "selftest-claude");
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, "settings.json");
			File.WriteAllText(path, "{\n  \"model\": \"opus\",\n  \"hooks\": { \"PreToolUse\": [ { \"matcher\": \"Bash\", \"hooks\": [ { \"type\": \"command\", \"command\": \"echo mine\" } ] } ] }\n}");
			ClaudeHooks.Install(path);
			string text = File.ReadAllText(path);
			Check("hooks: installed", ClaudeHooks.IsInstalled(path) && ClaudeHooks.IsCurrent(path));
			Check("hooks: other settings kept", text.Contains("\"model\": \"opus\"") && text.Contains("echo mine"));
			Check("hooks: backup saved", File.Exists(ClaudeHooks.BackupPath(path)) && File.ReadAllText(ClaudeHooks.BackupPath(path)).Contains("echo mine"));
			ClaudeHooks.Install(path);
			int count = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "X-DesktopPet:").Count;
			Check("hooks: installing twice does not duplicate", count == 10, count.ToString());
			ClaudeHooks.Uninstall(path);
			text = File.ReadAllText(path);
			Check("hooks: removed, the user's hook stays", !ClaudeHooks.IsInstalled(path) && text.Contains("echo mine") && text.Contains("opus"), text);
			File.Delete(path);
			ClaudeHooks.Install(path);
			Check("hooks: a missing file is created", ClaudeHooks.IsInstalled(path));
			ClaudeHooks.Uninstall(path);
			Check("hooks: empty hooks section removed", !File.ReadAllText(path).Contains("\"hooks\""));
			File.WriteAllText(path, "{ not json");
			bool refused = false;
			try { ClaudeHooks.Install(path); } catch (Exception) { refused = true; }
			Check("hooks: a broken settings file is never overwritten", refused && File.ReadAllText(path) == "{ not json");
			Directory.Delete(dir, true);
		}

		static void ClaudeEvents()
		{
			var cases = new (string path, string json, AgentBridge.Activity expected)[]
			{
				("UserPromptSubmit", "{}", AgentBridge.Activity.Thinking),
				("PreToolUse/edit", "", AgentBridge.Activity.Editing),
				("PreToolUse/run", "", AgentBridge.Activity.Running),
				("PreToolUse/read", "", AgentBridge.Activity.Reading),
				("PreToolUse/web", "", AgentBridge.Activity.Searching),
				("PreToolUse", "{\"tool_name\":\"Write\"}", AgentBridge.Activity.Editing),
				("PostToolUseFailure", "", AgentBridge.Activity.Error),
				("PostToolUse", "{\"tool_name\":\"Bash\"}", AgentBridge.Activity.None),
				("Notification", "", AgentBridge.Activity.Waiting),
				("Stop", "", AgentBridge.Activity.Done),
				("SessionStart", "", AgentBridge.Activity.Hello),
			};
			foreach (var c in cases)
				Check("claude event: " + c.path, AgentBridge.ClassifyPath(c.path, c.json) == c.expected, AgentBridge.ClassifyPath(c.path, c.json).ToString());
		}

		static async Task ClaudeEndpoint()
		{
			bool was = AgentBridge.Enabled;
			AgentBridge.Enabled = true;
			try
			{
				Check("endpoint: listening on 127.0.0.1", AgentBridge.Listening);
				if (!AgentBridge.Listening) return;
				int ok = await AgentBridge.SendTest("PreToolUse/edit", "{}");
				await Task.Delay(300);
				Check("endpoint: event accepted", ok == 204 && AgentBridge.Last == AgentBridge.Activity.Editing, ok + " " + AgentBridge.Last);
				int bad = await AgentBridge.SendTest("Stop", "{}", "wrong-token");
				await Task.Delay(200);
				Check("endpoint: wrong secret refused", bad == 403 && AgentBridge.Last == AgentBridge.Activity.Editing, bad + " " + AgentBridge.Last);

				// the real hook command, as Claude Code runs it (Git Bash if installed, otherwise PowerShell)
				string cmd = $"curl.exe -s -m 2 -X POST -d x -H \"X-DesktopPet: {AgentBridge.Token}\" http://127.0.0.1:{AgentBridge.Port}/hook/Stop; exit 0";
				foreach (var (shell, args) in new[] { (GitBash(), "-c"), ("powershell", "-NoProfile -Command"), ("pwsh", "-NoProfile -Command") })
				{
					var (code, output) = await RunShell(shell, args, cmd);
					if (code == int.MinValue) { lines.Add($"SKIP  hook command in {shell}: not installed"); continue; }
					await Task.Delay(300);
					Check($"hook command in {shell}: exit 0, no output, pet reacts", code == 0 && output.Trim().Length == 0 && AgentBridge.Last == AgentBridge.Activity.Done, $"exit {code}, output '{output.Trim()}', last {AgentBridge.Last}");
					AgentBridge.React(AgentBridge.Activity.Thinking);
				}
				foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();

				// pet closed: the hook must still end quietly (Claude Code must never show an error)
				AgentBridge.Enabled = false;
				var (code2, out2) = await RunShell("powershell", "-NoProfile -Command", cmd);
				Check("hook command with the pet closed: exit 0, no output", code2 == 0 && out2.Trim().Length == 0, $"exit {code2}, output '{out2.Trim()}'");
			}
			finally { AgentBridge.Enabled = was; }
		}

		static string GitBash()
		{
			foreach (var p in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Programs" })
			{
				if (string.IsNullOrEmpty(p)) continue;
				string b = Path.Combine(p, "Git", "bin", "bash.exe");
				if (File.Exists(b)) return b;
			}
			return "bash";
		}

		static async Task<(int code, string output)> RunShell(string shell, string args, string command)
		{
			try
			{
				var psi = new System.Diagnostics.ProcessStartInfo(shell)
				{
					UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, CreateNoWindow = true,
				};
				psi.Arguments = args + " \"" + command.Replace("\"", "\\\"") + "\"";
				using (var p = System.Diagnostics.Process.Start(psi))
				{
					p.StandardInput.Write("{\"hook_event_name\":\"Stop\"}");
					p.StandardInput.Close();
					string o = await p.StandardOutput.ReadToEndAsync();
					await Task.Run(() => p.WaitForExit(15000));
					return (p.ExitCode, o);
				}
			}
			catch (System.ComponentModel.Win32Exception) { return (int.MinValue, ""); }
		}

		static async Task ShortcutError()
		{
			Launchers.Open(new Launcher { Name = "nada", Target = @"C:\this\does\not\exist.txt" });
			bool shown = false;
			for (int i = 0; i < 20 && !shown; i++) { await Task.Delay(100); shown = Application.OpenForms.OfType<SpeechBubble>().Any(); }
			var p = Pet();
			Check("shortcut: a missing file only shows a message", shown,
				p == null ? "no pet on screen (the message went to the tray)" : $"pet {p.Bounds}, opacity {p.Opacity}, hidden {HideHotkey.Hidden}");
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
		}
	}
}
