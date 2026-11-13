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
			get => Program.MyData.GetSettingBool("Visitors", true);
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
}
