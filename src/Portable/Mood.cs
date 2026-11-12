using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// "How are you today?": the pet asks once a day (if turned on in Settings → Extras, at the chosen hour)
	/// or from the pet menu, answers with a kind line and keeps a small history (%LocalAppData%\DesktopPet\mood.json).
	/// </summary>
	public static class Mood
	{
		public static readonly string[] Faces = { "😄", "🙂", "😐", "😕", "😢" };
		static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "mood.json");
		static Dictionary<string, int> history;
		static Timer timer;

		public static bool Daily
		{
			get => Program.MyData.GetSettingBool("MoodDaily", false);
			set => Program.MyData.SetSetting("MoodDaily", value);
		}

		public static int Hour
		{
			get => Program.MyData.GetSettingInt("MoodHour", 15);
			set => Program.MyData.SetSetting("MoodHour", value);
		}

		/// <summary>Day ("yyyy-MM-dd") → answer (0 = great … 4 = bad).</summary>
		public static Dictionary<string, int> History
		{
			get
			{
				if (history != null) return history;
				try { history = File.Exists(FilePath) ? JsonText.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath)) : null; } catch (Exception) { }
				return history ?? (history = new Dictionary<string, int>());
			}
		}

		static string Today => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 60000 };
			timer.Tick += (s, e) =>
			{
				if (Daily && DateTime.Now.Hour >= Hour && !History.ContainsKey(Today)
					&& Program.MyData.GetSetting("MoodAsked") != Today)
				{
					Program.MyData.SetSetting("MoodAsked", Today);
					Ask();
				}
			};
			timer.Start();
		}

		public static void Ask()
		{
			var choices = Faces.Select((f, i) => new SpeechBubble.Choice(f, () => Answer(i), i == 0)).ToArray();
			SpeechBubble.Say(Lang.T("mood.ask"), 0, choices);
		}

		static void Answer(int i)
		{
			History[Today] = i;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonText.SerializeIndented(History));
			}
			catch (Exception) { }
			if (i <= 1) Program.Mainthread?.ReactAll("happy", "cheer", "dance", "jump");
			else if (i >= 3) Program.Mainthread?.ReactAll("love", "hearts", "hug", "wave");
			SpeechBubble.Say(Lang.T("mood.reply" + i), 10);
		}

		/// <summary>The last <paramref name="days"/> days as faces ("·" when not answered), oldest first.</summary>
		public static string Recent(int days = 14)
		{
			var parts = new List<string>();
			for (int d = days - 1; d >= 0; d--)
			{
				string key = DateTime.Today.AddDays(-d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				parts.Add(History.TryGetValue(key, out int v) && v >= 0 && v < Faces.Length ? Faces[v] : "·");
			}
			return string.Join(" ", parts);
		}
	}
}
