using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	public sealed partial class FormSettings
	{

		// ================================================================== Reminders page

		/// <summary>List of reminders: on/off, what and when, test, edit, delete; plus "remind me in N min".</summary>
		private sealed class RemindersView : ScrollSurface
		{
			private readonly FormSettings form;
			private readonly List<(Rectangle rect, object id)> hits = new List<(Rectangle, object)>();
			private object hover;
			static readonly int[] Quick = { 5, 10, 15, 30, 60 };

			public RemindersView(FormSettings f)
			{
				form = f;
				Reminders.Changed += OnChanged;
				Disposed += (s, e) => Reminders.Changed -= OnChanged;
			}

			void OnChanged() { if (!IsDisposed) { if (InvokeRequired) BeginInvoke(new Action(Invalidate)); else Invalidate(); } }

			protected override int PaintContent(Graphics g, int scrollY)
			{
				hits.Clear();
				int w = Width - ScrollBarWidth - 16;
				int y = 8 - scrollY;

				// --- actions: new reminder + quick timers
				var add = new Rectangle(8, y, Theme.MeasureText(g, Lang.T("rem.new"), form.fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width + 40, 36);
				Theme.DrawButton(g, add, Lang.T("rem.new"), form.fontBold, true, Equals(hover, "new"));
				hits.Add((add, "new"));
				int x = add.Right + 24;
				string quick = Lang.T("rem.quick");
				int qw = Theme.MeasureText(g, quick, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width;
				Theme.DrawText(g, quick, form.fontSmall, new Rectangle(x, y, qw + 4, 36), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				x += qw + 10;
				foreach (int m in Quick)
				{
					string t = Lang.T("rem.minutesShort", m);
					int cw = Theme.MeasureText(g, t, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 24;
					if (x + cw > w) break;
					var chip = new Rectangle(x, y + 5, cw, 26);
					Theme.FillRounded(g, Equals(hover, m) ? Theme.Hover : Theme.Surface, chip, 13);
					Theme.DrawRounded(g, Theme.Border, chip, 13);
					Theme.DrawText(g, t, form.fontSmall, chip, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					hits.Add((chip, m));
					x += cw + 6;
				}
				y += 52;

				var list = Reminders.All;
				if (list.Count == 0)
				{
					Theme.DrawText(g, Lang.T("rem.empty"), form.fontText, new Rectangle(8, y + 20, w, 40), Theme.TextMuted, TextFormatFlags.HorizontalCenter);
					return y + 80 + scrollY;
				}

				var now = DateTime.Now;
				foreach (var r in list)
				{
					var card = new Rectangle(8, y, w, 76);
					Theme.FillRounded(g, Equals(hover, r) ? Theme.SurfaceAlt : Theme.Surface, card, 10);
					Theme.DrawRounded(g, Theme.Border, card, 10);

					var toggle = new Rectangle(card.X + 18, card.Y + 27, 44, 22);
					Theme.DrawToggle(g, toggle, r.Enabled);
					hits.Add((Rectangle.Inflate(toggle, 6, 8), ("toggle", r)));

					// buttons on the right
					int bx = card.Right - 14;
					var buttons = new[] { ("delete", "rem.delete"), ("edit", "rem.edit"), ("test", "rem.test") };
					foreach (var b in buttons)
					{
						string t = Lang.T(b.Item2);
						int bw = Theme.MeasureText(g, t, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 22;
						bx -= bw;
						var br = new Rectangle(bx, card.Y + 23, bw, 30);
						Theme.DrawButton(g, br, t, form.fontSmall, false, Equals(hover, (b.Item1, r)));
						hits.Add((br, (b.Item1, r)));
						bx -= 6;
					}

					int tx = toggle.Right + 18, tw = bx - tx - 10;
					Theme.DrawText(g, r.Text, form.fontBold, new Rectangle(tx, card.Y + 14, tw, 22), r.Enabled ? Theme.Text : Theme.TextMuted,
						TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
					string sub = r.Summary();
					var next = r.Enabled ? r.Next(r.LastFired > now.AddDays(-1) ? r.LastFired : now) : null;
					if (next != null) sub += "  ·  " + Lang.T("rem.next", NextText(next.Value, now));
					Theme.DrawText(g, sub, form.fontSmall, new Rectangle(tx, card.Y + 40, tw, 20), Theme.TextMuted,
						TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
					hits.Add((card, r));
					y += card.Height + 10;
				}
				return y + 10 + scrollY;
			}

			static string NextText(DateTime t, DateTime now)
			{
				string hm = t.ToString("t", CultureInfo.CurrentCulture);
				if (t.Date == now.Date) return Lang.T("rem.today", hm);
				if (t.Date == now.Date.AddDays(1)) return Lang.T("rem.tomorrow", hm);
				return t.ToString("g", CultureInfo.CurrentCulture);
			}

			object HitAt(Point p)
			{
				// the most specific first: buttons and toggles were added before their card
				foreach (var h in hits) if (h.rect.Contains(p) && !(h.id is Reminder)) return h.id;
				foreach (var h in hits) if (h.rect.Contains(p)) return h.id;
				return null;
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var h = HitAt(e.Location);
				Cursor = h != null && !(h is Reminder) ? Cursors.Hand : Cursors.Default;
				if (!Equals(h, hover)) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				switch (HitAt(e.Location))
				{
					case "new":
						using (var dlg = new FormReminder(null))
							if (dlg.ShowDialog(form) == DialogResult.OK) { Reminders.Add(dlg.Result); form.StatusText = Lang.T("rem.saved"); }
						break;
					case int minutes:
						Reminders.In(minutes);
						form.StatusText = Lang.T("rem.timerSet", minutes);
						break;
					case ValueTuple<string, Reminder> act:
						var r = act.Item2;
						switch (act.Item1)
						{
							case "toggle":
								r.Enabled = !r.Enabled;
								if (r.Enabled) { r.LastFired = DateTime.Now; if (r.Kind == Reminder.Kinds.Once && r.At < DateTime.Now) r.At = DateTime.Now.AddMinutes(10); }
								Reminders.Save();
								break;
							case "test": Reminders.Fire(r); break;
							case "edit":
								using (var dlg = new FormReminder(r))
									if (dlg.ShowDialog(form) == DialogResult.OK) { Reminders.Save(); form.StatusText = Lang.T("rem.saved"); }
								break;
							case "delete":
								if (MessageBox.Show(form, Lang.T("rem.deleteConfirm", r.Text), Lang.T("rem.delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
									Reminders.Remove(r);
								break;
						}
						break;
				}
				Invalidate();
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);
				if (HitAt(e.Location) is Reminder r)
					using (var dlg = new FormReminder(r))
						if (dlg.ShowDialog(form) == DialogResult.OK) Reminders.Save();
			}
		}
	}

	/// <summary>Create or edit a reminder: what the pet says and when.</summary>
	internal sealed class FormReminder : Form
	{
		public Reminder Result { get; private set; }
		readonly Reminder editing;
		readonly TextBox text = new TextBox { MaxLength = 200, Width = 380 };
		readonly RadioButton kInterval = new RadioButton { AutoSize = true }, kDaily = new RadioButton { AutoSize = true }, kOnce = new RadioButton { AutoSize = true };
		readonly NumericUpDown minutes = new NumericUpDown { Minimum = 1, Maximum = 1440, Width = 70 };
		readonly NumericUpDown from = new NumericUpDown { Minimum = 0, Maximum = 23, Width = 56 }, to = new NumericUpDown { Minimum = 0, Maximum = 24, Width = 56 };
		readonly DateTimePicker time = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
		readonly DateTimePicker at = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy  HH:mm", Width = 170 };
		readonly CheckBox[] days = new CheckBox[7];
		readonly FlowLayoutPanel rowInterval, rowDaily, rowOnce, rowDays;

		public FormReminder(Reminder r)
		{
			editing = r;
			var model = r ?? new Reminder { Text = "", Days = 127 };
			Text = Lang.T(r == null ? "rem.new" : "rem.editTitle");
			Icon = Properties.Resources.icon;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			AutoSize = true;
			AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Padding = new Padding(16);

			var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
			Label L(string key, bool bold = false) => new Label { Text = Lang.T(key), AutoSize = true, Margin = new Padding(0, 10, 0, 4), Font = bold ? new Font(Theme.FontFamilyName, 9.5f, FontStyle.Bold) : null };
			FlowLayoutPanel Row(params Control[] cs)
			{
				var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(20, 2, 0, 2) };
				foreach (var c in cs) { c.Margin = new Padding(0, 4, 6, 0); p.Controls.Add(c); }
				return p;
			}
			Label T(string key) => new Label { Text = Lang.T(key), AutoSize = true, Padding = new Padding(0, 4, 0, 0) };

			root.Controls.Add(L("rem.whatSays", true));
			text.Text = model.Text;
			root.Controls.Add(text);

			root.Controls.Add(L("rem.when", true));
			kInterval.Text = Lang.T("rem.kindInterval"); kDaily.Text = Lang.T("rem.kindDaily"); kOnce.Text = Lang.T("rem.kindOnce");
			root.Controls.Add(kInterval);
			rowInterval = Row(T("rem.every"), minutes, T("rem.minutesFrom"), from, T("rem.hoursTo"), to, T("rem.hoursEnd"));
			root.Controls.Add(rowInterval);
			root.Controls.Add(kDaily);
			rowDaily = Row(T("rem.at"), time);
			root.Controls.Add(rowDaily);
			root.Controls.Add(kOnce);
			rowOnce = Row(T("rem.on"), at);
			root.Controls.Add(rowOnce);

			root.Controls.Add(L("rem.days", true));
			var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
			rowDays = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
			foreach (int i in new[] { 1, 2, 3, 4, 5, 6, 0 })   // Monday first
			{
				days[i] = new CheckBox { Text = names[i], AutoSize = true, Checked = (model.Days & (1 << i)) != 0, Margin = new Padding(0, 0, 8, 0) };
				rowDays.Controls.Add(days[i]);
			}
			root.Controls.Add(rowDays);

			var ok = new Button { Text = Lang.T("rem.save"), DialogResult = DialogResult.None, AutoSize = true, MinimumSize = new Size(110, 32) };
			var cancel = new Button { Text = Lang.T("rem.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(110, 32) };
			var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 18, 0, 0), Width = 380 };
			buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
			root.Controls.Add(buttons);
			Controls.Add(root);
			AcceptButton = ok; CancelButton = cancel;

			minutes.Value = Math.Max(1, Math.Min(1440, model.Minutes));
			from.Value = Math.Max(0, Math.Min(23, model.FromHour));
			to.Value = Math.Max(0, Math.Min(24, model.ToHour));
			time.Value = DateTime.Today + (TimeSpan.TryParse(model.Time, out var ts) ? ts : new TimeSpan(9, 0, 0));
			at.Value = model.At < DateTime.Now ? DateTime.Now.AddMinutes(10) : model.At;
			kInterval.Checked = model.Kind == Reminder.Kinds.Interval;
			kDaily.Checked = model.Kind == Reminder.Kinds.Daily;
			kOnce.Checked = model.Kind == Reminder.Kinds.Once;
			foreach (var k in new[] { kInterval, kDaily, kOnce }) k.CheckedChanged += (s, e) => UpdateEnabled();
			UpdateEnabled();

			ok.Click += (s, e) =>
			{
				if (string.IsNullOrWhiteSpace(text.Text)) { text.Focus(); System.Media.SystemSounds.Beep.Play(); return; }
				int mask = 0;
				for (int i = 0; i < 7; i++) if (days[i].Checked) mask |= 1 << i;
				if (mask == 0 && !kOnce.Checked) { days[1].Focus(); System.Media.SystemSounds.Beep.Play(); return; }
				var target = editing ?? new Reminder();
				target.Text = text.Text.Trim();
				target.Kind = kOnce.Checked ? Reminder.Kinds.Once : kDaily.Checked ? Reminder.Kinds.Daily : Reminder.Kinds.Interval;
				target.Minutes = (int)minutes.Value;
				target.FromHour = (int)from.Value;
				target.ToHour = (int)to.Value % 24;
				target.Time = time.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
				target.At = at.Value;
				target.Days = mask == 0 ? 127 : mask;
				target.Enabled = true;
				target.SnoozedUntil = DateTime.MinValue;
				// counting starts now (an interval reminder does not fire at once)
				target.LastFired = target.Kind == Reminder.Kinds.Once ? DateTime.MinValue : DateTime.Now;
				Result = target;
				DialogResult = DialogResult.OK;
			};

			Theme.ApplyTo(this);
			Theme.MakePrimary(ok);
			foreach (var n in new Control[] { minutes, from, to }) { n.BackColor = Theme.Surface; n.ForeColor = Theme.Text; }
		}

		void UpdateEnabled()
		{
			rowInterval.Enabled = kInterval.Checked;
			rowDaily.Enabled = kDaily.Checked;
			rowOnce.Enabled = kOnce.Checked;
			rowDays.Enabled = !kOnce.Checked;
		}
	}
}
