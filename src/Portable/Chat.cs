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

		/// <summary>Open the chat above the pet (or bring it back).</summary>
		public static void Open()
		{
			if (IsOpen) { open.Activate(); open.input.Focus(); return; }
			open = new FormChat();
			open.Show();
			open.Activate();
			open.input.Focus();
		}

		readonly TextBox input = new TextBox { BorderStyle = BorderStyle.None, MaxLength = 1000 };
		readonly MessageList list;
		readonly Font fontTitle = Theme.CreateFont(15, FontStyle.Bold), fontSmall = Theme.CreateFont(12), fontText = Theme.CreateFont(14), fontEmoji = Theme.CreateFont(18);
		Rectangle closeRect, clearRect, sendRect, micRect, inputRect;
		string hover;
		bool busy;
		DateTime keepOpenUntil = DateTime.MinValue;
		const int W = 380, H = 500, Header = 54, Footer = 66;

		FormChat()
		{
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			KeyPreview = true;
			DoubleBuffered = true;
			BackColor = Theme.Surface;
			ClientSize = new Size(W, H);
			Text = Lang.T("chat.title");

			list = new MessageList(this) { Location = new Point(1, Header), Size = new Size(W - 2, H - Header - Footer) };
			Controls.Add(list);
			inputRect = new Rectangle(14, H - Footer + 12, W - 28, 42);
			input.Font = fontText;
			input.BackColor = Theme.SurfaceAlt;
			input.ForeColor = Theme.Text;
			input.PlaceholderText = Lang.T("chat.placeholder", PetName);
			input.Location = new Point(inputRect.X + 14, inputRect.Y + (inputRect.Height - input.PreferredHeight) / 2);
			input.Width = inputRect.Width - 14 - 84;
			input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(input.Text); } };
			Controls.Add(input);
			micRect = new Rectangle(inputRect.Right - 78, inputRect.Y + 5, 32, 32);
			sendRect = new Rectangle(inputRect.Right - 40, inputRect.Y + 5, 32, 32);
			closeRect = new Rectangle(W - 40, 12, 28, 28);
			clearRect = new Rectangle(W - 74, 12, 28, 28);
			Place();
		}

		static string PetName => Program.Mainthread?.CurrentHeader?.Petname ?? "Pet";

		protected override bool ShowWithoutActivation => false;

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.ExStyle |= 0x00000080;     // WS_EX_TOOLWINDOW: not in Alt+Tab
				cp.ClassStyle |= 0x00020000;  // CS_DROPSHADOW
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
			int y = pet != null ? pet.Top - Height - 10 : wa.Bottom - Height - 24;
			if (pet != null && y < wa.Top) y = pet.Bottom + 10;
			Location = new Point(Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)), Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y)));
		}

		// ------------------------------------------------------------------ drawing the frame

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			Theme.Prepare(g);
			g.Clear(Theme.Surface);
			// header: ✨ Conversation · pet name · state
			Theme.DrawText(g, "✨", fontEmoji, new Rectangle(14, 12, 30, 30), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
			Theme.DrawText(g, PetName, fontTitle, new Rectangle(48, 8, W - 130, 22), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			string state = busy ? Lang.T("chat.thinking") : AiAssistant.Configured ? Lang.T("chat.ready") : Lang.T("chat.noAi");
			var dot = busy ? Color.Orange : AiAssistant.Configured ? Color.FromArgb(46, 160, 90) : Theme.TextMuted;
			using (var b = new SolidBrush(dot)) g.FillEllipse(b, 49, 35, 8, 8);
			Theme.DrawText(g, state, fontSmall, new Rectangle(62, 29, W - 142, 20), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			IconButton(g, clearRect, "🧹", "clear");
			IconButton(g, closeRect, "✕", "close");
			using (var p = new Pen(Theme.Border)) { g.DrawLine(p, 0, Header - 1, W, Header - 1); g.DrawLine(p, 0, H - Footer, W, H - Footer); g.DrawRectangle(p, 0, 0, W - 1, H - 1); }
			// input field
			Theme.FillRounded(g, Theme.SurfaceAlt, inputRect, 12);
			Theme.DrawRounded(g, input.Focused ? Theme.Accent : Theme.Border, inputRect, 12);
			IconButton(g, micRect, "🎤", "mic");
			bool canSend = input.Text.Trim().Length > 0 && !busy;
			Theme.FillRounded(g, canSend ? Theme.Accent : Theme.Border, sendRect, 9);
			Theme.DrawText(g, "➤", fontText, sendRect, canSend ? Theme.AccentText : Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		void IconButton(Graphics g, Rectangle r, string text, string id)
		{
			if (hover == id) Theme.FillRounded(g, Theme.Hover, r, 8);
			Theme.DrawText(g, text, text == "✕" ? fontText : fontSmall, r, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			string h = closeRect.Contains(e.Location) ? "close" : clearRect.Contains(e.Location) ? "clear" : micRect.Contains(e.Location) ? "mic" : sendRect.Contains(e.Location) ? "send" : null;
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
			if (clearRect.Contains(e.Location)) { NewConversation(); return; }
			if (micRect.Contains(e.Location)) { input.Focus(); keepOpenUntil = DateTime.Now.AddSeconds(30); FormAsk.VoiceTyping(); return; }
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
			if (open == this) open = null;
			base.OnFormClosed(e);
		}

		void NewConversation()
		{
			messages.Clear();
			AiAssistant.NewConversation();
			list.Invalidate();
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
}
