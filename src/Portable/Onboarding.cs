using System;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// The first time: the pet introduces itself and shows, in a few bubbles, what it can do (menu, drag, reminders
	/// and focus, Ctrl+Alt+P, AI). After an update: "What's new in version X" with a link to the full notes.
	/// Can be seen again from Settings.
	/// </summary>
	public static class Onboarding
	{
		/// <summary>Developer runs (self-test, snapshots) skip it.</summary>
		public static bool Disabled;

		static readonly string[] Steps = { "tour.menu", "tour.drag", "tour.focus", "tour.hide", "tour.ai" };

		public static void Start()
		{
			var t = new Timer { Interval = 6000 };
			t.Tick += (s, e) =>
			{
				t.Stop(); t.Dispose();
				if (Disabled) return;
				string current = Updater.CurrentVersion.ToString(3);
				string last = Program.MyData.GetSetting("LastVersion");
				Program.MyData.SetSetting("LastVersion", current);
				if (!Program.MyData.GetSettingBool("TourDone", false)) Tour();
				else if (last.Length > 0 && last != current) WhatsNew(current);
			};
			t.Start();
		}

		/// <summary>The introduction, one bubble at a time (can be skipped).</summary>
		public static void Tour()
		{
			string pet = Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";
			Program.Mainthread?.ReactAll("wave", "hello", "happy");
			SpeechBubble.Say(Lang.T("tour.hello", pet), 0,
				new SpeechBubble.Choice(Lang.T("tour.show"), () => Step(0), true),
				new SpeechBubble.Choice(Lang.T("tour.later"), Done));
		}

		static void Step(int i)
		{
			if (i >= Steps.Length) { Done(); return; }
			bool last = i == Steps.Length - 1;
			var next = new SpeechBubble.Choice(last ? Lang.T("tour.finish") : Lang.T("tour.next", i + 1, Steps.Length), () => Step(i + 1), true);
			if (Steps[i] == "tour.ai" && !AiAssistant.Configured)
				SpeechBubble.Say(Lang.T(Steps[i]), 0, new SpeechBubble.Choice(Lang.T("tour.openAi"), () => { Done(); FormSettings.ShowPage(FormSettings.Page.Ai); }, true),
					new SpeechBubble.Choice(Lang.T("tour.finish"), Done));
			else
				SpeechBubble.Say(Lang.T(Steps[i] == "tour.ai" ? "tour.aiReady" : Steps[i]), 0, next, new SpeechBubble.Choice(Lang.T("tour.skip"), Done));
		}

		static void Done() => Program.MyData.SetSetting("TourDone", true);

		/// <summary>What changed in this version (short, in the bubble) and a link to the release notes.</summary>
		public static void WhatsNew(string version)
		{
			string key = "whatsnew." + version;
			string text = Lang.T(key);
			if (text == key) return;   // nothing written for this version
			SpeechBubble.Say(Lang.T("whatsnew.title", version) + "\n" + text, 0,
				new SpeechBubble.Choice(Lang.T("whatsnew.more"), () => Program.OpenLink("https://github.com/lfeliipeadv-stack/meu-pet/releases/tag/v" + version), true),
				new SpeechBubble.Choice("OK", null));
		}
	}
}
