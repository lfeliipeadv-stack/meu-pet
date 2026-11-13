using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet
{
	/// <summary>
	/// The pet reacts to Claude Code: hooks of Claude Code (see <see cref="ClaudeHooks"/>) post each event to a tiny
	/// HTTP endpoint of this program on 127.0.0.1 only, with a random secret token. The pet then thinks, types,
	/// runs, waits for you, celebrates or gets sad. Nothing of your prompt, code, paths or output is ever shown:
	/// only the kind of event and the tool name are read.
	/// </summary>
	public static class AgentBridge
	{
		public enum Activity { None, Hello, Thinking, Reading, Editing, Running, Searching, Delegating, Working, Waiting, Done, Error }

		public const int DefaultPort = 45871;
		public static int Port => Program.MyData.GetSettingInt("AgentPort", DefaultPort);

		/// <summary>Secret sent by the hooks in the X-DesktopPet header (created once).</summary>
		public static string Token
		{
			get
			{
				string t = Program.MyData.GetSetting("AgentToken");
				if (t.Length < 32)
				{
					var bytes = new byte[24];
					RandomNumberGenerator.Fill(bytes);
					t = Convert.ToHexString(bytes).ToLowerInvariant();
					Program.MyData.SetSetting("AgentToken", t);
				}
				return t;
			}
		}

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("AgentBridge", false);
			set { Program.MyData.SetSetting("AgentBridge", value); Apply(); }
		}

		/// <summary>Also show what Claude is doing in bubbles (thinking, editing…), not only when it is done or needs you.</summary>
		public static bool ProgressBubbles
		{
			get => Program.MyData.GetSettingBool("AgentBubbles", false);
			set => Program.MyData.SetSetting("AgentBubbles", value);
		}

		public static bool Listening => listener != null;

		/// <summary>Listen when Claude Code, the other AI agents or the local API need it.</summary>
		static bool Wanted => Enabled || DevListen || LocalApi.Enabled || OtherAgents.AnyInstalled;

		/// <summary>Last activity (for the self-test and the menu).</summary>
		public static Activity Last { get; private set; }

		static TcpListener listener;
		static CancellationTokenSource cancel;
		static SynchronizationContext ui;
		static Activity shown = Activity.None;
		static DateTime shownAt = DateTime.MinValue;

		/// <summary>Start or stop listening, following the setting. Call from the UI thread.</summary>
		public static void Apply()
		{
			ui = SynchronizationContext.Current ?? ui;
			if (Wanted && listener == null)
			{
				try
				{
					listener = new TcpListener(IPAddress.Loopback, Port);
					listener.Start();
					cancel = new CancellationTokenSource();
					_ = AcceptLoop(listener, cancel.Token);
					StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.info, "agent bridge on 127.0.0.1:" + Port);
					WriteConnectionFile();
				}
				catch (Exception ex)
				{
					listener = null;
					StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "agent bridge: " + ex.Message);
				}
			}
			else if (!Wanted && listener != null)
			{
				cancel?.Cancel();
				try { listener.Stop(); } catch (Exception skipped) { LogFile.Ignored(skipped); }
				listener = null;
			}
		}

		static async Task AcceptLoop(TcpListener l, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				TcpClient client;
				try { client = await l.AcceptTcpClientAsync(ct); }
				catch (Exception) { return; }
				_ = Task.Run(() => Serve(client));
			}
		}

		/// <summary>One request: "POST /hook/&lt;Event&gt;" with the hook JSON, or "POST /say" with a short text.</summary>
		static async Task Serve(TcpClient client)
		{
			using (client)
			{
				try
				{
					client.ReceiveTimeout = 3000;
					var stream = client.GetStream();
					var head = await ReadHead(stream);
					if (head == null) return;
					var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
					var first = lines[0].Split(' ');
					string method = first.Length > 0 ? first[0] : "", path = first.Length > 1 ? first[1] : "";
					string token = "", host = ""; long length = 0; bool hasOrigin = false;
					foreach (var h in lines)
					{
						int c = h.IndexOf(':');
						if (c <= 0) continue;
						string name = h.Substring(0, c).Trim().ToLowerInvariant(), value = h.Substring(c + 1).Trim();
						if (name == "x-desktoppet") token = value;
						else if (name == "content-length") long.TryParse(value, out length);
						else if (name == "host") host = value;
						else if (name == "origin") hasOrigin = true;
					}
					// only local tools: never a web page (Origin) and the right secret
					bool hostOk = host == "127.0.0.1:" + Port || host == "localhost:" + Port;
					if (method != "POST" || hasOrigin || !hostOk || !FixedEquals(token, Token))
					{
						await Reply(stream, "403 Forbidden");
						return;
					}
					string body = await ReadBody(stream, Math.Min(length, 2 * 1024 * 1024));
					await Reply(stream, "204 No Content");
					if (path.StartsWith("/hook/", StringComparison.Ordinal)) OnHook(path.Substring(6), body);
					else if (path == "/say") { if (DevListen) DevNote("say " + body); Post(() => Say(body)); }
					else if (path == "/react") { var a = ReactionByName(body); if (a != Activity.None) Post(() => React(a)); }
				}
				catch (Exception skipped) { LogFile.Ignored(skipped); }
			}
		}

		static async Task<string> ReadHead(NetworkStream s)
		{
			var buf = new MemoryStream();
			var one = new byte[1];
			while (buf.Length < 16 * 1024)
			{
				int n = await s.ReadAsync(one, 0, 1);
				if (n == 0) return null;
				buf.WriteByte(one[0]);
				var b = buf.GetBuffer();
				long len = buf.Length;
				if (len >= 4 && b[len - 4] == '\r' && b[len - 3] == '\n' && b[len - 2] == '\r' && b[len - 1] == '\n')
					return Encoding.ASCII.GetString(b, 0, (int)len - 4);
			}
			return null;
		}

		static async Task<string> ReadBody(NetworkStream s, long length)
		{
			if (length <= 0) return "";
			var buf = new byte[length];
			int read = 0;
			while (read < length)
			{
				int n = await s.ReadAsync(buf, read, (int)length - read);
				if (n == 0) break;
				read += n;
			}
			return Encoding.UTF8.GetString(buf, 0, read);
		}

		static Task Reply(NetworkStream s, string status)
		{
			var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
			return s.WriteAsync(bytes, 0, bytes.Length);
		}

		static bool FixedEquals(string a, string b) =>
			a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

		static void Post(Action a) { if (ui != null) ui.Post(_ => a(), null); else a(); }

		// ------------------------------------------------------------------ what happened → how the pet reacts

		/// <summary>What a hook event means. Only the event name, the tool name and the error flag are read.</summary>
		public static Activity Classify(string hookEvent, string json)
		{
			string tool = "";
			bool error = false;
			try
			{
				using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json))
				{
					var root = doc.RootElement;
					if (root.TryGetProperty("hook_event_name", out var ev) && ev.ValueKind == JsonValueKind.String) hookEvent = ev.GetString();
					if (root.TryGetProperty("tool_name", out var tn) && tn.ValueKind == JsonValueKind.String) tool = tn.GetString();
					if (root.TryGetProperty("tool_response", out var resp) && resp.ValueKind == JsonValueKind.Object)
						error = (resp.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True)
							|| (resp.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False);
				}
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }

			switch (hookEvent)
			{
				case "UserPromptSubmit": return Activity.Thinking;
				case "Notification": return Activity.Waiting;
				case "Stop": return Activity.Done;
				case "PostToolUseFailure": return Activity.Error;
				case "PostToolUse": return error ? Activity.Error : Activity.None;
				case "PreToolUse":
					switch (tool)
					{
						case "Edit": case "Write": case "MultiEdit": case "NotebookEdit": return Activity.Editing;
						case "Bash": case "PowerShell": return Activity.Running;
						case "Read": case "Grep": case "Glob": case "LS": return Activity.Reading;
						case "WebFetch": case "WebSearch": return Activity.Searching;
						case "Task": case "Agent": return Activity.Delegating;
						default: return Activity.Working;
					}
				default: return Activity.None;
			}
		}

		/// <summary>"PreToolUse/edit" (the hooks name the kind of tool in the address, see ClaudeHooks) or "Stop".</summary>
		public static Activity ClassifyPath(string path, string json)
		{
			int slash = path.IndexOf('/');
			string ev = slash < 0 ? path : path.Substring(0, slash), kind = slash < 0 ? "" : path.Substring(slash + 1);
			switch (kind)
			{
				case "edit": return Activity.Editing;
				case "run": return Activity.Running;
				case "read": return Activity.Reading;
				case "web": return Activity.Searching;
				case "agent": return Activity.Delegating;
			}
			if (ev == "SessionStart") return Activity.Hello;
			return Classify(ev, json);
		}

		static void OnHook(string hookPath, string json)
		{
			if (DevListen) DevNote("hook " + hookPath);
			var a = ClassifyPath(hookPath, json);
			if (a == Activity.None) return;
			Post(() => React(a));
		}

		static readonly string[] AnimThinking = { "think", "review", "idea", "crystal ball", "read", "look around" };
		static readonly string[] AnimEditing = { "computer", "laptop", "code", "write", "typing", "paint" };
		static readonly string[] AnimRunning = { "compile", "loading", "run", "running", "download" };
		static readonly string[] AnimReading = { "read", "spellbook", "scroll", "think" };
		static readonly string[] AnimSearching = { "detective", "telescope", "sniff", "look around" };
		static readonly string[] AnimHello = { "wave", "hello", "happy" };
		static readonly string[] AnimWaiting = { "wave", "hello", "look around" };
		static readonly string[] AnimDone = { "cheer", "thumbs up", "happy", "jump", "dance", "party", "confetti", "flag" };
		static readonly string[] AnimError = { "error", "cry", "shocked", "scared", "dizzy", "angry" };

		/// <summary>Make the pet show an activity (also used by the "Test" button).</summary>
		public static void React(Activity a)
		{
			Last = a;
			// tool calls come many per second: change the animation only when the kind of work changes (or every 8 s)
			bool important = a == Activity.Done || a == Activity.Waiting || a == Activity.Error;
			if (!important && a == shown && (DateTime.Now - shownAt).TotalSeconds < 8) return;
			bool changed = a != shown;
			shown = a; shownAt = DateTime.Now;

			string[] anims;
			switch (a)
			{
				case Activity.Hello: anims = AnimHello; break;
				case Activity.Thinking: anims = AnimThinking; break;
				case Activity.Editing: anims = AnimEditing; break;
				case Activity.Running: anims = AnimRunning; break;
				case Activity.Reading: anims = AnimReading; break;
				case Activity.Searching: anims = AnimSearching; break;
				case Activity.Waiting: anims = AnimWaiting; break;
				case Activity.Done: anims = AnimDone; break;
				case Activity.Error: anims = AnimError; break;
				default: anims = AnimThinking; break;
			}
			Program.Mainthread?.ReactAll(anims);
			if (a == Activity.Done) AgentLevel.TaskDone();

			string key = "agent." + a.ToString().ToLowerInvariant();
			if (a == Activity.Done) SpeechBubble.Notify(Lang.T(key), 8);
			else if (a == Activity.Waiting) SpeechBubble.Notify(Lang.T(key), 15);
			else if (ProgressBubbles && changed && !SpeechBubble.QuietNow) SpeechBubble.Say(Lang.T(key), a == Activity.Error ? 6 : 4);
		}

		/// <summary>"/say": a short line from a local tool (one line, at most 140 characters, no paths or links).</summary>
		static void Say(string text)
		{
			if (!IsSafeToSay(text)) return;
			text = text.Replace("\r", " ").Replace("\n", " ").Trim();
			if (text.Length > 140) text = text.Substring(0, 137) + "…";
			SpeechBubble.Notify(text, 8);
		}

		/// <summary>Never paths, links, code or secrets in the bubble: what someone looking at the screen could read.</summary>
		public static bool IsSafeToSay(string text)
		{
			text = (text ?? "").Trim();
			if (text.Length == 0) return false;
			if (text.Contains(":\\") || text.Contains("://") || text.Contains("/home/") || text.Contains("/Users/") || text.Contains("\\\\")) return false;
			if (text.Contains("```") || text.Contains("{") && text.Contains("}") && text.Contains(";")) return false;
			// long runs of letters/digits look like keys or tokens
			if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[A-Za-z0-9_\-]{32,}")) return false;
			return true;
		}

		/// <summary>Developer ("--listen"): listen without changing the saved setting, and note every request in %TEMP%desktoppet-agent.log.</summary>
		public static bool DevListen;

		static void DevNote(string text)
		{
			try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "desktoppet-agent.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine); } catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		/// <summary>Reactions a local tool (the MCP server) may ask for.</summary>
		public static readonly string[] ReactionNames = { "thinking", "working", "editing", "running", "reading", "searching", "waiting", "success", "error", "celebrating", "waving" };

		public static Activity ReactionByName(string name)
		{
			switch ((name ?? "").Trim().ToLowerInvariant())
			{
				case "thinking": return Activity.Thinking;
				case "working": return Activity.Working;
				case "editing": return Activity.Editing;
				case "running": return Activity.Running;
				case "reading": return Activity.Reading;
				case "searching": return Activity.Searching;
				case "waiting": return Activity.Waiting;
				case "success": case "celebrating": return Activity.Done;
				case "error": return Activity.Error;
				case "waving": return Activity.Hello;
				default: return Activity.None;
			}
		}

		/// <summary>Where the MCP server (DesktopPet.exe --mcp, another process) finds the port and the secret.</summary>
		public static string ConnectionFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "agent.json");

		static void WriteConnectionFile()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ConnectionFile));
				File.WriteAllText(ConnectionFile, JsonSerializer.Serialize(new { port = Port, token = Token }));
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		/// <summary>Send events to ourselves through the real endpoint (the "Test" button and the self-test).</summary>
		public static async Task<int> SendTest(string hookEvent, string json, string token = null)
		{
			using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) })
			{
				var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"http://127.0.0.1:{Port}/hook/{hookEvent}")
				{
					Content = new System.Net.Http.StringContent(json ?? "{}", Encoding.UTF8, "application/json"),
				};
				req.Headers.Add("X-DesktopPet", token ?? Token);
				var resp = await http.SendAsync(req);
				return (int)resp.StatusCode;
			}
		}
	}
}
