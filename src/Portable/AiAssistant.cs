using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DesktopPet
{
	/// <summary>The AI service answered with an error (HTTP status kept to decide what to do: other model, wrong key…).</summary>
	public sealed class AiServiceException : InvalidOperationException
	{
		public int Status { get; }
		public AiServiceException(int status, string message) : base(message) { Status = status; }
	}

	/// <summary>An AI service that speaks the OpenAI "chat completions" format (almost all do).</summary>
	public sealed class AiProvider
	{
		public string Id, Name, BaseUrl, Model, KeyUrl;
		public bool NeedsKey, Local;
		/// <summary>Preferred models, the first available one is used when the saved one does not exist there.</summary>
		public string[] Prefer = new string[0];
	}

	/// <summary>
	/// "Ask the pet": a question goes to the AI service chosen in Settings → AI and the pet answers in its bubble,
	/// short, in the program's language and in its own voice. Ready-made: Ollama (on this computer, free, private),
	/// Google Gemini, Groq and OpenRouter (free plans, free key) and any other compatible service.
	/// The key is kept encrypted for this Windows user (DPAPI) and never goes into backups.
	/// </summary>
	public static class AiAssistant
	{
		public static readonly AiProvider[] Providers =
		{
			new AiProvider { Id = "ollama", Name = "Ollama (no seu computador)", BaseUrl = "http://localhost:11434/v1", Model = "llama3.2", Local = true, KeyUrl = "https://ollama.com/download",
				Prefer = new[] { "llama3.2", "llama3.1", "gemma3", "qwen2.5", "mistral", "phi" } },
			// "-latest" names follow Google's newest model (fixed versions are retired for new accounts after a while)
			new AiProvider { Id = "gemini", Name = "Google Gemini", BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai", Model = "gemini-flash-lite-latest", NeedsKey = true, KeyUrl = "https://aistudio.google.com/apikey",
				Prefer = new[] { "gemini-flash-lite-latest", "gemini-flash-latest", "flash-lite", "flash" } },
			new AiProvider { Id = "groq", Name = "Groq", BaseUrl = "https://api.groq.com/openai/v1", Model = "llama-3.3-70b-versatile", NeedsKey = true, KeyUrl = "https://console.groq.com/keys",
				Prefer = new[] { "llama-3.3-70b-versatile", "openai/gpt-oss-120b", "openai/gpt-oss-20b", "llama-3.1-8b-instant" } },
			new AiProvider { Id = "openrouter", Name = "OpenRouter (modelos grátis)", BaseUrl = "https://openrouter.ai/api/v1", Model = "openrouter/free", NeedsKey = true, KeyUrl = "https://openrouter.ai/keys",
				Prefer = new[] { ":free" } },
			new AiProvider { Id = "custom", Name = "Outro (compatível com OpenAI)", BaseUrl = "https://", Model = "", NeedsKey = true },
		};

		public static AiProvider Preset(string id) => Providers.FirstOrDefault(p => p.Id == id) ?? Providers[0];

		// ------------------------------------------------------------------ settings

		public static string ProviderId { get => Program.MyData.GetSetting("AiProvider"); set => Program.MyData.SetSetting("AiProvider", value ?? ""); }
		public static string BaseUrl { get => Program.MyData.GetSetting("AiBaseUrl"); set => Program.MyData.SetSetting("AiBaseUrl", value ?? ""); }
		public static string Model { get => Program.MyData.GetSetting("AiModel"); set => Program.MyData.SetSetting("AiModel", value ?? ""); }

		/// <summary>The key, encrypted with the Windows account (only this user on this computer can read it).</summary>
		public static string Key
		{
			get
			{
				string s = Program.MyData.GetSetting("AiKey");
				if (s.Length == 0) return "";
				try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), Entropy, DataProtectionScope.CurrentUser)); }
				catch (Exception) { return ""; }
			}
			set
			{
				string v = string.IsNullOrEmpty(value) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
				Program.MyData.SetSetting("AiKey", v);
			}
		}

		static readonly byte[] Entropy = Encoding.UTF8.GetBytes("desktopPet.ai");

		/// <summary>Set up: a service and a model were saved (and a key, when the service needs one).</summary>
		public static bool Configured => ProviderId.Length > 0 && BaseUrl.Length > 0 && Model.Length > 0 && (!Preset(ProviderId).NeedsKey || Key.Length > 0 || ProviderId == "custom");

		public static bool IsLocal(string baseUrl) => Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) && (u.IsLoopback || u.Host == "localhost");

		public static void Clear() { ProviderId = ""; BaseUrl = ""; Model = ""; Key = ""; Program.MyData.SetSetting("AiPrivacyOk", ""); }

		// ------------------------------------------------------------------ talking to the service

		static HttpClient Client(string key, int seconds)
		{
			var http = new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
			http.DefaultRequestHeaders.Add("User-Agent", "DesktopPet");
			if (!string.IsNullOrEmpty(key)) http.DefaultRequestHeaders.Add("Authorization", "Bearer " + key);
			http.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/lfeliipeadv-stack/meu-pet");   // OpenRouter: shows the app name
			http.DefaultRequestHeaders.Add("X-Title", "desktopPet");
			return http;
		}

		static string Url(string baseUrl, string path) => baseUrl.TrimEnd('/') + "/" + path;

		/// <summary>The models the service offers (to check the connection and fill the list).</summary>
		public static async Task<List<string>> ListModels(string baseUrl, string key)
		{
			if (!IsLocal(baseUrl) && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException(Lang.T("ai.httpsOnly"));
			using (var http = Client(key, 20))
			{
				var resp = await http.GetAsync(Url(baseUrl, "models"));
				string body = await resp.Content.ReadAsStringAsync();
				if (!resp.IsSuccessStatusCode) throw new AiServiceException((int)resp.StatusCode, ErrorText((int)resp.StatusCode, body));
				var data = JsonNode.Parse(body)?["data"] as JsonArray;
				return data?.Select(m => m?["id"]?.GetValue<string>()).Where(m => !string.IsNullOrEmpty(m)).Select(m => m.StartsWith("models/") ? m.Substring(7) : m).ToList()
					?? new List<string>();
			}
		}

		/// <summary>The model to use: the saved one when the service has it, otherwise the preferred ones, otherwise the first.</summary>
		public static string PickModel(AiProvider p, string wanted, List<string> available)
		{
			if (available == null || available.Count == 0) return wanted;
			if (!string.IsNullOrEmpty(wanted) && available.Contains(wanted)) return wanted;
			foreach (var pref in p.Prefer)
			{
				var hit = available.FirstOrDefault(m => m.Equals(pref, StringComparison.OrdinalIgnoreCase))
					?? available.FirstOrDefault(m => m.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0);
				if (hit != null) return hit;
			}
			return available[0];
		}

		/// <summary>Models that are not for chatting (pictures, voice, embeddings…).</summary>
		static readonly Regex NotChat = new Regex(@"tts|image|audio|live|embed|transcribe|robotics|computer-use|lyria|aqa|research|nano-banana|veo|imagen|whisper|guard|moderation", RegexOptions.IgnoreCase);

		/// <summary>
		/// Models to try, best first: the saved one, the preferred names of the service, then the others with the newest
		/// version first (gemini-3.8-flash before gemini-2.5-flash). Only chat models.
		/// </summary>
		public static List<string> Candidates(AiProvider p, string wanted, List<string> available)
		{
			var chat = available.Where(m => !NotChat.IsMatch(m)).ToList();
			var list = new List<string>();
			void Add(string m) { if (m != null && chat.Contains(m) && !list.Contains(m)) list.Add(m); }
			Add(wanted);
			foreach (var pref in p.Prefer)
			{
				Add(chat.FirstOrDefault(m => m.Equals(pref, StringComparison.OrdinalIgnoreCase)));
				foreach (var m in chat.Where(m => m.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0).OrderByDescending(Version)) Add(m);
			}
			foreach (var m in chat.OrderByDescending(Version)) Add(m);
			return list;
		}

		/// <summary>"gemini-3.8-flash" → 3.8 (0 when the name has no version).</summary>
		static double Version(string model)
		{
			var v = Regex.Match(model, @"-(\d+(\.\d+)?)(-|$)");
			return v.Success && double.TryParse(v.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
		}

		/// <summary>
		/// Try the candidates with a tiny question and keep the first that really answers (a service may list a model
		/// it does not let you use, or have one overloaded). Returns the model, or throws with the last error.
		/// </summary>
		public static async Task<string> FindWorkingModel(AiProvider p, string baseUrl, string key, string wanted, List<string> available, int tries = 6)
		{
			Exception last = null;
			foreach (var m in Candidates(p, wanted, available).Take(tries))
			{
				try { await Ask(baseUrl, m, key, "Oi", "Pet", 16); return m; }
				catch (AiServiceException ex) when (ex.Status != 401 && ex.Status != 403) { last = ex; }
			}
			throw last ?? new InvalidOperationException(Lang.T("ai.noModels"));
		}

		/// <summary>
		/// The pet's answer (short, plain text) to a question. If the saved model was retired by the service, another one
		/// is found and saved; if it is overloaded right now, another one answers this time.
		/// </summary>
		// ------------------------------------------------------------------ conversation and personality

		/// <summary>The last questions and answers (so "and then?" makes sense). Forgotten after 15 quiet minutes.</summary>
		static readonly List<(string q, string a)> history = new List<(string, string)>();
		static DateTime lastTalk = DateTime.MinValue;
		const int Remember = 6;

		public static int HistoryCount => history.Count;

		/// <summary>Start a new conversation (the pet forgets what was said).</summary>
		public static void NewConversation() => history.Clear();

		/// <summary>0 friendly (default), 1 to the point, 2 explains like to a child, 3 funny.</summary>
		public static int Style
		{
			get => Program.MyData.GetSettingInt("AiStyle", 0);
			set => Program.MyData.SetSetting("AiStyle", value);
		}

		static string StyleText(bool pt)
		{
			switch (Style)
			{
				case 1: return pt ? "Seja direto e objetivo, sem brincadeiras." : "Be direct and to the point, no jokes.";
				case 2: return pt ? "Explique como se fosse para uma criança de 8 anos, com palavras simples e um exemplo." : "Explain as if to an 8-year-old, with simple words and an example.";
				case 3: return pt ? "Seja bem divertido e brincalhão, com uma piadinha quando couber, mas sem errar a informação." : "Be very playful, with a small joke when it fits, but keep the facts right.";
				default: return pt ? "Seja simpático e carinhoso." : "Be friendly and warm.";
			}
		}

		public static async Task<string> Ask(string question)
		{
			string pet = Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";
			if ((DateTime.Now - lastTalk).TotalMinutes > 15) history.Clear();
			lastTalk = DateTime.Now;
			string answer = await AskOnce(question, pet);
			history.Add((question, answer));
			if (history.Count > Remember) history.RemoveAt(0);
			return answer;
		}

		static async Task<string> AskOnce(string question, string pet)
		{
			try { return await Ask(BaseUrl, Model, Key, question, pet); }
			catch (AiServiceException ex) when (ex.Status == 404 || ex.Status == 400 || ex.Status == 503 || ex.Status == 500)
			{
				var p = Preset(ProviderId);
				var models = await ListModels(BaseUrl, Key);
				var others = models.Where(m => m != Model).ToList();
				string m2 = await FindWorkingModel(p, BaseUrl, Key, null, others, 4);
				if (ex.Status == 404 || ex.Status == 400)
				{
					StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, $"ai: model {Model} not available ({ex.Message}), now using {m2}");
					Model = m2;   // retired: use the new one from now on
				}
				return await Ask(BaseUrl, m2, Key, question, pet);
			}
		}

		public static async Task<string> Ask(string baseUrl, string model, string key, string question, string petName, int maxTokens = 800)
		{
			bool pt = Lang.Current.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
			string system = pt
				? $"Você é {petName}, um bichinho de estimação que vive na área de trabalho do usuário. Responda em português do Brasil, no seu jeito, de forma clara e útil, em no máximo 3 frases curtas. {StyleText(true)} Não use markdown, listas, títulos nem emojis em excesso. Se não souber ou não tiver certeza, diga isso."
				: $"You are {petName}, a desktop pet. Answer in {(Lang.Current.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Spanish" : "English")} (or in the language the user writes in), in your own voice, clearly and helpfully, in at most 3 short sentences. {StyleText(false)} No markdown, lists or headings. If you don't know, say so.";
			var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = system } };
			// what was said before in this conversation (only for the pet's own questions, not the model checks)
			if (maxTokens >= 100)
				foreach (var (q, a) in history)
				{
					messages.Add(new JsonObject { ["role"] = "user", ["content"] = q });
					messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = a });
				}
			messages.Add(new JsonObject { ["role"] = "user", ["content"] = question });
			var req = new JsonObject
			{
				["model"] = model,
				["messages"] = messages,
				["max_tokens"] = maxTokens,
				["temperature"] = 0.6,
				["stream"] = false,
			};
			using (var http = Client(key, IsLocal(baseUrl) ? 120 : 45))
			{
				var resp = await http.PostAsync(Url(baseUrl, "chat/completions"), new StringContent(req.ToJsonString(), Encoding.UTF8, "application/json"));
				string body = await resp.Content.ReadAsStringAsync();
				if (!resp.IsSuccessStatusCode) throw new AiServiceException((int)resp.StatusCode, ErrorText((int)resp.StatusCode, body));
				var msg = JsonNode.Parse(body)?["choices"]?[0]?["message"];
				string text = msg?["content"]?.GetValue<string>() ?? "";
				return Clean(text);
			}
		}

		/// <summary>Plain text for the bubble: no thinking block, no markdown marks.</summary>
		public static string Clean(string text)
		{
			text = Regex.Replace(text ?? "", @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
			text = Regex.Replace(text, @"```[\s\S]*?```", "");
			text = Regex.Replace(text, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^\s*[-*]\s+", "• ", RegexOptions.Multiline);
			text = text.Replace("**", "").Replace("__", "").Replace("`", "");
			text = Regex.Replace(text, @"\n{3,}", "\n\n");
			return text.Trim();
		}

		/// <summary>A message a person understands, from the service's error.</summary>
		static string ErrorText(int status, string body)
		{
			string detail = "";
			try { detail = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? JsonNode.Parse(body)?["error"]?.GetValue<string>() ?? ""; } catch (Exception) { }
			if (detail.Length > 160) detail = detail.Substring(0, 160) + "…";
			switch (status)
			{
				case 401: case 403: return Lang.T("ai.errKey");
				case 404: return Lang.T("ai.errModel") + (detail.Length > 0 ? " (" + detail + ")" : "");
				case 429: return Lang.T("ai.errLimit");
				case 500: case 502: case 503: return Lang.T("ai.errBusy");
				default: return $"HTTP {status}" + (detail.Length > 0 ? ": " + detail : "");
			}
		}
	}
}
