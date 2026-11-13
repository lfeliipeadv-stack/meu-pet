using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	public sealed partial class FormSettings
	{
		// ================================================================== Home page

		/// <summary>
		/// The start page: the pet in use, the numbers of today (focus, breaks, reminders, mood), what comes next
		/// (reminders, meeting, weather) and the state of the program. Everything is read on this computer.
		/// </summary>
		private sealed class HomeView : ScrollSurface
		{
			private readonly FormSettings form;
			private readonly List<(Rectangle rect, Action run, string id)> buttons = new List<(Rectangle, Action, string)>();
			private string hover;
			private Font bigFont, emojiFont, heroFont;

			public HomeView(FormSettings f) { form = f; }

			protected override int PaintContent(Graphics g, int scrollY)
			{
				bigFont = bigFont ?? Theme.CreateFont(26, FontStyle.Bold);
				heroFont = heroFont ?? Theme.CreateFont(30, FontStyle.Bold);
				emojiFont = emojiFont ?? Theme.CreateFont(20);
				buttons.Clear();
				int y = 6 - scrollY;
				int w = Width - ScrollBarWidth - 16;
				const int Gap = 14;

				// --- hero: the pet in use
				var hero = new Rectangle(8, y, w, 170);
				using (var b = new LinearGradientBrush(hero, Theme.Accent, ControlPaint.Dark(Theme.Accent, 0.15f), LinearGradientMode.Horizontal))
				using (var path = Theme.RoundedRect(hero, 16))
					g.FillPath(b, path);
				Theme.DrawText(g, Lang.T("home.yourPet").ToUpperInvariant(), form.fontSmall, new Rectangle(hero.X + 26, hero.Y + 20, hero.Width - 220, 18), Color.FromArgb(210, Theme.AccentText), TextFormatFlags.SingleLine);
				string name = Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";
				Theme.DrawText(g, name, heroFont, new Rectangle(hero.X + 24, hero.Y + 40, hero.Width - 220, 44), Theme.AccentText, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, PetLine(), form.fontText, new Rectangle(hero.X + 26, hero.Y + 86, hero.Width - 240, 22), Color.FromArgb(230, Theme.AccentText), TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				int bx = hero.X + 26;
				bx = HeroButton(g, bx, hero.Y + 120, Lang.T("home.changePet"), "change", () => form.SelectPage(Page.Pets));
				bx = HeroButton(g, bx + 8, hero.Y + 120, "💬 " + Lang.T("hover.ask"), "ask", FormAsk.Open);
				bx = HeroButton(g, bx + 8, hero.Y + 120, "＋ " + Lang.T("home.addPet"), "add", () => Program.Mainthread.AddSheep());
				var img = form.CurrentPetImage();
				if (img != null)
				{
					var dest = Fit(img.Size, new Rectangle(hero.Right - 170, hero.Y + 20, 140, 130));
					Theme.DrawPixelImage(g, img, dest);
				}
				y += hero.Height + Gap;

				// --- the numbers of today
				var stats = new[]
				{
					("⏱️", Lang.T("home.focus"), DayStats.Get("focusMin") + " min", Lang.T("home.focusFoot")),
					("🚶", Lang.T("home.breaks"), DayStats.Get("breaks").ToString(), Lang.T("home.breaksFoot")),
					("⏰", Lang.T("home.reminders"), TodayReminders().Count.ToString(), Lang.T("home.remindersFoot")),
					("😊", Lang.T("home.mood"), MoodToday(), Lang.T("home.moodFoot")),
				};
				int cols = w >= 760 ? 4 : 2;
				int sw = (w - (cols - 1) * Gap) / cols;
				for (int i = 0; i < stats.Length; i++)
				{
					var r = new Rectangle(8 + (i % cols) * (sw + Gap), y + (i / cols) * (112 + Gap), sw, 112);
					Theme.FillRounded(g, Theme.Surface, r, 12);
					Theme.DrawRounded(g, Theme.Border, r, 12);
					var ic = new Rectangle(r.X + 16, r.Y + 14, 34, 34);
					Theme.FillRounded(g, Theme.AccentSoft, ic, 9);
					Theme.DrawText(g, stats[i].Item1, emojiFont, ic, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					Theme.DrawText(g, stats[i].Item2.ToUpperInvariant(), form.fontSmall, new Rectangle(ic.Right + 10, r.Y + 14, r.Width - 70, 34), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, stats[i].Item3, bigFont, new Rectangle(r.X + 16, r.Y + 50, r.Width - 32, 34), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, stats[i].Item4, form.fontSmall, new Rectangle(r.X + 16, r.Y + 84, r.Width - 32, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
				y += ((stats.Length + cols - 1) / cols) * (112 + Gap);

				// --- next (reminders, meeting, weather) | the program
				bool two = w >= 760;
				int lw = two ? (w - Gap) * 3 / 5 : w, rw = two ? w - lw - Gap : w;
				var next = NextItems();
				int lh = 64 + Math.Max(1, next.Count) * 40;
				var left = new Rectangle(8, y, lw, lh);
				Panel(g, left, "🗓️  " + Lang.T("home.next"));
				if (next.Count == 0)
					Theme.DrawText(g, Lang.T("home.nothing"), form.fontText, new Rectangle(left.X + 20, left.Y + 56, left.Width - 40, 24), Theme.TextMuted, TextFormatFlags.SingleLine);
				for (int i = 0; i < next.Count; i++)
				{
					var row = new Rectangle(left.X + 16, left.Y + 52 + i * 40, left.Width - 32, 34);
					Theme.FillRounded(g, Theme.SurfaceAlt, row, 8);
					Theme.DrawText(g, next[i].when, form.fontBold, new Rectangle(row.X + 12, row.Y, 110, row.Height), Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, next[i].what, form.fontText, new Rectangle(row.X + 126, row.Y, row.Width - 136, row.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}

				var right = two ? new Rectangle(left.Right + Gap, y, rw, Math.Max(lh, 236)) : new Rectangle(8, left.Bottom + Gap, rw, 236);
				Panel(g, right, "🩺  " + Lang.T("home.system"));
				int on = form.pluginsView?.TestCards.Count(c => c.Get == null || c.Get()) ?? 0, total = form.pluginsView?.TestCards.Count ?? 0;
				var lines = new[]
				{
					(Lang.T("home.pets"), Lang.T("home.petsValue", PetStore.Folders().Count + BuiltInPets.Folders.Length)),
					(Lang.T("home.plugins"), Lang.T("home.pluginsValue", on, total)),
					(Lang.T("home.updates"), PetUpdates.Outdated.Count > 0 ? Lang.T("home.updatesPets", PetUpdates.Outdated.Count) : Lang.T("home.updatesNone")),
					(Lang.T("home.version"), Updater.CurrentVersion.ToString(3)),
				};
				for (int i = 0; i < lines.Length; i++)
				{
					int ly = right.Y + 52 + i * 34;
					Theme.DrawText(g, lines[i].Item1, form.fontText, new Rectangle(right.X + 20, ly, right.Width / 2, 28), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					Theme.DrawText(g, lines[i].Item2, form.fontSmall, new Rectangle(right.X + right.Width / 2, ly, right.Width / 2 - 20, 28), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
				var manage = new Rectangle(right.X + 20, right.Bottom - 48, right.Width - 40, 32);
				Theme.DrawButton(g, manage, Lang.T("home.managePlugins"), form.fontSmall, false, hover == "plugins");
				buttons.Add((manage, () => form.SelectPage(Page.Plugins), "plugins"));

				int bottom = Math.Max(left.Bottom, right.Bottom);
				return bottom + scrollY + 12;
			}

			private void Panel(Graphics g, Rectangle r, string title)
			{
				Theme.FillRounded(g, Theme.Surface, r, 12);
				Theme.DrawRounded(g, Theme.Border, r, 12);
				Theme.DrawText(g, title, form.fontBold, new Rectangle(r.X + 20, r.Y + 14, r.Width - 40, 26), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}

			private int HeroButton(Graphics g, int x, int y, string text, string id, Action run)
			{
				int bw = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 30;
				var r = new Rectangle(x, y, bw, 32);
				Theme.FillRounded(g, hover == id ? Color.FromArgb(70, Color.White) : Color.FromArgb(40, Color.White), r, 16);
				Theme.DrawText(g, text, form.fontSmall, r, Theme.AccentText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				buttons.Add((r, run, id));
				return r.Right;
			}

			private static Rectangle Fit(Size img, Rectangle box)
			{
				double k = Math.Min(box.Width / (double)img.Width, box.Height / (double)img.Height);
				if (k >= 1) k = Math.Floor(k);   // pixel art: whole steps
				int w = (int)(img.Width * k), h = (int)(img.Height * k);
				return new Rectangle(box.X + (box.Width - w) / 2, box.Bottom - h, w, h);
			}

			/// <summary>One line about the pet now: focusing, its needs, or ready.</summary>
			private static string PetLine()
			{
				if (Pomodoro.Current != Pomodoro.Phase.Off) return "🍅 " + Pomodoro.Status();
				if (HideHotkey.Hidden) return Lang.T("home.hidden");
				return Lang.T("home.ready");
			}

			private static string MoodToday()
			{
				return DesktopPet.Mood.History.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out int v) && v >= 0 && v < DesktopPet.Mood.Faces.Length ? DesktopPet.Mood.Faces[v] : "—";
			}

			/// <summary>Reminders still due today, in order.</summary>
			private static List<(DateTime at, string text)> TodayReminders()
			{
				var now = DateTime.Now; var end = DateTime.Today.AddDays(1);
				var list = new List<(DateTime, string)>();
				foreach (var r in Reminders.All.Where(r => r.Enabled))
				{
					var t = r.Next(now);
					if (t.HasValue && t.Value < end) list.Add((t.Value, r.Text));
				}
				return list.OrderBy(x => x.Item1).ToList();
			}

			private static List<(string when, string what)> NextItems()
			{
				var items = new List<(DateTime at, string when, string what)>();
				foreach (var (at, text) in TodayReminders().Take(4)) items.Add((at, at.ToString("HH:mm"), "⏰ " + text));
				var meeting = Agenda.Configured ? Agenda.Next : null;
				if (meeting != null && meeting.Start < DateTime.Now.AddDays(1))
					items.Add((meeting.Start, (meeting.Start.Date == DateTime.Today ? "" : Lang.T("home.tomorrow") + " ") + meeting.Start.ToString("HH:mm"), "📅 " + meeting.Title));
				var list = items.OrderBy(i => i.at).Take(5).Select(i => (i.when, i.what)).ToList();
				var w = Weather.Cached; var city = Weather.City;
				if (Weather.Enabled && w != null && city != null)
				{
					var (key, emoji) = Weather.Describe(w.Code);
					list.Insert(0, (Lang.T("home.today"), emoji + " " + Lang.T(key) + $" · {w.Min}° / {w.Max}°" + (w.RainChance >= 40 ? " · ☔ " + w.RainChance + "%" : "") + " · " + city.Name.Split(',')[0]));
				}
				return list;
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				string h = buttons.FirstOrDefault(b => b.rect.Contains(e.Location)).id;
				Cursor = h != null ? Cursors.Hand : Cursors.Default;
				if (h != hover) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				var b = buttons.FirstOrDefault(x => x.rect.Contains(e.Location));
				b.run?.Invoke();
				Invalidate();
			}
		}

		/// <summary>The picture of the pet in use (gallery card, or the built-in eSheep).</summary>
		private Image CurrentPetImage()
		{
			string folder = petsView?.CurrentPet ?? "";
			var pet = WebPets.pets.FirstOrDefault(p => string.Equals(p.folder, folder, StringComparison.OrdinalIgnoreCase));
			if (pet?.Image != null) return pet.Image;
			try { return BuiltInPets.GetIcon(BuiltInPets.IsBuiltIn(folder) ? folder : "esheep64"); } catch (Exception) { return null; }
		}
	}
}
