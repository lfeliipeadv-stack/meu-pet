using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DesktopPet
{
	/// <summary>
	/// Warnings and errors (plus the start line) saved to %LocalAppData%\DesktopPet\logs\desktoppet-YYYY-MM-DD.log,
	/// so a problem on someone else's computer can be sent and read. Kept 7 days, at most 2 MB a day.
	/// The many "new animation" info lines stay only in the debug window.
	/// </summary>
	public static class LogFile
	{
		public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "logs");
		static readonly object gate = new object();
		static bool cleaned;

		public static string Today => Path.Combine(Folder, "desktoppet-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

		public static void Write(StartUp.DEBUG_TYPE type, string text)
		{
			if (type == StartUp.DEBUG_TYPE.info) return;
			Append(type == StartUp.DEBUG_TYPE.error ? "ERROR" : "WARN ", text);
		}

		/// <summary>Always written (start, version, crashes).</summary>
		public static void Important(string text) => Append("INFO ", text);

		static void Append(string level, string text)
		{
			try
			{
				lock (gate)
				{
					Directory.CreateDirectory(Folder);
					if (!cleaned) { cleaned = true; Clean(); }
					var file = Today;
					if (File.Exists(file) && new FileInfo(file).Length > 2 * 1024 * 1024) return;
					File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {level} {(text ?? "").Replace("\r", " ").Replace("\n", " | ")}{Environment.NewLine}", new UTF8Encoding(false));
				}
			}
			catch (Exception) { }   // logging must never break the program
		}

		static void Clean()
		{
			foreach (var f in Directory.GetFiles(Folder, "desktoppet-*.log").Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7)))
				try { File.Delete(f); } catch (Exception) { }
		}
	}
}
