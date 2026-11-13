using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	// ====================================================================== 1. full screen and "Do not disturb"

	/// <summary>
	/// While a game, a video or a presentation fills the screen, the pets hide by themselves (and come back after);
	/// while Windows is in "Do not disturb", they stay but keep quiet (notices wait, see <see cref="SpeechBubble.QuietNow"/>).
	/// </summary>
	public static class FullScreenWatch
	{
		[DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
		const int Busy = 2, D3dFullScreen = 3, Presentation = 4, QuietTime = 6, App = 7;

		static Timer timer;
		static DateTime gameSince = DateTime.MinValue;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("AutoHide", true);
			set { Program.MyData.SetSetting("AutoHide", value); Apply(); }
		}

		/// <summary>The pets are hidden because something fills the screen (not by Ctrl+Alt+P).</summary>
		public static bool Active { get; private set; }
		/// <summary>Windows is in "Do not disturb".</summary>
		public static bool Dnd { get; private set; }

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 2000 }; timer.Tick += (s, e) => Tick(); }
			if (Enabled) timer.Start();
			else
			{
				timer.Stop();
				if (Active) Show();
				if (Dnd) { Dnd = false; SpeechBubble.QuietChanged(); }
			}
		}

		static int State()
		{
			try { return SHQueryUserNotificationState(out int s) == 0 ? s : 5; }
			catch (Exception skipped) { LogFile.Ignored(skipped); return 5; }
		}

		internal static void Tick()
		{
			if (SelfTest.Running && !SelfTest.Live) return;
			int st = State();
			bool full = st == Busy || st == D3dFullScreen || st == Presentation || st == App;
			bool dnd = st == QuietTime;
			if (full && !Active && !HideHotkey.Hidden)
			{
				Active = true;
				if (st == D3dFullScreen) gameSince = DateTime.Now;
				HideHotkey.SetHidden(true);
			}
			else if (!full && Active) Show();
			if (dnd != Dnd) { Dnd = dnd; SpeechBubble.QuietChanged(); }
		}

		static void Show()
		{
			Active = false;
			if (HideHotkey.Hidden) HideHotkey.SetHidden(false);
			// back from a game of more than 5 minutes: "did you win?"
			if (gameSince != DateTime.MinValue)
			{
				var played = DateTime.Now - gameSince;
				gameSince = DateTime.MinValue;
				if (played.TotalMinutes >= 5) MusicWatch.GameOver(played);
			}
		}
	}

	// ====================================================================== 5. notices while the pets are hidden, quiet hours

	/// <summary>
	/// With the pets hidden by Ctrl+Alt+P, notices can go to Windows notifications instead of waiting; during quiet
	/// hours (for example 22h to 7h) nothing is shown and the notices wait until the morning.
	/// </summary>
	public static class NoticeOptions
	{
		static Timer timer;
		static bool wasQuiet;

		public static bool ToastsWhenHidden
		{
			get => Program.MyData.GetSettingBool("HiddenToasts", true);
			set => Program.MyData.SetSetting("HiddenToasts", value);
		}

		/// <summary>Quiet hours: from / to (hour of the day); -1 = off.</summary>
		public static int QuietFrom { get => Program.MyData.GetSettingInt("QuietFrom", -1); set { Program.MyData.SetSetting("QuietFrom", value); Check(); } }
		public static int QuietTo { get => Program.MyData.GetSettingInt("QuietTo", 7); set { Program.MyData.SetSetting("QuietTo", value); Check(); } }

		public static bool InQuietHours
		{
			get
			{
				int from = QuietFrom, to = QuietTo, h = DateTime.Now.Hour;
				if (from < 0 || from == to) return false;
				return from < to ? h >= from && h < to : h >= from || h < to;
			}
		}

		public static void Start()
		{
			wasQuiet = InQuietHours;
			timer = new Timer { Interval = 60000 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		static void Check()
		{
			bool q = InQuietHours;
			if (q != wasQuiet) { wasQuiet = q; SpeechBubble.QuietChanged(); }
		}

		/// <summary>The pets are hidden by the user: send the notice to Windows (true), or let it wait (false).</summary>
		public static bool ToToast(string text)
		{
			if (!HideHotkey.Hidden || FullScreenWatch.Active || InQuietHours || !ToastsWhenHidden) return false;
			ProcessIcon.Current?.Notify(Program.Mainthread?.CurrentHeader?.Petname ?? "desktopPet", text, () => HideHotkey.SetHidden(false));
			return true;
		}
	}

	// ====================================================================== 4. clipboard

	/// <summary>
	/// Off unless turned on. When a link or a long text is copied, the pet offers to open it, summarize it or translate it
	/// (with the AI, only when you click). The last copies are kept in memory (never on disk) to copy them again.
	/// Copies marked private by password managers are ignored.
	/// </summary>
	public static class ClipboardWatch
	{
		[DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
		[DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
		[DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);

		static Receiver window;
		static string last = "";
		static DateTime lastBubble = DateTime.MinValue;
		public static readonly List<string> History = new List<string>();
		/// <summary>Set while the program itself writes to the clipboard.</summary>
		public static bool Writing;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("ClipWatch", false);
			set { Program.MyData.SetSetting("ClipWatch", value); Apply(); }
		}

		/// <summary>Offer actions in a bubble (otherwise only the history is kept).</summary>
		public static bool Offers
		{
			get => Program.MyData.GetSettingBool("ClipOffers", true);
			set => Program.MyData.SetSetting("ClipOffers", value);
		}

		public static void Apply()
		{
			if (Enabled && window == null)
			{
				window = new Receiver();
				AddClipboardFormatListener(window.Handle);
			}
			else if (!Enabled && window != null)
			{
				RemoveClipboardFormatListener(window.Handle);
				window.DestroyHandle();
				window = null;
				History.Clear();
			}
		}

		/// <summary>Copy again (from the history), without offering it back.</summary>
		public static void Copy(string text)
		{
			Writing = true;
			try { Clipboard.SetText(text); } catch (Exception skipped) { LogFile.Ignored(skipped); }
			finally { Writing = false; }
		}

		static bool Private()
		{
			// password managers mark what they copy as "not for history / monitoring"
			try
			{
				return IsClipboardFormatAvailable(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"))
					|| IsClipboardFormatAvailable(RegisterClipboardFormat("CanIncludeInClipboardHistory"));
			}
			catch (Exception) { return true; }
		}

		static void Changed()
		{
			if (Writing || Private()) return;
			string text;
			try { if (!Clipboard.ContainsText()) return; text = Clipboard.GetText(); }
			catch (Exception skipped) { LogFile.Ignored(skipped); return; }
			text = text?.Trim() ?? "";
			if (text.Length == 0 || text == last) return;
			last = text;
			History.Remove(text);
			History.Insert(0, text);
			if (History.Count > 20) History.RemoveAt(History.Count - 1);

			if (!Offers || SpeechBubble.QuietNow || (DateTime.Now - lastBubble).TotalSeconds < 45) return;
			if (IsLink(text, out var uri))
			{
				lastBubble = DateTime.Now;
				var choices = new List<SpeechBubble.Choice> { new SpeechBubble.Choice(Lang.T("clip.open"), () => Program.OpenLink(uri.AbsoluteUri), true) };
				SpeechBubble.Say(Lang.T("clip.link", uri.Host), 8, choices.ToArray());
			}
			else if (text.Length >= 280 && AiAssistant.Configured)
			{
				lastBubble = DateTime.Now;
				string copied = text.Length > 6000 ? text.Substring(0, 6000) : text;
				SpeechBubble.Say(Lang.T("clip.long"), 8,
					new SpeechBubble.Choice(Lang.T("clip.summary"), () => AskAi(Lang.T("clip.askSummary") + "\n\n" + copied), true),
					new SpeechBubble.Choice(Lang.T("clip.translate"), () => AskAi(Lang.T("clip.askTranslate") + "\n\n" + copied)));
			}
		}

		public static bool IsLink(string text, out Uri uri)
		{
			uri = null;
			return !text.Contains('\n') && !text.Contains(' ') && Uri.TryCreate(text, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https");
		}

		static async void AskAi(string question)
		{
			SpeechBubble.Say(Lang.T("clip.thinking"), 30);
			try
			{
				string answer = await AiAssistant.Ask(question);
				SpeechBubble.Say(answer, 0, new SpeechBubble.Choice(Lang.T("clip.copyAnswer"), () => Copy(answer), true), new SpeechBubble.Choice("OK", null));
			}
			catch (Exception ex) { SpeechBubble.Say(Lang.T("clip.aiError", ex.Message), 8); }
		}

		sealed class Receiver : NativeWindow
		{
			public Receiver() { CreateHandle(new CreateParams()); }
			protected override void WndProc(ref Message m)
			{
				const int WM_CLIPBOARDUPDATE = 0x031D;
				if (m.Msg == WM_CLIPBOARDUPDATE) { try { Changed(); } catch (Exception skipped) { LogFile.Ignored(skipped); } }
				base.WndProc(ref m);
			}
		}

		/// <summary>The last copies: click one to copy it again.</summary>
		public static void ShowHistory(IWin32Window owner)
		{
			using (var f = new FormClipHistory()) f.ShowDialog(owner);
		}

		sealed class FormClipHistory : ThemedDialog
		{
			public FormClipHistory()
			{
				Text = Lang.T("clip.historyTitle");
				iconEmoji = "📋";
				const int W = 520;
				var list = new ListBox { Location = new Point(Pad, 88), Size = new Size(W - Pad * 2, 260), Font = fSmall, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
				foreach (var h in History) list.Items.Add(h.Replace("\r", " ").Replace("\n", " ⏎ "));
				if (History.Count == 0) list.Items.Add(Lang.T("clip.historyEmpty"));
				list.DoubleClick += (s, e) =>
				{
					if (list.SelectedIndex < 0 || list.SelectedIndex >= History.Count) return;
					Copy(History[list.SelectedIndex]);
					DialogResult = DialogResult.OK;
				};
				Controls.Add(list);
				var close = AddButton(Lang.T("launch.close"), DialogResult.OK, true);
				ApplyTheme(close);
				ClientSize = new Size(W, LayoutButtons(list.Bottom + 18, W) + Pad);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				Theme.Prepare(e.Graphics);
				PaintIcon(e.Graphics);
				Theme.DrawText(e.Graphics, Lang.T("clip.historyHelp"), fSmall, new Rectangle(Pad + 62, Pad + 4, ClientSize.Width - Pad * 2 - 62, 50), Theme.TextMuted);
			}
		}
	}

	// ====================================================================== 6. other AI agents, 7. the pet levels up, 8. local API

	/// <summary>
	/// Codex and Cursor (and any program that can run a command) tell the pet what they are doing, like Claude Code:
	/// they run "DesktopPet.exe --agent=done", which forwards it to the running pet through the local bridge.
	/// </summary>
	public static class OtherAgents
	{
		const string Marker = "--agent=";
		static string Exe => Environment.ProcessPath ?? Application.ExecutablePath;
		/// <summary>Self-test: write the agents' files in a test folder instead of the user's.</summary>
		internal static string HomeOverride;
		static string Home => HomeOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		public static string CodexConfig => Path.Combine(Home, ".codex", "config.toml");
		public static string CursorHooks => Path.Combine(Home, ".cursor", "hooks.json");

		public static bool AnyInstalled => CodexInstalled || CursorInstalled;

		/// <summary>"--agent=done" in another process: forward it to the running pet. Returns the exit code.</summary>
		public static int Signal(string what)
		{
			string ev;
			switch ((what ?? "").Trim().ToLowerInvariant())
			{
				case "done": case "stop": ev = "Stop"; break;
				case "thinking": case "start": ev = "UserPromptSubmit"; break;
				case "waiting": case "input": ev = "Notification"; break;
				case "error": ev = "PostToolUseFailure"; break;
				case "edit": ev = "PreToolUse/edit"; break;
				case "run": ev = "PreToolUse/run"; break;
				case "read": ev = "PreToolUse/read"; break;
				default: return 2;
			}
			try { McpServer.Send("/hook/" + ev, "{}"); return 0; }
			catch (Exception) { return 1; }   // the pet is not running: the agent must not fail because of it
		}

		public static string Command(string what) => "\"" + Exe + "\" " + Marker + what;

		// ------------------------------------------------------------------ Codex: notify = [...] in config.toml

		public static bool CodexInstalled
		{
			get
			{
				try { return File.Exists(CodexConfig) && File.ReadAllText(CodexConfig).Contains(Marker); }
				catch (Exception) { return false; }
			}
		}

		const string SavedPrefix = "# desktopPet, your notify before: ";

		static string Toml(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

		/// <summary>The strings of a one-line TOML array (notify = ["a", "b"]), or null if the line has another form.</summary>
		public static List<string> TomlStrings(string line)
		{
			int open = line.IndexOf('['), close = line.LastIndexOf(']');
			if (open < 0 || close < open) return null;
			var list = new List<string>();
			string inner = line.Substring(open + 1, close - open - 1);
			int i = 0;
			while (i < inner.Length)
			{
				char c = inner[i];
				if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }
				if (c == '\'') { int e = inner.IndexOf('\'', i + 1); if (e < 0) return null; list.Add(inner.Substring(i + 1, e - i - 1)); i = e + 1; continue; }
				if (c != '"') return null;
				var sb = new StringBuilder();
				i++;
				while (i < inner.Length && inner[i] != '"')
				{
					if (inner[i] == '\\' && i + 1 < inner.Length) { i++; sb.Append(inner[i] == 'n' ? '\n' : inner[i] == 't' ? '\t' : inner[i]); }
					else sb.Append(inner[i]);
					i++;
				}
				if (i >= inner.Length) return null;
				list.Add(sb.ToString());
				i++;
			}
			return list;
		}

		/// <summary>
		/// "DesktopPet.exe --agent=done [--then program args…] [payload]": tell the pet, then run the program that was in
		/// Codex's notify before (with the same arguments and the payload Codex adds at the end).
		/// </summary>
		public static int Run(string[] args)
		{
			int code = Signal(args[0].Substring(Marker.Length));
			int then = Array.IndexOf(args, "--then");
			if (then >= 0 && then + 1 < args.Length)
			{
				try
				{
					var psi = new ProcessStartInfo(args[then + 1]) { UseShellExecute = false, CreateNoWindow = true };
					for (int k = then + 2; k < args.Length; k++) psi.ArgumentList.Add(args[k]);
					using (var p = Process.Start(psi)) p?.WaitForExit(60000);
				}
				catch (Exception ex) { LogFile.Ignored(ex); }
			}
			return code == 2 ? 2 : 0;   // an agent must never fail because the pet is closed
		}

		/// <summary>Add or remove the notify line. Returns a message key when something prevents it.</summary>
		public static string SetCodex(bool on)
		{
			try
			{
				var lines = File.Exists(CodexConfig) ? File.ReadAllLines(CodexConfig).ToList() : new List<string>();
				int ours = lines.FindIndex(l => l.TrimStart().StartsWith("notify") && l.Contains(Marker));
				int saved = lines.FindIndex(l => l.StartsWith(SavedPrefix));
				if (!on)
				{
					if (ours < 0) return null;
					// put back the user's own notify line, exactly as it was
					if (saved >= 0) { lines[ours] = lines[saved].Substring(SavedPrefix.Length); lines.RemoveAt(saved); }
					else lines.RemoveAt(ours);
					File.WriteAllLines(CodexConfig, lines);
					return null;
				}
				if (ours >= 0) return null;
				string exe = Toml(Exe);
				string ourLine = $"notify = [{exe}, {Toml(Marker + "done")}]";
				int theirs = lines.FindIndex(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\s*notify\s*="));
				if (theirs >= 0)
				{
					// Codex runs only one notify program: run theirs after us ("--then"), keep a copy of their line to put it back
					var their = TomlStrings(lines[theirs]);
					if (their == null) return "agents.codexBusy";   // a form we do not read (several lines…): leave it alone
					ourLine = $"notify = [{exe}, {Toml(Marker + "done")}, {Toml("--then")}, {string.Join(", ", their.Select(Toml))}]";
					lines.Insert(theirs, SavedPrefix + lines[theirs]);
					lines[theirs + 1] = ourLine;
				}
				else
				{
					// notify is a top-level key: before the first [table]
					int firstTable = lines.FindIndex(l => l.TrimStart().StartsWith("["));
					if (firstTable < 0) lines.Add(ourLine); else lines.Insert(firstTable, ourLine);
				}
				Directory.CreateDirectory(Path.GetDirectoryName(CodexConfig));
				File.WriteAllLines(CodexConfig, lines);
				return null;
			}
			catch (Exception ex) { LogFile.Ignored(ex); return "agents.writeError"; }
			finally { AgentBridge.Apply(); }
		}

		// ------------------------------------------------------------------ Cursor: hooks.json (only hooks that just watch)

		public static bool CursorInstalled
		{
			get
			{
				try { return File.Exists(CursorHooks) && File.ReadAllText(CursorHooks).Contains(Marker); }
				catch (Exception) { return false; }
			}
		}

		public static string SetCursor(bool on)
		{
			try
			{
				JsonObject root = File.Exists(CursorHooks) ? JsonNode.Parse(File.ReadAllText(CursorHooks)) as JsonObject : null;
				root ??= new JsonObject { ["version"] = 1 };
				if (!(root["hooks"] is JsonObject hooks)) root["hooks"] = hooks = new JsonObject();
				foreach (var (ev, what) in new[] { ("afterFileEdit", "edit"), ("stop", "done") })
				{
					if (!(hooks[ev] is JsonArray list)) hooks[ev] = list = new JsonArray();
					foreach (var old in list.Where(h => (h?["command"]?.GetValue<string>() ?? "").Contains(Marker)).ToList()) list.Remove(old);
					if (on) list.Add(new JsonObject { ["command"] = Command(what) });
					if (list.Count == 0) hooks.Remove(ev);
				}
				Directory.CreateDirectory(Path.GetDirectoryName(CursorHooks));
				File.WriteAllText(CursorHooks, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
				return null;
			}
			catch (Exception ex) { LogFile.Ignored(ex); return "agents.writeError"; }
			finally { AgentBridge.Apply(); }
		}
	}

	/// <summary>The pet grows while you code: every task an AI agent finishes is worth a point; the level goes up.</summary>
	public static class AgentLevel
	{
		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("AgentLevel", true);
			set => Program.MyData.SetSetting("AgentLevel", value);
		}

		public static int Tasks => Program.MyData.GetSettingInt("AgentTasks", 0);

		/// <summary>Level 1 at the start, then 3, 8, 15, 24… tasks (each level needs a little more).</summary>
		public static int LevelFor(int tasks) => 1 + (int)Math.Floor(Math.Sqrt(tasks + 1) - 1);

		public static int Level => LevelFor(Tasks);

		public static void TaskDone()
		{
			if (!Enabled) return;
			int before = Level;
			Program.MyData.SetSetting("AgentTasks", Tasks + 1);
			if (Level > before)
			{
				Program.Mainthread?.ReactAll("cheer", "happy", "jump", "dance");
				SpeechBubble.Notify(Lang.T("agents.levelUp", Level), 8);
			}
		}
	}

	/// <summary>
	/// The local address other programs can use (only on this computer, with the secret of agent.json): POST /say with a
	/// short text, POST /react with an activity name, POST /hook/&lt;event&gt;.
	/// </summary>
	public static class LocalApi
	{
		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("LocalApi", false);
			set { Program.MyData.SetSetting("LocalApi", value); AgentBridge.Apply(); }
		}

		/// <summary>A PowerShell example that makes the pet say something.</summary>
		public static string Example =>
			"$c = Get-Content \"$env:LOCALAPPDATA\\DesktopPet\\agent.json\" | ConvertFrom-Json\r\n" +
			"Invoke-RestMethod -Method Post -Uri \"http://127.0.0.1:$($c.port)/say\" -Headers @{ 'X-DesktopPet' = $c.token } -Body '" + Lang.T("api.exampleText") + "'";
	}

	// ====================================================================== 9. music and games

	/// <summary>The pet dances when a new song starts in Spotify, and asks how the game went when a long one ends.</summary>
	public static class MusicWatch
	{
		static Timer timer;
		static string song = "";

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("MusicGames", true);
			set { Program.MyData.SetSetting("MusicGames", value); Apply(); }
		}

		/// <summary>Also say the name of each new song.</summary>
		public static bool SayTitle
		{
			get => Program.MyData.GetSettingBool("MusicTitle", false);
			set => Program.MyData.SetSetting("MusicTitle", value);
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 3000 }; timer.Tick += (s, e) => Tick(); }
			if (Enabled) timer.Start(); else timer.Stop();
		}

		/// <summary>"Artist - Song" from Spotify's window, or "" when nothing plays (the title is just "Spotify").</summary>
		/// <summary>Spotify shows "Artist - Song" while playing and just "Spotify…" when paused.</summary>
		public static bool IsSong(string title) => !string.IsNullOrEmpty(title) && title.Contains(" - ") && !title.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase);

		/// <summary>Self-test: a window title to use instead of Spotify's.</summary>
		internal static string FakeTitle;

		public static string Playing()
		{
			if (FakeTitle != null) return IsSong(FakeTitle) ? FakeTitle : "";
			try
			{
				foreach (var p in Process.GetProcessesByName("Spotify"))
				{
					using (p)
					{
						string t = p.MainWindowTitle ?? "";
						if (IsSong(t)) return t;
					}
				}
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			return "";
		}

		internal static void Tick()
		{
			if (SelfTest.Running && !SelfTest.Live) return;
			string now = Playing();
			if (now == song) return;
			song = now;
			if (now.Length == 0 || SpeechBubble.QuietNow) return;
			Program.Mainthread?.ReactAll("dance", "party", "happy", "cheer", "jump");
			if (SayTitle) SpeechBubble.Say("🎵 " + now, 5);
		}

		public static void GameOver(TimeSpan played)
		{
			if (!Enabled) return;
			var t = new Timer { Interval = 2500 };
			t.Tick += (s, e) =>
			{
				t.Stop(); t.Dispose();
				Program.Mainthread?.ReactAll("cheer", "happy", "wave");
				SpeechBubble.Say(Lang.T("game.over", (int)played.TotalMinutes), 10);
			};
			t.Start();
		}
	}

	// ====================================================================== 10. visits on the screen

	/// <summary>Now and then a butterfly flies to the pet, flutters around it, lands on its head and flies away.</summary>
	public static class Visitors
	{
		static Timer timer;
		static DateTime next = DateTime.Now.AddMinutes(12);
		static ButterflyForm current;
		/// <summary>A butterfly is on screen now, and where (self-test).</summary>
		internal static Form Flying => current != null && !current.IsDisposed && current.Visible ? current : null;
		static readonly Random rnd = new Random();

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("Visitors", true);
			set { Program.MyData.SetSetting("Visitors", value); Apply(); }
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 30000 }; timer.Tick += (s, e) => { if (DateTime.Now >= next) Visit(); }; }
			if (Enabled) timer.Start(); else timer.Stop();
		}

		static FormPet Pet() => Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);

		/// <summary>A butterfly now (also the "Try it" button).</summary>
		public static void Visit()
		{
			if (SelfTest.Running && !SelfTest.Live) return;
			next = DateTime.Now.AddMinutes(20 + rnd.Next(25));
			var pet = Pet();
			if (pet == null || SpeechBubble.QuietNow || (current != null && !current.IsDisposed)) return;
			current = new ButterflyForm(pet);
			current.Show();
		}

		sealed class ButterflyForm : Form
		{
			// 9 x 7 pixels, two wing positions; each pixel 3 x 3 on screen
			static readonly string[] Open = { "rr..b..rr", "rrr.b.rrr", "rryrbryrr", ".rrrbrrr.", "..rrbrr..", ".rr.b.rr.", ".r.....r." };
			static readonly string[] Shut = { ".........", "...rbr...", "..ryrbr..", "..rrbrr..", "..rrbrr..", "...rbr...", "........." };
			static readonly Color[] Colors = { Color.FromArgb(255, 150, 60), Color.FromArgb(120, 180, 255), Color.FromArgb(240, 110, 200), Color.FromArgb(255, 220, 70) };
			const int Px = 3;
			readonly FormPet pet;
			readonly Timer tick = new Timer { Interval = 40 };
			readonly Color wing;
			readonly Bitmap open, shut;
			double x, y, t;
			int phase;          // 0 flying in, 1 around the pet, 2 resting on the head, 3 flying away
			double phaseT;
			readonly int side;

			public ButterflyForm(FormPet pet)
			{
				this.pet = pet;
				wing = Colors[rnd.Next(Colors.Length)];
				open = Draw(Open); shut = Draw(Shut);
				FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
				BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
				Size = new Size(9 * Px, 7 * Px);
				var screen = Screen.FromControl(pet).WorkingArea;
				side = pet.Left > screen.Left + screen.Width / 2 ? 1 : -1;
				x = side > 0 ? screen.Right + 10 : screen.Left - 40;
				y = pet.Top - 60;
				Location = new Point((int)x, (int)y);
				tick.Tick += (s, e) => Step();
				tick.Start();
			}

			protected override CreateParams CreateParams
			{
				get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 /* no activate */ | 0x80 /* tool window */ | 0x20 /* click-through */; return cp; }
			}
			protected override bool ShowWithoutActivation => true;

			Bitmap Draw(string[] rows)
			{
				var b = new Bitmap(9 * Px, 7 * Px);
				using (var g = Graphics.FromImage(b))
				{
					g.Clear(Color.Magenta);
					for (int j = 0; j < rows.Length; j++)
						for (int i = 0; i < rows[j].Length; i++)
						{
							char c = rows[j][i];
							if (c == '.') continue;
							var col = c == 'b' ? Color.FromArgb(50, 35, 30) : c == 'y' ? Color.FromArgb(255, 245, 200) : wing;
							using (var br = new SolidBrush(col)) g.FillRectangle(br, i * Px, j * Px, Px, Px);
						}
				}
				return b;
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				e.Graphics.DrawImageUnscaled(phase == 2 ? ((int)(t * 2) % 4 == 0 ? shut : open) : ((int)(t * 12) % 2 == 0 ? open : shut), 0, 0);
			}

			void Step()
			{
				if (pet.IsDisposed || !pet.Visible) { Close(); return; }
				t += 0.04; phaseT += 0.04;
				double hx = pet.Left + pet.Width / 2.0 - Width / 2.0, hy = pet.Top + pet.Height * 0.35 - Height;   // above the head
				switch (phase)
				{
					case 0:   // fly to the pet, bobbing
						x += Math.Sign(hx - x) * Math.Min(4, Math.Abs(hx - x));
						y += (hy - 30 - y) * 0.05 + Math.Sin(t * 6) * 1.5;
						if (Math.Abs(hx - x) < 6) { phase = 1; phaseT = 0; pet.React("look around", "happy", "wave"); }
						break;
					case 1:   // circle around the pet
						x = hx + Math.Cos(t * 2.2) * pet.Width * 0.45;
						y = hy - 22 + Math.Sin(t * 4.4) * 12;
						if (phaseT > 6) { phase = 2; phaseT = 0; }
						break;
					case 2:   // rest on the head
						x += (hx - x) * 0.2; y += (hy - y) * 0.2;
						if (phaseT > 3) { phase = 3; phaseT = 0; pet.React("happy", "laugh", "wave"); }
						break;
					default:  // away, upwards
						x += side * -3.5; y -= 2.2 + Math.Sin(t * 6);
						if (phaseT > 5) { Close(); return; }
						break;
				}
				Location = new Point((int)x, (int)y);
				Invalidate();
			}

			protected override void OnFormClosed(FormClosedEventArgs e)
			{
				tick.Stop(); tick.Dispose(); open.Dispose(); shut.Dispose();
				base.OnFormClosed(e);
			}
		}
	}

	// ====================================================================== 11. pushing windows

	/// <summary>
	/// Off unless turned on. A walking pet that bumps into a small window standing on the same floor pushes it along.
	/// Big, maximized and the program's own windows are never moved.
	/// </summary>
	public static class WindowPush
	{
		[DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
		delegate bool EnumProc(IntPtr h, IntPtr l);
		[DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
		[DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
		[DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
		[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
		[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
		[DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
		[DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
		[DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
		struct RECT { public int L, T, R, B; }

		static Timer timer;
		static readonly Dictionary<FormPet, int> lastLeft = new Dictionary<FormPet, int>();
		/// <summary>Self-test: a window of this program that may be pushed.</summary>
		internal static IntPtr TestTarget;
		/// <summary>Pixels kept between the pet's window and the window it pushes.</summary>
		const int Gap = 3;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("PushWindows", false);
			set { Program.MyData.SetSetting("PushWindows", value); Apply(); }
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 60 }; timer.Tick += (s, e) => Tick(); }
			if (Enabled) timer.Start(); else { timer.Stop(); lastLeft.Clear(); }
		}

		static void Tick()
		{
			foreach (var pet in Application.OpenForms.OfType<FormPet>().Where(p => p.Visible && !p.IsChildWindow).ToList())
			{
				int prev = lastLeft.TryGetValue(pet, out int l) ? l : pet.Left;
				lastLeft[pet] = pet.Left;
				int dx = pet.Left - prev;
				if (dx == 0 || Math.Abs(dx) > 20 || !pet.OnGround) continue;
				// the front edge of the pet's window: the app makes a pet turn back when its window touches another one,
				// so the pushed window is kept a few pixels ahead of it
				int dir = Math.Sign(dx), front = dir > 0 ? pet.Right : pet.Left;
				var target = WindowAt(front, pet.Bottom, dir);
				if (target == IntPtr.Zero) continue;
				GetWindowRect(target, out var r);
				int edge = dir > 0 ? r.L : r.R, wanted = front + dir * Gap;
				int move = dir > 0 ? Math.Max(0, wanted - edge) : Math.Min(0, wanted - edge);
				if (move == 0) continue;
				SetWindowPos(target, IntPtr.Zero, r.L + move, r.T, 0, 0, 0x0001 /* no size */ | 0x0004 /* no z-order */ | 0x0010 /* no activate */);
			}
		}

		/// <summary>A small window whose near side is just at <paramref name="front"/> and that stands on the pet's floor.</summary>
		static IntPtr WindowAt(int front, int feet, int dir)
		{
			IntPtr found = IntPtr.Zero;
			uint me = (uint)Environment.ProcessId;
			EnumWindows((h, _) =>
			{
				if (!IsWindowVisible(h) || IsIconic(h) || IsZoomed(h)) return true;
				GetWindowThreadProcessId(h, out uint pid);
				if (pid == me && h != TestTarget) return true;
				int ex = GetWindowLong(h, -20);
				if ((ex & 0x80) != 0 || (ex & 0x8) != 0) return true;              // tool windows and topmost ones (widgets, overlays)
				if (DwmGetWindowAttribute(h, 14 /* cloaked */, out int cloaked, 4) == 0 && cloaked != 0) return true;
				GetWindowRect(h, out var r);
				int w = r.R - r.L, hh = r.B - r.T;
				if (w < 80 || hh < 60 || w > 900 || hh > 700) return true;           // only small windows
				if (r.B < feet - 8 || r.T > feet - 24) return true;                  // must stand on the same floor and rise above the feet
				int edge = dir > 0 ? r.L : r.R;
				int ahead = (edge - front) * dir;                                    // how far in front of the pet's window
				if (ahead < -6 || ahead > Gap + 8) return true;
				found = h;
				return false;
			}, IntPtr.Zero);
			return found;
		}
	}
}
