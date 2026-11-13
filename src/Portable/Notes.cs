using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// A notepad, a to-do list and a countdown, kept by the pet (%LocalAppData%\DesktopPet\notes.json).
	/// Opened from the pet's menu or the Plugins card; the countdown ends with a bubble.
	/// </summary>
	public static class Notes
	{
		public sealed class Task { public string Text { get; set; } = ""; public bool Done { get; set; } }
		sealed class Data { public string Text { get; set; } = ""; public List<Task> Tasks { get; set; } = new List<Task>(); }

		static Data data;
		static Timer countdown;
		public static DateTime CountdownEnd { get; private set; } = DateTime.MinValue;

		static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "notes.json");

		static Data D
		{
			get
			{
				if (data != null) return data;
				try { data = File.Exists(FilePath) ? JsonText.Deserialize<Data>(File.ReadAllText(FilePath)) : null; }
				catch (Exception skipped) { LogFile.Ignored(skipped); }
				return data ??= new Data();
			}
		}

		public static string Text { get => D.Text; set { D.Text = value ?? ""; Save(); } }
		public static List<Task> Tasks => D.Tasks;
		public static int OpenTasks => D.Tasks.Count(t => !t.Done);

		public static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonText.SerializeIndented(D));
			}
			catch (Exception skipped) { LogFile.Ignored(skipped); }
		}

		public static void Open()
		{
			FormPet.Hold(true);
			try { using (var f = new FormNotes()) f.ShowDialog(); }
			finally { FormPet.Hold(false); }
		}

		// ------------------------------------------------------------------ countdown

		public static bool Counting => CountdownEnd > DateTime.Now;
		public static string Remaining
		{
			get
			{
				var left = CountdownEnd - DateTime.Now;
				return left.TotalSeconds <= 0 ? "" : left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"m\:ss");
			}
		}

		public static void StartCountdown(int minutes)
		{
			CountdownEnd = DateTime.Now.AddMinutes(minutes);
			if (countdown == null)
			{
				countdown = new Timer { Interval = 1000 };
				countdown.Tick += (s, e) =>
				{
					if (DateTime.Now < CountdownEnd) return;
					countdown.Stop();
					CountdownEnd = DateTime.MinValue;
					Program.Mainthread?.ReactAll("cheer", "wave", "happy", "jump");
					SpeechBubble.Notify(Lang.T("notes.timeUp"), 0, new SpeechBubble.Choice("OK", null, true));
				};
			}
			countdown.Start();
		}

		public static void StopCountdown() { countdown?.Stop(); CountdownEnd = DateTime.MinValue; }

		sealed class FormNotes : ThemedDialog
		{
			readonly TextBox notes, newTask;
			readonly CheckedListBox tasks;
			readonly Label timeLeft;
			readonly Timer refresh = new Timer { Interval = 1000 };
			const int W = 560;

			public FormNotes()
			{
				Text = Lang.T("notes.title");
				iconEmoji = "📝";
				int y = 88;
				Label Head(string key) { var l = new Label { Text = Lang.T(key), Font = fText, AutoSize = true, Location = new Point(Pad, y) }; Controls.Add(l); y += 30; return l; }

				Head("notes.notes");
				notes = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Font = fSmall, Location = new Point(Pad, y), Size = new Size(W - Pad * 2, 110), Text = Notes.Text, AcceptsReturn = true };
				Controls.Add(notes);
				y = notes.Bottom + 18;

				Head("notes.tasks");
				tasks = new CheckedListBox { Font = fSmall, Location = new Point(Pad, y), Size = new Size(W - Pad * 2, 130), CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
				foreach (var t in Notes.Tasks) tasks.Items.Add(t.Text, t.Done);
				tasks.ItemCheck += (s, e) => { Notes.Tasks[e.Index].Done = e.NewValue == CheckState.Checked; Save(); };
				Controls.Add(tasks);
				y = tasks.Bottom + 8;
				newTask = new TextBox { Font = fSmall, Location = new Point(Pad, y), Width = W - Pad * 2 - 250, PlaceholderText = Lang.T("notes.newTask") };
				newTask.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddTask(); e.SuppressKeyPress = true; } };
				Controls.Add(newTask);
				var add = new Button { Text = Lang.T("notes.add"), Font = fSmall, AutoSize = true, Location = new Point(newTask.Right + 8, y - 3) };
				add.Click += (s, e) => AddTask();
				Controls.Add(add);
				var clear = new Button { Text = Lang.T("notes.clearDone"), Font = fSmall, AutoSize = true, Location = new Point(add.Right + 110, y - 3) };
				clear.Click += (s, e) =>
				{
					Notes.Tasks.RemoveAll(t => t.Done);
					Save();
					tasks.Items.Clear();
					foreach (var t in Notes.Tasks) tasks.Items.Add(t.Text, t.Done);
				};
				Controls.Add(clear);
				y = newTask.Bottom + 22;

				Head("notes.countdown");
				var minutes = new NumericUpDown { Font = fSmall, Location = new Point(Pad, y), Width = 80, Minimum = 1, Maximum = 600, Value = 10 };
				Controls.Add(minutes);
				var min = new Label { Text = "min", Font = fSmall, AutoSize = true, Location = new Point(minutes.Right + 6, y + 4) };
				Controls.Add(min);
				var start = new Button { Text = Lang.T("notes.start"), Font = fSmall, AutoSize = true, Location = new Point(min.Right + 14, y - 3) };
				start.Click += (s, e) => { StartCountdown((int)minutes.Value); UpdateTime(); };
				Controls.Add(start);
				var stop = new Button { Text = Lang.T("notes.stop"), Font = fSmall, AutoSize = true, Location = new Point(start.Right + 110, y - 3) };
				stop.Click += (s, e) => { StopCountdown(); UpdateTime(); };
				Controls.Add(stop);
				timeLeft = new Label { Font = fText, AutoSize = true, Location = new Point(stop.Right + 110, y + 1) };
				Controls.Add(timeLeft);
				y = minutes.Bottom + 26;

				var close = AddButton(Lang.T("launch.close"), DialogResult.OK, true);
				ApplyTheme(close);
				// buttons laid out after the theme set their size
				add.Left = newTask.Right + 8; clear.Left = add.Right + 8;
				start.Left = min.Right + 14; stop.Left = start.Right + 8; timeLeft.Left = stop.Right + 14;
				ClientSize = new Size(W, LayoutButtons(y, W) + Pad);
				refresh.Tick += (s, e) => UpdateTime();
				refresh.Start();
				UpdateTime();
			}

			void AddTask()
			{
				string text = newTask.Text.Trim();
				if (text.Length == 0) return;
				Notes.Tasks.Add(new Task { Text = text });
				Save();
				tasks.Items.Add(text, false);
				newTask.Clear();
				newTask.Focus();
			}

			void UpdateTime() => timeLeft.Text = Counting ? "⏳ " + Remaining : "";

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				Theme.Prepare(e.Graphics);
				PaintIcon(e.Graphics);
				Theme.DrawText(e.Graphics, Lang.T("notes.help"), fSmall, new Rectangle(Pad + 62, Pad + 4, ClientSize.Width - Pad * 2 - 62, 50), Theme.TextMuted);
			}

			protected override void OnFormClosing(FormClosingEventArgs e)
			{
				Notes.Text = notes.Text;
				refresh.Stop();
				base.OnFormClosing(e);
			}
		}
	}
}
