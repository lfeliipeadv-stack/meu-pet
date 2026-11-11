using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopPet
{
	/// <summary>
	/// Modern look for all application windows: a light and a dark palette that follow the Windows setting,
	/// Windows 11 rounded corners / dark title bar, drawing helpers and a flat renderer for menus.
	/// </summary>
	public static class Theme
	{
		/// <summary>True if Windows apps are set to dark mode.</summary>
		public static bool IsDark { get; private set; }

		// Palette (filled by Refresh)
		public static Color Background { get; private set; }
		public static Color Surface { get; private set; }
		public static Color SurfaceAlt { get; private set; }
		public static Color Hover { get; private set; }
		public static Color Border { get; private set; }
		public static Color Text { get; private set; }
		public static Color TextMuted { get; private set; }
		public static Color Accent { get; private set; }
		public static Color AccentText { get; private set; }
		public static Color AccentSoft { get; private set; }
		public static Color Link { get; private set; }
		public static Color Danger { get; private set; }
		public static Color Success { get; private set; }

		/// <summary>Font family used by all windows.</summary>
		public static string FontFamilyName { get; } = PickFont("Segoe UI Variable Text", "Segoe UI");

		static Theme()
		{
			Refresh();
			SystemEvents.UserPreferenceChanged += (s, e) =>
			{
				if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color) Refresh();
			};
		}

		/// <summary>Read the Windows light/dark setting and rebuild the palette.</summary>
		public static void Refresh()
		{
			IsDark = ReadAppsUseDarkTheme();
			// Friendly green accent (the eSheep meadow). Stays readable in light and dark.
			if (IsDark)
			{
				Background = Color.FromArgb(32, 32, 34);
				Surface = Color.FromArgb(43, 43, 46);
				SurfaceAlt = Color.FromArgb(52, 52, 56);
				Hover = Color.FromArgb(62, 62, 67);
				Border = Color.FromArgb(70, 70, 76);
				Text = Color.FromArgb(240, 240, 242);
				TextMuted = Color.FromArgb(170, 170, 178);
				Accent = Color.FromArgb(76, 194, 120);
				AccentText = Color.FromArgb(16, 32, 22);
				AccentSoft = Color.FromArgb(40, 76, 54);
				Link = Color.FromArgb(110, 190, 255);
				Danger = Color.FromArgb(232, 17, 35);
				Success = Color.FromArgb(76, 194, 120);
			}
			else
			{
				Background = Color.FromArgb(243, 244, 246);
				Surface = Color.White;
				SurfaceAlt = Color.FromArgb(248, 249, 250);
				Hover = Color.FromArgb(234, 236, 239);
				Border = Color.FromArgb(222, 225, 230);
				Text = Color.FromArgb(28, 30, 33);
				TextMuted = Color.FromArgb(104, 110, 118);
				Accent = Color.FromArgb(32, 150, 82);
				AccentText = Color.White;
				AccentSoft = Color.FromArgb(220, 243, 228);
				Link = Color.FromArgb(0, 102, 204);
				Danger = Color.FromArgb(196, 43, 28);
				Success = Color.FromArgb(32, 150, 82);
			}
		}

		private static bool ReadAppsUseDarkTheme()
		{
			// Optional override for testing / preference: DESKTOPPET_THEME=dark|light
			var forced = Environment.GetEnvironmentVariable("DESKTOPPET_THEME");
			if (string.Equals(forced, "dark", StringComparison.OrdinalIgnoreCase)) return true;
			if (string.Equals(forced, "light", StringComparison.OrdinalIgnoreCase)) return false;
			try
			{
				using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
				{
					var v = key?.GetValue("AppsUseLightTheme");
					if (v is int i) return i == 0;
				}
			}
			catch (Exception) { }
			return false;
		}

		private static string PickFont(params string[] names)
		{
			using (var fonts = new InstalledFontCollection())
			{
				foreach (var n in names)
					foreach (var f in fonts.Families)
						if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
			}
			return SystemFonts.MessageBoxFont.FontFamily.Name;
		}

		/// <summary>Create a UI font with a size in pixels (DPI independent in our custom painting).</summary>
		public static Font CreateFont(float pixelSize, FontStyle style = FontStyle.Regular)
		{
			return new Font(FontFamilyName, pixelSize, style, GraphicsUnit.Pixel);
		}

		// ------------------------------------------------------------------ Windows 11 window chrome

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
		private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
		private const int DWMWA_BORDER_COLOR = 34;
		private const int DWMWCP_ROUND = 2;

		/// <summary>
		/// Apply the dark title bar (if dark mode), rounded corners and a subtle border on Windows 11.
		/// Silently ignored on older Windows versions.
		/// </summary>
		public static void ApplyWindowChrome(Form form)
		{
			void apply()
			{
				try
				{
					int dark = IsDark ? 1 : 0;
					DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
					int corner = DWMWCP_ROUND;
					DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
					int border = ColorTranslator.ToWin32(Border);
					DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
				}
				catch (Exception) { }
			}
			if (form.IsHandleCreated) apply();
			else form.HandleCreated += (s, e) => apply();
		}

		/// <summary>
		/// Theme a standard (designer based) form and all its child controls.
		/// </summary>
		public static void ApplyTo(Form form)
		{
			ApplyWindowChrome(form);
			form.BackColor = Background;
			form.ForeColor = Text;
			form.Font = new Font(FontFamilyName, 9f);
			ApplyToControls(form.Controls);
		}

		private static void ApplyToControls(Control.ControlCollection controls)
		{
			foreach (Control c in controls)
			{
				switch (c)
				{
					case Button b:
						b.FlatStyle = FlatStyle.Flat;
						b.FlatAppearance.BorderColor = Border;
						b.FlatAppearance.MouseOverBackColor = Hover;
						b.FlatAppearance.MouseDownBackColor = Border;
						b.BackColor = Surface;
						b.ForeColor = Text;
						b.Cursor = Cursors.Hand;
						if (b.Height < 30) b.Height = 30;
						break;
					case LinkLabel l:
						l.LinkColor = Link;
						l.ActiveLinkColor = Accent;
						l.VisitedLinkColor = Link;
						l.LinkBehavior = LinkBehavior.HoverUnderline;
						l.BackColor = Color.Transparent;
						break;
					case TextBoxBase t:
						t.BackColor = Surface;
						t.ForeColor = Text;
						t.BorderStyle = BorderStyle.FixedSingle;
						break;
					case CheckBox cb:
						cb.FlatStyle = FlatStyle.Flat;
						cb.ForeColor = Text;
						cb.BackColor = Color.Transparent;
						break;
					case ProgressBar _:
						break;
					case TrackBar tb:
						tb.BackColor = Background;
						break;
					case Label lb:
						lb.ForeColor = Text;
						lb.BackColor = Color.Transparent;
						break;
					case TableLayoutPanel _:
					case FlowLayoutPanel _:
					case Panel _:
					case GroupBox _:
						c.BackColor = Color.Transparent;
						c.ForeColor = Text;
						break;
				}
				if (c.HasChildren) ApplyToControls(c.Controls);
			}
		}

		/// <summary>Make a button the primary (accent) action.</summary>
		public static void MakePrimary(Button b)
		{
			b.FlatStyle = FlatStyle.Flat;
			b.BackColor = Accent;
			b.ForeColor = AccentText;
			b.FlatAppearance.BorderSize = 0;
			b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Accent, 0.15f);
			b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Accent, 0.05f);
		}

		// ------------------------------------------------------------------ drawing helpers

		/// <summary>Prepare a Graphics for smooth, anti-aliased modern drawing.</summary>
		public static void Prepare(Graphics g)
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		}

		/// <summary>Rounded rectangle path.</summary>
		public static GraphicsPath RoundedRect(RectangleF r, float radius)
		{
			var path = new GraphicsPath();
			float d = Math.Max(0.1f, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
			path.AddArc(r.X, r.Y, d, d, 180, 90);
			path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}

		public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
		{
			using (var path = RoundedRect(r, radius))
			using (var b = new SolidBrush(color))
				g.FillPath(b, path);
		}

		public static void DrawRounded(Graphics g, Color color, RectangleF r, float radius, float width = 1f)
		{
			using (var path = RoundedRect(new RectangleF(r.X + width / 2, r.Y + width / 2, r.Width - width, r.Height - width), radius))
			using (var p = new Pen(color, width))
				g.DrawPath(p, path);
		}

		/// <summary>A flat, rounded button. Returns nothing; hit testing is done by the caller with the same rect.</summary>
		public static void DrawButton(Graphics g, Rectangle r, string text, Font font, bool primary, bool hover)
		{
			Color back = primary ? (hover ? ControlPaint.Light(Accent, 0.15f) : Accent) : (hover ? Hover : Surface);
			FillRounded(g, back, r, 6);
			if (!primary) DrawRounded(g, Border, r, 6);
			TextRenderer.DrawText(g, text, font, r, primary ? AccentText : Text,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		/// <summary>An on/off toggle switch (Windows 11 style).</summary>
		public static void DrawToggle(Graphics g, Rectangle r, bool on)
		{
			float radius = r.Height / 2f;
			if (on)
			{
				FillRounded(g, Accent, r, radius);
			}
			else
			{
				FillRounded(g, Surface, r, radius);
				DrawRounded(g, TextMuted, r, radius, 1.5f);
			}
			float knob = r.Height - 8;
			float x = on ? r.Right - knob - 4 : r.X + 4;
			using (var b = new SolidBrush(on ? AccentText : TextMuted))
				g.FillEllipse(b, x, r.Y + 4, knob, knob);
		}

		/// <summary>A horizontal slider. value in [0..max].</summary>
		public static void DrawSlider(Graphics g, Rectangle track, int value, int max)
		{
			float pct = max <= 0 ? 0 : Math.Max(0, Math.Min(1, (float)value / max));
			var bar = new RectangleF(track.X, track.Y + track.Height / 2f - 2, track.Width, 4);
			FillRounded(g, Border, bar, 2);
			FillRounded(g, Accent, new RectangleF(bar.X, bar.Y, bar.Width * pct, bar.Height), 2);
			float cx = track.X + track.Width * pct;
			float cy = track.Y + track.Height / 2f;
			using (var b = new SolidBrush(Surface)) g.FillEllipse(b, cx - 10, cy - 10, 20, 20);
			using (var p = new Pen(Border, 1)) g.DrawEllipse(p, cx - 10, cy - 10, 20, 20);
			using (var b = new SolidBrush(Accent)) g.FillEllipse(b, cx - 6, cy - 6, 12, 12);
		}

		/// <summary>Draw text with GDI (crisp ClearType) inside a rectangle.</summary>
		public static void DrawText(Graphics g, string text, Font font, Rectangle r, Color color, TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak)
		{
			TextRenderer.DrawText(g, text ?? "", font, r, color, flags | TextFormatFlags.NoPrefix);
		}

		public static Size MeasureText(Graphics g, string text, Font font, int maxWidth = int.MaxValue, TextFormatFlags flags = TextFormatFlags.WordBreak)
		{
			return TextRenderer.MeasureText(g, text ?? "", font, new Size(maxWidth, int.MaxValue), flags | TextFormatFlags.NoPrefix);
		}

		/// <summary>Image drawn with nearest neighbour (crisp pixel art pets).</summary>
		public static void DrawPixelImage(Graphics g, Image image, Rectangle dest, System.Drawing.Imaging.ImageAttributes attributes = null)
		{
			var oldI = g.InterpolationMode;
			var oldP = g.PixelOffsetMode;
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			if (attributes == null) g.DrawImage(image, dest);
			else g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
			g.InterpolationMode = oldI;
			g.PixelOffsetMode = oldP;
		}

		// ------------------------------------------------------------------ menus

		/// <summary>A new flat renderer for context menus (tray menu, debug menu).</summary>
		public static ToolStripRenderer MenuRenderer => new ModernMenuRenderer();

		/// <summary>Apply colors and spacing to a context menu.</summary>
		public static void ApplyTo(ToolStrip menu)
		{
			menu.Renderer = MenuRenderer;
			menu.BackColor = Surface;
			menu.ForeColor = Text;
			menu.Font = new Font(FontFamilyName, 9.5f);
			menu.Padding = new Padding(4, 6, 4, 6);
			menu.ShowItemToolTips = false;
			foreach (ToolStripItem item in menu.Items)
			{
				item.ForeColor = Text;
				if (!(item is ToolStripSeparator)) item.Padding = new Padding(2, 5, 2, 5);
			}
		}

		private sealed class ModernColors : ProfessionalColorTable
		{
			public override Color ToolStripDropDownBackground => Surface;
			public override Color ImageMarginGradientBegin => Surface;
			public override Color ImageMarginGradientMiddle => Surface;
			public override Color ImageMarginGradientEnd => Surface;
			public override Color MenuBorder => Border;
			public override Color MenuItemBorder => Color.Transparent;
			public override Color MenuItemSelected => Hover;
			public override Color SeparatorDark => Border;
			public override Color SeparatorLight => Color.Transparent;
		}

		private sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
		{
			public ModernMenuRenderer() : base(new ModernColors()) { RoundedEdges = false; }

			protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
			{
				using (var b = new SolidBrush(Surface)) e.Graphics.FillRectangle(b, e.AffectedBounds);
			}

			protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
			{
				using (var p = new Pen(Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
			}

			protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

			protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
			{
				if (!e.Item.Selected || !e.Item.Enabled) return;
				var g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
				FillRounded(g, Hover, r, 5);
			}

			protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
			{
				e.TextColor = e.Item.Enabled ? Text : TextMuted;
				base.OnRenderItemText(e);
			}

			protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
			{
				e.ArrowColor = TextMuted;
				base.OnRenderArrow(e);
			}

			protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
			{
				int y = e.Item.Height / 2;
				using (var p = new Pen(Border)) e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
			}

			protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
			{
				var g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				var r = new Rectangle(e.ImageRectangle.X - 2, e.ImageRectangle.Y - 2, e.ImageRectangle.Width + 4, e.ImageRectangle.Height + 4);
				FillRounded(g, AccentSoft, r, 4);
				g.DrawImage(e.Image, e.ImageRectangle);
			}
		}
	}
}
