using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// While the mouse is on the pet, two round buttons appear beside its head: a message (the small chat balloon
	/// above the pet) and the microphone (the same balloon, with the Windows dictation on). They go away a moment
	/// after the mouse leaves.
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

		/// <summary>Self-test: are the buttons on screen.</summary>
		internal static bool Showing => pill != null && !pill.IsDisposed && pill.Visible;

		/// <summary>Self-test: click one of the buttons (0 message, 1 voice).</summary>
		internal static void TestClick(int button) { if (Showing) pill.Press(button); }

		static void Check()
		{
			var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
			bool can = Enabled && pet != null && !pet.Busy && !HideHotkey.Hidden
				&& !Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible)   // the bubble is above the pet: no buttons over it
				&& !(Form.ActiveForm is FormAsk) && !FormChat.IsOpen && !QuickChat.IsOpen;
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

		/// <summary>Two round buttons beside the pet: write a message, or talk. They never take the focus.</summary>
		sealed class Pill : Form
		{
			const int D = 28, Gap = 6;
			int hover = -1;   // 0 message, 1 voice
			readonly ToolTip tips = new ToolTip();

			public Pill()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
				Cursor = Cursors.Hand;
				BackColor = Theme.Surface;
				Size = new Size(D + 2, D * 2 + Gap + 2);
				using (var path = new System.Drawing.Drawing2D.GraphicsPath())
				{
					path.AddEllipse(0, 0, D + 2, D + 2);
					path.AddEllipse(0, D + Gap, D + 2, D + 2);
					Region = new Region(path);
				}
			}

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

			static Rectangle ButtonRect(int i) => new Rectangle(1, 1 + i * (D + Gap), D - 1, D - 1);
			static int Which(Point p) => p.Y < D + Gap / 2 ? 0 : 1;

			/// <summary>On the right of the pet, beside its head (left when there is no room).</summary>
			public void Place(FormPet pet)
			{
				var wa = Screen.FromControl(pet).WorkingArea;
				int x = pet.Right + 4, y = pet.Top + pet.Height / 3 - Height / 2;
				if (x + Width > wa.Right) x = pet.Left - Width - 4;
				x = Math.Max(wa.Left, Math.Min(wa.Right - Width, x));
				y = Math.Max(wa.Top, Math.Min(wa.Bottom - Height, y));
				if (Left != x || Top != y) Location = new Point(x, y);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Surface);
				for (int i = 0; i < 2; i++)
				{
					var r = ButtonRect(i);
					using (var b = new SolidBrush(hover == i ? Theme.Hover : Theme.Surface)) g.FillEllipse(b, r);
					using (var p = new Pen(Theme.Border, 1.2f)) g.DrawEllipse(p, r);
					var icon = Rectangle.Inflate(r, -7, -7);
					if (i == 0) ChatIcons.Message(g, icon, ChatIcons.Blue); else ChatIcons.Mic(g, icon, ChatIcons.Green);
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				int h = Which(e.Location);
				if (h == hover) return;
				hover = h;
				tips.SetToolTip(this, Lang.T(h == 1 ? "hover.talk" : "hover.message"));
				Invalidate();
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

			protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Press(Which(e.Location)); }

			public void Press(int button)
			{
				Hide();
				QuickChat.Open(voice: button == 1);
			}

			protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
		}
	}
}
