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
	/// <summary>Settings → AI: choose the service, model and key, test it, save or remove it.</summary>
	internal sealed class FormAiSettings : PetDialog
	{
		readonly ComboBox provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
		readonly TextBox url = new TextBox { Width = 380 };
		readonly ComboBox model = new ComboBox { Width = 380 };
		readonly TextBox key = new TextBox { Width = 380, UseSystemPasswordChar = true };
		readonly Label keyLabel, info = new Label { AutoSize = true, MaximumSize = new Size(380, 0) };
		readonly LinkLabel getKey = new LinkLabel { AutoSize = true };
		readonly Button test = new Button { AutoSize = true, MinimumSize = new Size(110, 32) };
		readonly Button save = new Button { AutoSize = true, MinimumSize = new Size(110, 32) };
		readonly Button remove = new Button { AutoSize = true, MinimumSize = new Size(110, 32) };
		bool tested;

		public FormAiSettings()
		{
			Text = Lang.T("ai.title");
			Icon = Properties.Resources.icon;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.CenterParent;
			AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Padding = new Padding(16);

			var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
			Label L(string k) => new Label { Text = Lang.T(k), AutoSize = true, Margin = new Padding(0, 10, 0, 3), Font = new Font(Theme.FontFamilyName, 9.5f, FontStyle.Bold) };
			root.Controls.Add(new Label { Text = Lang.T("ai.intro"), AutoSize = true, MaximumSize = new Size(380, 0) });
			root.Controls.Add(L("ai.service")); root.Controls.Add(provider);
			root.Controls.Add(L("ai.address")); root.Controls.Add(url);
			root.Controls.Add(keyLabel = L("ai.key")); root.Controls.Add(key); root.Controls.Add(getKey);
			root.Controls.Add(L("ai.model")); root.Controls.Add(model);
			info.Margin = new Padding(0, 10, 0, 0);
			root.Controls.Add(info);
			var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
			test.Text = Lang.T("ai.test"); save.Text = Lang.T("ai.save"); remove.Text = Lang.T("ai.remove");
			row.Controls.Add(test); row.Controls.Add(save); row.Controls.Add(remove);
			root.Controls.Add(row);
			Controls.Add(root);

			foreach (var p in AiAssistant.Providers) provider.Items.Add(p.Id == "custom" ? Lang.T("ai.custom") : p.Name);
			string saved = AiAssistant.ProviderId;
			provider.SelectedIndex = Math.Max(0, Array.FindIndex(AiAssistant.Providers, p => p.Id == saved));
			if (saved.Length > 0)
			{
				url.Text = AiAssistant.BaseUrl;
				model.Text = AiAssistant.Model;
				key.Text = AiAssistant.Key;
				tested = true;
			}
			else LoadPreset();
			provider.SelectedIndexChanged += (s, e) => { LoadPreset(); tested = false; Refresh2(); };
			url.TextChanged += (s, e) => tested = false;
			key.TextChanged += (s, e) => tested = false;
			getKey.LinkClicked += (s, e) => { var p = Current; if (!string.IsNullOrEmpty(p.KeyUrl)) Program.OpenLink(p.KeyUrl); };
			test.Click += async (s, e) => await Test();
			save.Click += async (s, e) => await Save();
			remove.Click += (s, e) => { AiAssistant.Clear(); DialogResult = DialogResult.OK; };

			Theme.ApplyTo(this);
			Theme.MakePrimary(save);
			foreach (var c in new Control[] { provider, model }) { c.BackColor = Theme.Surface; c.ForeColor = Theme.Text; }
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

		void Refresh2()
		{
			var p = Current;
			bool needsKey = p.NeedsKey;
			keyLabel.Visible = key.Visible = needsKey;
			getKey.Visible = !string.IsNullOrEmpty(p.KeyUrl);
			getKey.Text = p.Local ? Lang.T("ai.getOllama") : Lang.T("ai.getKey");
			url.ReadOnly = p.Id != "custom" && p.Id != "ollama";
			info.Text = p.Local ? Lang.T("ai.infoLocal") : Lang.T("ai.infoCloud", p.Name);
			remove.Visible = AiAssistant.ProviderId.Length > 0;
		}

		/// <summary>Connect, list the models and choose one.</summary>
		async Task<bool> Test()
		{
			var p = Current;
			test.Enabled = save.Enabled = false;
			info.Text = Lang.T("ai.testing");
			try
			{
				if (p.NeedsKey && p.Id != "custom" && key.Text.Trim().Length == 0) throw new InvalidOperationException(Lang.T("ai.needKey"));
				var models = await AiAssistant.ListModels(url.Text.Trim(), key.Text.Trim());
				if (models.Count == 0)
					throw new InvalidOperationException(p.Local ? Lang.T("ai.noModelsOllama") : Lang.T("ai.noModels"));
				// a service may list models it no longer lets you use: ask a tiny question and keep one that answers
				info.Text = Lang.T("ai.testingModels");
				string pick = await AiAssistant.FindWorkingModel(p, url.Text.Trim(), key.Text.Trim(), model.Text.Trim(), models);
				model.Items.Clear();
				foreach (var m in AiAssistant.Candidates(p, pick, models)) model.Items.Add(m);
				model.Text = pick;
				info.Text = Lang.T("ai.testOk", models.Count, pick);
				info.ForeColor = Theme.Success;
				tested = true;
				return true;
			}
			catch (HttpRequestException) when (p.Local)
			{
				info.Text = Lang.T("ai.ollamaOff");
				info.ForeColor = Theme.Danger;
			}
			catch (Exception ex)
			{
				info.Text = Lang.T("ai.testFail", ex is TaskCanceledException ? Lang.T("ai.timeout") : ex.Message);
				info.ForeColor = Theme.Danger;
			}
			finally { test.Enabled = save.Enabled = true; }
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
		static void VoiceTyping()
		{
			const byte VK_LWIN = 0x5B, VK_H = 0x48; const uint UP = 0x0002;
			keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
			keybd_event(VK_H, 0, 0, UIntPtr.Zero);
			keybd_event(VK_H, 0, UP, UIntPtr.Zero);
			keybd_event(VK_LWIN, 0, UP, UIntPtr.Zero);
		}

		/// <summary>Open the box (or the AI settings when nothing is set up yet).</summary>
		public static void Open()
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

		/// <summary>Think, ask the service, say the answer (shortened in the bubble, whole on "See all").</summary>
		public static async Task AskNow(string q)
		{
			// once: questions to a service on the internet leave this computer
			if (!AiAssistant.IsLocal(AiAssistant.BaseUrl) && Program.MyData.GetSetting("AiPrivacyOk") != "1")
			{
				var p = AiAssistant.Preset(AiAssistant.ProviderId);
				if (MessageBox.Show(Lang.T("ask.privacy", p.Id == "custom" ? AiAssistant.BaseUrl : p.Name), Lang.T("ai.title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
				Program.MyData.SetSetting("AiPrivacyOk", "1");
			}
			Program.Mainthread?.ReactAll("think", "review", "idea", "crystal ball", "read");
			SpeechBubble.Say(Lang.T("ask.thinking"), 0);
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
			using (var f = new PetDialog
			{
				Text = Lang.T("ask.title", Program.Mainthread?.CurrentHeader?.Petname ?? "Pet"), Icon = Properties.Resources.icon,
				StartPosition = FormStartPosition.CenterScreen, Size = new Size(560, 420), MinimizeBox = false, ShowInTaskbar = false, TopMost = true,
			})
			{
				var box = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
					Text = "❓ " + q + Environment.NewLine + Environment.NewLine + answer.Replace("\n", Environment.NewLine), Font = new Font(Theme.FontFamilyName, 10.5f) };
				var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
				pad.Controls.Add(box);
				f.Controls.Add(pad);
				Theme.ApplyTo(f);
				box.BackColor = Theme.Background; box.ForeColor = Theme.Text;
				f.ShowDialog();
			}
		}
	}
}
