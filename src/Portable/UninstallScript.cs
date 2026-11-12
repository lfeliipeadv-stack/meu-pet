using System;
using System.Collections.Generic;
using System.Text;

namespace DesktopPet
{
	/// <summary>
	/// The uninstall.cmd written by both installers (the program's own and DesktopPet-Instalador.exe; this file is shared).
	/// The program lives in %LocalAppData%\DesktopPet, the same folder as the user's pets, reminders, mood and logs,
	/// so the script asks before deleting them (30 seconds without an answer, or a quiet uninstall: they are kept).
	/// Kept: only the program files go (exe, dll, lang). Deleted: the whole folder and the settings (%LocalAppData%\eSheep64).
	/// </summary>
	internal static class UninstallScript
	{
		public static string Build(string appName, IEnumerable<string> shortcuts, string uninstallKey, bool pt)
		{
			string ask = pt
				? "Apagar tambem seus pets, lembretes, humor, atalhos e configuracoes? [S]im / [N]ao (em 30 s: Nao) "
				: "Also delete your pets, reminders, mood, shortcuts and settings? [Y]es / [N]o (in 30 s: No) ";
			string bye = pt ? "O desktopPet foi desinstalado." : "desktopPet was uninstalled.";
			string kept = pt ? "Seus pets e configuracoes ficaram guardados: instale de novo e eles voltam." : "Your pets and settings were kept: install again and they come back.";
			var s = new StringBuilder();
			s.AppendLine("@echo off");
			s.AppendLine("setlocal");
			s.AppendLine("set \"APPDIR=%~dp0\"");
			s.AppendLine("set \"APPDIR=%APPDIR:~0,-1%\"");
			s.AppendLine("cd /d \"%APPDIR%\\..\"");
			s.AppendLine($"taskkill /IM {appName}.exe /f >nul 2>&1");
			foreach (string sc in shortcuts) s.AppendLine($"del \"{sc}\" >nul 2>&1");
			s.AppendLine($"reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v {appName} /f >nul 2>&1");
			s.AppendLine($"reg delete \"HKCU\\{uninstallKey}\" /f >nul 2>&1");
			s.AppendLine("echo.");
			// S/Y = yes, N = no; default No after 30 seconds
			s.AppendLine($"choice /C SYN /N /T 30 /D N /M \"{ask}\"");
			s.AppendLine("if errorlevel 3 goto keep");
			s.AppendLine("rd /s /q \"%LOCALAPPDATA%\\eSheep64\" >nul 2>&1");
			s.AppendLine($"echo {bye}");
			s.AppendLine("timeout /t 2 >nul");
			// (goto) leaves the script before its own folder goes away
			s.AppendLine("(goto) 2>nul & rd /s /q \"%APPDIR%\"");
			s.AppendLine(":keep");
			s.AppendLine("del /q \"%APPDIR%\\*.exe\" \"%APPDIR%\\*.dll\" \"%APPDIR%\\*.pdb\" \"%APPDIR%\\*.runtimeconfig.json\" \"%APPDIR%\\*.deps.json\" >nul 2>&1");
			s.AppendLine("rd /s /q \"%APPDIR%\\lang\" >nul 2>&1");
			s.AppendLine($"echo {bye}");
			s.AppendLine($"echo {kept}");
			s.AppendLine("timeout /t 4 >nul");
			s.AppendLine("(goto) 2>nul & del \"%~f0\"");
			return s.ToString();
		}
	}
}
