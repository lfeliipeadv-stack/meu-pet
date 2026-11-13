using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// "Create my pet": pictures (PNG, GIF, JPG…) for walking, standing, sleeping, falling and being dragged become a
	/// complete pet — walk, turn, idle, sleep, fall, land, drag and toss, like the others. Only "walking" is needed;
	/// the rest falls back on it. Saved on this computer ("My pets"), included in backups, never sent anywhere.
	/// </summary>
	public static class PetCreator
	{
		public const string Prefix = "meu-";

		public sealed class Input
		{
			public string Name = "";
			public List<Image> Walk = new List<Image>(), Idle = new List<Image>(), Sleep = new List<Image>(), Fall = new List<Image>(), Drag = new List<Image>();
			/// <summary>The pictures look to the right (the program walks to the left by default and mirrors when needed).</summary>
			public bool FacingRight;
		}

		/// <summary>A folder name from the pet name: "meu-" + letters and digits.</summary>
		public static string FolderFor(string name)
		{
			string n = new string((name ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD)
				.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
			n = string.Concat(n.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
			while (n.Contains("--")) n = n.Replace("--", "-");
			return Prefix + (n.Length == 0 ? "pet" : (n.Length > 30 ? n.Substring(0, 30) : n));
		}

		/// <summary>Build the pet (animations.xml text). Throws with a clear message when something is missing.</summary>
		public static string Build(Input input)
		{
			if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException(Lang.T("create.needName"));
			if (input.Walk.Count == 0) throw new ArgumentException(Lang.T("create.needWalk"));
			var idle = input.Idle.Count > 0 ? input.Idle : new List<Image> { input.Walk[0] };
			var fall = input.Fall.Count > 0 ? input.Fall : idle;
			var drag = input.Drag.Count > 0 ? input.Drag : fall;
			var all = input.Walk.Concat(idle).Concat(input.Sleep).Concat(fall).Concat(drag).ToList();

			// one cell size for every frame: the biggest picture, at most 160 px tall (bigger ones are reduced)
			int maxW = all.Max(i => i.Width), maxH = all.Max(i => i.Height);
			double scale = Math.Min(1.0, 160.0 / maxH);
			int cw = Math.Max(8, (int)Math.Round(maxW * scale)), ch = Math.Max(8, (int)Math.Round(maxH * scale));
			int tilesX = Math.Min(8, all.Count), tilesY = (all.Count + tilesX - 1) / tilesX;

			byte[] sheetPng;
			using (var sheet = new Bitmap(tilesX * cw, tilesY * ch, PixelFormat.Format32bppArgb))
			{
				using (var g = Graphics.FromImage(sheet))
				{
					g.Clear(Color.Transparent);
					g.InterpolationMode = scale < 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
					g.PixelOffsetMode = PixelOffsetMode.Half;
					for (int i = 0; i < all.Count; i++)
					{
						var img = all[i];
						int w = (int)Math.Round(img.Width * scale), h = (int)Math.Round(img.Height * scale);
						// centred, standing on the bottom of the cell
						var dest = new Rectangle((i % tilesX) * cw + (cw - w) / 2, (i / tilesX) * ch + (ch - h), w, h);
						if (input.FacingRight) { dest.X += dest.Width; dest.Width = -dest.Width; }   // mirrored: the program's pets look left
						g.DrawImage(img, dest);
					}
				}
				using (var ms = new MemoryStream()) { sheet.Save(ms, ImageFormat.Png); sheetPng = ms.ToArray(); }
			}

			// frame numbers in the sheet
			int at = 0;
			int[] Take(int n) { var r = Enumerable.Range(at, n).ToArray(); at += n; return r; }
			int[] fWalk = Take(input.Walk.Count), fIdle = Take(idle.Count), fSleep = Take(input.Sleep.Count), fFall = Take(fall.Count), fDrag = Take(drag.Count);
			bool sleeps = fSleep.Length > 0;

			var x = new StringBuilder();
			string F(int[] frames) => string.Concat(frames.Select(f => $"<frame>{f}</frame>"));
			void Anim(int id, string name, string move, string interval, string repeat, int from, int[] frames, string next, string border = null, string gravity = null, string action = null, string opacity = "1|1")
			{
				var m = move.Split('|'); var iv = interval.Split('|'); var op = opacity.Split('|');
				x.Append($"<animation id=\"{id}\"><name>{name}</name>");
				x.Append($"<start><x>{m[0]}</x><y>{m[1]}</y><offsety>0</offsety><opacity>{op[0]}</opacity><interval>{iv[0]}</interval></start>");
				x.Append($"<end><x>{(m.Length > 2 ? m[2] : m[0])}</x><y>{(m.Length > 2 ? m[3] : m[1])}</y><offsety>0</offsety><opacity>{op[1]}</opacity><interval>{(iv.Length > 1 ? iv[1] : iv[0])}</interval></end>");
				x.Append($"<sequence repeatfrom=\"{from}\" repeat=\"{repeat}\">{F(frames)}{(action != null ? $"<action>{action}</action>" : "")}{next}</sequence>");
				if (border != null) x.Append($"<border>{border}</border>");
				if (gravity != null) x.Append($"<gravity>{gravity}</gravity>");
				x.Append("</animation>\n");
			}
			string N(int prob, int id, string only = "none") => $"<next probability=\"{prob}\" only=\"{only}\">{id}</next>";

			Anim(1, "walk", "-2|0", "120", "random/4+5", 0, fWalk, N(55, 1) + N(15, 2) + N(20, 11) + (sleeps ? N(10, 13, "taskbar") : ""), N(100, 2), N(100, 5));
			Anim(2, "turn", "0|0", "150", "0", 0, new[] { fIdle[0] }, N(100, 1), action: "flip");
			Anim(3, "drag", "0|0", "110", "40", 0, fDrag, N(100, 3));
			Anim(4, "toss", "0|0", "80", "30", 0, fFall, N(100, 4));
			Anim(5, "fall", "0|2|0|9", "90|40", "20", 0, fFall, N(100, 6), N(100, 7));
			Anim(6, "fall fast", "0|9", "40", "10", 0, fFall, N(100, 6), N(100, 7));
			Anim(7, "fall soft", "0|0", "200", "0", 0, new[] { fIdle[0] }, N(100, 1));
			Anim(10, "sync", "0|0", "160", "0", 0, fIdle, N(100, 1));
			Anim(11, "idle", "0|0", "300", "random/3+2", 0, fIdle, N(70, 1) + N(30, 2), gravity: N(100, 5));
			if (sleeps)
			{
				Anim(13, "sleep", "0|0", "400|520", "random/4+5", 0, fSleep, N(100, 14));
				Anim(14, "wake up", "0|0", "220", "0", 0, fSleep.Reverse().Concat(new[] { fIdle[0] }).ToArray(), N(100, 1));
			}
			Anim(99, "kill", "0|0", "150|220", "0", 0, new[] { fIdle[0], fIdle[0], fIdle[0] }, "", opacity: "1|0");

			string Esc(string s) => System.Security.SecurityElement.Escape(s);
			string icon = Convert.ToBase64String(Icon(all[input.Idle.Count > 0 ? input.Walk.Count : 0], input.FacingRight));
			return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
				+ "<animations xmlns=\"https://esheep.petrucci.ch/\">\n"
				+ $"<header><author>{Esc(Environment.UserName)}</author><title>{Esc(input.Name)}</title><petname>{Esc(input.Name)}</petname><version>1.0</version>"
				+ $"<info>{Esc(Lang.T("create.info"))}</info><application>1</application><icon><![CDATA[{icon}]]></icon></header>\n"
				+ $"<image><tilesx>{tilesX}</tilesx><tilesy>{tilesY}</tilesy><png><![CDATA[{Convert.ToBase64String(sheetPng)}]]></png><transparency>Magenta</transparency></image>\n"
				+ "<spawns><spawn id=\"1\" probability=\"60\"><x>screenW+10</x><y>areaH-imageH</y><next>1</next></spawn>"
				+ "<spawn id=\"2\" probability=\"40\"><x>random*(screenW-imageW-50)/100+25</x><y>-imageH-20</y><next>5</next></spawn></spawns>\n"
				+ "<animations>\n" + x + "</animations>\n<childs />\n<sounds />\n</animations>\n";
		}

		/// <summary>A classic .ico (32 and 64 px, 32-bit) from one picture: readable by every Windows API.</summary>
		static byte[] Icon(Image src, bool mirror)
		{
			int[] sizes = { 32, 64 };
			var entries = new List<byte[]>();
			foreach (int s in sizes)
				using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
				{
					using (var g = Graphics.FromImage(bmp))
					{
						g.Clear(Color.Transparent);
						g.InterpolationMode = InterpolationMode.HighQualityBicubic;
						double k = Math.Min(s / (double)src.Width, s / (double)src.Height);
						int w = (int)(src.Width * k), h = (int)(src.Height * k);
						var r = new Rectangle((s - w) / 2, (s - h) / 2, w, h);
						if (mirror) { r.X += r.Width; r.Width = -r.Width; }
						g.DrawImage(src, r);
					}
					int mask = (s + 31) / 32 * 4;
					var data = new byte[40 + s * s * 4 + mask * s];
					BitConverter.GetBytes(40).CopyTo(data, 0); BitConverter.GetBytes(s).CopyTo(data, 4); BitConverter.GetBytes(s * 2).CopyTo(data, 8);
					BitConverter.GetBytes((short)1).CopyTo(data, 12); BitConverter.GetBytes((short)32).CopyTo(data, 14); BitConverter.GetBytes(s * s * 4).CopyTo(data, 20);
					int o = 40;
					for (int y = s - 1; y >= 0; y--) for (int xx = 0; xx < s; xx++) { var c = bmp.GetPixel(xx, y); data[o++] = c.B; data[o++] = c.G; data[o++] = c.R; data[o++] = c.A; }
					for (int y = s - 1; y >= 0; y--) { for (int xx = 0; xx < s; xx++) if (bmp.GetPixel(xx, y).A < 128) data[o + xx / 8] |= (byte)(0x80 >> (xx % 8)); o += mask; }
					entries.Add(data);
				}
			using (var ms = new MemoryStream())
			using (var w = new BinaryWriter(ms))
			{
				w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
				int offset = 6 + 16 * sizes.Length;
				for (int i = 0; i < sizes.Length; i++)
				{
					w.Write((byte)sizes[i]); w.Write((byte)sizes[i]); w.Write((byte)0); w.Write((byte)0);
					w.Write((short)1); w.Write((short)32); w.Write(entries[i].Length); w.Write(offset);
					offset += entries[i].Length;
				}
				foreach (var e in entries) w.Write(e);
				w.Flush();
				return ms.ToArray();
			}
		}
	}

	/// <summary>The "Create my pet" window: the name, one card per movement (pick its pictures), create.</summary>
	internal sealed class FormPetCreator : ThemedDialog
	{
		readonly TextBox name = new TextBox { BorderStyle = BorderStyle.None, MaxLength = 40 };
		readonly CheckBox facingRight = new CheckBox { AutoSize = true };
		readonly PetCreator.Input input = new PetCreator.Input();
		readonly List<(string emoji, string key, List<Image> list, bool required, Button pick)> moves = new List<(string, string, List<Image>, bool, Button)>();
		Rectangle nameField, introRect;
		string error = "";
		int movesTop;
		const int W = 560, RowH = 58;

		/// <summary>The folder of the pet created (null until it is).</summary>
		public string CreatedFolder { get; private set; }

		public FormPetCreator()
		{
			Text = Lang.T("create.title");
			iconEmoji = "🎨";
			var create = AddButton(Lang.T("create.create"), DialogResult.None, true);
			AddButton(Lang.T("rem.cancel"), DialogResult.Cancel, false);
			name.Font = fText;
			Controls.Add(name);
			void Move(string emoji, string key, List<Image> list, bool required)
			{
				var pick = new Button { Text = Lang.T("create.pick"), AutoSize = true, MinimumSize = new Size(110, 34), Font = fSmall };
				pick.Click += (s, e) =>
				{
					using (var d = new OpenFileDialog { Multiselect = true, Filter = Lang.T("create.images") + "|*.png;*.gif;*.jpg;*.jpeg;*.bmp" })
					{
						if (d.ShowDialog(this) != DialogResult.OK) return;
						foreach (var img in list) img.Dispose();
						list.Clear();
						// in name order: frame_01, frame_02…
						foreach (var f in d.FileNames.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
							try { using (var tmp = Image.FromFile(f)) list.Add(new Bitmap(tmp)); } catch (Exception skipped) { LogFile.Ignored(skipped); }
						error = "";
						Invalidate();
					}
				};
				Controls.Add(pick);
				moves.Add((emoji, key, list, required, pick));
			}
			Move("🚶", "create.walk", input.Walk, true);
			Move("🧍", "create.idle", input.Idle, false);
			Move("💤", "create.sleep", input.Sleep, false);
			Move("🪂", "create.fall", input.Fall, false);
			Move("✋", "create.drag", input.Drag, false);
			facingRight.Text = Lang.T("create.facingRight");
			facingRight.Font = fSmall;
			Controls.Add(facingRight);
			create.Click += (s, e) =>
			{
				try
				{
					input.Name = name.Text.Trim();
					input.FacingRight = facingRight.Checked;
					string xml = PetCreator.Build(input);
					var check = new System.Xml.XmlDocument();
					check.LoadXml(xml);
					string folder = PetCreator.FolderFor(input.Name);
					PetStore.Save(folder, xml, "local");
					CreatedFolder = folder;
					DialogResult = DialogResult.OK;
				}
				catch (Exception ex) { error = ex.Message; LayoutAll(); }
			};
			foreach (var b in buttons) b.Font = fText;
			ClientSize = new Size(W, 100);
			ApplyTheme(create);
			name.BorderStyle = BorderStyle.None;
			name.BackColor = Theme.Surface; name.ForeColor = Theme.Text;
			LayoutAll();
			ActiveControl = name;
		}

		void LayoutAll()
		{
			int tx = Pad + 64, tw = W - tx - Pad, ih;
			using (var g = CreateGraphics()) ih = Theme.MeasureText(g, Lang.T("create.intro"), fSmall, tw, TextFormatFlags.WordBreak).Height;
			introRect = new Rectangle(tx, Pad + 34, tw, ih);
			int y = Math.Max(Pad + 48, introRect.Bottom) + 22;
			y += 24;   // the "Pet name" label
			nameField = new Rectangle(Pad, y, W - Pad * 2, 44);
			name.Width = nameField.Width - 28;
			name.Location = new Point(nameField.X + 14, nameField.Y + (44 - name.Height) / 2);
			y = nameField.Bottom + 18;
			movesTop = y;
			for (int i = 0; i < moves.Count; i++)
			{
				var b = moves[i].pick;
				b.Location = new Point(W - Pad - 14 - b.Width, y + i * (RowH + 8) + (RowH - b.Height) / 2);
			}
			y += moves.Count * (RowH + 8) + 4;
			facingRight.Location = new Point(Pad + 2, y);
			y = facingRight.Bottom + (error.Length > 0 ? 50 : 18);
			int bottom = LayoutButtons(y + 10, W);
			ClientSize = new Size(W, bottom + Pad);
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			var g = e.Graphics;
			Theme.Prepare(g);
			PaintIcon(g);
			Theme.DrawText(g, Lang.T("create.title"), fTitle, new Rectangle(Pad + 64, Pad - 2, W - Pad * 2 - 64, 30), Theme.Text, TextFormatFlags.SingleLine);
			Theme.DrawText(g, Lang.T("create.intro"), fSmall, introRect, Theme.TextMuted, TextFormatFlags.WordBreak);
			Theme.DrawText(g, Lang.T("create.name"), fText, new Rectangle(Pad, nameField.Y - 24, 300, 22), Theme.Text, TextFormatFlags.SingleLine);
			Theme.FillRounded(g, Theme.Surface, nameField, 10);
			Theme.DrawRounded(g, name.Focused ? Theme.Accent : Theme.Border, nameField, 10);
			for (int i = 0; i < moves.Count; i++)
			{
				var m = moves[i];
				var r = new Rectangle(Pad, movesTop + i * (RowH + 8), W - Pad * 2, RowH);
				bool has = m.list.Count > 0;
				Theme.FillRounded(g, Theme.Surface, r, 10);
				Theme.DrawRounded(g, has ? Theme.Accent : Theme.Border, r, 10);
				var ic = new Rectangle(r.X + 12, r.Y + 11, 36, 36);
				Theme.FillRounded(g, Theme.AccentSoft, ic, 9);
				Theme.DrawText(g, m.emoji, fText, ic, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				Theme.DrawText(g, Lang.T(m.key) + (m.required ? " *" : ""), fText, new Rectangle(r.X + 60, r.Y + 8, 220, 22), Theme.Text, TextFormatFlags.SingleLine);
				string count = has ? Lang.T("create.count", m.list.Count) : Lang.T(m.required ? "create.needed" : "create.optional");
				Theme.DrawText(g, (has ? "✓ " : "") + count, fSmall, new Rectangle(r.X + 60, r.Y + 30, 260, 20), has ? Theme.Accent : Theme.TextMuted, TextFormatFlags.SingleLine);
			}
			if (error.Length > 0)
			{
				var er = new Rectangle(Pad, facingRight.Bottom + 10, W - Pad * 2, 34);
				Theme.FillRounded(g, Color.FromArgb(36, Theme.Danger), er, 8);
				Theme.DrawText(g, "⚠️  " + error, fSmall, new Rectangle(er.X + 12, er.Y, er.Width - 20, er.Height), Theme.Danger, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
			}
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Pad, buttons[0].Top - 12, W - Pad, buttons[0].Top - 12);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			foreach (var list in new[] { input.Walk, input.Idle, input.Sleep, input.Fall, input.Drag })
				foreach (var img in list) img.Dispose();
			base.OnFormClosed(e);
		}
	}
}
