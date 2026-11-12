using System;
using System.Linq;

namespace DesktopPet
{
	/// <summary>Little games of the pet menu: fortune cookie and magic 8-ball. Texts are in the language files ("|" separated).</summary>
	public static class Fun
	{
		static readonly Random rnd = new Random();

		static string Pick(string key)
		{
			var items = Lang.T(key).Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
			return items.Length == 0 ? "" : items[rnd.Next(items.Length)];
		}

		public static void FortuneCookie()
		{
			Program.Mainthread?.ReactAll("eat", "eat fish", "eat meat", "cookie", "happy");
			SpeechBubble.Say("🥠 " + Pick("fun.fortunes"), 14);
		}

		public static void MagicBall()
		{
			SpeechBubble.Say(Lang.T("fun.ballAsk"), 0,
				new SpeechBubble.Choice(Lang.T("fun.ballShake"), () =>
				{
					Program.Mainthread?.ReactAll("think", "spin", "dizzy");
					SpeechBubble.Say("🎱 " + Pick("fun.ballAnswers"), 10);
				}, true));
		}

		/// <summary>The pet tells a random fact or joke.</summary>
		public static void Joke() => SpeechBubble.Say(Pick("fun.jokes"), 14);
	}
}
