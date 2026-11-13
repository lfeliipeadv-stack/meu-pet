using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Weather plugin: the pet says the weather of the day in the morning and warns before rain ("take the umbrella!").
	/// Uses Open-Meteo (free, no key, no account): only the chosen city (its coordinates) leaves this computer.
	/// </summary>
	public static class Weather
	{
		public sealed class Place { public string Name = ""; public double Lat, Lon; }
		public sealed class Today
		{
			public int Code, Max, Min, RainChance;
			public double Now;
			/// <summary>Highest chance of rain in the next 2 hours.</summary>
			public int RainSoon;
		}

		static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		static Timer timer;
		static Today last;
		static DateTime lastLoad = DateTime.MinValue;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("WeatherOn", false) && City != null;
			set { Program.MyData.SetSetting("WeatherOn", value); Apply(); }
		}

		/// <summary>The chosen city (null if none yet).</summary>
		public static Place City
		{
			get
			{
				string s = Program.MyData.GetSetting("WeatherCity");
				var p = s.Split('|');
				if (p.Length != 3 || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
					|| !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) return null;
				return new Place { Name = p[0], Lat = lat, Lon = lon };
			}
			set
			{
				Program.MyData.SetSetting("WeatherCity", value == null ? "" : value.Name.Replace("|", " ") + "|"
					+ value.Lat.ToString(CultureInfo.InvariantCulture) + "|" + value.Lon.ToString(CultureInfo.InvariantCulture));
				last = null;
			}
		}

		/// <summary>Warn when rain is coming (default on).</summary>
		public static bool RainAlerts
		{
			get => Program.MyData.GetSettingBool("WeatherRain", true);
			set => Program.MyData.SetSetting("WeatherRain", value);
		}

		/// <summary>The weather of the day in the morning (default on).</summary>
		public static bool Morning
		{
			get => Program.MyData.GetSettingBool("WeatherMorning", true);
			set => Program.MyData.SetSetting("WeatherMorning", value);
		}

		public static void Apply()
		{
			if (timer == null) { timer = new Timer { Interval = 10 * 60 * 1000 }; timer.Tick += async (s, e) => await Tick(); }
			if (Enabled)
			{
				timer.Start();
				var soon = new Timer { Interval = 20000 };   // a moment after the start
				soon.Tick += async (s, e) => { soon.Stop(); soon.Dispose(); await Tick(); };
				soon.Start();
			}
			else timer.Stop();
		}

		// ------------------------------------------------------------------ Open-Meteo

		/// <summary>Find a city by name (the first match, in the language in use).</summary>
		public static async Task<Place> FindCity(string name)
		{
			string lang = Lang.Current.Split('-')[0];
			string url = "https://geocoding-api.open-meteo.com/v1/search?count=1&format=json&language=" + Uri.EscapeDataString(lang) + "&name=" + Uri.EscapeDataString(name.Trim());
			var json = JsonNode.Parse(await http.GetStringAsync(url));
			var r = json?["results"]?.AsArray()?.FirstOrDefault();
			if (r == null) return null;
			string label = string.Join(", ", new[] { (string)r["name"], (string)r["admin1"], (string)r["country_code"] }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
			return new Place { Name = label, Lat = (double)r["latitude"], Lon = (double)r["longitude"] };
		}

		/// <summary>Today's weather at a place.</summary>
		public static async Task<Today> Load(Place p)
		{
			string url = "https://api.open-meteo.com/v1/forecast?timezone=auto&forecast_days=1"
				+ "&latitude=" + p.Lat.ToString(CultureInfo.InvariantCulture) + "&longitude=" + p.Lon.ToString(CultureInfo.InvariantCulture)
				+ "&current=temperature_2m,weather_code&hourly=precipitation_probability"
				+ "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max";
			var j = JsonNode.Parse(await http.GetStringAsync(url));
			var t = new Today
			{
				Now = (double?)j?["current"]?["temperature_2m"] ?? 0,
				Code = (int?)j?["daily"]?["weather_code"]?[0] ?? (int?)j?["current"]?["weather_code"] ?? 0,
				Max = (int)Math.Round((double?)j?["daily"]?["temperature_2m_max"]?[0] ?? 0),
				Min = (int)Math.Round((double?)j?["daily"]?["temperature_2m_min"]?[0] ?? 0),
				RainChance = (int?)j?["daily"]?["precipitation_probability_max"]?[0] ?? 0,
			};
			// the next 2 hours, from the hourly list (local time of the place)
			var times = j?["hourly"]?["time"]?.AsArray();
			var probs = j?["hourly"]?["precipitation_probability"]?.AsArray();
			string nowText = (string)j?["current"]?["time"];
			if (times != null && probs != null && DateTime.TryParse(nowText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var placeNow))
				for (int i = 0; i < times.Count && i < probs.Count; i++)
					if (DateTime.TryParse((string)times[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)
						&& h > placeNow.AddHours(-1) && h <= placeNow.AddHours(2))
						t.RainSoon = Math.Max(t.RainSoon, (int?)probs[i] ?? 0);
			return t;
		}

		/// <summary>The weather now (cached for 10 minutes), or null without a city or internet.</summary>
		public static async Task<Today> Current()
		{
			var city = City;
			if (city == null) return null;
			if (last != null && (DateTime.Now - lastLoad).TotalMinutes < 10) return last;
			try { last = await Load(city); lastLoad = DateTime.Now; }
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "weather: " + ex.Message); }
			return last;
		}

		/// <summary>The last weather read (for the Home page), without going to the internet.</summary>
		public static Today Cached => last;

		// ------------------------------------------------------------------ what the pet says

		/// <summary>WMO weather code → text key and emoji.</summary>
		public static (string key, string emoji) Describe(int code)
		{
			if (code == 0) return ("weather.clear", "☀️");
			if (code <= 2) return ("weather.partly", "⛅");
			if (code == 3) return ("weather.cloudy", "☁️");
			if (code == 45 || code == 48) return ("weather.fog", "🌫️");
			if (code >= 51 && code <= 57) return ("weather.drizzle", "🌦️");
			if ((code >= 61 && code <= 67) || (code >= 80 && code <= 82)) return ("weather.rain", "🌧️");
			if ((code >= 71 && code <= 77) || code == 85 || code == 86) return ("weather.snow", "❄️");
			if (code >= 95) return ("weather.storm", "⛈️");
			return ("weather.cloudy", "☁️");
		}

		/// <summary>"Today in São Paulo: rain 🌧️, 18° to 26°." plus a tip (umbrella, heat, cold).</summary>
		public static string Summary(Today t, Place p)
		{
			var (key, emoji) = Describe(t.Code);
			string s = Lang.T("weather.today", p.Name.Split(',')[0], Lang.T(key), emoji, t.Min, t.Max);
			if (t.RainChance >= 60) s += " " + Lang.T("weather.tipUmbrella", t.RainChance);
			else if (t.Max >= 32) s += " " + Lang.T("weather.tipHot");
			else if (t.Min <= 10) s += " " + Lang.T("weather.tipCold");
			return s;
		}

		/// <summary>Morning summary once a day; a rain warning at most every 3 hours.</summary>
		static async Task Tick()
		{
			if (!Enabled) return;
			var city = City;
			var t = await Current();
			if (t == null) return;
			var now = DateTime.Now;
			string today = now.ToString("yyyy-MM-dd");
			if (Morning && now.Hour >= 6 && now.Hour < 12 && Program.MyData.GetSetting("WeatherSaid") != today)
			{
				Program.MyData.SetSetting("WeatherSaid", today);
				Program.Mainthread?.ReactAll("wave", "hello", "happy");
				SpeechBubble.Notify(Lang.T("weather.morning") + " " + Summary(t, city), 12);
				return;
			}
			if (RainAlerts && t.RainSoon >= 70 && now.Hour >= 6 && now.Hour < 23
				&& (!DateTime.TryParse(Program.MyData.GetSetting("WeatherRainSaid"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var said) || (now - said).TotalHours >= 3))
			{
				Program.MyData.SetSetting("WeatherRainSaid", now.ToString("s", CultureInfo.InvariantCulture));
				Program.Mainthread?.ReactAll("surprised", "look up", "think");
				SpeechBubble.Notify(Lang.T("weather.rainSoon", city.Name.Split(',')[0], t.RainSoon), 12);
			}
		}

		/// <summary>Pet menu "How is the weather?": say it now.</summary>
		public static async void SayNow()
		{
			var city = City;
			if (city == null) { SpeechBubble.Say(Lang.T("weather.noCity"), 8, new SpeechBubble.Choice(Lang.T("weather.choose"), () => FormSettings.ShowPage(FormSettings.Page.Plugins), true)); return; }
			var t = await Current();
			if (t == null) { SpeechBubble.Say(Lang.T("weather.offline"), 6); return; }
			Program.Mainthread?.ReactAll("look up", "think", "happy");
			SpeechBubble.Say(Summary(t, city) + " " + Lang.T("weather.nowTemp", Math.Round(t.Now)), 10);
		}
	}
}

namespace DesktopPet
{
	/// <summary>On/off of the plugins that have no switch of their own (reminders, focus, games, shortcuts): on by default.</summary>
	public static class PluginSwitch
	{
		public static bool On(string id) => Program.MyData.GetSettingBool("Plug." + id, true);

		public static void Set(string id, bool on)
		{
			Program.MyData.SetSetting("Plug." + id, on);
			if (id == "focus" && !on && Pomodoro.Current != Pomodoro.Phase.Off) Pomodoro.Stop();
			ContextMenus.UpdateTexts();
		}
	}
}
