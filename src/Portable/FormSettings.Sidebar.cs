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
	/// <summary>The navigation sidebar.</summary>
	public sealed partial class FormSettings
	{
		// ================================================================== Sidebar

		private sealed class Sidebar : Control
		{
			private readonly FormSettings form;
			private readonly (Page page, string text, string glyph)[] items =
			{
				(Page.Home, "nav.home", "\uE80F"),            // home
				(Page.Pets, "nav.pets", "\uE8D4"),            // pets (people)
				(Page.Reminders, "nav.reminders", "\uE823"),  // clock
				(Page.Ai, "nav.ai", "\uE8BD"),                // chat
				(Page.Plugins, "nav.plugins", "\uEA86"),      // puzzle
				(Page.Integrations, "nav.integrations", "\uE71B"), // link
				(Page.Application, "nav.application", "\uE713"), // settings
				(Page.About, "nav.about", "\uE946"),          // info
			};
			private int hover = -1;
			private readonly Font glyphFont;

			public Sidebar(FormSettings f)
			{
				form = f;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
				glyphFont = new Font(FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 16, GraphicsUnit.Pixel);
				Cursor = Cursors.Hand;
			}

			private static bool FontExists(string name)
			{
				using (var f = new Font(name, 10)) return string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase);
			}

			private Rectangle ItemRect(int i) => new Rectangle(10, 96 + i * 44, Width - 20, 38);

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.SurfaceAlt);
				using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);

				// app logo + name
				Theme.DrawPixelImage(g, Resources.logo, new Rectangle(24, 26, 32, 32));   // 64 px art at exactly 1/2: crisp pixels
				Theme.DrawText(g, "desktopPet", form.fontBold, new Rectangle(68, 24, Width - 70, 20), Theme.Text, TextFormatFlags.Left | TextFormatFlags.SingleLine);
				Theme.DrawText(g, Lang.T("nav.subtitle"), form.fontSmall, new Rectangle(68, 44, Width - 70, 18), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.SingleLine);

				for (int i = 0; i < items.Length; i++)
				{
					var r = ItemRect(i);
					bool selected = form.page == items[i].page;
					if (selected) Theme.FillRounded(g, Theme.Hover, r, 6);
					else if (hover == i) Theme.FillRounded(g, Color.FromArgb(120, Theme.Hover), r, 6);
					if (selected)
						Theme.FillRounded(g, Theme.Accent, new Rectangle(r.X, r.Y + 10, 3, r.Height - 20), 1.5f);
					TextRenderer.DrawText(g, items[i].glyph, glyphFont, new Rectangle(r.X + 14, r.Y, 24, r.Height), selected ? Theme.Accent : Theme.TextMuted,
						TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
					Theme.DrawText(g, Lang.T(items[i].text), form.fontNav, new Rectangle(r.X + 48, r.Y, r.Width - 50, r.Height), Theme.Text,
						TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
				}

				// bottom: quick actions
				var add = AddRect;
				Theme.DrawButton(g, add, Lang.T("nav.addPet"), form.fontText, true, hover == 100);
			}

			private Rectangle AddRect => new Rectangle(16, Height - 60, Width - 32, 38);

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				int h = -1;
				for (int i = 0; i < items.Length; i++) if (ItemRect(i).Contains(e.Location)) h = i;
				if (AddRect.Contains(e.Location)) h = 100;
				Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
				if (h != hover) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				if (hover != -1) { hover = -1; Invalidate(); }
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				for (int i = 0; i < items.Length; i++)
					if (ItemRect(i).Contains(e.Location)) form.SelectPage(items[i].page);
				if (AddRect.Contains(e.Location)) Program.Mainthread.AddSheep();
			}
		}

	}
}
