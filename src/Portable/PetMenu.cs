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
		public static ContextMenuStrip Show(FormPet pet, Control anchor, Point at, Action pickAnimation)
		{
			var cm = new ContextMenuStrip();
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

			var remind = Item(cm.Items, Lang.T("tray.reminders"), null);
			foreach (int m in new[] { 5, 10, 15, 30, 60 }) { int min = m; Item(remind.DropDownItems, Lang.T("tray.remindIn", min), () => Reminders.In(min)); }
			remind.DropDownItems.Add(new ToolStripSeparator());
			Item(remind.DropDownItems, Lang.T("tray.manageReminders"), () => FormSettings.ShowPage(FormSettings.Page.Reminders));

			var shortcuts = Item(cm.Items, Lang.T("menu.shortcuts"), null);
			foreach (var l in Launchers.All) { var launcher = l; Item(shortcuts.DropDownItems, l.Name, () => Launchers.Open(launcher)); }
			if (Launchers.All.Count > 0) shortcuts.DropDownItems.Add(new ToolStripSeparator());
			Item(shortcuts.DropDownItems, Lang.T("menu.shortcutsManage"), () => { using (var f = new FormLaunchers()) f.ShowDialog(); });

			Item(cm.Items, Lang.T("menu.mood"), Mood.Ask);
			var mon = Item(cm.Items, Lang.T("menu.monitor"), () => PcMonitor.Enabled = !PcMonitor.Enabled);
			mon.Checked = PcMonitor.Enabled;

			cm.Items.Add(new ToolStripSeparator());
			if (pickAnimation != null) Item(cm.Items, Lang.T("menu.animations"), pickAnimation);
			Item(cm.Items, Lang.T("tray.panel").Replace("&", ""), () => FormSettings.ShowPage(FormSettings.Page.Pets));
			Item(cm.Items, Lang.T("menu.sendAway"), () => { if (!Program.Mainthread.KillSheep(pet)) pet.Close(); });

			cm.Closed += (s, e) => cm.BeginInvoke(new MethodInvoker(cm.Dispose));
			cm.Show(anchor, at);
			return cm;
		}
	}
}
