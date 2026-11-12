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
	/// <summary>Pets page: the cards, tabs and the detail panel.</summary>
	public sealed partial class FormSettings
	{
		// ================================================================== Pets gallery

		private sealed class PetsView : ScrollSurface
		{
			private readonly FormSettings form;
			private OptionsPet selected;
			private OptionsPet hoverPet;
			private bool hoverAdopt, hoverClose;
			private string filter = "";
			private readonly List<(Rectangle rect, OptionsPet pet)> cards = new List<(Rectangle, OptionsPet)>();
			private readonly List<(Rectangle rect, string url)> links = new List<(Rectangle, string)>();
			private Rectangle adoptRect, closeRect;
			private readonly List<(Rectangle rect, int index)> swatches = new List<(Rectangle, int)>();
			private int hoverSwatch = -1;
			public bool Loading = true;
			public string CurrentPet;
			/// <summary>Pet being downloaded (its button becomes a progress bar), and how far (0..1).</summary>
			public string DownloadingFolder;
			public double DownloadProgress;
			/// <summary>Pet whose last download failed: its button offers to try again.</summary>
			public string FailedFolder;
			/// <summary>The gallery list could not be fetched: only pets on this computer are shown.</summary>
			public bool Offline;
			private Rectangle cancelRect, removeRect, retryListRect, soundRect;
			private bool hoverCancel, hoverRemove, hoverRetryList, hoverSound;

			// with the detail panel open, the page scroll bar sits next to the cards, not on the panel
			protected override int ScrollBarX => selected != null ? GridArea.Right : base.ScrollBarX;
			private Rectangle readmeArea;
			private int detailScroll, detailMax;
			private const int CardW = 150, CardH = 150, Gap = 14;
			private const int DetailW = 340;

			public PetsView(FormSettings f)
			{
				form = f;
				// nothing downloaded yet: start where the pets are
				if (PetStore.Folders().Count == 0) CurrentTab = Tab.Download;
			}

			public bool DetailOpen => selected != null;

			public string Filter
			{
				get => filter;
				set { filter = (value ?? "").Trim(); ScrollY = 0; Invalidate(); }
			}

			public void PetsChanged() => Invalidate();

			public void CloseDetail() { selected = null; detailScroll = 0; Invalidate(); }

			public void SelectForSnapshot(OptionsPet pet) { selected = pet; detailScroll = 0; Invalidate(); }

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				// over the description of the detail panel: scroll the description, not the grid
				if (selected != null && detailMax > 0 && readmeArea.Contains(e.Location))
				{
					detailScroll = Math.Max(0, Math.Min(detailMax, detailScroll - e.Delta / 120 * 40));
					Invalidate();
					return;
				}
				base.OnMouseWheel(e);
			}

			/// <summary>Tabs of the gallery: pets on this computer, and pets to download.</summary>
			public enum Tab { Mine, Download }
			public Tab CurrentTab = Tab.Mine;
			private readonly List<(Rectangle rect, Tab tab)> tabRects = new List<(Rectangle, Tab)>();
			private int hoverTab = -1;

			/// <summary>On this computer: the built-in eSheep and every downloaded pet (a color group if any color is).</summary>
			private bool IsMine(OptionsPet p) => p.Variants != null
				? p.Variants.Any(v => v != null && form.IsDownloaded(v))
				: form.IsDownloaded(p);

			/// <summary>Still to download (a color group while some color is missing).</summary>
			private bool IsToDownload(OptionsPet p) => p.Variants != null
				? p.Variants.Any(v => v != null && !form.IsDownloaded(v))
				: !form.IsDownloaded(p);

			private IEnumerable<OptionsPet> TabPets(Tab t) => form.WebPets.pets.Where(p => t == Tab.Mine ? IsMine(p) : IsToDownload(p));

			private IEnumerable<OptionsPet> FilteredPets()
			{
				var pets = TabPets(CurrentTab);
				if (filter.Length == 0) return pets;
				bool Has(string s) => (s ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
				return pets.Where(p =>
					Has(p.folder) || Has(p.author) || Has(p.DisplayName) ||
					// group card: any of its colors, by folder ("pink_sheep") or color name ("rosa")
					(p.Variants != null && p.Variants.Where(v => v != null).Any(v => Has(v.folder))) ||
					(p.VariantNames != null && p.VariantNames.Any(k => Has(Lang.T(k)))));
			}

			private Rectangle GridArea => selected == null
				? new Rectangle(0, 0, Width - ScrollBarWidth, Height)
				: new Rectangle(0, 0, Width - DetailW - 16 - ScrollBarWidth, Height);

			/// <summary>Order of the gallery cards.</summary>
			private enum SortMode { Recent, Name, Author }
			private SortMode sort = SortMode.Recent;
			private readonly List<(Rectangle rect, SortMode mode)> sortChips = new List<(Rectangle, SortMode)>();
			private int hoverChip = -1;
			private const int MaxCardW = 190;

			private IEnumerable<OptionsPet> Sorted(IEnumerable<OptionsPet> pets)
			{
				switch (sort)
				{
					case SortMode.Name: return pets.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
					case SortMode.Author: return pets.OrderBy(p => p.author ?? "", StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
					default: return pets.OrderByDescending(p => p.lastupdate ?? "").ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
				}
			}

			protected override int PaintContent(Graphics g, int scrollY)
			{
				cards.Clear();
				sortChips.Clear();
				var area = GridArea;
				int y = 4 - scrollY;

				// --- tabs: "Meus pets (n)" | "Baixar pets (n)", underlined like the Windows 11 pivots
				tabRects.Clear();
				int tx = 8;
				var tabs = new[] { (Tab.Mine, "pets.tabMine"), (Tab.Download, "pets.tabDownload") };
				for (int i = 0; i < tabs.Length; i++)
				{
					int count = TabPets(tabs[i].Item1).Count();
					string text = Lang.T(tabs[i].Item2) + (Loading && tabs[i].Item1 == Tab.Download ? "" : "  " + count);
					int tw = Theme.MeasureText(g, text, form.fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width + 24;
					var tr = new Rectangle(tx, y, tw, 34);
					bool on = CurrentTab == tabs[i].Item1;
					if (hoverTab == i && !on) Theme.FillRounded(g, Theme.Hover, tr, 6);
					Theme.DrawText(g, text, form.fontBold, tr, on ? Theme.Text : Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					if (on) Theme.FillRounded(g, Theme.Accent, new Rectangle(tr.X + 12, tr.Bottom - 3, tr.Width - 24, 3), 1.5f);
					tabRects.Add((tr, tabs[i].Item1));
					tx += tw + 6;
				}
				using (var line = new Pen(Theme.Border)) g.DrawLine(line, 8, y + 34, area.Width - 8, y + 34);
				y += 44;

				// --- sort chips: "Ordenar: Recentes | Nome | Autor"
				int cx = 8;
				Theme.DrawText(g, Lang.T("pets.sortBy"), form.fontSmall, new Rectangle(cx, y, 120, 28), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				cx += Theme.MeasureText(g, Lang.T("pets.sortBy"), form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 8;
				var modes = new[] { (SortMode.Recent, "pets.sort.recent"), (SortMode.Name, "pets.sort.name"), (SortMode.Author, "pets.sort.author") };
				for (int i = 0; i < modes.Length; i++)
				{
					string text = Lang.T(modes[i].Item2);
					int w = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 24;
					var chip = new Rectangle(cx, y + 2, w, 24);
					bool on = sort == modes[i].Item1;
					Theme.FillRounded(g, on ? Theme.AccentSoft : hoverChip == i ? Theme.Hover : Theme.Surface, chip, 12);
					Theme.DrawRounded(g, on ? Theme.Accent : Theme.Border, chip, 12);
					Theme.DrawText(g, text, form.fontSmall, chip, on ? Theme.Accent : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					sortChips.Add((chip, modes[i].Item1));
					cx += w + 6;
				}
				y += 38;

				// --- cards fill the width: as many columns as fit, cards stretched up to MaxCardW, grid centered
				int usable = area.Width - 16;
				int cols = Math.Max(1, (usable + Gap) / (CardW + Gap));
				int cardW = Math.Min(MaxCardW, (usable - (cols - 1) * Gap) / cols);
				int x0 = 8 + (usable - (cols * cardW + (cols - 1) * Gap)) / 2;

				// --- no internet: say why there is nothing to download, with a way to try again
				retryListRect = Rectangle.Empty;
				if (Offline && CurrentTab == Tab.Download)
				{
					var box = new Rectangle(8, y, area.Width - 16, 44);
					Theme.FillRounded(g, Theme.SurfaceAlt, box, 8);
					string btn = Lang.T("pets.offlineRetry");
					int bw = Theme.MeasureText(g, btn, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 24;
					retryListRect = new Rectangle(box.Right - bw - 8, box.Y + 8, bw, 28);
					Theme.DrawText(g, Lang.T("pets.offline"), form.fontSmall, new Rectangle(box.X + 12, box.Y, retryListRect.X - box.X - 20, box.Height), Theme.Text,
						TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
					Theme.DrawButton(g, retryListRect, btn, form.fontSmall, false, hoverRetryList);
					y += box.Height + 12;
				}

				var list = FilteredPets().ToList();
				if (list.Count == 0)
				{
					string msg = Loading ? Lang.T("pets.loading")
						: filter.Length > 0 ? Lang.T("pets.noMatch", filter)
						: CurrentTab == Tab.Download && !Offline ? Lang.T("pets.allDownloaded")
						: CurrentTab == Tab.Download ? ""
						: Lang.T("pets.empty");
					Theme.DrawText(g, msg, form.fontText, new Rectangle(8, y + 32, area.Width - 16, 40), Theme.TextMuted, TextFormatFlags.HorizontalCenter);
				}

				// --- one list: the eSheep (comes with the program) first, then every pet of the gallery
				var ordered = list.Where(p => p.Variants == null && BuiltInPets.IsBuiltIn(p.folder))
					.Concat(Sorted(list.Where(p => p.Variants != null || !BuiltInPets.IsBuiltIn(p.folder))))
					.ToList();
				int col = 0;
				foreach (var pet in ordered)
				{
					var r = new Rectangle(x0 + col * (cardW + Gap), y, cardW, CardH);
					cards.Add((r, pet));
					if (r.Bottom >= 0 && r.Top <= Height) DrawCard(g, r, pet);
					col++;
					if (col >= cols) { col = 0; y += CardH + Gap; }
				}
				if (col != 0) y += CardH + Gap;
				y += 10;
				int contentHeight = y + scrollY + 8;

				if (selected != null) DrawDetail(g);
				return contentHeight;
			}

			private void DrawCard(Graphics g, Rectangle r, OptionsPet pet)
			{
				bool isSel = pet == selected, isHover = pet == hoverPet;
				Theme.FillRounded(g, isHover ? Theme.SurfaceAlt : Theme.Surface, r, 10);
				Theme.DrawRounded(g, isSel ? Theme.Accent : Theme.Border, r, 10, isSel ? 2f : 1f);

				var iconRect = new Rectangle(r.X + (r.Width - 64) / 2, r.Y + 20, 64, 64);
				if (pet.Image != null) Theme.DrawPixelImage(g, pet.Image, iconRect);
				else Theme.FillRounded(g, Theme.Hover, iconRect, 8);

				Theme.DrawText(g, pet.DisplayName, form.fontBold, new Rectangle(r.X + 8, r.Y + 94, r.Width - 16, 20), Theme.Text,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				if (pet.Variants != null)
				{
					// color group: a row of dots instead of the author, the shown color is bigger
					int n = pet.Variants.Length, d = 9, gap = 5, sel = pet.SelectedVariant;
					int total = n * d + (n - 1) * gap, dx = r.X + (r.Width - total) / 2, dy = r.Y + 120;
					var old = g.SmoothingMode;
					g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					for (int i = 0; i < n; i++)
					{
						var dot = new Rectangle(dx + i * (d + gap), dy, d, d);
						if (i == sel) dot.Inflate(2, 2);
						using (var b = new SolidBrush(pet.Variants[i] == null ? Theme.Hover : pet.VariantColors[i])) g.FillEllipse(b, dot);
					}
					g.SmoothingMode = old;
				}
				else
				{
					Theme.DrawText(g, pet.author, form.fontSmall, new Rectangle(r.X + 8, r.Y + 116, r.Width - 16, 18), Theme.TextMuted,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}

				// badge: update > in use > new ("downloaded" is said by the tab itself)
				string badgeKey = form.IsOutdated(pet) ? "pets.badgeUpdate"
					: pet.Contains(CurrentPet) ? "pets.badgeInUse"
					: form.IsNewPet(pet) ? "pets.badgeNew"
					: null;
				if (badgeKey != null)
				{
					string text = Lang.T(badgeKey);
					int bw = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 18;
					var badge = new Rectangle(r.Right - bw - 8, r.Y + 8, bw, 20);
					bool muted = badgeKey == "pets.badgeDownloaded";
					Theme.FillRounded(g, muted ? Theme.SurfaceAlt : Theme.AccentSoft, badge, 10);
					Theme.DrawText(g, text, form.fontSmall, badge, muted ? Theme.TextMuted : Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
			}

			private void DrawDetail(Graphics g)
			{
				links.Clear();
				var panel = new Rectangle(Width - DetailW - 4, 8, DetailW, Height - 16);
				Theme.FillRounded(g, Theme.Surface, panel, 12);
				Theme.DrawRounded(g, Theme.Border, panel, 12);

				var pet = selected;
				closeRect = new Rectangle(panel.Right - 40, panel.Y + 10, 30, 30);
				if (hoverClose) Theme.FillRounded(g, Theme.Hover, closeRect, 6);
				Theme.DrawText(g, "✕", form.fontText, closeRect, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

				int x = panel.X + 20, w = panel.Width - 40, y = panel.Y + 20;
				// big preview
				var iconBack = new Rectangle(x, y, 96, 96);
				Theme.FillRounded(g, Theme.SurfaceAlt, iconBack, 12);
				if (pet.Image != null) Theme.DrawPixelImage(g, pet.Image, new Rectangle(x + 8, y + 8, 80, 80));
				Theme.DrawText(g, pet.DisplayName, form.fontH2, new Rectangle(x + 110, y + 10, w - 140, 48), Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("pets.by", pet.author), form.fontSmall, new Rectangle(x + 110, y + 60, w - 110, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("pets.updated", pet.lastupdate), form.fontSmall, new Rectangle(x + 110, y + 78, w - 110, 18), Theme.TextMuted, TextFormatFlags.SingleLine);
				y += 112;

				// color picker of a group card
				swatches.Clear();
				if (pet.Variants != null)
				{
					int sel = pet.SelectedVariant;
					string colorName = sel >= 0 ? Lang.T(pet.VariantNames[sel]) : "";
					Theme.DrawText(g, Lang.T("pets.color", colorName), form.fontBold, new Rectangle(x, y, w, 20), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					y += 26;
					int n = pet.Variants.Length, gap = 6;
					int size = Math.Min(40, (w - gap * (n - 1)) / n);
					for (int i = 0; i < n; i++)
					{
						var v = pet.Variants[i];
						var sr = new Rectangle(x + i * (size + gap), y, size, size);
						bool isSel = i == sel, isHover = i == hoverSwatch;
						Theme.FillRounded(g, isSel ? Theme.AccentSoft : isHover ? Theme.Hover : Theme.SurfaceAlt, sr, 8);
						if (v?.Image != null) Theme.DrawPixelImage(g, v.Image, Rectangle.Inflate(sr, -4, -4));
						// color bar under the icon, so similar colors are easy to tell apart
						Theme.FillRounded(g, v == null ? Theme.Hover : pet.VariantColors[i], new Rectangle(sr.X + 8, sr.Bottom - 6, sr.Width - 16, 3), 1.5f);
						Theme.DrawRounded(g, isSel ? Theme.Accent : isHover ? Theme.Border : Theme.SurfaceAlt, sr, 8, isSel ? 2f : 1f);
						if (v != null) swatches.Add((sr, i));
					}
					y += size + 14;
				}

				adoptRect = new Rectangle(x, y, w, 38);
				cancelRect = removeRect = soundRect = Rectangle.Empty;
				bool inUse = pet.folder == CurrentPet;
				if (DownloadingFolder != null && DownloadingFolder == pet.folder)
				{
					// progress bar + ✕ to stop the download
					adoptRect = new Rectangle(x, y, w - 46, 38);
					cancelRect = new Rectangle(adoptRect.Right + 8, y, 38, 38);
					DrawProgressButton(g, adoptRect, DownloadProgress);
					Theme.DrawButton(g, cancelRect, "✕", form.fontBold, false, hoverCancel);
				}
				else
				{
					string weight = FormatSize(pet.size);
					string action = FailedFolder == pet.folder ? Lang.T("pets.retry")
						: form.IsOutdated(pet) ? (weight.Length > 0 ? Lang.T("pets.downloadUpdateSize", weight) : Lang.T("pets.downloadUpdate"))
						: inUse ? Lang.T("pets.adoptAnother")
						: !form.IsDownloaded(pet) ? (weight.Length > 0 ? Lang.T("pets.downloadSize", weight) : Lang.T("pets.download"))
						: Lang.T("pets.adopt");
					Theme.DrawButton(g, adoptRect, action, form.fontBold, true, hoverAdopt);
				}
				y += 46;
				// a downloaded pet (not the one in use) can leave the computer; it can be downloaded again
				if (DownloadingFolder != pet.folder && !inUse && !BuiltInPets.IsBuiltIn(pet.folder) && PetStore.Has(pet.folder))
				{
					string text = Lang.T("pets.removeLink");
					var ts = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine);
					removeRect = new Rectangle(x + (w - ts.Width - 16) / 2, y - 4, ts.Width + 16, 22);
					if (hoverRemove) Theme.FillRounded(g, Theme.Hover, removeRect, 6);
					Theme.DrawText(g, text, form.fontSmall, removeRect, hoverRemove ? Theme.Danger : Theme.TextMuted,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					y += 24;
				}
				else y += 6;

				// stats: from the file when it is here, otherwise from pets.json
				var stats = pet.Detail != null ? ReadStats(pet.Detail)
					: pet.animations > 0 ? new List<(string value, string label)>
					{
						(pet.animations.ToString(), Lang.T("pets.statAnimations")),
						(pet.sounds.ToString(), Lang.T("pets.statSounds")),
						("1", Lang.T("pets.statSkins")),
					}
					: null;
				if (stats != null)
				{
					// color group: the third box counts the colors to choose from, not the skins of one file
					if (pet.Variants != null && stats.Count >= 3)
						stats[2] = (pet.Variants.Length.ToString(), Lang.T("pets.statColors"));
					int sw = (w - 16) / 3;
					for (int i = 0; i < stats.Count && i < 3; i++)
					{
						var sr = new Rectangle(x + i * (sw + 8), y, sw, 54);
						Theme.FillRounded(g, Theme.SurfaceAlt, sr, 8);
						Theme.DrawText(g, stats[i].value, form.fontBold, new Rectangle(sr.X, sr.Y + 8, sr.Width, 20), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
						Theme.DrawText(g, stats[i].label, form.fontSmall, new Rectangle(sr.X, sr.Y + 28, sr.Width, 18), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
					}
					y += 66;
					// sounds of this pet on/off (only when it has sounds and is on this computer)
					soundRect = Rectangle.Empty;
					if (form.IsDownloaded(pet) && stats.Count >= 2 && int.TryParse(stats[1].value, out int nSounds) && nSounds > 0)
					{
						bool on = !Program.MyData.IsPetMuted(pet.folder);
						var row = new Rectangle(x, y, w, 30);
						soundRect = row;
						if (hoverSound) Theme.FillRounded(g, Theme.Hover, row, 6);
						Theme.DrawText(g, Lang.T("pets.sounds"), form.fontText, new Rectangle(row.X + 8, row.Y, row.Width - 70, row.Height), Theme.Text,
							TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
						Theme.DrawToggle(g, new Rectangle(row.Right - 52, row.Y + 5, 44, 20), on);
						y += 38;
					}
					else y += 2;
				}
				else if (form.IsDownloaded(pet))   // the file is here: its counts are on the way
				{
					Theme.DrawText(g, Lang.T("pets.loadingDetails"), form.fontSmall, new Rectangle(x, y, w, 18), Theme.TextMuted);
					y += 28;
				}

				// readme (simple markdown) in its own scrolling area: it never leaves the panel.
				// TextRenderer ignores the Graphics clip unless PreserveGraphicsClipping is set.
				readmeArea = new Rectangle(panel.X + 1, y, panel.Width - 2, Math.Max(0, panel.Bottom - y - 10));
				const TextFormatFlags Keep = TextFormatFlags.PreserveGraphicsClipping;
				var clip = g.Clip;
				g.SetClip(readmeArea);
				int top = y;
				y -= detailScroll;
				foreach (var block in ReadmeBlocks(pet))
				{
					if (block.heading)
					{
						y += 6;
						if (y + 24 > readmeArea.Top && y < readmeArea.Bottom)
							Theme.DrawText(g, block.text, form.fontBold, new Rectangle(x, y, w, 20), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | Keep);
						y += 24;
						continue;
					}
					int indent = block.bullet ? 14 : 0;
					var size = Theme.MeasureText(g, block.text, form.fontSmall, w - indent);
					var tr = new Rectangle(x + indent, y, w - indent, size.Height);
					if (tr.Bottom > readmeArea.Top && tr.Top < readmeArea.Bottom)
					{
						if (block.bullet) using (var b = new SolidBrush(Theme.TextMuted)) g.FillEllipse(b, x + 2, y + 6, 5, 5);
						bool isLink = block.url.StartsWith("https://");
						Theme.DrawText(g, block.text, form.fontSmall, tr, isLink ? Theme.Link : Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | Keep);
						var visible = Rectangle.Intersect(tr, readmeArea);
						if (isLink && !visible.IsEmpty) links.Add((visible, block.url));
					}
					y += size.Height + 8;
				}
				g.Clip = clip;
				int total = y + detailScroll - top;
				detailMax = Math.Max(0, total - readmeArea.Height);
				if (detailScroll > detailMax) { detailScroll = detailMax; Invalidate(); }
				if (detailMax > 0)   // small scroll thumb on the right edge of the panel
				{
					// soft fade where the text goes on below (instead of a line cut in half)
					if (detailScroll < detailMax)
					{
						var fade = new Rectangle(readmeArea.X, readmeArea.Bottom - 28, readmeArea.Width - 12, 28);
						using (var fb = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(fade.X, fade.Y - 1, fade.Width, fade.Height + 2),
							Color.FromArgb(0, Theme.Surface), Theme.Surface, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
							g.FillRectangle(fb, fade);
					}
					int th = Math.Max(24, readmeArea.Height * readmeArea.Height / Math.Max(1, total));
					int ty = readmeArea.Top + (int)((readmeArea.Height - th) * (detailScroll / (double)detailMax));
					Theme.FillRounded(g, Theme.Border, new Rectangle(panel.Right - 9, ty, 4, th), 2);
				}
			}

			/// <summary>
			/// README as blocks: headings, bullets and paragraphs. Lines of one paragraph are joined
			/// (markdown wraps long sentences over several lines); a blank line ends a paragraph.
			/// </summary>
			private static List<(bool heading, bool bullet, string text, string url)> ReadmeBlocks(OptionsPet pet)
			{
				var blocks = new List<(bool, bool, string, string)>();
				var cur = new System.Text.StringBuilder();
				bool curBullet = false;
				void Flush()
				{
					if (cur.Length == 0) return;
					string text = cur.ToString().Trim();
					// links: keep the label, remember the first url of the block
					var m = Regex.Match(text, @"\[(.*?)\]\((.*?)\)");
					string url = m.Success ? m.Groups[2].Value : Regex.Match(text, @"https?://\S+").Value;
					text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
					text = Regex.Replace(text, @"\*\*(.*?)\*\*|\*(.*?)\*|__(.*?)__", "$1$2$3").Replace("\\_", "_");
					text = Regex.Replace(text, @"<[^>]+>", "").Replace("`", "");
					blocks.Add((false, curBullet, text, url ?? ""));
					cur.Clear();
					curBullet = false;
				}
				foreach (var raw in pet.Readme ?? new List<string>())
				{
					string line = (raw ?? "").TrimEnd();
					string trimmed = line.Trim();
					if (trimmed.Length == 0 || Regex.IsMatch(trimmed, @"^(-{3,}|\*{3,}|_{3,})$")) { Flush(); continue; }
					if (trimmed.StartsWith("#"))
					{
						Flush();
						string t = trimmed.TrimStart('#').Trim();
						if (t.Equals(pet.DisplayName, StringComparison.OrdinalIgnoreCase) || t.Equals(pet.folder, StringComparison.OrdinalIgnoreCase)) continue;
						blocks.Add((true, false, t, ""));
						continue;
					}
					if (Regex.IsMatch(line, @"^\s*([*-]|\d+\.)\s+"))
					{
						Flush();
						curBullet = true;
						cur.Append(Regex.Replace(line, @"^\s*([*-]|\d+\.)\s+", ""));
						continue;
					}
					if (cur.Length > 0) cur.Append(' ');
					cur.Append(trimmed);
				}
				Flush();
				return blocks;
			}

			/// <summary>File size for the download button: "850 KB", "1,4 MB" (empty when unknown).</summary>
			public static string FormatSize(long bytes)
			{
				if (bytes <= 0) return "";
				if (bytes < 1024 * 1024) return Math.Max(1, (int)Math.Round(bytes / 1024.0)) + " KB";
				return (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " MB";
			}

			/// <summary>The download button while the file arrives: it fills from left to right.</summary>
			private void DrawProgressButton(Graphics g, Rectangle r, double progress)
			{
				progress = Math.Max(0, Math.Min(1, progress));
				string text = Lang.T("pets.downloadingPct", (int)Math.Floor(progress * 100));
				const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
					| TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping;
				var old = g.Clip;
				Theme.FillRounded(g, Theme.AccentSoft, r, 6);
				Theme.DrawRounded(g, Theme.Accent, r, 6);
				var done = new Rectangle(r.X, r.Y, (int)Math.Round(r.Width * progress), r.Height);
				// empty part: accent text on the soft track
				g.SetClip(new Rectangle(done.Right, r.Y, r.Right - done.Right, r.Height), System.Drawing.Drawing2D.CombineMode.Intersect);
				TextRenderer.DrawText(g, text, form.fontBold, r, Theme.Accent, flags);
				g.Clip = old;
				// filled part: the normal button colors
				if (done.Width > 0)
				{
					g.SetClip(done, System.Drawing.Drawing2D.CombineMode.Intersect);
					Theme.FillRounded(g, Theme.Accent, r, 6);
					TextRenderer.DrawText(g, text, form.fontBold, r, Theme.AccentText, flags);
					g.Clip = old;
				}
			}

			private static List<(string value, string label)> ReadStats(XmlDocument doc)
			{
				var res = new List<(string, string)>();
				XmlNode root = doc.DocumentElement;
				int Count(string name) => root?.SelectSingleNode("*[local-name()='" + name + "']")?.ChildNodes.Count ?? 0;
				res.Add((Count("animations").ToString(), Lang.T("pets.statAnimations")));
				res.Add((Count("sounds").ToString(), Lang.T("pets.statSounds")));
				var skins = root?.SelectSingleNode("*[local-name()='image']/*[local-name()='skins']")?.InnerText;
				res.Add((string.IsNullOrEmpty(skins) ? "1" : skins, Lang.T("pets.statSkins")));
				return res;
			}

			private OptionsPet PetAt(Point p)
			{
				foreach (var c in cards) if (c.rect.Contains(p) && GridArea.Contains(p)) return c.pet;
				return null;
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var hp = PetAt(e.Location);
				bool ha = selected != null && adoptRect.Contains(e.Location);
				bool hc = selected != null && closeRect.Contains(e.Location);
				bool hx = selected != null && cancelRect.Contains(e.Location);
				bool hr = selected != null && removeRect.Contains(e.Location);
				bool hrl = Offline && retryListRect.Contains(e.Location);
				bool hsn = selected != null && soundRect.Contains(e.Location);
				bool hl = selected != null && links.Any(l => l.rect.Contains(e.Location));
				int hs = selected == null ? -1 : swatches.Where(s => s.rect.Contains(e.Location)).Select(s => s.index).DefaultIfEmpty(-1).First();
				int hch = sortChips.FindIndex(c => c.rect.Contains(e.Location));
				int htb = tabRects.FindIndex(t => t.rect.Contains(e.Location));
				Cursor = (hp != null || ha || hc || hx || hr || hrl || hsn || hl || hs >= 0 || hch >= 0 || htb >= 0) ? Cursors.Hand : Cursors.Default;
				if (hp != hoverPet || ha != hoverAdopt || hc != hoverClose || hs != hoverSwatch || hch != hoverChip
					|| hx != hoverCancel || hr != hoverRemove || hrl != hoverRetryList || hsn != hoverSound || htb != hoverTab)
				{
					hoverPet = hp; hoverAdopt = ha; hoverClose = hc; hoverSwatch = hs; hoverChip = hch;
					hoverCancel = hx; hoverRemove = hr; hoverRetryList = hrl; hoverSound = hsn; hoverTab = htb;
					Invalidate();
				}
				if (hs >= 0 && selected?.VariantNames != null)
					form.StatusText = Lang.T("pets.color", Lang.T(selected.VariantNames[hs]));
				if (hl) form.StatusText = links.First(l => l.rect.Contains(e.Location)).url;
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hoverPet = null; hoverAdopt = hoverClose = hoverCancel = hoverRemove = hoverRetryList = hoverSound = false; hoverSwatch = -1; hoverChip = -1; hoverTab = -1;
				Invalidate();
			}

			protected override async void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				foreach (var c in sortChips)
				{
					if (!c.rect.Contains(e.Location)) continue;
					sort = c.mode;
					Invalidate();
					return;
				}
				foreach (var t in tabRects)
				{
					if (!t.rect.Contains(e.Location)) continue;
					if (CurrentTab != t.tab) { CurrentTab = t.tab; ScrollY = 0; CloseDetail(); }
					return;
				}
				if (Offline && retryListRect.Contains(e.Location)) { form.ReloadPets(); return; }
				if (selected != null)
				{
					if (closeRect.Contains(e.Location)) { CloseDetail(); return; }
					if (cancelRect.Contains(e.Location)) { form.CancelDownload(); return; }
					if (removeRect.Contains(e.Location)) { form.RemovePet(selected); return; }
					if (soundRect.Contains(e.Location))
					{
						bool muted = !Program.MyData.IsPetMuted(selected.folder);
						Program.MyData.SetPetMuted(selected.folder, muted);
						form.StatusText = Lang.T(muted ? "status.soundOff" : "status.soundOn", selected.DisplayName);
						Invalidate();
						return;
					}
					if (adoptRect.Contains(e.Location))
					{
						if (DownloadingFolder == null) form.PrimaryAction(selected);   // the progress bar is not a button
						return;
					}
					foreach (var l in links) if (l.rect.Contains(e.Location)) { Program.OpenLink(l.url); return; }
					foreach (var s in swatches)
					{
						if (!s.rect.Contains(e.Location)) continue;
						var card = selected;
						card.SelectVariant(card.Variants[s.index]);
						Invalidate();
						await form.LoadDetailsAsync(card);
						if (selected == card) Invalidate();
						return;
					}
				}
				var pet = PetAt(e.Location);
				if (pet == null) return;
				if (selected != pet) detailScroll = 0;
				selected = pet;
				form.MarkSeen(pet);
				Invalidate();
				await form.LoadDetailsAsync(pet);
				if (selected == pet) Invalidate();
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);
				var pet = PetAt(e.Location);
				if (pet != null) form.PrimaryAction(pet);
			}
		}

	}
}
