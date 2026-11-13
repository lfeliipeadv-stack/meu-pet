using System;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Right click on a pet: everything the pet can do for you in one menu — needs (virtual pet), little games,
	/// focus timer, reminders, shortcuts, mood, PC monitor, animations and the panel.
	/// </summary>
	public static class PetMenu
	{
		public static ContextMenuStrip Show(FormPet pet, Control anchor, Point at)
		{
			var cm = new ContextMenuStrip();
			FormPet.Hold(true);   // every pet stands still while the menu is open
			Theme.ApplyTo(cm);
			ToolStripMenuItem Item(ToolStripItemCollection to, string text, Action run, bool enabled = true)
			{
				var it = new ToolStripMenuItem(text) { Enabled = enabled };
				if (run != null) it.Click += (s, e) => run();
				to.Add(it);
				return it;
			}

			// header: the pet (and its needs)
			var head = Item(cm.Items, Program.Mainthread?.CurrentHeader?.Petname ?? "Pet", null, false);
			head.Font = new Font(head.Font, FontStyle.Bold);
			if (VirtualPet.Enabled)
			{
				Item(cm.Items, VirtualPet.Bars(VirtualPet.Current), null, false);
				Item(cm.Items, Lang.T("vpet.feed"), VirtualPet.Feed);
				Item(cm.Items, Lang.T("vpet.pet"), VirtualPet.Pet);
				Item(cm.Items, Lang.T("vpet.play"), VirtualPet.Play);
				Item(cm.Items, Lang.T("vpet.sleep"), VirtualPet.Sleep);
			}
			cm.Items.Add(new ToolStripSeparator());

			// the chat: reminders, weather and meetings work without the AI too
			{
				var ask = Item(cm.Items, Lang.T("menu.ask"), FormAsk.Open);
				ask.Font = new Font(ask.Font, FontStyle.Bold);
			}

			var games = Item(cm.Items, Lang.T("menu.games"), null);
			Item(games.DropDownItems, Lang.T("menu.fortune"), Fun.FortuneCookie);
			Item(games.DropDownItems, Lang.T("menu.ball"), Fun.MagicBall);
			Item(games.DropDownItems, Lang.T("menu.joke"), Fun.Joke);

			var focus = Item(cm.Items, Pomodoro.Current == Pomodoro.Phase.Off ? Lang.T("menu.focus") : Pomodoro.Status(), null);
			if (Pomodoro.Current == Pomodoro.Phase.Off)
				Item(focus.DropDownItems, Lang.T("menu.focusStart", Pomodoro.FocusMinutes), Pomodoro.StartFocus);
			else
			{
				Item(focus.DropDownItems, Pomodoro.Paused ? Lang.T("menu.focusResume") : Lang.T("menu.focusPause"), Pomodoro.TogglePause);
				Item(focus.DropDownItems, Lang.T("menu.focusStop"), Pomodoro.Stop);
			}

			if (Weather.Enabled) Item(cm.Items, Lang.T("menu.weather"), Weather.SayNow);

			var remind = Item(cm.Items, Lang.T("tray.reminders"), null);
			foreach (int m in new[] { 5, 10, 15, 30, 60 }) { int min = m; Item(remind.DropDownItems, Lang.T("tray.remindIn", min), () => Reminders.In(min)); }
			remind.DropDownItems.Add(new ToolStripSeparator());
			Item(remind.DropDownItems, Lang.T("rem.smartMenu"), SmartReminder.AskAndAdd);
			Item(remind.DropDownItems, Lang.T("tray.manageReminders"), () => FormSettings.ShowPage(FormSettings.Page.Reminders));

			var shortcuts = Item(cm.Items, Lang.T("menu.shortcuts"), null);
			foreach (var l in Launchers.All) { var launcher = l; Item(shortcuts.DropDownItems, l.Name, () => Launchers.Open(launcher)); }
			if (Launchers.All.Count > 0) shortcuts.DropDownItems.Add(new ToolStripSeparator());
			Item(shortcuts.DropDownItems, Lang.T("menu.shortcutsManage"), () => { FormPet.Hold(true); try { using (var f = new FormLaunchers()) f.ShowDialog(); } finally { FormPet.Hold(false); } });

			// plugins switched off leave the menu
			games.Visible = PluginSwitch.On("games");
			focus.Visible = PluginSwitch.On("focus") || Pomodoro.Current != Pomodoro.Phase.Off;
			remind.Visible = PluginSwitch.On("reminders");
			shortcuts.Visible = PluginSwitch.On("shortcuts");

			if (Agenda.Configured)
			{
				var next = Agenda.Next;
				Item(cm.Items, next == null ? Lang.T("menu.agendaNone")
					: Lang.T("menu.agendaNext", next.Start.ToString("t", System.Globalization.CultureInfo.CurrentCulture), next.Title.Length > 40 ? next.Title.Substring(0, 39) + "…" : next.Title), null, false);
			}
			Item(cm.Items, Lang.T("menu.mood"), Mood.Ask);
			var mon = Item(cm.Items, Lang.T("menu.monitor"), () => PcMonitor.Enabled = !PcMonitor.Enabled);
			mon.Checked = PcMonitor.Enabled;

			cm.Items.Add(new ToolStripSeparator());
			Item(cm.Items, Lang.T("tray.panel").Replace("&", ""), () => FormSettings.ShowPage(FormSettings.Page.Home));
			Item(cm.Items, Lang.T("menu.sendAway"), () => { if (!Program.Mainthread.KillSheep(pet)) pet.Close(); });

			cm.Closed += (s, e) => { FormPet.Hold(false); cm.BeginInvoke(new MethodInvoker(cm.Dispose)); };
			cm.Show(anchor, at);
			return cm;
		}
	}
}
