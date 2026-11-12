using System;
using System.Linq;
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

		static bool skyTaken;

		/// <summary>The very first time the program opens (the introduction was never seen).</summary>
		static bool FirstRun => !Disabled && !Program.MyData.GetSettingBool("TourDone", false) && Program.MyData.GetSetting("LastVersion").Length == 0;

		/// <summary>Self-test: the next pet comes from the sky even though this is not the first run.</summary>
		internal static bool ForceSky;

		/// <summary>The first pet of the first run comes down from the sky (asked once, by that pet).</summary>
		public static bool TakeSkyArrival()
		{
			if (ForceSky) { ForceSky = false; return true; }
			if (skyTaken || !FirstRun) return false;
			skyTaken = true;
			return true;
		}

		/// <summary>First run: wait until the pet that fell from the sky lands, stop it there, then introduce it.</summary>
		static void WaitForLanding()
		{
			var start = DateTime.Now;
			DateTime landed = DateTime.MinValue;
			var t = new Timer { Interval = 150 };
			t.Tick += (s, e) =>
			{
				var pet = Program.Mainthread?.sheeps?.FirstOrDefault(p => p != null && !p.IsDisposed && p.Visible && !p.IsChildWindow);
				bool down = pet != null && pet.OnGround;
				if (!down) landed = DateTime.MinValue;
				else if (landed == DateTime.MinValue) landed = DateTime.Now;
				// on the ground for a moment (the landing bounce is over), or 30 s at most
				if ((landed != DateTime.MinValue && (DateTime.Now - landed).TotalMilliseconds > 600) || (DateTime.Now - start).TotalSeconds > 30)
				{
					t.Stop(); t.Dispose();
					if (Disabled) return;
					Program.MyData.SetSetting("LastVersion", Updater.CurrentVersion.ToString(3));
					Tour();
				}
			};
			t.Start();
		}

		public static void Start()
		{
			if (FirstRun) { WaitForLanding(); return; }
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
			HoldDuringTour();
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

		static void Done()
		{
			Program.MyData.SetSetting("TourDone", true);
			ReleaseTour();
		}

		// ------------------------------------------------------------------ the pets stay still for the whole tour

		static bool tourHeld;
		static Timer watch;

		/// <summary>
		/// From the first bubble to the last, every pet stays where it is (only animations that do not move), also in the
		/// moment between two bubbles. If the bubbles go away some other way, the pets are let go a moment later.
		/// </summary>
		static void HoldDuringTour()
		{
			if (tourHeld) return;
			tourHeld = true;
			FormPet.Hold(true);
			int quiet = 0;
			watch = new Timer { Interval = 500 };
			watch.Tick += (s, e) =>
			{
				bool bubble = Application.OpenForms.OfType<SpeechBubble>().Any(b => b.Visible);
				quiet = bubble ? 0 : quiet + 1;
				if (quiet >= 4) ReleaseTour();
			};
			watch.Start();
		}

		static void ReleaseTour()
		{
			if (!tourHeld) return;
			tourHeld = false;
			watch?.Stop(); watch?.Dispose(); watch = null;
			FormPet.Hold(false);
		}

		/// <summary>Self-test: the tour is keeping the pets still.</summary>
		internal static bool Holding => tourHeld;
		internal static void StopHolding() => ReleaseTour();

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
