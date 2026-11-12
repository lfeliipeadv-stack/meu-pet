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
				try { list = File.Exists(FilePath) ? JsonText.Deserialize<List<Launcher>>(File.ReadAllText(FilePath)) : null; } catch (Exception) { }
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

	/// <summary>Add and remove shortcuts.</summary>
	internal sealed class FormLaunchers : PetDialog
	{
		readonly ListBox items = new ListBox { Width = 420, Height = 200, IntegralHeight = false };

		public FormLaunchers()
		{
			Text = Lang.T("launch.title");
			Icon = Properties.Resources.icon;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Padding = new Padding(16);

			var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
			root.Controls.Add(new Label { Text = Lang.T("launch.intro"), AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 8) });
			items.DisplayMember = "Name";
			items.FormattingEnabled = true;
			items.Format += (s, e) => { if (e.ListItem is Launcher l) e.Value = l.Name + "   —   " + l.Target; };
			root.Controls.Add(items);
			var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
			Button B(string key, EventHandler h) { var b = new Button { Text = Lang.T(key), AutoSize = true, MinimumSize = new Size(0, 32) }; b.Click += h; row.Controls.Add(b); return b; }
			B("launch.addFolder", (s, e) =>
			{
				using (var d = new FolderBrowserDialog()) if (d.ShowDialog(this) == DialogResult.OK) Add(Path.GetFileName(d.SelectedPath.TrimEnd('\\')), d.SelectedPath);
			});
			B("launch.addFile", (s, e) =>
			{
				using (var d = new OpenFileDialog()) if (d.ShowDialog(this) == DialogResult.OK) Add(Path.GetFileNameWithoutExtension(d.FileName), d.FileName);
			});
			B("launch.addSite", (s, e) =>
			{
				string url = Prompt(Lang.T("launch.siteAsk"), "https://");
				if (string.IsNullOrWhiteSpace(url) || url == "https://") return;
				if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
				Add(Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url, url);
			});
			B("launch.remove", (s, e) =>
			{
				if (items.SelectedItem is Launcher l) { Launchers.All.Remove(l); Launchers.Save(); Fill(); }
			});
			root.Controls.Add(row);
			var close = new Button { Text = Lang.T("launch.close"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(110, 32), Margin = new Padding(0, 14, 0, 0) };
			root.Controls.Add(close);
			AcceptButton = close; CancelButton = close;
			Controls.Add(root);
			Fill();
			Theme.ApplyTo(this);
			Theme.MakePrimary(close);
			items.BackColor = Theme.Surface; items.ForeColor = Theme.Text; items.BorderStyle = BorderStyle.FixedSingle;
		}

		void Add(string name, string target)
		{
			name = Prompt(Lang.T("launch.nameAsk"), name);
			if (string.IsNullOrWhiteSpace(name)) return;
			Launchers.All.Add(new Launcher { Name = name.Trim(), Target = target });
			Launchers.Save();
			Fill();
		}

		void Fill()
		{
			items.Items.Clear();
			foreach (var l in Launchers.All) items.Items.Add(l);
		}

		/// <summary>A one-line question.</summary>
		internal static string Prompt(string question, string value)
		{
			using (var f = new PetDialog
			{
				Text = "desktopPet", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
				ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16),
				Icon = Properties.Resources.icon,
			})
			{
				var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
				p.Controls.Add(new Label { Text = question, AutoSize = true });
				var t = new TextBox { Text = value ?? "", Width = 340 };
				p.Controls.Add(t);
				var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, MinimumSize = new Size(90, 30), AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
				p.Controls.Add(ok);
				f.Controls.Add(p);
				f.AcceptButton = ok;
				Theme.ApplyTo(f);
				Theme.MakePrimary(ok);
				return f.ShowDialog() == DialogResult.OK ? t.Text : null;
			}
		}
	}
}
