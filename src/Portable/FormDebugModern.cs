using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Debug window in the style of the panel: what the pet does, live. Opens when the program starts with
	/// CTRL (or SHIFT) held down, or from Settings. Filters by level, search, pause, copy, and the XML tools.
	/// The log is kept by <see cref="StartUp"/> from the start, so the first lines are never lost.
	/// </summary>
	public sealed class FormDebugModern : Form
	{
		static FormDebugModern instance;

		/// <summary>Open the window (or bring it to the front).</summary>
		public static void ShowWindow()
		{
			if (instance == null || instance.IsDisposed) instance = new FormDebugModern();
			instance.Show();
			if (instance.WindowState == FormWindowState.Minimized) instance.WindowState = FormWindowState.Normal;
			instance.Activate();
		}

		/// <summary>The window is open (the pets then show their debug menu on right click).</summary>
		public static bool IsOpen => instance != null && !instance.IsDisposed;

		/// <summary>New lines arrived (any thread): the view refreshes on its timer.</summary>
		internal static void Poke() { if (instance != null) instance.dirty = true; }

		readonly DebugView view;
		readonly TextBox search;
		readonly Timer timer;
		volatile bool dirty = true;

		FormDebugModern()
		{
			Text = Lang.T("debug.title");
			Icon = Properties.Resources.icon;
			StartPosition = FormStartPosition.Manual;
			var wa = Screen.PrimaryScreen.WorkingArea;
			Size = new Size(Math.Min(880, wa.Width - 40), Math.Min(560, wa.Height - 40));
			Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);
			MinimumSize = new Size(560, 320);
			KeyPreview = true;

			view = new DebugView(this) { Dock = DockStyle.Fill };
			search = new TextBox { BorderStyle = BorderStyle.None, PlaceholderText = Lang.T("debug.search") };
			search.TextChanged += (s, e) => { view.Filter = search.Text.Trim(); };
			view.Controls.Add(search);
			Controls.Add(view);

			timer = new Timer { Interval = 250 };
			timer.Tick += (s, e) => { if (dirty) { dirty = false; view.Reload(); } };
			timer.Start();

			ApplyTheme();
			Theme.Changed += ApplyTheme;
			Lang.Changed += OnLang;
			FormClosed += (s, e) => { timer.Stop(); Theme.Changed -= ApplyTheme; Lang.Changed -= OnLang; instance = null; };
			KeyDown += (s, e) =>
			{
				if (e.Control && e.KeyCode == Keys.F) { search.Focus(); e.Handled = true; }
				if (e.KeyCode == Keys.Escape && search.Focused && search.Text.Length > 0) { search.Text = ""; e.Handled = true; }
			};
		}

		void OnLang() { Text = Lang.T("debug.title"); search.PlaceholderText = Lang.T("debug.search"); view.Invalidate(); }

		void ApplyTheme()
		{
			BackColor = Theme.Background;
			search.BackColor = Theme.Surface;
			search.ForeColor = Theme.Text;
			search.Font = view.FontText;
			if (IsHandleCreated) Theme.ApplyWindowChrome(this);
			view.Invalidate();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Theme.ApplyWindowChrome(this);
		}

		internal void PlaceSearch(Rectangle r)
		{
			var b = new Rectangle(r.X + 30, r.Y + (r.Height - search.PreferredHeight) / 2, r.Width - 38, search.PreferredHeight);
			if (search.Bounds != b) search.Bounds = b;
		}

		// ================================================================ the view

		sealed class DebugView : Control
		{
			readonly FormDebugModern form;
			public readonly Font FontText = Theme.CreateFont(13), FontSmall = Theme.CreateFont(11.5f), FontBold = Theme.CreateFont(13, FontStyle.Bold),
				FontMono = MonoFont(), FontTitle = Theme.CreateFont(20, FontStyle.Bold);

			List<StartUp.LogLine> lines = new List<StartUp.LogLine>();
			readonly bool[] show = { true, true, true };   // info, warning, error
			string filter = "";
			bool paused;
			int scroll;          // first visible row
			int hoverRow = -1;
			object hoverButton;
			const int RowH = 24, HeaderH = 112;
			Rectangle listArea, thumb;
			bool dragThumb; int dragOffset;
			readonly List<(Rectangle rect, object id)> buttons = new List<(Rectangle, object)>();

			public DebugView(FormDebugModern f)
			{
				form = f;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
			}

			protected override void OnResize(EventArgs e)
			{
				base.OnResize(e);
				if (!paused) scroll = int.MaxValue;   // keep the newest lines in view
				Reload();
			}

			public string Filter { set { filter = value ?? ""; Reload(); } }

			static Font MonoFont()
			{
				foreach (var name in new[] { "Cascadia Mono", "Consolas" })
					using (var test = new Font(name, 12, GraphicsUnit.Pixel))
						if (string.Equals(test.Name, name, StringComparison.OrdinalIgnoreCase)) return new Font(name, 12.5f, GraphicsUnit.Pixel);
				return new Font(FontFamily.GenericMonospace, 12.5f, GraphicsUnit.Pixel);
			}

			/// <summary>Where the rows go (known before the first paint, so the view starts at the newest line).</summary>
			Rectangle ListRect => Rectangle.Inflate(new Rectangle(12, HeaderH, Math.Max(0, Width - 24), Math.Max(0, Height - HeaderH - 12)), -6, -6);

			bool AtBottom => scroll >= Math.Max(0, lines.Count - VisibleRows) - 1;
			int VisibleRows => Math.Max(1, ListRect.Height / RowH);

			public void Reload()
			{
				bool follow = !paused && AtBottom;
				lines = StartUp.LogSnapshot().Where(l => show[(int)l.Type - 1] && (filter.Length == 0 || l.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
				int max = Math.Max(0, lines.Count - VisibleRows);
				if (follow) scroll = max;
				scroll = Math.Max(0, Math.Min(scroll, max));
				Invalidate();
			}

			static Color LevelColor(StartUp.DEBUG_TYPE t) =>
				t == StartUp.DEBUG_TYPE.error ? Theme.Danger : t == StartUp.DEBUG_TYPE.warning ? Color.FromArgb(214, 150, 20) : Theme.TextMuted;

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				buttons.Clear();
				var all = StartUp.LogSnapshot();

				// --- title and what is on screen
				Theme.DrawText(g, Lang.T("debug.title"), FontTitle, new Rectangle(20, 12, Width - 40, 30), Theme.Text, TextFormatFlags.SingleLine);
				string pet = Animations.Xml?.AnimationXML?.Header?.Petname ?? "";
				Theme.DrawText(g, Lang.T("debug.subtitle", pet, Program.Mainthread?.PetCount ?? 0, all.Count), FontSmall,
					new Rectangle(20, 42, Width - 40, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

				// --- level chips with counts
				int x = 20, y = 70;
				string[] keys = { "debug.info", "debug.warnings", "debug.errors" };
				for (int i = 0; i < 3; i++)
				{
					int n = all.Count(l => (int)l.Type == i + 1);
					string text = Lang.T(keys[i]) + "  " + n;
					int w = Theme.MeasureText(g, text, FontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 34;
					var chip = new Rectangle(x, y, w, 28);
					bool on = show[i];
					Theme.FillRounded(g, on ? Theme.AccentSoft : Equals(hoverButton, i) ? Theme.Hover : Theme.Surface, chip, 14);
					Theme.DrawRounded(g, on ? Theme.Accent : Theme.Border, chip, 14);
					using (var b = new SolidBrush(LevelColor((StartUp.DEBUG_TYPE)(i + 1)))) g.FillEllipse(b, chip.X + 12, chip.Y + 10, 8, 8);
					Theme.DrawText(g, text, FontSmall, new Rectangle(chip.X + 24, chip.Y, chip.Width - 28, chip.Height), on ? Theme.Accent : Theme.Text,
						TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					buttons.Add((chip, i));
					x += w + 6;
				}

				// --- actions on the right, search between
				var actions = new (string id, string key)[] { ("pause", paused ? "debug.resume" : "debug.pause"), ("copy", "debug.copy"), ("clear", "debug.clear"), ("xml", "debug.xml"), ("dot", "debug.dot") };
				int ax = Width - 20;
				for (int i = actions.Length - 1; i >= 0; i--)
				{
					string text = Lang.T(actions[i].key);
					int w = Theme.MeasureText(g, text, FontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 22;
					ax -= w;
					var r = new Rectangle(ax, y, w, 28);
					Theme.DrawButton(g, r, text, FontSmall, actions[i].id == "pause" && paused, Equals(hoverButton, actions[i].id));
					buttons.Add((r, actions[i].id));
					ax -= 6;
				}
				var searchBox = new Rectangle(x + 8, y, Math.Max(80, ax - x - 14), 28);
				Theme.FillRounded(g, Theme.Surface, searchBox, 6);
				Theme.DrawRounded(g, Theme.Border, searchBox, 6);
				Theme.DrawText(g, "⌕", FontText, new Rectangle(searchBox.X + 8, searchBox.Y, 20, searchBox.Height), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				form.PlaceSearch(searchBox);

				// --- the log
				var card = new Rectangle(12, HeaderH, Width - 24, Height - HeaderH - 12);
				Theme.FillRounded(g, Theme.Surface, card, 10);
				Theme.DrawRounded(g, Theme.Border, card, 10);
				listArea = Rectangle.Inflate(card, -6, -6);
				if (lines.Count == 0)
				{
					Theme.DrawText(g, Lang.T("debug.empty"), FontText, listArea, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
					return;
				}
				var clip = g.Clip;
				g.SetClip(listArea);
				const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPrefix;
				int rows = VisibleRows + 1;
				int timeW = TextRenderer.MeasureText(g, "00:00:00.000", FontMono, Size.Empty, TextFormatFlags.NoPadding).Width + 16;
				for (int i = 0; i < rows && scroll + i < lines.Count; i++)
				{
					var l = lines[scroll + i];
					var rr = new Rectangle(listArea.X, listArea.Y + i * RowH, listArea.Width - 12, RowH);
					if (scroll + i == hoverRow) Theme.FillRounded(g, Theme.Hover, rr, 4);
					else if (l.Type != StartUp.DEBUG_TYPE.info)
						using (var b = new SolidBrush(Color.FromArgb(Theme.IsDark ? 28 : 18, LevelColor(l.Type)))) g.FillRectangle(b, rr);
					Theme.DrawText(g, l.Time.ToString("HH:mm:ss.fff"), FontMono, new Rectangle(rr.X + 8, rr.Y, timeW, rr.Height), Theme.TextMuted, Line);
					using (var b = new SolidBrush(LevelColor(l.Type))) g.FillRectangle(b, rr.X + 12 + timeW, rr.Y + 5, 3, RowH - 10);
					Theme.DrawText(g, l.Text, FontMono, new Rectangle(rr.X + 22 + timeW, rr.Y, rr.Width - 26 - timeW, rr.Height),
						l.Type == StartUp.DEBUG_TYPE.info ? Theme.Text : LevelColor(l.Type), Line);
				}
				g.Clip = clip;

				// --- scroll thumb
				thumb = Rectangle.Empty;
				int max = lines.Count - VisibleRows;
				if (max > 0)
				{
					int th = Math.Max(30, listArea.Height * VisibleRows / lines.Count);
					int ty = listArea.Y + (int)((listArea.Height - th) * (scroll / (double)max));
					thumb = new Rectangle(listArea.Right - 7, ty, 6, th);
					Theme.FillRounded(g, dragThumb ? Theme.TextMuted : Theme.Border, thumb, 3);
				}
			}

			int RowAt(Point p) => listArea.Contains(p) && p.X < listArea.Right - 12 ? scroll + (p.Y - listArea.Y) / RowH : -1;

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				scroll = Math.Max(0, Math.Min(Math.Max(0, lines.Count - VisibleRows), scroll - e.Delta / 120 * 3));
				Invalidate();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				if (dragThumb)
				{
					int max = Math.Max(0, lines.Count - VisibleRows);
					double pct = (e.Y - dragOffset - listArea.Y) / (double)Math.Max(1, listArea.Height - thumb.Height);
					scroll = Math.Max(0, Math.Min(max, (int)Math.Round(pct * max)));
					Invalidate();
					return;
				}
				var hb = buttons.FirstOrDefault(b => b.rect.Contains(e.Location)).id;
				int hr = RowAt(e.Location);
				if (hr >= lines.Count) hr = -1;
				Cursor = hb != null ? Cursors.Hand : Cursors.Default;
				if (!Equals(hb, hoverButton) || hr != hoverRow) { hoverButton = hb; hoverRow = hr; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverButton = null; hoverRow = -1; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (!thumb.IsEmpty && e.X >= thumb.X - 4 && listArea.Contains(e.Location))
				{
					dragThumb = true; dragOffset = thumb.Contains(e.Location) ? e.Y - thumb.Y : thumb.Height / 2; Capture = true;
				}
			}

			protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (dragThumb) { dragThumb = false; Capture = false; Invalidate(); } }

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				var hit = buttons.FirstOrDefault(b => b.rect.Contains(e.Location)).id;
				switch (hit)
				{
					case int level: show[level] = !show[level]; Reload(); break;
					case "pause": paused = !paused; if (!paused) scroll = int.MaxValue; Reload(); break;
					case "copy": Copy(lines); break;
					case "clear": StartUp.ClearLog(); Reload(); break;
					case "xml": OpenInNotepad(Animations.Xml?.AnimationXMLString); break;
					case "dot": try { OpenInNotepad(Tools.XmlToDot.ProcessXml(Animations.Xml.AnimationXML)); } catch (Exception skipped) { LogFile.Ignored(skipped); } break;
				}
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);
				int r = RowAt(e.Location);
				if (r >= 0 && r < lines.Count) Copy(new List<StartUp.LogLine> { lines[r] });
			}

			static void Copy(List<StartUp.LogLine> list)
			{
				if (list.Count == 0) return;
				try { Clipboard.SetText(string.Join(Environment.NewLine, list.Select(l => $"{l.Time:HH:mm:ss.fff} [{l.Type}] {l.Text}"))); } catch (Exception skipped) { LogFile.Ignored(skipped); }
			}

			/// <summary>Show a text in Notepad (a temporary file: no typing into another program's window).</summary>
			static void OpenInNotepad(string text)
			{
				if (string.IsNullOrEmpty(text)) return;
				try
				{
					string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopPet", "debug-" + DateTime.Now.ToString("HHmmss") + ".txt");
					System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
					System.IO.File.WriteAllText(file, text);
					Process.Start(new ProcessStartInfo("notepad.exe", "\"" + file + "\"") { UseShellExecute = false });
				}
				catch (Exception skipped) { LogFile.Ignored(skipped); }
			}
		}
	}
}
