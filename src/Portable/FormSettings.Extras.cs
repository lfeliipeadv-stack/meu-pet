using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	public sealed partial class FormSettings
	{
		// ================================================================== Extras page

		/// <summary>Assistant page: the configured AI providers (one in use), then the chat options.</summary>
		private void BuildAiOptions()
		{
			aiView.Clear();
			string active = AiProfiles.ActiveId;
			var profiles = AiProfiles.All;
			active = AiProfiles.ActiveId;
			aiView.AddHeader(() => Lang.T("ai.providers", AiProfiles.All.Count));
			foreach (var p in profiles)
			{
				var prof = p;
				bool local = AiAssistant.IsLocal(p.BaseUrl);
				string desc = (p.Model.Length > 0 ? p.Model : "—") + " · " + Lang.T(local ? "ai.whereLocal" : "ai.whereInternet") + (p.KeyBlob.Length > 0 ? " · " + Lang.T("ai.keySaved") : "");
				var buttons = new List<(string, Action)>();
				if (p.Id != active) buttons.Add((Lang.T("ai.use"), () => { AiProfiles.Use(prof); StatusText = Lang.T("ai.saved", AiProfiles.Name(prof), prof.Model); BuildAiOptions(); }));
				buttons.Add((Lang.T("ai.edit"), () => EditProvider(prof)));
				buttons.Add((Lang.T("ai.removeBtn"), () =>
				{
					if (PetMessage.Show(this, Lang.T("ai.removeConfirm", AiProfiles.Name(prof)), Lang.T("ai.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
					AiProfiles.Remove(prof);
					StatusText = Lang.T("ai.removed");
					BuildAiOptions();
				}));
				aiView.AddButtons(local ? "\uE7F4" : "\uE753", AiProfiles.Name(p), desc, p.Id == active ? Lang.T("ai.inUse") : null, buttons.ToArray());
			}
			aiView.AddAction("\uE710", /* add */ "ai.add", "ai.add.button", AddProvider, profiles.Count == 0 ? "ai.addFirst.desc" : null);

			aiView.AddHeader("ai.secChat");
			aiView.AddAction("\uE8BD", /* chat */ "opt.aiAsk", "opt.aiAsk.button", FormAsk.Open);
			int[] styles = { 0, 1, 2, 3 };
			aiView.AddChoice("", /* emoji */ "opt.aiStyle", () => AiAssistant.Style, v => AiAssistant.Style = v, styles,
				new[] { "opt.aiStyle.friendly", "opt.aiStyle.direct", "opt.aiStyle.kids", "opt.aiStyle.funny" });
			aiView.AddToggle("", /* cursor */ "opt.hoverAsk", () => HoverAsk.Enabled, on => HoverAsk.Enabled = on);
			// off, or one of the shortcuts; a shortcut taken by another program says so
			aiView.AddDropDown("", /* keyboard */ "opt.askHotkey",
				() => new[] { Lang.T("opt.off") }.Concat(Enumerable.Range(0, HideHotkey.AskKeyCount).Select(HideHotkey.AskKeyText)).ToArray(),
				() => HideHotkey.AskEnabled ? HideHotkey.AskKey + 1 : 0,
				i =>
				{
					if (i <= 0) { HideHotkey.AskEnabled = false; StatusText = Lang.T("status.disabled", Lang.T("opt.askHotkey.title")); return; }
					Program.MyData.SetSetting("AskHotkey", true);
					HideHotkey.AskKey = i - 1;
					StatusText = HideHotkey.AskBusy ? Lang.T("status.hotkeyBusy", HideHotkey.AskText) : Lang.T("status.setTo", Lang.T("opt.askHotkey.title"), HideHotkey.AskText);
				});
		}

		/// <summary>"Add provider": the AI window starts empty; cancelling puts back the one in use.</summary>
		private void AddProvider()
		{
			var before = AiProfiles.Snapshot();
			AiProfiles.Restore(("", "", "", ""));
			bool ok;
			using (var f = new FormAiSettings()) ok = f.ShowDialog(this) == DialogResult.OK && AiAssistant.Configured;
			if (ok)
			{
				AiProfiles.SaveCurrent(asNew: true);
				StatusText = Lang.T("ai.saved", AiAssistant.Preset(AiAssistant.ProviderId).Name, AiAssistant.Model);
				SpeechBubble.Say(Lang.T("ai.ready"), 8);
			}
			else AiProfiles.Restore(before);
			BuildAiOptions();
		}

		/// <summary>Edit a provider (it becomes the one in use); "Remove" inside the window forgets it.</summary>
		private void EditProvider(AiProfile p)
		{
			var before = AiProfiles.Snapshot();
			string wasActive = AiProfiles.ActiveId;
			AiProfiles.Use(p);
			using (var f = new FormAiSettings())
			{
				if (f.ShowDialog(this) != DialogResult.OK) { if (wasActive != p.Id) { AiProfiles.Restore(before); AiProfiles.ActiveId = wasActive; } }
				else if (AiAssistant.Configured) { AiProfiles.SaveCurrent(); StatusText = Lang.T("ai.saved", AiAssistant.Preset(AiAssistant.ProviderId).Name, AiAssistant.Model); }
				else { AiProfiles.Remove(p); StatusText = Lang.T("ai.removed"); }
			}
			BuildAiOptions();
		}

		/// <summary>Paste (or clear) the private iCal link of the calendar, then check it.</summary>
		private async void ConfigureAgenda()
		{
			string url = FormLaunchers.Prompt(Lang.T("agenda.ask"), Agenda.Url);
			if (url == null) return;   // cancelled
			url = url.Trim();
			if (url.Length == 0) { Agenda.Url = ""; StatusText = Lang.T("agenda.removed"); return; }
			try
			{
				StatusText = Lang.T("agenda.checking");
				var events = await Agenda.Load(url);
				Agenda.Url = url;
				var next = events.FirstOrDefault(e => e.Start > DateTime.Now);
				StatusText = Lang.T("agenda.connected", events.Count);
				SpeechBubble.Say(next == null ? Lang.T("agenda.connectedNone") : Lang.T("agenda.connectedNext", next.Title, next.Start.ToString("t", CultureInfo.CurrentCulture)), 10);
			}
			catch (Exception ex) { StatusText = Lang.T("agenda.error", ex.Message); }
		}

		/// <summary>Turn the Claude Code reactions on (hooks + listener) or off.</summary>
		private void SetClaude(bool on)
		{
			try
			{
				if (on)
				{
					string path = ClaudeHooks.DefaultPath;
					if (PetMessage.Show(this, Lang.T("claude.confirmOn", path, System.IO.Path.GetFileName(ClaudeHooks.BackupPath(path))),
						Lang.T("opt.claude.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
					AgentBridge.Enabled = true;
					if (!AgentBridge.Listening) { AgentBridge.Enabled = false; StatusText = Lang.T("claude.portBusy", AgentBridge.Port); return; }
					ClaudeHooks.Install(path);
					StatusText = Lang.T("claude.on");
					SpeechBubble.Say(Lang.T("claude.on"), 8);
				}
				else
				{
					ClaudeHooks.Uninstall();
					if (!ClaudeMcp.IsRegistered()) AgentBridge.Enabled = false;   // the MCP server still needs the channel
					StatusText = Lang.T("claude.off");
				}
			}
			catch (Exception ex) { StatusText = Lang.T("claude.error", ex.Message); }
		}

		/// <summary>Let Claude talk through the pet: register (or remove) the MCP server in Claude Code.</summary>
		private async void SetClaudeMcp(bool on)
		{
			try
			{
				if (on && !AgentBridge.Enabled)
				{
					// the MCP server talks to the pet through the same local channel
					AgentBridge.Enabled = true;
					if (!AgentBridge.Listening) { AgentBridge.Enabled = false; StatusText = Lang.T("claude.portBusy", AgentBridge.Port); return; }
				}
				var (ok, output) = on ? await ClaudeMcp.Register() : await ClaudeMcp.Unregister();
				if (output == null)
				{
					// Claude Code not found: give the command to paste
					try { Clipboard.SetText(ClaudeMcp.ManualCommand); } catch (Exception skipped) { LogFile.Ignored(skipped); }
					PetMessage.Show(this, Lang.T("mcp.manual", ClaudeMcp.ManualCommand), Lang.T("opt.claudeMcp.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				StatusText = ok ? Lang.T(on ? "mcp.on" : "mcp.off") : Lang.T("mcp.error", output.Length > 160 ? output.Substring(0, 160) + "…" : output);
				if (ok && on) SpeechBubble.Say(Lang.T("mcp.on"), 8);
				integrationsView?.Reload();
			}
			catch (Exception ex) { StatusText = Lang.T("mcp.error", ex.Message); }
		}

		/// <summary>A short example, through the real local endpoint when it is on.</summary>
		private async void TestClaude()
		{
			var steps = new[] { ("UserPromptSubmit", AgentBridge.Activity.Thinking), ("PreToolUse/edit", AgentBridge.Activity.Editing),
				("PreToolUse/run", AgentBridge.Activity.Running), ("Stop", AgentBridge.Activity.Done) };
			bool keep = AgentBridge.ProgressBubbles;
			AgentBridge.ProgressBubbles = true;   // the example shows every step
			try
			{
				foreach (var (path, activity) in steps)
				{
					if (AgentBridge.Listening) await AgentBridge.SendTest(path, "{}");
					else AgentBridge.React(activity);
					await System.Threading.Tasks.Task.Delay(2200);
				}
			}
			catch (Exception ex) { StatusText = Lang.T("claude.error", ex.Message); }
			finally { AgentBridge.ProgressBubbles = keep; }
		}
	}
}
