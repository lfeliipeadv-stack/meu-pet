using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopPet
{
	/// <summary>
	/// Adds (or removes) the desktopPet hooks in the Claude Code user settings (~/.claude/settings.json).
	/// Each hook only runs <c>curl.exe</c> to 127.0.0.1 with the secret token; the kind of tool is in the address
	/// (one matcher per kind), so the hook never reads what Claude is doing. "; exit 0" keeps Claude Code quiet
	/// when the pet is not running. Everything else in the file is kept, and a copy is saved before writing.
	/// </summary>
	public static class ClaudeHooks
	{
		const string Marker = "X-DesktopPet:";

		public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

		public static string BackupPath(string path) => path + ".desktoppet-backup";

		static string Command(string address) =>
			$"curl.exe -s -m 2 -X POST -d x -H \"{Marker} {AgentBridge.Token}\" http://127.0.0.1:{AgentBridge.Port}/hook/{address}; exit 0";

		/// <summary>(event, matcher or null, address)</summary>
		static readonly (string ev, string matcher, string address)[] Hooks =
		{
			("SessionStart", null, "SessionStart"),
			("UserPromptSubmit", null, "UserPromptSubmit"),
			("PreToolUse", "Edit|Write|MultiEdit|NotebookEdit", "PreToolUse/edit"),
			("PreToolUse", "Bash|PowerShell", "PreToolUse/run"),
			("PreToolUse", "Read|Grep|Glob", "PreToolUse/read"),
			("PreToolUse", "WebFetch|WebSearch", "PreToolUse/web"),
			("PreToolUse", "Task|Agent", "PreToolUse/agent"),
			("PostToolUseFailure", null, "PostToolUseFailure"),
			("Notification", null, "Notification"),
			("Stop", null, "Stop"),
		};

		static JsonObject Load(string path)
		{
			if (!File.Exists(path)) return new JsonObject();
			string text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
			return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
				?? throw new InvalidDataException("settings.json is not a JSON object");
		}

		static void Write(string path, JsonObject root)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			if (File.Exists(path)) File.Copy(path, BackupPath(path), true);
			string tmp = path + ".tmp";
			File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
			File.Copy(tmp, path, true);
			File.Delete(tmp);
		}

		static bool IsOurs(JsonNode group) =>
			group?["hooks"] is JsonArray hooks && hooks.Any(h => (h?["command"]?.GetValue<string>() ?? "").Contains(Marker));

		/// <summary>Remove our groups from every event; drop events and "hooks" left empty. Returns true if something was removed.</summary>
		static bool Strip(JsonObject root)
		{
			if (!(root["hooks"] is JsonObject events)) return false;
			bool removed = false;
			foreach (var name in events.Select(e => e.Key).ToList())
			{
				if (!(events[name] is JsonArray groups)) continue;
				foreach (var g in groups.Where(IsOurs).ToList()) { groups.Remove(g); removed = true; }
				if (groups.Count == 0) events.Remove(name);
			}
			if (events.Count == 0) root.Remove("hooks");
			return removed;
		}

		public static bool IsInstalled(string path = null)
		{
			try
			{
				var root = Load(path ?? DefaultPath);
				return root["hooks"] is JsonObject events && events.Any(e => e.Value is JsonArray a && a.Any(IsOurs));
			}
			catch (Exception) { return false; }
		}

		/// <summary>True if the hooks there still carry the current secret and port.</summary>
		public static bool IsCurrent(string path = null)
		{
			try
			{
				var root = Load(path ?? DefaultPath);
				if (!(root["hooks"] is JsonObject events)) return false;
				var ours = events.SelectMany(e => e.Value as JsonArray ?? new JsonArray()).Where(IsOurs)
					.SelectMany(g => (JsonArray)g["hooks"]).Select(h => h?["command"]?.GetValue<string>() ?? "").ToList();
				return ours.Count == Hooks.Length && ours.All(c => c.Contains($"{Marker} {AgentBridge.Token}") && c.Contains($"127.0.0.1:{AgentBridge.Port}/hook/"));
			}
			catch (Exception) { return false; }
		}

		public static void Install(string path = null)
		{
			path = path ?? DefaultPath;
			var root = Load(path);
			Strip(root);   // never twice
			if (!(root["hooks"] is JsonObject events)) root["hooks"] = events = new JsonObject();
			foreach (var h in Hooks)
			{
				if (!(events[h.ev] is JsonArray groups)) events[h.ev] = groups = new JsonArray();
				var group = new JsonObject();
				if (h.matcher != null) group["matcher"] = h.matcher;
				group["hooks"] = new JsonArray(new JsonObject
				{
					["type"] = "command",
					["command"] = Command(h.address),
					["timeout"] = 5,
				});
				groups.Add(group);
			}
			Write(path, root);
		}

		public static void Uninstall(string path = null)
		{
			path = path ?? DefaultPath;
			if (!File.Exists(path)) return;
			var root = Load(path);
			if (Strip(root)) Write(path, root);
		}
	}
}
