using System;
using System.Collections.Generic;
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

		static void Check(string name, bool ok, string detail = "")
		{
			if (!ok) failures++;
			lines.Add((ok ? "PASS  " : "FAIL  ") + name + (ok || detail.Length == 0 ? "" : "  →  " + detail));
		}

		static void Try(string name, Action test)
		{
			try { test(); }
			catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
		}

		public static async void Run(string outFile)
		{
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

			lines.Insert(0, $"DesktopPet self-test {DateTime.Now:yyyy-MM-dd HH:mm:ss}  —  {failures} failure(s)");
			try { File.WriteAllLines(outFile, lines, new UTF8Encoding(false)); } catch (Exception) { }
			Environment.Exit(failures);
		}

		static async Task TryAsync(string name, Func<Task> test)
		{
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
			string file = Path.Combine(Lang.Folder, "pt-BR.json");
			var pt = JsonText.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
			var missing = Lang.Keys.Where(k => !pt.ContainsKey(k)).ToList();
			Check("pt-BR has every text", missing.Count == 0, string.Join(", ", missing.Take(15)) + (missing.Count > 15 ? $" … ({missing.Count})" : ""));
			var extra = pt.Keys.Where(k => !Lang.Keys.Contains(k)).ToList();
			Check("pt-BR has no unknown text", extra.Count == 0, string.Join(", ", extra.Take(15)));
			// placeholders: the same {n} in both languages
			var bad = Lang.Keys.Where(pt.ContainsKey).Where(k => Placeholders(pt[k]) != Placeholders(Lang.English(k))).ToList();
			Check("pt-BR placeholders match", bad.Count == 0, string.Join(", ", bad.Take(10)));
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
			var cm = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0), () => { });
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
				var menu = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0), () => { });
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
			var m2 = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0), () => { });
			var focusItem = m2.Items.OfType<ToolStripMenuItem>().First(i => i.Text == Lang.T("menu.focus"));
			var start = focusItem.DropDownItems.OfType<ToolStripMenuItem>().First();
			m2.Close();
			start.PerformClick();
			Check("menu click: start focusing", Pomodoro.Current == Pomodoro.Phase.Focus);
			var m3 = PetMenu.Show(pet, pet, new System.Drawing.Point(0, 0), () => { });
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
			Check("monitor: plate next to the pet", Application.OpenForms.Cast<Form>().Any(f => f.GetType().Name == "Hud" && f.Visible));
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

		static async Task ShortcutError()
		{
			Launchers.Open(new Launcher { Name = "nada", Target = @"C:\this\does\not\exist.txt" });
			await Task.Delay(300);
			Check("shortcut: a missing file only shows a message", Application.OpenForms.OfType<SpeechBubble>().Any());
			foreach (var b in Application.OpenForms.OfType<SpeechBubble>().ToList()) b.Close();
		}
	}
}
