using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// With the AI set up, a small "Ask" button appears next to the pet while the mouse is on it (like the CPU plate):
	/// one click and the question box opens, no menu needed. It goes away a moment after the mouse leaves.
	/// </summary>
	public static class HoverAsk
	{
		static Timer timer;
		static Pill pill;
		static DateTime lastOver = DateTime.MinValue;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("HoverAsk", true);
			set { Program.MyData.SetSetting("HoverAsk", value); if (!value) pill?.Hide(); }
		}

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 80 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		/// <summary>Self-test: is the button on screen.</summary>
		internal static bool Showing => pill != null && !pill.IsDisposed && pill.Visible;

		static void Check()
		{
			var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
			bool can = Enabled && pet != null && !pet.Busy && !HideHotkey.Hidden
				&& !Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible)   // the bubble is above the pet: no button over it
				&& !(Form.ActiveForm is FormAsk) && !FormChat.IsOpen;
			if (!can) { if (pill != null && pill.Visible) pill.Hide(); return; }

			var mouse = Cursor.Position;
			bool over = Rectangle.Inflate(pet.Bounds, 6, 6).Contains(mouse) || (pill != null && pill.Visible && Rectangle.Inflate(pill.Bounds, 8, 8).Contains(mouse));
			if (over) lastOver = DateTime.Now;
			bool show = over || (DateTime.Now - lastOver).TotalMilliseconds < 700;
			if (!show) { if (pill != null && pill.Visible) pill.Hide(); return; }

			if (pill == null || pill.IsDisposed) pill = new Pill();
			pill.Place(pet);
			if (!pill.Visible) pill.Show();
		}

		/// <summary>The button: rounded, themed, never takes the focus.</summary>
		sealed class Pill : Form
		{
			readonly Font font = Theme.CreateFont(12.5f, FontStyle.Bold);
			bool hover;

			public Pill()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
				Cursor = Cursors.Hand;
				using (var g = CreateGraphics())
					Size = new Size(Theme.MeasureText(g, Text2, font, int.MaxValue, TextFormatFlags.SingleLine).Width + 30, 30);
				Theme.ApplyWindowChrome(this);
			}

			string Text2 => "💬 " + Lang.T("hover.ask");

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

			/// <summary>On the right of the pet, near its head (left when there is no room).</summary>
			public void Place(FormPet pet)
			{
				var wa = Screen.FromControl(pet).WorkingArea;
				int x = pet.Right + 2, y = pet.Top + pet.Height / 3 - Height / 2;
				if (x + Width > wa.Right) x = pet.Left - Width - 2;
				x = Math.Max(wa.Left, Math.Min(wa.Right - Width, x));
				y = Math.Max(wa.Top, Math.Min(wa.Bottom - Height, y));
				if (Left != x || Top != y) Location = new Point(x, y);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(hover ? ControlPaint.Light(Theme.Accent, 0.15f) : Theme.Accent);
				Theme.DrawText(g, Text2, font, ClientRectangle, Theme.AccentText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			}

			protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				Hide();
				FormAsk.Open();
			}

			protected override void Dispose(bool disposing) { if (disposing) font.Dispose(); base.Dispose(disposing); }
		}
	}
}
