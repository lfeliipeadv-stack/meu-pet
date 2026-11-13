using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Add or edit an AI provider: pick the service in cards, paste the key (with a link to get one free), test the
	/// connection (it also picks a model that answers), save. Same look as the rest of the program.
	/// </summary>
	internal sealed class FormAiSettings : PetDialog
	{
		readonly ProviderPicker provider;
		readonly TextBox url = new TextBox { BorderStyle = BorderStyle.None };
		readonly ComboBox model = new ComboBox { FlatStyle = FlatStyle.Flat };
		readonly TextBox key = new TextBox { BorderStyle = BorderStyle.None, UseSystemPasswordChar = true };
		readonly Banner info = new Banner();
		readonly LinkLabel getKey = new LinkLabel { AutoSize = true, LinkBehavior = LinkBehavior.HoverUnderline };
		readonly LinkLabel showKey = new LinkLabel { AutoSize = true, LinkBehavior = LinkBehavior.HoverUnderline };
		readonly Button test = new Button { AutoSize = true, MinimumSize = new Size(150, 38) };
		readonly Button save = new Button { AutoSize = true, MinimumSize = new Size(120, 38) };
		readonly Button remove = new Button { AutoSize = true, MinimumSize = new Size(110, 38) };
		readonly Label title = new Label { AutoSize = true }, subtitle = new Label { AutoSize = false }, step1 = new Label { AutoSize = true }, step2 = new Label { AutoSize = true };
		readonly Label keyLabel = new Label { AutoSize = true }, urlLabel = new Label { AutoSize = true }, modelLabel = new Label { AutoSize = true }, keyHelp = new Label { AutoSize = false }, modelHelp = new Label { AutoSize = false };
		readonly Rectangle[] fields = new Rectangle[3];   // the rounded boxes drawn around key, address, model
		readonly bool editing;
		bool tested;
		/// <summary>What the chosen service shows (kept here: Visible reads false until the window is on screen).</summary>
		bool needKey, editUrl, hasKeyLink;
		const int W = 620, Pad = 28;

		public FormAiSettings()
		{
			editing = AiAssistant.ProviderId.Length > 0;
			Text = Lang.T(editing ? "ai.editTitle" : "ai.addTitle");
			Icon = Properties.Resources.icon;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			DoubleBuffered = true;
			BackColor = Theme.Background;
			ClientSize = new Size(W, 600);

			title.Text = Lang.T(editing ? "ai.editTitle" : "ai.addTitle");
			title.Font = Theme.CreateFont(22, FontStyle.Bold);
			subtitle.Text = Lang.T("ai.intro");
			subtitle.Font = Theme.CreateFont(13);
			step1.Text = Lang.T("ai.step1").ToUpperInvariant(); step2.Text = Lang.T("ai.step2").ToUpperInvariant();
			foreach (var l in new[] { step1, step2 }) l.Font = Theme.CreateFont(12, FontStyle.Bold);
			keyLabel.Text = Lang.T("ai.key"); urlLabel.Text = Lang.T("ai.address"); modelLabel.Text = Lang.T("ai.model");
			foreach (var l in new[] { keyLabel, urlLabel, modelLabel }) l.Font = Theme.CreateFont(13, FontStyle.Bold);
			keyHelp.Text = Lang.T("ai.keyHelp"); modelHelp.Text = Lang.T("ai.modelAuto");
			foreach (var l in new[] { keyHelp, modelHelp }) l.Font = Theme.CreateFont(12);
			foreach (var t in new Control[] { url, key, model }) t.Font = Theme.CreateFont(14);
			getKey.Font = showKey.Font = Theme.CreateFont(13);
			test.Text = Lang.T("ai.test"); save.Text = Lang.T("ai.save"); remove.Text = Lang.T("ai.remove");

			provider = new ProviderPicker { Size = new Size(W - Pad * 2, 112) };
			Controls.AddRange(new Control[] { title, subtitle, step1, provider, step2, keyLabel, getKey, key, showKey, keyHelp, urlLabel, url, modelLabel, model, modelHelp, info, remove, test, save });

			string saved = AiAssistant.ProviderId;
			provider.SelectedIndex = Math.Max(0, Array.FindIndex(AiAssistant.Providers, p => p.Id == (saved.Length > 0 ? saved : "gemini")));
			if (saved.Length > 0)
			{
				url.Text = AiAssistant.BaseUrl;
				model.Text = AiAssistant.Model;
				key.Text = AiAssistant.Key;
				tested = true;
			}
			else LoadPreset();
			provider.Changed += () => { LoadPreset(); tested = false; Refresh2(); };
			url.TextChanged += (s, e) => tested = false;
			key.TextChanged += (s, e) => tested = false;
			getKey.LinkClicked += (s, e) => { var p = Current; if (!string.IsNullOrEmpty(p.KeyUrl)) Program.OpenLink(p.KeyUrl); };
			showKey.LinkClicked += (s, e) => { key.UseSystemPasswordChar = !key.UseSystemPasswordChar; showKey.Text = Lang.T(key.UseSystemPasswordChar ? "ai.showKey" : "ai.hideKey"); };
			test.Click += async (s, e) => await Test();
			save.Click += async (s, e) => await Save();
			remove.Click += (s, e) => { AiAssistant.Clear(); DialogResult = DialogResult.OK; };

			Theme.ApplyTo(this);
			Theme.MakePrimary(save);
			// colors Theme.ApplyTo does not set the way this window wants them
			BackColor = Theme.Background;
			foreach (var t in new Control[] { url, key, model }) { t.BackColor = Theme.Surface; t.ForeColor = Theme.Text; }
			url.BorderStyle = key.BorderStyle = BorderStyle.None;   // the rounded box around them is the border
			subtitle.ForeColor = keyHelp.ForeColor = modelHelp.ForeColor = Theme.TextMuted;
			step1.ForeColor = step2.ForeColor = Theme.Accent;
			getKey.LinkColor = showKey.LinkColor = getKey.ActiveLinkColor = showKey.ActiveLinkColor = Theme.Accent;
			remove.ForeColor = Theme.Danger;
			showKey.Text = Lang.T("ai.showKey");
			Refresh2();
		}

		AiProvider Current => AiAssistant.Providers[Math.Max(0, provider.SelectedIndex)];

		void LoadPreset()
		{
			var p = Current;
			url.Text = p.BaseUrl;
			model.Items.Clear();
			model.Text = p.Model;
			if (p.Id != AiAssistant.ProviderId) key.Text = "";
		}

		/// <summary>Show what the chosen service needs, and lay the window out.</summary>
		void Refresh2()
		{
			var p = Current;
			needKey = p.NeedsKey; editUrl = p.Id == "custom" || p.Id == "ollama"; hasKeyLink = !string.IsNullOrEmpty(p.KeyUrl);
			keyLabel.Visible = key.Visible = showKey.Visible = keyHelp.Visible = needKey;
			urlLabel.Visible = url.Visible = editUrl;
			getKey.Visible = hasKeyLink;
			getKey.Text = (p.Local ? Lang.T("ai.getOllama") : Lang.T("ai.getKey")) + " ↗";
			info.Set(Banner.Kind.Info, p.Local ? Lang.T("ai.infoLocal") : Lang.T("ai.infoCloud", p.Name));
			remove.Visible = editing;
			LayoutAll();
		}

		void LayoutAll()
		{
			int w = W - Pad * 2, y = 22;
			title.Location = new Point(Pad - 2, y); y += 40;
			subtitle.Location = new Point(Pad, y);
			subtitle.Size = new Size(w, TextRenderer.MeasureText(subtitle.Text, subtitle.Font, new Size(w, 0), TextFormatFlags.WordBreak).Height + 4);
			y = subtitle.Bottom + 18;
			step1.Location = new Point(Pad, y); y += 26;
			provider.Location = new Point(Pad, y); y = provider.Bottom + 20;
			step2.Location = new Point(Pad, y); y += 28;

			// a field: label (and a link on the right), the rounded box with the control inside, a help line
			int Field(int index, Label label, Control box, Control right, Label help, int y0)
			{
				label.Location = new Point(Pad, y0);
				if (right != null) right.Location = new Point(W - Pad - right.Width, y0 + 1);
				y0 += 26;
				fields[index] = new Rectangle(Pad, y0, w, 44);
				int inner = box is ComboBox ? w - 20 : w - 28 - (box == key ? showKey.Width + 16 : 0);
				box.Width = inner;
				box.Location = new Point(Pad + 14, y0 + (44 - box.Height) / 2);
				if (box == key) showKey.Location = new Point(W - Pad - showKey.Width - 14, y0 + (44 - showKey.Height) / 2);
				y0 += 50;
				if (help != null)
				{
					help.Location = new Point(Pad + 2, y0 - 2);
					help.Size = new Size(w, TextRenderer.MeasureText(help.Text, help.Font, new Size(w, 0), TextFormatFlags.WordBreak).Height + 2);
					y0 = help.Bottom + 8;
				}
				return y0 + 6;
			}
			fields[0] = fields[1] = fields[2] = Rectangle.Empty;
			if (needKey) y = Field(0, keyLabel, key, hasKeyLink ? getKey : null, keyHelp, y);
			else if (hasKeyLink) { getKey.Location = new Point(Pad, y); y += 30; }
			if (editUrl) y = Field(1, urlLabel, url, null, null, y);
			model.DropDownStyle = ComboBoxStyle.DropDown;
			y = Field(2, modelLabel, model, null, modelHelp, y);

			info.Location = new Point(Pad, y + 4);
			info.Width = w;
			info.Height = info.Needed(w);
			y = info.Bottom + 20;

			save.Location = new Point(W - Pad - save.Width, y);
			test.Location = new Point(save.Left - 10 - test.Width, y);
			remove.Location = new Point(Pad, y);
			ClientSize = new Size(W, save.Bottom + 22);
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			foreach (var r in fields.Where(f => !f.IsEmpty))
			{
				Theme.FillRounded(g, Theme.Surface, r, 10);
				Theme.DrawRounded(g, Theme.Border, r, 10);
			}
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Pad, save.Top - 12, W - Pad, save.Top - 12);
		}

		/// <summary>Connect, list the models and choose one.</summary>
		async Task<bool> Test()
		{
			var p = Current;
			test.Enabled = save.Enabled = false;
			info.Set(Banner.Kind.Busy, Lang.T("ai.testing"));
			LayoutAll();
			try
			{
				if (p.NeedsKey && p.Id != "custom" && key.Text.Trim().Length == 0) throw new InvalidOperationException(Lang.T("ai.needKey"));
				var models = await AiAssistant.ListModels(url.Text.Trim(), key.Text.Trim());
				if (models.Count == 0)
					throw new InvalidOperationException(p.Local ? Lang.T("ai.noModelsOllama") : Lang.T("ai.noModels"));
				// a service may list models it no longer lets you use: ask a tiny question and keep one that answers
				info.Set(Banner.Kind.Busy, Lang.T("ai.testingModels"));
				string pick = await AiAssistant.FindWorkingModel(p, url.Text.Trim(), key.Text.Trim(), model.Text.Trim(), models);
				model.Items.Clear();
				foreach (var m in AiAssistant.Candidates(p, pick, models)) model.Items.Add(m);
				model.Text = pick;
				info.Set(Banner.Kind.Ok, Lang.T("ai.testOk", models.Count, pick));
				tested = true;
				return true;
			}
			catch (HttpRequestException) when (p.Local)
			{
				info.Set(Banner.Kind.Error, Lang.T("ai.ollamaOff"));
			}
			catch (Exception ex)
			{
				info.Set(Banner.Kind.Error, Lang.T("ai.testFail", ex is TaskCanceledException ? Lang.T("ai.timeout") : ex.Message));
			}
			finally { test.Enabled = save.Enabled = true; LayoutAll(); }
			return false;
		}

		async Task Save()
		{
			if (!tested && !await Test()) return;
			AiAssistant.ProviderId = Current.Id;
			AiAssistant.BaseUrl = url.Text.Trim();
			AiAssistant.Model = model.Text.Trim();
			AiAssistant.Key = Current.NeedsKey ? key.Text.Trim() : "";
			DialogResult = DialogResult.OK;
		}

		/// <summary>Self-test: choose a service by its id.</summary>
		internal void TestChoose(string id) { provider.SelectedIndex = Array.FindIndex(AiAssistant.Providers, p => p.Id == id); }
		internal bool TestKeyVisible => key.Visible;
		internal bool TestUrlVisible => url.Visible;

		// ------------------------------------------------------------------ the service cards

		/// <summary>The services as cards: emoji, short name and what it is (free, on this computer…).</summary>
		sealed class ProviderPicker : Control
		{
			int selected, hover = -1;
			public event Action Changed;
			readonly Font name = Theme.CreateFont(13, FontStyle.Bold), tag = Theme.CreateFont(11), emoji = Theme.CreateFont(24);
			static readonly Dictionary<string, (string emoji, string name, string tag)> Look = new Dictionary<string, (string, string, string)>
			{
				["ollama"] = ("🦙", "Ollama", "ai.tag.ollama"),
				["gemini"] = ("✨", "Gemini", "ai.tag.gemini"),
				["groq"] = ("⚡", "Groq", "ai.tag.groq"),
				["openrouter"] = ("🔀", "OpenRouter", "ai.tag.openrouter"),
				["custom"] = ("🔧", "", "ai.tag.custom"),
			};

			public ProviderPicker()
			{
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
				Cursor = Cursors.Hand;
			}

			public int SelectedIndex
			{
				get => selected;
				set { if (value < 0 || value == selected) { selected = Math.Max(0, value); Invalidate(); return; } selected = value; Invalidate(); Changed?.Invoke(); }
			}

			Rectangle Tile(int i)
			{
				int n = AiAssistant.Providers.Length, gap = 10, tw = (Width - gap * (n - 1)) / n;
				return new Rectangle(i * (tw + gap), 0, tw, Height - 1);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				for (int i = 0; i < AiAssistant.Providers.Length; i++)
				{
					var p = AiAssistant.Providers[i];
					var look = Look.TryGetValue(p.Id, out var l) ? l : ("🤖", p.Name, "");
					var r = Tile(i);
					bool sel = i == selected;
					Theme.FillRounded(g, sel ? Theme.AccentSoft : hover == i ? Theme.Hover : Theme.Surface, r, 12);
					Theme.DrawRounded(g, sel ? Theme.Accent : Theme.Border, r, 12);
					if (sel) using (var pen = new Pen(Theme.Accent, 2)) g.DrawPath(pen, Theme.RoundedRect(new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), 11));
					Theme.DrawText(g, look.Item1, emoji, new Rectangle(r.X, r.Y + 10, r.Width, 36), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
					string nm = look.Item2.Length > 0 ? look.Item2 : Lang.T("ai.customShort");
					Theme.DrawText(g, nm, name, new Rectangle(r.X + 4, r.Y + 50, r.Width - 8, 20), sel ? Theme.Accent : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, Lang.T(look.Item3), tag, new Rectangle(r.X + 6, r.Y + 72, r.Width - 12, 34), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				int h = Enumerable.Range(0, AiAssistant.Providers.Length).FirstOrDefault(i => Tile(i).Contains(e.Location), -1);
				if (h != hover) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				for (int i = 0; i < AiAssistant.Providers.Length; i++) if (Tile(i).Contains(e.Location)) SelectedIndex = i;
			}
		}

		/// <summary>A colored line with what is happening: blue (info, testing), green (works), red (did not work).</summary>
		sealed class Banner : Control
		{
			public enum Kind { Info, Busy, Ok, Error }
			Kind kind; string text = "";
			readonly Font font = Theme.CreateFont(13), icon = Theme.CreateFont(16);

			public Banner() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }

			public void Set(Kind k, string t) { kind = k; text = t ?? ""; Invalidate(); }

			public int Needed(int width)
			{
				using (var g = CreateGraphics()) return Math.Max(46, Theme.MeasureText(g, text, font, width - 64, TextFormatFlags.WordBreak).Height + 24);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				var (back, fore, sign) = kind == Kind.Ok ? (Color.FromArgb(40, Theme.Success), Theme.Success, "✅")
					: kind == Kind.Error ? (Color.FromArgb(36, Theme.Danger), Theme.Danger, "⚠️")
					: kind == Kind.Busy ? (Theme.AccentSoft, Theme.Accent, "⏳")
					: (Theme.SurfaceAlt, Theme.TextMuted, "💡");
				var r = new Rectangle(0, 0, Width - 1, Height - 1);
				Theme.FillRounded(g, back, r, 10);
				Theme.DrawText(g, sign, icon, new Rectangle(12, 0, 30, Height), fore, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
				Theme.DrawText(g, text, font, new Rectangle(48, 12, Width - 64, Height - 20), kind == Kind.Info ? Theme.Text : fore, TextFormatFlags.WordBreak);
			}
		}
	}

	/// <summary>The little box to write a question, and the answer in the pet's bubble.</summary>
	internal sealed class FormAsk : PetDialog
	{
		readonly TextBox question = new TextBox { Multiline = true, Width = 380, Height = 70, ScrollBars = ScrollBars.Vertical, MaxLength = 1000 };

		FormAsk()
		{
			string pet = Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";
			Text = Lang.T("ask.title", pet);
			Icon = Properties.Resources.icon;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			StartPosition = FormStartPosition.CenterScreen;
			TopMost = true;
			AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Padding = new Padding(16);
			var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
			root.Controls.Add(new Label { Text = Lang.T("ask.prompt", pet), AutoSize = true, Margin = new Padding(0, 0, 0, 6) });
			root.Controls.Add(question);
			var ok = new Button { Text = Lang.T("ask.send"), AutoSize = true, MinimumSize = new Size(110, 32), Margin = new Padding(0, 0, 8, 0) };
			// speak instead of typing: the voice typing of Windows (Win+H) writes into the box
			var voice = new Button { Text = "🎤 " + Lang.T("ask.voice"), AutoSize = true, MinimumSize = new Size(110, 32) };
			voice.Click += (s, e) => { question.Focus(); VoiceTyping(); };
			var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
			row.Controls.Add(ok); row.Controls.Add(voice);
			root.Controls.Add(row);
			if (AiAssistant.HistoryCount > 0)
				root.Controls.Add(new Label { Text = Lang.T("ask.continuing"), AutoSize = true, ForeColor = Theme.TextMuted, Margin = new Padding(0, 8, 0, 0) });
			Controls.Add(root);
			AcceptButton = ok;
			ok.Click += (s, e) => { if (question.Text.Trim().Length > 0) { DialogResult = DialogResult.OK; } };
			question.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; ok.PerformClick(); } };
			Theme.ApplyTo(this);
			Theme.MakePrimary(ok);
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

		/// <summary>Win+H: the dictation of Windows 10/11 types what you say into the focused box.</summary>
		internal static void VoiceTyping()
		{
			const byte VK_LWIN = 0x5B, VK_H = 0x48; const uint UP = 0x0002;
			keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
			keybd_event(VK_H, 0, 0, UIntPtr.Zero);
			keybd_event(VK_H, 0, UP, UIntPtr.Zero);
			keybd_event(VK_LWIN, 0, UP, UIntPtr.Zero);
		}

		/// <summary>Open the chat box above the pet (reminders, weather and meetings work even without the AI).</summary>
		public static void Open() => FormChat.Open();

		/// <summary>The old one-question box (or the AI settings when nothing is set up yet).</summary>
		internal static void OpenSimple()
		{
			if (!AiAssistant.Configured) { FormSettings.ShowPage(FormSettings.Page.Ai); return; }
			string q;
			FormPet.Hold(true);   // the pets wait while you write
			try
			{
				using (var f = new FormAsk())
				{
					if (f.ShowDialog() != DialogResult.OK) return;
					q = f.question.Text.Trim();
				}
			}
			finally { FormPet.Hold(false); }
			_ = AskNow(q);
		}

		/// <summary>Once: questions to a service on the internet leave this computer (asked the first time).</summary>
		internal static bool PrivacyOk()
		{
			if (AiAssistant.IsLocal(AiAssistant.BaseUrl) || Program.MyData.GetSetting("AiPrivacyOk") == "1") return true;
			var p = AiAssistant.Preset(AiAssistant.ProviderId);
			if (PetMessage.Show(Lang.T("ask.privacy", p.Id == "custom" ? AiAssistant.BaseUrl : p.Name), Lang.T("ai.title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return false;
			Program.MyData.SetSetting("AiPrivacyOk", "1");
			return true;
		}

		/// <summary>Think, ask the service, say the answer (shortened in the bubble, whole on "See all").</summary>
		public static async Task AskNow(string q)
		{
			// "me lembra de… às 15h": a reminder, understood here (nothing is sent anywhere)
			bool wantsReminder = SmartReminder.LooksLikeReminder(q);
			if (wantsReminder)
			{
				var local = SmartReminder.Parse(q, DateTime.Now);
				if (local != null) { SmartReminder.AddAndConfirm(local); return; }
			}
			if (!PrivacyOk()) return;
			Program.Mainthread?.ReactAll("think", "review", "idea", "crystal ball", "read");
			SpeechBubble.Say(Lang.T("ask.thinking"), 0);
			if (wantsReminder)
			{
				// a reminder said in another way: the AI rewrites it in a simple form, read here
				var r = await SmartReminder.Understand(q);
				if (r != null) { SmartReminder.AddAndConfirm(r); return; }
			}
			try
			{
				string answer = await AiAssistant.Ask(q);
				if (answer.Length == 0) answer = Lang.T("ask.empty");
				const int max = 260;
				var choices = new List<SpeechBubble.Choice>();
				string shown = answer;
				if (answer.Length > max)
				{
					shown = answer.Substring(0, max).TrimEnd() + "…";
					choices.Add(new SpeechBubble.Choice(Lang.T("ask.seeAll"), () => ShowAll(q, answer), true));
				}
				choices.Add(new SpeechBubble.Choice(Lang.T("ask.again"), Open, choices.Count == 0));
				choices.Add(new SpeechBubble.Choice(Lang.T("ask.newChat"), () => { AiAssistant.NewConversation(); Open(); }));
				Program.Mainthread?.ReactAll("idea", "happy", "wave");
				SpeechBubble.Say(shown, 0, choices.ToArray());
			}
			catch (Exception ex)
			{
				string why = ex is TaskCanceledException ? Lang.T("ai.timeout")
					: ex is HttpRequestException && AiAssistant.IsLocal(AiAssistant.BaseUrl) ? Lang.T("ai.ollamaOff")
					: ex.Message;
				SpeechBubble.Say(Lang.T("ask.error", why), 12, new SpeechBubble.Choice(Lang.T("ask.settings"), () => FormSettings.ShowPage(FormSettings.Page.Ai)));
			}
		}

		static void ShowAll(string q, string answer)
		{
			using (var f = new AnswerWindow(q, answer)) f.ShowDialog();
		}
	}
}
