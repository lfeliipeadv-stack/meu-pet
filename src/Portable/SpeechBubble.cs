using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// A speech bubble above the pet: a short text and up to two buttons ("Done", "Snooze 10 min").
	/// It follows the pet while it walks and closes by itself after a while (not while the mouse is on it).
	/// With no pet on screen (all hidden), the text goes to a Windows notification instead.
	/// </summary>
	public sealed class SpeechBubble : Form
	{
		/// <summary>A button of the bubble.</summary>
		public sealed class Choice
		{
			public string Text;
			public Action Run;
			public bool Primary;
			public Choice(string text, Action run, bool primary = false) { Text = text; Run = run; Primary = primary; }
		}

		static SpeechBubble current;

		/// <summary>
		/// Show a bubble above the main pet. A new bubble replaces the one on screen.
		/// <paramref name="seconds"/> = 0 keeps it until a button (or the ✕) is clicked.
		/// </summary>
		public static void Say(string text, int seconds = 12, params Choice[] choices)
		{
			var pet = MainPet();
			if (pet == null)
			{
				ProcessIcon.Current?.Notify(Program.Mainthread?.CurrentHeader?.Petname ?? "desktopPet", text, null);
				return;
			}
			if (current != null && !current.IsDisposed) current.Close();
			current = new SpeechBubble(pet, text, seconds, choices ?? new Choice[0]);
			current.Show();
			// the pet stops and stays still while it talks (an action it just started, like sleeping, finishes first)
			// every pet stays still while a bubble is open (each bubble holds once and lets go when it closes)
			FormPet.Hold(true);
			current.FormClosed += (s, e) => FormPet.Hold(false);
		}

		/// <summary>
		/// A short line said by one pet to another (pets that meet): its own bubble above that pet, next to the main one,
		/// gone by itself. Not during quiet time, and never over a bubble that is talking to the user.
		/// </summary>
		public static SpeechBubble Chat(FormPet pet, string text, int seconds = 4)
		{
			if (pet == null || pet.IsDisposed || !pet.Visible || QuietNow) return null;
			var b = new SpeechBubble(pet, text, seconds, new Choice[0]);
			b.Show();
			FormPet.Hold(true);
			b.FormClosed += (s, e) => FormPet.Hold(false);
			return b;
		}

		/// <summary>A bubble is talking to the user now (a question, a reminder…): pets do not chat over it.</summary>
		public static bool Talking => current != null && !current.IsDisposed && current.Visible;

		// ------------------------------------------------------------------ quiet time

		/// <summary>Focus time (Pomodoro running, not paused) or pets hidden with Ctrl+Alt+P: no bubbles, no sounds.</summary>
		public static bool QuietNow => HideHotkey.Hidden || (Pomodoro.Current == Pomodoro.Phase.Focus && !Pomodoro.Paused);

		static readonly List<(string text, Choice[] choices)> held = new List<(string, Choice[])>();
		static Timer releaseTimer;

		/// <summary>Number of notices waiting for the quiet time to end.</summary>
		public static int HeldCount => held.Count;

		/// <summary>
		/// A notice the pet gives on its own (reminder, meeting, battery, download…). During quiet time it waits,
		/// and when the quiet time ends the pet tells how many arrived and can show them one by one.
		/// Things the user asked for right now (games, menu actions) use <see cref="Say"/> instead.
		/// </summary>
		public static void Notify(string text, int seconds = 12, params Choice[] choices)
		{
			if (QuietNow)
			{
				held.Add((text, choices ?? new Choice[0]));
				if (held.Count > 30) held.RemoveAt(0);
				return;
			}
			Say(text, seconds, choices);
		}

		/// <summary>Call when the quiet time may have ended (focus over, pets shown again).</summary>
		public static void QuietChanged()
		{
			if (QuietNow || held.Count == 0) return;
			// a few seconds later: the "break time!" bubble of the focus timer comes first
			if (releaseTimer == null)
			{
				releaseTimer = new Timer { Interval = 7000 };
				releaseTimer.Tick += (s, e) =>
				{
					releaseTimer.Stop();
					if (QuietNow || held.Count == 0) return;
					Say(Lang.T(held.Count == 1 ? "quiet.heldOne" : "quiet.heldMany", held.Count), 0,
						new Choice(Lang.T("quiet.show"), ShowNextHeld, true),
						new Choice(Lang.T("quiet.dismiss"), () => held.Clear()));
				};
			}
			releaseTimer.Stop();
			releaseTimer.Start();
		}

		static void ShowNextHeld()
		{
			if (held.Count == 0) return;
			var (text, choices) = held[0];
			held.RemoveAt(0);
			var list = new List<Choice>(choices);
			if (held.Count > 0) list.Add(new Choice(Lang.T("quiet.next", held.Count), ShowNextHeld, list.Count == 0));
			Say(text, 0, list.ToArray());
		}

		/// <summary>The first real pet that is visible on screen (not a balloon or other child window).</summary>
		static FormPet MainPet()
		{
			if (HideHotkey.Hidden) return null;
			return Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
		}

		readonly FormPet pet;
		readonly string text;
		readonly Choice[] choices;
		readonly Timer follow;
		readonly Font font = Theme.CreateFont(14), fontBold = Theme.CreateFont(13, FontStyle.Bold), fontName = Theme.CreateFont(11.5f, FontStyle.Bold);
		readonly List<(Rectangle rect, Choice choice)> buttonRects = new List<(Rectangle, Choice)>();
		Rectangle closeRect, textRect;
		object hover;
		DateTime closeAt;
		const int Pad = 14, MaxTextW = 280;

		SpeechBubble(FormPet pet, string text, int seconds, Choice[] choices)
		{
			this.pet = pet;
			this.text = text;
			this.choices = choices;
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			DoubleBuffered = true;
			BackColor = Theme.Surface;
			closeAt = seconds > 0 ? DateTime.Now.AddSeconds(seconds) : DateTime.MaxValue;
			Theme.ApplyWindowChrome(this);
			Layout();
			Place();
			follow = new Timer { Interval = 40 };
			follow.Tick += (s, e) =>
			{
				if (pet.IsDisposed || !pet.Visible) { Close(); return; }
				if (DateTime.Now >= closeAt && !Bounds.Contains(Cursor.Position)) { Close(); return; }
				Place();
			};
			follow.Start();
			FormClosed += (s, e) => { follow.Stop(); if (current == this) current = null; };
		}

		/// <summary>Self-test: what this bubble says.</summary>
		internal string Said => text;

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW: not in Alt+Tab
				cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE: never steals the focus
				cp.ExStyle |= 0x00000008;   // WS_EX_TOPMOST (set here: the TopMost property would activate the window)
				cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
				return cp;
			}
		}

		string PetName => Program.Mainthread?.CurrentHeader?.Petname ?? "";

		new void Layout()
		{
			using (var g = CreateGraphics())
			{
				var ts = Theme.MeasureText(g, text, font, MaxTextW, TextFormatFlags.WordBreak);
				int w = Math.Max(ts.Width, 120);
				int y = Pad;
				int nameH = PetName.Length > 0 ? 18 : 0;
				y += nameH;
				textRect = new Rectangle(Pad, y, w, ts.Height);
				y += ts.Height;
				buttonRects.Clear();
				if (choices.Length > 0)
				{
					y += 10;
					int bx = Pad;
					foreach (var c in choices)
					{
						int bw = Theme.MeasureText(g, c.Text, fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width + 22;
						buttonRects.Add((new Rectangle(bx, y, bw, 30), c));
						bx += bw + 8;
					}
					w = Math.Max(w, bx - 8 - Pad);
					y += 30;
				}
				int width = w + Pad * 2 + 18;   // room for the ✕
				ClientSize = new Size(width, y + Pad);
				closeRect = new Rectangle(width - 28, 8, 20, 20);
			}
		}

		/// <summary>Above the pet (below if there is no room), always on the pet's screen.</summary>
		void Place()
		{
			var wa = Screen.FromControl(pet).WorkingArea;
			int x = pet.Left + pet.Width / 2 - Width / 2;
			int y = pet.Top - Height - 4;
			if (y < wa.Top) y = pet.Bottom + 4;
			x = Math.Max(wa.Left + 4, Math.Min(wa.Right - Width - 4, x));
			y = Math.Max(wa.Top + 4, Math.Min(wa.Bottom - Height - 4, y));
			if (Left != x || Top != y) Location = new Point(x, y);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			Theme.Prepare(g);
			g.Clear(Theme.Surface);
			using (var accent = new SolidBrush(Theme.Accent)) g.FillRectangle(accent, 0, 0, 4, Height);
			if (PetName.Length > 0)
				Theme.DrawText(g, PetName, fontName, new Rectangle(Pad, Pad - 4, Width - Pad * 2 - 24, 18), Theme.Accent, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			Theme.DrawText(g, text, font, textRect, Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
			foreach (var b in buttonRects)
				Theme.DrawButton(g, b.rect, b.choice.Text, fontBold, b.choice.Primary, Equals(hover, b.choice));
			if (Equals(hover, "close")) Theme.FillRounded(g, Theme.Hover, closeRect, 4);
			Theme.DrawText(g, "✕", fontBold, closeRect, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			object h = closeRect.Contains(e.Location) ? "close" : buttonRects.FirstOrDefault(b => b.rect.Contains(e.Location)).choice;
			Cursor = h != null ? Cursors.Hand : Cursors.Default;
			if (!Equals(h, hover)) { hover = h; Invalidate(); }
		}

		protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (closeRect.Contains(e.Location)) { Close(); return; }
			var hit = buttonRects.FirstOrDefault(b => b.rect.Contains(e.Location)).choice;
			if (hit != null) { Close(); try { hit.Run?.Invoke(); } catch (Exception skipped) { LogFile.Ignored(skipped); } return; }
			// a click on the text: just keep it open a bit longer
			closeAt = DateTime.Now.AddSeconds(10);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing) { follow?.Dispose(); font.Dispose(); fontBold.Dispose(); fontName.Dispose(); }
			base.Dispose(disposing);
		}
	}
}
