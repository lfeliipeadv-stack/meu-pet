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
			get => Program.MyData.GetSettingBool("AutoHide", false);
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
			get => Program.MyData.GetSettingBool("HiddenToasts", false);
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
			get => Program.MyData.GetSettingBool("MusicGames", false);
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

	/// <summary>
	/// Now and then a small animal visits the pet: a butterfly flutters around it and lands on its head, a bird perches
	/// and pecks, a bee buzzes around it, a ladybug lands and crawls on it. Then it flies on to the other side of the
	/// screen and disappears there.
	/// </summary>
	public static class Visitors
	{
		static Timer timer;
		static DateTime next = DateTime.Now.AddMinutes(12);
		static VisitorForm current;
		/// <summary>A visitor is on screen now, and where (self-test).</summary>
		internal static Form Flying => current != null && !current.IsDisposed && current.Visible ? current : null;
		/// <summary>Which visitor is on screen now (self-test).</summary>
		internal static Kind? FlyingKind => Flying != null ? current.What : (Kind?)null;
		static readonly Random rnd = new Random();

		public enum Kind { Butterfly, Bird, Bee, Ladybug }

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("Visitors", false);
			set { Program.MyData.SetSetting("Visitors", value); Apply(); }
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 30000 }; timer.Tick += (s, e) => { if (DateTime.Now >= next) Visit(); }; }
			if (Enabled) timer.Start(); else timer.Stop();
		}

		static FormPet Pet() => Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);

		/// <summary>A visitor now, chosen at random (also the "Call now" button).</summary>
		public static void Visit() => Visit((Kind)rnd.Next(4));

		/// <summary>This visitor now.</summary>
		internal static void Visit(Kind kind)
		{
			if (SelfTest.Running && !SelfTest.Live) return;
			next = DateTime.Now.AddMinutes(20 + rnd.Next(25));
			var pet = Pet();
			if (pet == null || SpeechBubble.QuietNow || (current != null && !current.IsDisposed)) return;
			current = new VisitorForm(pet, kind);
			current.Show();
		}

		// ------------------------------------------------------------------ the drawings
		// one character per pixel: '.' clear, 'k' outline; the other letters get the colors of each animal

		static readonly string[] ButterflyOpen =
		{
			".....k.......k.....",
			"......k.....k......",
			".kkk...k...k...kkk.",
			"kdwwkk..kbk..kkwwdk",
			"kdwwwwk.kbk.kwwwwdk",
			"kdwyywwkkbkkwwyywdk",
			"kdwyywwwkbkwwwyywdk",
			"kddwwwwwkbkwwwwwddk",
			".kddwwwwkbkwwwwddk.",
			"..kkkdwwkbkwwdkkk..",
			"...kdwwwkbkwwwdk...",
			"..kdwllwkbkwllwdk..",
			"..kdwllwkbkwllwdk..",
			"..kddwwk.k.kwwddk..",
			"...kkkk.....kkkk...",
		};

		static readonly string[] BirdPerch =
		{
			"..................",
			"..........kkkk....",
			".........kbbbbk...",
			"........kbbbbbbk..",
			"........kbbbkebk..",
			"...kk...kbbbbbbkoo",
			"..kbbk.kbbbbbbbk..",
			"..kbbbkbbwwwbblk..",
			"...kbbbbwwwwbllk..",
			"....kbbbwwwwbllk..",
			".....kbbbwwbbllk..",
			"......kkbbbbllk...",
			"........kkkkkk....",
			".........f..f.....",
			".........ff.ff....",
		};
		static readonly string[] BirdPeck =
		{
			"..................",
			"..................",
			"..................",
			"..........kkkk....",
			".........kbbbbk...",
			"...kk...kbbbbbbk..",
			"..kbbk.kbbbbbkebk.",
			"..kbbbkbbwwwbbbkoo",
			"...kbbbbwwwwbbbk..",
			"....kbbbwwwwbllk..",
			".....kbbbwwbbllk..",
			"......kkbbbbllk...",
			"........kkkkkk....",
			".........f..f.....",
			".........ff.ff....",
		};
		static readonly string[] BirdFly1 =
		{
			"....kkkk..........",
			"...kwwwwk.kkkk....",
			"....kwwwwkbbbbk...",
			".....kwwwkbbbbbk..",
			"..kk..kwwkbbkebk..",
			".kbbk.kwwbbbbbbkoo",
			".kbbbkbbbbbbbbbk..",
			"..kbbbbbbbbbbllk..",
			"...kkbbbbbbbbllk..",
			".....kkkbbbbllk...",
			"........kkkkkk....",
			"..................",
		};
		static readonly string[] BirdFly2 =
		{
			"..........kkkk....",
			".........kbbbbk...",
			"........kbbbbbbk..",
			"..kk....kbbbkebk..",
			".kbbk...kbbbbbbkoo",
			".kbbbkkkbbbbbbbk..",
			"..kbbbbwwwwwbllk..",
			"...kkbwwwwwwbllk..",
			"....kwwwwwwkbllk..",
			"...kwwwwwkkbllk...",
			"....kkkkk.kkkk....",
			"..................",
		};

		static readonly string[] Bee1 =
		{
			".....kkk.kkk...",
			"....kwwwkwwwk..",
			"....kwlwkwlwk..",
			".....kwwkwwk...",
			"...kkkkkkkkkk..",
			"..kyykyykyykkk.",
			".kyyykyykyykkek",
			"kkyyykyykyykkkk",
			".kyyykyykyykkk.",
			"..kyykyykyykk..",
			"...kkkkkkkkk...",
			"....k...k......",
		};
		static readonly string[] Bee2 =
		{
			"...............",
			"...............",
			"...............",
			"..kkwwkkwwk....",
			".kwwlwwkwlwwk..",
			"..kkkkkkkkkkk..",
			"..kyykyykyykkk.",
			".kyyykyykyykkek",
			"kkyyykyykyykkkk",
			".kyyykyykyykkk.",
			"..kyykyykyykk..",
			"...kkkkkkkkk...",
		};

		static readonly string[] LadyWalk =
		{
			"...k.....k...",
			"....k...k....",
			".....kkk.....",
			"....kkkkk....",
			"..kkkrkrkkk..",
			".krrrrkrrrrk.",
			"krkkrrkrrkkrk",
			"krkkrrkrrkkrk",
			"krrrrrkrrrrrk",
			"krrkkrkrkkrrk",
			".krkkrkrkkrk.",
			"..krrrkrrrk..",
			"...kkkkkkk...",
			"..k.k...k.k..",
		};
		static readonly string[] LadyFly =
		{
			"...k.....k...",
			"....k...k....",
			".....kkk.....",
			".kk.kkkkk.kk.",
			"krrkk.k.kkrrk",
			"krkkrkwkrkkrk",
			"krkkrkwkrkkrk",
			"krrrkwwwkrrrk",
			"krrkkwwwkkrrk",
			".kkkwwlwwkkk.",
			"...kwwlwwk...",
			"...kwlllwk...",
			"....kwwwk....",
			".....kkk.....",
		};

		/// <summary>The same drawing with the wings folded towards the body (rows 0-2, the antennae, stay).</summary>
		static string[] Fold(string[] open, double f)
		{
			int c = open[0].Length / 2;
			return open.Select((row, j) => j < 3 ? row : new string(Enumerable.Range(0, row.Length).Select(x =>
			{
				int src = (int)Math.Round(c + (x - c) / f);
				return src >= 0 && src < row.Length ? row[src] : '.';
			}).ToArray())).ToArray();
		}

		/// <summary>The drawings of one animal, with their colors (wings and body of a random color).</summary>
		internal static string[][] Drawings(Kind kind) => kind switch
		{
			Kind.Butterfly => new[] { ButterflyOpen, Fold(ButterflyOpen, 0.62), Fold(ButterflyOpen, 0.22), Fold(ButterflyOpen, 0.62) },
			Kind.Bird => new[] { BirdFly1, BirdFly2, BirdPerch, BirdPeck },
			Kind.Bee => new[] { Bee1, Bee2 },
			_ => new[] { LadyFly, LadyWalk },
		};

		internal static Dictionary<char, Color> Colors(Kind kind, Random r)
		{
			var p = new Dictionary<char, Color> { ['k'] = Color.FromArgb(45, 32, 36), ['e'] = Color.FromArgb(15, 15, 18) };
			switch (kind)
			{
				case Kind.Butterfly:
					var wings = new[] { (Color.FromArgb(245, 140, 40), Color.FromArgb(185, 80, 20)), (Color.FromArgb(90, 160, 245), Color.FromArgb(40, 90, 190)),
						(Color.FromArgb(240, 110, 200), Color.FromArgb(170, 60, 140)), (Color.FromArgb(255, 210, 60), Color.FromArgb(200, 140, 20)) }[r.Next(4)];
					p['w'] = wings.Item1; p['d'] = wings.Item2; p['y'] = Color.FromArgb(255, 248, 225); p['l'] = Color.FromArgb(255, 225, 160); p['b'] = Color.FromArgb(70, 48, 36);
					break;
				case Kind.Bird:
					var body = new[] { (Color.FromArgb(80, 140, 225), Color.FromArgb(45, 90, 175)), (Color.FromArgb(215, 75, 60), Color.FromArgb(160, 45, 40)),
						(Color.FromArgb(120, 175, 90), Color.FromArgb(75, 125, 60)) }[r.Next(3)];
					p['b'] = body.Item1; p['w'] = body.Item2; p['l'] = Color.FromArgb(250, 225, 185); p['o'] = Color.FromArgb(250, 170, 40); p['f'] = Color.FromArgb(225, 140, 60);
					break;
				case Kind.Bee:
					p['y'] = Color.FromArgb(255, 205, 40); p['w'] = Color.FromArgb(215, 235, 250); p['l'] = Color.FromArgb(250, 253, 255);
					break;
				default:
					p['r'] = Color.FromArgb(225, 40, 40); p['w'] = Color.FromArgb(205, 225, 240); p['l'] = Color.FromArgb(245, 250, 255);
					break;
			}
			return p;
		}

		sealed class VisitorForm : Form
		{
			public readonly Kind What;
			readonly FormPet pet;
			readonly Timer tick = new Timer { Interval = 40 };
			readonly Bitmap[] frames, flipped;
			readonly Rectangle screen;
			readonly int side;          // +1 came in from the right (leaves to the left), -1 the other way
			double x, y, t, phaseT, lastX;
			int phase;                  // 0 flying in, 1 the visit, 2 flying on to the other side
			int frame;
			bool faceLeft;

			public VisitorForm(FormPet pet, Kind kind)
			{
				this.pet = pet; What = kind;
				int px = Math.Max(1, (int)Math.Round(pet.Height / 40.0));   // small next to the pet (1 px at 40 px tall), bigger with it
				var colors = Colors(kind, rnd);
				frames = Drawings(kind).Select(d => Draw(d, colors, px, false)).ToArray();
				flipped = Drawings(kind).Select(d => Draw(d, colors, px, true)).ToArray();
				FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
				BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
				Size = new Size(frames.Max(b => b.Width), frames.Max(b => b.Height));
				screen = Screen.FromControl(pet).WorkingArea;
				side = pet.Left + pet.Width / 2 > screen.Left + screen.Width / 2 ? 1 : -1;
				x = side > 0 ? screen.Right + 4 : screen.Left - Width - 4;
				y = Math.Max(screen.Top + 10, pet.Top - 70);
				lastX = x;
				Location = new Point((int)x, (int)y);
				tick.Tick += (s, e) => Step();
				tick.Start();
			}

			protected override CreateParams CreateParams
			{
				get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 /* no activate */ | 0x80 /* tool window */ | 0x20 /* click-through */; return cp; }
			}
			protected override bool ShowWithoutActivation => true;

			static Bitmap Draw(string[] rows, Dictionary<char, Color> colors, int px, bool flip)
			{
				int w = rows.Max(r => r.Length);
				var b = new Bitmap(w * px, rows.Length * px);
				using (var g = Graphics.FromImage(b))
				{
					g.Clear(Color.Magenta);
					for (int j = 0; j < rows.Length; j++)
						for (int i = 0; i < rows[j].Length; i++)
						{
							if (!colors.TryGetValue(rows[j][i], out var col)) continue;
							using (var br = new SolidBrush(col)) g.FillRectangle(br, (flip ? w - 1 - i : i) * px, j * px, px, px);
						}
				}
				return b;
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var set = faceLeft ? flipped : frames;
				var img = set[Math.Min(frame, set.Length - 1)];
				e.Graphics.DrawImageUnscaled(img, (Width - img.Width) / 2, Height - img.Height);
			}

			// where the visitor stands on the head, and a point above it
			double HeadX => pet.Left + pet.Width / 2.0 - Width / 2.0;
			double HeadY => pet.Top + pet.Height * 0.35 - Height;

			void Step()
			{
				if (pet.IsDisposed || !pet.Visible) { Close(); return; }
				t += 0.04; phaseT += 0.04;
				double hx = HeadX, hy = HeadY;
				switch (phase)
				{
					case 0:   // fly to the pet, bobbing
						x += Math.Sign(hx - x) * Math.Min(What == Kind.Bee ? 6 : 4.5, Math.Abs(hx - x));
						y += (hy - 30 - y) * 0.06 + Math.Sin(t * (What == Kind.Bee ? 14 : 6)) * 1.5;
						if (Math.Abs(hx - x) < 6) { phase = 1; phaseT = 0; pet.React("look around", "happy", "wave"); }
						break;
					case 1:
						if (Visit(hx, hy)) { phase = 2; phaseT = 0; pet.React("happy", "laugh", "wave"); }
						break;
					default:  // on to the other side of the screen, a little upwards, then gone
						x -= side * (What == Kind.Bee ? 10 : 8);
						y = Math.Max(screen.Top + 4, y - 0.7 + Math.Sin(t * (What == Kind.Bee ? 14 : 5)) * (What == Kind.Butterfly ? 2.2 : 1));
						if (x + Width < screen.Left - 2 || x > screen.Right + 2 || phaseT > 40) { Close(); return; }
						break;
				}
				Pick();
				if (Math.Abs(x - lastX) > 0.5) faceLeft = x < lastX;
				lastX = x;
				Location = new Point((int)Math.Round(x), (int)Math.Round(y));
				Invalidate();
			}

			/// <summary>One step of the visit itself; true when it is over.</summary>
			bool Visit(double hx, double hy)
			{
				switch (What)
				{
					case Kind.Butterfly:   // around the pet, then a rest on its head
						if (phaseT < 6) { x = hx + Math.Cos(t * 2.2) * pet.Width * 0.45; y = hy - 22 + Math.Sin(t * 4.4) * 12; }
						else { x += (hx - x) * 0.2; y += (hy - y) * 0.2; }
						return phaseT > 9;
					case Kind.Bird:        // perches, hops a little and pecks
					{
						x += (hx - x) * 0.25;
						double hop = phaseT % 1.6 < 0.3 ? Math.Sin(phaseT % 1.6 / 0.3 * Math.PI) * 6 : 0;
						y += (hy - hop - y) * 0.35;
						return phaseT > 6;
					}
					case Kind.Bee:         // buzzes around in a figure of eight
						x = hx + Math.Sin(t * 2.6) * pet.Width * 0.6 + Math.Sin(t * 23) * 1.5;
						y = hy - 18 + Math.Sin(t * 5.2) * 14 + Math.Cos(t * 19) * 1.5;
						if (phaseT > 1 && phaseT < 1.05) pet.React("scared", "surprised", "look around");
						return phaseT > 6;
					default:               // lands and crawls a little from side to side
						x += (hx + Math.Sin(phaseT * 1.3) * pet.Width * 0.18 - x) * 0.25;
						y += (hy - y) * 0.3;
						return phaseT > 7;
				}
			}

			/// <summary>The drawing to show now.</summary>
			void Pick()
			{
				switch (What)
				{
					case Kind.Butterfly:
						bool resting = phase == 1 && phaseT >= 6.5;
						frame = resting ? new[] { 0, 1, 2, 1, 0, 0, 0, 0 }[(int)(phaseT * 8) % 8] : (int)(t * 16) % 4;   // resting: a slow flap now and then
						break;
					case Kind.Bird:
						bool perched = phase == 1 && Math.Abs(HeadY - y) < 8;
						frame = perched ? (phaseT % 1.6 >= 0.8 && phaseT % 1.6 < 1.2 ? 3 : 2) : (int)(t * 10) % 2;
						break;
					case Kind.Bee:
						frame = (int)(t * 25) % 2;
						break;
					default:
						frame = phase == 1 && phaseT > 0.6 ? 1 : 0;
						break;
				}
			}

			protected override void OnFormClosed(FormClosedEventArgs e)
			{
				tick.Stop(); tick.Dispose();
				foreach (var b in frames.Concat(flipped)) b.Dispose();
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

	// ====================================================================== 12. memory

	/// <summary>
	/// Off unless turned on. Watches how much of the memory (RAM) is in use; when it stays high for a few minutes the pet
	/// offers to tidy it up (or does it by itself). Simple tidy: every program it may open gives back the memory it is not
	/// using (no administrator needed). Full tidy: also empties the standby, modified and file caches of Windows, through
	/// a short-lived copy of the program started as administrator (Windows asks first). Our own code (Windows API).
	/// </summary>
	public static class MemoryCare
	{
		static Timer timer;
		static DateTime? highSince;
		static DateTime lastClean = DateTime.MinValue, lastAsk = DateTime.MinValue;
		static bool cleaning;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("MemoryCare", false);
			set { Program.MyData.SetSetting("MemoryCare", value); Apply(); }
		}

		/// <summary>Percentage of memory in use that counts as high.</summary>
		public static int Limit
		{
			get { int v = Program.MyData.GetSettingInt("MemoryLimit", 85); return v >= 50 && v <= 98 ? v : 85; }
			set => Program.MyData.SetSetting("MemoryLimit", value);
		}

		/// <summary>Tidy by itself (simple tidy) instead of asking first.</summary>
		public static bool Auto
		{
			get => Program.MyData.GetSettingBool("MemoryAuto", false);
			set => Program.MyData.SetSetting("MemoryAuto", value);
		}

		/// <summary>Minutes the memory has to stay high, and minimum minutes between two tidies / two questions.</summary>
		internal const int HighMinutes = 2, CleanEveryMinutes = 10, AskEveryMinutes = 30;

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 30000 }; timer.Tick += (s, e) => Check(DateTime.Now, Status().load); }
			if (Enabled) timer.Start(); else { timer.Stop(); highSince = null; }
		}

		// ------------------------------------------------------------------ reading the memory

		[StructLayout(LayoutKind.Sequential)]
		struct MEMORYSTATUSEX
		{
			public uint dwLength, dwMemoryLoad;
			public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
		}
		[DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

		/// <summary>Memory in use now (percentage), total and available (bytes).</summary>
		public static (int load, long total, long available) Status()
		{
			var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
			if (!GlobalMemoryStatusEx(ref m)) return (0, 0, 0);
			return ((int)m.dwMemoryLoad, (long)m.ullTotalPhys, (long)m.ullAvailPhys);
		}

		/// <summary>"1,2 GB" / "350 MB".</summary>
		public static string Size(long bytes)
		{
			double mb = Math.Abs(bytes) / 1048576.0;
			return mb >= 1024 ? (mb / 1024).ToString("0.0") + " GB" : Math.Round(mb) + " MB";
		}

		// ------------------------------------------------------------------ when to offer

		/// <summary>What to do now: nothing, ask, or tidy. Separate from the clock, so the self-test can play with time.</summary>
		internal enum Decision { None, Ask, Clean }

		internal static Decision Decide(DateTime now, int load, int limit, bool auto, ref DateTime? since, DateTime lastClean, DateTime lastAsk, bool quiet)
		{
			if (load < limit) { since = null; return Decision.None; }
			if (since == null) since = now;
			if (quiet || (now - since.Value).TotalMinutes < HighMinutes) return Decision.None;
			if ((now - lastClean).TotalMinutes < CleanEveryMinutes) return Decision.None;
			if (auto) return Decision.Clean;
			return (now - lastAsk).TotalMinutes < AskEveryMinutes ? Decision.None : Decision.Ask;
		}

		static void Check(DateTime now, int load)
		{
			if (!Enabled || cleaning) return;
			bool quiet = SpeechBubble.QuietNow || FullScreenWatch.Active;   // never in the middle of a game or a video
			switch (Decide(now, load, Limit, Auto, ref highSince, lastClean, lastAsk, quiet))
			{
				case Decision.Clean: Tidy(false); break;
				case Decision.Ask:
					lastAsk = now;
					SpeechBubble.Notify(Lang.T("mem.high", load), 0,
						new SpeechBubble.Choice(Lang.T("mem.tidy"), () => Tidy(false), true),
						new SpeechBubble.Choice(Lang.T("mem.later"), null));
					break;
			}
		}

		// ------------------------------------------------------------------ tidying

		const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, PROCESS_SET_QUOTA = 0x0100;
		[DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
		[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
		[DllImport("psapi.dll", SetLastError = true)] static extern bool EmptyWorkingSet(IntPtr process);
		[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int pid);

		/// <summary>
		/// Simple tidy: every process this user may open gives back the memory it is not using (it comes back from the
		/// disk cache when needed). The program in front is left alone, so what you are using does not slow down.
		/// Returns how many programs were tidied.
		/// </summary>
		public static int TrimPrograms()
		{
			GetWindowThreadProcessId(GetForegroundWindow(), out int front);
			int n = 0;
			foreach (var p in Process.GetProcesses())
			{
				try
				{
					if (p.Id <= 4 || p.Id == front) continue;
					IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
					if (h == IntPtr.Zero) continue;   // a program of the system or of another user: not ours to touch
					try { if (EmptyWorkingSet(h)) n++; } finally { CloseHandle(h); }
				}
				catch (Exception) { }
				finally { p.Dispose(); }
			}
			return n;
		}

		[DllImport("ntdll.dll")] static extern int NtSetSystemInformation(int infoClass, ref int info, int length);
		[DllImport("kernel32.dll", SetLastError = true)] static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, uint flags);
		[DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValue(string system, string name, out long luid);
		[DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, int length, IntPtr previous, IntPtr returnLength);
		[DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
		[StructLayout(LayoutKind.Sequential, Pack = 4)] struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attributes; }

		const int SystemMemoryListInformation = 80;
		const int MemoryEmptyWorkingSets = 2, MemoryFlushModifiedList = 3, MemoryPurgeStandbyList = 4;

		static bool Privilege(string name)
		{
			if (!OpenProcessToken(GetCurrentProcess(), 0x0020 /* adjust */ | 0x0008 /* query */, out IntPtr token)) return false;
			try
			{
				if (!LookupPrivilegeValue(null, name, out long luid)) return false;
				var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = 2 /* enabled */ };
				return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastWin32Error() == 0;
			}
			finally { CloseHandle(token); }
		}

		static bool MemoryList(int command) { int c = command; return NtSetSystemInformation(SystemMemoryListInformation, ref c, sizeof(int)) == 0; }

		/// <summary>True if this copy of the program runs as administrator.</summary>
		public static bool IsAdmin
		{
			get { using (var id = System.Security.Principal.WindowsIdentity.GetCurrent()) return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); }
		}

		/// <summary>
		/// Full tidy (needs administrator): the working sets of every process, the file cache, the modified pages
		/// (written to disk) and the standby list. Returns one line per step: "name ok" or "name refused".
		/// </summary>
		public static List<string> FullClean()
		{
			var steps = new List<string>();
			bool profile = Privilege("SeProfileSingleProcessPrivilege"), quota = Privilege("SeIncreaseQuotaPrivilege");
			steps.Add("workingsets " + (profile && MemoryList(MemoryEmptyWorkingSets) ? "ok" : "refused"));
			steps.Add("filecache " + (quota && SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0) ? "ok" : "refused"));
			steps.Add("modified " + (profile && MemoryList(MemoryFlushModifiedList) ? "ok" : "refused"));
			steps.Add("standby " + (profile && MemoryList(MemoryPurgeStandbyList) ? "ok" : "refused"));
			return steps;
		}

		/// <summary>"--clean-memory=&lt;file&gt;": the copy started as administrator does the full tidy, writes how it went, and ends.</summary>
		public static void FullCleanToFile(string file)
		{
			var steps = IsAdmin ? FullClean() : new List<string> { "admin refused" };
			try { File.WriteAllLines(file, steps); } catch (Exception) { }
		}

		/// <summary>Full tidy from this copy: directly if it is already administrator, else through an elevated copy.</summary>
		static async Task<List<string>> FullCleanElevated()
		{
			if (IsAdmin) return await Task.Run(FullClean);
			string file = Path.Combine(Path.GetTempPath(), "desktoppet-memory-" + Guid.NewGuid().ToString("N") + ".txt");
			var psi = new ProcessStartInfo(Environment.ProcessPath, "\"--clean-memory=" + file + "\"") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
			try
			{
				using (var p = Process.Start(psi))
					await Task.Run(() => p.WaitForExit(60000));
				return File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string> { "admin refused" };
			}
			catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return null; }   // "No" on the Windows question
			finally { try { File.Delete(file); } catch (Exception) { } }
		}

		/// <summary>Tidy now and tell how much was freed (simple, or full with administrator).</summary>
		public static async void Tidy(bool full)
		{
			if (cleaning) return;
			cleaning = true;
			try
			{
				var before = Status();
				Program.Mainthread?.ReactAll("clean", "sweep", "work", "happy");
				List<string> steps = null;
				if (full)
				{
					steps = await FullCleanElevated();
					if (steps == null) { SpeechBubble.Say(Lang.T("mem.adminNo"), 6); return; }
				}
				await Task.Run(TrimPrograms);
				await Task.Delay(1500);   // Windows takes a moment to count the memory again
				var after = Status();
				lastClean = DateTime.Now; highSince = null;
				long freed = after.available - before.available;
				bool refused = steps != null && steps.Any(s => s.EndsWith("refused"));
				string text = freed > 50L * 1048576 ? Lang.T("mem.freed", Size(freed), after.load) : Lang.T("mem.nothing", after.load);
				if (refused) text += " " + Lang.T("mem.partly");
				Program.Mainthread?.ReactAll("happy", "cheer", "dance");
				SpeechBubble.Say(text, 8);
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "memory tidy: " + ex.Message); }
			finally { cleaning = false; }
		}
	}

	// ====================================================================== 13. simple timer

	/// <summary>
	/// Off unless turned on. A timer that stays with the pet: a small panel under it (beside it when the pet stands
	/// on the taskbar) with the time left, a progress bar, Pause, +5 min and Cancel. When the time is up the pet
	/// reacts and the panel stays, saying so, until it is closed.
	/// </summary>
	public static class SimpleTimer
	{
		static Timer tick;
		static TimerPanel panel;
		static DateTime end;
		static TimeSpan total, left;
		static bool paused;

		/// <summary>Minutes offered in the menu.</summary>
		internal static readonly int[] Presets = { 1, 5, 10, 15, 25, 30, 60 };

		/// <summary>A timer is on screen (running, paused or finished and not closed yet).</summary>
		public static bool Active => total > TimeSpan.Zero;
		public static bool Finished { get; private set; }
		public static bool Running => Active && !Finished;
		public static bool Paused => paused && Running;

		public static TimeSpan Left
		{
			get
			{
				if (!Active || Finished) return TimeSpan.Zero;
				if (paused) return left;
				var l = end - DateTime.Now;
				return l > TimeSpan.Zero ? l : TimeSpan.Zero;
			}
		}

		/// <summary>0 at the start, 1 when the time is up.</summary>
		public static double Progress => !Active ? 0 : Finished ? 1 : Math.Max(0, Math.Min(1, 1 - Left.TotalSeconds / total.TotalSeconds));

		public static string LeftText => Format(Left);

		/// <summary>"4:59", "1:00:00" (the seconds rounded up, so it never shows 0:00 before the end).</summary>
		public static string Format(TimeSpan t)
		{
			t = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, t.TotalSeconds)));
			return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
		}

		public static void Start(TimeSpan length)
		{
			if (length <= TimeSpan.Zero) return;
			total = left = length;
			end = DateTime.Now + length;
			paused = false;
			Finished = false;
			if (tick == null) { tick = new Timer { Interval = 200 }; tick.Tick += (s, e) => Step(); }
			tick.Start();
			if (panel == null || panel.IsDisposed) panel = new TimerPanel();
			Step();
			Program.Mainthread?.ReactAll("ok", "wave", "happy");
		}

		public static void TogglePause()
		{
			if (!Running) return;
			if (paused) { end = DateTime.Now + left; paused = false; }
			else { left = Left; paused = true; }
			Step();
		}

		/// <summary>More time (after the end: a new timer of that length).</summary>
		public static void AddMinutes(int minutes)
		{
			var more = TimeSpan.FromMinutes(minutes);
			if (!Active || Finished) { Start(more); return; }
			total += more;
			if (paused) left += more; else end += more;
			Step();
		}

		/// <summary>Cancel it, or close it when the time is up.</summary>
		public static void Close()
		{
			total = TimeSpan.Zero;
			Finished = paused = false;
			tick?.Stop();
			if (panel != null && !panel.IsDisposed) panel.Close();
			panel = null;
		}

		static void Step()
		{
			if (!Active) { tick?.Stop(); return; }
			if (Running && !paused && DateTime.Now >= end) { Finished = true; TimeUp(); }
			panel?.Follow();
		}

		static void TimeUp()
		{
			Program.Mainthread?.ReactAll("surprise", "jump", "wave", "happy");
			if (!SpeechBubble.QuietNow) System.Media.SystemSounds.Asterisk.Play();
			if (HideHotkey.Hidden) SpeechBubble.Notify(Lang.T("timer.done"));   // the pets are hidden: a notification instead of the panel
		}

		/// <summary>"10" → 10 minutes, "1:30" → 1 min 30 s, "1:00:00" → 1 hour, "90s", "2h". Null if it cannot be read.</summary>
		internal static TimeSpan? Parse(string text)
		{
			text = (text ?? "").Trim().ToLowerInvariant().Replace(',', '.').Replace(" ", "");
			if (text.Length == 0) return null;
			TimeSpan t;
			var parts = text.Split(':');
			if (parts.Length >= 2 && parts.Length <= 3 && parts.All(p => p.Length > 0 && p.All(char.IsDigit)))
			{
				var n = parts.Select(int.Parse).ToArray();
				t = n.Length == 3 ? new TimeSpan(n[0], n[1], n[2]) : new TimeSpan(0, n[0], n[1]);
			}
			else
			{
				var m = System.Text.RegularExpressions.Regex.Match(text, @"^(\d+(?:\.\d+)?)(h|hora|horas|min|m|minutos?|s|seg|segundos?)?$");
				if (!m.Success) return null;
				double v = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
				string unit = m.Groups[2].Value;
				t = unit.StartsWith("h") ? TimeSpan.FromHours(v) : unit.StartsWith("s") ? TimeSpan.FromSeconds(v) : TimeSpan.FromMinutes(v);
			}
			return t >= TimeSpan.FromSeconds(1) && t <= TimeSpan.FromHours(24) ? t : (TimeSpan?)null;
		}

		/// <summary>Ask how long (the Plugins card and "Other time…" in the menu).</summary>
		public static void AskAndStart()
		{
			string answer = FormLaunchers.Prompt(Lang.T("timer.ask"), "10");
			if (answer == null || answer.Trim().Length == 0) return;
			var t = Parse(answer);
			if (t == null) { SpeechBubble.Say(Lang.T("timer.bad"), 8); return; }
			Start(t.Value);
		}

		/// <summary>Self-test: the panel on screen.</summary>
		internal static Form Panel => panel != null && !panel.IsDisposed && panel.Visible ? panel : null;
		/// <summary>Self-test: click one of the panel's buttons ("pause", "add", "cancel", "close").</summary>
		internal static void TestPress(string button) => panel?.Press(button);
		/// <summary>Self-test: the time is up now.</summary>
		internal static void TestEndNow() { if (Running) { paused = false; end = DateTime.Now; Step(); } }

		/// <summary>The panel: time left, progress bar and buttons. Never takes the focus; follows the pet.</summary>
		sealed class TimerPanel : Form
		{
			const int W = 264, H = 92;
			readonly Font font = Theme.CreateFont(12.5f), bold = Theme.CreateFont(12.5f, FontStyle.Bold), icon = Theme.CreateFont(13);
			readonly List<(Rectangle rect, string id, string text)> buttons = new List<(Rectangle, string, string)>();
			string hover;

			public TimerPanel()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
				BackColor = Theme.Surface;
				Size = new Size(W, H);
				using (var path = Theme.RoundedRect(new RectangleF(0, 0, W, H), 14)) Region = new Region(path);
			}

			protected override bool ShowWithoutActivation => true;

			protected override CreateParams CreateParams
			{
				get
				{
					var cp = base.CreateParams;
					cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00000008;   // tool window, no activate, topmost
					return cp;
				}
			}

			/// <summary>Under the pet; beside it (left) when the pet stands on the bottom of the screen.</summary>
			public void Follow()
			{
				var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0 && !p.Dying);
				if (pet == null || HideHotkey.Hidden) { if (Visible) Hide(); return; }
				var wa = Screen.FromControl(pet).WorkingArea;
				int x = pet.Left + pet.Width / 2 - W / 2, y = pet.Bottom + 6;
				if (y + H > wa.Bottom)
				{
					y = pet.Bottom - H;
					x = pet.Left - W - 8;
					if (x < wa.Left) x = pet.Right + 8 + 40;   // the right side also has the hover buttons
				}
				x = Math.Max(wa.Left + 4, Math.Min(wa.Right - W - 4, x));
				y = Math.Max(wa.Top + 4, Math.Min(wa.Bottom - H - 4, y));
				if (Left != x || Top != y) Location = new Point(x, y);
				if (!Visible) Show();
				Invalidate();
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Surface);
				using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, W - 1, H - 1), 14))
				using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);

				// ⏱ Timer · 4:59 left
				Theme.DrawText(g, "⏱", icon, new Rectangle(10, 8, 22, 22), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				string line = Finished ? Lang.T("timer.done") : Lang.T("timer.title") + " · " + Lang.T(Paused ? "timer.pausedLeft" : "timer.left", LeftText);
				Theme.DrawText(g, line, Finished ? bold : font, new Rectangle(34, 8, W - 44, 22), Finished ? Theme.Text : Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

				// progress bar
				var bar = new Rectangle(12, 36, W - 24, 6);
				Theme.FillRounded(g, Theme.SurfaceAlt, bar, 3);
				int done = (int)Math.Round(bar.Width * Progress);
				if (done >= 6) Theme.FillRounded(g, Paused ? Theme.TextMuted : Finished ? ChatIcons.Green : Theme.Accent, new Rectangle(bar.X, bar.Y, done, bar.Height), 3);

				// buttons
				buttons.Clear();
				if (Finished) Layout2(g, ("close", Lang.T("timer.close")), ("add", Lang.T("timer.add5")));
				else Layout2(g, ("pause", Lang.T(Paused ? "timer.resume" : "timer.pause")), ("add", Lang.T("timer.add5")), ("cancel", Lang.T("timer.cancel")));
				foreach (var (r, id, text) in buttons)
				{
					bool main = id == "pause" || id == "close", danger = id == "cancel";
					var fill = main ? Theme.Accent : danger ? Theme.Danger : Theme.SurfaceAlt;
					if (hover == id) fill = main || danger ? ControlPaint.Light(fill, 0.25f) : Theme.Hover;
					Theme.FillRounded(g, fill, r, 8);
					if (!main && !danger) Theme.DrawRounded(g, Theme.Border, r, 8);
					Theme.DrawText(g, text, bold, r, main ? Theme.AccentText : danger ? Color.White : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
			}

			/// <summary>The buttons side by side, each as wide as its text needs (the spare room shared out).</summary>
			void Layout2(Graphics g, params (string id, string text)[] items)
			{
				int gap = 6, room = W - 24 - gap * (items.Length - 1);
				var need = items.Select(it => Theme.MeasureText(g, it.text, bold, int.MaxValue, TextFormatFlags.SingleLine).Width + 16).ToArray();
				int spare = Math.Max(0, room - need.Sum()) / items.Length;
				int x = 12;
				for (int i = 0; i < items.Length; i++)
				{
					int w = i == items.Length - 1 ? W - 12 - x : need[i] + spare;
					buttons.Add((new Rectangle(x, 52, w, 28), items[i].id, items[i].text));
					x += w + gap;
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				string h = buttons.FirstOrDefault(b => b.rect.Contains(e.Location)).id;
				Cursor = h != null ? Cursors.Hand : Cursors.Default;
				if (h != hover) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				string id = buttons.FirstOrDefault(b => b.rect.Contains(e.Location)).id;
				if (id != null) Press(id);
			}

			public void Press(string id)
			{
				switch (id)
				{
					case "pause": TogglePause(); break;
					case "add": AddMinutes(5); break;
					case "cancel": case "close": SimpleTimer.Close(); break;
				}
			}

			protected override void Dispose(bool disposing) { if (disposing) { font.Dispose(); bold.Dispose(); icon.Dispose(); } base.Dispose(disposing); }
		}
	}
}
