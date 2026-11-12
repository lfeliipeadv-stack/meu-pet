using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopPetSetup
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.2 / 1.3
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			// developer: "--snapshot=file.png [--pretend-missing]" saves the window as a picture and closes
			string shot = args.FirstOrDefault(a => a.StartsWith("--snapshot="))?.Substring(11);
			SetupForm.PretendMissing = args.Contains("--pretend-missing");
			var form = new SetupForm();
			if (shot != null)
			{
				form.StartPosition = FormStartPosition.Manual;
				form.Location = new Point(-5000, -5000);
				form.Shown += (s, e) =>
				{
					var t = new Timer { Interval = 500 };
					t.Tick += (s2, e2) =>
					{
						t.Stop();
						using (var bmp = new Bitmap(form.Width, form.Height))
						{
							form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
							bmp.Save(shot);
						}
						form.Close();
					};
					t.Start();
				};
			}
			Application.Run(form);
		}
	}

	/// <summary>
	/// One page: is the .NET 10 Desktop Runtime here? (if not: "Install .NET 10" runs Microsoft's official installer,
	/// and "Install" stays disabled until it is found), the shortcuts to create, and Install.
	/// The program goes to %LocalAppData%\DesktopPet like the program's own installer, with the same shortcuts and
	/// uninstall entry, so "Repair" and "Uninstall" of the program keep working.
	/// </summary>
	sealed class SetupForm : Form
	{
		const string AppName = "DesktopPet";
		const string RuntimeUrl = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";
		const string Webpage = "https://github.com/lfeliipeadv-stack/meu-pet";

		static readonly bool Pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
		static string T(string pt, string en) => Pt ? pt : en;

		static readonly Color Accent = Color.FromArgb(31, 122, 77), Ok = Color.FromArgb(31, 122, 77), Bad = Color.FromArgb(196, 43, 28), Muted = Color.FromArgb(96, 96, 96);

		readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(410, 0) };
		readonly Label statusIcon = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
		readonly Button getRuntime = new Button();
		readonly ProgressBar progress = new ProgressBar { Visible = false };
		readonly CheckBox startMenu = new CheckBox { Checked = true, AutoSize = true };
		readonly CheckBox desktop = new CheckBox { Checked = true, AutoSize = true };
		readonly CheckBox autostart = new CheckBox { Checked = false, AutoSize = true };
		readonly Button install = new Button();
		readonly Button cancel = new Button();
		readonly Timer recheck = new Timer { Interval = 3000 };
		bool runtimeOk, busy;
		public static bool PretendMissing;

		static string InstallPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

		public SetupForm()
		{
			Text = T("Instalar o desktopPet", "Install desktopPet");
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false; MinimizeBox = false;
			StartPosition = FormStartPosition.CenterScreen;
			BackColor = Color.White;
			Font = new Font("Segoe UI", 9.5f);
			AutoScaleMode = AutoScaleMode.Dpi;
			ClientSize = new Size(520, 408);

			var logo = new PictureBox { Location = new Point(24, 22), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom };
			using (var s = typeof(SetupForm).Assembly.GetManifestResourceStream("logo.png")) if (s != null) logo.Image = new Bitmap(Image.FromStream(s));
			var title = new Label { Text = "desktopPet", Font = new Font("Segoe UI", 20f, FontStyle.Bold), Location = new Point(100, 22), AutoSize = true };
			var subtitle = new Label { Text = T("Um bichinho para a sua área de trabalho", "A little pet for your desktop"), ForeColor = Muted, Location = new Point(102, 62), AutoSize = true };

			var box = new Panel { Location = new Point(24, 108), Size = new Size(472, 132), BackColor = Color.FromArgb(246, 247, 248) };
			var stepTitle = new Label { Text = T("1. .NET 10 Desktop Runtime", "1. .NET 10 Desktop Runtime"), Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(14, 10), AutoSize = true };
			statusIcon.Location = new Point(12, 38); statusIcon.Size = new Size(26, 26); statusIcon.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
			status.Location = new Point(42, 41);
			getRuntime.Text = T("Instalar o .NET 10", "Install .NET 10");
			getRuntime.Location = new Point(42, 90); getRuntime.Size = new Size(170, 30);
			progress.Location = new Point(222, 96); progress.Size = new Size(236, 18);
			box.Controls.AddRange(new Control[] { stepTitle, statusIcon, status, getRuntime, progress });

			var optionsTitle = new Label { Text = T("2. Atalhos", "2. Shortcuts"), Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(24, 254), AutoSize = true };
			startMenu.Text = T("Menu Iniciar", "Start menu"); startMenu.Location = new Point(28, 282);
			desktop.Text = T("Área de trabalho", "Desktop"); desktop.Location = new Point(180, 282);
			autostart.Text = T("Iniciar com o Windows", "Start with Windows"); autostart.Location = new Point(330, 282);

			var line = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(0, 340), Size = new Size(520, 2) };
			install.Text = T("Instalar", "Install"); install.Location = new Point(286, 358); install.Size = new Size(110, 32);
			install.BackColor = Accent; install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat; install.FlatAppearance.BorderSize = 0;
			cancel.Text = T("Cancelar", "Cancel"); cancel.Location = new Point(404, 358); cancel.Size = new Size(92, 32);
			var hint = new Label { Text = T("Instala só para você, sem precisar de administrador.", "Installs for you only, no administrator needed."), ForeColor = Muted, Location = new Point(24, 360), AutoSize = true, MaximumSize = new Size(250, 0) };

			Controls.AddRange(new Control[] { logo, title, subtitle, box, optionsTitle, startMenu, desktop, autostart, line, hint, install, cancel });
			AcceptButton = install; CancelButton = cancel;

			getRuntime.Click += async (s, e) => await GetRuntime();
			install.Click += (s, e) => Install();
			cancel.Click += (s, e) => Close();
			recheck.Tick += (s, e) => { if (!busy) CheckRuntime(); };
			Shown += (s, e) => { CheckRuntime(); recheck.Start(); };
		}

		// ------------------------------------------------------------------ .NET 10

		/// <summary>The .NET 10 Desktop Runtime (Microsoft.WindowsDesktop.App 10.x, 64 bits) is installed.</summary>
		public static bool HasRuntime()
		{
			try
			{
				foreach (var root in new[] { Environment.GetEnvironmentVariable("DOTNET_ROOT"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"), @"C:\Program Files\dotnet" })
				{
					if (string.IsNullOrEmpty(root)) continue;
					string dir = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
					if (Directory.Exists(dir) && Directory.GetDirectories(dir).Any(d => Path.GetFileName(d).StartsWith("10."))) return true;
				}
				using (var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
				using (var key = hk.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"))
					if (key != null && key.GetValueNames().Any(v => v.StartsWith("10."))) return true;
			}
			catch (Exception) { }
			return false;
		}

		void CheckRuntime()
		{
			runtimeOk = HasRuntime() && !PretendMissing;
			statusIcon.Text = runtimeOk ? "✓" : "✕";
			statusIcon.ForeColor = runtimeOk ? Ok : Bad;
			status.Text = runtimeOk
				? T("O .NET 10 já está instalado neste computador. Tudo pronto!", "The .NET 10 is already installed on this computer. All set!")
				: T("O .NET 10 Desktop Runtime não está instalado. Ele é necessário para o desktopPet funcionar.", "The .NET 10 Desktop Runtime is not installed. desktopPet needs it.");
			getRuntime.Visible = !runtimeOk;
			install.Enabled = runtimeOk && !busy;
			install.BackColor = install.Enabled ? Accent : Color.FromArgb(200, 200, 200);
			if (runtimeOk) recheck.Stop();
		}

		/// <summary>Download Microsoft's official .NET 10 Desktop Runtime installer and run it (it asks for permission itself).</summary>
		async Task GetRuntime()
		{
			busy = true;
			getRuntime.Enabled = false; install.Enabled = false;
			progress.Visible = true; progress.Value = 0;
			status.Text = T("Baixando o instalador oficial da Microsoft…", "Downloading Microsoft's official installer…");
			string file = Path.Combine(Path.GetTempPath(), "windowsdesktop-runtime-10-win-x64.exe");
			try
			{
				using (var web = new WebClient())
				{
					web.Headers.Add("User-Agent", "DesktopPet-Setup");
					web.DownloadProgressChanged += (s, e) => progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
					await web.DownloadFileTaskAsync(new Uri(RuntimeUrl), file);
				}
				progress.Visible = false;
				status.Text = T("Siga o instalador da Microsoft. Quando ele terminar, o botão Instalar será liberado.", "Follow Microsoft's installer. When it finishes, the Install button is enabled.");
				using (var p = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }))
					await Task.Run(() => p?.WaitForExit());
			}
			catch (Exception ex)
			{
				progress.Visible = false;
				MessageBox.Show(this, T("Não foi possível baixar o .NET 10 automaticamente:\n", "Could not download .NET 10 automatically:\n") + ex.Message
					+ T("\n\nA página de download da Microsoft será aberta.", "\n\nMicrosoft's download page will open."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/10.0") { UseShellExecute = true });
			}
			finally
			{
				try { if (File.Exists(file)) File.Delete(file); } catch (Exception) { }
				busy = false;
				getRuntime.Enabled = true;
				CheckRuntime();
				if (!runtimeOk) recheck.Start();
			}
		}

		// ------------------------------------------------------------------ install

		void Install()
		{
			if (!HasRuntime()) { CheckRuntime(); return; }
			try
			{
				busy = true; install.Enabled = false;
				foreach (var p in Process.GetProcessesByName(AppName)) { try { p.Kill(); p.WaitForExit(3000); } catch (Exception) { } }
				Directory.CreateDirectory(InstallPath);
				string exe = Path.Combine(InstallPath, AppName + ".exe");
				using (var s = typeof(SetupForm).Assembly.GetManifestResourceStream("DesktopPet.exe"))
				using (var f = File.Create(exe)) s.CopyTo(f);

				string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
				string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
				string autostartPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
				// the same kind of shortcut as the program's own installer (it finds and removes them)
				string shortcut = "[InternetShortcut]\r\nURL=file:///" + exe.Replace('\\', '/') + "\r\nIconFile=" + exe + "\r\nIconIndex=0\r\n";
				void Shortcut(bool on, string folder) { string f = Path.Combine(folder, AppName + ".url"); if (on) File.WriteAllText(f, shortcut); else if (File.Exists(f)) File.Delete(f); }
				Shortcut(startMenu.Checked, startMenuPath);
				Shortcut(desktop.Checked, desktopPath);
				Shortcut(autostart.Checked, autostartPath);

				// uninstall: the same script and entry as the program's own installer ("Apps" in Windows Settings)
				string keyName = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;
				string script = DesktopPet.UninstallScript.Build(AppName, new[] { Path.Combine(startMenuPath, AppName + ".url"), Path.Combine(desktopPath, AppName + ".url"), Path.Combine(autostartPath, AppName + ".url") }, keyName, Pt);
				string scriptPath = Path.Combine(InstallPath, "uninstall.cmd");
				File.WriteAllText(scriptPath, script);
				string version = FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "";
				using (var key = Registry.CurrentUser.CreateSubKey(keyName))
				{
					key.SetValue("DisplayIcon", exe);
					key.SetValue("DisplayName", AppName);
					key.SetValue("DisplayVersion", version);
					key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
					key.SetValue("InstallLocation", InstallPath);
					key.SetValue("Publisher", "desktopPet");
					key.SetValue("QuietUninstallString", scriptPath);
					key.SetValue("UninstallString", scriptPath);
					key.SetValue("URLInfoAbout", Webpage);
					key.SetValue("URLUpdateInfo", Webpage);
					key.SetValue("EstimatedSize", 2500, RegistryValueKind.DWord);
					key.SetValue("NoModify", 1, RegistryValueKind.DWord);
					key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
				}

				Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = InstallPath });
				MessageBox.Show(this, T("Pronto! O desktopPet foi instalado e já está aberto.\nProcure o ícone perto do relógio.", "Done! desktopPet is installed and running.\nLook for its icon near the clock."),
					Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, T("A instalação falhou: ", "The installation failed: ") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				busy = false;
				CheckRuntime();
			}
		}
	}
}
