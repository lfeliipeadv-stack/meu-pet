using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// The small windows of the program in its own look (instead of the gray Windows boxes): a message with buttons,
	/// a question with a text field, and the whole answer of the AI. All of them keep the pets still while open.
	/// </summary>
	internal static class Dialogs
	{
		/// <summary>Self-test and snapshots: one of each window, not shown yet.</summary>
		internal static List<(string name, Func<Form> make)> TestWindows() => new List<(string, Func<Form>)>
		{
			("win-message", () => new PetMessage(Lang.T("pets.removeConfirm", "Rex"), Lang.T("pets.remove"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)),
			("win-error", () => new PetMessage(Lang.T("agenda.error", "404"), "desktopPet", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1)),
			("win-prompt", () => new PromptDialog(Lang.T("weather.askCity"), "Curitiba")),
			("win-answer", () => new AnswerWindow(Lang.T("chat.sugHelp"), Lang.T("chat.help"))),
		};
	}

	/// <summary>The base of these windows: background, fonts, the icon in a circle and the buttons row.</summary>
	internal class ThemedDialog : PetDialog
	{
		protected readonly Font fTitle = Theme.CreateFont(18, FontStyle.Bold), fText = Theme.CreateFont(14), fSmall = Theme.CreateFont(12), fIcon = Theme.CreateFont(22);
		protected readonly List<Button> buttons = new List<Button>();
		protected string iconEmoji = "💬";
		protected Color iconBack = Theme.AccentSoft;
		protected const int Pad = 24;

		public ThemedDialog()
		{
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			Icon = Properties.Resources.icon;
			DoubleBuffered = true;
			KeyPreview = true;
			BackColor = Theme.Background;
			ForeColor = Theme.Text;
		}

		protected Button AddButton(string text, DialogResult result, bool primary)
		{
			var b = new Button { Text = text, DialogResult = result, AutoSize = true, MinimumSize = new Size(104, 38), FlatStyle = FlatStyle.Flat };
			buttons.Add(b);
			Controls.Add(b);
			if (primary) AcceptButton = b;
			if (result == DialogResult.Cancel || result == DialogResult.No) CancelButton = b;
			return b;
		}

		/// <summary>Buttons on the right of the bottom line; the first one added ends up on the right.</summary>
		protected int LayoutButtons(int y, int width)
		{
			int x = width - Pad;
			foreach (var b in buttons)
			{
				x -= b.Width;
				b.Location = new Point(x, y);
				x -= 10;
			}
			return y + (buttons.Count > 0 ? buttons[0].Height : 0);
		}

		protected void ApplyTheme(Button primary)
		{
			Theme.ApplyTo(this);
			BackColor = Theme.Background;
			if (primary != null) Theme.MakePrimary(primary);
		}

		/// <summary>The emoji in a colored circle at the top left.</summary>
		protected void PaintIcon(Graphics g)
		{
			var r = new Rectangle(Pad, Pad, 48, 48);
			Theme.FillRounded(g, iconBack, r, 24);
			Theme.DrawText(g, iconEmoji, fIcon, r, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape && CancelButton == null) { DialogResult = DialogResult.Cancel; Close(); }
		}
	}

	/// <summary>A message with buttons, like MessageBox.Show (same arguments and results), in the program's look.</summary>
	internal sealed class PetMessage : ThemedDialog
	{
		readonly string text, caption;
		Rectangle textRect, titleRect;
		const int W = 460;

		public PetMessage(string text, string caption, MessageBoxButtons kind, MessageBoxIcon icon, MessageBoxDefaultButton def)
		{
			this.text = text ?? ""; this.caption = string.IsNullOrEmpty(caption) ? "desktopPet" : caption;
			Text = this.caption;
			(iconEmoji, iconBack) = icon == MessageBoxIcon.Question ? ("❓", Theme.AccentSoft)
				: icon == MessageBoxIcon.Warning ? ("⚠️", Color.FromArgb(50, 255, 170, 0))
				: icon == MessageBoxIcon.Error ? ("❌", Color.FromArgb(40, Theme.Danger))
				: icon == MessageBoxIcon.Information ? ("💡", Theme.AccentSoft) : ("💬", Theme.AccentSoft);

			var list = kind == MessageBoxButtons.YesNo ? new[] { (Lang.T("dlg.yes"), DialogResult.Yes), (Lang.T("dlg.no"), DialogResult.No) }
				: kind == MessageBoxButtons.YesNoCancel ? new[] { (Lang.T("dlg.yes"), DialogResult.Yes), (Lang.T("dlg.no"), DialogResult.No), (Lang.T("rem.cancel"), DialogResult.Cancel) }
				: kind == MessageBoxButtons.OKCancel ? new[] { ("OK", DialogResult.OK), (Lang.T("rem.cancel"), DialogResult.Cancel) }
				: new[] { ("OK", DialogResult.OK) };
			int primaryIndex = def == MessageBoxDefaultButton.Button2 ? 1 : def == MessageBoxDefaultButton.Button3 ? 2 : 0;
			primaryIndex = Math.Min(primaryIndex, list.Length - 1);
			Button primary = null;
			// added from right to left: the first choice (Yes/OK) on the right
			for (int i = 0; i < list.Length; i++)
			{
				var b = AddButton(list[i].Item1, list[i].Item2, i == primaryIndex);
				if (i == primaryIndex) primary = b;
			}
			if (kind == MessageBoxButtons.OK) CancelButton = buttons[0];

			int tx = Pad + 48 + 16, tw = W - tx - Pad;
			int th, bh;
			using (var g = CreateGraphics())
			{
				th = Theme.MeasureText(g, this.caption, fTitle, tw, TextFormatFlags.WordBreak).Height;
				bh = Theme.MeasureText(g, this.text, fText, tw, TextFormatFlags.WordBreak).Height;
			}
			titleRect = new Rectangle(tx, Pad, tw, th);
			textRect = new Rectangle(tx, titleRect.Bottom + 8, tw, bh);
			int y = Math.Max(Pad + 48, textRect.Bottom) + 24;
			ClientSize = new Size(W, 100);
			foreach (var b in buttons) { b.Font = fText; b.PerformLayout(); }
			ApplyTheme(primary);
			int bottom = LayoutButtons(y, W);
			ClientSize = new Size(W, bottom + Pad);
			ActiveControl = primary;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			PaintIcon(g);
			Theme.DrawText(g, caption, fTitle, titleRect, Theme.Text, TextFormatFlags.WordBreak);
			Theme.DrawText(g, text, fText, textRect, Theme.TextMuted, TextFormatFlags.WordBreak);
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Pad, buttons[0].Top - 12, ClientSize.Width - Pad, buttons[0].Top - 12);
		}

		// ------------------------------------------------------------------ MessageBox.Show, the same calls

		public static DialogResult Show(string text) => Show(null, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(string text, string caption) => Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(string text, string caption, MessageBoxButtons b) => Show(null, text, caption, b, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(string text, string caption, MessageBoxButtons b, MessageBoxIcon i) => Show(null, text, caption, b, i, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d) => Show(null, text, caption, b, i, d);
		public static DialogResult Show(IWin32Window owner, string text) => Show(owner, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(IWin32Window owner, string text, string caption) => Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b) => Show(owner, text, caption, b, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b, MessageBoxIcon i) => Show(owner, text, caption, b, i, MessageBoxDefaultButton.Button1);

		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d)
		{
			try
			{
				using (var m = new PetMessage(text, caption, b, i, d))
				{
					if (owner == null) { m.StartPosition = FormStartPosition.CenterScreen; m.TopMost = true; }
					return owner != null ? m.ShowDialog(owner) : m.ShowDialog();
				}
			}
			catch (Exception)
			{
				// very early (no theme yet) or in a broken state: the Windows box still tells it
				return MessageBox.Show(text, caption, b, i, d);
			}
		}
	}

	/// <summary>A question with a text field (a link, a name, a city…): Cancel or OK.</summary>
	internal sealed class PromptDialog : ThemedDialog
	{
		readonly string question;
		readonly TextBox box = new TextBox { BorderStyle = BorderStyle.None };
		Rectangle textRect, field;
		const int W = 500;

		public string Value => box.Text;

		public PromptDialog(string question, string value)
		{
			this.question = question ?? "";
			Text = "desktopPet";
			iconEmoji = "✏️";
			var ok = AddButton("OK", DialogResult.OK, true);
			AddButton(Lang.T("rem.cancel"), DialogResult.Cancel, false);
			box.Text = value ?? "";
			box.Font = fText;
			Controls.Add(box);

			int tx = Pad + 48 + 16, tw = W - tx - Pad;
			int bh;
			using (var g = CreateGraphics()) bh = Theme.MeasureText(g, this.question, fText, tw, TextFormatFlags.WordBreak).Height;
			textRect = new Rectangle(tx, Pad + 2, tw, bh);
			field = new Rectangle(Pad, Math.Max(Pad + 48, textRect.Bottom) + 16, W - Pad * 2, 44);
			foreach (var b in buttons) b.Font = fText;
			ClientSize = new Size(W, 100);
			ApplyTheme(ok);
			box.BorderStyle = BorderStyle.None;
			box.BackColor = Theme.Surface; box.ForeColor = Theme.Text;
			box.Width = field.Width - 28;
			box.Location = new Point(field.X + 14, field.Y + (field.Height - box.Height) / 2);
			int bottom = LayoutButtons(field.Bottom + 28, W);
			ClientSize = new Size(W, bottom + Pad);
			ActiveControl = box;
			box.SelectAll();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			PaintIcon(g);
			Theme.DrawText(g, question, fText, textRect, Theme.Text, TextFormatFlags.WordBreak);
			Theme.FillRounded(g, Theme.Surface, field, 10);
			Theme.DrawRounded(g, box.Focused ? Theme.Accent : Theme.Border, field, 10);
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Pad, buttons[0].Top - 12, ClientSize.Width - Pad, buttons[0].Top - 12);
		}

		/// <summary>Ask; null when cancelled.</summary>
		public static string Ask(string question, string value)
		{
			using (var d = new PromptDialog(question, value))
			{
				var owner = Form.ActiveForm;
				return (owner != null && !(owner is PromptDialog) ? d.ShowDialog(owner) : d.ShowDialog()) == DialogResult.OK ? d.Value : null;
			}
		}
	}

	/// <summary>The whole answer of the AI: the question in a balloon, the answer in a card, Copy and Close.</summary>
	internal sealed class AnswerWindow : ThemedDialog
	{
		readonly string question;
		readonly TextBox answer = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
		Rectangle qRect, card;

		public AnswerWindow(string q, string text)
		{
			question = q ?? "";
			string pet = Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";
			Text = Lang.T("ask.title", pet);
			iconEmoji = "🐾";
			FormBorderStyle = FormBorderStyle.Sizable;
			MinimumSize = new Size(460, 360);
			StartPosition = FormStartPosition.CenterScreen;
			TopMost = true;
			AddButton(Lang.T("launch.close"), DialogResult.Cancel, true);
			var copy = AddButton(Lang.T("int.copyCmd.button"), DialogResult.None, false);
			copy.Click += (s, e) => { try { Clipboard.SetText(text ?? ""); copy.Text = "✓"; } catch (Exception skipped) { LogFile.Ignored(skipped); } };
			answer.Text = (text ?? "").Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
			answer.Font = fText;
			Controls.Add(answer);
			foreach (var b in buttons) b.Font = fText;
			ClientSize = new Size(600, 480);
			ApplyTheme(buttons[0]);
			answer.BorderStyle = BorderStyle.None;
			answer.BackColor = Theme.Surface; answer.ForeColor = Theme.Text;
			LayoutAll();
			Resize += (s, e) => LayoutAll();
		}

		void LayoutAll()
		{
			int w = ClientSize.Width;
			int qh;
			using (var g = CreateGraphics()) qh = Math.Min(80, Theme.MeasureText(g, question, fText, w - Pad * 2 - 100, TextFormatFlags.WordBreak).Height);
			qRect = new Rectangle(Pad + 64, Pad + 6, Math.Min(w - Pad * 2 - 64, 0) + 0, qh + 20);
			using (var g = CreateGraphics()) qRect.Width = Math.Min(w - Pad * 2 - 64, Theme.MeasureText(g, question, fText, w - Pad * 2 - 100, TextFormatFlags.WordBreak).Width + 28);
			int by = ClientSize.Height - Pad - (buttons.Count > 0 ? buttons[0].Height : 38);
			LayoutButtons(by, w);
			card = new Rectangle(Pad, Math.Max(Pad + 48, qRect.Bottom) + 16, w - Pad * 2, by - 16 - (Math.Max(Pad + 48, qRect.Bottom) + 16));
			answer.Location = new Point(card.X + 16, card.Y + 14);
			answer.Size = new Size(card.Width - 24, card.Height - 24);
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			PaintIcon(g);
			Theme.FillRounded(g, Theme.Accent, qRect, 14);
			Theme.DrawText(g, question, fText, new Rectangle(qRect.X + 14, qRect.Y + 10, qRect.Width - 24, qRect.Height - 16), Theme.AccentText, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
			Theme.FillRounded(g, Theme.Surface, card, 12);
			Theme.DrawRounded(g, Theme.Border, card, 12);
		}
	}
}
