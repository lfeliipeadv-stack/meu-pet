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

		/// <summary>Focus timer, virtual pet, PC monitor, mood and shortcuts (all used from the pet menu).</summary>
		private void BuildExtrasOptions()
		{
			int[] focus = { 15, 25, 45, 50 };
			extrasView.AddChoice("", /* stopwatch */ "opt.pomoFocus",
				() => Program.MyData.GetSettingInt("PomodoroFocus", 25),
				v => Program.MyData.SetSetting("PomodoroFocus", v),
				focus, focus.Select(m => m + " min").ToArray());
			int[] pause = { 5, 10, 15 };
			extrasView.AddChoice("", /* moon */ "opt.pomoBreak",
				() => Program.MyData.GetSettingInt("PomodoroBreak", 5),
				v => Program.MyData.SetSetting("PomodoroBreak", v),
				pause, pause.Select(m => m + " min").ToArray());
			extrasView.AddToggle("", /* heart */ "opt.vpet", () => VirtualPet.Enabled, on => VirtualPet.Enabled = on);
			extrasView.AddToggle("", /* chart */ "opt.monitor", () => PcMonitor.Enabled, on => PcMonitor.Enabled = on);
			extrasView.AddToggle("", /* emoji */ "opt.mood", () => Mood.Daily, on => Mood.Daily = on);
			int[] hours = { 10, 13, 15, 17 };
			extrasView.AddChoice("", /* clock */ "opt.moodHour", () => Mood.Hour, v => Mood.Hour = v, hours, hours.Select(h => h + "h").ToArray());
			extrasView.AddAction("", /* history */ "opt.moodHistory", "opt.moodHistory.button", () =>
				MessageBox.Show(this, Lang.T("mood.history", Mood.Recent(14)), Lang.T("opt.moodHistory.title"), MessageBoxButtons.OK, MessageBoxIcon.None));
			extrasView.AddAction("", /* apps */ "opt.shortcuts", "opt.shortcuts.button", () => { using (var f = new FormLaunchers()) f.ShowDialog(this); });

			// the pet and the computer
			extrasView.AddToggle("", /* people */ "opt.petsMeet", () => PetsMeet.Enabled, on => PetsMeet.Enabled = on);
			extrasView.AddToggle("", /* monitor */ "opt.pcEvents", () => ComputerEvents.Enabled, on => ComputerEvents.Enabled = on);

			// calendar (private iCal link of Google or Outlook)
			extrasView.AddAction("", /* calendar */ "opt.agenda", "opt.agenda.button", ConfigureAgenda);
			int[] lead = { 5, 10, 15 };
			extrasView.AddChoice("", /* clock */ "opt.agendaLead", () => Agenda.LeadMinutes, v => Agenda.LeadMinutes = v, lead, lead.Select(m => m + " min").ToArray());

			// Claude Code
			extrasView.AddToggle("", /* code */ "opt.claude", () => AgentBridge.Enabled && ClaudeHooks.IsInstalled(), SetClaude);
			extrasView.AddToggle("", /* message */ "opt.claudeBubbles", () => AgentBridge.ProgressBubbles, on => AgentBridge.ProgressBubbles = on);
			extrasView.AddAction("", /* play */ "opt.claudeTest", "opt.claudeTest.button", TestClaude);
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
					if (MessageBox.Show(this, Lang.T("claude.confirmOn", path, System.IO.Path.GetFileName(ClaudeHooks.BackupPath(path))),
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
					AgentBridge.Enabled = false;
					StatusText = Lang.T("claude.off");
				}
			}
			catch (Exception ex) { StatusText = Lang.T("claude.error", ex.Message); }
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
