using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// CPU, memory and disk in a small plate next to the pet (Settings → Extras or the pet menu).
	/// When the CPU stays above 90% for a minute, the pet comments on it (at most every 30 minutes).
	/// </summary>
	public static class PcMonitor
	{
		[StructLayout(LayoutKind.Sequential)] struct FILETIME { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }
		[DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		class MEMORYSTATUSEX { public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); public uint dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);

		static ulong lastIdle, lastTotal;
		static Timer timer;
		static Hud hud;
		static int busySeconds;
		static DateTime lastComment = DateTime.MinValue;

		public static int Cpu { get; private set; }
		public static int Ram { get; private set; }
		public static string Disk { get; private set; } = "";

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("PcMonitor", false);
			set { Program.MyData.SetSetting("PcMonitor", value); Apply(); }
		}

		public static void Apply()
		{
			if (Enabled)
			{
				if (timer == null) { timer = new Timer { Interval = 2000 }; timer.Tick += (s, e) => Tick(); }
				timer.Start();
				Tick();
			}
			else
			{
				timer?.Stop();
				hud?.Close();
				hud = null;
			}
		}

		static void Tick()
		{
			try
			{
				if (GetSystemTimes(out var idle, out var kernel, out var user))
				{
					ulong total = kernel.Value + user.Value;
					if (lastTotal > 0 && total > lastTotal)
						Cpu = (int)Math.Round(100.0 * (1 - (idle.Value - lastIdle) / (double)(total - lastTotal)));
					lastIdle = idle.Value; lastTotal = total;
				}
				var mem = new MEMORYSTATUSEX();
				if (GlobalMemoryStatusEx(mem)) Ram = (int)mem.dwMemoryLoad;
				var sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
				Disk = sys.IsReady ? Lang.T("mon.diskFree", sys.Name.TrimEnd('\\'), sys.AvailableFreeSpace / (1024.0 * 1024 * 1024)) : "";
			}
			catch (Exception) { }

			// the pet notices a busy computer
			busySeconds = Cpu >= 90 ? busySeconds + 2 : 0;
			if (busySeconds >= 60 && (DateTime.Now - lastComment).TotalMinutes > 30)
			{
				lastComment = DateTime.Now;
				busySeconds = 0;
				Program.Mainthread?.ReactAll("tired", "dizzy", "hot", "sweat");
				SpeechBubble.Say(Lang.T("mon.busy", Cpu), 8);
			}

			var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
			if (pet == null || HideHotkey.Hidden) { hud?.Hide(); return; }
			if (hud == null || hud.IsDisposed) hud = new Hud();
			hud.Follow(pet);
			hud.Invalidate();
			if (!hud.Visible) hud.Show();
		}

		/// <summary>The little plate beside the pet.</summary>
		sealed class Hud : Form
		{
			readonly Font font = Theme.CreateFont(11.5f), bold = Theme.CreateFont(11.5f, FontStyle.Bold);
			FormPet pet;
			readonly Timer follow = new Timer { Interval = 40 };

			public Hud()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
				Size = new Size(150, 70);
				Theme.ApplyWindowChrome(this);
				follow.Tick += (s, e) => { if (pet != null && !pet.IsDisposed && pet.Visible) Place(); };
				follow.Start();
				FormClosed += (s, e) => { follow.Stop(); follow.Dispose(); };
			}

			protected override bool ShowWithoutActivation => true;

			protected override CreateParams CreateParams
			{
				get
				{
					var cp = base.CreateParams;
					cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00000020 | 0x00000008;   // tool window, no activate, click-through, topmost
					return cp;
				}
			}

			public void Follow(FormPet p) { pet = p; Place(); }

			void Place()
			{
				var wa = Screen.FromControl(pet).WorkingArea;
				int x = pet.Left - Width - 4, y = pet.Bottom - Height - 6;
				if (x < wa.Left) x = pet.Right + 4;
				x = Math.Max(wa.Left, Math.Min(wa.Right - Width, x));
				y = Math.Max(wa.Top, Math.Min(wa.Bottom - Height, y));
				if (Left != x || Top != y) Location = new Point(x, y);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Surface);
				void Row(int y, string label, int pct)
				{
					Theme.DrawText(g, label, bold, new Rectangle(10, y, 40, 16), Theme.Text, TextFormatFlags.SingleLine);
					var track = new Rectangle(48, y + 5, 64, 6);
					Theme.FillRounded(g, Theme.Hover, track, 3);
					var c = pct >= 90 ? Theme.Danger : pct >= 70 ? Color.FromArgb(214, 150, 20) : Theme.Accent;
					Theme.FillRounded(g, c, new Rectangle(track.X, track.Y, Math.Max(6, track.Width * Math.Min(100, pct) / 100), track.Height), 3);
					Theme.DrawText(g, pct + "%", font, new Rectangle(116, y, 32, 16), Theme.TextMuted, TextFormatFlags.SingleLine);
				}
				Row(8, "CPU", Cpu);
				Row(27, "RAM", Ram);
				Theme.DrawText(g, Disk, font, new Rectangle(10, 47, Width - 14, 16), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}

			protected override void Dispose(bool disposing) { if (disposing) { font.Dispose(); bold.Dispose(); } base.Dispose(disposing); }
		}
	}
}
