using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopPet
{
	/// <summary>
	/// "DesktopPet.exe --mcp": a small MCP server (JSON-RPC 2.0 over stdin/stdout) that lets Claude Code — or any MCP
	/// client — talk through the pet: pet_say shows a short line in the speech bubble, pet_react plays a reaction,
	/// pet_status checks that the pet is listening. It forwards to the running desktopPet through the local endpoint
	/// of <see cref="AgentBridge"/> (port and secret in %LocalAppData%\DesktopPet\agent.json). No window is opened.
	/// </summary>
	public static class McpServer
	{
		const string ProtocolVersion = "2025-06-18";

		public static int Run(TextReader input, TextWriter output)
		{
			string line;
			while ((line = input.ReadLine()) != null)
			{
				if (line.Trim().Length == 0) continue;
				JsonNode reply;
				try { reply = Handle(JsonNode.Parse(line) as JsonObject); }
				catch (Exception ex) { reply = Error(null, -32700, "Parse error: " + ex.Message); }
				if (reply == null) continue;   // a notification: no answer
				output.Write(reply.ToJsonString() + "\n");
				output.Flush();
			}
			return 0;
		}

		/// <summary>One request (also used by the self-test, without a process).</summary>
		public static JsonNode Handle(JsonObject msg)
		{
			if (msg == null) return Error(null, -32600, "Invalid request");
			var id = msg["id"]?.DeepClone();
			string method = msg["method"]?.GetValue<string>() ?? "";
			if (id == null) return null;   // notifications/initialized and friends

			switch (method)
			{
				case "initialize":
					string asked = msg["params"]?["protocolVersion"]?.GetValue<string>();
					return Result(id, new JsonObject
					{
						["protocolVersion"] = string.IsNullOrEmpty(asked) ? ProtocolVersion : asked,
						["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
						["serverInfo"] = new JsonObject { ["name"] = "desktoppet", ["version"] = System.Windows.Forms.Application.ProductVersion },
						["instructions"] = "Use pet_say to tell the user something short through their desktop pet (finished a long task, need their attention). Keep it to one friendly sentence, no file paths, code or secrets. Use pet_react to show what you are doing.",
					});
				case "ping":
					return Result(id, new JsonObject());
				case "tools/list":
					return Result(id, new JsonObject { ["tools"] = Tools() });
				case "tools/call":
					return Call(id, msg["params"] as JsonObject);
				default:
					return Error(id, -32601, "Method not found: " + method);
			}
		}

		static JsonArray Tools()
		{
			var reactions = new JsonArray();
			foreach (var r in AgentBridge.ReactionNames) reactions.Add(r);
			return new JsonArray
			{
				new JsonObject
				{
					["name"] = "pet_say",
					["description"] = "Show a short message (max 140 characters, one sentence) in the speech bubble of the user's desktop pet. Paths, links and code are not shown.",
					["inputSchema"] = new JsonObject
					{
						["type"] = "object",
						["properties"] = new JsonObject { ["text"] = new JsonObject { ["type"] = "string", ["description"] = "What the pet says.", ["maxLength"] = 140 } },
						["required"] = new JsonArray("text"),
					},
				},
				new JsonObject
				{
					["name"] = "pet_react",
					["description"] = "Make the desktop pet show a reaction (an animation, and for success/waiting a bubble).",
					["inputSchema"] = new JsonObject
					{
						["type"] = "object",
						["properties"] = new JsonObject { ["reaction"] = new JsonObject { ["type"] = "string", ["enum"] = reactions } },
						["required"] = new JsonArray("reaction"),
					},
				},
				new JsonObject
				{
					["name"] = "pet_status",
					["description"] = "Check whether the desktop pet is running and listening.",
					["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
				},
			};
		}

		static JsonNode Call(JsonNode id, JsonObject p)
		{
			string name = p?["name"]?.GetValue<string>() ?? "";
			var args = p?["arguments"] as JsonObject ?? new JsonObject();
			try
			{
				switch (name)
				{
					case "pet_say":
						string text = args["text"]?.GetValue<string>() ?? "";
						if (text.Trim().Length == 0) return ToolText(id, "Nothing to say: 'text' is empty.", true);
						if (!AgentBridge.IsSafeToSay(text))
							return ToolText(id, "Not shown: the pet never shows file paths, links, code or keys. Say it in plain words.", true);
						Send("/say", text);
						return ToolText(id, "The pet said it.", false);
					case "pet_react":
						string reaction = args["reaction"]?.GetValue<string>() ?? "";
						if (AgentBridge.ReactionByName(reaction) == AgentBridge.Activity.None)
							return ToolText(id, "Unknown reaction. Use one of: " + string.Join(", ", AgentBridge.ReactionNames), true);
						Send("/react", reaction);
						return ToolText(id, "The pet reacted: " + reaction, false);
					case "pet_status":
						Send("/ping", "");
						return ToolText(id, "The desktop pet is running and listening.", false);
					default:
						return Error(id, -32602, "Unknown tool: " + name);
				}
			}
			catch (Exception ex)
			{
				return ToolText(id, "The desktop pet is not listening (" + ex.Message + "). Open desktopPet and turn on Settings → Extras → \"React to Claude Code\".", true);
			}
		}

		/// <summary>POST to the running program, with the secret from agent.json.</summary>
		internal static void Send(string path, string body)
		{
			var info = JsonNode.Parse(File.ReadAllText(AgentBridge.ConnectionFile));
			int port = info["port"].GetValue<int>();
			string token = info["token"].GetValue<string>();
			using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
			{
				var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}{path}") { Content = new StringContent(body ?? "", Encoding.UTF8, "text/plain") };
				req.Headers.Add("X-DesktopPet", token);
				var resp = http.SendAsync(req).GetAwaiter().GetResult();
				if ((int)resp.StatusCode == 403) throw new InvalidOperationException("secret refused");
				resp.EnsureSuccessStatusCode();
			}
		}

		static JsonNode Result(JsonNode id, JsonNode result) => new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

		static JsonNode Error(JsonNode id, int code, string message) =>
			new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

		static JsonNode ToolText(JsonNode id, string text, bool isError) => Result(id, new JsonObject
		{
			["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
			["isError"] = isError,
		});
	}
}
