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
	/// <summary>About page.</summary>
	public sealed partial class FormSettings
	{
		// ================================================================== About

		private sealed class AboutView : ScrollSurface
		{
			private readonly FormSettings form;
			private readonly List<(Rectangle rect, string url)> links = new List<(Rectangle, string)>();

			public AboutView(FormSettings f) { form = f; }

			private string petTitle = "", petAuthor = "", petVersion = "", petInfo = "";

			/// <summary>Take the header of the pet currently running (no XML parsing, no settings written).</summary>
			protected override void OnVisibleChanged(EventArgs e)
			{
				base.OnVisibleChanged(e);
				if (!Visible) return;
				var header = Program.Mainthread?.CurrentHeader;
				if (header == null) return;
				petTitle = header.Title ?? "";
				petAuthor = header.Author ?? "";
				petVersion = header.Version ?? "";
				petInfo = CleanInfo(header.Info);
			}

			protected override int PaintContent(Graphics g, int scrollY)
			{
				links.Clear();
				int w = Math.Min(760, Width - ScrollBarWidth - 16);
				int x = 8, y = 8 - scrollY;

				var infoSize = Theme.MeasureText(g, petInfo, form.fontSmall, w - 40);
				var card = new Rectangle(x, y, w, 110 + infoSize.Height);
				Theme.FillRounded(g, Theme.Surface, card, 12);
				Theme.DrawRounded(g, Theme.Border, card, 12);
				Theme.DrawText(g, Lang.T("about.currentPet"), form.fontSmall, new Rectangle(card.X + 20, card.Y + 16, 300, 16), Theme.Accent, TextFormatFlags.SingleLine);
				Theme.DrawText(g, petTitle, form.fontH2, new Rectangle(card.X + 20, card.Y + 34, w - 40, 26), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("about.petLine", petAuthor, petVersion), form.fontSmall, new Rectangle(card.X + 20, card.Y + 62, w - 40, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, petInfo, form.fontSmall, new Rectangle(card.X + 20, card.Y + 90, w - 40, infoSize.Height), Theme.Text);
				y = card.Bottom + 16;

				// credits: card removed for now, it comes back when the project is finished

				// links
				var linkList = new[] {
					(Lang.T("about.link.github"), AppInfo.RepoUrl),
					(Lang.T("about.link.gallery"), AppInfo.PetsWebUrl),
					(Lang.T("about.link.help"), AppInfo.HelpUrl) };
				var lr = new Rectangle(x, y, w, 52 + linkList.Length * 30);
				Theme.FillRounded(g, Theme.Surface, lr, 12);
				Theme.DrawRounded(g, Theme.Border, lr, 12);
				Theme.DrawText(g, Lang.T("about.links"), form.fontSmall, new Rectangle(lr.X + 20, lr.Y + 16, 300, 16), Theme.Accent, TextFormatFlags.SingleLine);
				int ly = lr.Y + 42;
				foreach (var l in linkList)
				{
					var r = new Rectangle(lr.X + 20, ly, w - 40, 22);
					Theme.DrawText(g, l.Item1 + "  ↗", form.fontText, r, Theme.Link, TextFormatFlags.SingleLine);
					links.Add((new Rectangle(r.X, r.Y, Theme.MeasureText(g, l.Item1 + "  ↗", form.fontText).Width, r.Height), l.Item2));
					ly += 30;
				}
				y = lr.Bottom + 16;
				return y + scrollY;
			}

			private static string CleanInfo(string info)
			{
				if (string.IsNullOrEmpty(info)) return "";
				info = info.Replace("[br]", "\n");
				info = Regex.Replace(info, @"\[link:(.*?)\]", "$1");
				return info.Trim();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var l = links.FirstOrDefault(x => x.rect.Contains(e.Location));
				Cursor = l.url != null ? Cursors.Hand : Cursors.Default;
				if (l.url != null) form.StatusText = l.url;
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				var l = links.FirstOrDefault(x => x.rect.Contains(e.Location));
				if (l.url != null) Program.OpenLink(l.url);
			}
		}

	}
}
