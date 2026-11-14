using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	public sealed partial class FormSettings
	{
		// ================================================================== Plugins and Integrations: cards

		/// <summary>
		/// One feature shown as a card: icon, name, what it does, on/off and "Configure". The features are part of the
		/// program (nothing is downloaded); the card only gathers each one's switch and options in one place.
		/// </summary>
		private sealed class Card
		{
			public string Id, Emoji, Key, Category;
			/// <summary>On/off; null = always available (used from the pet menu).</summary>
			public Func<bool> Get;
			public Action<bool> Set;
			/// <summary>"Configure" (null = nothing to set).</summary>
			public Action Configure;
			/// <summary>A short line under the name ("São Paulo", "connected"…), optional.</summary>
			public Func<string> Status;
			public string ConfigureKey = "plugins.configure";
			/// <summary>A drawn icon instead of the emoji (the Claude logo), optional.</summary>
			public Action<Graphics, Rectangle> Icon;
			public string TitleKey;
			public string Title => Lang.T(TitleKey ?? Key + ".title");
			public string Description => Lang.T(Key + ".desc");
			public Rectangle Rect, Toggle, Button;
		}

		private sealed class CardsView : ScrollSurface
		{
			private readonly FormSettings form;
			private readonly Func<List<Card>> source;
			private readonly string[] categories;
			private List<Card> cards;
			private string category = "all";
			private readonly List<(Rectangle rect, string cat)> chips = new List<(Rectangle, string)>();
			private object hover;
			private Font emojiFont;
			private const int CardH = 190, Gap = 14, MinCardW = 300;

			public CardsView(FormSettings f, Func<List<Card>> cards, params string[] categories)
			{
				form = f; source = cards; this.categories = categories;
			}

			public void Reload() { cards = null; Invalidate(); }

			private List<Card> Cards => cards ?? (cards = source());

			protected override int PaintContent(Graphics g, int scrollY)
			{
				emojiFont = emojiFont ?? Theme.CreateFont(22);
				int y = 6 - scrollY;
				int w = Width - ScrollBarWidth - 16;

				// --- category chips
				chips.Clear();
				if (categories.Length > 0)
				{
					int cx = 8;
					foreach (string cat in new[] { "all" }.Concat(categories))
					{
						string text = Lang.T("plugins.cat." + cat) + "  " + (cat == "all" ? Cards.Count : Cards.Count(c => c.Category == cat));
						int cw = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 26;
						var r = new Rectangle(cx, y, cw, 30);
						bool on = category == cat;
						Theme.FillRounded(g, on ? Theme.Accent : (hover is string h && h == cat ? Theme.Hover : Theme.Surface), r, 15);
						if (!on) Theme.DrawRounded(g, Theme.Border, r, 15);
						Theme.DrawText(g, text, form.fontSmall, r, on ? Theme.AccentText : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
						chips.Add((r, cat));
						cx += cw + 8;
					}
					y += 46;
				}

				// --- cards, as many columns as fit
				var shown = Cards.Where(c => category == "all" || c.Category == category).ToList();
				int cols = Math.Max(1, (w + Gap) / (MinCardW + Gap));
				int cardW = (w - (cols - 1) * Gap) / cols;
				// each row is as tall as its longest text needs (the whole description is always shown)
				for (int row = 0; row * cols < shown.Count; row++)
				{
					var inRow = shown.Skip(row * cols).Take(cols).ToList();
					int textH = inRow.Max(c => Theme.MeasureText(g, c.Description, form.fontSmall, cardW - 32, TextFormatFlags.WordBreak).Height);
					int h = Math.Max(CardH, 76 + textH + 14 + 50);
					for (int i = 0; i < inRow.Count; i++)
					{
						var c = inRow[i];
						c.Rect = new Rectangle(8 + i * (cardW + Gap), y, cardW, h);
						PaintCard(g, c, textH);
					}
					y += h + Gap;
				}
				foreach (var c in Cards.Except(shown)) { c.Rect = c.Toggle = c.Button = Rectangle.Empty; }
				return y + scrollY + 8;
			}

			private void PaintCard(Graphics g, Card c, int textH)
			{
				var r = c.Rect;
				bool hot = hover == c;
				bool on = c.Get == null || c.Get();
				Theme.FillRounded(g, Theme.Surface, r, 12);
				Theme.DrawRounded(g, hot ? Theme.Accent : Theme.Border, r, 12);

				// icon
				var icon = new Rectangle(r.X + 16, r.Y + 16, 48, 48);
				Theme.FillRounded(g, Theme.AccentSoft, icon, 12);
				Theme.DrawText(g, c.Emoji, emojiFont, icon, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

				// name + pills (state and a short status)
				int tx = icon.Right + 14, tw = r.Right - tx - 14;
				Theme.DrawText(g, c.Title, form.fontBold, new Rectangle(tx, r.Y + 16, tw, 22), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				if (c.Icon != null) { Theme.FillRounded(g, Theme.Surface, icon, 12); c.Icon(g, icon); }   // a drawn logo instead of the emoji
				int px = tx;
				string state = c.Get == null ? Lang.T("plugins.always") : on ? Lang.T("plugins.on") : Lang.T("plugins.off");
				string status0 = c.Status?.Invoke();
				if (c.Get != null || string.IsNullOrEmpty(status0))   // always available with its own status: the status says it all
					px = Pill(g, px, r.Y + 42, state, on ? Theme.AccentSoft : Theme.SurfaceAlt, on ? Theme.Accent : Theme.TextMuted);
				string status = c.Status?.Invoke();
				if (!string.IsNullOrEmpty(status)) Pill(g, px == tx ? px : px + 6, r.Y + 42, status, Theme.SurfaceAlt, Theme.TextMuted, r.Right - px - 26);

				// what it does: the whole text
				var desc = new Rectangle(r.X + 16, r.Y + 76, r.Width - 32, textH + 4);
				Theme.DrawText(g, c.Description, form.fontSmall, desc, Theme.TextMuted, TextFormatFlags.WordBreak);

				// footer: switch on the left, "Configure" on the right
				int fy = r.Bottom - 44;
				using (var p = new Pen(Theme.Border)) g.DrawLine(p, r.X + 12, fy - 6, r.Right - 12, fy - 6);
				if (c.Get != null)
				{
					c.Toggle = new Rectangle(r.X + 16, fy + 8, 44, 22);
					Theme.DrawToggle(g, c.Toggle, on);
					Theme.DrawText(g, on ? Lang.T("opt.on") : Lang.T("opt.off"), form.fontSmall, new Rectangle(c.Toggle.Right + 8, fy + 6, 90, 26), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
				else c.Toggle = Rectangle.Empty;
				if (c.Configure != null)
				{
					string label = Lang.T(c.ConfigureKey);
					int bw = Theme.MeasureText(g, label, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 30;
					c.Button = new Rectangle(r.Right - 16 - bw, fy + 4, bw, 30);
					Theme.DrawButton(g, c.Button, label, form.fontSmall, false, hover == c && c.Button.Contains(PointToClient(MousePosition)));
				}
				else c.Button = Rectangle.Empty;
			}

			private int Pill(Graphics g, int x, int y, string text, Color back, Color fore, int max = 400)
			{
				int w = Math.Min(max, Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 16);
				if (w < 30) return x;
				var r = new Rectangle(x, y, w, 22);
				Theme.FillRounded(g, back, r, 11);
				Theme.DrawText(g, text, form.fontSmall, r, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				return r.Right;
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				object h = (object)chips.FirstOrDefault(c => c.rect.Contains(e.Location)).cat ?? Cards.FirstOrDefault(c => c.Rect.Contains(e.Location));
				bool hand = h is string || (h is Card c && (c.Toggle.Contains(e.Location) || c.Button.Contains(e.Location)));
				Cursor = hand ? Cursors.Hand : Cursors.Default;
				if (!Equals(h, hover)) { hover = h; Invalidate(); }
				else if (h is Card) Invalidate();
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				foreach (var (rect, cat) in chips) if (rect.Contains(e.Location)) { category = cat; ScrollY = 0; Invalidate(); return; }
				var card = Cards.FirstOrDefault(c => c.Rect.Contains(e.Location));
				if (card == null) return;
				if (card.Toggle.Contains(e.Location) && card.Set != null)
				{
					bool on = !card.Get();
					card.Set(on);
					form.StatusText = Lang.T(card.Get() ? "status.enabled" : "status.disabled", card.Title);
					Invalidate();
				}
				else if (card.Button.Contains(e.Location)) { card.Configure(); Invalidate(); }
			}

			/// <summary>Self-test: the cards and where they are.</summary>
			internal List<Card> TestCards => Cards;
		}

		/// <summary>"Configure" of a card: the options of that feature, in a small window (same rows as the settings).</summary>
		private sealed class PluginDialog : PetDialog
		{
			public PluginDialog(FormSettings form, string titleKey, Action<OptionsView> build)
			{
				Text = Lang.T(titleKey);
				Icon = form.Icon;
				FormBorderStyle = FormBorderStyle.FixedDialog;
				MaximizeBox = MinimizeBox = false;
				ShowInTaskbar = false;
				StartPosition = FormStartPosition.CenterParent;
				var view = new OptionsView(form) { Dock = DockStyle.Fill };
				build(view);
				ClientSize = new Size(680, Math.Min(560, 40 + view.RowCount * 92));
				BackColor = Theme.Background;
				Theme.ApplyWindowChrome(this);
				Controls.Add(view);
			}
		}

		private void ShowPluginOptions(string titleKey, Action<OptionsView> build)
		{
			using (var d = new PluginDialog(this, titleKey, build)) d.ShowDialog(this);
			pluginsView?.Reload();
			integrationsView?.Reload();
			homeView?.Invalidate();
		}

		// ------------------------------------------------------------------ the plugins

		private List<Card> BuildPlugins()
		{
			int[] focus = { 15, 25, 45, 50 }, pause = { 5, 10, 15 }, hours = { 10, 13, 15, 17 }, lead = { 5, 10, 15 };
			int[] battery = { 15, 20, 30, 40 }, quietFrom = { -1, 21, 22, 23, 0 }, quietTo = { 6, 7, 8, 9 }, memLimit = { 70, 80, 85, 90 };
			return new List<Card>
			{
				new Card { Id = "weather", Emoji = "🌦️", Key = "plug.weather", Category = "day",
					Get = () => Weather.Enabled, Set = on => { if (on && Weather.City == null) ChooseCity(); else Weather.Enabled = on; },
					Status = () => Weather.City?.Name.Split(',')[0],
					Configure = () => ShowPluginOptions("plug.weather.title", v =>
					{
						v.AddAction("\uE707", /* map */ "plug.weatherCity", "plug.weatherCity.button", () => { ChooseCity(); v.Invalidate(); });
						v.AddToggle("\uE706", /* sun */ "plug.weatherMorning", () => Weather.Morning, on => Weather.Morning = on);
						v.AddToggle("\uE753", /* rain */ "plug.weatherRain", () => Weather.RainAlerts, on => Weather.RainAlerts = on);
						v.AddAction("\uE768", /* play */ "plug.weatherNow", "plug.weatherNow.button", Weather.SayNow);
					}) },
				new Card { Id = "reminders", Emoji = "⏰", Key = "plug.reminders", Category = "day", Get = () => PluginSwitch.On("reminders"), Set = on => PluginSwitch.Set("reminders", on),
					Status = () => Lang.T("plug.reminders.count", Reminders.All.Count(r => r.Enabled)),
					ConfigureKey = "plugins.open", Configure = () => SelectPage(Page.Reminders) },
				new Card { Id = "agenda", Emoji = "📅", Key = "opt.agenda", Category = "day",
					Get = () => Agenda.Configured, Set = on => { if (on) ConfigureAgenda(); else { Agenda.Url = ""; StatusText = Lang.T("agenda.removed"); } },
					Configure = () => ShowPluginOptions("opt.agenda.title", v =>
					{
						v.AddAction("\uE787", /* calendar */ "opt.agenda", "opt.agenda.button", ConfigureAgenda);
						v.AddChoice("\uE917", /* clock */ "opt.agendaLead", () => Agenda.LeadMinutes, x => Agenda.LeadMinutes = x, lead, lead.Select(m => m + " min").ToArray());
					}) },
				new Card { Id = "mail", Emoji = "✉️", Key = "opt.mail", Category = "day",
					Get = () => OutlookMail.Enabled, Set = on => OutlookMail.Enabled = on },
				new Card { Id = "focus", Emoji = "🍅", Key = "plug.focus", Category = "day", Get = () => PluginSwitch.On("focus"), Set = on => PluginSwitch.Set("focus", on),
					Status = () => Pomodoro.Current == Pomodoro.Phase.Off ? Pomodoro.FocusMinutes + " min" : Pomodoro.Status(),
					Configure = () => ShowPluginOptions("plug.focus.title", v =>
					{
						v.AddChoice("\uE916", /* stopwatch */ "opt.pomoFocus", () => Program.MyData.GetSettingInt("PomodoroFocus", 25), x => Program.MyData.SetSetting("PomodoroFocus", x), focus, focus.Select(m => m + " min").ToArray());
						v.AddChoice("\uE708", /* moon */ "opt.pomoBreak", () => Program.MyData.GetSettingInt("PomodoroBreak", 5), x => Program.MyData.SetSetting("PomodoroBreak", x), pause, pause.Select(m => m + " min").ToArray());
					}) },
				new Card { Id = "care", Emoji = "🚶", Key = "opt.activity", TitleKey = "plug.care.title", Category = "care",
					Get = () => Activity.Enabled, Set = on => Activity.Enabled = on },
				new Card { Id = "mood", Emoji = "😊", Key = "opt.mood", Category = "care",
					Get = () => Mood.Daily, Set = on => Mood.Daily = on,
					Configure = () => ShowPluginOptions("opt.mood.title", v =>
					{
						v.AddChoice("\uE917", /* clock */ "opt.moodHour", () => Mood.Hour, x => Mood.Hour = x, hours, hours.Select(h => h + "h").ToArray());
						v.AddAction("\uE81C", /* history */ "opt.moodHistory", "opt.moodHistory.button", () =>
							PetMessage.Show(this, Lang.T("mood.history", Mood.Recent(14)), Lang.T("opt.moodHistory.title"), MessageBoxButtons.OK, MessageBoxIcon.None));
					}) },
				new Card { Id = "vpet", Emoji = "🍖", Key = "opt.vpet", Category = "fun",
					Get = () => VirtualPet.Enabled, Set = on => VirtualPet.Enabled = on },
				new Card { Id = "meet", Emoji = "🐑", Key = "opt.petsMeet", Category = "fun",
					Get = () => PetsMeet.Enabled, Set = on => PetsMeet.Enabled = on },
				new Card { Id = "games", Emoji = "🥠", Key = "plug.games", Category = "fun", Get = () => PluginSwitch.On("games"), Set = on => PluginSwitch.Set("games", on) },
				new Card { Id = "monitor", Emoji = "📊", Key = "opt.monitor", Category = "pc",
					Get = () => PcMonitor.Enabled, Set = on => PcMonitor.Enabled = on },
				new Card { Id = "pcEvents", Emoji = "🔋", Key = "opt.pcEvents", Category = "pc",
					Get = () => ComputerEvents.Enabled, Set = on => ComputerEvents.Enabled = on,
					Status = () => Lang.T("plug.battery.status", ComputerEvents.BatteryLow),
					Configure = () => ShowPluginOptions("opt.pcEvents.title", v =>
					{
						v.AddChoice("", /* battery */ "plug.batteryLow", () => ComputerEvents.BatteryLow, x => ComputerEvents.BatteryLow = x, battery, battery.Select(b => b + "%").ToArray());
						v.AddToggle("", /* plug */ "plug.plugNotices", () => ComputerEvents.PlugNotices, on => ComputerEvents.PlugNotices = on);
					}) },
				new Card { Id = "shortcuts", Emoji = "🚀", Key = "opt.shortcuts", Category = "pc", Get = () => PluginSwitch.On("shortcuts"), Set = on => PluginSwitch.Set("shortcuts", on),
					Status = () => Lang.T("plug.shortcuts.count", Launchers.All.Count),
					ConfigureKey = "opt.shortcuts.button", Configure = () => { using (var f = new FormLaunchers()) f.ShowDialog(this); } },

				// ---- added in 2.7
				new Card { Id = "notes", Emoji = "📝", Key = "plug.notes", Category = "day", Get = () => PluginSwitch.On("notes"), Set = on => PluginSwitch.Set("notes", on),
					Status = () => Notes.Counting ? "⏳ " + Notes.Remaining : Lang.T("plug.notes.count", Notes.OpenTasks),
					ConfigureKey = "plugins.open", Configure = Notes.Open },
				new Card { Id = "fullscreen", Emoji = "🎬", Key = "plug.fullscreen", Category = "pc",
					Get = () => FullScreenWatch.Enabled, Set = on => FullScreenWatch.Enabled = on,
					Status = () => FullScreenWatch.Active ? Lang.T("plug.fullscreen.hidden") : FullScreenWatch.Dnd ? Lang.T("plug.fullscreen.dnd") : null },
				new Card { Id = "notices", Emoji = "🔔", Key = "plug.notices", Category = "day",
					Get = () => NoticeOptions.ToastsWhenHidden, Set = on => NoticeOptions.ToastsWhenHidden = on,
					Status = () => NoticeOptions.QuietFrom < 0 ? null : Lang.T("plug.quiet.status", NoticeOptions.QuietFrom, NoticeOptions.QuietTo),
					Configure = () => ShowPluginOptions("plug.notices.title", v =>
					{
						v.AddToggle("", /* notification */ "plug.toasts", () => NoticeOptions.ToastsWhenHidden, on => NoticeOptions.ToastsWhenHidden = on);
						v.AddChoice("", /* moon */ "plug.quietFrom", () => NoticeOptions.QuietFrom, x => NoticeOptions.QuietFrom = x, quietFrom, quietFrom.Select(h => h < 0 ? Lang.T("plug.quietOff") : h + "h").ToArray());
						v.AddChoice("", /* sun */ "plug.quietTo", () => NoticeOptions.QuietTo, x => NoticeOptions.QuietTo = x, quietTo, quietTo.Select(h => h + "h").ToArray());
					}) },
				new Card { Id = "clipboard", Emoji = "📋", Key = "plug.clipboard", Category = "pc",
					Get = () => ClipboardWatch.Enabled, Set = on => ClipboardWatch.Enabled = on,
					Status = () => ClipboardWatch.Enabled ? Lang.T("plug.clipboard.count", ClipboardWatch.History.Count) : null,
					Configure = () => ShowPluginOptions("plug.clipboard.title", v =>
					{
						v.AddToggle("", /* message */ "plug.clipOffers", () => ClipboardWatch.Offers, on => ClipboardWatch.Offers = on);
						v.AddAction("", /* history */ "plug.clipHistory", "plug.clipHistory.button", () => ClipboardWatch.ShowHistory(this));
					}) },
				new Card { Id = "music", Emoji = "🎵", Key = "plug.music", Category = "fun",
					Get = () => MusicWatch.Enabled, Set = on => MusicWatch.Enabled = on,
					Status = () => { string s = MusicWatch.Enabled ? MusicWatch.Playing() : ""; return s.Length == 0 ? null : "🎵 " + s; },
					Configure = () => ShowPluginOptions("plug.music.title", v => v.AddToggle("", /* audio */ "plug.musicTitle", () => MusicWatch.SayTitle, on => MusicWatch.SayTitle = on)) },
				new Card { Id = "visitors", Emoji = "🦋", Key = "plug.visitors", Category = "fun",
					Get = () => Visitors.Enabled, Set = on => Visitors.Enabled = on,
					ConfigureKey = "plug.visitors.button", Configure = Visitors.Visit },
				new Card { Id = "push", Emoji = "🪟", Key = "plug.push", Category = "fun",
					Get = () => WindowPush.Enabled, Set = on => WindowPush.Enabled = on },
				new Card { Id = "timer", Emoji = "⏱️", Key = "plug.timer", Category = "day",
					Get = () => PluginSwitch.On("timer"), Set = on => { PluginSwitch.Set("timer", on); if (!on) SimpleTimer.Close(); },
					Status = () => SimpleTimer.Active ? "⏱ " + (SimpleTimer.Finished ? Lang.T("timer.doneShort") : SimpleTimer.LeftText) : null,
					ConfigureKey = "plug.timer.button", Configure = () => { if (!PluginSwitch.On("timer")) PluginSwitch.Set("timer", true); SimpleTimer.AskAndStart(); } },
				new Card { Id = "memory", Emoji = "💾", Key = "plug.memory", Category = "pc", Icon = DrawRamIcon,
					Get = () => MemoryCare.Enabled, Set = on => MemoryCare.Enabled = on,
					Status = () => Lang.T("plug.memory.status", MemoryCare.Status().load),
					Configure = () => ShowPluginOptions("plug.memory.title", v =>
					{
						v.AddChoice("", /* chart */ "plug.memoryLimit", () => MemoryCare.Limit, x => MemoryCare.Limit = x, memLimit, memLimit.Select(p => p + "%").ToArray());
						v.AddToggle("", /* sync */ "plug.memoryAuto", () => MemoryCare.Auto, on => MemoryCare.Auto = on);
						v.AddAction("", /* broom-ish: delete */ "plug.memoryTidy", "plug.memoryTidy.button", () => MemoryCare.Tidy(false));
						v.AddAction("", /* admin shield */ "plug.memoryFull", "plug.memoryFull.button", () => MemoryCare.Tidy(true));
					}) },
			};
		}

		/// <summary>Ask the city, find it on Open-Meteo, turn the weather on.</summary>
		private async void ChooseCity()
		{
			string name = FormLaunchers.Prompt(Lang.T("weather.askCity"), Weather.City?.Name.Split(',')[0] ?? "");
			if (string.IsNullOrWhiteSpace(name)) return;
			StatusText = Lang.T("weather.searching");
			try
			{
				var place = await Weather.FindCity(name);
				if (place == null) { StatusText = Lang.T("weather.notFound", name); return; }
				Weather.City = place;
				Weather.Enabled = true;
				StatusText = Lang.T("weather.cityOk", place.Name);
				Weather.SayNow();
			}
			catch (Exception ex) { StatusText = Lang.T("weather.error", ex.Message); }
			pluginsView?.Reload();
			homeView?.Invalidate();
		}

		// ------------------------------------------------------------------ integrations

		/// <summary>The AI service lives in Assistant; here only Claude Code, everything of it in one card.</summary>
		private List<Card> BuildIntegrations() => new List<Card>
		{
			new Card { Id = "claude", Emoji = "🤖", Key = "int.claude", Icon = DrawClaudeLogo,
				Get = () => AgentBridge.Enabled && ClaudeHooks.IsInstalled(), Set = SetClaude,
				Status = () => Lang.T(ClaudeMcp.IsRegistered() ? "int.mcpOn" : "int.mcpOff"),
				Configure = ShowClaude },
			new Card { Id = "agents", Emoji = "🧑‍💻", Key = "int.agents",
				Get = () => OtherAgents.AnyInstalled, Set = on => { if (on) ShowAgents(); else { OtherAgents.SetCodex(false); OtherAgents.SetCursor(false); } },
				Status = () => AgentLevel.Enabled ? Lang.T("int.agents.level", AgentLevel.Level, AgentLevel.Tasks) : null,
				Configure = ShowAgents },
			new Card { Id = "api", Emoji = "🔌", Key = "int.api",
				Get = () => LocalApi.Enabled, Set = on => LocalApi.Enabled = on,
				Status = () => LocalApi.Enabled && AgentBridge.Listening ? "127.0.0.1:" + AgentBridge.Port : null,
				Configure = () => ShowPluginOptions("int.api.title", v =>
				{
					v.AddToggle("", /* network */ "int.apiOn", () => LocalApi.Enabled, on => { LocalApi.Enabled = on; v.Invalidate(); });
					v.AddAction("", /* copy */ "int.apiExample", "int.copyCmd.button", () => CopyText(LocalApi.Example));
					v.AddAction("", /* folder */ "int.apiFile", "opt.logs.button", () =>
					{
						try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(AgentBridge.ConnectionFile)) { UseShellExecute = true }); }
						catch (Exception skipped) { LogFile.Ignored(skipped); }
					});
				}) },
		};

		/// <summary>Codex and Cursor: their switches, the command for any other program, the pet's level.</summary>
		private void ShowAgents()
		{
			ShowPluginOptions("int.agents.title", v =>
			{
				v.AddToggle("", /* console */ "int.codex", () => OtherAgents.CodexInstalled, on => { Report(OtherAgents.SetCodex(on)); v.Invalidate(); });
				v.AddToggle("", /* code */ "int.cursor", () => OtherAgents.CursorInstalled, on => { Report(OtherAgents.SetCursor(on)); v.Invalidate(); });
				v.AddAction("", /* copy */ "int.agentCmd", "int.copyCmd.button", () => CopyText(OtherAgents.Command("done")));
				v.AddToggle("", /* star */ "int.level", () => AgentLevel.Enabled, on => AgentLevel.Enabled = on);
			});
			void Report(string key) { if (key != null) PetMessage.Show(this, Lang.T(key), Lang.T("int.agents.title"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
		}

		private void CopyText(string text)
		{
			try { ClipboardWatch.Copy(text); StatusText = Lang.T("int.copied"); }
			catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		/// <summary>Claude Code in one window: connection, reactions (hooks), messages (MCP) and advanced.</summary>
		private void ShowClaude()
		{
			ShowPluginOptions("int.claude.title", BuildClaude);
		}

		private void BuildClaude(OptionsView v)
		{
			string claudeDir = System.IO.Path.GetDirectoryName(ClaudeHooks.DefaultPath);
				v.AddHeader("int.secConnection");
				v.AddStatus("\uE7F4", /* desktop */ "int.cli", () =>
				{
					string exe = ClaudeMcp.FindClaude();
					return exe != null ? Lang.T("int.cliFound", exe) : Lang.T("int.cliMissing");
				}, "int.refresh", () => v.Invalidate());
				v.AddStatus("\uE968", /* network */ "int.listener", () =>
					AgentBridge.Listening ? Lang.T("int.listening", AgentBridge.Port) : Lang.T("int.notListening"), "int.refresh", () => v.Invalidate());
				v.AddHeader("int.secReactions");
				v.AddToggle("\uE99A", /* robot */ "opt.claude", () => AgentBridge.Enabled && ClaudeHooks.IsInstalled(), on => { SetClaude(on); v.Invalidate(); });
				v.AddToggle("\uE8BD", /* message */ "opt.claudeBubbles", () => AgentBridge.ProgressBubbles, on => AgentBridge.ProgressBubbles = on);
				v.AddAction("\uE768", /* play */ "opt.claudeTest", "opt.claudeTest.button", TestClaude);
				v.AddHeader("int.secMcp");
				v.AddToggle("\uE8F2", /* chat */ "opt.claudeMcp", ClaudeMcp.IsRegistered, on => { SetClaudeMcp(on); v.Invalidate(); });
				v.AddAction("\uE8C8", /* copy */ "int.copyCmd", "int.copyCmd.button", () =>
				{
					try { Clipboard.SetText(ClaudeMcp.ManualCommand); StatusText = Lang.T("int.copied"); } catch (Exception skipped) { LogFile.Ignored(skipped); }
				});
				v.AddHeader("int.secAdvanced");
				v.AddAction("\uE838", /* folder */ "int.folder", "opt.logs.button", () =>
				{
					try { System.IO.Directory.CreateDirectory(claudeDir); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(claudeDir) { UseShellExecute = true }); } catch (Exception skipped) { LogFile.Ignored(skipped); }
				});
		}

		/// <summary>The official Claude icon (Images\claude.png, embedded as "img/claude.png"), loaded once.</summary>
		private static readonly Lazy<Image> claudeIcon = new Lazy<Image>(() =>
		{
			try
			{
				using (var s = typeof(FormSettings).Assembly.GetManifestResourceStream("img/claude.png"))
					return s == null ? null : new Bitmap(s);
			}
			catch (Exception ex) { LogFile.Write("claude icon: " + ex.Message); return null; }
		});

		/// <summary>A memory (RAM) stick: there is no emoji for it. Green board, black chips, gold contacts and the notch.</summary>
		internal static void DrawRamIcon(Graphics g, Rectangle r)
		{
			var oldSmooth = g.SmoothingMode;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			float w = r.Width * 0.84f, h = w * 0.42f;
			float x = r.X + (r.Width - w) / 2, y = r.Y + (r.Height - h) / 2;
			using (var board = new SolidBrush(Color.FromArgb(46, 140, 82)))
			using (var edge = new Pen(Color.FromArgb(26, 92, 52), Math.Max(1f, w / 40)))
			using (var chip = new SolidBrush(Color.FromArgb(38, 38, 44)))
			using (var gold = new SolidBrush(Color.FromArgb(232, 184, 64)))
			using (var notch = new SolidBrush(Theme.Surface))
			{
				g.FillRectangle(board, x, y, w, h);
				g.DrawRectangle(edge, x, y, w, h);
				// four chips
				float cw = w * 0.17f, ch = h * 0.46f, gap = (w - 4 * cw) / 5;
				for (int i = 0; i < 4; i++) g.FillRectangle(chip, x + gap + i * (cw + gap), y + h * 0.14f, cw, ch);
				// gold contacts along the bottom edge
				float pinY = y + h * 0.74f, pinH = h * 0.2f;
				int pins = 9;
				float pw = w / (pins * 2 + 1);
				for (int i = 0; i < pins; i++) g.FillRectangle(gold, x + pw * (1 + i * 2), pinY, pw, pinH);
				// the key notch, a little off center
				g.FillRectangle(notch, x + w * 0.58f, y + h * 0.84f, w * 0.06f, h * 0.17f);
			}
			g.SmoothingMode = oldSmooth;
		}

		/// <summary>The Claude mark: the embedded icon, or a drawn starburst if it is missing.</summary>
		private static void DrawClaudeLogo(Graphics g, Rectangle r)
		{
			var img = claudeIcon.Value;
			if (img != null)
			{
				var oldMode = g.InterpolationMode;
				var oldOffset = g.PixelOffsetMode;
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
				g.DrawImage(img, r);
				g.InterpolationMode = oldMode;
				g.PixelOffsetMode = oldOffset;
				return;
			}
			DrawClaudeStarburst(g, r);
		}

		/// <summary>Fallback: an orange rounded square with a white starburst.</summary>
		private static void DrawClaudeStarburst(Graphics g, Rectangle r)
		{
			var old = g.SmoothingMode;
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			Theme.FillRounded(g, Color.FromArgb(217, 119, 87), r, r.Width * 0.24f);
			float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
			using (var b = new SolidBrush(Color.White))
				for (int i = 0; i < 12; i++)
				{
					double a = i * Math.PI / 6 + 0.12;
					float len = r.Width * (i % 2 == 0 ? 0.36f : 0.30f), half = r.Width * 0.045f;
					var tip = new PointF(cx + (float)Math.Cos(a) * len, cy + (float)Math.Sin(a) * len);
					var l = new PointF(cx + (float)Math.Cos(a + Math.PI / 2) * half, cy + (float)Math.Sin(a + Math.PI / 2) * half);
					var rr = new PointF(cx - (float)Math.Cos(a + Math.PI / 2) * half, cy - (float)Math.Sin(a + Math.PI / 2) * half);
					g.FillPolygon(b, new[] { l, tip, rr });
				}
			using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - r.Width * 0.07f, cy - r.Width * 0.07f, r.Width * 0.14f, r.Width * 0.14f);
			g.SmoothingMode = old;
		}
	}
}

namespace DesktopPet
{
	public sealed partial class FormSettings
	{
		/// <summary>
		/// Self-test: open the window off screen, draw every page (Home, Plugins, Integrations, Settings…), switch one
		/// card off and on again, and check that the old pages lead to the new ones. Returns (check, ok, detail).
		/// </summary>
		internal static List<(string name, bool ok, string detail)> SelfCheck()
		{
			var r = new List<(string, bool, string)>();
			using (var f = new FormSettings(Page.Home) { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000) })
			{
				f.Show();
				foreach (Page p in new[] { Page.Home, Page.Plugins, Page.Integrations, Page.Application, Page.Ai, Page.Reminders, Page.About })
				{
					string err = null;
					try
					{
						f.SelectPage(p);
						using (var bmp = new Bitmap(f.Width, f.Height)) f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.Size));
					}
					catch (Exception ex) { err = ex.GetType().Name + ": " + ex.Message; }
					r.Add(("page draws: " + p, err == null, err ?? ""));
				}
				f.SelectPage(Page.Extras);
				Page shown = f.page;   // a copy: calling a member on a Form's field directly gives warning CS1690
				r.Add(("old Extras page opens Plugins", shown == Page.Plugins, shown.ToString()));
				f.SelectPage(Page.Behaviour);
				shown = f.page;
				r.Add(("old Behaviour page opens Settings", shown == Page.Application, shown.ToString()));

				var cards = f.pluginsView.TestCards;
				r.Add(("plugins: cards", cards.Count >= 12, cards.Count.ToString()));
				r.Add(("plugins: weather card", cards.Any(c => c.Id == "weather"), ""));
				r.Add(("plugins: every card has a text", cards.All(c => c.Title != c.Key + ".title" && c.Description != c.Key + ".desc"),
					string.Join(", ", cards.Where(c => c.Title == c.Key + ".title" || c.Description == c.Key + ".desc").Select(c => c.Key))));
				var meet = cards.First(c => c.Id == "meet");
				bool was = meet.Get();
				meet.Set(!was); bool flipped = meet.Get() == !was;
				meet.Set(was);
				r.Add(("plugins: a card switches the feature", flipped && meet.Get() == was, ""));
				var ints = f.integrationsView.TestCards;
				r.Add(("integrations: Claude Code first (one card), other agents and the local API", ints.Count == 3 && ints[0].Id == "claude" && ints[0].Icon != null && ints[1].Id == "agents" && ints[2].Id == "api", string.Join(",", ints.Select(c => c.Id))));
				r.Add(("plugins: every card can be switched off", cards.All(c => c.Get != null && c.Set != null), string.Join(", ", cards.Where(c => c.Get == null).Select(c => c.Id))));
				f.Close();
			}
			return r;
		}
	}
}
