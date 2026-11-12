using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>Needs of one pet (0 = empty, 100 = full).</summary>
	public sealed class PetNeeds
	{
		public double Food { get; set; } = 80;
		public double Love { get; set; } = 80;
		public double Energy { get; set; } = 80;
		public DateTime Updated { get; set; } = DateTime.Now;
		public DateTime LastComplaint { get; set; } = DateTime.MinValue;
	}

	/// <summary>
	/// Tamagotchi-style needs: food, affection and energy go down slowly (also while the program is closed,
	/// but never below 10 for that time) and go up from the pet menu: feed, pet, play, sleep.
	/// When one gets low the pet asks for it, at most once an hour. Off by default (Settings → Extras).
	/// </summary>
	public static class VirtualPet
	{
		static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "needs.json");
		static Dictionary<string, PetNeeds> all;
		static Timer timer;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("VirtualPet", false);
			set { Program.MyData.SetSetting("VirtualPet", value); if (value) Start(); Changed?.Invoke(); }
		}

		public static event Action Changed;

		static Dictionary<string, PetNeeds> All
		{
			get
			{
				if (all != null) return all;
				try { all = File.Exists(FilePath) ? JsonText.Deserialize<Dictionary<string, PetNeeds>>(File.ReadAllText(FilePath)) : null; } catch (Exception) { }
				return all ?? (all = new Dictionary<string, PetNeeds>(StringComparer.OrdinalIgnoreCase));
			}
		}

		/// <summary>The needs of the pet on screen, brought up to date.</summary>
		public static PetNeeds Current
		{
			get
			{
				string key = Program.MyData.CurrentPetKey();
				if (!All.TryGetValue(key, out var n)) All[key] = n = new PetNeeds();
				Decay(n);
				return n;
			}
		}

		static void Decay(PetNeeds n)
		{
			var now = DateTime.Now;
			double minutes = Math.Max(0, (now - n.Updated).TotalMinutes);
			n.Updated = now;
			if (minutes <= 0) return;
			bool away = minutes > 30;   // the program was closed: be gentle
			double Down(double v, double perHour) { double r = Math.Max(0, v - perHour * minutes / 60); return away ? Math.Max(Math.Min(v, 10), r) : r; }
			n.Food = Down(n.Food, 8);       // ~10 h from full to empty
			n.Love = Down(n.Love, 6);
			n.Energy = Down(n.Energy, 5);
		}

		public static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonText.SerializeIndented(All));
			}
			catch (Exception) { }
		}

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 60000 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		static void Check()
		{
			if (!Enabled) return;
			var n = Current;
			Save();
			Changed?.Invoke();
			if ((DateTime.Now - n.LastComplaint).TotalMinutes < 60) return;
			string key = n.Food < 25 ? "vpet.hungry" : n.Love < 25 ? "vpet.lonely" : n.Energy < 20 ? "vpet.sleepy" : null;
			if (key == null) return;
			n.LastComplaint = DateTime.Now;
			Save();
			var actions = new List<SpeechBubble.Choice>();
			if (key == "vpet.hungry") actions.Add(new SpeechBubble.Choice(Lang.T("vpet.feed"), Feed, true));
			if (key == "vpet.lonely") actions.Add(new SpeechBubble.Choice(Lang.T("vpet.pet"), Pet, true));
			if (key == "vpet.sleepy") actions.Add(new SpeechBubble.Choice(Lang.T("vpet.sleep"), Sleep, true));
			actions.Add(new SpeechBubble.Choice(Lang.T("vpet.later"), null));
			SpeechBubble.Say(Lang.T(key), 0, actions.ToArray());
		}

		static void Change(Action<PetNeeds> change, string sayKey, params string[] anims)
		{
			var n = Current;
			change(n);
			n.Food = Math.Min(100, Math.Max(0, n.Food));
			n.Love = Math.Min(100, Math.Max(0, n.Love));
			n.Energy = Math.Min(100, Math.Max(0, n.Energy));
			Save();
			Changed?.Invoke();
			Program.Mainthread?.ReactAll(anims);
			SpeechBubble.Say(Lang.T(sayKey) + "\n" + Bars(n), 6);
		}

		public static void Feed() => Change(n => { n.Food += 35; n.Energy += 5; }, "vpet.fed", "eat", "eat fish", "eat meat", "pizza", "happy");
		public static void Pet() => Change(n => n.Love += 30, "vpet.petted", "love", "in love", "hearts", "happy", "wave");
		public static void Play() => Change(n => { n.Love += 15; n.Energy -= 12; n.Food -= 5; }, "vpet.played", "dance", "jump", "juggle", "spin", "cheer");
		public static void Sleep() => Change(n => n.Energy += 45, "vpet.slept", "sleep", "yawn");

		/// <summary>"🍖 80%  ❤ 65%  ⚡ 40%"</summary>
		public static string Bars(PetNeeds n) =>
			string.Format(CultureInfo.CurrentCulture, "🍖 {0:0}%   ❤️ {1:0}%   ⚡ {2:0}%", n.Food, n.Love, n.Energy);
	}
}
