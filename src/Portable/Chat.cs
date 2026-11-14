using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// The chat box above the pet: the conversation in balloons, suggestions to start, a text field, voice (Windows
	/// dictation) and send. Some questions are answered here without the AI (the weather, the next meeting, a reminder
	/// written as a sentence, what the pet can do, how the computer is); the rest goes to the AI when it is set up.
	/// The conversation stays while the program is open ("new conversation" clears it).
	/// </summary>
	internal sealed class FormChat : PetDialog
	{
		public sealed class Message
		{
			public bool User;
			public string Text;
			/// <summary>A button under the message (e.g. "Set up the AI"), optional.</summary>
			public string ActionText; public Action Action;
		}

		static FormChat open;
		static readonly List<Message> messages = new List<Message>();

		/// <summary>Self-test: the conversation.</summary>
		internal static IReadOnlyList<Message> Messages => messages;

		public static bool IsOpen => open != null && !open.IsDisposed && open.Visible;

		/// <summary>Open the chat above the pet (or bring it back), optionally with a text already written.</summary>
		public static void Open(string text = null)
		{
			if (!IsOpen) { open = new FormChat(); open.Show(); }
			open.Activate();
			if (!string.IsNullOrWhiteSpace(text)) { open.input.Text = text; open.input.SelectionStart = text.Length; }
			open.input.Focus();
		}

		/// <summary>
		/// A message sent from the small balloon: the pet thinks and answers in its speech bubble (with "Reply" and
		/// "Open the conversation"); question and answer also go into the conversation.
		/// </summary>
		internal static async void AskInBubble(string text)
		{
			text = (text ?? "").Trim();
			if (text.Length == 0) return;
			messages.Add(new Message { User = true, Text = text });
			Program.Mainthread?.ReactAll("think", "review", "idea", "read");
			SpeechBubble.Say(Lang.T("ask.thinking"), 0);
			Message reply;
			try { reply = await Answer(text); }
			catch (Exception ex) { reply = new Message { Text = Lang.T("ask.error", ex.Message) }; }
			messages.Add(reply);
			Program.Mainthread?.ReactAll("happy", "wave", "idea", "talk");
			var choices = new List<SpeechBubble.Choice>();
			if (reply.ActionText != null && reply.Action != null)
			{
				var r = reply;
				choices.Add(new SpeechBubble.Choice(r.ActionText, () => { r.Action(); r.ActionText = null; }, true));
			}
			choices.Add(new SpeechBubble.Choice(Lang.T("chat.reply"), () => QuickChat.Open(), choices.Count == 0));
			choices.Add(new SpeechBubble.Choice(Lang.T("chat.openFull"), () => Open()));
			SpeechBubble.Say(reply.Text, 30, choices.ToArray());
		}

		readonly TextBox input = new TextBox { BorderStyle = BorderStyle.None, MaxLength = 1000 };
		readonly MessageList list;
		readonly Font fontTitle = Theme.CreateFont(15, FontStyle.Bold), fontSmall = Theme.CreateFont(12), fontText = Theme.CreateFont(14), fontEmoji = Theme.CreateFont(18), fontPill = Theme.CreateFont(12.5f, FontStyle.Bold);
		Rectangle closeRect, clearRect, sendRect, talkRect, inputRect;
		string hover;
		bool busy;
		DateTime keepOpenUntil = DateTime.MinValue;
		bool below;   // placed under the pet (no room above): no tip
		float tipX;   // where the tip is, so it points at the pet also when the window is pushed in from the screen edge
		const int W = 400, H = 500, Header = 56, Footer = 68, Tip = 9;

		FormChat()
		{
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			KeyPreview = true;
			DoubleBuffered = true;
			BackColor = Theme.Surface;
			ClientSize = new Size(W, H + Tip);
			Text = Lang.T("chat.title");

			list = new MessageList(this) { Location = new Point(1, Header), Size = new Size(W - 2, H - Header - Footer) };
			Controls.Add(list);
			inputRect = new Rectangle(14, H - Footer + 13, W - 28 - 52, 42);
			sendRect = new Rectangle(W - 14 - 42, H - Footer + 13, 42, 42);
			input.Font = fontText;
			input.BackColor = Theme.Surface;
			input.ForeColor = Theme.Text;
			input.PlaceholderText = Lang.T("chat.placeholder", PetName);
			input.Location = new Point(inputRect.X + 14, inputRect.Y + (inputRect.Height - input.PreferredHeight) / 2);
			input.Width = inputRect.Width - 28;
			input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(input.Text); } };
			Controls.Add(input);
			closeRect = new Rectangle(W - 42, 14, 28, 28);
			using (var g = CreateGraphics())
			{
				int tw = Theme.MeasureText(g, Lang.T("ask.voice"), fontPill, int.MaxValue, TextFormatFlags.SingleLine).Width + 44;
				talkRect = new Rectangle(closeRect.X - 8 - tw, 14, tw, 28);
			}
			clearRect = new Rectangle(talkRect.X - 34, 14, 28, 28);
			Place();
			Region = new Region(below ? Theme.RoundedRect(new RectangleF(0, 0, W, H), 18) : ChatIcons.Balloon(W, H, Tip, 18, tipX));
		}

		static string PetName => Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";

		protected override bool ShowWithoutActivation => false;

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.ExStyle |= 0x00000080;     // WS_EX_TOOLWINDOW: not in Alt+Tab
				return cp;
			}
		}

		[DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			int round = 2;   // DWMWCP_ROUND: rounded corners on Windows 11
			try { DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); } catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		/// <summary>Above the pet, inside the screen (below it when there is no room above).</summary>
		void Place()
		{
			var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
			var wa = pet != null ? Screen.FromControl(pet).WorkingArea : Screen.PrimaryScreen.WorkingArea;
			int x = pet != null ? pet.Left + pet.Width / 2 - Width / 2 : wa.Right - Width - 24;
			int y = pet != null ? pet.Top - Height + 4 : wa.Bottom - Height - 24;   // the tip just over the pet's head
			below = pet != null && y < wa.Top;   // no room above: below the pet, without the tip
			if (below) y = pet.Bottom + 10;
			Location = new Point(Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)), Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y)));
			tipX = Math.Max(Tip + 24, Math.Min(Width - Tip - 24, pet != null ? pet.Left + pet.Width / 2f - Left : Width / 2f));
		}

		// ------------------------------------------------------------------ drawing the frame

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			Theme.Prepare(g);
			g.Clear(Theme.Surface);
			// header: ✨ pet name · state pill · 🎤 Talk · (clear) · ✕
			Theme.DrawText(g, "✨", fontEmoji, new Rectangle(12, 13, 30, 30), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
			int right = (messages.Count > 0 ? clearRect.X : talkRect.X) - 8;
			int nameW = Math.Min(Theme.MeasureText(g, PetName, fontTitle, int.MaxValue, TextFormatFlags.SingleLine).Width, right - 46 - 90);
			Theme.DrawText(g, PetName, fontTitle, new Rectangle(46, 14, nameW, 28), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			string state = busy ? Lang.T("chat.thinking") : AiAssistant.Configured ? Lang.T("chat.ready") : Lang.T("chat.readyNoAi");
			var dot = busy ? Color.Orange : AiAssistant.Configured ? Color.FromArgb(46, 160, 90) : Theme.TextMuted;
			int sw = Math.Min(Theme.MeasureText(g, state, fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 30, right - (46 + nameW + 8));
			var pill = new Rectangle(46 + nameW + 8, 16, Math.Max(0, sw), 24);
			if (pill.Width > 30)
			{
				Theme.FillRounded(g, Theme.SurfaceAlt, pill, 12);
				Theme.DrawRounded(g, Theme.Border, pill, 12);
				using (var b = new SolidBrush(dot)) g.FillEllipse(b, pill.X + 10, pill.Y + 8, 8, 8);
				Theme.DrawText(g, state, fontSmall, new Rectangle(pill.X + 22, pill.Y, pill.Width - 26, pill.Height), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			}
			// 🎤 Talk: the Windows recognizer listens (green while the microphone is open; a click stops)
			var talkFill = listening != null && micOpen ? ChatIcons.Green : hover == "talk" ? Theme.Hover : Theme.Surface;
			var talkInk = listening != null && micOpen ? Color.White : Theme.Text;
			Theme.FillRounded(g, talkFill, talkRect, 14);
			Theme.DrawRounded(g, listening != null ? ChatIcons.Green : Theme.Border, talkRect, 14);
			ChatIcons.Mic(g, new RectangleF(talkRect.X + 12, talkRect.Y + 6, 16, 16), talkInk);
			Theme.DrawText(g, Lang.T(listening != null ? "voice.short" : "ask.voice"), fontPill, new Rectangle(talkRect.X + 32, talkRect.Y, talkRect.Width - 34, talkRect.Height), talkInk, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			if (messages.Count > 0) IconButton(g, clearRect, "🧹", "clear");
			if (hover == "close") Theme.FillRounded(g, Theme.Hover, closeRect, 8);
			ChatIcons.Close(g, Rectangle.Inflate(closeRect, -8, -8), Theme.TextMuted);
			using (var p = new Pen(Theme.Border)) { g.DrawLine(p, 0, Header - 1, W, Header - 1); g.DrawLine(p, 0, H - Footer, W, H - Footer); }
			// input field and send
			Theme.FillRounded(g, Theme.Surface, inputRect, 14);
			Theme.DrawRounded(g, input.Focused ? Theme.Accent : Theme.Border, inputRect, 14, input.Focused ? 2f : 1f);
			bool canSend = input.Text.Trim().Length > 0 && !busy;
			Theme.FillRounded(g, canSend ? Theme.Accent : (hover == "send" ? Theme.Hover : Theme.SurfaceAlt), sendRect, 12);
			ChatIcons.Send(g, Rectangle.Inflate(sendRect, -12, -12), canSend ? Theme.AccentText : Theme.TextMuted);
			// outline of the balloon, with its tip towards the pet
			ChatIcons.DrawBalloonOutline(g, W, H, below ? 0 : Tip, 18, Theme.Border, Theme.Surface, tipX);
		}

		void IconButton(Graphics g, Rectangle r, string text, string id)
		{
			if (hover == id) Theme.FillRounded(g, Theme.Hover, r, 8);
			Theme.DrawText(g, text, text == "✕" ? fontText : fontSmall, r, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			string h = closeRect.Contains(e.Location) ? "close" : messages.Count > 0 && clearRect.Contains(e.Location) ? "clear" : talkRect.Contains(e.Location) ? "talk" : sendRect.Contains(e.Location) ? "send" : null;
			Cursor = h != null ? Cursors.Hand : e.Y < Header ? Cursors.SizeAll : Cursors.Default;
			if (h != hover) { hover = h; Invalidate(); }
		}

		[DllImport("user32.dll")] static extern bool ReleaseCapture();
		[DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button != MouseButtons.Left) return;
			if (closeRect.Contains(e.Location)) { Close(); return; }
			if (messages.Count > 0 && clearRect.Contains(e.Location)) { NewConversation(); return; }
			if (talkRect.Contains(e.Location)) { Listen(); return; }
			if (sendRect.Contains(e.Location)) { Send(input.Text); return; }
			if (inputRect.Contains(e.Location)) { input.Focus(); return; }
			if (e.Y < Header) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); }   // drag the window by its header
		}

		protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			input.TextChanged += (s, a) => Invalidate(sendRect);
			input.GotFocus += (s, a) => Invalidate(inputRect);
			input.LostFocus += (s, a) => Invalidate(inputRect);
			list.ScrollToEnd();
		}

		/// <summary>A click outside closes it (not while the Windows dictation is typing, nor while the pet thinks).</summary>
		protected override void OnDeactivate(EventArgs e)
		{
			base.OnDeactivate(e);
			if (busy || DateTime.Now < keepOpenUntil) return;
			BeginInvoke(new Action(() => { if (!IsDisposed && Form.ActiveForm != this) Close(); }));
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			listening?.Cancel();   // closed while listening: the microphone stops
			if (open == this) open = null;
			base.OnFormClosed(e);
		}

		System.Threading.CancellationTokenSource listening;
		bool micOpen;

		/// <summary>Listen with the Windows recognizer (no window opens) and send what it heard; a second click stops.</summary>
		async void Listen()
		{
			if (listening != null) { listening.Cancel(); return; }
			if (busy) return;
			listening = new System.Threading.CancellationTokenSource();
			micOpen = false;
			keepOpenUntil = DateTime.Now.AddMinutes(1);
			string placeholder = input.PlaceholderText;
			input.PlaceholderText = Lang.T("voice.starting");
			Invalidate();
			var (outcome, text) = await VoiceInput.Listen(() => { micOpen = true; if (!IsDisposed) { input.PlaceholderText = Lang.T("voice.listening"); Invalidate(); } }, listening.Token);
			listening.Dispose(); listening = null; micOpen = false;
			keepOpenUntil = DateTime.MinValue;
			if (IsDisposed) return;
			input.PlaceholderText = placeholder;
			Invalidate();
			switch (outcome)
			{
				case VoiceInput.Outcome.Text: Send(text); break;
				case VoiceInput.Outcome.Nothing: input.PlaceholderText = Lang.T("voice.nothing"); break;
				case VoiceInput.Outcome.Privacy:
				case VoiceInput.Outcome.Failed: Close(); VoiceInput.Explain(outcome); break;
			}
		}

		void NewConversation()
		{
			messages.Clear();
			AiAssistant.NewConversation();
			list.Invalidate();
			Invalidate();
			input.Focus();
		}

		// ------------------------------------------------------------------ sending

		internal async void Send(string text)
		{
			text = (text ?? "").Trim();
			if (text.Length == 0 || busy) return;
			input.Text = "";
			messages.Add(new Message { User = true, Text = text });
			busy = true;
			Invalidate(); list.ScrollToEnd();
			Program.Mainthread?.ReactAll("think", "review", "idea", "read");
			Message reply;
			try { reply = await Answer(text); }
			catch (Exception ex) { reply = new Message { Text = Lang.T("ask.error", ex.Message) }; }
			busy = false;
			if (IsDisposed) { SpeechBubble.Say(reply.Text, 12); return; }   // closed meanwhile: the bubble says it
			messages.Add(reply);
			Program.Mainthread?.ReactAll("happy", "wave", "idea", "talk");
			Invalidate(); list.ScrollToEnd();
			input.Focus();
		}

		/// <summary>The reply: answered here when it can be, otherwise by the AI.</summary>
		internal static async Task<Message> Answer(string text)
		{
			var local = await LocalAnswer(text);
			if (local != null) return local;
			if (!AiAssistant.Configured)
				return new Message { Text = Lang.T("chat.needAi"), ActionText = Lang.T("ask.settings"), Action = () => FormSettings.ShowPage(FormSettings.Page.Ai) };
			if (!FormAsk.PrivacyOk()) return new Message { Text = Lang.T("chat.privacyNo") };
			string answer = await AiAssistant.Ask(text);
			return new Message { Text = answer.Length > 0 ? answer : Lang.T("ask.empty") };
		}

		const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

		/// <summary>Questions answered on this computer: reminders, weather, meetings, the computer, what the pet can do.</summary>
		internal static async Task<Message> LocalAnswer(string text)
		{
			if (SmartReminder.LooksLikeReminder(text))
			{
				// read here; only a sentence it cannot read goes to the AI (after the privacy notice)
				var r = SmartReminder.Parse(text, DateTime.Now) ?? (AiAssistant.Configured && FormAsk.PrivacyOk() ? await SmartReminder.Understand(text) : null);
				if (r == null) return new Message { Text = Lang.T("rem.smartFail") };
				Reminders.Add(r);
				return new Message { Text = Lang.T("rem.smartOk", r.Text, SmartReminder.When(r, DateTime.Now)), ActionText = Lang.T("rem.smartUndo"), Action = () => { Reminders.Remove(r); messages.Add(new Message { Text = Lang.T("rem.smartUndone") }); open?.list.Invalidate(); } };
			}
			if (Regex.IsMatch(text, @"\b(tempo|clima|chov\w*|chuva|guarda-chuva|weather|rain\w*|umbrella|tiempo|llov\w*|lluvia|paraguas)\b", I) && !Regex.IsMatch(text, @"\b(quanto tempo|how long|cu[aá]nto tiempo)\b", I))
			{
				if (Weather.City == null)
					return new Message { Text = Lang.T("weather.noCity"), ActionText = Lang.T("weather.choose"), Action = () => FormSettings.ShowPage(FormSettings.Page.Plugins) };
				var w = await Weather.Current();
				return new Message { Text = w == null ? Lang.T("weather.offline") : Weather.Summary(w, Weather.City) + " " + Lang.T("weather.nowTemp", Math.Round(w.Now)) };
			}
			if (Regex.IsMatch(text, @"\b(reuni[aãoõ]\w*|agenda|compromisso\w*|meeting\w*|calendar|reuni[oó]n\w*|cita)\b", I))
			{
				if (!Agenda.Configured)
					return new Message { Text = Lang.T("chat.noAgenda"), ActionText = Lang.T("opt.agenda.button"), Action = () => FormSettings.ShowPage(FormSettings.Page.Plugins) };
				var next = Agenda.Next;
				return new Message { Text = next == null ? Lang.T("menu.agendaNone") : Lang.T("chat.nextMeeting", next.Title, next.Start.Date == DateTime.Today ? next.Start.ToString("HH:mm") : next.Start.ToString("g")) };
			}
			if (Regex.IsMatch(text, @"\b(cpu|mem[oó]ria|memory|ram|computador|computer|ordenador|sistema|system)\b", I))
				return new Message { Text = Lang.T("chat.system", PcMonitor.Cpu, PcMonitor.Ram, Reminders.All.Count(r => r.Enabled)) };
			if (Regex.IsMatch(text, @"(o que (voc[eê] )?sabe fazer|o que voc[eê] faz|ajuda|what can you do|help|qu[eé] puedes hacer|ayuda)", I))
				return new Message { Text = Lang.T("chat.help") };
			return null;
		}

		/// <summary>The suggestions shown while the conversation is empty.</summary>
		internal static string[] Suggestions => new[] { Lang.T("chat.sugHelp"), Lang.T("chat.sugWeather"), Lang.T("chat.sugRemind"), Lang.T("chat.sugMeeting") };

		// ------------------------------------------------------------------ the conversation

		sealed class MessageList : Control
		{
			readonly FormChat chat;
			int scroll, contentH;
			readonly List<(Rectangle rect, Action run)> hits = new List<(Rectangle, Action)>();
			Rectangle hoverRect;
			readonly Font bigEmoji = Theme.CreateFont(30);

			public MessageList(FormChat c)
			{
				chat = c;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
				BackColor = Theme.Surface;
			}

			public void ScrollToEnd() { scroll = int.MaxValue; Invalidate(); }

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				hits.Clear();
				int maxW = Width * 78 / 100;
				if (messages.Count == 0 && !chat.busy) { PaintEmpty(g); return; }

				// measure everything, then draw from the scroll position
				var items = new List<(Message m, Size size)>();
				int total = 12;
				foreach (var m in messages)
				{
					var s = Theme.MeasureText(g, m.Text, chat.fontText, maxW - 24, TextFormatFlags.WordBreak);
					items.Add((m, s));
					total += s.Height + 20 + (m.ActionText != null ? 38 : 0) + 10;
				}
				if (chat.busy) total += 44;
				contentH = total;
				int max = Math.Max(0, contentH - Height);
				scroll = Math.Max(0, Math.Min(max, scroll));
				int y = 12 - scroll;
				foreach (var (m, s) in items)
				{
					int bw = Math.Min(maxW, s.Width + 24), bh = s.Height + 20;
					var r = new Rectangle(m.User ? Width - bw - 12 : 12, y, bw, bh);
					Theme.FillRounded(g, m.User ? Theme.Accent : Theme.Surface, r, 14);
					if (!m.User) Theme.DrawRounded(g, Theme.Border, r, 14);
					Theme.DrawText(g, m.Text, chat.fontText, new Rectangle(r.X + 12, r.Y + 10, bw - 24, s.Height), m.User ? Theme.AccentText : Theme.Text, TextFormatFlags.WordBreak);
					y += bh + 6;
					if (m.ActionText != null)
					{
						int aw = Theme.MeasureText(g, m.ActionText, chat.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 28;
						var a = new Rectangle(12, y, aw, 30);
						Theme.DrawButton(g, a, m.ActionText, chat.fontSmall, false, hoverRect == a);
						var msg = m;
						hits.Add((a, () => { msg.Action?.Invoke(); msg.ActionText = null; Invalidate(); }));
						y += 38;
					}
					y += 4;
				}
				if (chat.busy)
				{
					var r = new Rectangle(12, y, 70, 34);
					Theme.FillRounded(g, Theme.Surface, r, 14);
					Theme.DrawRounded(g, Theme.Border, r, 14);
					int dots = (int)(DateTime.Now.Millisecond / 333) + 1;
					Theme.DrawText(g, new string('•', dots), chat.fontText, r, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					BeginInvoke(new Action(async () => { await Task.Delay(300); if (!IsDisposed) Invalidate(); }));
				}
			}

			/// <summary>Empty: a hello and suggestions to tap.</summary>
			void PaintEmpty(Graphics g)
			{
				Theme.DrawText(g, "🐾", bigEmoji, new Rectangle(0, 34, Width, 44), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
				Theme.DrawText(g, Lang.T("chat.hello", PetName), chat.fontTitle, new Rectangle(16, 86, Width - 32, 26), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T(AiAssistant.Configured ? "chat.helloSub" : "chat.helloSubNoAi"), chat.fontSmall, new Rectangle(24, 116, Width - 48, 40), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
				int x = 16, y = 176;
				foreach (string sug in Suggestions)
				{
					int w = Theme.MeasureText(g, sug, chat.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 26;
					if (x + w > Width - 16) { x = 16; y += 40; }
					var r = new Rectangle(x, y, w, 32);
					Theme.FillRounded(g, hoverRect == r ? Theme.Hover : Theme.Surface, r, 16);
					Theme.DrawRounded(g, Theme.Border, r, 16);
					Theme.DrawText(g, sug, chat.fontSmall, r, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					string text = sug;
					hits.Add((r, () =>
					{
						// "Remind me to…" fills the field to complete; the others are asked at once
						if (text == Lang.T("chat.sugRemind")) { chat.input.Text = Lang.T("chat.sugRemindFill"); chat.input.Focus(); chat.input.SelectionStart = chat.input.Text.Length; }
						else chat.Send(text);
					}));
					x += w + 8;
				}
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				scroll -= e.Delta / 120 * 50;
				Invalidate();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var h = hits.FirstOrDefault(x => x.rect.Contains(e.Location)).rect;
				Cursor = h.IsEmpty ? Cursors.Default : Cursors.Hand;
				if (h != hoverRect) { hoverRect = h; Invalidate(); }
			}

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				hits.FirstOrDefault(x => x.rect.Contains(e.Location)).run?.Invoke();
			}

			/// <summary>Self-test: what can be clicked.</summary>
			internal int Clickable => hits.Count;
		}

		/// <summary>Self-test: the window, its list and sending a text.</summary>
		internal int TestClickable => list.Clickable;
		internal bool TestBusy => busy;
	}

	/// <summary>The small drawn icons of the chat (message, microphone, send, close) and the balloon shape with its tip.</summary>
	internal static class ChatIcons
	{
		public static readonly Color Blue = Color.FromArgb(37, 99, 235), Green = Color.FromArgb(22, 150, 80);

		static Pen Line(Color c, RectangleF r) => new Pen(c, Math.Max(1.6f, r.Width / 9f)) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round, StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };

		/// <summary>A speech balloon: rounded square with a little tail.</summary>
		public static void Message(Graphics g, RectangleF r, Color c)
		{
			using (var pen = Line(c, r))
			{
				var body = new RectangleF(r.X, r.Y + r.Height * 0.04f, r.Width, r.Height * 0.72f);
				using (var path = Theme.RoundedRect(body, r.Width * 0.24f)) g.DrawPath(pen, path);
				using (var b = new SolidBrush(c))
					g.FillPolygon(b, new[] { new PointF(r.X + r.Width * 0.2f, body.Bottom - pen.Width / 2), new PointF(r.X + r.Width * 0.16f, r.Bottom), new PointF(r.X + r.Width * 0.48f, body.Bottom - pen.Width / 2) });
			}
		}

		/// <summary>A microphone: capsule, the holder under it and the stand.</summary>
		public static void Mic(Graphics g, RectangleF r, Color c)
		{
			using (var pen = Line(c, r))
			{
				float cx = r.X + r.Width / 2, cw = r.Width * 0.38f;
				var capsule = new RectangleF(cx - cw / 2, r.Y, cw, r.Height * 0.6f);
				using (var path = Theme.RoundedRect(capsule, cw / 2)) g.DrawPath(pen, path);
				var arc = new RectangleF(cx - r.Width * 0.34f, r.Y + r.Height * 0.18f, r.Width * 0.68f, r.Height * 0.58f);
				g.DrawArc(pen, arc, 0, 180);
				g.DrawLine(pen, cx, arc.Bottom, cx, r.Bottom);
				g.DrawLine(pen, cx - r.Width * 0.18f, r.Bottom, cx + r.Width * 0.18f, r.Bottom);
			}
		}

		/// <summary>A paper plane pointing right.</summary>
		public static void Send(Graphics g, RectangleF r, Color c)
		{
			using (var pen = Line(c, r))
			{
				var pts = new[] { new PointF(r.X, r.Y + r.Height * 0.05f), new PointF(r.Right, r.Y + r.Height / 2), new PointF(r.X, r.Bottom - r.Height * 0.05f), new PointF(r.X + r.Width * 0.2f, r.Y + r.Height / 2) };
				g.DrawPolygon(pen, pts);
				g.DrawLine(pen, pts[3], new PointF(r.X + r.Width * 0.55f, r.Y + r.Height / 2));
			}
		}

		public static void Close(Graphics g, RectangleF r, Color c)
		{
			using (var pen = Line(c, r)) { g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom); g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom); }
		}

		/// <summary>The shape of a balloon window: a rounded card of w x h and a small tip under its middle.</summary>
		public static System.Drawing.Drawing2D.GraphicsPath Balloon(int w, int h, int tip, int radius, float tipX)
		{
			var path = Theme.RoundedRect(new RectangleF(0, 0, w, h), radius);
			path.AddPolygon(new[] { new PointF(tipX - tip - 1, h - 2), new PointF(tipX, h + tip), new PointF(tipX + tip + 1, h - 2) });
			path.FillMode = System.Drawing.Drawing2D.FillMode.Winding;
			return path;
		}

		/// <summary>The border of <see cref="Balloon"/>: the card and the two sides of the tip, open where they meet.</summary>
		public static void DrawBalloonOutline(Graphics g, int w, int h, int tip, int radius, Color border, Color fill, float tipX)
		{
			using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, w - 1, h - 1.5f), radius))
			using (var p = new Pen(border)) g.DrawPath(p, path);
			using (var b = new SolidBrush(fill))
				g.FillPolygon(b, new[] { new PointF(tipX - tip, h - 2), new PointF(tipX, h + tip - 1), new PointF(tipX + tip, h - 2) });
			using (var p = new Pen(border))
			{
				g.DrawLine(p, tipX - tip, h - 1, tipX, h + tip - 1);
				g.DrawLine(p, tipX, h + tip - 1, tipX + tip, h - 1);
			}
		}
	}

	/// <summary>
	/// The small chat balloon above the head of the pet (the message button beside the pet): a field to write and send,
	/// a button that opens the whole conversation, and a close button. The pet answers in its speech bubble.
	/// </summary>
	internal sealed class QuickChat : PetDialog
	{
		static QuickChat open;
		public static bool IsOpen => open != null && !open.IsDisposed && open.Visible;
		/// <summary>Self-test: the balloon on screen.</summary>
		internal static QuickChat Current => IsOpen ? open : null;
		/// <summary>Self-test: where the tip is, from the left of the balloon.</summary>
		internal int TestTipX => (int)Math.Round(tipX);

		/// <summary>Open the balloon above the pet (with voice, it listens right away and sends what it hears).</summary>
		public static void Open(bool voice = false)
		{
			if (!IsOpen) { open = new QuickChat(); open.Show(); }
			open.Activate();
			open.input.Focus();
			if (voice) open.Listen();
		}

		System.Threading.CancellationTokenSource listening;
		bool micOpen;   // the recognizer is ready: talk now
		/// <summary>Self-test: listening now.</summary>
		internal bool TestListening => listening != null;

		/// <summary>Listen with the Windows recognizer (no window opens), put what it heard in the field and send it.</summary>
		internal async void Listen()
		{
			if (listening != null) { listening.Cancel(); return; }   // a second click stops
			listening = new System.Threading.CancellationTokenSource();
			micOpen = false;
			keepOpenUntil = DateTime.Now.AddMinutes(1);
			string placeholder = input.PlaceholderText;
			input.PlaceholderText = Lang.T("voice.starting");
			input.Text = "";
			Invalidate();
			var (outcome, text) = await VoiceInput.Listen(() => { micOpen = true; if (!IsDisposed) { input.PlaceholderText = Lang.T("voice.listening"); Invalidate(); } }, listening.Token);
			listening.Dispose(); listening = null; micOpen = false;
			keepOpenUntil = DateTime.MinValue;
			if (IsDisposed) return;
			input.PlaceholderText = placeholder;
			Invalidate();
			switch (outcome)
			{
				case VoiceInput.Outcome.Text: input.Text = text; Send(); break;
				case VoiceInput.Outcome.Nothing: input.PlaceholderText = Lang.T("voice.nothing"); break;
				case VoiceInput.Outcome.Privacy:
				case VoiceInput.Outcome.Failed: Close(); VoiceInput.Explain(outcome); break;
			}
		}

		const int W = 300, CardH = 92, Tip = 9;
		readonly TextBox input = new TextBox { BorderStyle = BorderStyle.None, MaxLength = 1000 };
		readonly Font fontTitle = Theme.CreateFont(14, FontStyle.Bold), fontText = Theme.CreateFont(13.5f), fontEmoji = Theme.CreateFont(15);
		readonly Rectangle expandRect, closeRect, inputRect, sendRect;
		readonly ToolTip tips = new ToolTip();
		readonly Timer follow = new Timer { Interval = 100 };   // stays over the pet if it moves
		string hover;
		DateTime keepOpenUntil = DateTime.MinValue;
		bool below;   // placed under the pet (no room above): no tip
		float tipX;   // where the tip is, so it points at the pet also when the window is pushed in from the screen edge

		QuickChat()
		{
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			KeyPreview = true;
			DoubleBuffered = true;
			BackColor = Theme.Surface;
			ClientSize = new Size(W, CardH + Tip);
			Text = Lang.T("chat.quick");
			closeRect = new Rectangle(W - 38, 10, 26, 26);
			expandRect = new Rectangle(W - 70, 10, 26, 26);
			inputRect = new Rectangle(12, 44, W - 12 - 12 - 46, 38);
			sendRect = new Rectangle(W - 12 - 38, 44, 38, 38);
			input.Font = fontText;
			input.BackColor = Theme.Surface;
			input.ForeColor = Theme.Text;
			input.PlaceholderText = Lang.T("chat.placeholder", Program.Mainthread?.CurrentHeader?.Petname ?? "Pet");
			input.Location = new Point(inputRect.X + 12, inputRect.Y + (inputRect.Height - input.PreferredHeight) / 2);
			input.Width = inputRect.Width - 24;
			input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); } };
			Controls.Add(input);
			Place();
			Shape();
			follow.Tick += (s, e) => { var was = (Location, below, tipX); Place(); if ((Location, below, tipX) != was) { Shape(); Invalidate(); } };
			follow.Start();
		}

		protected override CreateParams CreateParams
		{
			get { var cp = base.CreateParams; cp.ExStyle |= 0x00000080; return cp; }   // not in Alt+Tab
		}

		/// <summary>The window's shape: the card, with the tip at the pet (no tip when the balloon is under the pet).</summary>
		void Shape() => Region = new Region(below ? Theme.RoundedRect(new RectangleF(0, 0, W, CardH), 16) : ChatIcons.Balloon(W, CardH, Tip, 16, tipX));

		/// <summary>Over the head of the pet, its tip pointing at the pet.</summary>
		void Place()
		{
			var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow && p.Opacity > 0);
			var wa = pet != null ? Screen.FromControl(pet).WorkingArea : Screen.PrimaryScreen.WorkingArea;
			int x = pet != null ? pet.Left + pet.Width / 2 - Width / 2 : wa.Right - Width - 24;
			int y = pet != null ? pet.Top - Height + 6 : wa.Bottom - Height - 24;
			below = pet != null && y < wa.Top;   // no room above: below the pet, without the tip
			if (below) y = pet.Bottom + 10;
			Location = new Point(Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)), Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y)));
			tipX = Math.Max(Tip + 24, Math.Min(Width - Tip - 24, pet != null ? pet.Left + pet.Width / 2f - Left : Width / 2f));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			Theme.Prepare(g);
			g.Clear(Theme.Surface);
			Theme.DrawText(g, "✨", fontEmoji, new Rectangle(12, 10, 24, 26), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
			Theme.DrawText(g, Lang.T("chat.quick"), fontTitle, new Rectangle(40, 10, expandRect.X - 46, 26), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			foreach (var (r, id) in new[] { (expandRect, "expand"), (closeRect, "close") })
			{
				Theme.FillRounded(g, hover == id ? Theme.Hover : Theme.Surface, r, 7);
				Theme.DrawRounded(g, Theme.Border, r, 7);
				var ico = RectangleF.Inflate(r, -7, -7);
				if (id == "expand") ChatIcons.Message(g, ico, Theme.Text); else ChatIcons.Close(g, RectangleF.Inflate(ico, -1, -1), Theme.Text);
			}
			Theme.FillRounded(g, Theme.Surface, inputRect, 12);
			Theme.DrawRounded(g, input.Focused ? Theme.Accent : Theme.Border, inputRect, 12, input.Focused ? 2f : 1f);
			// the button: send when there is text; otherwise the microphone (green while it listens)
			bool canSend = input.Text.Trim().Length > 0 && listening == null;
			var icon = RectangleF.Inflate(sendRect, -11, -11);
			if (canSend)
			{
				Theme.FillRounded(g, Theme.Accent, sendRect, 11);
				ChatIcons.Send(g, icon, Theme.AccentText);
			}
			else if (listening != null)
			{
				Theme.FillRounded(g, micOpen ? ChatIcons.Green : Theme.SurfaceAlt, sendRect, 11);
				ChatIcons.Mic(g, icon, micOpen ? Color.White : ChatIcons.Green);
			}
			else
			{
				Theme.FillRounded(g, hover == "send" ? Theme.Hover : Theme.SurfaceAlt, sendRect, 11);
				ChatIcons.Mic(g, icon, ChatIcons.Green);
			}
			ChatIcons.DrawBalloonOutline(g, W, CardH, below ? 0 : Tip, 16, Theme.Border, Theme.Surface, tipX);
		}

		/// <summary>The button beside the field: send with text, otherwise listen (or stop listening).</summary>
		void SendOrListen()
		{
			if (listening == null && input.Text.Trim().Length > 0) Send();
			else Listen();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			string h = expandRect.Contains(e.Location) ? "expand" : closeRect.Contains(e.Location) ? "close" : sendRect.Contains(e.Location) ? "send" : null;
			Cursor = h != null ? Cursors.Hand : Cursors.Default;
			if (h == hover) return;
			hover = h;
			string sendTip = listening != null ? Lang.T("voice.stop") : input.Text.Trim().Length > 0 ? Lang.T("chat.send") : Lang.T("hover.talk");
			tips.SetToolTip(this, h == "expand" ? Lang.T("chat.openFull") : h == "close" ? Lang.T("chat.close") : h == "send" ? sendTip : null);
			Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = null; Invalidate(); }

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button != MouseButtons.Left) return;
			if (closeRect.Contains(e.Location)) Close();
			else if (expandRect.Contains(e.Location)) Expand();
			else if (sendRect.Contains(e.Location)) SendOrListen();
			else if (inputRect.Contains(e.Location)) input.Focus();
		}

		/// <summary>The whole conversation, with what was written here.</summary>
		internal void Expand()
		{
			string text = input.Text;
			keepOpenUntil = DateTime.Now.AddSeconds(2);   // not closed by the deactivation while the conversation opens
			FormChat.Open(text);
			Close();
		}

		internal void Send(string text = null)
		{
			text = (text ?? input.Text).Trim();
			if (text.Length == 0) return;
			Close();
			FormChat.AskInBubble(text);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			input.TextChanged += (s, a) => Invalidate(sendRect);
			input.GotFocus += (s, a) => Invalidate(inputRect);
			input.LostFocus += (s, a) => Invalidate(inputRect);
		}

		/// <summary>A click elsewhere closes it (not while the Windows dictation is typing into it).</summary>
		protected override void OnDeactivate(EventArgs e)
		{
			base.OnDeactivate(e);
			if (DateTime.Now < keepOpenUntil) return;
			BeginInvoke(new Action(() => { if (!IsDisposed && Form.ActiveForm != this) Close(); }));
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			listening?.Cancel();   // closed while listening: the microphone stops
			follow.Stop();
			if (open == this) open = null;
			base.OnFormClosed(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing) { follow.Dispose(); fontTitle.Dispose(); fontText.Dispose(); fontEmoji.Dispose(); tips.Dispose(); }
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// Listening with the speech recognizer of Windows, without opening any window: a hidden Windows PowerShell
	/// (in every Windows 10 and 11) asks the recognizer for one sentence and writes it back. This keeps the program
	/// small (the Windows Runtime libraries for .NET would make it ten times bigger).
	/// Free sentences need "Online speech recognition" on in the Windows privacy settings.
	/// </summary>
	internal static class VoiceInput
	{
		public enum Outcome { Text, Nothing, Privacy, Failed, Canceled }

		// S:listening when ready, R:<text> at the end, E:<reason> on error (ASCII only: Windows PowerShell reads it as is)
		const string Script = @"$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
try {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime
  $null = [Windows.Media.SpeechRecognition.SpeechRecognizer, Windows.Media.SpeechRecognition, ContentType = WindowsRuntime]
  $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
  function Wait($op, [Type]$t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(); $task.Result }
  $r = New-Object Windows.Media.SpeechRecognition.SpeechRecognizer
  $r.Timeouts.InitialSilenceTimeout = [TimeSpan]::FromSeconds(7)
  $r.Timeouts.EndSilenceTimeout = [TimeSpan]::FromSeconds(1.2)
  $compiled = Wait ($r.CompileConstraintsAsync()) ([Windows.Media.SpeechRecognition.SpeechRecognitionCompilationResult])
  if ($compiled.Status -ne 'Success') { 'E:compile:' + $compiled.Status; exit 1 }
  'S:listening'; [Console]::Out.Flush(); $sw = [Diagnostics.Stopwatch]::StartNew()
  $res = Wait ($r.RecognizeAsync()) ([Windows.Media.SpeechRecognition.SpeechRecognitionResult])
  'I:' + $res.Status + ' confidence=' + $res.Confidence + ' raw=' + [Math]::Round($res.RawConfidence, 2) + ' ms=' + $sw.ElapsedMilliseconds + ' duration=' + [Math]::Round($res.PhraseDuration.TotalSeconds, 1) + ' alternates=' + (($res.GetAlternates(3) | % { $_.Text }) -join ' | ')
  if ($res.Status -ne 'Success') { 'E:status:' + $res.Status; exit 1 }
  'R:' + $res.Text
}
catch {
  $h = $_.Exception.HResult; $inner = $_.Exception.InnerException; while ($inner) { $h = $inner.HResult; $inner = $inner.InnerException }
  if ($h -eq -2147199735) { 'E:privacy' } else { 'E:' + ('{0:X8}' -f $h) + ':' + $_.Exception.Message }
  exit 1
}
";

		static string ScriptPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "ouvir.ps1");

		/// <summary>The last reason it could not listen (for the log and the self-test).</summary>
		internal static string LastError { get; private set; }

		/// <summary>
		/// Listen to one sentence. <paramref name="ready"/> runs when the microphone is open (on the calling thread).
		/// Canceling stops the listening at once.
		/// </summary>
		public static async Task<(Outcome outcome, string text)> Listen(Action ready, System.Threading.CancellationToken cancel)
		{
			LastError = null;
			try
			{
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScriptPath));
				System.IO.File.WriteAllText(ScriptPath, Script.Replace("\r\n", "\n").Replace("\n", "\r\n"), new System.Text.UTF8Encoding(true));
				var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + ScriptPath + "\"")
				{
					UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
				};
				using (var p = System.Diagnostics.Process.Start(psi))
				using (cancel.Register(() => { try { if (!p.HasExited) p.Kill(); } catch (Exception) { } }))
				{
					string line, result = null, error = null;
					while ((line = await p.StandardOutput.ReadLineAsync()) != null)
					{
						if (line == "S:listening") ready?.Invoke();
						else if (line.StartsWith("R:")) result = line.Substring(2).Trim();
						else if (line.StartsWith("E:")) error = line.Substring(2);
						else if (line.StartsWith("I:")) LogFile.Important("voice heard: " + line.Substring(2));
					}
					if (cancel.IsCancellationRequested) return (Outcome.Canceled, null);
					LastError = error;
					LogFile.Important($"voice: {(result == null ? "no text" : result.Length == 0 ? "empty text" : result.Length + " letters")}, {error ?? "no error"}, exit {p.ExitCode}");
					if (!string.IsNullOrEmpty(result)) return (Outcome.Text, result);
					if (error == "privacy") return (Outcome.Privacy, null);
					// nothing understood (silence, a pause, noise, Windows stopping by itself): "I did not hear"; the microphone, the network or the language: a real failure
					if (result != null || (error != null && System.Text.RegularExpressions.Regex.IsMatch(error, "^status:(Timeout|PauseLimit|UserCanceled|Unknown|AudioQuality)"))) return (Outcome.Nothing, null);
					if (error != null) StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "voice: " + error);
					return (Outcome.Failed, null);
				}
			}
			catch (Exception ex)
			{
				LastError = ex.Message;
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "voice: " + ex.Message);
				return cancel.IsCancellationRequested ? (Outcome.Canceled, null) : (Outcome.Failed, null);
			}
		}

		/// <summary>Could not listen: the pet says why and offers the way out (the setting, or the Windows dictation).</summary>
		public static void Explain(Outcome outcome)
		{
			var dictation = new SpeechBubble.Choice(Lang.T("voice.useDictation"), () => { QuickChat.Open(); FormAsk.VoiceTyping(); });
			if (outcome == Outcome.Privacy)
				SpeechBubble.Say(Lang.T("voice.privacy"), 0,
					new SpeechBubble.Choice(Lang.T("voice.openSettings"), () =>
					{
						try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-speech") { UseShellExecute = true }); }
						catch (Exception skipped) { LogFile.Ignored(skipped); }
					}, true),
					dictation);
			else if (outcome == Outcome.Failed)
				SpeechBubble.Say(Lang.T("voice.failed"), 0, dictation);
		}
	}
}
