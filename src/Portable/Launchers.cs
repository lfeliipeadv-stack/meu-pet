using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>A shortcut of the pet menu: a folder, a program/file or a website.</summary>
	public sealed class Launcher
	{
		public string Name { get; set; } = "";
		public string Target { get; set; } = "";
	}

	/// <summary>Shortcuts kept in %LocalAppData%\DesktopPet\launchers.json and opened from the pet menu.</summary>
	public static class Launchers
	{
		internal static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "launchers.json");
		static List<Launcher> list;
		internal static void Reload() => list = null;

		public static List<Launcher> All
		{
			get
			{
				if (list != null) return list;
				try { list = File.Exists(FilePath) ? JsonText.Deserialize<List<Launcher>>(File.ReadAllText(FilePath)) : null; } catch (Exception skipped) { LogFile.Ignored(skipped); }
				return list ?? (list = new List<Launcher>());
			}
		}

		public static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonText.SerializeIndented(All));
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "launchers: " + ex.Message); }
		}

		/// <summary>Open it: folders in Explorer, files with their program, only https links in the browser.</summary>
		public static void Open(Launcher l)
		{
			try
			{
				string t = l.Target ?? "";
				if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				{
					if (!Program.OpenLink(t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + t.Substring(7) : t))
						throw new Exception(Lang.T("launch.badLink"));
				}
				else if (Directory.Exists(t) || File.Exists(t))
					Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
				else throw new FileNotFoundException(Lang.T("launch.missing"));
				SpeechBubble.Say(Lang.T("launch.opening", l.Name), 4);
			}
			catch (Exception ex) { SpeechBubble.Say(Lang.T("launch.error", l.Name, ex.Message), 8); }
		}
	}

	/// <summary>Add and remove shortcuts: a list of cards (folder, program, site) with ✕ to remove.</summary>
	internal sealed class FormLaunchers : ThemedDialog
	{
		readonly ShortcutList list;
		const int W = 560;

		public FormLaunchers()
		{
			Text = Lang.T("launch.title");
			iconEmoji = "🚀";
			var close = AddButton(Lang.T("launch.close"), DialogResult.OK, true);
			CancelButton = close;
			list = new ShortcutList(this) { Location = new Point(Pad, 104), Size = new Size(W - Pad * 2, 250) };
			Controls.Add(list);
			var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Location = new Point(Pad - 3, list.Bottom + 12), MaximumSize = new Size(W - Pad * 2 + 6, 0) };
			Button B(string text, EventHandler h) { var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 36), Font = fSmall, Margin = new Padding(3, 3, 6, 3) }; b.Click += h; row.Controls.Add(b); return b; }
			B("＋ 📁 " + Lang.T("launch.addFolder"), (s, e) =>
			{
				using (var d = new FolderBrowserDialog()) if (d.ShowDialog(this) == DialogResult.OK) Add(Path.GetFileName(d.SelectedPath.TrimEnd('\\')), d.SelectedPath);
			});
			B("＋ 📄 " + Lang.T("launch.addFile"), (s, e) =>
			{
				using (var d = new OpenFileDialog()) if (d.ShowDialog(this) == DialogResult.OK) Add(Path.GetFileNameWithoutExtension(d.FileName), d.FileName);
			});
			B("＋ 🌐 " + Lang.T("launch.addSite"), (s, e) =>
			{
				string url = Prompt(Lang.T("launch.siteAsk"), "https://");
				if (string.IsNullOrWhiteSpace(url) || url == "https://") return;
				if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
				Add(Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url, url);
			});
			Controls.Add(row);
			close.Font = fText;
			ClientSize = new Size(W, 100);
			ApplyTheme(close);
			row.PerformLayout();
			int bottom = LayoutButtons(row.Bottom + 26, W);
			ClientSize = new Size(W, bottom + Pad);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			PaintIcon(g);
			Theme.DrawText(g, Lang.T("launch.title"), fTitle, new Rectangle(Pad + 64, Pad - 2, W - Pad * 2 - 64, 30), Theme.Text, TextFormatFlags.SingleLine);
			Theme.DrawText(g, Lang.T("launch.intro"), fSmall, new Rectangle(Pad + 64, Pad + 30, W - Pad * 2 - 64, 40), Theme.TextMuted, TextFormatFlags.WordBreak);
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Pad, buttons[0].Top - 12, W - Pad, buttons[0].Top - 12);
		}

		void Add(string name, string target)
		{
			name = Prompt(Lang.T("launch.nameAsk"), name);
			if (string.IsNullOrWhiteSpace(name)) return;
			Launchers.All.Add(new Launcher { Name = name.Trim(), Target = target });
			Launchers.Save();
			list.Invalidate();
		}

		/// <summary>The shortcuts as rows: icon by kind, name, where it goes, ✕ to remove; a friendly line when empty.</summary>
		sealed class ShortcutList : Control
		{
			readonly FormLaunchers form;
			int scroll, hoverX = -1;
			readonly List<Rectangle> removes = new List<Rectangle>();

			public ShortcutList(FormLaunchers f)
			{
				form = f;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			}

			static string Kind(Launcher l) =>
				l.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "🌐" : Directory.Exists(l.Target) ? "📁" : "📄";

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				removes.Clear();
				var r0 = new Rectangle(0, 0, Width - 1, Height - 1);
				Theme.FillRounded(g, Theme.Surface, r0, 12);
				Theme.DrawRounded(g, Theme.Border, r0, 12);
				if (Launchers.All.Count == 0)
				{
					Theme.DrawText(g, "🚀", form.fIcon, new Rectangle(0, Height / 2 - 50, Width, 36), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
					Theme.DrawText(g, Lang.T("launch.empty"), form.fText, new Rectangle(24, Height / 2 - 6, Width - 48, 50), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
					return;
				}
				int rowH = 52, max = Math.Max(0, Launchers.All.Count * rowH + 12 - Height);
				scroll = Math.Max(0, Math.Min(max, scroll));
				g.SetClip(new Rectangle(1, 1, Width - 2, Height - 2));
				for (int i = 0; i < Launchers.All.Count; i++)
				{
					var l = Launchers.All[i];
					var r = new Rectangle(8, 6 + i * rowH - scroll, Width - 16, rowH - 6);
					if (i % 2 == 1) Theme.FillRounded(g, Theme.SurfaceAlt, r, 8);
					Theme.DrawText(g, Kind(l), form.fText, new Rectangle(r.X + 8, r.Y, 30, r.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
					Theme.DrawText(g, l.Name, form.fText, new Rectangle(r.X + 44, r.Y + 4, r.Width - 90, 20), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, l.Target, form.fSmall, new Rectangle(r.X + 44, r.Y + 24, r.Width - 90, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
					var x = new Rectangle(r.Right - 36, r.Y + (r.Height - 28) / 2, 28, 28);
					if (hoverX == i) Theme.FillRounded(g, Color.FromArgb(40, Theme.Danger), x, 8);
					Theme.DrawText(g, "✕", form.fText, x, hoverX == i ? Theme.Danger : Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					removes.Add(x);
				}
			}

			protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); scroll -= e.Delta / 120 * 40; Invalidate(); }

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				int h = removes.FindIndex(r => r.Contains(e.Location));
				Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
				if (h != hoverX) { hoverX = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverX = -1; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				int i = removes.FindIndex(r => r.Contains(e.Location));
				if (i < 0 || i >= Launchers.All.Count) return;
				Launchers.All.RemoveAt(i);
				Launchers.Save();
				hoverX = -1;
				Invalidate();
			}
		}

		/// <summary>A one-line question.</summary>
		internal static string Prompt(string question, string value)
		{
			return PromptDialog.Ask(question, value);   // the program's own window (Dialogs.cs)
		}
	}
}
