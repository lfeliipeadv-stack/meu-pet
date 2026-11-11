using System.Drawing;

namespace PetGen;

/// <summary>
/// "Nuvi" — an original pixel-art cloud robot for DesktopPet: a puffy blue cloud head
/// with a dark terminal screen for a face (it "talks" with a >_ prompt), a small
/// round body with a >_ badge and stubby legs. Its expressions are screen glyphs.
/// Drawn on a 16-wide grid with 2 rows of headroom (18 rows in all).
/// </summary>
static class NuviPet
{
	const char Out = 'O', Blue = 'B', Light = 'L', Shade = 'D', Screen = 'S', Cyan = 'C', Badge = 'W', Red = 'R', Zz = 'Z';
	const char Yellow = 'Y', Green = 'G', DarkGreen = 'g', Pink = 'P', Drop = 'd', Coffee = 'b';
	const int Head = 2;   // headroom: art row r is frame y = r - Head

	// Front view. Art rows 0..17 (row 17 = feet). Screen interior: x 3..12, rows 3..7.
	static readonly string[] BaseArt =
	{
		"..OOO.OOOO.OOO..",   //  0 three cloud puffs
		".OLLLOLLLLOLLLO.",   //  1
		".OLBBBBBBBBBBBO.",   //  2
		"OLBSSSSSSSSSSBBO",   //  3 screen
		"OBBSSSSSSSSSSBBO",   //  4
		".OBSSSSSSSSSSBO.",   //  5 side puffs pinch in
		"OBBSSSSSSSSSSBDO",   //  6
		"OBBSSSSSSSSSSBDO",   //  7
		".OBBBBBBBBBBBDO.",   //  8
		"..OBBDOBBODBDO..",   //  9 lower puffs
		"...OOOOOOOOOO...",   // 10
		"....OLBBBBDO....",   // 11 body
		"...OBLBBBBDBO...",   // 12 arms
		"..OBOLBBBBDOBO..",   // 13
		"...OOLBBBBDOO...",   // 14
		"....OBDOOBDO....",   // 15 legs
		"....OBDOOBDO....",   // 16
		".....OO..OO.....",   // 17 feet
	};

	public static PetDef Build()
	{
		Frame.NewHeadroom = Head;
		var p = new PetDef
		{
			Folder = "nuvi",
			Title = "Nuvi the Cloud Terminal",
			PetName = "Nuvi",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Nuvi is a little blue cloud with a terminal screen for a face.[br]" +
				   "It types code, loads, throws the odd error and naps on your taskbar.[br]" +
				   "Original character — feel free to distribute.",
			Palette = new Dictionary<char, Color>
			{
				[Out] = Color.FromArgb(22, 34, 92),
				[Blue] = Color.FromArgb(66, 112, 226),
				[Light] = Color.FromArgb(124, 164, 246),
				[Shade] = Color.FromArgb(42, 76, 180),
				[Screen] = Color.FromArgb(24, 28, 54),
				[Cyan] = Color.FromArgb(128, 228, 236),
				[Badge] = Color.FromArgb(222, 232, 255),
				[Red] = Color.FromArgb(246, 96, 96),
				[Zz] = Color.FromArgb(206, 220, 255),
				[Yellow] = Color.FromArgb(252, 214, 72),
				[Green] = Color.FromArgb(96, 226, 124),
				[DarkGreen] = Color.FromArgb(36, 116, 66),
				[Pink] = Color.FromArgb(250, 108, 150),
				[Drop] = Color.FromArgb(150, 204, 255),
				[Coffee] = Color.FromArgb(126, 78, 46),
			},
			Scale = 3,
			TilesX = 8,
		};

		// ------------------------------------------------------------ art-row drawing
		void D(Frame f, int x, int r, char c) => f.Dot(x, r - Head, c);
		void R(Frame f, int x, int r, int w, int h, char c) => f.Rect(x, r - Head, w, h, c);
		char At(Frame f, int x, int r) => f[x, r - Head];

		Frame Base()
		{
			var f = new Frame();
			f.Stamp(0, -Head, BaseArt);
			// the little >_ badge on the belly
			D(f, 6, 12, Badge); D(f, 7, 13, Badge); D(f, 6, 14, Badge); D(f, 8, 14, Badge); D(f, 9, 14, Badge);
			return f;
		}

		void ClearScreen(Frame f) => R(f, 3, 3, 10, 5, Screen);

		// Screen faces. dx shifts the glyphs (looking left / right).
		void Prompt(Frame f, bool cursor = true, int dx = 0)
		{
			ClearScreen(f);
			R(f, 4 + dx, 4, 2, 1, Cyan); R(f, 5 + dx, 5, 2, 1, Cyan); R(f, 4 + dx, 6, 2, 1, Cyan);   // chunky >
			if (cursor) R(f, 8 + dx, 6, 3, 1, Cyan);
		}
		void Happy(Frame f)
		{
			ClearScreen(f);
			D(f, 4, 5, Cyan); D(f, 5, 4, Cyan); D(f, 6, 5, Cyan);
			D(f, 9, 5, Cyan); D(f, 10, 4, Cyan); D(f, 11, 5, Cyan);
		}
		void Closed(Frame f) { ClearScreen(f); R(f, 4, 5, 3, 1, Cyan); R(f, 9, 5, 3, 1, Cyan); }
		void Wide(Frame f) { ClearScreen(f); R(f, 4, 4, 2, 2, Cyan); R(f, 10, 4, 2, 2, Cyan); R(f, 7, 6, 2, 1, Cyan); }
		void Error(Frame f)
		{
			ClearScreen(f);
			foreach (int x in new[] { 4, 9 })
			{
				D(f, x, 4, Red); D(f, x + 2, 4, Red); D(f, x + 1, 5, Red); D(f, x, 6, Red); D(f, x + 2, 6, Red);
			}
		}
		void Question(Frame f)
		{
			ClearScreen(f);
			R(f, 6, 3, 3, 1, Cyan); D(f, 9, 4, Cyan); D(f, 8, 5, Cyan); D(f, 7, 5, Cyan); D(f, 7, 7, Cyan);
		}
		// "code" lines being typed: n = 1..4
		void Code(Frame f, int n)
		{
			ClearScreen(f);
			D(f, 4, 4, Cyan); D(f, 4, 6, Cyan);
			if (n >= 1) R(f, 6, 4, 3, 1, Badge);
			if (n >= 2) R(f, 10, 4, 2, 1, Cyan);
			if (n >= 3) R(f, 6, 6, 2, 1, Badge);
			if (n >= 4) R(f, 9, 6, 3, 1, Cyan);
			else D(f, n >= 3 ? 9 : 6, 6, Cyan);   // cursor
		}
		// progress bar: filled 0..8
		void Loading(Frame f, int filled)
		{
			ClearScreen(f);
			R(f, 4, 5, 8, 1, Shade);
			if (filled > 0) R(f, 4, 5, filled, 1, Cyan);
		}

		// legs: a lifted leg is one row shorter (its foot moves up)
		void Legs(Frame f, int liftL, int liftR)
		{
			R(f, 4, 15, 8, 3, '.');
			LegAt(f, 4, liftL);
			LegAt(f, 8, liftR);
		}
		void LegAt(Frame f, int x, int lift)
		{
			for (int r = 15; r <= 16 - lift; r++) { D(f, x, r, Out); D(f, x + 1, r, Blue); D(f, x + 2, r, Shade); D(f, x + 3, r, Out); }
			D(f, x + 1, 17 - lift, Out); D(f, x + 2, 17 - lift, Out);
		}

		// One arm (right side; mirror = left side). 0 down (as drawn), 1 up, 2 out.
		void Arm(Frame f, int pose, bool mirror)
		{
			int X(int x) => mirror ? 15 - x : x;
			if (pose == 0) return;
			// remove the hanging stub and close the body outline
			D(f, X(11), 12, Out); D(f, X(12), 12, '.'); D(f, X(12), 13, '.'); D(f, X(13), 13, '.'); D(f, X(12), 14, '.');
			if (pose == 1)
			{
				D(f, X(12), 12, Blue); D(f, X(13), 11, Blue); D(f, X(14), 10, Light);
				D(f, X(13), 12, Out); D(f, X(14), 11, Out); D(f, X(15), 10, Out); D(f, X(15), 9, Out); D(f, X(14), 9, Out);
			}
			else
			{
				D(f, X(12), 12, Blue); D(f, X(13), 12, Blue); D(f, X(14), 12, Light);
				foreach (int x in new[] { 12, 13, 14 }) { D(f, X(x), 11, Out); D(f, X(x), 13, Out); }
				D(f, X(15), 12, Out);
			}
		}
		void Arms(Frame f, int right, int left = 0) { Arm(f, right, false); Arm(f, left, true); }

		// Squash: head and body drop n rows; the legs get shorter (crouch, landing, sitting).
		Frame Squash(Frame f, int n)
		{
			var top = f.Clone();
			R(f, 0, 0, 16, 18, '.');
			for (int r = 0; r <= 14; r++) for (int x = 0; x < 16; x++)
			{
				char c = At(top, x, r);
				if (c != '.' && r + n <= 16) D(f, x, r + n, c);
			}
			for (int r = 15 + n; r <= 16; r++) { D(f, 4, r, Out); D(f, 5, r, Blue); D(f, 6, r, Shade); D(f, 7, r, Out); D(f, 8, r, Out); D(f, 9, r, Blue); D(f, 10, r, Shade); D(f, 11, r, Out); }
			D(f, 5, 17, Out); D(f, 6, 17, Out); D(f, 9, 17, Out); D(f, 10, 17, Out);
			return f;
		}

		// ------------------------------------------------------------ frames
		var st = Base(); Prompt(st); int fStand = p.Add(st);
		var bl = Base(); Prompt(bl, cursor: false); int fBlink = p.Add(bl);          // cursor blink
		var hp = Base(); Happy(hp); int fHappy = p.Add(hp);
		var cl = Base(); Closed(cl); int fClosed = p.Add(cl);

		// walk: legs alternate, cursor blinks along
		int[] walk = new int[4];
		for (int k = 0; k < 4; k++)
		{
			var w = Base(); Prompt(w, cursor: k < 2);
			Legs(w, k == 1 ? 1 : 0, k == 3 ? 1 : 0);
			walk[k] = p.Add(w);
		}

		// run: arms out, legs blur, glyphs lean into the direction of travel (left)
		var r1 = Base(); Prompt(r1, dx: -1); Arms(r1, 2, 2); Legs(r1, 1, 0); int fRun1 = p.Add(r1);
		var r2 = Base(); Prompt(r2, dx: -1); Arms(r2, 2, 2); Legs(r2, 0, 1); int fRun2 = p.Add(r2);

		// crouch / jump / fall / land
		var cr = Base(); Closed(cr); Squash(cr, 2); int fCrouch = p.Add(cr);
		var jp = Base(); Happy(jp); Arms(jp, 1, 1); Legs(jp, 1, 1); int fJump = p.Add(jp);
		var fa = Base(); Wide(fa); Arms(fa, 1, 1); int fFall = p.Add(fa);
		var ld = Base(); Closed(ld); Squash(ld, 3); int fLand = p.Add(ld);

		// sleep: screen dims to - -, z's float up the top-right corner
		var s1 = Base(); Closed(s1); Squash(s1, 1); int fSl1 = p.Add(s1);
		var s2 = s1.Clone(); D(s2, 14, 1, Zz); D(s2, 15, 0, Zz); int fSl2 = p.Add(s2);
		var s3 = Base(); Closed(s3); Squash(s3, 2); D(s3, 14, 1, Zz); D(s3, 15, 0, Zz); D(s3, 13, -1, Zz); D(s3, 14, -2, Zz); D(s3, 13, -2, Zz); int fSl3 = p.Add(s3);
		var sit = Base(); Prompt(sit); Squash(sit, 1); int fSit = p.Add(sit);

		// wave
		var wv1 = Base(); Happy(wv1); Arms(wv1, 1); int fWave1 = p.Add(wv1);
		var wv2 = Base(); Happy(wv2); Arms(wv2, 2); int fWave2 = p.Add(wv2);

		// look left / right
		var lL = Base(); Prompt(lL, dx: -1); int fLookL = p.Add(lL);
		var lR = Base(); Prompt(lR, dx: 1); int fLookR = p.Add(lR);

		// think
		var q = Base(); Question(q); int fThink = p.Add(q);

		// typing code (both hands out on a keyboard)
		int[] code = new int[4];
		for (int n = 1; n <= 4; n++) { var c = Base(); Code(c, n); Arms(c, 2, 2); code[n - 1] = p.Add(c); }

		// loading bar
		int[] load = new int[4];
		for (int n = 0; n < 4; n++) { var c = Base(); Loading(c, n * 2 + 2); load[n] = p.Add(c); }

		// error glitch
		var e1 = Base(); Error(e1); int fErr1 = p.Add(e1);
		var e2 = Base(); Error(e2); R(e2, 3, 5, 10, 1, Red); R(e2, 3, 5, 3, 1, Screen); Arms(e2, 1, 1); int fErr2 = p.Add(e2);

		// drag (legs dangle) / toss
		var d1 = Base(); Wide(d1); Arms(d1, 1, 1); Legs(d1, 0, 1); int fDrag1 = p.Add(d1);
		var d2 = Base(); Wide(d2); Arms(d2, 1, 1); Legs(d2, 1, 0); int fDrag2 = p.Add(d2);

		// shutdown: x x, then screen off, sinking
		var k1 = Base(); Error(k1); Squash(k1, 2); int fDie1 = p.Add(k1);
		var k2 = Base(); ClearScreen(k2); R(k2, 7, 5, 2, 1, Cyan); Squash(k2, 4); int fDie2 = p.Add(k2);

		// ============================================================ extra frames (animations 25..44)
		// A glyph on the screen: '#' cells get colour c; clipped to the screen (x 3..12, rows 3..7).
		void Glyph(Frame f, int x, int r, char c, params string[] rows)
		{
			for (int j = 0; j < rows.Length; j++)
				for (int i = 0; i < rows[j].Length; i++)
				{
					int gx = x + i, gr = r + j;
					if (rows[j][i] == '#' && gx >= 3 && gx <= 12 && gr >= 3 && gr <= 7) D(f, gx, gr, c);
				}
		}
		Frame Blank() { var f = Base(); ClearScreen(f); return f; }

		// 25 rain: calm face, drops fall beside the body
		int[] rain = new int[3];
		var drops = new[] { (0, 11), (15, 12), (1, 14), (14, 16), (0, 17) };
		for (int k = 0; k < 3; k++)
		{
			var f = Base(); Closed(f);
			foreach (var (x, r0) in drops)
			{
				int r = 11 + (r0 - 11 + k * 2) % 7;
				D(f, x, r, Drop); if (r + 1 <= 17) D(f, x, r + 1, Drop);
			}
			rain[k] = p.Add(f);
		}

		// 26 thunder: a bolt on the screen, then an inverted flash
		string[] bolt = { "  ##", " ## ", "####", " ## ", "##  " };
		var tb = Blank(); Glyph(tb, 5, 3, Yellow, bolt); int fBolt = p.Add(tb);
		var tf = Base(); R(tf, 3, 3, 10, 5, Yellow); Glyph(tf, 5, 3, Screen, bolt); Arms(tf, 1, 1); int fFlash = p.Add(tf);

		// 27 love: heart eyes, then a beating heart
		var hEyes = Blank(); Glyph(hEyes, 4, 4, Pink, "#.#", "###", ".#."); Glyph(hEyes, 9, 4, Pink, "#.#", "###", ".#."); int fHeartEyes = p.Add(hEyes);
		var hBig = Blank(); Glyph(hBig, 5, 3, Pink, ".##.##.", "#######", "#######", ".#####.", "..###.."); int fHeartBig = p.Add(hBig);
		var hSmall = Blank(); Glyph(hSmall, 6, 4, Pink, "##.##", "#####", ".###.", "..#.."); int fHeartSmall = p.Add(hSmall);

		// 28 dance: arms swap, a music note hops from side to side
		var dn1 = Base(); Happy(dn1); Arms(dn1, 0, 1); D(dn1, 15, 10, Cyan); D(dn1, 15, 11, Cyan); D(dn1, 15, 12, Cyan); D(dn1, 14, 12, Cyan); D(dn1, 14, 13, Cyan); int fDance1 = p.Add(dn1);
		var dn2 = Base(); Happy(dn2); Arms(dn2, 1, 0); Squash(dn2, 1); D(dn2, 1, 10, Cyan); D(dn2, 1, 11, Cyan); D(dn2, 1, 12, Cyan); D(dn2, 0, 12, Cyan); D(dn2, 0, 13, Cyan); int fDance2 = p.Add(dn2);

		// 29 coffee: holds a little cup, sips, steam rises
		Frame Cup(Action<Frame> face, int steam)
		{
			var f = Base(); face(f); Arms(f, 2);
			D(f, 14, 10, Badge); D(f, 15, 10, Coffee); D(f, 14, 11, Badge); D(f, 15, 11, Badge);
			if (steam == 1) D(f, 15, 9, Zz);
			if (steam == 2) D(f, 14, 9, Zz);
			return f;
		}
		int fCup1 = p.Add(Cup(f => Prompt(f), 1));
		int fCup2 = p.Add(Cup(Closed, 2));
		int fCup3 = p.Add(Cup(Happy, 1));

		// 30 download / 31 upload: an arrow slides through the screen
		string[] down = { "  ##  ", "  ##  ", "######", " #### ", "  ##  " };
		string[] up = { "  ##  ", " #### ", "######", "  ##  ", "  ##  " };
		int[] dl = new int[3], ul = new int[3];
		for (int k = 0; k < 3; k++)
		{
			var a = Blank(); Glyph(a, 5, 1 + k * 2, Cyan, down); dl[k] = p.Add(a);
			var b = Blank(); Glyph(b, 5, 5 - k * 2, Cyan, up); ul[k] = p.Add(b);
		}
		var ok = Blank(); Glyph(ok, 4, 3, Green, ".......#", "......#.", "#....#..", ".#..#...", "..##...."); int fCheck = p.Add(ok);

		// 32 wifi: signal arcs light up one by one
		int[] wifi = new int[3];
		for (int k = 0; k < 3; k++)
		{
			var f = Blank(); R(f, 7, 7, 2, 1, Cyan);
			if (k >= 1) { R(f, 6, 5, 4, 1, Cyan); D(f, 5, 6, Cyan); D(f, 10, 6, Cyan); }
			if (k >= 2) { R(f, 5, 3, 6, 1, Cyan); D(f, 4, 4, Cyan); D(f, 11, 4, Cyan); }
			wifi[k] = p.Add(f);
		}

		// 33 matrix: green code rain on the screen
		int[] matrix = new int[3];
		for (int k = 0; k < 3; k++)
		{
			var f = Blank();
			for (int i = 0; i < 5; i++)
			{
				int x = 4 + i * 2, r = 3 + (i * 3 + k * 2) % 6;
				if (r <= 7) D(f, x, r, Green);
				if (r - 1 >= 3 && r - 1 <= 7) D(f, x, r - 1, DarkGreen);
				if (r - 2 >= 3 && r - 2 <= 7) D(f, x, r - 2, DarkGreen);
			}
			matrix[k] = p.Add(f);
		}

		// 34 hello: "HI" on the screen while waving
		string[] hi = { "#.#.###", "#.#..#.", "###..#.", "#.#..#.", "#.#.###" };
		var hi1 = Blank(); Glyph(hi1, 5, 3, Cyan, hi); Arms(hi1, 1); int fHi1 = p.Add(hi1);
		var hi2 = Blank(); Glyph(hi2, 5, 3, Cyan, hi); Arms(hi2, 2); int fHi2 = p.Add(hi2);

		// 35 static: TV noise
		int[] noise = new int[2];
		for (int k = 0; k < 2; k++)
		{
			var f = Blank();
			char[] cols = { Badge, Shade, Screen, Cyan, Shade };
			for (int r = 3; r <= 7; r++) for (int x = 3; x <= 12; x++) {
				uint h = (uint)(x * 73856093 ^ r * 19349663 ^ (k + 1) * 83492791);
				h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
				D(f, x, r, cols[h % 5]);
			}
			noise[k] = p.Add(f);
		}

		// 36 stretch and yawn
		var str = Base(); Closed(str); Arms(str, 1, 1); int fStretch = p.Add(str);
		var yawn = Base(); Closed(yawn); R(yawn, 7, 6, 2, 2, Cyan); Arms(yawn, 1, 1); int fYawn = p.Add(yawn);

		// 37 dizzy: spiral eyes
		var dz = Blank(); Glyph(dz, 4, 4, Cyan, "###", "#.#", "##."); Glyph(dz, 9, 4, Cyan, "###", "#.#", "##."); int fDizzy = p.Add(dz);
		var dzL = p.Frames[fDizzy].Clone().Shift(-1, 0); int fDizzyL = p.Add(dzL);
		var dzR = p.Frames[fDizzy].Clone().Shift(1, 0); int fDizzyR = p.Add(dzR);

		// 38 battery low: the charge drops, then it naps
		Frame Battery(int level, char c)
		{
			var f = Blank();
			Glyph(f, 4, 4, Badge, "#######", "#.....##", "#######");
			if (level > 0) R(f, 5, 5, level, 1, c);
			return f;
		}
		int fBat5 = p.Add(Battery(5, Green)), fBat3 = p.Add(Battery(3, Yellow)), fBat1 = p.Add(Battery(1, Red)), fBat0 = p.Add(Battery(0, Red));

		// 39 bug hunt: a little bug crawls across the screen, gets squashed
		int[] bug = new int[4];
		for (int k = 0; k < 4; k++)
		{
			var f = Blank(); int x = 10 - k * 2;
			R(f, x, 5, 2, 2, Red); D(f, x, 5, Badge); D(f, x - 1, 6 + (k % 2), Shade); D(f, x + 2, 6 + ((k + 1) % 2), Shade);
			bug[k] = p.Add(f);
		}

		// 40 idea: a yellow "!"
		var idea = Blank(); R(idea, 7, 3, 2, 3, Yellow); R(idea, 7, 7, 2, 1, Yellow); int fIdea = p.Add(idea);

		// 41 compile: a spinner
		var ring = new[] { (7, 4), (8, 4), (9, 5), (9, 6), (8, 7), (7, 7), (6, 6), (6, 5) };
		int[] spin = new int[4];
		for (int k = 0; k < 4; k++)
		{
			var f = Blank();
			foreach (var (x, r) in ring) D(f, x, r, Shade);
			D(f, ring[k * 2].Item1, ring[k * 2].Item2, Cyan); D(f, ring[k * 2 + 1].Item1, ring[k * 2 + 1].Item2, Cyan);
			spin[k] = p.Add(f);
		}

		// 42 wink
		var wk = Blank(); D(wk, 4, 5, Cyan); D(wk, 5, 4, Cyan); D(wk, 6, 5, Cyan); R(wk, 9, 5, 3, 1, Cyan); Arms(wk, 2); int fWink = p.Add(wk);

		// 44 hourglass: the sand runs down
		int[] glass = new int[3];
		for (int k = 0; k < 3; k++)
		{
			var f = Blank();
			Glyph(f, 6, 3, Badge, "####", "#..#", ".##.", "#..#", "####");
			if (k == 0) R(f, 7, 4, 2, 1, Yellow);
			if (k == 1) { D(f, 7, 4, Yellow); D(f, 8, 6, Yellow); }
			if (k == 2) R(f, 7, 6, 2, 1, Yellow);
			glass[k] = p.Add(f);
		}

		p.IconFrame = fStand;

		// ------------------------------------------------------------ spawns
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 45, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 10, "0", "areaH-imageH", 2));

		// ------------------------------------------------------------ animations
		p.A(1, "walk").Move("-3", "0").Speed("180").Loop("random/4+8")
			.Seq(walk[0], walk[1], walk[2], walk[3])
			.Then(50, 1).Then(10, 2).Then(8, 10).Then(6, 15).Then(5, 17).Then(6, 18).Then(5, 21).Then(4, 22).Then(4, 23)
			.Then(6, 16, "taskbar")
			// extra idle animations (25..44), each a small chance
			.Then(2, 25).Then(1, 26).Then(2, 27).Then(2, 28).Then(2, 29).Then(2, 30).Then(2, 31).Then(2, 32).Then(2, 33).Then(2, 34)
			.Then(1, 35).Then(2, 36).Then(1, 37).Then(1, 38).Then(2, 39).Then(2, 40).Then(2, 41).Then(2, 42).Then(2, 43).Then(2, 44)
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window").OnBorder(40, 19, "windowSide")
			.OnGravity(100, 5);

		p.A(2, "turn").Move("0", "0").Speed("130").Seq(fLookR, fStand).Then(100, 1); p.Anims.Last().Action = "flip";

		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("40").Loop("30").Seq(fJump, fFall, fLand, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fFall).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("130", "200").Seq(fLand, fLand, fCrouch, fStand, fStand).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("160", "270").Seq(fLand, fErr1, fErr2, fErr1, fLand, fStand).Then(80, 1).Then(20, 13);

		p.A(9, "jump").Move("-5", "-8", "-5", "8").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1).OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window");

		p.A(10, "sync").Move("0", "0").Speed("100", "160").Loop("6").Seq(fHappy, fWave1, fHappy, fJump).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("500").Seq(fStand, fBlink, fStand, fBlink, fStand).Then(70, 1).Then(15, 24).Then(8, 42).Then(7, 40);

		p.A(12, "climb").Move("0", "-3").Speed("160").Loop("5000", 2)
			.Seq(fJump, fDrag1, fJump, fDrag2).Then(100, 12).OnBorder(100, 2);

		p.A(13, "sleep").Move("0", "0").Speed("1000", "500").Loop("random/4+8", 2)
			.Seq(fSit, fSl1, fSl1, fSl2, fSl3, fSl2).Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("220", "180")
			.Seq(fSl1, load[0], load[1], load[2], load[3], fHappy, fStand).Then(100, 1);

		p.A(15, "look around").Move("0", "0").Speed("350", "260").Seq(fStand, fLookL, fLookR, fBlink, fStand).Then(100, 1);
		p.A(16, "sit").Move("0", "0").Speed("800", "600").Loop("random/3+5", 2).Seq(fSit, fSit, fClosed, fSit).Then(50, 1).Then(30, 13).Then(6, 29).Then(6, 25).Then(4, 38).Then(4, 44);
		p.A(17, "wave").Move("0", "0").Speed("190").Loop("3", 1).Seq(fHappy, fWave1, fWave2, fWave1).Then(100, 1);
		p.A(18, "code").Move("0", "0").Speed("220").Loop("2")
			.Seq(code[0], code[1], code[2], code[3], code[3], fHappy).Then(100, 1);
		p.A(19, "look side").Move("0", "0").Speed("220", "300").Seq(fStand, fLookR, fBlink, fStand).Then(60, 2).Then(40, 9);

		p.A(20, "run").Move("-7", "0").Speed("90").Loop("random/3+10").Seq(fRun1, fRun2)
			.Then(70, 1).Then(30, 20).OnBorder(100, 2).OnGravity(100, 5);

		p.A(21, "loading").Move("0", "0").Speed("260").Seq(fBlink, load[0], load[1], load[2], load[3], load[3], fHappy, fStand).Then(100, 1);
		p.A(22, "error").Move("0", "0").Speed("120", "220").Seq(fErr1, fErr2, fErr1, fErr2, fErr1, fBlink, fStand).Then(100, 1);
		p.A(23, "happy").Move("0", "0").Speed("200").Loop("2").Seq(fHappy, fJump, fHappy, fStand).Then(100, 1);

		p.A(24, "think").Move("0", "0").Speed("380").Loop("3", 1).Seq(fStand, fThink, fBlink, fThink).Then(100, 1);

		// ------------------------------------------------------------ extra animations
		p.A(25, "rain").Move("0", "0").Speed("160").Loop("6").Seq(rain[0], rain[1], rain[2]).Then(100, 1);
		p.A(26, "thunder").Move("0", "0").Speed("110", "200").Seq(fFall, fBolt, fFlash, fBolt, fFlash, fFall, fStand).Then(60, 25).Then(40, 1);
		p.A(27, "love").Move("0", "0").Speed("240").Loop("3", 1).Seq(fHeartEyes, fHeartBig, fHeartSmall, fHeartBig).Then(100, 23);
		p.A(28, "dance").Move("0", "0").Speed("230").Loop("5").Seq(fDance1, fDance2).Then(80, 1).Then(20, 23);
		p.A(29, "coffee").Move("0", "0").Speed("500", "350").Loop("2", 1).Seq(fCup1, fCup2, fCup3, fCup2).Then(100, 1);
		p.A(30, "download").Move("0", "0").Speed("160").Loop("3", 0).Seq(dl[0], dl[1], dl[2]).Then(100, 43);
		p.A(31, "upload").Move("0", "0").Speed("160").Loop("3", 0).Seq(ul[0], ul[1], ul[2]).Then(100, 43);
		p.A(32, "wifi").Move("0", "0").Speed("300").Loop("3").Seq(wifi[0], wifi[1], wifi[2], wifi[2]).Then(60, 30).Then(40, 1);
		p.A(33, "matrix").Move("0", "0").Speed("150").Loop("6").Seq(matrix[0], matrix[1], matrix[2]).Then(100, 1);
		p.A(34, "hello").Move("0", "0").Speed("260").Loop("3").Seq(fHi1, fHi2).Then(100, 1);
		p.A(35, "static").Move("0", "0").Speed("80").Loop("8").Seq(noise[0], noise[1]).Then(60, 1).Then(40, 22);
		p.A(36, "stretch").Move("0", "0").Speed("420", "260").Seq(fClosed, fStretch, fYawn, fYawn, fStretch, fHappy, fStand).Then(100, 1);
		p.A(37, "dizzy").Move("0", "0").Speed("90", "260").Seq(fLookL, fLookR, fLookL, fLookR, fLookL, fLookR, fDizzy, fDizzyL, fDizzy, fDizzyR, fDizzy, fStand).Then(100, 1);
		p.A(38, "battery low").Move("0", "0").Speed("500").Seq(fBat5, fBat3, fBat1, fBat0, fBat1, fBat0, fClosed).Then(100, 13);
		p.A(39, "bug hunt").Move("0", "0").Speed("260").Seq(fStand, bug[0], bug[1], bug[2], bug[3], fFall, fCrouch, fCheck, fHappy).Then(100, 1);
		p.A(40, "idea").Move("0", "0").Speed("260").Seq(fBlink, fIdea, fJump, fIdea, fHappy).Then(100, 18);
		p.A(41, "compile").Move("0", "0").Speed("120").Loop("5").Seq(spin[0], spin[1], spin[2], spin[3]).Then(85, 43).Then(15, 22);
		p.A(42, "wink").Move("0", "0").Speed("380").Seq(fHappy, fWink, fWink, fHappy, fStand).Then(100, 1);
		p.A(43, "commit").Move("0", "0").Speed("320").Seq(code[3], spin[0], spin[2], fCheck, fCheck, fHappy, fStand).Then(100, 1);
		p.A(44, "hourglass").Move("0", "0").Speed("420").Loop("3").Seq(glass[0], glass[1], glass[2]).Then(70, 1).Then(30, 16);

		p.A(99, "kill").Move("0", "1").Speed("130", "230").Fade(1, 0).Seq(fErr1, fDie1, fDie1, fDie2, fDie2).Then(100, 99);

		p.Readme = """
			# Nuvi the Cloud Terminal

			## Description

			A little blue cloud robot with a terminal screen for a face. It talks with a
			blinking >_ prompt, types code, shows a loading bar, throws the odd error and
			naps on your taskbar. Completely original — feel free to distribute, modify,
			or use it in your own projects.

			## About pet

			Author: Luan Felipe

			Original character. No existing mascot or sprite was used as a base.
			Licensed under the MIT License — see [LICENSE](../LICENSE.md).

			## Animations

			* walk, run, turn
			* jump onto windows, fall (soft / hard landings with an error glitch)
			* climb screen borders
			* blinking cursor, look around, think (?), wave, happy hop
			* type code, loading bar, error x x
			* sit on the taskbar, sleep and boot up
			* drag and toss with the mouse
			* rain, thunder, love (heart), dance, coffee break
			* download, upload, wifi, matrix code rain, "HI" hello, TV static
			* stretch and yawn, dizzy spin, battery low, bug hunt, idea (!)
			* compile spinner, wink, commit ✓, hourglass
			""";
		p.ReadmePt = """
			# Nuvi a Nuvem Terminal

			## Descrição

			Um robozinho em forma de nuvem azul com uma tela de terminal no lugar do
			rosto. Ele "fala" com um prompt >_ piscando, digita código, mostra uma barra
			de carregamento, dá um erro de vez em quando e cochila na barra de tarefas.
			Personagem 100 % original — pode distribuir, modificar e usar à vontade.

			## Sobre o pet

			Autor: Luan Felipe

			Personagem original. Nenhum mascote ou sprite existente foi usado de base.
			Licenciado sob a Licença MIT — veja [LICENSE](../LICENSE.md).

			## Animações

			* andar, correr, virar
			* pular sobre janelas, cair (pouso suave / brusco com tela de erro)
			* escalar as bordas da tela
			* cursor piscando, olhar ao redor, pensar (?), dar tchau, pulinho feliz
			* digitar código, barra de carregamento, erro x x
			* sentar na barra de tarefas, dormir e reiniciar
			* arrastar e jogar com o mouse
			* chuva, trovão, amor (coração), dança, pausa para o café
			* download, upload, wi-fi, chuva de código (matrix), "HI" de olá, tela chiando
			* espreguiçar e bocejar, tontura, bateria fraca, caça ao bug, ideia (!)
			* compilando (spinner), piscadinha, commit ✓, ampulheta
			""";

		return p;
	}
}
