using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Registers "DesktopPet.exe --mcp" as the user-scope MCP server "desktoppet" of Claude Code, with Claude Code's own
	/// command ("claude mcp add/remove"), so its configuration file is never edited by hand.
	/// </summary>
	public static class ClaudeMcp
	{
		public const string Name = "desktoppet";

		/// <summary>The Claude Code command line: on the PATH, or the copy that comes with the Claude app.</summary>
		public static string FindClaude()
		{
			foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
			{
				try
				{
					foreach (var exe in new[] { "claude.exe", "claude.cmd" })
					{
						string p = Path.Combine(dir.Trim(), exe);
						if (dir.Trim().Length > 0 && File.Exists(p)) return p;
					}
				}
				catch (Exception skipped) { LogFile.Ignored(skipped); }
			}
			try
			{
				string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code");
				if (Directory.Exists(root))
					return Directory.GetFiles(root, "claude.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
			return null;
		}

		/// <summary>The command a person can paste in a terminal when Claude Code is not found.</summary>
		public static string ManualCommand => $"claude mcp add --scope user {Name} -- \"{Application.ExecutablePath}\" --mcp";

		/// <summary>True if the user configuration of Claude Code lists our server (read only).</summary>
		public static bool IsRegistered()
		{
			try
			{
				string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
				return File.Exists(file) && File.ReadAllText(file).Contains("\"" + Name + "\"");
			}
			catch (Exception) { return false; }
		}

		public static Task<(bool ok, string output)> Register() =>
			Run($"mcp add --scope user {Name} -- \"{Application.ExecutablePath}\" --mcp");

		public static Task<(bool ok, string output)> Unregister() => Run($"mcp remove --scope user {Name}");

		static async Task<(bool ok, string output)> Run(string args)
		{
			string claude = FindClaude();
			if (claude == null) return (false, null);
			var psi = new ProcessStartInfo(claude, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
			using (var p = Process.Start(psi))
			{
				var o = p.StandardOutput.ReadToEndAsync();
				var e = p.StandardError.ReadToEndAsync();
				await Task.Run(() => p.WaitForExit(30000));
				string text = ((await o) + " " + (await e)).Trim();
				return (p.ExitCode == 0, text);
			}
		}
	}
}
