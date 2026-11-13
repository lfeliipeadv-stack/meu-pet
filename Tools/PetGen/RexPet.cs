using System.Drawing;

namespace PetGen;

/// <summary>
/// "Rex" — a little green pixel dinosaur for DesktopPet. The standing pose is a pixel-exact copy
/// of the reference art the user provided (25 x 26 pixels, read cell by cell from the image).
/// Every other pose is built from it by editing body parts in art coordinates (eye, jaw, arms,
/// feet, tail) and then transforming the whole figure (bob, crouch, lean, lie down, roll over);
/// props and costumes from the reference sheets are drawn on top. Several scenes release an
/// object as a separate "child" window that moves on its own.
/// Frames are 32 px wide with 6 rows of headroom; the feet stand on row 31.
/// </summary>
static class RexPet
{
	const int W = 32, Head = 6, AX = 4, AY = 6;

	// The reference dinosaur, exactly as in the user's image ('.' = transparent). Faces left.
	static readonly string[] Art =
	{
		".......GGGGG.............",   //  0
		".....GGGGGGMMOO..........",   //  1
		"..GGGGGGGGGGMMR..........",   //  2
		".GGGGGGGEMGGGMD..........",   //  3 eye
		"GGGMGGGGEMGGGMDOO........",   //  4 eye, nostril
		"GGGGGGGGGGGGGMDRR........",   //  5
		"GGGGGGGGGGGGGMDR.........",   //  6
		"GGGGGGGGDGGGGMD..........",   //  7 mouth corner
		".DDDDDDDMGGGMMDO.........",   //  8 jaw
		"..MMMMGGGGGGMMRR.........",   //  9 chin
		".......MMMGGMMR..........",   // 10
		".......MMGGGGM...........",   // 11
		"......MGGGGGGMMOO........",   // 12
		"...DDDGGGMGGGGMMR........",   // 13 near arm
		"..DDDMGMMMGGGGGMM.OO...MD",   // 14
		"..DD.MMMDDGGGGGGMMRR.GGMD",   // 15
		".....DDDGGGGGGGGGGGGGGMMD",   // 16
		".....MGGGGMGGGGGMGGGGMMD.",   // 17
		".....MGGGGGGGGGGMMMMMMD..",   // 18
		".....MMGGDGGGGGGDMMMMD...",   // 19
		"......DMMDGGGGGMDDDDD....",   // 20
		"......DDMDMGGGMDDDD......",   // 21 legs
		"......DDDDDDGGMD.........",   // 22
		".....DMMDD.MGGMD.........",   // 23
		"....DDDDD.DGGMMD.........",   // 24 front foot
		"..........DDDDDD.........",   // 25 back foot
	};
	const int AR = 26, AC = 25;

	// palette characters
	const char G = 'G', M = 'M', D = 'D', O = 'O', R = 'R', E = 'E';
	const char Wt = 'W', Mouth = 'X', Tongue = 'T', Pink = 'F', Water = 'w', Glass = 'c', Teal = 'C';
	const char Brown = 'B', BrownD = 'b', Yel = 'Y', YelD = 'y', Blk = 'K', GrayD = 'k', Gray = 'g', GrayL = 'l';
	const char Purp = 'P', PurpL = 'p', Navy = 'N', Red = 'H', RedL = 'h', Olive = 'A', Zz = 'Z', Blue = 'U', Mint = 'u';

	sealed class Look
	{
		public int Eye;        // 0 open, 1 closed, 2 happy ^, 3 wide, 4 angry, 5 look up, 6 squeezed >, 7 half (tired)
		public int Jaw;        // 0 closed, 1 open, 2 roar (wide, teeth), 3 tongue out, 4 "o"
		public int Arm;        // near arm: 0 as drawn, 1 raised, 2 reaching forward, 3 tucked (none)
		public bool FarUp;     // far arm raised too
		public int Feet;       // 0 both down, 1 front foot up, 2 back foot up, 3 both tucked (jump)
		public int Tail;       // 0, 1 up, -1 down
		public bool Blush;
	}

	public static PetDef Build()
	{
		Frame.NewHeadroom = Head;
		Frame.NewWidth = W;
		var p = new PetDef
		{
			Folder = "rex",
			Title = "Rex the Dino",
			PetName = "Rex",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Rex is a little green pixel dinosaur with orange spikes.[br]" +
				   "He stomps, roars, plays ball, dresses up as a wizard, a chef, an astronaut...[br]" +
				   "and some of his toys fly off on their own.",
			Palette = new Dictionary<char, Color>
			{
				[G] = Color.FromArgb(155, 188, 96), [M] = Color.FromArgb(66, 130, 68), [D] = Color.FromArgb(32, 84, 48),
				[O] = Color.FromArgb(240, 144, 44), [R] = Color.FromArgb(204, 108, 48), [E] = Color.FromArgb(0, 0, 0),
				[Wt] = Color.FromArgb(255, 255, 255), [Mouth] = Color.FromArgb(110, 30, 40), [Tongue] = Color.FromArgb(240, 110, 130),
				[Pink] = Color.FromArgb(250, 170, 200), [Water] = Color.FromArgb(110, 190, 245), [Glass] = Color.FromArgb(200, 232, 250),
				[Teal] = Color.FromArgb(80, 160, 180), [Brown] = Color.FromArgb(150, 96, 52), [BrownD] = Color.FromArgb(100, 62, 34),
				[Yel] = Color.FromArgb(250, 210, 60), [YelD] = Color.FromArgb(210, 160, 30), [Blk] = Color.FromArgb(30, 30, 34),
				[GrayD] = Color.FromArgb(90, 90, 98), [Gray] = Color.FromArgb(150, 150, 158), [GrayL] = Color.FromArgb(214, 214, 220),
				[Purp] = Color.FromArgb(130, 80, 190), [PurpL] = Color.FromArgb(190, 150, 240), [Navy] = Color.FromArgb(40, 60, 120),
				[Red] = Color.FromArgb(220, 50, 60), [RedL] = Color.FromArgb(255, 130, 130), [Olive] = Color.FromArgb(118, 128, 60),
				[Zz] = Color.FromArgb(40, 96, 56), [Blue] = Color.FromArgb(60, 120, 220), [Mint] = Color.FromArgb(120, 210, 150),
			},
			Scale = 2,
			TilesX = 8,
		};

		// ============================================================ body (art coordinates)
		char[,] Body(Look s)
		{
			var a = new char[AR, AC];
			for (int r = 0; r < AR; r++) for (int c = 0; c < AC; c++) a[r, c] = Art[r][c];
			void Set(int r, int c, char ch) { if (r >= 0 && r < AR && c >= 0 && c < AC) a[r, c] = ch; }

			// --- tail (cols 17..24) moves as a block
			if (s.Tail != 0)
			{
				var t = (char[,])a.Clone();
				for (int r = 12; r <= 20; r++) for (int c = 18; c < AC; c++) a[r, c] = '.';
				for (int r = 12; r <= 20; r++) for (int c = 18; c < AC; c++)
				{
					char ch = t[r, c]; if (ch == '.') continue;
					int nr = r - s.Tail * (c >= 21 ? 2 : 1);
					if (nr >= 0 && nr < AR) a[nr, c] = ch;
				}
				for (int r = 12; r <= 20; r++) if (a[r, 18] == '.' && a[r, 17] != '.' && a[r, 17] != 'O' && a[r, 17] != 'R') a[r, 17] = D;
			}
			// --- feet
			void LiftCols(int c0, int c1, int rows)
			{
				var t = (char[,])a.Clone();
				for (int r = 21; r < AR; r++) for (int c = c0; c <= c1; c++) a[r, c] = '.';
				for (int r = 21; r < AR; r++) for (int c = c0; c <= c1; c++) { char ch = t[r, c]; if (ch != '.') Set(r - rows, c, ch); }
			}
			if (s.Feet == 1 || s.Feet == 3) LiftCols(4, 9, s.Feet == 3 ? 2 : 1);
			if (s.Feet == 2 || s.Feet == 3) LiftCols(10, 15, s.Feet == 3 ? 2 : 1);

			// --- near arm
			if (s.Arm != 0)
			{
				foreach (var (r, c) in new[] { (13, 3), (13, 4), (13, 5), (14, 2), (14, 3), (14, 4), (15, 2), (15, 3) }) a[r, c] = '.';
				if (s.Arm == 1) { Set(13, 5, D); Set(13, 4, D); Set(12, 3, D); Set(12, 4, M); Set(11, 2, D); Set(11, 3, M); Set(10, 1, D); Set(10, 2, D); Set(9, 1, D); Set(9, 0, D); Set(11, 1, D); }
				if (s.Arm == 2) { Set(13, 5, D); Set(13, 4, D); Set(13, 3, D); Set(13, 2, D); Set(12, 1, D); Set(14, 1, D); }
			}
			// --- far arm raised
			if (s.FarUp)
			{
				foreach (var (r, c) in new[] { (14, 7), (14, 8), (14, 9), (15, 5), (15, 6), (15, 7), (15, 8), (15, 9) }) a[r, c] = G;
				a[15, 5] = M; a[14, 7] = G;
				Set(12, 9, M); Set(11, 10, D); Set(10, 10, D); Set(9, 10, M); Set(13, 9, M);
			}
			// --- jaw
			if (s.Jaw == 1 || s.Jaw == 2 || s.Jaw == 3 || s.Jaw == 4)
			{
				int n = s.Jaw == 2 ? 2 : 1;
				var jaw = new List<(int r, int c, char ch)>();
				for (int c = 1; c <= 6; c++) jaw.Add((8, c, a[8, c]));
				for (int c = 2; c <= 5; c++) jaw.Add((9, c, a[9, c]));
				foreach (var j in jaw) a[j.r, j.c] = Mouth;
				foreach (var j in jaw) Set(j.r + n, j.c, j.ch);
				if (s.Jaw == 4) { for (int c = 1; c <= 3; c++) a[8, c] = G; a[9, 2] = '.'; a[9, 3] = '.'; Set(10, 2, '.'); }
				if (s.Jaw == 2) { a[8, 2] = Wt; a[8, 4] = Wt; a[8, 6] = Wt; a[9, 3] = Wt; a[9, 5] = Wt; a[9, 1] = '.'; a[9, 6] = Mouth; }
				if (s.Jaw == 3) { Set(9, 1, Tongue); Set(10, 1, Tongue); Set(10, 0, Tongue); Set(11, 1, Tongue); }
			}
			// --- eye (pupil at 3..4,8; socket shading at 3..4,9)
			void EyeClear() { a[3, 8] = G; a[4, 8] = G; a[3, 9] = G; a[4, 9] = G; }
			switch (s.Eye)
			{
				case 1: EyeClear(); a[4, 7] = E; a[4, 8] = E; a[4, 9] = E; break;
				case 2: EyeClear(); a[3, 8] = E; a[4, 7] = E; a[4, 9] = E; break;
				case 3: EyeClear(); for (int r = 2; r <= 4; r++) for (int c = 7; c <= 9; c++) a[r, c] = Wt; a[3, 8] = E; a[1, 7] = D; break;
				case 4: a[2, 9] = E; a[2, 8] = E; a[1, 10] = E; break;
				case 5: EyeClear(); a[2, 8] = E; a[3, 8] = E; a[2, 9] = M; a[3, 9] = M; break;
				case 6: EyeClear(); a[3, 7] = E; a[4, 8] = E; a[5, 7] = E; break;
				case 7: a[3, 8] = M; a[3, 9] = M; a[2, 8] = D; a[2, 9] = D; break;
			}
			if (s.Blush) { Set(6, 9, Pink); Set(6, 10, Pink); }
			return a;
		}
		Frame Place(char[,] a, int dx = 0, int dy = 0)
		{
			var f = new Frame();
			for (int r = 0; r < AR; r++) for (int c = 0; c < AC; c++) if (a[r, c] != '.') f[c + AX + dx, r + AY + dy] = a[r, c];
			return f;
		}
		Frame Pose(int eye = 0, int jaw = 0, int arm = 0, bool farUp = false, int feet = 0, int tail = 0, bool blush = false, int dy = 0)
			=> Place(Body(new Look { Eye = eye, Jaw = jaw, Arm = arm, FarUp = farUp, Feet = feet, Tail = tail, Blush = blush }), 0, dy);

		// ============================================================ whole-figure transforms (frame coords)
		Frame Squash(Frame f, int n)            // crouch / sit: everything above the feet drops n rows
		{
			var s = f.Clone(); int feet = 28;
			for (int y = f.MinY; y < feet; y++) for (int x = 0; x < W; x++) f[x, y] = '.';
			for (int y = f.MinY; y < feet; y++) for (int x = 0; x < W; x++) if (s[x, y] != '.') f[x, Math.Min(feet - 1, y + n)] = s[x, y] == '.' ? f[x, Math.Min(feet - 1, y + n)] : s[x, y];
			for (int y = feet - n; y < feet; y++) for (int x = 0; x < W; x++) if (f[x, y] == '.' && s[x, y] != '.') f[x, y] = s[x, y];
			return f;
		}
		Frame Lean(Frame f, int dx, int belowY)  // shear: rows above belowY shift sideways (more at the top)
		{
			var s = f.Clone();
			for (int y = f.MinY; y < belowY; y++) for (int x = 0; x < W; x++) f[x, y] = '.';
			for (int y = f.MinY; y < belowY; y++)
			{
				int d = (int)Math.Round(dx * (double)(belowY - y) / (belowY - 6));
				for (int x = 0; x < W; x++) if (s[x, y] != '.') f[x + d, y] = s[x, y];
			}
			return f;
		}
		Frame Rotate(Frame f, bool cw)            // 90 degrees around the art (for lying on the back / flying)
		{
			var s = f.Clone(); var o = new Frame();
			for (int y = 6; y < 32; y++) for (int x = 0; x < W; x++)
			{
				char ch = s[x, y]; if (ch == '.') continue;
				int ry = y - 6, rx = x - 3;   // 0..25, 0..28
				int nx = cw ? 31 - ry : ry + 3, ny = cw ? rx + 3 : 28 - rx + 3;
				o[nx, ny] = ch;
			}
			return o;
		}
		// curled up lying pose (sleep): low body hump, head resting on the ground, tail along the floor
		Frame Lying(int eye, int z)
		{
			var f = new Frame();
			// body hump
			for (int y = 22; y <= 31; y++) for (int x = 10; x <= 27; x++)
			{
				double dx = (x - 18.5) / 9.0, dy = (y - 31.5) / 9.5;
				if (dx * dx + dy * dy <= 1) f[x, y] = dy > -0.25 && x > 22 ? M : G;
			}
			// tail
			for (int x = 26; x <= 31; x++) { f[x, 30] = G; f[x, 31] = M; }
			f[31, 29] = M;
			// outline + spikes
			var s = f.Clone();
			for (int y = 20; y <= 31; y++) for (int x = 0; x < W; x++)
				if (s[x, y] != '.' && (s[x - 1, y] == '.' || s[x + 1, y] == '.' || s[x, y - 1] == '.' || (y < 31 && s[x, y + 1] == '.'))) f[x, y] = D;
			foreach (int x in new[] { 13, 17, 21, 25 }) { f[x, 22 + (x == 25 ? 2 : 0) - (x == 17 || x == 21 ? 1 : 0)] = O; f[x + 1, 22 + (x == 25 ? 2 : 0) - (x == 17 || x == 21 ? 1 : 0)] = R; }
			// head resting on the floor (art rows 0..9, cols 0..13), chin on row 31
			var a = Body(new Look { Eye = eye });
			for (int r = 0; r <= 9; r++) for (int c = 0; c <= 13; c++) if (a[r, c] != '.') f[c + 1, r + 22] = a[r, c];
			if (z >= 1) Zed(f, 20, 12, 4);
			if (z >= 2) Zed(f, 25, 5, 5);
			return f;
		}
		// knocked over: on the back, little legs in the air, tongue out
		Frame OnBack(int k)
		{
			var f = Lying(6, 0);
			var a = Body(new Look { Eye = 6, Jaw = 3 });
			for (int r = 0; r <= 11; r++) for (int c = 0; c <= 13; c++) if (a[r, c] != '.') f[c + 1, r + 22 - 2] = a[r, c];
			for (int y = 31; y > 20; y--) for (int x = 0; x < 16; x++) if (y > 29 && f[x, y] == '.' && x > 1) f[x, y] = '.';
			// legs up (two little legs with feet)
			foreach (int lx in new[] { 15, 21 })
			{
				int lift = k == 0 ? (lx == 15 ? 0 : 1) : (lx == 15 ? 1 : 0);
				f.Rect(lx, 16 + lift, 3, 5, M).Rect(lx, 16 + lift, 1, 5, D).Rect(lx + 2, 16 + lift, 1, 5, D).Rect(lx - 1, 15 + lift, 5, 2, D);
			}
			return f;
		}
		Frame Up(Frame f, int n) => f.Shift(0, -n);

		// ============================================================ props and effects (frame coords)
		void St(Frame f, int x, int y, params string[] rows) => f.Stamp(x, y, rows.Select(r => r.Replace('.', ' ')).ToArray());
		void Behind(Frame f, int x, int y, params string[] rows)   // only on empty pixels (cape, backpack, tail-side props)
		{
			for (int j = 0; j < rows.Length; j++) for (int i = 0; i < rows[j].Length; i++)
				if (rows[j][i] != '.' && f[x + i, y + j] == '.') f[x + i, y + j] = rows[j][i];
		}
		void Zed(Frame f, int x, int y, int n)
		{
			f.Rect(x, y, n, 1, Zz).Rect(x, y + n - 1, n, 1, Zz);
			for (int i = 1; i < n - 1; i++) f.Dot(x + n - 1 - i, y + i, Zz);
		}
		void Note(Frame f, int x, int y) => St(f, x, y, ".DD", ".D.", ".D.", "DD.");
		void Star(Frame f, int x, int y, char c) { f.Dot(x, y, c).Dot(x - 1, y, c).Dot(x + 1, y, c).Dot(x, y - 1, c).Dot(x, y + 1, c); }
		void Heart(Frame f, int x, int y) => St(f, x, y, "HH.HH", "HhHHH", ".HHH.", "..H..");
		void Drop(Frame f, int x, int y) => f.Dot(x, y, Water).Dot(x, y + 1, Water);
		void Lines(Frame f, params (int x, int y, int len, bool vert)[] ls) { foreach (var (x, y, len, v) in ls) f.Rect(x, y, v ? 1 : len, v ? len : 1, D); }
		void Circle(Frame f, float cx, float cy, float r, char fill, char edge)
		{
			for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
				for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
				{
					float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
					if (d <= r) f[x, y] = d > r - 1 ? edge : fill;
				}
		}
		void Ring(Frame f, float cx, float cy, float r, char c)
		{
			for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
				for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
				{
					float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
					if (d <= r && d > r - 1) f[x, y] = c;
				}
		}
		// hats sit on the head (head top is row 6, head spans x 4..17)
		void Hat(Frame f, string kind, int dx = 0, int dy = 0)
		{
			switch (kind)
			{
				case "detective": St(f, 6 + dx, 2 + dy, "....bBBBb....", "..bBBBBBBBb..", ".bBBbBBbBBBb.", "bbbbbbbbbbbbb"); break;
				case "wizard": St(f, 7 + dx, -4 + dy, ".......PP..", "......PPP..", ".....PYPPP.", "....PPPPPp.", "...PPPYPPP.", "..PPYPPPPPP", "PPPPPPPPPPPPPP", ".pppppppppppp."); break;
				case "fedora": St(f, 6 + dx, 2 + dy, "...KKKKKKK...", "..KKKKKKKKK..", "..kkkkkkkkk..", "KKKKKKKKKKKKK"); break;
				case "police": St(f, 7 + dx, 2 + dy, "..NNNNNNNNN.", ".NNNNNYNNNNN", "NNNNNNNNNNNN", "...KKKKKKKKK"); break;
				case "explorer": St(f, 6 + dx, 2 + dy, "...AAAAAAA...", "..AAAAAyAAA..", ".AAAAAAAAAAA.", "AAAAAAAAAAAAAAA"); break;
				case "builder": St(f, 6 + dx, 2 + dy, "....YYYYY....", "..YYYYlYYYY..", ".YYYYYYYYYYY.", "yyyyyyyyyyyyy"); break;
				case "chef": St(f, 7 + dx, -3 + dy, "..lWWl.lWWl", ".WWWWWWWWWl", "lWWWWlWWWWl", ".WWWWWWWWW.", ".WWWWWWWWW.", ".lllllllll."); break;
				case "party": St(f, 11 + dx, -2 + dy, "...Y...", "..HPH..", "..PHP..", ".HPHPH.", ".PHPHP.", "HPHPHPH", "YYYYYYY"); break;
				case "beret": St(f, 7 + dx, 3 + dy, ".....K.....", "..HHHHHHH..", ".HHHHHHHHH.", "HHHHHHHHHHH"); break;
			}
		}
		void Sunglasses(Frame f) { f.Rect(8, 9, 8, 2, Blk).Rect(16, 9, 2, 1, Blk).Dot(10, 9, GrayD).Dot(14, 9, GrayD); }
		void Glasses(Frame f) { St(f, 9, 8, "KKKK.KKKK", "KccK.KccK", "KKKKKKKKK"); }
		void Goggles(Frame f) { St(f, 9, 8, "CCCC", "CccC", "CCCC"); f.Rect(13, 9, 5, 1, Teal); }
		void Headphones(Frame f, char cup)
		{
			foreach (var (x, y) in new[] { (9, 5), (10, 4), (11, 4), (12, 4), (13, 4), (14, 4), (15, 4), (16, 5), (17, 6), (17, 7) }) f.Dot(x, y, Blk);
			St(f, 15, 8, "KKK", "K" + cup + "K", "K" + cup + "K", "KKK");
		}
		void Cape(Frame f, int k)
		{
			Behind(f, 14, 15, k == 0
				? new[] { "HHHHH...........", "HHHHHHH.........", ".HHHHHHHH.......", ".HHHHHHHHHH.....", "..HHHHHHHHHHH...", "..HHHHHHhHHHHHH.", "...HHHHHHHHHHHh.", "...HHHHHHHHHH...", "....HHHHHHhH....", ".....HHHHHH.....", "......HHHH......" }
				: new[] { "HHHHH...........", "HHHHHHHH........", ".HHHHHHHHHH.....", ".HHHHHHHHHHHHH..", "..HHHHHHHHHHHHHH", "..HHHHHHhHHHHHh.", "...HHHHHHHHHH...", "....HHHHHHHh....", ".....HHHHHH.....", ".......HHH......", "................" });
			f.Rect(13, 16, 6, 1, Red).Rect(12, 17, 2, 1, Red).Dot(11, 17, RedL);
		}
		// held items: the near hand is at about (5..7, 19..21)
		void Hold(Frame f, string item, int k = 0)
		{
			switch (item)
			{
				case "meat": St(f, 0, 11, "..HHHH..", ".HHhhHH.", "HHhHHHHH", "HHHHHHHh", ".HHHHHH.", "..HHHH..", ".....WW.", "......WW", ".....W.W"); break;
				case "magnifier": St(f, 0, 14, ".gggg.", "gccccg", "gcWccg", "gccccg", "gccccg", ".gggg.", "....bb", ".....b"); break;
				case "book": St(f, 0, 16, "BBBBB.BBBBB", "BWWWWbWWWWB", "BWllWbWllWB", "BWWWWbWWWWB", "BWllWbWllWB", "BWWWWbWWWWB", "BBBBBBBBBBB"); break;
				case "glass": St(f, 0, 10, "...HH", "...H.", "c.H.c", "cwwwc", "cwwwc", "cwwwc", "cwwwc", "ccccc"); break;
				case "pizza": St(f, 0, 11, "bbbbbbbb", "bYYHYYYb", ".YHHYYHY", "..YYYHY.", "...YYY..", "....Y..."); break;
				case "balloon": Circle(f, 3f, 0f, 3.2f, Red, Red); f.Dot(2, -2, RedL).Dot(3, 3, Red); for (int y = 4; y <= 18; y++) f.Dot(3 + (y > 11 ? 1 : 0) + (y > 16 ? 1 : 0), y, GrayL); break;
				case "gloves": St(f, 0, 16, ".HHHH.", "HHhHHH", "HHHHHH", "HHHHHH", ".HHHH.", "..WW.."); St(f, 8, 19, ".HHHH", "HHhHH", "HHHHH", ".HHH."); break;
				case "popcorn": St(f, 0, 15, ".W.Y.W.", "YWYWYWY", "WYWYWYW", "HWHWHWH", "HWHWHWH", "HWHWHWH", ".HWHWH."); break;
				case "camera": St(f, 7, 8, "..KK...", "KKKKKKK", "KccKKKK", "KccKKgK", "KKKKKKK"); break;
				case "tube": St(f, 1, 15, "cc", "cc", "Fc", "FF", "FF", "cc"); break;
				case "wand": for (int i = 0; i < 6; i++) f.Dot(4 - i / 2, 19 - i, BrownD); Star(f, 1, 12, Yel); f.Dot(0, 9, PurpL).Dot(3, 10, Yel); break;
				case "watering": St(f, 0, 16, "C..CCCCC.", ".C.CCCCCC", "..CCCCCC.", "...CCCCC.", "...CCCCC."); if (k == 1) { f.Dot(0, 21, Water).Dot(1, 23, Water).Dot(0, 25, Water).Dot(1, 27, Water); } break;
				case "basketball": Circle(f, 3.5f, 20.5f, 3.6f, O, R); f.Rect(0, 20, 7, 1, Blk).Rect(3, 17, 1, 7, Blk); break;
				case "cupcake": St(f, 1, 14, "..Y..", "..H..", ".FFF.", "FFFFF", "BbBbB", ".BbB."); break;
				case "pan": St(f, 0, 21, "KKKKKKKgg", ".KkkkkK..", "..KKKK..."); St(f, 1, 12, "gggg", "gkgk", "gggg", ".bb.", ".b..", ".b..", ".b.."); break;
				case "walkie": St(f, 2, 15, ".K..", ".K..", "KKKK", "KgKK", "KKKK", "KYKK", "KKKK"); break;
				case "notes": St(f, 0, 16, "lllllll", "lWWWWWl", "lWKKKWl", "lWWWWWl", "lWKKWWl", "lWWWWWl", "lllllll"); for (int i = 0; i < 5; i++) f.Dot(7 + i / 2, 18 - i, Yel); f.Dot(7, 19, Blk); break;
				case "map": St(f, 0, 16, "lllllllll", "lwwWWuuul", "lwwwuuuwl", "luuwwHwwl", "luuuwwwwl", "lllllllll"); break;
				case "hammer": St(f, 0, 11, "kkkkk", "kgkkk", "kkkkk", "..b..", "..b..", "..b..", "..b..", "..b.."); break;
				case "phone": St(f, 2, 16, "KKK", "KcK", "KcK", "KKK"); break;
				case "mic": for (int y = 14; y <= 31; y++) f.Dot(2, y, GrayD); f.Rect(0, 31, 5, 1, GrayD); St(f, 1, 12, "KK", "KK"); f.Dot(3, 13, GrayD); break;
				case "brush": for (int i = 0; i < 5; i++) f.Dot(4 - i / 2, 19 - i, BrownD); f.Dot(2, 14, Red); St(f, 7, 21, ".BBB", "BHYB", "BUGB", ".BB."); break;
				case "drumsticks": f.Dot(4, 18, BrownD).Dot(3, 17, BrownD).Dot(2, 16, BrownD).Dot(9, 18, BrownD).Dot(8, 17, BrownD); break;
				case "fish": St(f, 0, 17, ".UUU.U", "UuUUUU", ".UUU.U"); break;
				case "orb": break;
			}
		}
		Frame Fx(Frame f, string kind, int k = 0)
		{
			switch (kind)
			{
				case "stars": foreach (var (x, y, c) in new[] { (12, 6, Yel), (20, 4, O), (16, 2, Mint) }) Star(f, x + (k == 0 ? 0 : 1), y - k, c); break;
				case "shock": Lines(f, (3, 3, 2, false), (2, 7, 2, false), (20, 4, 2, false), (19, 1, 2, false), (12, 1, 1, true)); break;
				case "tears": Drop(f, 11, 11 + k); Drop(f, 12, 14 - k); Drop(f, 13, 17 + k); break;
				case "sweat": Drop(f, 15, 7 + k); break;
				case "sun": Circle(f, 3f, -2f, 2.6f, Yel, YelD); foreach (var (x, y) in new[] { (3, -6), (3, 2), (-1, -2), (7, -2), (0, -5), (6, -5), (0, 1), (6, 1) }) if (k == 1 || (x + y) % 2 == 0) f.Dot(x, y, Yel); break;
				case "snow": foreach (var (x, y) in new[] { (2, 2), (8, 0), (20, 2), (24, 6), (5, 9), (18, -2) }) f.Dot(x, (y + k * 2 + 20) % 14 - 3, Glass); break;
				case "confetti":
					char[] cs = { Red, Yel, Blue, Purp, Mint };
					var pts = new[] { (2, -4), (7, -2), (13, -5), (19, -3), (25, -1), (4, 2), (22, 3), (10, -6), (28, 6), (0, 6) };
					for (int i = 0; i < pts.Length; i++) f.Dot(pts[i].Item1, pts[i].Item2 + (k + i) % 3, cs[(i + k) % 5]);
					break;
				case "notes": Note(f, 1 + k * 2, 2 - k); Note(f, 22, -2 + k); break;
				case "zz": Zed(f, 20, 0, 4); if (k == 1) Zed(f, 25, -6, 5); break;
				case "shiver": Lines(f, (0, 12, 2, true), (24, 14, 2, true), (2, 20, 2, true), (26, 20, 2, true)); break;
				case "teeth": f.Rect(4, 14, 5, 1, Wt).Dot(5, 14, GrayL).Dot(7, 14, GrayL); break;
				case "smoke": Circle(f, 21f, 0f, 2.6f, Gray, Gray); Circle(f, 24f, -1f, 2f, GrayL, Gray); break;
			}
			return f;
		}
		void Costume(Frame f, string kind)
		{
			char Recolor(char c, char light, char dark) => c == G ? light : (c == M || c == D) ? dark : c;
			int y0 = kind == "trench" ? 16 : 17, y1 = kind == "trench" ? 30 : 26;
			for (int y = y0; y <= y1; y++) for (int x = 8; x <= 22; x++)
			{
				char c = f[x, y]; if (c == '.' || c == O || c == R) continue;
				switch (kind)
				{
					case "labcoat": f[x, y] = Recolor(c, Wt, GrayL); break;
					case "trench": f[x, y] = Recolor(c, GrayD, Blk); break;
					case "astronaut": f[x, y] = Recolor(c, GrayL, Gray); break;
				}
			}
			if (kind == "labcoat") f.Rect(14, 18, 1, 8, GrayL);
			if (kind == "trench") f.Rect(14, 17, 1, 13, Blk).Dot(13, 20, Gray).Dot(13, 24, Gray);
			if (kind == "astronaut") { f.Dot(12, 21, Red).Dot(13, 21, Blue).Rect(20, 17, 4, 7, Gray); }
		}

		// ============================================================ frames
		int F(Frame f) => p.Add(f);
		int fStand = F(Pose());
		int fBlink = F(Pose(eye: 1));
		int fHappy = F(Pose(eye: 2, blush: true));
		int fLookUp = F(Pose(eye: 5));
		int fWide = F(Pose(eye: 3, jaw: 4));
		int fTailUp = F(Pose(eye: 2, tail: 1));
		int fTailDown = F(Pose(eye: 2, tail: -1));
		// walk: alternate feet with a 1 px bob
		int fWalk1 = F(Pose(feet: 1, tail: 1, dy: -1));
		int fWalk2 = F(Pose());
		int fWalk3 = F(Pose(feet: 2, tail: -1, dy: -1));
		// run: leaning forward, arms tucked
		int fRun1 = F(Lean(Pose(arm: 3, feet: 1, tail: 1, jaw: 1), -2, 26));
		int fRun2 = F(Lean(Pose(arm: 3, feet: 2, tail: -1, jaw: 1), -2, 26));
		int fCrouch = F(Squash(Pose(eye: 6), 2));
		int fJump = F(Up(Pose(eye: 2, arm: 1, feet: 3, tail: 1), 3));
		int fFall = F(Pose(eye: 3, jaw: 4, arm: 1, farUp: true, feet: 3, tail: 1));
		int fLand = F(Squash(Pose(eye: 6, jaw: 1), 3));
		int fSit = F(Squash(Pose(tail: -1), 3));
		int fSitBlink = F(Squash(Pose(eye: 1, tail: -1), 3));
		int fSleep1 = F(Lying(1, 0)), fSleep2 = F(Lying(1, 1)), fSleep3 = F(Lying(1, 2));
		var dz1 = OnBack(0); Star(dz1, 3, 16, Yel); Star(dz1, 9, 13, O); Star(dz1, 14, 17, Mint); int fDizzy1 = F(dz1);
		var dz2 = OnBack(1); Star(dz2, 5, 14, Mint); Star(dz2, 11, 12, Yel); Star(dz2, 2, 18, O); int fDizzy2 = F(dz2);
		int fWave1 = F(Pose(eye: 2, arm: 1, blush: true));
		int fWave2 = F(Pose(eye: 2, arm: 1, tail: 1, blush: true, dy: -1));
		int fCheer = F(Up(Pose(eye: 2, jaw: 1, arm: 1, farUp: true, tail: 1), 2));
		int fDrag1 = F(Pose(eye: 3, jaw: 4, arm: 1, farUp: true, feet: 3, tail: 1));
		int fDrag2 = F(Lean(Pose(eye: 3, jaw: 4, arm: 1, farUp: true, feet: 3, tail: -1), 1, 30));
		int fClimb1 = F(Pose(eye: 5, arm: 1, feet: 1));
		int fClimb2 = F(Pose(eye: 5, arm: 2, farUp: true, feet: 2));
		// roar
		int fRoarPrep = F(Squash(Pose(eye: 6, jaw: 1, tail: -1), 1));
		int fRoar1 = F(Lean(Pose(eye: 4, jaw: 2, arm: 1, tail: 1), -1, 26));
		int fRoar2 = F(Lean(Pose(eye: 4, jaw: 2, arm: 2, farUp: true, tail: 1), -2, 26));

		// --- reference sheet 4: feelings
		var eat1 = Pose(eye: 2, jaw: 2); Hold(eat1, "meat"); int fEat1 = F(eat1);
		var eat2 = Pose(eye: 1, jaw: 0, blush: true); Hold(eat2, "meat"); eat2.Rect(0, 14, 3, 3, '.'); int fEat2 = F(eat2);
		int fShock1 = F(Fx(Pose(eye: 3, jaw: 2, arm: 1, farUp: true), "shock"));
		int fShock2 = F(Fx(Up(Pose(eye: 3, jaw: 2, arm: 1, farUp: true), 1), "shock"));
		int fCry1 = F(Fx(Squash(Pose(eye: 1, jaw: 1, tail: -1), 3), "tears", 0));
		int fCry2 = F(Fx(Squash(Pose(eye: 1, jaw: 4, tail: -1), 3), "tears", 1));
		int fThumb = F(Pose(eye: 2, arm: 1, blush: true));
		int fAngry1 = F(Pose(eye: 4, jaw: 0, arm: 2, farUp: false, tail: -1));
		int fAngry2 = F(Squash(Pose(eye: 4, jaw: 1, arm: 2, tail: -1), 1));
		var cool1 = Pose(arm: 2); Sunglasses(cool1); int fCool1 = F(cool1);
		var cool2 = Pose(arm: 1, tail: 1, eye: 0); Sunglasses(cool2); cool2.Dot(20, 6, Yel).Dot(21, 5, Yel); int fCool2 = F(cool2);
		int fAh = F(Up(Pose(eye: 6, jaw: 1, tail: 1), 1));
		var choo = Lean(Pose(eye: 6, jaw: 2, arm: 2, tail: 1), -2, 26); Lines(choo, (0, 11, 2, false), (0, 15, 2, false), (1, 13, 2, false)); int fChoo = F(choo);
		int fDance1 = F(Fx(Lean(Pose(eye: 2, jaw: 1, arm: 1, feet: 1, tail: 1, blush: true), -1, 26), "notes", 0));
		int fDance2 = F(Fx(Lean(Pose(eye: 2, jaw: 0, arm: 2, farUp: true, feet: 2, tail: -1, blush: true), 1, 26), "notes", 1));
		var sniff1 = Lean(Pose(eye: 0, arm: 2, tail: 1), -2, 28); Hold(sniff1, "magnifier"); int fSniff1 = F(sniff1);
		var sniff2 = Lean(Pose(eye: 6, jaw: 4, arm: 2, tail: 1), -3, 28); Hold(sniff2, "magnifier"); int fSniff2 = F(sniff2);
		int fParty1 = F(Fx(Pose(eye: 2, jaw: 1, arm: 1, farUp: true, blush: true), "confetti", 0));
		int fParty2 = F(Fx(Up(Pose(eye: 2, jaw: 1, arm: 1, farUp: true, feet: 3, tail: 1, blush: true), 3), "confetti", 1));
		int fLove = F(Pose(eye: 2, blush: true, tail: 1));
		int fTired1 = F(Fx(Squash(Lean(Pose(eye: 7, jaw: 3, tail: -1), -1, 26), 1), "sweat", 0));
		int fTired2 = F(Fx(Squash(Lean(Pose(eye: 7, jaw: 3, tail: -1), -1, 26), 2), "sweat", 1));

		// --- reference sheet 5: everyday things
		var rd1 = Squash(Pose(eye: 1, arm: 2, tail: -1), 2); Hold(rd1, "book"); int fRead1 = F(rd1);
		var rd2 = Squash(Pose(eye: 0, arm: 2, tail: -1), 2); Hold(rd2, "book"); rd2.Rect(5, 16, 3, 1, Wt); int fRead2 = F(rd2);
		var dr1 = Pose(eye: 0, arm: 2); Hold(dr1, "glass"); int fDrink1 = F(dr1);
		var dr2 = Pose(eye: 1, arm: 2, blush: true); Hold(dr2, "glass"); dr2.Rect(2, 16, 2, 2, Glass); int fDrink2 = F(dr2);
		var pz1 = Pose(eye: 2, jaw: 2, arm: 2); Hold(pz1, "pizza"); int fPizza1 = F(pz1);
		var pz2 = Pose(eye: 1, jaw: 0, arm: 2, blush: true); Hold(pz2, "pizza"); pz2.Rect(0, 13, 3, 3, '.'); int fPizza2 = F(pz2);
		Frame Cold(int k) { var f = Pose(eye: 6, jaw: 1, arm: 2, farUp: false); Fx(f, "teeth"); Fx(f, "snow", k); Fx(f, "shiver"); f.Shift(k == 0 ? -1 : 1, 0); return f; }
		int fCold1 = F(Cold(0)), fCold2 = F(Cold(1));
		int fHot1 = F(Fx(Fx(Pose(eye: 7, jaw: 3, arm: 1), "sun", 0), "sweat", 0));
		int fHot2 = F(Fx(Fx(Pose(eye: 7, jaw: 3, arm: 2), "sun", 1), "sweat", 1));
		Frame Computer(int k)
		{
			var f = Squash(Pose(eye: k == 2 ? 2 : 0, arm: 2, tail: -1), 1);
			St(f, 0, 15, "kkkkkkkkk", "kCCCCCCCk", "kCcCCCCCk", "kCCCCcCCk", "kCCCCCCCk", "kkkkkkkkk", "....k....", "..kkkkk..");
			f.Dot(2 + k * 2, 17 + k, Wt);
			return f;
		}
		int fPc1 = F(Computer(0)), fPc2 = F(Computer(1)), fPc3 = F(Computer(2));
		var bx1 = Pose(eye: 4, arm: 2); Hold(bx1, "gloves"); int fBox1 = F(bx1);
		var bx2 = Lean(Pose(eye: 4, jaw: 1, arm: 2, feet: 1), -2, 28); Hold(bx2, "gloves"); bx2.Shift(-2, 0); Lines(bx2, (0, 14, 2, true)); int fBox2 = F(bx2);
		var bl1 = Pose(eye: 5, arm: 2, tail: 1); Hold(bl1, "balloon"); int fBalloon1 = F(bl1);
		var bl2 = Pose(eye: 2, arm: 2, tail: -1, blush: true); Hold(bl2, "balloon"); bl2.Shift(0, -1); int fBalloon2 = F(bl2);
		var dt1 = Pose(eye: 0, arm: 2); Hat(dt1, "detective"); Hold(dt1, "magnifier"); int fDetective1 = F(dt1);
		var dt2 = Lean(Pose(eye: 6, arm: 2, tail: 1), -2, 28); Hat(dt2, "detective", -1); Hold(dt2, "magnifier"); int fDetective2 = F(dt2);
		var pc1 = Squash(Pose(eye: 0, jaw: 1, arm: 2, tail: -1), 2); Hold(pc1, "popcorn"); int fPop1 = F(pc1);
		var pc2 = Squash(Pose(eye: 3, jaw: 2, arm: 2, tail: -1), 2); Hold(pc2, "popcorn"); pc2.Dot(4, 12, Yel).Dot(6, 10, Wt); int fPop2 = F(pc2);
		var hd1 = Pose(eye: 1, jaw: 0, arm: 1, blush: true); Headphones(hd1, Purp); Fx(hd1, "notes", 0); int fMusic1 = F(hd1);
		var hd2 = Lean(Pose(eye: 2, jaw: 1, arm: 2, feet: 1, tail: 1, blush: true), 1, 26); Headphones(hd2, Blue); Fx(hd2, "notes", 1); int fMusic2 = F(hd2);

		// --- reference sheet 6: hobbies and costumes
		Frame Fishing(int k)
		{
			var f = Squash(Pose(eye: k == 2 ? 3 : 0, arm: 2, tail: -1), 3);
			St(f, 18, 27, "bBBBBb", ".b..b.", ".b..b.", ".b..b.");   // stool behind
			for (int i = 0; i < 10; i++) f.Dot(5 - i / 2, 21 - i, BrownD);
			for (int y = 12; y <= 30 - k * 4; y++) f.Dot(0, y, GrayL);
			if (k == 0) f.Rect(0, 31, 3, 1, Water);
			if (k == 2) St(f, 0, 24, "UUU", "uUU", "UUU");
			return f;
		}
		int fFish1 = F(Fishing(0)), fFish2 = F(Fishing(1)), fFish3 = F(Fishing(2));
		var cm1 = Pose(eye: 0, arm: 2); Hold(cm1, "camera"); int fCamera1 = F(cm1);
		var cm2 = Pose(eye: 0, arm: 2); Hold(cm2, "camera"); Star(cm2, 6, 7, Yel); cm2.Dot(3, 6, Yel).Dot(9, 5, Yel); int fCamera2 = F(cm2);
		Frame Paint(int k)
		{
			var f = Pose(eye: k == 1 ? 2 : 0, arm: 2, tail: k == 1 ? 1 : 0);
			f.Shift(4, 0);
			St(f, 0, 12, "WWWWWW", "WwwwwW", "WwYwwW", "WGGwGW", "WGGGGW", "WWWWWW");
			for (int y = 18; y <= 31; y++) { f.Dot(1, y, BrownD); f.Dot(4, y, BrownD); }
			f.Dot(2, 18, BrownD).Dot(3, 18, BrownD);
			for (int i = 0; i < 4; i++) f.Dot(9 - i, 19 - i, BrownD);
			f.Dot(6, 15, k == 0 ? Red : Blue);
			St(f, 11, 21, ".BBB", "BHYB", "BUGB", ".BB.");
			return f;
		}
		int fPaint1 = F(Paint(0)), fPaint2 = F(Paint(1));
		var sc1 = Pose(eye: 0, arm: 2); Costume(sc1, "labcoat"); Hold(sc1, "tube"); int fScience1 = F(sc1);
		var sc2 = Pose(eye: 3, jaw: 4, arm: 2); Costume(sc2, "labcoat"); Hold(sc2, "tube"); sc2.Dot(1, 13, Mint).Dot(2, 11, Mint).Dot(0, 10, Pink); int fScience2 = F(sc2);
		var wz1 = Pose(eye: 0, arm: 2); Hat(wz1, "wizard"); Hold(wz1, "wand"); int fWizard1 = F(wz1);
		var wz2 = Pose(eye: 2, jaw: 1, arm: 1, tail: 1); Hat(wz2, "wizard"); for (int i = 0; i < 5; i++) wz2.Dot(3 - i / 2, 13 - i, BrownD); Star(wz2, 1, 6, Yel); Star(wz2, 6, 3, PurpL); wz2.Dot(9, 6, Yel); int fWizard2 = F(wz2);
		var spy1 = Pose(eye: 4, arm: 3); Costume(spy1, "trench"); Hat(spy1, "fedora"); int fSpy1 = F(spy1);
		var spy2 = Pose(eye: 6, arm: 3, feet: 1); Costume(spy2, "trench"); Hat(spy2, "fedora"); spy2.Shift(-1, 0); int fSpy2 = F(spy2);
		Frame Guitar(int k)
		{
			var f = Pose(eye: k == 1 ? 1 : 2, jaw: k, arm: 2, tail: k == 0 ? 1 : -1, blush: true);
			Circle(f, 13f, 24f, 3.6f, Brown, BrownD); f.Dot(12, 24, Blk).Dot(13, 24, Blk).Dot(13, 23, Blk);
			for (int i = 0; i < 10; i++) f.Dot(10 - i, 22 - i / 2, BrownD);
			f.Rect(0, 16, 2, 2, BrownD);
			f.Dot(14 + k, 21, D);
			Note(f, 22 + k, 4 - k * 3);
			return f;
		}
		int fGuitar1 = F(Guitar(0)), fGuitar2 = F(Guitar(1));
		var wt1 = Pose(eye: 0, arm: 2); Hold(wt1, "watering", 0); int fWater1 = F(wt1);
		var wt2 = Lean(Pose(eye: 2, arm: 2, blush: true), -1, 28); Hold(wt2, "watering", 1); int fWater2 = F(wt2);
		Frame Swim(int k)
		{
			var f = Pose(eye: 0, jaw: 1, arm: k == 0 ? 1 : 2, farUp: k == 1, feet: k == 0 ? 1 : 2, tail: k == 0 ? 1 : -1);
			Goggles(f);
			for (int x = 8; x <= 22; x++) { f[x, 23] = O; f[x, 24] = R; }
			f.Rect(10, 23, 2, 2, Wt).Rect(17, 23, 2, 2, Wt);
			f.Dot(2 + k, 7 - k, Glass).Dot(4, 4 + k, Glass);
			return f;
		}
		int fSwim1 = F(Swim(0)), fSwim2 = F(Swim(1));
		var bb1 = Pose(eye: 0, arm: 2); Hold(bb1, "basketball"); int fBall1 = F(bb1);
		var bb2 = Up(Pose(eye: 5, arm: 1, farUp: true, feet: 3, tail: 1), 3); Circle(bb2, 5f, 2f, 3f, O, R); bb2.Rect(3, 2, 5, 1, Blk); int fBall2 = F(bb2);
		var cc1 = Pose(eye: 2, jaw: 0, arm: 2, blush: true); Hat(cc1, "party"); Hold(cc1, "cupcake"); int fCake1 = F(cc1);
		var cc2 = Pose(eye: 2, jaw: 4, arm: 2, blush: true); Hat(cc2, "party"); Hold(cc2, "cupcake"); cc2.Dot(3, 13, O); int fCake2 = F(cc2);

		// --- reference sheet 7: jobs
		Frame Astronaut(int k)
		{
			var f = Up(Pose(eye: 2, jaw: 1, arm: 2, feet: 3, tail: k == 0 ? 1 : -1), 2 + k);
			Costume(f, "astronaut");
			Ring(f, 11f, 9.5f - (2 + k), 9f, Glass);
			f.Dot(5, 3 - k, Wt).Dot(6, 2 - k, Wt);
			return f;
		}
		int fAstro1 = F(Astronaut(0)), fAstro2 = F(Astronaut(1));
		var ch1 = Pose(eye: 0, arm: 2); Hat(ch1, "chef"); Hold(ch1, "pan"); int fChef1 = F(ch1);
		var ch2 = Pose(eye: 2, arm: 2, tail: 1); Hat(ch2, "chef"); Hold(ch2, "pan"); ch2.Shift(0, -1); St(ch2, 0, 15, ".Y.", "YWY"); int fChef2 = F(ch2);
		var ft1 = Pose(eye: 0, feet: 0); Circle(ft1, 2.5f, 28.5f, 2.6f, Wt, Blk); ft1.Dot(2, 28, Blk).Dot(1, 29, Blk); int fSoccer1 = F(ft1);
		var ft2 = Lean(Pose(eye: 6, jaw: 1, feet: 1, arm: 1), 1, 30); int fSoccer2 = F(ft2);
		var pl1 = Pose(eye: 0, arm: 2); Hat(pl1, "police"); Hold(pl1, "walkie"); pl1.Dot(12, 19, Yel); int fPolice1 = F(pl1);
		var pl2 = Pose(eye: 0, jaw: 1, arm: 2); Hat(pl2, "police"); Hold(pl2, "walkie"); pl2.Dot(12, 19, Yel); Lines(pl2, (0, 12, 1, true), (1, 10, 1, true)); int fPolice2 = F(pl2);
		var wr1 = Squash(Pose(eye: 1, arm: 2, tail: -1), 2); Hold(wr1, "notes"); int fWrite1 = F(wr1);
		var wr2 = Squash(Pose(eye: 0, arm: 2, tail: -1), 2); Hold(wr2, "notes"); wr2.Dot(3, 19, Blk).Dot(2, 21, Blk); int fWrite2 = F(wr2);
		Frame Drums(int k)
		{
			var f = Pose(eye: k == 1 ? 6 : 2, jaw: k, arm: 2, farUp: k == 1, tail: k == 0 ? 1 : -1);
			St(f, 0, 23, ".HHHHHHH.", "HWWWWWWWH", "HWWWWWWWH", "HHHHHHHHH", "HlHlHlHlH", "HHHHHHHHH", ".g.....g.", "g.......g", ".........");
			St(f, 0, 18, "YYYY");
			f.Dot(1, 19, GrayD);
			Hold(f, "drumsticks");
			if (k == 1) { f.Dot(5, 21, Yel).Dot(1, 16, Yel); }
			return f;
		}
		int fDrums1 = F(Drums(0)), fDrums2 = F(Drums(1));
		var ex1 = Pose(eye: 0, arm: 2); Hat(ex1, "explorer"); Behind(ex1, 19, 15, ".bbbb", "bBBBBb", "bBUBBb", "bBBBBb", "bBBBBb", ".bbbb."); Hold(ex1, "map"); int fExplore1 = F(ex1);
		var ex2 = Pose(eye: 5, arm: 2, feet: 1); Hat(ex2, "explorer"); Behind(ex2, 19, 15, ".bbbb", "bBBBBb", "bBUBBb", "bBBBBb", "bBBBBb", ".bbbb."); Hold(ex2, "map"); int fExplore2 = F(ex2);
		Frame Hero(int k, bool flying)
		{
			var f = flying ? Lean(Up(Pose(eye: 0, jaw: 0, arm: 2, feet: 3, tail: 1), 2), -4, 30) : Pose(eye: 2, arm: 1, tail: 1);
			Cape(f, k);
			if (flying) { f.Dot(28, 14, D).Dot(29, 18, D).Dot(28, 22, D); }
			return f;
		}
		int fHeroStand = F(Hero(0, false)), fHeroFly1 = F(Hero(0, true)), fHeroFly2 = F(Hero(1, true));
		var bd1 = Pose(eye: 0, arm: 1); Hat(bd1, "builder"); Hold(bd1, "hammer"); bd1.Shift(0, 0); int fBuild1 = F(bd1);
		var bd2 = Pose(eye: 6, jaw: 1, arm: 2); Hat(bd2, "builder"); St(bd2, 0, 19, "kkk", "kkk", ".bbbb"); bd2.Dot(1, 23, Yel).Dot(3, 24, Yel); int fBuild2 = F(bd2);
		// Juggling: three balls travel around a loop in front of his face (thrown up from one hand, over the top,
		// caught by the other), a third of the loop apart, so every ball really moves from frame to frame.
		char[] orbColors = { Blue, Red, Mint };
		// 24 frames: each ball moves about 2 px per frame, so the loop looks smooth (the body keeps an 8-step rhythm)
		const int JuggleFrames = 24;
		Frame Juggle(int k)
		{
			// the hand that is about to throw goes up, the other one waits low
			int beat = k * 8 / JuggleFrames;
			bool throwNear = beat % 4 < 2;
			var f = Pose(eye: 5, jaw: beat % 4 == 1 ? 4 : 0, arm: throwNear ? 1 : 2, farUp: !throwNear, tail: beat % 2 == 0 ? 1 : -1);
			for (int i = 0; i < 3; i++)
			{
				// position on the loop (0..1): up on the outside, down on the inside, faster at the bottom
				double t = (k / (double)JuggleFrames + i / 3.0) % 1.0;
				double a = Math.PI * 2 * t;
				int x = (int)Math.Round(9 + Math.Cos(a) * 7);   // 2..16: the whole ball stays inside the frame
				int y = (int)Math.Round(3 - Math.Sin(a) * 7 * (Math.Sin(a) > 0 ? 1.0 : 0.45));
				Circle(f, x, y, 1.6f, orbColors[i], orbColors[i]);
				f.Dot(x - 1, y - 1, Wt);   // a little shine
			}
			return f;
		}
		int[] fJuggle = Enumerable.Range(0, JuggleFrames).Select(k => F(Juggle(k))).ToArray();
		Frame Sing(int k)
		{
			var f = Pose(eye: k == 0 ? 2 : 1, jaw: k == 0 ? 1 : 2, arm: 2, tail: k == 0 ? 1 : -1, blush: true);
			Hold(f, "mic");
			Note(f, 1 + k * 3, 1 - k * 2); Note(f, 24, -3 + k * 2);
			return f;
		}
		int fSing1 = F(Sing(0)), fSing2 = F(Sing(1));
		int fYawn = F(Up(Pose(eye: 1, jaw: 2, arm: 1, farUp: true, tail: 1), 1));
		int fStomp1 = F(Squash(Pose(eye: 4, jaw: 1, feet: 1, tail: 1), 1));
		var stomp2 = Pose(eye: 4, jaw: 2, feet: 0, tail: -1); Lines(stomp2, (0, 31, 3, false), (6, 30, 1, true), (26, 31, 3, false)); int fStomp2 = F(stomp2);
		int fPhone1 = F(Pose(eye: 0, jaw: 0, arm: 2)); Hold(p.Frames[fPhone1], "phone");
		var ph2 = Pose(eye: 2, jaw: 1, arm: 2, blush: true); Hold(ph2, "phone"); ph2.Dot(4, 13, Yel); int fPhone2 = F(ph2);

		// ============================================================ child objects (separate windows)
		// roar sound waves
		Frame Waves(int k)
		{
			var f = new Frame();
			for (int i = 0; i < 3; i++) for (int y = -2 - i; y <= 2 + i; y++) f.Dot(3 - i * 2 - Math.Abs(y) / 2 + k, 12 + y, i % 2 == 0 ? M : G);
			return f;
		}
		int fWaves1 = F(Waves(0)), fWaves2 = F(Waves(1));
		// soccer ball on the floor, rolling
		Frame Ball(int k)
		{
			var f = new Frame(); Circle(f, 2.5f, 28.5f, 2.6f, Wt, Blk);
			var spots = new[] { (2, 28), (1, 29), (3, 27), (2, 29) }; var (sx, sy) = spots[k % 4]; f.Dot(sx, sy, Blk).Dot(sx + 1, sy, Blk);
			return f;
		}
		var rollBall = new int[4]; for (int i = 0; i < 4; i++) rollBall[i] = F(Ball(i));
		// balloon on its own
		Frame BalloonAlone(int k) { var f = new Frame(); Hold(f, "balloon"); if (k == 1) f.Shift(1, 0); return f; }
		int fBalAway1 = F(BalloonAlone(0)), fBalAway2 = F(BalloonAlone(1));
		// basketball in the air
		Frame Hoop(int k) { var f = new Frame(); Circle(f, 5f, 2f, 3f, O, R); if (k == 0) f.Rect(3, 2, 5, 1, Blk); else f.Rect(5, 0, 1, 5, Blk); return f; }
		int fHoop1 = F(Hoop(0)), fHoop2 = F(Hoop(1));
		// an ant runs away from the magnifier
		Frame Ant(int k) { var f = new Frame(); St(f, 0, 29, k == 0 ? "K.K.K." : ".K.K.K", "KKKKKK", k == 0 ? ".K.K.K" : "K.K.K."); f.Dot(0, 28, Blk); return f; }
		int fAnt1 = F(Ant(0)), fAnt2 = F(Ant(1));
		// angry smoke puff
		Frame Smoke(int k) { var f = new Frame(); Circle(f, 21f, 0f, 2.6f + k * 0.4f, Gray, Gray); Circle(f, 24f, -1f, 2f + k * 0.3f, GrayL, Gray); return f; }
		int fSmoke1 = F(Smoke(0)), fSmoke2 = F(Smoke(1));
		// love heart in a speech bubble
		Frame Bubble() { var f = new Frame(); St(f, 15, -4, ".DDDDDDD.", "D.......D", "D..H.H..D", "D..HHH..D", "D...H...D", ".DDDDDDD.", "...DD....", "...D....."); return f; }
		int fLoveBubble = F(Bubble());
		// a music note floating up
		var nt = new Frame(); Note(nt, 22, 0); int fNoteUp = F(nt);
		// sneeze puff
		var pf = new Frame(); Circle(pf, 2f, 12f, 2.2f, GrayL, GrayL); pf.Dot(4, 10, GrayL).Dot(0, 14, GrayL); int fPuff = F(pf);
		// paper airplane
		Frame Plane(int k) { var f = new Frame(); St(f, 0, 15, k == 0 ? new[] { "....lll", "..llWWl", "llWWWgl", "..llggl" } : new[] { "..lllll", "llWWWWl", "..lWggl", "...ll.." }); return f; }
		int fPlane1 = F(Plane(0)), fPlane2 = F(Plane(1));
		var plh = Pose(eye: 0, arm: 1); St(plh, 1, 10, "....lll", "..llWWl", "llWWWgl"); int fPlaneHold = F(plh);
		int fPlaneThrow = F(Lean(Pose(eye: 2, arm: 2, feet: 1), -1, 28));
		// a flower that grows where Rex watered, then wilts
		Frame Flower(int g)
		{
			var f = new Frame();
			f.Rect(0, 31, 6, 1, BrownD);
			if (g >= 1) f.Rect(2, 30 - g, 1, g, M).Dot(1, 30 - g / 2, M);
			if (g >= 3) { St(f, 0, 30 - g - 3, ".H.", "HYH", ".H."); }
			if (g == 5) { f.Rect(0, 26, 3, 3, '.'); St(f, 2, 26, "H.", ".H", "HY"); }
			return f;
		}
		int fFlower1 = F(Flower(1)), fFlower2 = F(Flower(2)), fFlower3 = F(Flower(3)), fFlower4 = F(Flower(4)), fWilt = F(Flower(5));
		// bubbles while swimming
		Frame Bubbles(int k) { var f = new Frame(); Ring(f, 3f, 5f, 1.6f, Glass); Ring(f, 6f, 1f - k, 1.1f, Glass); return f; }
		int fBub1 = F(Bubbles(0)), fBub2 = F(Bubbles(1));

		p.IconFrame = fHappy;

		// ============================================================ animations
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 35, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 20, "0", "areaH-imageH", 2));

		Anim Show(int id, string name, int ms, params (int f, int n)[] steps)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			foreach (var (f, n) in steps) a.Seq(Enumerable.Repeat(f, n).ToArray());
			a.Then(100, 1);
			return a;
		}
		Anim Loop(int id, string name, int ms, string loops, params int[] frames)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString()).Loop(loops, 0);
			a.Seq(frames); a.Then(100, 1);
			return a;
		}
		Anim Lead(int id, string name, int ms, int next, params (int f, int n)[] steps)   // leads into another animation
		{
			var a = Show(id, name, ms, steps); a.Next.Clear(); a.Then(100, next); return a;
		}

		// --- core behaviour (ids the app knows by name)
		p.A(1, "walk").Move("-3", "0").Speed("150").Loop("random/4+8")
			.Seq(fWalk1, fWalk2, fWalk3, fWalk2)
			.Then(50, 1).Then(8, 2).Then(10, 11).Then(5, 20).Then(4, 21).Then(3, 9).Then(3, 22).Then(2, 23)
			.Then(3, 72).Then(3, 74).Then(3, 61).Then(2, 63)
			.Then(8, 16, "taskbar").Then(5, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window")
			.OnGravity(100, 5);
		var turn = p.A(2, "turn").Move("0", "0").Speed("130");
		turn.Seq(fBlink, fStand, fStand); turn.Action = "flip"; turn.Then(100, 1);
		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("45").Loop("30").Seq(fDrag1, fFall, fDrag2, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall, fDrag2).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fDrag2).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("120", "200").Seq(fLand, fLand, fCrouch, fStand, fHappy).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("150", "260").Seq(fLand, fDizzy1, fDizzy2, fDizzy1, fDizzy2, fDizzy1, fCrouch, fBlink, fStand).Then(100, 1);
		p.A(9, "jump").Move("-5", "-9", "-5", "9").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");
		p.A(10, "sync").Move("0", "0").Speed("100", "150").Loop("6").Seq(fHappy, fCheer, fWave1, fCheer).Then(100, 1);
		p.A(11, "blink").Move("0", "0").Speed("450", "120")
			.Seq(fStand, fBlink, fStand, fBlink, fStand)
			.Then(30, 1).Then(5, 15).Then(5, 17).Then(4, 18).Then(4, 19).Then(4, 24)
			.Then(2, 30).Then(2, 31).Then(2, 32).Then(2, 33).Then(2, 34).Then(2, 35).Then(2, 36).Then(2, 37).Then(2, 38).Then(2, 39).Then(2, 40).Then(2, 41)
			.Then(2, 42).Then(2, 43).Then(2, 44).Then(2, 45).Then(2, 46).Then(2, 47).Then(2, 48).Then(2, 49).Then(2, 50).Then(2, 51).Then(2, 52).Then(2, 53)
			.Then(2, 54).Then(2, 55).Then(2, 56).Then(2, 57).Then(2, 58).Then(2, 59).Then(2, 60).Then(2, 61).Then(2, 62).Then(2, 63).Then(2, 64).Then(2, 65)
			.Then(2, 66).Then(2, 67).Then(2, 68).Then(2, 69).Then(2, 70).Then(2, 71).Then(3, 72).Then(2, 73).Then(3, 74).Then(2, 75).Then(2, 76).Then(2, 77)
			.Then(3, 80).Then(3, 82).Then(3, 84).Then(3, 86).Then(3, 88).Then(3, 90).Then(3, 92).Then(3, 94).Then(3, 96);
		p.A(12, "climb").Move("0", "-3").Speed("140").Loop("5000", 2).Seq(fClimb1, fClimb2, fClimb1, fClimb2).Then(100, 12).OnBorder(100, 2);
		p.A(13, "sleep").Move("0", "0").Speed("800", "450").Loop("random/4+5", 2).Seq(fSitBlink, fSleep1, fSleep1, fSleep2, fSleep3, fSleep2).Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("220", "160").Seq(fSleep1, fSitBlink, fSit, fYawn, fYawn, fStand, fHappy).Then(100, 1);
		Show(15, "look around", 260, (fStand, 1), (fLookUp, 2), (fStand, 1), (fTailUp, 1), (fTailDown, 1), (fStand, 1));
		p.A(16, "sit").Move("0", "0").Speed("600", "400").Loop("random/3+4", 1).Seq(fSit, fSit, fSitBlink, fSit).Then(60, 1).Then(40, 13);
		Loop(17, "wave", 170, "3", fStand, fWave1, fWave2, fWave1, fWave2);
		Loop(18, "tail wag", 140, "random/3+4", fTailUp, fHappy, fTailDown, fHappy);
		Show(19, "yawn", 220, (fStand, 1), (fYawn, 5), (fBlink, 2), (fStand, 1));
		// 20 roar (child: sound waves fly forward)
		Lead(20, "roar", 140, 81, (fStand, 1), (fRoarPrep, 3));
		Show(81, "ROAR", 120, (fRoar1, 2), (fRoar2, 6), (fRoar1, 2), (fHappy, 3));
		p.A(101, "roar waves").Move("-6", "0").Speed("80").Loop("8", 0).Fade(1, 0).Seq(fWaves1, fWaves2);
		p.Childs.Add((81, "imageX", "imageY", 101));
		p.A(21, "run").Move("-7", "0").Speed("90").Loop("random/3+10").Seq(fRun1, fRun2).Then(70, 1).Then(30, 21).OnBorder(100, 2).OnBorder(40, 9, "window").OnGravity(100, 5);
		Loop(22, "stomp", 150, "random/3+3", fStomp1, fStomp2);
		Loop(23, "cheer", 140, "random/3+3", fCheer, fHappy);
		Loop(24, "think", 300, "2", fLookUp, fStand, fLookUp, fBlink);

		// --- feelings (sheet 4)
		Show(30, "eat meat", 200, (fEat1, 2), (fEat2, 2), (fEat1, 2), (fEat2, 3), (fHappy, 2));
		Loop(31, "shocked", 100, "random/3+4", fShock1, fShock2);
		Loop(32, "cry", 220, "random/3+4", fCry1, fCry2);
		Show(33, "thumbs up", 200, (fStand, 1), (fThumb, 6), (fHappy, 2));
		// 34 angry (child: smoke puff rises)
		Lead(34, "angry", 140, 83, (fAngry1, 3), (fAngry2, 2));
		Show(83, "fuming", 140, (fAngry2, 2), (fAngry1, 3), (fAngry2, 2), (fAngry1, 3), (fBlink, 1), (fStand, 1));
		p.A(103, "smoke").Move("1", "-2").Speed("110").Loop("8", 0).Fade(1, 0).Seq(fSmoke1, fSmoke2);
		p.Childs.Add((83, "imageX", "imageY", 103));
		Loop(35, "cool", 300, "random/3+3", fCool1, fCool2);
		// 36 sneeze (child: puff drifts forward)
		Lead(36, "sneeze", 150, 85, (fStand, 1), (fAh, 3), (fStand, 1), (fAh, 4));
		Show(85, "achoo", 140, (fChoo, 4), (fBlink, 2), (fHappy, 2));
		p.A(105, "sneeze puff").Move("-3", "0", "-1", "-1").Speed("100").Loop("8", 0).Fade(0.9, 0).Seq(fPuff);
		p.Childs.Add((85, "imageX", "imageY", 105));
		// 37 dance (child: notes)
		Lead(37, "dance", 150, 87, (fDance1, 1), (fDance2, 1));
		var dance = p.A(87, "dancing").Move("0", "0").Speed("150").Loop("random/3+5", 0); dance.Seq(fDance1, fDance2); dance.Then(100, 1);
		p.A(107, "note").Move("-1", "-2", "1", "-2").Speed("100").Loop("10", 0).Fade(1, 0).Seq(fNoteUp);
		p.Childs.Add((87, "imageX", "imageY", 107));
		// 38 sniff with a magnifier (child: an ant runs away)
		Lead(38, "sniff", 220, 89, (fSniff1, 4), (fSniff2, 3), (fSniff1, 2), (fSniff2, 3));
		Show(89, "ant!", 140, (fShock1, 3), (fLookUp, 2), (fHappy, 3));
		p.A(109, "ant runs").Move("-4", "0").Speed("70").Loop("80", 0).Seq(fAnt1, fAnt2).Then(100, 109);
		p.Childs.Add((89, "imageX", "imageY", 109));
		Loop(39, "party", 130, "random/3+5", fParty1, fParty2);
		// 40 love (child: heart bubble floats up)
		Lead(40, "love", 200, 91, (fStand, 1));
		Show(91, "in love", 220, (fLove, 6), (fHappy, 3));
		p.A(111, "love bubble").Move("0", "-2").Speed("110").Loop("10", 0).Fade(1, 0).Seq(fLoveBubble);
		p.Childs.Add((91, "imageX", "imageY", 111));
		Loop(41, "tired", 260, "random/3+3", fTired1, fTired2);

		// --- everyday things (sheet 5)
		Loop(42, "read", 420, "random/3+3", fRead1, fRead1, fRead2, fRead1);
		Show(43, "drink", 260, (fDrink1, 3), (fDrink2, 4), (fDrink1, 2), (fDrink2, 3), (fHappy, 2));
		Show(44, "pizza", 200, (fPizza1, 2), (fPizza2, 2), (fPizza1, 2), (fPizza2, 3), (fHappy, 2));
		Loop(45, "cold", 70, "random/3+12", fCold1, fCold2);
		Loop(46, "hot", 240, "random/3+4", fHot1, fHot2);
		Loop(47, "computer", 140, "random/3+6", fPc1, fPc2, fPc1, fPc2, fPc3);
		Loop(48, "boxing", 140, "random/3+5", fBox1, fBox2);
		// 49 balloon (child: let go, it floats away)
		Lead(49, "balloon", 260, 93, (fBalloon1, 3), (fBalloon2, 3), (fBalloon1, 3), (fBalloon2, 3));
		Show(93, "let go", 170, (fWide, 3), (fLookUp, 6), (fWave1, 2), (fWave2, 2), (fHappy, 2));
		p.A(113, "balloon away").Move("-1", "-3", "1", "-4").Speed("70").Loop("100", 0).Seq(fBalAway1, fBalAway1, fBalAway2, fBalAway2).Then(100, 113);
		p.Childs.Add((93, "imageX", "imageY", 113));
		Loop(50, "detective", 300, "random/3+3", fDetective1, fDetective2);
		Loop(51, "popcorn", 260, "random/3+4", fPop1, fPop1, fPop2);
		// 52 superhero: stands, takes off, flies across the screen, lands
		Lead(52, "superhero", 150, 53, (fHeroStand, 6), (fCrouch, 2));
		p.A(53, "hero takeoff").Move("-3", "-6").Speed("80").Loop("random/4+5", 0).Seq(fHeroFly1, fHeroFly2)
			.Then(100, 54).OnBorder(100, 54, "horizontal").OnBorder(100, 2, "vertical");
		p.A(54, "hero flight").Move("-8", "0").Speed("70").Loop("random/4+10", 0).Seq(fHeroFly1, fHeroFly2)
			.Then(100, 55).OnBorder(100, 55, "vertical");
		p.A(55, "hero landing").Move("-3", "5").Speed("80").Loop("300", 0).Seq(fHeroFly1, fHeroFly2)
			.Then(100, 55).OnBorder(100, 7, "taskbar").OnBorder(100, 7, "window").OnBorder(100, 2, "vertical");
		Loop(56, "headphones", 200, "random/3+5", fMusic1, fMusic2);

		// --- hobbies and costumes (sheet 6)
		Show(57, "fishing", 280, (fFish1, 6), (fFish2, 3), (fFish1, 3), (fFish2, 4), (fFish3, 5), (fHappy, 2));
		Show(58, "photo", 200, (fCamera1, 4), (fCamera2, 2), (fCamera1, 3), (fCamera2, 2), (fHappy, 2));
		Loop(59, "paint", 320, "random/3+3", fPaint1, fPaint1, fPaint2);
		Show(60, "science", 240, (fScience1, 4), (fScience2, 3), (fScience1, 2), (fScience2, 3), (fHappy, 2));
		Show(61, "wizard", 200, (fWizard1, 4), (fWizard2, 3), (fWizard1, 2), (fWizard2, 3), (fHappy, 1));
		var spy = p.A(62, "spy").Move("-1", "0").Speed("240").Loop("random/3+4", 0); spy.Seq(fSpy1, fSpy2); spy.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		Lead(63, "guitar", 180, 95, (fGuitar1, 1));
		var gt = p.A(95, "play guitar").Move("0", "0").Speed("180").Loop("random/3+5", 0); gt.Seq(fGuitar1, fGuitar2); gt.Then(100, 1);
		p.Childs.Add((95, "imageX", "imageY", 107));
		// 64 watering (child: a flower grows there, stays a while, then wilts)
		Lead(64, "water plant", 240, 97, (fWater1, 3), (fWater2, 5));
		Show(97, "flower!", 200, (fHappy, 4), (fWave1, 2), (fWave2, 2), (fHappy, 2));
		var fl = p.A(117, "flower").Move("0", "0").Speed("300").Seq(fFlower1, fFlower2, fFlower3, fFlower4);
		fl.Seq(Enumerable.Repeat(fFlower4, 60).ToArray()); fl.Seq(fWilt, fWilt, fWilt, fFlower2);
		p.Childs.Add((97, "imageX", "imageY", 117));
		// 65 swim (child: bubbles)
		Lead(65, "swim", 200, 98, (fSwim1, 1));
		var sw = p.A(98, "swimming").Move("0", "0").Speed("200").Loop("random/3+4", 0); sw.Seq(fSwim1, fSwim2); sw.Then(100, 1);
		p.A(118, "bubbles").Move("0", "-2").Speed("110").Loop("10", 0).Fade(1, 0).Seq(fBub1, fBub2);
		p.Childs.Add((98, "imageX", "imageY", 118));
		// 66 basketball (child: the shot flies in an arc)
		Lead(66, "basketball", 160, 79, (fBall1, 3), (fCrouch, 2));
		Show(79, "shoot", 130, (fBall2, 1), (fCheer, 3), (fLookUp, 6), (fCheer, 3), (fHappy, 2));
		p.A(119, "ball arc").Move("-4", "-9", "-4", "9").Speed("60").Loop("1", 0).Seq(Enumerable.Repeat(fHoop1, 8).Concat(Enumerable.Repeat(fHoop2, 8)).ToArray())
			.Then(100, 120).OnBorder(100, 120, "taskbar").OnBorder(100, 120, "window");
		p.A(120, "ball bounce").Move("-3", "-4", "-3", "4").Speed("70").Loop("6", 0).Seq(fHoop1, fHoop2, fHoop1, fHoop2).Then(100, 120);
		p.Childs.Add((79, "imageX", "imageY", 119));
		Show(67, "birthday", 220, (fCake1, 3), (fCake2, 2), (fCake1, 2), (fCake2, 3), (fParty1, 2), (fParty2, 2));

		// --- jobs (sheet 7)
		var astro = p.A(68, "astronaut").Move("0", "0").Speed("300").Loop("random/3+4", 0); astro.Seq(fAstro1, fAstro2); astro.Then(100, 1);
		Show(69, "chef", 200, (fChef1, 3), (fChef2, 2), (fChef1, 3), (fChef2, 2), (fHappy, 2));
		// 70 soccer (child: the ball rolls away)
		Lead(70, "soccer", 180, 100, (fSoccer1, 4));
		Show(100, "kick", 130, (fSoccer2, 3), (fLookUp, 2), (fCheer, 3), (fHappy, 2));
		p.A(121, "ball roll").Move("-6", "0", "-3", "0").Speed("70").Loop("40", 0).Seq(rollBall).Then(100, 121);
		p.Childs.Add((100, "imageX", "imageY", 121));
		Loop(71, "police", 260, "random/3+3", fPolice1, fPolice2);
		// 72 notes, then a paper airplane (child)
		Lead(72, "write", 300, 73, (fWrite1, 3), (fWrite2, 2), (fWrite1, 3), (fWrite2, 2), (fPlaneHold, 4));
		Show(73, "paper plane", 140, (fPlaneThrow, 3), (fLookUp, 4), (fHappy, 2));
		p.A(122, "plane flight").Move("-6", "-2", "-6", "1").Speed("60").Loop("30", 0).Seq(fPlane1, fPlane1, fPlane2, fPlane2).Then(100, 122);
		p.Childs.Add((73, "imageX", "imageY", 122));
		Lead(74, "drums", 120, 75, (fDrums1, 1));
		var drums = p.A(75, "drumming").Move("0", "0").Speed("120").Loop("random/3+6", 0); drums.Seq(fDrums1, fDrums2); drums.Then(100, 1);
		p.Childs.Add((75, "imageX", "imageY", 107));
		var explore = p.A(76, "explorer").Move("-1", "0").Speed("260").Loop("random/3+4", 0); explore.Seq(fExplore1, fExplore2); explore.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		Show(77, "builder", 150, (fBuild1, 2), (fBuild2, 2), (fBuild1, 2), (fBuild2, 2), (fBuild1, 2), (fBuild2, 2), (fHappy, 2));
		Loop(80, "juggle", 45, "random/3+3", fJuggle);
		Lead(82, "sing", 220, 84, (fSing1, 1));
		var sing = p.A(84, "singing").Move("0", "0").Speed("220").Loop("random/3+4", 0); sing.Seq(fSing1, fSing2); sing.Then(100, 1);
		p.Childs.Add((84, "imageX", "imageY", 107));
		Show(86, "phone", 240, (fPhone1, 4), (fPhone2, 3), (fPhone1, 3), (fPhone2, 3), (fHappy, 1));
		Loop(88, "tail chase", 120, "random/3+3", fTailUp, fTailDown, fCheer, fTailDown);
		Show(90, "big roar", 120, (fStomp1, 2), (fStomp2, 2), (fStomp1, 2), (fRoar2, 6), (fHappy, 2));
		Loop(92, "hop", 120, "random/3+3", fCrouch, fJump, fJump, fCrouch);
		Show(94, "stretch", 220, (fStand, 1), (fCheer, 5), (fYawn, 3), (fBlink, 1), (fStand, 1));
		Show(96, "wink", 220, (fStand, 2), (fBlink, 1), (fHappy, 4));

		// 99 kill: waves goodbye and fades
		p.A(99, "kill").Move("0", "0").Speed("130", "220").Fade(1, 0).Seq(fWave1, fWave2, fWave1, fBlink, fBlink).Then(100, 99);

		p.Readme = """
			# Rex the Dino

			## Description

			A little green pixel dinosaur with orange spikes, a big head and tiny arms.

			He stomps along your taskbar, roars (the sound waves fly off), plays soccer and
			basketball, dances, sings, plays guitar and drums, reads, paints, fishes, swims,
			dresses up as a detective, a wizard, a spy, a scientist, a chef, a police officer,
			an explorer, a builder, an astronaut and a superhero who flies across your screen.

			## About pet

			Author: Luan Felipe

			## Animations

			* walk, run, turn, jump onto windows, fall (soft landing, hard landing knocked over)
			* climb the screen border, sit, sleep curled up, wake up, yawn, stretch
			* blink, look around, wave, tail wag, stomp, roar, cheer, hop, wink
			* eat meat, pizza, popcorn, drink, birthday cupcake
			* shocked, crying, thumbs up, angry (smoke), cool sunglasses, sneeze, dance, love, tired
			* read, computer, phone, boxing, balloon, headphones, fishing, photo, painting, guitar,
			  drums, singing, juggling, watering a flower, swimming, basketball, soccer, writing
			* costumes: detective, wizard, spy, scientist, chef, police, explorer, builder,
			  astronaut, superhero
			* separate little windows: roar waves, smoke, sneeze puff, music notes, an ant,
			  a heart bubble, a balloon, a basketball, a soccer ball, a paper plane, a flower,
			  bubbles
			""";
		p.ReadmePt = """
			# Rex, o Dino

			## Descrição

			Um dinossaurinho verde de pixel art com espinhos laranja, cabeção e bracinhos.

			Ele anda pisando forte pela barra de tarefas, ruge (as ondas do rugido saem
			voando), joga futebol e basquete, dança, canta, toca violão e bateria, lê, pinta,
			pesca, nada e se fantasia de detetive, mago, espião, cientista, chef, policial,
			explorador, pedreiro, astronauta e de super-herói que atravessa a tela voando.

			## Sobre o pet

			Autor: Luan Felipe

			## Animações

			* andar, correr, virar, pular sobre janelas, cair (pouso suave / cai de costas tonto)
			* escalar as bordas da tela, sentar, dormir enrolado, acordar, bocejar, espreguiçar
			* piscar, olhar ao redor, dar tchau, abanar o rabo, pisar forte, rugir, comemorar
			* comer carne, pizza, pipoca, beber, cupcake de aniversário
			* susto, choro, joinha, bravo (fumaça), óculos escuros, espirro, dança, apaixonado, cansado
			* ler, computador, celular, boxe, balão, fones, pesca, foto, pintura, violão,
			  bateria, cantar, malabares, regar uma flor, nadar, basquete, futebol, escrever
			* fantasias: detetive, mago, espião, cientista, chef, policial, explorador,
			  pedreiro, astronauta, super-herói
			* janelinhas independentes: ondas do rugido, fumaça, espirro, notas musicais,
			  formiga, coração, balão, bola de basquete, bola de futebol, avião de papel,
			  flor, bolhas
			""";

		Frame.NewHeadroom = 0;
		Frame.NewWidth = Frame.Size;
		return p;
	}
}
