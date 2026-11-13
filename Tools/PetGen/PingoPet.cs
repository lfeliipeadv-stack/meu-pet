using System.Drawing;

namespace PetGen;

/// <summary>
/// "Pingo" — a pixel-art penguin for DesktopPet. The standing pose is a pixel-exact copy of the
/// reference art the user provided (22 x 25 pixels, read cell by cell from the image); every
/// other pose is derived from it (blink, waddle, raised flippers, lying on the belly...).
/// Frames are 26 px wide with 6 rows of headroom; the feet stand on row 25.
/// </summary>
static class PingoPet
{
	const int W = 26, Head = 6;   // 6 rows above the head for hats, balloons, umbrellas...
	const char Out = 'K', Eye = 'E', Gray = 'D', Light = 'L', White = 'W';
	const char Or = 'O', Or2 = 'M', Or3 = 'R', Or4 = 'X';
	const char Snow = 'S', Fish = 'F', FishLight = 'f', Zz = 'Z', Blush = 'B', Spark = 'Y', Water = 'w';
	const char Heart = 'H', HeartLight = 'h', Blue = 'U', Ice = 'u', Green = 'G', Purple = 'V', PurpleLight = 'v';
	const char Pink = 'P', PinkDark = 'p', Wood = 'N', WoodDark = 'n', SparkDark = 'y', ChickY = 'C', ChickO = 'c';

	// The reference penguin, exactly as in the user's image ('.' = transparent).
	static readonly string[] Art =
	{
		".......KKKKKKK........",   //  0
		".....KDDDDDDDDKK......",   //  1
		"....DDDDDDDDDDDDK.....",   //  2
		"....DLWWDDDDWWLDK.....",   //  3
		"...DLWWWWDDWWWWLKK....",   //  4
		"...DWWWWWDDWWWWWDK....",   //  5
		"...DWWEWWDDWEWWWDK....",   //  6 eyes
		"...DWWEWOOMWEWWWDK....",   //  7 eyes + beak
		"...DWWWMMMRWWWWWDK....",   //  8 beak
		"...DWWWWWWWWWWWLKK....",   //  9
		"..KDDWWWWWWWWWWKKDK...",   // 10
		"..KDWWWWWWWWWWLDDDDK..",   // 11
		".DDDWWWWWWWWWWWWDDDK..",   // 12 flippers
		".DKWWWWWWWWWWWWWDDDDK.",   // 13
		"DDKWWWWWWWWWWWWWLKDDK.",   // 14
		"DDKWWWWWWWWWWWWWLKDDDK",   // 15
		"DKKWWWWWWWWWWWWWLKKDDK",   // 16
		"K.KWWWWWWWWWWWWWLKK.KK",   // 17
		"..KWWWWWWWWWWWWWLDK...",   // 18
		"..KLWWWWWWWWWWWLDDK...",   // 19
		"...KWWWWWWWWWWLLKKK...",   // 20
		"...KLLLLWWWWLLLDKK....",   // 21
		"....OORLLLLLLOORK.....",   // 22 feet
		"...OOORRKKKKOOORR.....",   // 23
		"...RRRRX....RRRRX.....",   // 24
	};
	const int AX = 2, AY = 1;   // where the art sits in the frame (feet on row 25)

	public static PetDef Build()
	{
		Frame.NewHeadroom = Head;
		Frame.NewWidth = W;
		var p = new PetDef
		{
			Folder = "pingo",
			Title = "Pingo the Penguin",
			PetName = "Pingo",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Pingo is a chubby little pixel penguin.[br]" +
				   "He waddles along your taskbar, flaps his flippers, slides on his belly,[br]" +
				   "eats fish and naps in the snow.",
			Palette = new Dictionary<char, Color>
			{
				[Out] = Color.FromArgb(30, 30, 30),
				[Eye] = Color.FromArgb(2, 2, 2),
				[Gray] = Color.FromArgb(68, 68, 68),
				[Light] = Color.FromArgb(205, 205, 205),
				[White] = Color.FromArgb(255, 255, 255),
				[Or] = Color.FromArgb(247, 130, 52),
				[Or2] = Color.FromArgb(232, 106, 54),
				[Or3] = Color.FromArgb(210, 82, 48),
				[Or4] = Color.FromArgb(195, 58, 50),
				[Snow] = Color.FromArgb(150, 196, 240),
				[Fish] = Color.FromArgb(84, 136, 204),
				[FishLight] = Color.FromArgb(156, 196, 238),
				[Zz] = Color.FromArgb(150, 170, 240),
				[Blush] = Color.FromArgb(248, 172, 182),
				[Spark] = Color.FromArgb(255, 214, 70),
				[Water] = Color.FromArgb(120, 190, 240),
				[Heart] = Color.FromArgb(228, 62, 88), [HeartLight] = Color.FromArgb(255, 150, 170),
				[Blue] = Color.FromArgb(66, 112, 204), [Ice] = Color.FromArgb(178, 222, 250),
				[Green] = Color.FromArgb(90, 186, 90), [Purple] = Color.FromArgb(124, 84, 200), [PurpleLight] = Color.FromArgb(186, 156, 246),
				[Pink] = Color.FromArgb(250, 150, 200), [PinkDark] = Color.FromArgb(220, 96, 160),
				[Wood] = Color.FromArgb(160, 104, 56), [WoodDark] = Color.FromArgb(110, 68, 36), [SparkDark] = Color.FromArgb(214, 160, 30),
				[ChickY] = Color.FromArgb(255, 216, 72), [ChickO] = Color.FromArgb(240, 170, 40),
			},
			Scale = 2,
			TilesX = 8,
		};

		// ------------------------------------------------------------ helpers
		Frame Base()
		{
			var f = new Frame();
			f.Stamp(AX, AY, Art.Select(r => r.Replace('.', ' ')).ToArray());
			return f;
		}
		// art coordinates -> frame coordinates
		int X(int c) => c + AX;
		int Y(int r) => r + AY;

		// eyes: 0 open, 1 closed, 2 happy ^ ^, 3 look left, 4 look right, 5 wide, 6 squeezed > <
		void Eyes(Frame f, int mode)
		{
			foreach (int c in new[] { 6, 12 }) { f.Dot(X(c), Y(6), White).Dot(X(c), Y(7), White); }
			foreach (int c in new[] { 6, 12 })
			{
				int x = X(c);
				switch (mode)
				{
					case 0: f.Dot(x, Y(6), Eye).Dot(x, Y(7), Eye); break;
					case 1: f.Dot(x, Y(7), Eye).Dot(x - 1, Y(7), Gray).Dot(x - 1, Y(7), Eye); break;
					case 2: f.Dot(x - 1, Y(7), Eye).Dot(x, Y(6), Eye).Dot(x + 1, Y(7), Eye); break;
					case 3: f.Dot(x - 1, Y(6), Eye).Dot(x - 1, Y(7), Eye); break;
					case 4: f.Dot(x + 1, Y(6), Eye).Dot(x + 1, Y(7), Eye); break;
					case 5: f.Dot(x, Y(5), Eye).Dot(x, Y(6), Eye).Dot(x, Y(7), Eye); break;
					case 6: f.Dot(x - (c == 6 ? 1 : -1), Y(5), Eye).Dot(x, Y(6), Eye).Dot(x - (c == 6 ? 1 : -1), Y(7), Eye); break;
				}
			}
			if (mode == 1) { f.Dot(X(5), Y(7), White).Dot(X(11), Y(7), White); f.Dot(X(5), Y(7), Eye).Dot(X(11), Y(7), Eye); }
		}
		void Cheeks(Frame f) { f.Dot(X(5), Y(8), Blush).Dot(X(14), Y(8), Blush); }
		// beak: 0 closed (as drawn), 1 open, 2 wide open
		void Beak(Frame f, int mode)
		{
			if (mode == 0) return;
			f.Rect(X(7), Y(8), 4, 1, Out).Rect(X(8), Y(9), 3, 1, Or3);
			if (mode == 2) f.Rect(X(8), Y(9), 3, 1, Out).Rect(X(8), Y(10), 3, 1, Or3);
		}
		// remove the flippers that stick out, and re-outline the body where they were
		void NoWings(Frame f, bool left = true, bool right = true)
		{
			var gone = new List<(int x, int y)>();
			for (int r = 10; r <= 17; r++)
				for (int c = 0; c < 22; c++)
				{
					bool l = left && c <= 1 && r >= 12;
					bool rr = right && (c >= 19 || (c == 18 && r >= 13));
					if (l || rr) { if (f[X(c), Y(r)] != '.') gone.Add((X(c), Y(r))); f[X(c), Y(r)] = '.'; }
				}
			foreach (var (x, y) in gone)
				foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
				{
					char ch = f[x + dx, y + dy];
					if (ch == Gray || ch == White || ch == Light) f[x + dx, y + dy] = Out;
				}
		}
		// flipper poses: 0 down (as drawn), 1 half up, 2 raised high
		void Wing(Frame f, bool left, int pose)
		{
			if (pose == 0) return;
			NoWings(f, left, !left);
			string[] up = pose == 2
				? new[] { "K....", "DK...", "DDK..", "KDDK.", ".KDDK", "..KDD", "...KD" }
				: new[] { ".....", ".....", "KK...", "DDKK.", "DDDDK", ".KKDD", "...KD" };
			var rows = left ? up : up.Select(r => new string(r.Reverse().ToArray())).ToArray();
			f.Stamp(left ? 0 : 21, 6 + (pose == 1 ? 1 : 0), rows.Select(r => r.Replace('.', ' ')).ToArray());
		}
		// lift one foot (waddling)
		void LiftFoot(Frame f, bool left)
		{
			int x0 = left ? X(3) : X(12), x1 = left ? X(7) : X(16);
			var src = f.Clone();
			for (int y = Y(22); y <= Y(24); y++) for (int x = x0; x <= x1; x++) f[x, y] = y == Y(22) ? src[x, y] : '.';
			for (int y = Y(22); y <= Y(24); y++) for (int x = x0; x <= x1; x++) if (src[x, y] != '.' && y > Y(22)) f[x, y - 1] = src[x, y];
		}
		// tilt: rows above `belowY` shift sideways
		Frame Lean(Frame f, int dx, int belowY)
		{
			var src = f.Clone();
			for (int y = f.MinY; y < belowY; y++) { for (int x = 0; x < W; x++) f[x, y] = '.'; for (int x = 0; x < W; x++) if (src[x, y] != '.') f[x + dx, y] = src[x, y]; }
			return f;
		}
		// squash: everything above the feet drops n rows (crouch / landing / sitting)
		Frame Squash(Frame f, int n)
		{
			var src = f.Clone();
			for (int y = f.MinY; y < Y(22); y++) for (int x = 0; x < W; x++) f[x, y] = '.';
			for (int y = f.MinY; y < Y(22) - n; y++) for (int x = 0; x < W; x++) if (src[x, y] != '.') f[x, y + n] = src[x, y];
			for (int y = Y(22) - n; y < Y(22); y++) for (int x = 0; x < W; x++) if (src[x, y] != '.' && f[x, y] == '.') f[x, y] = src[x, y];
			return f;
		}
		Frame Up(Frame f, int n) => f.Shift(0, -n);
		// lying on the belly, head first (the art turned 90 degrees)
		Frame Lying(int eyes, bool puff, int bob)
		{
			var b = Base(); Eyes(b, eyes);
			var f = new Frame();
			for (int r = 0; r < 25; r++)
				for (int c = 0; c < 22; c++)
				{
					char ch = b[X(c), Y(r)];
					if (ch == '.') continue;
					f[r, 25 - 21 + c - bob] = ch;   // row -> x (head on the left), column -> y
				}
			if (puff) f.Dot(24, 23, Snow).Dot(25, 21, Snow).Dot(25, 24, Snow).Dot(23, 25, Snow);
			return f;
		}
		void FishAt(Frame f, int x, int y, bool flip = false)
		{
			string[] fish = { ".FFF.F", "FfFFFF", ".FFF.F" };
			if (flip) fish = fish.Select(r => new string(r.Reverse().ToArray())).ToArray();
			f.Stamp(x, y, fish.Select(r => r.Replace('.', ' ')).ToArray());
		}
		void Zed(Frame f, int x, int y) => f.Rect(x, y, 3, 1, Zz).Dot(x + 1, y + 1, Zz).Rect(x, y + 2, 3, 1, Zz);
		void Flakes(Frame f, int k)
		{
			foreach (var (x, y) in new[] { (2, -2), (11, 0), (21, -1), (6, 4), (24, 6), (16, -2) })
				f.Dot(x, (y + k * 3 + 30) % 12 - 2, Snow);
		}
		Frame Pose(int eyes = 0, int beak = 0, int wl = 0, int wr = 0, bool cheeks = false)
		{
			var f = Base(); Eyes(f, eyes); Beak(f, beak); Wing(f, true, wl); Wing(f, false, wr); if (cheeks) Cheeks(f); return f;
		}

		// ------------------------------------------------------------ frames
		int fStand = p.Add(Base());
		int fBlink = p.Add(Pose(1));
		int fHappy = p.Add(Pose(2, 0, 0, 0, true));
		int fLookL = p.Add(Pose(3));
		int fLookR = p.Add(Pose(4));
		int fSurprise = p.Add(Pose(5, 1, 2, 2));

		// waddle: tilt one way while lifting the opposite foot
		var w1 = Pose(3); Lean(w1, -1, Y(22)); LiftFoot(w1, false); int fWalk1 = p.Add(w1);
		var w2 = Pose(3); int fWalk2 = p.Add(w2);
		var w3 = Pose(3); Lean(w3, 1, Y(22)); LiftFoot(w3, true); int fWalk3 = p.Add(w3);

		int fWave1 = p.Add(Pose(2, 0, 0, 2, true));
		int fWave2 = p.Add(Pose(2, 0, 0, 1, true));
		int fFlapUp = p.Add(Pose(2, 1, 2, 2, true));
		int fFlapMid = p.Add(Pose(2, 0, 1, 1, true));
		int fDrag1 = p.Add(Pose(5, 2, 2, 2));
		int fDrag2 = p.Add(Pose(5, 1, 1, 1));
		int fFall = p.Add(Pose(5, 1, 2, 2));
		int fCrouch = p.Add(Squash(Pose(0), 2));
		int fLand = p.Add(Squash(Pose(6, 1, 1, 1), 3));
		int fJump = p.Add(Up(Pose(2, 0, 2, 2), 2));
		int fClimb1 = p.Add(Pose(4, 0, 2, 0));
		int fClimb2 = p.Add(Pose(4, 0, 0, 2));
		int fSit = p.Add(Squash(Pose(0), 2));
		Frame Sleep(int z)
		{
			var f = Squash(Pose(1), 2);
			if (z >= 1) Zed(f, 20, 2);
			if (z >= 2) Zed(f, 23, -2);
			return f;
		}
		int fSleep1 = p.Add(Sleep(0)), fSleep2 = p.Add(Sleep(1)), fSleep3 = p.Add(Sleep(2));

		// belly slide
		int fDive = p.Add(Lean(Pose(5, 0, 2, 2), -2, Y(18)));
		int fSlide1 = p.Add(Lying(3, true, 0)), fSlide2 = p.Add(Lying(3, false, 1));
		int fGetUp = p.Add(Lean(Pose(2, 0, 1, 1), -1, Y(18)));

		// eat a fish: tossed up, caught, gulp
		var e1 = Pose(5, 0, 0, 2); FishAt(e1, 14, -2); int fFishUp = p.Add(e1);
		var e2 = Pose(5, 2, 0, 0); FishAt(e2, 7, 2, true); int fFishFall = p.Add(e2);
		var e3 = Pose(6, 1); e3.Rect(X(10), Y(9), 3, 1, Fish).Dot(X(13), Y(9), FishLight); int fGulp = p.Add(e3);
		int fYum = p.Add(Pose(2, 0, 1, 1, true));

		// dance: side steps with flippers
		var d1 = Pose(2, 0, 2, 0, true); Lean(d1, -1, Y(22)); LiftFoot(d1, false); int fDance1 = p.Add(d1);
		var d2 = Pose(2, 0, 0, 2, true); Lean(d2, 1, Y(22)); LiftFoot(d2, true); int fDance2 = p.Add(d2);

		// snow: flakes fall, he looks up and catches one
		Frame SnowPose(int k, int eyes, int beak)
		{
			var f = Pose(eyes, beak); Flakes(f, k); return f;
		}
		int fSnow1 = p.Add(SnowPose(0, 0, 0)), fSnow2 = p.Add(SnowPose(1, 5, 1)), fSnow3 = p.Add(SnowPose(2, 2, 0));

		// sneeze
		int fAh = p.Add(Up(Pose(6, 1), 1));
		var ch = Squash(Pose(6, 2, 1, 1), 1); ch.Dot(X(7), Y(10), Snow).Dot(X(6), Y(11), Snow).Dot(X(9), Y(11), Snow); int fChoo = p.Add(ch);

		// trip: falls flat on the belly, gets back up dizzy
		int fTrip = p.Add(Lean(Pose(5, 1, 2, 2), -3, Y(20)));
		int fFlat = p.Add(Lying(1, true, 0));
		var dz = Pose(1); dz.Dot(4, -1, Spark).Dot(20, 0, Spark).Dot(12, -2, Spark); int fDizzy = p.Add(dz);

		// shiver
		var sv1 = Pose(6, 0, 1, 1); sv1.Shift(-1, 0); int fShiver1 = p.Add(sv1);
		var sv2 = Pose(6, 0, 1, 1); sv2.Shift(1, 0); int fShiver2 = p.Add(sv2);

		// swim stroke on the floor (pretends to swim)
		int fSwim1 = p.Add(Lying(2, false, 0));
		var sw2 = Lying(2, false, 1); sw2.Dot(3, 25, Water).Dot(7, 25, Water).Dot(1, 24, Water); int fSwim2 = p.Add(sw2);

		// ================================================================ 50 MORE (31-90, children 101-114)
		void St(Frame f, int x, int y, params string[] rows) => f.Stamp(x, y, rows.Select(r => r.Replace('.', ' ')).ToArray());
		void Circle(Frame f, float cx, float cy, float r, char fill, char edge)
		{
			for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
				for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
				{
					float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
					if (d <= r) f[x, y] = d > r - 1 ? edge : fill;
				}
		}
		void HeartP(Frame f, int x, int y) => St(f, x, y, "HH.HH", "HhHHH", ".HHH.", "..H..");
		void NoteP(Frame f, int x, int y) => St(f, x, y, ".KK", ".K.", "KK.");
		void Drop(Frame f, int x, int y) => f.Dot(x, y, Water).Dot(x, y + 1, Water);
		void Confetti(Frame f, int k)
		{
			char[] cs = { Heart, Spark, Blue, Green };
			var pts = new[] { (2, -5), (8, -3), (14, -6), (20, -4), (24, -1), (4, 0), (17, 0), (11, -5) };
			for (int i = 0; i < pts.Length; i++) f.Dot(pts[i].Item1, pts[i].Item2 + (k + i) % 3, cs[(i + k) % 4]);
		}
		// flippers held in front of the belly: 0 apart, 1 together (clap), 2 left up / right down, 3 the reverse
		void Front(Frame f, int mode)
		{
			NoWings(f);
			int ly = mode == 2 ? 12 : 14, ry = mode == 3 ? 12 : 14;
			int lx = mode == 1 ? 9 : 5, rx = mode == 1 ? 14 : 17;
			St(f, lx, ly, ".KK", "KDDK", ".KDK");
			St(f, rx, ry, "KK.", "KDDK", "KDK.");
		}
		Frame Back()
		{
			var f = Base();
			for (int y = f.MinY; y < Y(22); y++) for (int x = 0; x < W; x++)
			{
				char c = f[x, y];
				if (c == White || c == Light || c == Eye || c == Or || c == Or2 || c == Or3) f[x, y] = Gray;
			}
			return f;
		}
		void Sunglasses(Frame f) { f.Rect(6, 7, 4, 2, Eye).Rect(13, 7, 4, 2, Eye).Rect(10, 7, 3, 1, Eye).Dot(7, 7, Light).Dot(14, 7, Light); }
		void Scarf(Frame f) { f.Rect(5, 10, 15, 1, Heart).Rect(5, 11, 15, 1, HeartLight).Rect(15, 12, 2, 3, Heart).Dot(15, 15, HeartLight).Dot(16, 15, Heart); }
		void Beanie(Frame f) => St(f, 6, -3, ".....LWL.....", "...HHHHHHH...", "..HHhHHhHHH..", ".KhhhhhhhhhhK");
		void Headphones(Frame f)
		{
			foreach (var (x, y) in new[] { (4, 4), (4, 3), (5, 2), (6, 1), (7, 0), (18, 0), (19, 1), (20, 2), (21, 3), (21, 4) }) f.Dot(x, y, Purple);
			f.Rect(8, -1, 10, 1, Purple).Rect(3, 5, 2, 4, Purple).Rect(21, 5, 2, 4, Purple).Dot(3, 5, PurpleLight).Dot(21, 5, PurpleLight);
		}
		void Bulb(Frame f, bool lit) { Circle(f, 21.5f, -2.5f, 2.6f, lit ? Spark : Light, lit ? Spark : Light); f.Rect(20, 0, 3, 1, Light).Dot(21, 1, Light); if (lit) f.Dot(17, -4, Spark).Dot(25, -3, Spark).Dot(18, -6, Spark); }
		void Skates(Frame f) { f.Rect(4, 25, 7, 1, Light).Rect(13, 25, 7, 1, Light).Dot(3, 24, Light).Dot(12, 24, Light); }
		void Snowball(Frame f, int x, int y) => St(f, x, y, ".LL.", "LWWL", "LWSL", ".LL.");
		void Plane(Frame f, int x, int y, int k)
		{
			if (k == 0) St(f, x, y, "....LLL", "..LLWWL", "LLWWWSL", "..LLSSL");
			else St(f, x, y, "..LLLLL", "LLWWWWL", "..LWSSL", "...LL..");
		}
		void Chick(Frame f, int x, int y, int k)
		{
			St(f, x, y, ".CCC.", "CECCC", "OCCCc", ".CCc.", k == 0 ? ".O.O." : "O...O");
		}
		void IceCube(Frame f, int x, int y) => St(f, x, y, "LLLLL", "LWuuL", "LuuuL", "LuuuL", "LLLLL");
		void Bubble(Frame f, float cx, float cy, float r) { Circle(f, cx, cy, r, '.', Ice); f.Dot((int)(cx - r * 0.4f), (int)(cy - r * 0.4f), White); }

		// ---- expressions
		int fYawn = p.Add(Pose(1, 2, 2, 2));
		int fStretch = p.Add(Up(Pose(1, 0, 2, 2), 1));
		var gg1 = Pose(2, 1, 0, 0, true); int fGiggle1 = p.Add(gg1);
		int fGiggle2 = p.Add(Up(Pose(2, 1, 0, 0, true), 1));
		Frame Angry(int k)
		{
			var f = Pose(0, k, 1, 1); f.Dot(7, 6, Out).Dot(8, 6, Out).Dot(6, 5, Out).Dot(16, 6, Out).Dot(15, 6, Out).Dot(17, 5, Out);
			f.Dot(5, 9, Heart).Dot(18, 9, Heart);
			St(f, 20, 0, "H.H", ".H.", "H.H");
			if (k == 1) f.Shift(1, 0);
			return f;
		}
		int fAngry1 = p.Add(Angry(0)), fAngry2 = p.Add(Angry(1));
		Frame Cry(int k)
		{
			var f = Pose(1, 1); Drop(f, 8, 9 + k); Drop(f, 14, 10 - k); Drop(f, 7, 12 + k); Drop(f, 15, 12 + (1 - k)); return f;
		}
		int fCry1 = p.Add(Cry(0)), fCry2 = p.Add(Cry(1));
		var lv1 = Pose(2, 0, 0, 0, true); HeartP(lv1, 19, -2); int fLove1 = p.Add(lv1);
		var lv2 = Pose(2, 0, 0, 0, true); HeartP(lv2, 20, -5); HeartP(lv2, 1, -2); int fLove2 = p.Add(lv2);
		var ht1 = Pose(6, 1, 0, 1); Drop(ht1, 19, 2); Drop(ht1, 4, 4); int fHot1 = p.Add(ht1);
		var ht2 = Pose(6, 1, 0, 2); Drop(ht2, 19, 4); Drop(ht2, 4, 6); int fHot2 = p.Add(ht2);
		Frame Think(int k)
		{
			var f = Pose(4); for (int i = 0; i <= k; i++) f.Dot(19 + i * 2, 2 - i * 2, Out); return f;
		}
		int fThink1 = p.Add(Think(0)), fThink2 = p.Add(Think(1)), fThink3 = p.Add(Think(2));
		var id1 = Pose(5); Bulb(id1, false); int fIdea1 = p.Add(id1);
		var id2 = Pose(2, 1, 1, 1, true); Bulb(id2, true); int fIdea2 = p.Add(id2);
		int fJumpScare = p.Add(Up(Pose(5, 2, 2, 2), 4));
		var hc = Up(Pose(5, 1), 2); f_dot(hc); int fHic = p.Add(hc);
		void f_dot(Frame f) { Circle(f, 21f, 2f, 1.6f, '.', Ice); }
		var bp = Pose(2, 2, 0, 0, true); Circle(bp, 4f, 8f, 2.2f, Ice, Ice); bp.Dot(3, 7, White); int fBurp = p.Add(bp);
		var wk = Pose(0, 0, 0, 0, true); wk.Dot(8, 7, White).Dot(8, 8, White).Dot(7, 8, Eye).Dot(8, 8, Eye).Dot(9, 8, Eye); int fWink = p.Add(wk);

		// ---- actions
		Frame Drum(int m) { var f = Pose(2, 1, 0, 0, true); Front(f, m); if (m == 2) f.Dot(4, 13, Out).Dot(3, 12, Out); else f.Dot(21, 13, Out).Dot(22, 12, Out); return f; }
		int fDrum1 = p.Add(Drum(2)), fDrum2 = p.Add(Drum(3));
		var cl1 = Pose(2, 1, 0, 0, true); Front(cl1, 0); int fClap1 = p.Add(cl1);
		var cl2 = Pose(2, 1, 0, 0, true); Front(cl2, 1); cl2.Dot(12, 11, Spark).Dot(11, 10, Spark).Dot(14, 10, Spark); int fClap2 = p.Add(cl2);
		var mr1 = Pose(0, 0, 1, 0); LiftFoot(mr1, true); Up(mr1, 1); int fMarch1 = p.Add(mr1);
		var mr2 = Pose(0, 0, 0, 1); LiftFoot(mr2, false); Up(mr2, 1); int fMarch2 = p.Add(mr2);
		int fBack = p.Add(Back());
		Frame Music(int k)
		{
			var f = Pose(1, 0, 0, 0); Headphones(f); Lean(f, k == 0 ? -1 : 1, Y(20)); NoteP(f, k == 0 ? 22 : 1, k == 0 ? -3 : -1); return f;
		}
		int fMusic1 = p.Add(Music(0)), fMusic2 = p.Add(Music(1));
		var sg1 = Pose(2, 1, 0, 1); NoteP(sg1, 21, -2); int fSing1 = p.Add(sg1);
		var sg2 = Pose(2, 2, 1, 0); NoteP(sg2, 1, -3); NoteP(sg2, 21, -5); int fSing2 = p.Add(sg2);
		var sk1 = Pose(2, 0, 1, 1); Lean(sk1, -1, Y(22)); Skates(sk1); sk1.Dot(24, 24, Ice).Dot(25, 22, Ice); int fSkate1 = p.Add(sk1);
		var sk2 = Pose(2, 0, 2, 1); Lean(sk2, -2, Y(22)); LiftFoot(sk2, false); Skates(sk2); int fSkate2 = p.Add(sk2);
		int fHopUp = p.Add(Up(Pose(2, 0, 1, 1), 3));
		int fHopDown = p.Add(Squash(Pose(2), 1));
		int fBow = p.Add(Squash(Pose(1), 3));
		var sl = Pose(0, 0, 0, 2); sl.Dot(19, 4, Out).Dot(20, 5, Out); int fSalute = p.Add(sl);
		var db = Pose(1, 0, 2, 1); Lean(db, -1, Y(12)); int fDab = p.Add(db);
		Frame Meditate(int k) { var f = Squash(Pose(1, 0, 1, 1), 2); Up(f, 3 + k); f.Dot(6, 24 - k, Ice).Dot(12, 25 - k, Spark).Dot(18, 24 - k, Ice); return f; }
		int fMed1 = p.Add(Meditate(0)), fMed2 = p.Add(Meditate(1));
		int fSquatDown = p.Add(Squash(Pose(6, 1, 1, 1), 3));
		int fSquatUp = p.Add(Pose(2, 0, 1, 1));
		Frame Book(int k)
		{
			var f = Pose(1); Front(f, 0);
			f.Rect(7, 13, 12, 5, Blue).Rect(8, 13, 5, 4, Ice).Rect(13, 13, 5, 4, Ice).Dot(9, 14, Light).Dot(10, 15, Light).Dot(14, 14, Light).Dot(15, 15, Light);
			if (k == 1) f.Rect(13, 11, 3, 2, Ice);
			return f;
		}
		int fRead1 = p.Add(Book(0)), fRead2 = p.Add(Book(1));
		var sc = Pose(2, 0, 0, 0, true); Scarf(sc); int fScarf = p.Add(sc);
		var sc2 = Pose(1, 0, 0, 0, true); Scarf(sc2); int fScarf2 = p.Add(sc2);
		var bn = Pose(2, 0, 0, 0, true); Beanie(bn); int fBeanie = p.Add(bn);
		var bn2 = Pose(0, 0, 0, 1); Beanie(bn2); int fBeanie2 = p.Add(bn2);
		var sgl = Pose(0, 0, 0, 2); Sunglasses(sgl); int fCool1 = p.Add(sgl);
		var sgl2 = Pose(0, 0, 1, 1); Sunglasses(sgl2); sgl2.Dot(20, 2, Spark).Dot(21, 1, Spark); int fCool2 = p.Add(sgl2);
		Frame Umbrella(int k)
		{
			var f = Pose(3, 0, 0, 1);
			St(f, 3, -5, ".....UUUUUUUU.....", "...UUUUUUUUUUUU...", ".UUUuUUUUUUUUuUUU.", "UU..UU...UU...UU..UU".Substring(0, 18));
			f.Rect(18, -2, 1, 9, Out);
			foreach (var (x, y) in k == 0 ? new[] { (1, 0), (24, 3), (0, 8), (25, 12) } : new[] { (2, 3), (25, 0), (1, 12), (24, 9) }) Drop(f, x, y);
			return f;
		}
		int fUmb1 = p.Add(Umbrella(0)), fUmb2 = p.Add(Umbrella(1));
		(int x, int y)[] jp = { (1, -2), (9, -5), (18, -3) };
		Frame JuggleF(int k)
		{
			var f = Pose(5, 0, k % 2 == 0 ? 2 : 1, k % 2 == 0 ? 1 : 2);
			for (int i = 0; i < 3; i++) { var (x, y) = jp[(i + k) % 3]; FishAt(f, x, y + (k + i) % 2, i % 2 == 0); }
			return f;
		}
		int fJug1 = p.Add(JuggleF(0)), fJug2 = p.Add(JuggleF(1)), fJug3 = p.Add(JuggleF(2));
		Frame Gum(int r)
		{
			var f = Pose(r >= 3 ? 5 : 0, 0);
			if (r < 3) { Circle(f, 11.5f, 10.5f, 1.2f + r, Pink, PinkDark); f.Dot(10, 9, White); }
			else foreach (var (x, y) in new[] { (7, 9), (15, 10), (11, 12), (9, 6), (14, 6) }) f.Dot(x, y, Pink);
			return f;
		}
		int fGum1 = p.Add(Gum(0)), fGum2 = p.Add(Gum(1)), fGum3 = p.Add(Gum(2)), fGumPop = p.Add(Gum(3));
		var sf1 = Pose(3, 0, 0, 2); sf1.Rect(20, 1, 3, 4, Eye).Dot(21, 2, Ice); int fSelfie = p.Add(sf1);
		var sf2 = Pose(2, 0, 0, 2, true); sf2.Rect(20, 1, 3, 4, Eye); St(sf2, 19, -4, "..Y..", ".YYY.", "YYYYY", ".YYY.", "..Y.."); int fFlash = p.Add(sf2);

		// ---- more actions
		Frame Fishing(int k)
		{
			var f = Pose(k == 2 ? 2 : 3, 0, 0, 1);
			for (int i = 0; i < 6; i++) f.Dot(20 + i, 5 - i * 2, Wood).Dot(20 + i, 4 - i * 2, Wood);
			f.Rect(25, -6, 1, 30, '.');
			for (int y = -6; y < 22 - k * 6; y++) f.Dot(25, y, Light);
			if (k == 2) FishAt(f, 20, 12, true);
			return f;
		}
		int fFish1 = p.Add(Fishing(0)), fFish2 = p.Add(Fishing(1)), fFish3 = p.Add(Fishing(2));
		Frame Sled(int k)
		{
			var f = Squash(Pose(2, 0, 1, 1), 3);
			f.Rect(2, 24, 22, 1, Wood).Rect(3, 25, 20, 1, WoodDark).Dot(1, 23, Wood).Dot(1, 22, Wood).Dot(2, 22, Wood);
			if (k == 1) f.Dot(24, 23, Ice).Dot(25, 21, Ice);
			return f;
		}
		int fSled1 = p.Add(Sled(0)), fSled2 = p.Add(Sled(1));
		Frame Trumpet(int k)
		{
			var f = Pose(1, 0, 1, 0);
			f.Rect(3, 9, 8, 1, Spark).Rect(1, 7, 2, 5, Spark).Dot(0, 8, Spark).Dot(0, 10, Spark).Dot(6, 8, SparkDark).Dot(8, 8, SparkDark);
			NoteP(f, k == 0 ? 2 : 6, k == 0 ? 1 : -2);
			return f;
		}
		int fTrumpet1 = p.Add(Trumpet(0)), fTrumpet2 = p.Add(Trumpet(1));
		Frame Sunbathe(int k)
		{
			var f = Lying(2, false, 0); f.Rect(6, 9, 2, 3, Eye).Rect(6, 15, 2, 3, Eye);
			Circle(f, 3f, -3f, 2.5f, Spark, Spark); if (k == 1) f.Dot(7, -4, Spark).Dot(0, 1, Spark).Dot(7, 0, Spark);
			return f;
		}
		int fSun1 = p.Add(Sunbathe(0)), fSun2 = p.Add(Sunbathe(1));
		var swk1 = Pose(1, 0, 1, 1); Lean(swk1, -1, Y(22)); LiftFoot(swk1, false); Zed(swk1, 20, -3); int fSleepwalk1 = p.Add(swk1);
		var swk2 = Pose(1, 0, 1, 1); Lean(swk2, 1, Y(22)); LiftFoot(swk2, true); Zed(swk2, 22, -5); int fSleepwalk2 = p.Add(swk2);
		var pt1 = Pose(2, 1, 2, 2, true); Confetti(pt1, 0); int fParty1 = p.Add(pt1);
		var pt2 = Up(Pose(2, 1, 1, 1, true), 2); Confetti(pt2, 1); int fParty2 = p.Add(pt2);

		// ================================================================ children (separate windows)
		// snowball
		var sbh = Pose(3, 0, 2, 0); Snowball(sbh, 0, 1); int fSbHold = p.Add(sbh);
		int fSbThrow = p.Add(Pose(2, 1, 1, 0));
		var sbf = new Frame(); Snowball(sbf, 0, 1); int fSbFly = p.Add(sbf);
		var sbs = new Frame(); St(sbs, 0, 22, "..W..", ".WSW.", "WSWSW", ".S.S."); int fSbSplat = p.Add(sbs);
		// balloon
		var blh = Pose(5, 0, 0, 2); Circle(blh, 22f, -3f, 2.8f, Heart, Heart); blh.Dot(21, -5, HeartLight).Dot(22, 0, Heart).Dot(22, 1, Light).Dot(22, 2, Light); int fBalHold = p.Add(blh);
		var blr = Pose(5, 1, 0, 2); int fBalLet = p.Add(blr);
		Frame BalloonFly(int k) { var f = new Frame(); Circle(f, 22f + k * 0.4f, -3f, 2.8f, Heart, Heart); f.Dot(21, -5, HeartLight).Dot(22, 0, Heart).Dot(22 - k, 1, Light).Dot(22 + k, 2, Light).Dot(22, 3, Light); return f; }
		int fBalFly1 = p.Add(BalloonFly(0)), fBalFly2 = p.Add(BalloonFly(1));
		// fish that slips away and flops along the floor
		var fsh = Pose(5, 2, 1, 1); FishAt(fsh, 7, 9, true); int fFishHold = p.Add(fsh);
		int fFishOops = p.Add(Pose(5, 1, 2, 2));
		var ff1 = new Frame(); FishAt(ff1, 2, 23); int fFlop1 = p.Add(ff1);
		var ff2 = new Frame(); St(ff2, 2, 21, "..F...", ".FfF..", "FFFF.F", "..FFF."); int fFlop2 = p.Add(ff2);
		// paper plane
		var plh = Pose(3, 0, 2, 0); Plane(plh, 0, 0, 0); int fPlaneHold = p.Add(plh);
		int fPlaneThrow = p.Add(Pose(2, 0, 1, 0));
		var pf1 = new Frame(); Plane(pf1, 0, 0, 0); int fPlaneFly1 = p.Add(pf1);
		var pf2 = new Frame(); Plane(pf2, 0, -1, 1); int fPlaneFly2 = p.Add(pf2);
		// egg hatches, the chick waddles off
		Frame Egg(int k)
		{
			var f = Pose(k == 2 ? 5 : 3);
			Circle(f, 2.5f, 22f + (k == 1 ? 0 : 0), 2.6f, White, Light);
			if (k >= 1) f.Dot(1, 21, Out).Dot(2, 22, Out).Dot(3, 21, Out);
			if (k == 1) f.Shift(0, 0);
			return f;
		}
		int fEgg1 = p.Add(Egg(0)), fEgg2 = p.Add(Egg(1)), fEgg3 = p.Add(Egg(2));
		var hatched = Pose(2, 0, 0, 1, true); St(hatched, 0, 23, "LWL.L", ".LWL."); int fHatched = p.Add(hatched);
		var ch1 = new Frame(); Chick(ch1, 0, 20, 0); int fChick1 = p.Add(ch1);
		var ch2 = new Frame(); Chick(ch2, 0, 19, 1); int fChick2 = p.Add(ch2);
		// ice cube kicked across the floor
		var ich = Pose(3); IceCube(ich, 0, 21); int fIceHold = p.Add(ich);
		var ick = Pose(6, 0, 1, 1); Lean(ick, 1, Y(22)); LiftFoot(ick, true); int fIceKick = p.Add(ick);
		var icf = new Frame(); IceCube(icf, 0, 21); icf.Dot(5, 24, Ice).Dot(6, 22, Ice); int fIceSlide = p.Add(icf);
		// blown kiss
		var ks = Pose(1, 0, 1, 0, true); ks.Dot(11, 10, Heart); int fKiss = p.Add(ks);
		var kh = new Frame(); HeartP(kh, 1, 6); int fKissHeart = p.Add(kh);
		// soap bubbles
		var bb = Pose(0, 0, 1, 0); Bubble(bb, 2f, 7f, 1.8f); int fBlow = p.Add(bb);
		var bc1 = new Frame(); Bubble(bc1, 2.5f, 5f, 2.6f); int fBubBig = p.Add(bc1);
		var bc2 = new Frame(); Bubble(bc2, 6f, 3f, 1.7f); int fBubSmall = p.Add(bc2);
		var bpo = new Frame(); foreach (var (x, y) in new[] { (0, 2), (4, 2), (2, 0), (5, 5), (1, 6) }) bpo.Dot(x, y, Ice); int fBubPop = p.Add(bpo);
		// snowman: built next to Pingo, stays a while, then melts
		Frame Snowman(int melt)
		{
			var f = new Frame();
			int d = melt * 3;
			if (melt < 3)
			{
				Circle(f, 13f, 21.5f + d * 0.3f, 4.5f - melt * 0.6f, White, Light);
				Circle(f, 13f, 14.5f + d, 3.5f - melt * 0.5f, White, Light);
				if (melt < 2) { Circle(f, 13f, 8.5f + d, 2.8f, White, Light); f.Dot(12, 8 + d, Eye).Dot(14, 8 + d, Eye).Rect(13, 9 + d, 2, 1, Or); }
				if (melt == 0) { f.Rect(10, 4, 6, 1, Out).Rect(11, 1, 4, 3, Out).Dot(7, 13, Wood).Dot(6, 12, Wood).Dot(19, 13, Wood).Dot(20, 12, Wood); }
			}
			f.Rect(7 - melt, 25, 13 + melt * 2, 1, melt == 0 ? Light : Water);
			return f;
		}
		int fSnowman = p.Add(Snowman(0)), fMelt1 = p.Add(Snowman(1)), fMelt2 = p.Add(Snowman(2)), fMelt3 = p.Add(Snowman(3));
		Frame Building(int r)
		{
			var f = Pose(r % 2 == 0 ? 3 : 1, 0, 1, 0); Lean(f, -1, Y(20));
			Circle(f, 2.5f, 23.5f - r * 0.5f, 1.2f + r * 0.6f, White, Light);
			return f;
		}
		int fBuild1 = p.Add(Building(0)), fBuild2 = p.Add(Building(1)), fBuild3 = p.Add(Building(2));
		// rolling snowball
		Frame BigSnowball(int k)
		{
			var f = new Frame(); Circle(f, 3f, 22.5f, 3.2f, White, Light);
			var spot = new[] { (2, 21), (4, 21), (4, 23), (2, 23) }[k % 4]; f.Dot(spot.Item1, spot.Item2, Snow);
			return f;
		}
		var rsh = Pose(3, 0, 1, 1); Circle(rsh, 3f, 22.5f, 3.2f, White, Light); int fRollHold = p.Add(rsh);
		var rsp = Pose(6, 0, 1, 1); Lean(rsp, -2, Y(22)); int fRollPush = p.Add(rsp);
		var rolls = new int[4]; for (int i = 0; i < 4; i++) rolls[i] = p.Add(BigSnowball(i));

		p.IconFrame = fHappy;

		// ------------------------------------------------------------ animations
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 35, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 20, "0", "areaH-imageH", 2));

		p.A(1, "walk").Move("-2", "0").Speed("150").Loop("random/4+8")
			.Seq(fWalk1, fWalk2, fWalk3, fWalk2)
			.Then(55, 1).Then(10, 2).Then(10, 11).Then(5, 20).Then(4, 22).Then(4, 23).Then(3, 24).Then(3, 27).Then(3, 25)
			.Then(4, 46).Then(4, 50).Then(4, 51).Then(3, 76).Then(2, 79).Then(3, 74).Then(3, 70)
			.Then(8, 16, "taskbar").Then(5, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window")
			.OnGravity(100, 5);

		var turn = p.A(2, "turn").Move("0", "0").Speed("130");
		turn.Seq(fLookR, fStand, fLookL);
		turn.Action = "flip";
		turn.Then(100, 1);

		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("45").Loop("30").Seq(fDrag1, fFall, fDrag2, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall, fDrag2).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fDrag2).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("120", "200").Seq(fLand, fLand, fCrouch, fStand, fHappy).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("150", "260").Seq(fLand, fFlat, fFlat, fFlat, fDizzy, fDizzy, fBlink, fStand).Then(100, 1);

		p.A(9, "jump").Move("-4", "-9", "-4", "9").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		p.A(10, "sync").Move("0", "0").Speed("100", "150").Loop("6").Seq(fHappy, fFlapUp, fFlapMid, fFlapUp).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("450", "120")
			.Seq(fStand, fBlink, fStand, fBlink, fStand)
			.Then(40, 1).Then(8, 15).Then(8, 17).Then(7, 20).Then(7, 21).Then(7, 22).Then(6, 23).Then(5, 24).Then(5, 25).Then(4, 26).Then(4, 27).Then(4, 28)
			.Then(3, 31).Then(2, 32).Then(3, 33).Then(2, 34).Then(2, 35).Then(3, 36).Then(2, 37).Then(3, 38).Then(3, 39).Then(2, 40).Then(2, 41).Then(2, 42).Then(3, 43)
			.Then(3, 44).Then(3, 45).Then(3, 46).Then(3, 47).Then(3, 48).Then(3, 49).Then(3, 50).Then(3, 51).Then(2, 52).Then(2, 53).Then(3, 54).Then(2, 55).Then(3, 56)
			.Then(2, 57).Then(2, 58).Then(2, 59).Then(3, 60).Then(2, 61).Then(3, 62).Then(3, 63).Then(3, 64).Then(3, 75).Then(3, 76).Then(3, 77).Then(2, 78).Then(2, 79).Then(3, 80)
			.Then(4, 65).Then(4, 66).Then(4, 67).Then(4, 68).Then(4, 69).Then(4, 70).Then(4, 71).Then(4, 72).Then(3, 73).Then(4, 74);

		p.A(12, "climb").Move("0", "-3").Speed("140").Loop("5000", 2)
			.Seq(fClimb1, fClimb2, fClimb1, fClimb2)
			.Then(100, 12)
			.OnBorder(100, 2);

		p.A(13, "sleep").Move("0", "0").Speed("800", "450").Loop("random/4+5", 2)
			.Seq(fSit, fSleep1, fSleep1, fSleep2, fSleep3, fSleep2)
			.Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("220", "160")
			.Seq(fSleep1, fSit, fSurprise, fStand, fHappy, fStand)
			.Then(100, 1);

		p.A(15, "look around").Move("0", "0").Speed("260").Seq(fStand, fLookL, fLookL, fStand, fLookR, fLookR, fStand).Then(100, 1);
		p.A(16, "sit").Move("0", "0").Speed("600", "400").Loop("random/3+4", 1).Seq(fSit, fSit, fSit, fSit).Then(60, 1).Then(40, 13);
		p.A(17, "wave").Move("0", "0").Speed("170").Loop("4", 1).Seq(fStand, fWave1, fWave2, fWave1, fWave2).Then(100, 1);

		// --- penguin specials ----------------------------------------------------
		Anim Show(int id, string name, int ms, params (int f, int n)[] steps)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			foreach (var (f, n) in steps) a.Seq(Enumerable.Repeat(f, n).ToArray());
			a.Then(100, 1);
			return a;
		}
		// 20-21 belly slide: dives, slides fast on the belly, gets up
		p.A(20, "dive").Move("-2", "0").Speed("110").Seq(fCrouch, fDive, fDive).Then(100, 21);
		p.A(21, "belly slide").Move("-7", "0", "-4", "0").Speed("70").Loop("random/4+6", 0)
			.Seq(fSlide1, fSlide2)
			.Then(100, 29)
			.OnBorder(100, 29).OnGravity(100, 5);
		p.A(29, "get up").Move("0", "0").Speed("140").Seq(fSlide1, fGetUp, fStand, fHappy).Then(100, 1);
		var flap = p.A(22, "flap").Move("0", "0").Speed("90").Loop("random/3+5", 0);
		flap.Seq(fFlapUp, fFlapMid, fStand, fFlapMid); flap.Then(100, 1);
		Show(23, "eat fish", 160, (fStand, 1), (fFishUp, 3), (fFishFall, 2), (fGulp, 3), (fYum, 4), (fHappy, 2));
		var dance = p.A(24, "dance").Move("0", "0").Speed("160").Loop("random/3+4", 0);
		dance.Seq(fDance1, fDance2); dance.Then(100, 1);
		var snow = p.A(25, "snow").Move("0", "0").Speed("200").Loop("random/3+3", 0);
		snow.Seq(fSnow1, fSnow2, fSnow3, fSnow1); snow.Then(100, 1);
		Show(26, "sneeze", 140, (fStand, 1), (fAh, 3), (fStand, 1), (fAh, 4), (fChoo, 3), (fBlink, 2), (fStand, 1));
		Show(27, "trip", 140, (fWalk1, 1), (fTrip, 2), (fFlat, 6), (fGetUp, 2), (fDizzy, 4), (fBlink, 1), (fStand, 1));
		var shiver = p.A(28, "shiver").Move("0", "0").Speed("60").Loop("10", 0);
		shiver.Seq(fShiver1, fShiver2); shiver.Then(100, 1);
		var swim = p.A(30, "swim").Move("-1", "0").Speed("180").Loop("random/3+4", 0);
		swim.Seq(fSwim1, fSwim2); swim.Then(100, 29).OnBorder(100, 29).OnGravity(100, 5);

		// ================================================================ 50 MORE
		Anim Loop2(int id, string name, int ms, string loops, params int[] frames)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString()).Loop(loops, 0);
			a.Seq(frames); a.Then(100, 1);
			return a;
		}
		// ---- expressions (31-43)
		Show(31, "yawn", 220, (fStand, 1), (fYawn, 5), (fBlink, 2), (fStand, 1));
		Show(32, "stretch", 200, (fStand, 1), (fStretch, 6), (fHappy, 2));
		Loop2(33, "giggle", 120, "random/3+3", fGiggle1, fGiggle2);
		Loop2(34, "angry", 90, "random/3+5", fAngry1, fAngry2);
		Loop2(35, "cry", 200, "random/3+4", fCry1, fCry2);
		Loop2(36, "love", 260, "random/3+3", fLove1, fLove2);
		Loop2(37, "hot", 130, "random/3+5", fHot1, fHot2);
		Show(38, "think", 300, (fThink1, 2), (fThink2, 2), (fThink3, 4), (fThink2, 1), (fThink3, 3), (fStand, 1));
		Show(39, "idea", 160, (fThink3, 3), (fIdea1, 2), (fIdea2, 1), (fIdea1, 1), (fIdea2, 6), (fHappy, 2));
		Show(40, "startled", 90, (fStand, 3), (fSurprise, 1), (fJumpScare, 3), (fSurprise, 2), (fLand, 2), (fBlink, 2), (fStand, 1));
		Show(41, "hiccup", 120, (fStand, 3), (fHic, 2), (fStand, 4), (fHic, 2), (fStand, 3), (fHic, 2), (fSurprise, 2), (fStand, 1));
		Show(42, "burp", 180, (fStand, 2), (fBurp, 4), (fGiggle1, 2), (fGiggle2, 1), (fHappy, 2));
		Show(43, "wink", 200, (fStand, 2), (fWink, 4), (fHappy, 2));
		// ---- actions (44-64)
		Loop2(44, "belly drum", 110, "random/3+5", fDrum1, fDrum2);
		Loop2(45, "clap", 120, "random/3+5", fClap1, fClap2);
		var march = p.A(46, "march").Move("-1", "0").Speed("160").Loop("random/3+5", 0);
		march.Seq(fMarch1, fStand, fMarch2, fStand); march.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		Loop2(47, "spin", 90, "random/3+3", fStand, fWalk3, fBack, fWalk1);   // front, leaning one side, back, leaning the other
		Loop2(48, "headphones", 200, "random/3+5", fMusic1, fMusic2);
		Loop2(49, "sing", 240, "random/3+3", fSing1, fSing2);
		var skate = p.A(50, "ice skate").Move("-6", "0", "-3", "0").Speed("110").Loop("random/3+5", 0);
		skate.Seq(fSkate1, fSkate1, fSkate2, fSkate2); skate.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		var hop = p.A(51, "hop hop").Move("-3", "0").Speed("110").Loop("random/3+4", 0);
		hop.Seq(fHopDown, fHopUp, fHopUp, fHopDown); hop.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		Show(52, "bow", 220, (fStand, 1), (fBow, 4), (fHappy, 2));
		Show(53, "salute", 260, (fStand, 1), (fSalute, 6), (fStand, 1));
		Show(54, "dab", 200, (fStand, 1), (fDab, 5), (fHappy, 2));
		Loop2(55, "meditate", 400, "random/3+4", fMed1, fMed2);
		Loop2(56, "squats", 200, "random/3+5", fSquatUp, fSquatDown);
		Loop2(57, "read", 420, "random/3+3", fRead1, fRead1, fRead2, fRead1);
		Loop2(58, "scarf", 400, "random/3+3", fScarf, fScarf2);
		Loop2(59, "beanie", 380, "random/3+3", fBeanie, fBeanie2);
		Loop2(60, "sunglasses", 300, "random/3+3", fCool1, fCool2);
		Loop2(61, "umbrella", 170, "random/3+5", fUmb1, fUmb2);
		Loop2(62, "juggle fish", 140, "random/3+4", fJug1, fJug2, fJug3);
		Show(63, "bubble gum", 200, (fStand, 1), (fGum1, 2), (fGum2, 2), (fGum3, 3), (fGumPop, 3), (fSurprise, 2), (fGiggle1, 2));
		Show(64, "selfie", 200, (fSelfie, 4), (fFlash, 2), (fSelfie, 2), (fFlash, 2), (fHappy, 2));
		// ---- more actions (75-80)
		Show(75, "fishing", 260, (fFish1, 6), (fFish2, 3), (fFish1, 2), (fFish2, 4), (fFish3, 4), (fHappy, 2));
		var sled = p.A(76, "sled").Move("-6", "0", "-3", "0").Speed("90").Loop("random/3+6", 0);
		sled.Seq(fSled1, fSled2); sled.Then(100, 1).OnBorder(100, 2).OnGravity(100, 5);
		Loop2(77, "trumpet", 200, "random/3+4", fTrumpet1, fTrumpet2);
		Loop2(78, "sunbathe", 500, "random/3+3", fSun1, fSun1, fSun2);
		var sleepwalk = p.A(79, "sleepwalk").Move("-1", "0").Speed("260").Loop("random/3+4", 0);
		sleepwalk.Seq(fSleepwalk1, fSleepwalk2); sleepwalk.Then(100, 14).OnBorder(100, 2).OnGravity(100, 5);
		Loop2(80, "party", 130, "random/3+5", fParty1, fParty2);

		// ================================================================ with a separate window (65-74)
		// Each scene: a "hold" animation (the object is drawn in Pingo's frame) that leads to a "release"
		// animation; when the release starts, the app opens the child window with the object, which
		// then moves on its own. Child animations with no next close the window when they end.
		Anim Hold(int id, string name, int ms, int frame, int n, int next)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			a.Seq(Enumerable.Repeat(frame, n).ToArray()); a.Then(100, next);
			return a;
		}
		// 65 snowball
		Hold(65, "hold snowball", 170, fSbHold, 4, 81);
		Show(81, "throw snowball", 150, (fSbThrow, 2), (fLookL, 6), (fGiggle1, 2), (fGiggle2, 2));
		p.A(101, "snowball arc").Move("-6", "-6", "-6", "4").Speed("60")
			.Seq(Enumerable.Repeat(fSbFly, 12).ToArray()).Then(100, 102).OnBorder(100, 103, "taskbar").OnBorder(100, 103, "window");
		p.A(102, "snowball fall").Move("-5", "6").Speed("60").Loop("200", 0)
			.Seq(fSbFly).Then(100, 102).OnBorder(100, 103, "taskbar").OnBorder(100, 103, "window");
		p.A(103, "snow splat").Move("0", "0").Speed("180").Seq(fSbSplat, fSbSplat, fSbSplat);
		p.Childs.Add((81, "imageX", "imageY", 101));
		// 66 balloon
		Hold(66, "balloon", 260, fBalHold, 8, 82);
		Show(82, "let balloon go", 170, (fBalLet, 3), (fSurprise, 3), (fWave1, 2), (fWave2, 2), (fWave1, 2), (fHappy, 2));
		p.A(104, "balloon away").Move("-1", "-3", "1", "-4").Speed("70").Loop("100", 0)
			.Seq(fBalFly1, fBalFly1, fBalFly2, fBalFly2).Then(100, 104);
		p.Childs.Add((82, "imageX", "imageY", 104));
		// 67 slippery fish
		Hold(67, "hold fish", 200, fFishHold, 4, 83);
		Show(83, "fish slips", 140, (fFishOops, 4), (fLookL, 6), (fCry1, 2), (fCry2, 2), (fStand, 1));
		p.A(105, "fish flop").Move("-4", "0").Speed("100").Loop("40", 0)
			.Seq(fFlop1, fFlop2).Then(100, 105);
		p.Childs.Add((83, "imageX", "imageY", 105));
		// 68 paper plane
		Hold(68, "hold plane", 180, fPlaneHold, 5, 84);
		Show(84, "throw plane", 150, (fPlaneThrow, 2), (fLookL, 6), (fHappy, 3));
		p.A(106, "plane flight").Move("-6", "-2", "-6", "1").Speed("60").Loop("30", 0)
			.Seq(fPlaneFly1, fPlaneFly1, fPlaneFly2, fPlaneFly2).Then(100, 106);
		p.Childs.Add((84, "imageX", "imageY", 106));
		// 69 an egg hatches, the chick waddles away
		p.A(69, "egg").Move("0", "0").Speed("200").Seq(fEgg1, fEgg1, fEgg2, fEgg1, fEgg2, fEgg2, fEgg3, fEgg3).Then(100, 85);
		Show(85, "chick hatches", 180, (fHatched, 3), (fLookL, 4), (fWave1, 2), (fWave2, 2), (fWave1, 2), (fHappy, 2));
		p.A(107, "chick walk").Move("-2", "0").Speed("140").Loop("120", 0)
			.Seq(fChick1, fChick2).Then(100, 107);
		p.Childs.Add((85, "imageX", "imageY", 107));
		// 70 ice cube kick
		Hold(70, "ice cube", 220, fIceHold, 4, 86);
		Show(86, "kick ice", 130, (fIceKick, 3), (fLookL, 6), (fHappy, 2));
		p.A(108, "ice slide").Move("-8", "0", "-3", "0").Speed("60").Loop("40", 0)
			.Seq(fIceSlide).Then(100, 108);
		p.Childs.Add((86, "imageX", "imageY", 108));
		// 71 blown kiss
		Show(71, "blow kiss", 170, (fStand, 1));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 87);
		Show(87, "kiss", 170, (fKiss, 4), (fHappy, 3), (fGiggle1, 1), (fGiggle2, 1));
		p.A(109, "kiss heart").Move("-3", "-2", "-2", "-3").Speed("80").Loop("8", 0).Fade(1, 0)
			.Seq(fKissHeart, fKissHeart);
		p.Childs.Add((87, "imageX", "imageY", 109));
		// 72 soap bubbles
		Show(72, "bubbles", 200, (fStand, 1));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 88);
		Show(88, "blow bubbles", 200, (fBlow, 3), (fGiggle1, 2), (fLookL, 4), (fHappy, 2));
		p.A(110, "big bubble").Move("-1", "-2", "1", "-3").Speed("90").Loop("10", 0).Seq(fBubBig).Then(100, 112);
		p.A(111, "small bubble").Move("1", "-3", "-1", "-2").Speed("80").Loop("8", 0).Seq(fBubSmall).Then(100, 112);
		p.A(112, "bubble pop").Move("0", "0").Speed("120").Seq(fBubPop);
		p.Childs.Add((88, "imageX", "imageY", 110));
		p.Childs.Add((88, "imageX", "imageY", 111));
		// 73 snowman: built beside Pingo, it stays while he goes on, then melts away
		p.A(73, "build snowman").Move("0", "0").Speed("220").Seq(fBuild1, fBuild1, fBuild2, fBuild2, fBuild3, fBuild3).Then(100, 89);
		Show(89, "snowman done", 200, (fHappy, 3), (fWave1, 2), (fWave2, 2), (fHappy, 2));
		p.A(113, "snowman").Move("0", "0").Speed("400").Loop("60", 0)
			.Seq(fSnowman);
		p.Anims.Last().Then(100, 115);
		p.A(115, "snowman melts").Move("0", "0").Speed("500").Seq(fMelt1, fMelt2, fMelt3, fMelt3);
		p.Childs.Add((89, "imageX-imageW*0.55", "imageY", 113));
		// 74 rolling snowball
		Hold(74, "big snowball", 220, fRollHold, 4, 90);
		Show(90, "push snowball", 140, (fRollPush, 3), (fLookL, 5), (fHappy, 2));
		p.A(114, "snowball roll").Move("-4", "0").Speed("90").Loop("60", 0)
			.Seq(rolls).Then(100, 114);
		p.Childs.Add((90, "imageX", "imageY", 114));

		// 99 kill
		p.A(99, "kill").Move("0", "0").Speed("120", "220").Fade(1, 0)
			.Seq(fWave1, fWave2, fWave1, fBlink, fBlink)
			.Then(100, 99);

		p.Readme = """
			# Pingo the Penguin

			## Description

			A chubby little pixel penguin with a dark back, a big white belly, an orange
			beak and orange feet.

			He waddles along your taskbar, flaps his flippers, dives and slides on his
			belly, eats fish, dances, watches the snow, sneezes, trips (and gets back up),
			shivers and naps when left alone.

			## About pet

			Author: Luan Felipe

			## Animations

			* waddle, turn, jump onto windows, fall (soft and hard landings)
			* climb the screen border, sit and sleep on the taskbar
			* blink, look around, wave, flap, dance, shiver
			* belly slide, pretend swim, eat a fish, snow, sneeze, trip
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Pingo, o Pinguim

			## Descrição

			Um pinguim de pixel art gordinho, com as costas escuras, a barriga branca,
			o bico e os pés laranja.

			Ele anda gingando pela barra de tarefas, bate as asas, mergulha e escorrega
			de barriga, come peixe, dança, olha a neve cair, espirra, tropeça (e
			levanta), treme de frio e tira um cochilo quando fica sozinho.

			## Sobre o pet

			Autor: Luan Felipe

			## Animações

			* andar gingando, virar, pular sobre janelas, cair (pouso suave / brusco)
			* escalar as bordas da tela, sentar e dormir na barra de tarefas
			* piscar, olhar ao redor, dar tchau, bater as asas, dançar, tremer de frio
			* escorregar de barriga, fingir nadar, comer peixe, neve, espirro, tropeço
			* arrastar e jogar com o mouse
			""";

		Frame.NewHeadroom = 0;
		Frame.NewWidth = Frame.Size;
		return p;
	}
}
