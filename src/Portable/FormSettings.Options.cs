using DesktopPet.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static DesktopPet.LocalData;

namespace DesktopPet
{
	/// <summary>Behaviour and Settings pages: the option builders and the options list.</summary>
	public sealed partial class FormSettings
	{
		// ------------------------------------------------------------------ options

		private void BuildBehaviourOptions()
		{
			// Titles and descriptions are translation keys, resolved at paint time (instant language switch).
			behaviourView.AddToggle("", /* monitor */ "opt.multiscreen", SettingName.Multiscreen);
			behaviourView.AddToggle("", /* stopwatch */ "opt.smooth", SettingName.SmoothMovements);
			behaviourView.AddToggle("", /* taskbar */ "opt.taskbar", SettingName.StealTaskbarFocus);
			behaviourView.AddToggle("", /* window to front */ "opt.foreground", SettingName.WinForeground);
			behaviourView.AddSlider("", /* volume */ "opt.volume", SettingName.Volume, 0, 100, v => Lang.T("opt.volume.value", v));
		}

		private void BuildApplicationOptions()
		{
			// Language: Auto + every language file found in the lang folder.
			var languages = Lang.Available();
			var codes = new List<string> { "auto" };
			codes.AddRange(languages.Select(l => l.Code));
			applicationView.AddDropDown("", /* globe */ "opt.language",
				() => codes.Select(c => c == "auto" ? Lang.T("opt.language.auto") : languages.First(l => l.Code == c).Name).ToArray(),
				() => Math.Max(0, codes.FindIndex(c => string.Equals(c, Program.MyData.GetLanguage(), StringComparison.OrdinalIgnoreCase))),
				i =>
				{
					Program.MyData.SetLanguage(codes[i]);
					Lang.Use(codes[i]);
				});

			// Theme: automatic (Windows), light or dark — applied at once to every window.
			var themeModes = new[] { "auto", "light", "dark" };
			applicationView.AddChoice("", /* color palette */ "opt.theme",
				() => Math.Max(0, Array.IndexOf(themeModes, Theme.Mode)),
				i =>
				{
					Program.MyData.SetTheme(themeModes[i]);
					Theme.Mode = themeModes[i];
				},
				new[] { 0, 1, 2 }, new[] { "opt.theme.auto", "opt.theme.light", "opt.theme.dark" });

			applicationView.AddToggle("", /* computer */ "opt.startup", () => WindowsStartup.Enabled, on =>
			{
				if (!WindowsStartup.Set(on)) StatusText = Lang.T("status.startupError");
			});
			applicationView.AddSlider("", /* start */ "opt.quantity", SettingName.AutostartPets, 0, StartUp.MAX_SHEEPS,
				v => v == 1 ? Lang.T("opt.quantity.one") : Lang.T("opt.quantity.many", v));
			applicationView.AddChoice("", /* size */ "opt.scale",
				() => Program.MyData.GetScale(),
				v => { Program.MyData.SetScale(v); Program.RestartApp(); },
				new[] { 1, 2, 4 }, new[] { "1×", "2×", "4×" }, confirmRestart: true);
			applicationView.AddAction("", /* undo */ "opt.restore", "opt.restore.button", () =>
			{
				if (PetMessage.Show(this, Lang.T("dlg.restore.text"), Lang.T("dlg.restore.title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "restoring default XML");
				Program.MyData.SetIcon("");
				Program.MyData.SetImages("");
				Program.MyData.SetXml(Resources.animations, "esheep64");
				Program.Mainthread.LoadNewXMLFromString(Resources.animations);
				StatusText = Lang.T("status.restored");
				// Clear saved pet folder so gallery badge resets.
				Program.MyData.SetCurrentPetFolder("");
				petsView.CurrentPet = "";
				petsView.Invalidate();
			});
			applicationView.AddToggle("", /* keyboard */ "opt.hotkey", () => Program.MyData.GetHotkeyEnabled(), on =>
			{
				Program.MyData.SetHotkeyEnabled(on);
				if (!HideHotkey.Apply()) StatusText = Lang.T("status.hotkeyBusy", HideHotkey.Text);
				ContextMenus.UpdateTexts();
			});
			applicationView.AddAction("", /* export */ "opt.export", "opt.export.button", ExportPets);
			applicationView.AddAction("", /* import */ "opt.import", "opt.import.button", ImportPets);
			applicationView.AddAction("", /* create */ "opt.create", "opt.create.button", CreatePet);
			applicationView.AddAction("", /* bug */ "opt.debug", "opt.debug.button", FormDebugModern.ShowWindow);
			applicationView.AddAction("", /* tour */ "opt.tour", "opt.tour.button", Onboarding.Tour);
			applicationView.AddAction("", /* folder */ "opt.logs", "opt.logs.button", () =>
			{
				System.IO.Directory.CreateDirectory(LogFile.Folder);
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + LogFile.Folder + "\"") { UseShellExecute = false });
			});
#if PORTABLE
			applicationView.AddAction("", /* download */ Program.IsApplicationInstalled() ? "opt.install.titleInstalled" : "opt.install", "opt.install.button",
				() => ContextMenus.OpenInstaller(), "opt.install.desc");
#endif
		}


		// ================================================================== Options list

		private sealed class OptionsView : ScrollSurface
		{
			private abstract class Row
			{
				public string Glyph;   // Segoe Fluent Icons code point
				/// <summary>Translation key: "{Key}.title" and "{Key}.desc" are shown.</summary>
				public string Key;
				/// <summary>Optional key overriding the description key.</summary>
				public string DescKey;
				public string Title => Lang.T(Key.EndsWith(".titleInstalled") ? Key : Key + ".title");
				/// <summary>Optional: a description that changes (a status), instead of the translation key.</summary>
				public Func<string> DescFunc;
				public string Description => DescFunc?.Invoke() ?? Lang.T(DescKey ?? Key + ".desc");
				public Rectangle Rect, Control;
			}
			private sealed class ToggleRow : Row
			{
				public string Setting;
				/// <summary>Optional: a toggle that is not a LocalData setting (e.g. start with Windows).</summary>
				public Func<bool> Get; public Action<bool> Set;
				public bool On => Get != null ? Get() : Program.MyData.GetIntValue(Setting) > 0;
			}
			private sealed class SliderRow : Row { public string Setting; public int Min, Max, Value; public Func<int, string> Format; }
			private sealed class ChoiceRow : Row { public bool ConfirmRestart; public Func<int> Get; public Action<int> Set; public int[] Values; public string[] Texts; public Rectangle[] Segments; }
			private sealed class ActionRow : Row { public string ButtonKey; public Action Run; }
			private sealed class DropDownRow : Row { public Func<string[]> Items; public Func<int> Get; public Action<int> Set; }
			/// <summary>A section title between rows ("Pet", "Program").</summary>
			private sealed class HeaderRow : Row { }
			/// <summary>A row with a badge and several buttons (a configured AI provider: Use, Edit, Remove).</summary>
			private sealed class ButtonsRow : Row { public string Badge; public string TitleText; public List<(string text, Action run)> Buttons = new List<(string, Action)>(); public List<Rectangle> Hits = new List<Rectangle>(); }

			private readonly FormSettings form;
			private readonly List<Row> rows = new List<Row>();
			private SliderRow dragging;
			private object hover;
			private int hoverSegment = -1;

			public OptionsView(FormSettings f) { form = f; }

			/// <summary>Windows 11 icon font (Segoe Fluent Icons, or Segoe MDL2 Assets on Windows 10).</summary>
			private Font glyphFont;
			private Font GlyphFont => glyphFont ?? (glyphFont = new Font(new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(n => { using (var f = new Font(n, 10)) return string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase); }) ?? Theme.FontFamilyName, 20, GraphicsUnit.Pixel));

			public void AddToggle(string glyph, string key, string setting) =>
				rows.Add(new ToggleRow { Glyph = glyph, Key = key, Setting = setting });

			public void AddToggle(string glyph, string key, Func<bool> get, Action<bool> set) =>
				rows.Add(new ToggleRow { Glyph = glyph, Key = key, Get = get, Set = set });

			public void AddSlider(string glyph, string key, string setting, int min, int max, Func<int, string> format) =>
				rows.Add(new SliderRow { Glyph = glyph, Key = key, Setting = setting, Min = min, Max = max, Format = format, Value = Program.MyData.GetIntValue(setting) });

			public void AddChoice(string glyph, string key, Func<int> get, Action<int> set, int[] values, string[] texts, bool confirmRestart = false) =>
				rows.Add(new ChoiceRow { Glyph = glyph, ConfirmRestart = confirmRestart, Key = key, Get = get, Set = set, Values = values, Texts = texts });

			/// <summary>A button whose description is a live status ("found at…", "listening on port…").</summary>
			public void AddStatus(string glyph, string key, Func<string> status, string buttonKey, Action run) =>
				rows.Add(new ActionRow { Glyph = glyph, Key = key, ButtonKey = buttonKey, Run = run, DescFunc = status });

			public void AddAction(string glyph, string key, string buttonKey, Action run, string descKey = null) =>
				rows.Add(new ActionRow { Glyph = glyph, Key = key, ButtonKey = buttonKey, Run = run, DescKey = descKey });

			public void AddHeader(string key) => rows.Add(new HeaderRow { Key = key });
			public void AddHeader(Func<string> text) => rows.Add(new HeaderRow { Key = "", DescFunc = text });

			/// <summary>Start again (the rows are built anew, e.g. after a provider was added).</summary>
			public void Clear() { rows.Clear(); hover = null; Invalidate(); }

			public void AddButtons(string glyph, string title, string description, string badge, params (string text, Action run)[] buttons) =>
				rows.Add(new ButtonsRow { Glyph = glyph, Key = "", TitleText = title, DescFunc = () => description, Badge = badge, Buttons = buttons.ToList() });

			/// <summary>Rows without the section titles (the size of a small options window).</summary>
			public int RowCount => rows.Count(r => !(r is HeaderRow));

			public void AddDropDown(string glyph, string key, Func<string[]> items, Func<int> get, Action<int> set) =>
				rows.Add(new DropDownRow { Glyph = glyph, Key = key, Items = items, Get = get, Set = set });

			protected override int PaintContent(Graphics g, int scrollY)
			{
				int y = 8 - scrollY;
				int w = Width - ScrollBarWidth - 16;
				foreach (var row in rows)
				{
					if (row is HeaderRow)
					{
						y += row == rows[0] ? 0 : 14;
						row.Rect = Rectangle.Empty;
						Theme.DrawText(g, (row.DescFunc?.Invoke() ?? Lang.T(row.Key)).ToUpperInvariant(), form.fontSmall, new Rectangle(14, y, w, 22), Theme.Accent, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
						y += 30;
						continue;
					}
					// segments of a choice grow with their text ("1×" … "Automático")
					int segW = row is ChoiceRow crw ? Math.Max(50, crw.Texts.Max(t => Theme.MeasureText(g, Lang.T(t), form.fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width) + 22) : 0;
					int controlW = row is SliderRow ? 260 : row is ChoiceRow cr ? cr.Values.Length * (segW + 6) : row is ActionRow ? 120 : row is DropDownRow ? 220 : row is ButtonsRow br ? br.Buttons.Sum(b => Theme.MeasureText(g, b.text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 34) : 130;
					int textW = w - controlW - 110;
					// a wide choice in a narrow window: under the text instead of squeezing it
					bool stacked = row is ChoiceRow && textW < 220;
					if (stacked) textW = w - 110;
					var descSize = Theme.MeasureText(g, row.Description, form.fontSmall, textW);
					int h = Math.Max(76, 40 + descSize.Height + 16) + (stacked ? 46 : 0);
					row.Rect = new Rectangle(8, y, w, h);
					bool isHover = hover == row;
					Theme.FillRounded(g, isHover ? Theme.SurfaceAlt : Theme.Surface, row.Rect, 10);
					Theme.DrawRounded(g, Theme.Border, row.Rect, 10);

					var iconBack = new Rectangle(row.Rect.X + 16, row.Rect.Y + (h - 44) / 2, 44, 44);
					Theme.FillRounded(g, Theme.AccentSoft, iconBack, 10);
					if (!string.IsNullOrEmpty(row.Glyph))
						TextRenderer.DrawText(g, row.Glyph, GlyphFont, iconBack, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

					int tx = row.Rect.X + 76;
					string title = row is ButtonsRow tb ? tb.TitleText : row.Title;
					Theme.DrawText(g, title, form.fontBold, new Rectangle(tx, row.Rect.Y + 16, textW, 22), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					if (row is ButtonsRow bb && !string.IsNullOrEmpty(bb.Badge))
					{
						int tw2 = Math.Min(textW, Theme.MeasureText(g, title, form.fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width);
						int bw2 = Theme.MeasureText(g, bb.Badge, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 16;
						var badge = new Rectangle(tx + tw2 + 8, row.Rect.Y + 16, bw2, 22);
						Theme.FillRounded(g, Theme.AccentSoft, badge, 11);
						Theme.DrawText(g, bb.Badge, form.fontSmall, badge, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					}
					Theme.DrawText(g, row.Description, form.fontSmall, new Rectangle(tx, row.Rect.Y + 40, textW, descSize.Height), Theme.TextMuted);

					int cx = row.Rect.Right - controlW - 20;
					int cy = row.Rect.Y + h / 2;
					switch (row)
					{
						case ToggleRow t:
							t.Control = new Rectangle(row.Rect.Right - 20 - 44, cy - 11, 44, 22);
							bool on = t.On;
							Theme.DrawToggle(g, t.Control, on);
							Theme.DrawText(g, on ? Lang.T("opt.on") : Lang.T("opt.off"), form.fontSmall, new Rectangle(t.Control.X - 90, cy - 9, 82, 18), Theme.TextMuted, TextFormatFlags.Right | TextFormatFlags.SingleLine);
							break;
						case SliderRow s:
							s.Control = new Rectangle(cx, cy - 12, controlW - 70, 24);
							Theme.DrawSlider(g, s.Control, s.Value - s.Min, s.Max - s.Min);
							Theme.DrawText(g, s.Format(s.Value), form.fontBold, new Rectangle(s.Control.Right + 12, cy - 10, 62, 20), Theme.Text, TextFormatFlags.Right | TextFormatFlags.SingleLine);
							break;
						case ChoiceRow c:
							int current = c.Get();
							c.Control = stacked ? new Rectangle(row.Rect.X + 76, row.Rect.Bottom - 48, Math.Min(controlW, row.Rect.Width - 96), 32) : new Rectangle(row.Rect.Right - 20 - controlW, cy - 16, controlW, 32);
							Theme.FillRounded(g, Theme.SurfaceAlt, c.Control, 8);
							Theme.DrawRounded(g, Theme.Border, c.Control, 8);
							c.Segments = new Rectangle[c.Values.Length];
							for (int i = 0; i < c.Values.Length; i++)
							{
								var seg = new Rectangle(c.Control.X + 3 + i * (segW + 6), c.Control.Y + 3, segW, 26);
								c.Segments[i] = seg;
								bool sel = c.Values[i] == current;
								if (sel) Theme.FillRounded(g, Theme.Accent, seg, 6);
								else if (hover == c && hoverSegment == i) Theme.FillRounded(g, Theme.Hover, seg, 6);
								// texts may be translation keys (instant language switch); plain texts pass through
								Theme.DrawText(g, Lang.T(c.Texts[i]), form.fontBold, seg, sel ? Theme.AccentText : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
							}
							break;
						case ActionRow a:
							a.Control = new Rectangle(row.Rect.Right - 20 - controlW, cy - 17, controlW, 34);
							Theme.DrawButton(g, a.Control, Lang.T(a.ButtonKey), form.fontText, false, hover == a && a.Control.Contains(PointToClient(MousePosition)));
							break;
						case ButtonsRow br2:
							br2.Hits.Clear();
							int bxx = row.Rect.Right - 20;
							foreach (var (text, _) in Enumerable.Reverse(br2.Buttons))
							{
								int bw3 = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 26;
								var br3 = new Rectangle(bxx - bw3, cy - 16, bw3, 32);
								br2.Hits.Insert(0, br3);
								Theme.DrawButton(g, br3, text, form.fontSmall, false, hover == br2 && br3.Contains(PointToClient(MousePosition)));
								bxx -= bw3 + 8;
							}
							break;
						case DropDownRow d:
							d.Control = new Rectangle(row.Rect.Right - 20 - controlW, cy - 17, controlW, 34);
							var items = d.Items();
							int idx = Math.Max(0, Math.Min(items.Length - 1, d.Get()));
							bool hot = hover == d && d.Control.Contains(PointToClient(MousePosition));
							Theme.FillRounded(g, hot ? Theme.Hover : Theme.Surface, d.Control, 6);
							Theme.DrawRounded(g, Theme.Border, d.Control, 6);
							Theme.DrawText(g, items.Length > 0 ? items[idx] : "", form.fontText, new Rectangle(d.Control.X + 12, d.Control.Y, d.Control.Width - 40, d.Control.Height), Theme.Text,
								TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
							Theme.DrawText(g, "▾", form.fontText, new Rectangle(d.Control.Right - 28, d.Control.Y, 20, d.Control.Height), Theme.TextMuted,
								TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
							break;
					}
					y += h + 10;
				}
				return y + scrollY;
			}

			/// <summary>Open a themed menu under the drop-down to pick an item.</summary>
			private void OpenDropDown(DropDownRow d)
			{
				var menu = new ContextMenuStrip();
				var items = d.Items();
				int current = d.Get();
				for (int i = 0; i < items.Length; i++)
				{
					int index = i;
					var item = new ToolStripMenuItem(items[i]) { Checked = i == current };
					item.Click += (s, e) => { d.Set(index); Invalidate(); };
					menu.Items.Add(item);
				}
				Theme.ApplyTo(menu);
				menu.MinimumSize = new Size(d.Control.Width, 0);
				menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
				menu.Show(this, new Point(d.Control.X, d.Control.Bottom + 2));
			}

			private Row RowAt(Point p) => rows.FirstOrDefault(r => r.Rect.Contains(p));

			private void SetSliderFromMouse(SliderRow s, int x)
			{
				float pct = Math.Max(0, Math.Min(1, (x - s.Control.X) / (float)s.Control.Width));
				int v = s.Min + (int)Math.Round(pct * (s.Max - s.Min));
				if (v != s.Value) { s.Value = v; Invalidate(); }
			}

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				var row = RowAt(e.Location);
				switch (row)
				{
					case ToggleRow t:
						int v = t.On ? 0 : 1;
						if (t.Set != null) t.Set(v > 0); else Program.MyData.SetIntValue(t.Setting, v);
						form.StatusText = Lang.T(v > 0 ? "status.enabled" : "status.disabled", t.Title);
						Invalidate();
						break;
					case SliderRow s:
						var hit = s.Control; hit.Inflate(12, 10);
						if (hit.Contains(e.Location)) { dragging = s; Capture = true; SetSliderFromMouse(s, e.X); }
						break;
					case ChoiceRow c:
						for (int i = 0; c.Segments != null && i < c.Segments.Length; i++)
						{
							if (c.Segments[i].Contains(e.Location) && c.Values[i] != c.Get())
							{
								// only options that really restart the program ask first (pet size); the theme applies at once
									if (!c.ConfirmRestart || PetMessage.Show(form, Lang.T("dlg.restart.text"), c.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
										c.Set(c.Values[i]);
							}
						}
						break;
					case ActionRow a:
						if (a.Control.Contains(e.Location)) a.Run();
						break;
					case DropDownRow d:
						if (d.Control.Contains(e.Location)) OpenDropDown(d);
						break;
					case ButtonsRow b:
						for (int i = 0; i < b.Hits.Count; i++) if (b.Hits[i].Contains(e.Location)) { b.Buttons[i].run(); break; }
						break;
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				if (dragging != null) { SetSliderFromMouse(dragging, e.X); return; }
				var row = RowAt(e.Location);
				int seg = -1;
				if (row is ChoiceRow c && c.Segments != null)
					for (int i = 0; i < c.Segments.Length; i++) if (c.Segments[i].Contains(e.Location)) seg = i;
				bool clickable = row is ToggleRow || (row is SliderRow s && s.Control.Contains(e.Location)) || seg >= 0
					|| (row is ActionRow a && a.Control.Contains(e.Location)) || (row is DropDownRow d && d.Control.Contains(e.Location)) || (row is ButtonsRow bq && bq.Hits.Any(h => h.Contains(e.Location)));
				Cursor = clickable ? Cursors.Hand : Cursors.Default;
				if (row != hover || seg != hoverSegment) { hover = row; hoverSegment = seg; Invalidate(); }
				else if (row is ActionRow || row is DropDownRow || row is ButtonsRow) Invalidate();
			}

			protected override void OnMouseUp(MouseEventArgs e)
			{
				base.OnMouseUp(e);
				if (dragging != null)
				{
					Program.MyData.SetIntValue(dragging.Setting, dragging.Value);
					form.StatusText = Lang.T("status.setTo", dragging.Title, dragging.Format(dragging.Value));
					dragging = null;
					Capture = false;
				}
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hover = null; hoverSegment = -1;
				Invalidate();
			}
		}

	}
}
