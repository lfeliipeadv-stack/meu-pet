using System.Drawing;

namespace PetGen;

/// <summary>
/// "Boo" — an original pixel-art little ghost for DesktopPet: a round white sheet with a
/// bluish outline, big dark eyes, rosy cheeks and a wavy hem, floating over a soft shadow.
/// Drawn from scratch on a 16-wide grid with 4 rows of headroom for bobbing and props.
/// </summary>
static class BooPet
{
	const char Sheet = 'W', Line = 'o', Shade = 's', Ink = 'K', Blush = 'P', Mouth = 'm', Shadow = 'q';
	const char Heart = 'h', Glow = 'y', Flame = 'f', Pumpkin = 'O', Stem = 'G', Zz = 'z', Tear = 'c';
	const char Gum2 = 'u', GumC = 'v', Mint = 'i', Cone = 'n', BookBlue = 'B', Red = 'R', RedLight = 'r';
	const char Purple = 'U', PurpleLight = 'V', GlowLight = 'L', GlowCyan = 'l', GlowDeep = 'D';

	// Front view. Row 0 of the array is y = 0; the shadow lies on y = 15.
	static readonly string[] Body =
	{
		"......oooo......",   //  0
		"....ooWWWWoo....",   //  1
		"...oWWWWWWWWo...",   //  2
		"..oWWWWWWWWWWo..",   //  3
		"..oWWWWWWWWWWo..",   //  4
		".oWWWWWWWWWWWWo.",   //  5 eyes go on rows 5-6
		".oWWWWWWWWWWWWo.",   //  6
		".oWWWWWWWWWWWWo.",   //  7 cheeks and mouth
		".oWWWWWWWWWWWWo.",   //  8
		"oWWWWWWWWWWWWWWo",   //  9 little arms
		".oWWWWWWWWWWWWo.",   // 10
		".oWWWWWWWWWWWso.",   // 11
		".osWWWWWWWWWsso.",   // 12
	};
	static readonly string[][] Hems =
	{
		new[] { ".oWsoWWsoWWsoWo.", "..oo..oo..oo.o.." },
		new[] { ".oWWsoWWsoWWsoo.", "..o.oo..oo..oo.." },
	};

	public static PetDef Build()
	{
		Frame.NewHeadroom = 4;   // room above the head for balloons, umbrellas, notes, stars...
		var p = new PetDef
		{
			Folder = "boo",
			Title = "Boo the Little Ghost",
			PetName = "Boo",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Boo is a shy, friendly little pixel ghost.[br]" +
				   "He floats around your desktop, plays peekaboo, giggles, goes invisible[br]" +
				   "and says BOO! when you least expect it. Original character — feel free to distribute.",
			Palette = new Dictionary<char, Color>
			{
				[Sheet] = Color.FromArgb(250, 251, 255),
				[Line] = Color.FromArgb(150, 162, 196),
				[Shade] = Color.FromArgb(214, 220, 240),
				[Ink] = Color.FromArgb(34, 32, 48),
				[Blush] = Color.FromArgb(246, 168, 186),
				[Mouth] = Color.FromArgb(214, 82, 104),
				[Shadow] = Color.FromArgb(178, 182, 198),
				[Heart] = Color.FromArgb(240, 84, 116),
				[Glow] = Color.FromArgb(252, 228, 120),
				[Flame] = Color.FromArgb(246, 150, 50),
				[Pumpkin] = Color.FromArgb(236, 130, 40),
				[Stem] = Color.FromArgb(90, 150, 70),
				[Zz] = Color.FromArgb(170, 186, 240),
				[Tear] = Color.FromArgb(130, 200, 250),
				[Gum2] = Color.FromArgb(250, 150, 200),
				[GumC] = Color.FromArgb(255, 200, 228),
				[Mint] = Color.FromArgb(150, 228, 196),
				[Cone] = Color.FromArgb(214, 160, 92),
				[BookBlue] = Color.FromArgb(70, 110, 200),
				[Red] = Color.FromArgb(226, 64, 70),
				[RedLight] = Color.FromArgb(255, 150, 150),
				[Purple] = Color.FromArgb(140, 92, 210),
				[PurpleLight] = Color.FromArgb(196, 160, 250),
				[GlowLight] = Color.FromArgb(214, 255, 240),
				[GlowCyan] = Color.FromArgb(150, 255, 230),
				[GlowDeep] = Color.FromArgb(40, 180, 170),
			},
			Scale = 3,
			TilesX = 8,
		};

		// ------------------------------------------------------------ helpers
		// lift: how high the ghost floats (0 = low, up to 2 using the headroom).
		Frame Ghost(int lift = 1, int hem = 0, bool shadow = true)
		{
			var f = new Frame();
			f.Stamp(0, -lift, Body);
			var rows = Hems[hem % 2];
			for (int j = 0; j < 2; j++) f.Stamp(0, 13 + j - lift, rows[j].Replace('.', ' '));
			if (shadow) f.Rect(lift >= 2 ? 6 : 5, 15, lift >= 2 ? 4 : 6, 1, Shadow);
			return f;
		}
		int Top(Frame f, int lift) => -lift;
		// eyes: 0 open, 1 closed (happy arcs), 2 look left, 3 look right, 4 wide, 5 squeezed (> <)
		void Face(Frame f, int lift, int eyes, int mouth = 0, bool blush = true)
		{
			int y = 5 - lift;
			switch (eyes)
			{
				case 0: f.Rect(4, y - 1, 2, 3, Ink).Rect(10, y - 1, 2, 3, Ink); break;           // tall oval eyes
				case 1: f.Dot(4, y + 1, Ink).Dot(5, y, Ink).Dot(6, y + 1, Ink).Dot(9, y + 1, Ink).Dot(10, y, Ink).Dot(11, y + 1, Ink); break;
				case 2: f.Rect(3, y - 1, 2, 3, Ink).Rect(9, y - 1, 2, 3, Ink); break;
				case 3: f.Rect(5, y - 1, 2, 3, Ink).Rect(11, y - 1, 2, 3, Ink); break;
				case 4: f.Rect(4, y - 2, 2, 4, Ink).Rect(10, y - 2, 2, 4, Ink).Dot(5, y - 2, Sheet).Dot(11, y - 2, Sheet); break;
				case 5: f.Dot(4, y, Ink).Dot(5, y + 1, Ink).Dot(4, y + 2, Ink).Dot(11, y, Ink).Dot(10, y + 1, Ink).Dot(11, y + 2, Ink); break;
				case 6: f.Rect(4, y + 1, 2, 1, Ink).Rect(10, y + 1, 2, 1, Ink); break;   // sleepy line
			}
			if (blush) f.Dot(3, y + 2, Blush).Dot(12, y + 2, Blush);
			switch (mouth)
			{
				case 0: f.Rect(7, y + 2, 2, 1, Mouth); break;                                   // smile
				case 1: f.Rect(7, y + 2, 2, 2, Mouth).Dot(7, y + 2, Ink).Dot(8, y + 2, Ink); break; // "o"
				case 2: f.Rect(6, y + 2, 4, 3, Ink).Rect(7, y + 3, 2, 2, Mouth); break;          // BOO!
				case 3: f.Dot(7, y + 2, Mouth).Dot(8, y + 3, Mouth).Dot(9, y + 2, Mouth); break; // giggle
				case 4: break;
			}
		}
		void Arms(Frame f, int lift, int left, int right)           // 0 down, 1 up, 2 over the eyes
		{
			int y = 9 - lift;
			if (left == 1) f.Dot(0, y, '.').Dot(1, y - 2, Line).Dot(0, y - 3, Line).Dot(1, y - 3, Sheet).Dot(1, y - 1, Sheet);
			if (right == 1) f.Dot(15, y, '.').Dot(14, y - 2, Line).Dot(15, y - 3, Line).Dot(14, y - 3, Sheet).Dot(14, y - 1, Sheet);
			if (left == 2) f.Rect(3, y - 4, 4, 3, Sheet).Rect(3, y - 5, 4, 1, Line);
			if (right == 2) f.Rect(9, y - 4, 4, 3, Sheet).Rect(9, y - 5, 4, 1, Line);
		}
		void HeartAt(Frame f, int x, int y) { f.Dot(x, y, Heart).Dot(x + 2, y, Heart).Rect(x, y + 1, 3, 1, Heart).Dot(x + 1, y + 2, Heart); }
		// Dither the ghost away (k = 0..3) for the vanish trick; the shadow stays.
		Frame Fade(Frame f, int k)
		{
			for (int y = f.MinY; y < 15; y++)
				for (int x = 0; x < 16; x++)
					if (f[x, y] != '.' && ((x + y * 2) % 4) < k) f[x, y] = '.';
			return f;
		}

		Frame G(int lift, int hem, int eyes, int mouth = 0, int la = 0, int ra = 0, bool shadow = true)
		{
			var f = Ghost(lift, hem, shadow); Face(f, lift, eyes, mouth); Arms(f, lift, la, ra); return f;
		}

		// ------------------------------------------------------------ frames
		int fStand = p.Add(G(1, 0, 0));
		int fStand2 = p.Add(G(2, 1, 0));
		int fBlink = p.Add(G(1, 0, 6));
		int fFloat1 = p.Add(G(1, 0, 2)), fFloat2 = p.Add(G(2, 1, 2)), fFloat3 = p.Add(G(1, 1, 2)), fFloat4 = p.Add(G(0, 0, 2));
		int fHappy = p.Add(G(1, 0, 1, 0));
		int fWave = p.Add(G(1, 1, 1, 0, 0, 1));
		int fWave2 = p.Add(G(2, 0, 1, 0, 0, 1));
		int fSurprise = p.Add(G(1, 0, 4, 1, 1, 1));
		int fLookL = p.Add(G(1, 0, 2)), fLookR = p.Add(G(1, 1, 3));
		var dr1 = G(1, 0, 4, 1, 1, 1, false); int fDrag1 = p.Add(dr1);
		var dr2 = G(1, 1, 4, 1, 1, 1, false); dr2.Shift(1, 0); int fDrag2 = p.Add(dr2);
		int fFall = p.Add(G(1, 1, 4, 1, 1, 1, false));
		var sq = G(0, 0, 1, 0); sq.Shift(0, 1); sq.Rect(4, 15, 8, 1, Shadow); int fSquash = p.Add(sq);
		int fLand = p.Add(G(0, 1, 5, 1));
		var cl1 = G(1, 0, 3, 0, 1, 0, false); int fClimb1 = p.Add(cl1);
		var cl2 = G(2, 1, 3, 0, 0, 1, false); int fClimb2 = p.Add(cl2);
		var sit = G(0, 0, 0); sit.Shift(0, 1); sit.Rect(3, 15, 10, 1, Shadow); int fSit = p.Add(sit);
		Frame Sleep(int z)
		{
			var f = G(0, z % 2, 6, 4); f.Shift(0, 1); f.Rect(3, 15, 10, 1, Shadow);
			if (z >= 1) f.Dot(13, 0, Zz).Dot(14, -1, Zz);
			if (z >= 2) f.Rect(12, -2, 3, 1, Zz).Dot(13, -1, '.').Dot(13, -1, Zz);
			return f;
		}
		int fSleep1 = p.Add(Sleep(0)), fSleep2 = p.Add(Sleep(1)), fSleep3 = p.Add(Sleep(2));

		// BOO!: puffs up, arms up, big mouth
		var bo1 = G(2, 0, 5, 1, 1, 1); int fBooPrep = p.Add(bo1);
		var bo2 = G(2, 1, 4, 2, 1, 1); bo2.Dot(0, -2, Ink).Dot(15, -2, Ink).Dot(1, -1, Ink).Dot(14, -1, Ink); int fBoo = p.Add(bo2);
		// giggle
		int fGiggle1 = p.Add(G(1, 0, 1, 3)), fGiggle2 = p.Add(G(2, 1, 1, 3));
		// peekaboo
		int fHide = p.Add(G(1, 0, 0, 0, 2, 2)), fPeek = p.Add(G(1, 1, 0, 0, 2, 0)), fPeekBoo = p.Add(G(2, 0, 1, 1, 1, 1));
		// spin: squashed side views
		Frame Side(int dir)
		{
			var f = Ghost(1, 0); for (int y = -1; y < 15; y++) { f[1, y] = '.'; f[2, y] = f[2, y] == '.' ? '.' : Line; f[14, y] = '.'; f[13, y] = f[13, y] == '.' ? '.' : Line; }
			f.Rect(dir < 0 ? 4 : 10, 4, 2, 2, Ink);
			return f;
		}
		var back = Ghost(1, 1); int fBack = p.Add(back);
		int fSideL = p.Add(Side(-1)), fSideR = p.Add(Side(1));
		// hearts
		var ht1 = G(1, 0, 1, 0); HeartAt(ht1, 12, 0); int fHeart1 = p.Add(ht1);
		var ht2 = G(2, 1, 1, 0); HeartAt(ht2, 12, -2); HeartAt(ht2, 1, 1); int fHeart2 = p.Add(ht2);
		// vanish
		int fFade1 = p.Add(Fade(G(1, 0, 1, 0), 1)), fFade2 = p.Add(Fade(G(1, 1, 1, 0), 2)), fFade3 = p.Add(Fade(G(1, 0, 1, 0), 3));
		var gone = new Frame(); gone.Rect(6, 15, 4, 1, Shadow); int fGone = p.Add(gone);
		// candle
		Frame Candle(int k)
		{
			var f = G(1, k, 3, 0, 0, 0);
			f.Rect(7, 10, 2, 3, Glow).Rect(6, 13, 4, 1, Flame).Dot(7, 9, Ink);                // candle held in front
			f.Dot(7 + k, 8, Flame).Dot(7 + k, 7, Glow);                                       // flickering flame
			f.Dot(5, 9 + k, Glow).Dot(10, 10 - k, Glow);
			return f;
		}
		int fCandle1 = p.Add(Candle(0)), fCandle2 = p.Add(Candle(1));
		// pumpkin
		Frame Pump(int k)
		{
			var f = G(1, k, 1, 0, 0, 0);
			f.Stamp(5, 9 - 1, ".GG.", "OOOO", "OKOK", "OOOO").Rect(4, 9, 1, 3, Pumpkin).Rect(9, 9, 1, 3, Pumpkin);
			if (k == 1) f.Dot(6, 9, Glow).Dot(8, 9, Glow);
			return f;
		}
		int fPump1 = p.Add(Pump(0)), fPump2 = p.Add(Pump(1));
		// sad / cry (when tossed or after a hard fall)
		var cry = G(0, 0, 6, 4); cry.Dot(4, 7, Tear).Dot(11, 8, Tear); int fCry = p.Add(cry);
		// shiver in the wind
		var sv1 = G(1, 0, 5, 1); sv1.Shift(-1, 0); int fShiver1 = p.Add(sv1);
		var sv2 = G(1, 1, 5, 1); sv2.Shift(1, 0); int fShiver2 = p.Add(sv2);

		// ================================================================ 30 MORE (ids 40-69)
		void FillCircle(Frame f, float cx, float cy, float r, char c, char hi)
		{
			for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
				for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
				{
					float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
					if (dx * dx + dy * dy <= r * r) f[x, y] = c;
				}
			f.Dot((int)(cx - r * 0.45f), (int)(cy - r * 0.45f), hi);
		}
		void OutlineAll(Frame f)
		{
			var src = f.Clone();
			for (int y = f.MinY; y <= 15; y++)
				for (int x = 0; x < 16; x++)
					if (src[x, y] == Sheet && (src[x - 1, y] == '.' || src[x + 1, y] == '.' || src[x, y - 1] == '.' || src[x, y + 1] == '.')) f[x, y] = Line;
		}
		Frame Lean(Frame f, int dx, int belowY)          // shifts the rows above belowY sideways (tilt)
		{
			var src = f.Clone();
			for (int y = f.MinY; y < belowY; y++) { for (int x = 0; x < 16; x++) f[x, y] = '.'; for (int x = 0; x < 16; x++) if (src[x, y] != '.') f[x + dx, y] = src[x, y]; }
			return f;
		}
		Frame FlipV(Frame f)                              // upside down (for the loop-the-loop)
		{
			var src = f.Clone();
			for (int y = f.MinY; y <= 15; y++) for (int x = 0; x < 16; x++) f[x, y] = '.';
			for (int y = f.MinY; y <= 15; y++) for (int x = 0; x < 16; x++) if (src[x, y] != '.') f[x, 12 - y] = src[x, y];
			return f;
		}
		void Trail(Frame f, params (int x, int y)[] pts) { foreach (var (x, y) in pts) if (f[x, y] == '.') f.Dot(x, y, Line); }
		void Note(Frame f, int x, int y) { f.Rect(x + 1, y, 1, 3, Ink).Dot(x + 2, y, Ink).Rect(x, y + 2, 2, 1, Ink); }
		void Star(Frame f, int x, int y) { f.Dot(x, y, Glow).Dot(x - 1, y, Glow).Dot(x + 1, y, Glow).Dot(x, y - 1, Glow).Dot(x, y + 1, Glow); }
		void Mini(Frame f, int x, int y) { f.Stamp(x, y, ".oo.", "oKWK", "oWWo", "o.o.".Replace('.', ' ')); }
		void Flower(Frame f, int x, int y) { f.Dot(x, y + 2, Stem).Dot(x, y + 3, Stem).Dot(x - 1, y + 2, Stem).Dot(x, y, Glow).Dot(x - 1, y, Heart).Dot(x + 1, y, Heart).Dot(x, y - 1, Heart).Dot(x, y + 1, Heart); }

		// ---- flying (40-46)
		var flA = G(1, 0, 2, 0, 1, 1, false); Lean(flA, -1, 6); Trail(flA, (15, 5), (15, 9), (14, 12)); int fFlyA = p.Add(flA);
		var flB = G(2, 1, 2, 0, 0, 0, false); Lean(flB, -1, 5); Trail(flB, (15, 3), (15, 7), (14, 10), (15, 11)); int fFlyB = p.Add(flB);
		var flUp1 = G(2, 0, 4, 3, 1, 1, false); Trail(flUp1, (5, 15), (10, 15), (7, 14)); int fUp1 = p.Add(flUp1);
		var flUp2 = G(2, 1, 4, 3, 1, 1, false); Trail(flUp2, (4, 14), (9, 15), (12, 14)); int fUp2 = p.Add(flUp2);
		var flDn1 = G(0, 0, 2, 0, 1, 1, false); Trail(flDn1, (4, -2), (11, -2), (7, -1)); int fDown1 = p.Add(flDn1);
		var flDn2 = G(0, 1, 2, 0, 1, 1, false); Trail(flDn2, (5, -1), (10, -2), (13, -1)); int fDown2 = p.Add(flDn2);
		var lp1 = G(1, 0, 4, 3, 1, 1, false); Lean(lp1, 1, 6); int fLoop1 = p.Add(lp1);
		var lp2 = FlipV(G(1, 1, 1, 3, 1, 1, false)); int fLoop2 = p.Add(lp2);          // upside down at the top of the loop
		var sw = G(1, 0, 4, 1, 1, 1, false); Lean(sw, -2, 6); Trail(sw, (15, 2), (15, 6), (15, 10), (14, 13)); int fSwoop = p.Add(sw);
		var zz1 = G(2, 0, 3, 3, 1, 0, false); Lean(zz1, -1, 6); Trail(zz1, (15, 12), (14, 14)); int fZig = p.Add(zz1);
		var zz2 = G(0, 1, 2, 3, 0, 1, false); Lean(zz2, -1, 6); Trail(zz2, (15, 0), (14, -1)); int fZag = p.Add(zz2);

		// ---- dance (47)
		var dA = G(1, 0, 1, 3, 1, 0); Lean(dA, -1, 10); int fDanceA = p.Add(dA);
		var dB = G(2, 1, 1, 3, 0, 1); Lean(dB, 1, 10); int fDanceB = p.Add(dB);
		// ---- sing (48)
		var sg1 = G(1, 0, 1, 1); Note(sg1, 12, -2); int fSing1 = p.Add(sg1);
		var sg2 = G(2, 1, 1, 2); Note(sg2, 13, -2); Note(sg2, 1, 0); int fSing2 = p.Add(sg2);
		// ---- bubble gum (49)
		Frame Gum(int r)
		{
			var f = G(1, 0, r >= 3 ? 4 : 0, 4);
			if (r < 3) FillCircle(f, 7.5f, 8.5f, r + 0.6f, Gum2, GumC);
			else { f.Dot(3, 3, GumC).Dot(12, 4, GumC).Dot(6, 7, GumC).Dot(9, 8, GumC).Dot(5, 9, GumC).Dot(11, 7, GumC); }
			return f;
		}
		int fGum1 = p.Add(Gum(0)), fGum2 = p.Add(Gum(1)), fGum3 = p.Add(Gum(2)), fGumPop = p.Add(Gum(3));
		// ---- ice cream (50)
		Frame IceCream(int k)
		{
			var f = G(1, 0, k == 1 ? 1 : 3, k == 1 ? 4 : 0, 0, 0);
			f.Rect(12, 4, 3, 2, Mint).Dot(12, 4, Sheet).Rect(12, 6, 3, 1, Cone).Rect(13, 7, 1, 2, Cone).Dot(14, 9, Line);
			if (k == 1) f.Dot(10, 7, Mouth).Dot(11, 6, Mouth);                         // licking
			if (k == 2) f.Dot(11, 7, Mint);                                             // a drip
			return f;
		}
		int fIce1 = p.Add(IceCream(0)), fIce2 = p.Add(IceCream(1)), fIce3 = p.Add(IceCream(2));
		// ---- reading (51)
		Frame Read(int page)
		{
			var f = G(1, 0, 7, 4); f.Rect(4, 5, 2, 1, Ink).Rect(10, 5, 2, 1, Ink);         // eyes down
			f.Rect(4, 8, 8, 4, BookBlue).Rect(5, 8, 3, 3, Cone).Rect(8, 8, 3, 3, Cone).Dot(6, 9, Line).Dot(9, 9, Line).Dot(6, 10, Line);
			if (page == 1) f.Rect(8, 7, 2, 2, Cone).Dot(9, 8, Line);
			return f;
		}
		int fRead1 = p.Add(Read(0)), fRead2 = p.Add(Read(1));
		// ---- balloon (52)
		Frame Balloon(int k)
		{
			var f = G(1 + k, k, 1, 0, 0, 1);
			int by = -2 + (k == 1 ? -1 : 0);
			FillCircle(f, 13.5f, by + 2.5f, 2.3f, Red, RedLight);
			f.Dot(13, by + 5, Red).Dot(14, by + 6, Line).Dot(14, by + 7, Line);
			return f;
		}
		int fBal1 = p.Add(Balloon(0)), fBal2 = p.Add(Balloon(1));
		// ---- letting the balloon go (70-72): Boo opens his hand, the balloon becomes a separate
		// little window (a "child") that floats up on its own until it leaves the top of the screen.
		Frame BalloonAlone(int k)                         // same place as in Balloon(0), string swaying
		{
			var f = new Frame();
			FillCircle(f, 13.5f + (k == 1 ? 0.4f : 0f), 0.5f, 2.3f, Red, RedLight);
			f.Dot(13, 3, Red);
			if (k == 0) f.Dot(14, 4, Line).Dot(13, 5, Line).Dot(14, 6, Line).Dot(13, 7, Line);
			else f.Dot(13, 4, Line).Dot(14, 5, Line).Dot(13, 6, Line).Dot(14, 7, Line);
			return f;
		}
		int fBalAway1 = p.Add(BalloonAlone(0)), fBalAway2 = p.Add(BalloonAlone(1));
		int fLetGo = p.Add(G(1, 0, 4, 1, 0, 1));          // hand open, "oh!"
		var lookUp = G(1, 1, 4, 0, 0, 0); lookUp.Rect(4, 2, 2, 1, Sheet).Rect(10, 2, 2, 1, Sheet); lookUp.Rect(4, 1, 2, 2, Ink).Rect(10, 1, 2, 2, Ink);
		int fLookUp = p.Add(lookUp);                       // watches it fly away
		// ---- umbrella in the rain (53)
		Frame Umbrella(int k)
		{
			var f = G(1, k, 3, 0, 0, 1);
			f.Rect(3, -1, 10, 1, Purple).Rect(5, -2, 6, 1, Purple).Dot(4, 0, Purple).Dot(8, 0, Purple).Dot(12, 0, Purple).Dot(7, -2, PurpleLight);
			f.Rect(13, 0, 1, 6, Line);
			foreach (var (x, y) in k == 0 ? new[] { (1, 1), (15, 3), (0, 7), (14, 12) } : new[] { (1, 4), (15, 0), (0, 10), (15, 9) }) f.Dot(x, y, Tear);
			return f;
		}
		int fUmb1 = p.Add(Umbrella(0)), fUmb2 = p.Add(Umbrella(1));
		// ---- snowflakes on the tongue (54)
		Frame Snow(int k)
		{
			var f = G(1, k, k == 2 ? 1 : 4, 4); f.Rect(7, 7, 2, 2, Mouth).Dot(7, 7, Ink).Dot(8, 7, Ink);
			foreach (var (x, y) in new[] { (3, -2 + k), (12, -1 + k), (8, 1 + k) }) f.Dot(x, y, Zz);
			if (k == 2) f.Dot(8, 7, Zz);
			return f;
		}
		int fSnow1 = p.Add(Snow(0)), fSnow2 = p.Add(Snow(1)), fSnow3 = p.Add(Snow(2));
		// ---- juggling stars (55)
		(int x, int y)[] jPath = { (2, 4), (5, -1), (10, -1), (13, 4) };
		Frame JuggleStars(int k)
		{
			var f = G(1, k % 2, 0, 3, k % 2 == 0 ? 1 : 0, k % 2 == 0 ? 0 : 1);
			Star(f, jPath[k % 4].x, jPath[k % 4].y); Star(f, jPath[(k + 2) % 4].x, jPath[(k + 2) % 4].y);
			return f;
		}
		var jug = new int[4]; for (int i = 0; i < 4; i++) jug[i] = p.Add(JuggleStars(i));
		// ---- yo-yo (56)
		Frame YoYo(int d)
		{
			var f = G(1, d % 2, d == 0 ? 3 : 0, 0, 0, 0);
			f.Dot(14, 8, Sheet).Rect(14, 9, 1, d, Ink);
			FillCircle(f, 14.5f, 9.5f + d, 1.3f, Red, RedLight);
			return f;
		}
		int fYo0 = p.Add(YoYo(0)), fYo1 = p.Add(YoYo(2)), fYo2 = p.Add(YoYo(4));
		// ---- selfie (57)
		var sf1 = G(1, 0, 3, 0, 0, 1); sf1.Rect(13, 2, 2, 3, Ink).Dot(13, 2, Zz); int fSelfie = p.Add(sf1);
		var sf2 = G(1, 0, 1, 3, 0, 1); sf2.Rect(13, 2, 2, 3, Ink); Star(sf2, 14, 0); sf2.Dot(11, -1, Glow).Dot(15, 4, Glow); int fFlash = p.Add(sf2);
		// ---- sneeze (58)
		int fAh = p.Add(G(2, 0, 5, 1));
		var ch = G(0, 1, 5, 2); ch.Dot(1, 7, Zz).Dot(0, 9, Zz).Dot(2, 11, Zz); int fChoo = p.Add(Fade(ch, 1));
		// ---- hiccup (59)
		var hc1 = G(0, 0, 4, 1); int fHicLow = p.Add(hc1);
		var hc2 = G(2, 1, 4, 1); hc2.Dot(13, -1, Tear).Dot(14, -2, Tear).Dot(12, -2, Tear).Dot(13, -2, Sheet); int fHicUp = p.Add(hc2);
		// ---- yawn (60)
		int fYawn = p.Add(G(2, 0, 6, 2, 1, 1));
		// ---- tired, sweating (61)
		var tr1 = G(0, 0, 6, 5); tr1.Dot(13, 2, Tear).Dot(13, 3, Tear); int fTired1 = p.Add(tr1);
		var tr2 = G(0, 1, 6, 1); tr2.Dot(13, 3, Tear).Dot(13, 4, Tear).Dot(2, 3, Tear); int fTired2 = p.Add(tr2);
		// ---- blep (62)
		var bl = G(1, 0, 1, 0); bl.Dot(8, 7, Mouth).Dot(8, 8, Mouth); int fBlep = p.Add(bl);
		// ---- wink and kiss (63)
		var wk = G(1, 0, 0, 1); wk.Rect(10, 3, 2, 3, '.').Rect(10, 3, 2, 3, Sheet).Rect(10, 5, 2, 1, Ink); int fWink = p.Add(wk);
		var ks = G(1, 1, 1, 4); ks.Dot(8, 6, Mouth).Dot(7, 6, Heart); HeartAt(ks, 12, -1); int fKiss = p.Add(ks);
		// ---- bounce (64)
		var bn1 = G(0, 0, 5, 0); bn1.Shift(0, 1); bn1.Rect(3, 15, 10, 1, Shadow); int fBoingLow = p.Add(bn1);
		int fBoingHigh = p.Add(G(2, 1, 4, 3, 1, 1));
		// ---- turns into a cloud and back (65)
		Frame Cloud(int k)
		{
			var f = new Frame();
			FillCircle(f, 5, 7, 3.6f, Sheet, Sheet); FillCircle(f, 10.5f, 6, 4.2f, Sheet, Sheet); FillCircle(f, 8, 9, 4f, Sheet, Sheet);
			OutlineAll(f);
			Face(f, 1, 1, 0);
			f.Rect(5, 15, 6, 1, Shadow);
			if (k == 1) f.Dot(2, 13, Tear).Dot(6, 14, Tear).Dot(10, 13, Tear).Dot(13, 14, Tear);
			return f;
		}
		int fCloud1 = p.Add(Cloud(0)), fCloud2 = p.Add(Cloud(1));
		// ---- smelling a flower (66)
		var fl1 = G(1, 0, 3, 0, 0, 1); Flower(fl1, 14, 1); int fFlower1 = p.Add(fl1);
		var fl2 = G(1, 1, 1, 0, 0, 1); Flower(fl2, 13, 3); HeartAt(fl2, 2, -1); int fFlower2 = p.Add(fl2);
		// ---- a tiny friend circles around (67)
		(int x, int y)[] miniPath = { (0, 2), (5, -2), (12, -1), (12, 9) };
		var minis = new int[4];
		for (int i = 0; i < 4; i++) { var f = G(1, i % 2, i < 2 ? 2 : 3, i == 3 ? 3 : 0); Mini(f, miniPath[i].x, miniPath[i].y); minis[i] = p.Add(f); }
		// ---- jelly wobble (68)
		var jl1 = G(0, 0, 1, 3); jl1.Shift(0, 1); jl1.Rect(2, 15, 12, 1, Shadow); Lean(jl1, 1, 8); int fJelly1 = p.Add(jl1);
		var jl2 = G(1, 1, 1, 3); Lean(jl2, -1, 8); int fJelly2 = p.Add(jl2);
		// ---- glows in the dark (69)
		Frame GlowUp(int k)
		{
			var f = G(1 + k, k, 1, 0);
			for (int y = f.MinY; y <= 15; y++) for (int x = 0; x < 16; x++)
			{
				if (f[x, y] == Sheet) f[x, y] = k == 0 ? GlowLight : GlowCyan;
				else if (f[x, y] == Line || f[x, y] == Shade) f[x, y] = GlowDeep;
			}
			Star(f, k == 0 ? 1 : 14, k == 0 ? 0 : 2);
			return f;
		}
		int fGlow1 = p.Add(GlowUp(0)), fGlow2 = p.Add(GlowUp(1));

		// ================================================================ THROWN / RELEASED THINGS (72-96)
		// Each of these spawns a "child": a separate little window with only the object in it,
		// placed exactly where the object was in Boo's frame, that then moves on its own.
		// The front of Boo is the LEFT side of the frame (he faces left; the app mirrors it).
		void StampK(Frame f, int x, int y, params string[] rows) => f.Stamp(x, y, rows.Select(r => r.Replace('.', ' ')).ToArray());

		// ---- paper plane
		void Plane(Frame f, int x, int y, int k)
		{
			if (k == 0) StampK(f, x, y, "....ooo", "..ooWWo", "ooWWWso", "..oosso");
			else StampK(f, x, y, "..ooooo", "ooWWWWo", "..oWsso", "...oo..");
		}
		var phold = G(1, 0, 2, 0, 1, 0); Plane(phold, 0, -3, 0); int fPlaneHold = p.Add(phold);
		var pthrow = G(1, 1, 1, 3, 0, 0); pthrow.Dot(0, 6, Line).Dot(1, 6, '.').Dot(0, 8, Line); int fPlaneThrow = p.Add(pthrow);
		var pc1 = new Frame(); Plane(pc1, 0, -3, 0); int fPlaneFly1 = p.Add(pc1);
		var pc2 = new Frame(); Plane(pc2, 0, -4, 1); int fPlaneFly2 = p.Add(pc2);

		// ---- soap bubbles
		void Bubble(Frame f, float cx, float cy, float r)
		{
			for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
				for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
				{
					float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
					if (d <= r && d > r - 1.1f) f[x, y] = Tear;
				}
			f.Dot((int)(cx - r * 0.4f), (int)(cy - r * 0.4f), GlowLight);
		}
		var bb1 = G(1, 0, 1, 1, 1, 0); bb1.Rect(0, 1, 1, 3, Line); Bubble(bb1, 1.5f, 0f, 1.6f); int fBlow1 = p.Add(bb1);   // wand ring at the front
		var bb2 = G(1, 1, 1, 1, 1, 0); bb2.Rect(0, 1, 1, 3, Line); int fBlow2 = p.Add(bb2);
		var bc1 = new Frame(); Bubble(bc1, 2.5f, -1.5f, 2.4f); int fBubBig1 = p.Add(bc1);
		var bc2 = new Frame(); Bubble(bc2, 3f, -1.5f, 2.2f); int fBubBig2 = p.Add(bc2);
		var bs1 = new Frame(); Bubble(bs1, 6.5f, -2f, 1.5f); int fBubSmall1 = p.Add(bs1);
		var bs2 = new Frame(); Bubble(bs2, 6f, -2f, 1.4f); int fBubSmall2 = p.Add(bs2);
		var bpop = new Frame(); foreach (var (x, y) in new[] { (0, -3), (4, -3), (1, 1), (5, 0), (2, -4) }) bpop.Dot(x, y, Tear); int fBubPop = p.Add(bpop);

		// ---- boomerang (4 spin poses)
		string[][] boom =
		{
			new[] { "nn...", ".nn..", "..nn.", ".nn..", "nn..." },
			new[] { "..n..", ".nnn.", "nn.nn", "n...n", "....." },
			new[] { "...nn", "..nn.", ".nn..", "..nn.", "...nn" },
			new[] { ".....", "n...n", "nn.nn", ".nnn.", "..n.." },
		};
		var bmh = G(1, 0, 2, 0, 1, 0); StampK(bmh, 0, -3, boom[0]); int fBoomHold = p.Add(bmh);
		var bmt = G(1, 1, 2, 3, 0, 0); int fBoomThrow = p.Add(bmt);
		var bmw = G(1, 0, 2, 0, 0, 0); int fBoomWatch = p.Add(bmw);
		var bmc = G(2, 1, 1, 3, 1, 0); StampK(bmc, 0, -3, boom[2]); int fBoomCatch = p.Add(bmc);
		var boomSpin = new int[4];
		for (int i = 0; i < 4; i++) { var f = new Frame(); StampK(f, 0, -3, boom[i]); boomSpin[i] = p.Add(f); }

		// ---- snowball
		void Snowball(Frame f, int x, int y) => StampK(f, x, y, ".oo.", "oWWo", "oWzo", ".oo.");
		var snh = G(1, 0, 2, 0, 1, 0); Snowball(snh, 0, -3); int fSnowHold = p.Add(snh);
		var snt = G(1, 1, 5, 3, 0, 0); int fSnowThrow = p.Add(snt);
		var snc = new Frame(); Snowball(snc, 0, -3); int fSnowFly = p.Add(snc);
		var spl1 = new Frame(); StampK(spl1, 0, 12, "..W..", ".WWW.", "WzWzW"); int fSplat1 = p.Add(spl1);
		var spl2 = new Frame(); StampK(spl2, 0, 13, "W.W.W", ".zWz."); spl2.Dot(2, 11, Zz); int fSplat2 = p.Add(spl2);

		// ---- butterfly
		void Butterfly(Frame f, int x, int y, bool open)
		{
			if (open) StampK(f, x, y, "UV.VU", "UVKVU", ".VKV.", "U.K.U");
			else StampK(f, x, y, ".V.V.", ".UKU.", "..K..", ".V.V.");
		}
		var bfh = G(1, 0, 4, 0); Butterfly(bfh, 6, -4, true); int fBflyHead1 = p.Add(bfh);
		var bfh2 = G(1, 1, 4, 0); Butterfly(bfh2, 6, -4, false); int fBflyHead2 = p.Add(bfh2);
		int fBflyBye = p.Add(G(1, 0, 1, 3, 1, 0));
		var bc_1 = new Frame(); Butterfly(bc_1, 6, -4, true); int fBfly1 = p.Add(bc_1);
		var bc_2 = new Frame(); Butterfly(bc_2, 6, -3, false); int fBfly2 = p.Add(bc_2);

		// ---- blown kiss: a heart floats away
		var kisser = G(1, 0, 1, 4, 1, 0); kisser.Dot(7, 6, Mouth).Dot(1, 5, Heart); int fBlowKiss = p.Add(kisser);
		var kh1 = new Frame(); HeartAt(kh1, 0, 3); int fKissHeart1 = p.Add(kh1);
		var kh2 = new Frame(); HeartAt(kh2, 0, 3); kh2.Dot(1, 3, GumC); int fKissHeart2 = p.Add(kh2);

		// ---- clone: a pale copy splits off and drifts away the other way
		Frame Pale(Frame f)
		{
			for (int y = f.MinY; y <= 15; y++) for (int x = 0; x < 16; x++)
			{
				char c = f[x, y];
				if (c == Shadow) f[x, y] = '.';
				else if (c == Sheet) f[x, y] = GlowLight;
				else if (c == Line || c == Shade) f[x, y] = Tear;
			}
			return f;
		}
		int fShimmer = p.Add(Fade(G(1, 0, 5, 0), 1));
		int fClone1 = p.Add(Pale(G(1, 0, 1, 3)));
		int fClone2 = p.Add(Pale(G(2, 1, 1, 3)));

		// ---- pumpkin: drops it and it rolls away along the ground
		string[][] pumpRoll =
		{
			new[] { ".GG.", "OOOO", "OKOK", "OOOO" },
			new[] { "OOO.", "OKOG", "OOOG", "OKO." },
			new[] { "OOOO", "KOKO", "OOOO", ".GG." },
			new[] { ".OKO", "GOOO", "GOKO", ".OOO" },
		};
		var pmh = G(1, 0, 1, 0); StampK(pmh, 5, 8, pumpRoll[0]); int fPumpHold = p.Add(pmh);
		var pmd = G(1, 1, 4, 1, 1, 1); int fPumpOops = p.Add(pmd);
		var pumpRolls = new int[4];
		for (int i = 0; i < 4; i++) { var f = new Frame(); StampK(f, 5, 11, pumpRoll[i]); pumpRolls[i] = p.Add(f); }

		p.IconFrame = fHappy;

		// ------------------------------------------------------------ animations
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 30, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 25, "random*(screenW-imageW-50)/100+25", "areaH-imageH", 25));

		p.A(1, "walk").Move("-3", "0").Speed("150").Loop("random/4+8")
			.Seq(fFloat1, fFloat2, fFloat3, fFloat4)
			.Then(50, 1).Then(10, 2).Then(10, 11).Then(5, 20).Then(5, 21).Then(4, 22).Then(4, 23).Then(4, 24).Then(3, 26).Then(3, 27)
			.Then(10, 40).Then(3, 47).Then(3, 64).Then(2, 59)
			.Then(8, 16, "taskbar").Then(6, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(25, 12, "vertical").OnBorder(30, 9, "window")
			.OnGravity(100, 5);

		var turn = p.A(2, "turn").Move("0", "0").Speed("110");
		turn.Seq(fSideL, fBack, fSideR);
		turn.Action = "flip";
		turn.Then(100, 1);

		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("45").Loop("30").Seq(fSideL, fBack, fSideR, fFall).Then(100, 4);
		// ghosts drift down gently
		p.A(5, "fall").Move("0", "2", "0", "5").Speed("80").Loop("20").Seq(fFall, fSurprise).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "5").Speed("50").Loop("10").Seq(fFall, fFall, fSurprise).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("120", "200").Seq(fSquash, fLand, fStand, fHappy, fStand).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("150", "260").Seq(fSquash, fSquash, fLand, fCry, fCry, fCry, fBlink, fStand).Then(100, 1);

		p.A(9, "jump").Move("-4", "-9", "-4", "9").Speed("70")
			.Seq(fSquash, fStand2, fWave2, fWave2, fFloat2, fFloat2, fFall, fFall, fSquash, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		p.A(10, "sync").Move("0", "0").Speed("100", "150").Loop("6").Seq(fHappy, fGiggle1, fGiggle2, fWave).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("420", "120")
			.Seq(fStand, fBlink, fStand2, fBlink, fStand)
			.Then(45, 1).Then(8, 15).Then(8, 17).Then(7, 20).Then(7, 21).Then(6, 22).Then(6, 23).Then(5, 24).Then(5, 26).Then(5, 27).Then(4, 28)
			.Then(12, 40)
			.Then(3, 47).Then(3, 48).Then(3, 49).Then(3, 50).Then(3, 51).Then(3, 52).Then(2, 53).Then(2, 54).Then(3, 55).Then(3, 56).Then(3, 57)
			.Then(2, 58).Then(2, 59).Then(3, 60).Then(2, 61).Then(3, 62).Then(3, 63).Then(3, 64).Then(2, 65).Then(3, 66).Then(3, 67).Then(3, 68).Then(2, 69)
			.Then(4, 72).Then(4, 75).Then(4, 81).Then(4, 82).Then(4, 87).Then(4, 90).Then(3, 92).Then(3, 94);

		p.A(12, "climb").Move("0", "-3").Speed("130").Loop("5000", 2)
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
		p.A(17, "wave").Move("0", "0").Speed("160").Loop("4", 1).Seq(fStand, fWave, fWave2, fWave, fWave2).Then(100, 1);

		// --- ghost tricks -----------------------------------------------------
		Anim Seq(int id, string name, int ms, params (int f, int n)[] steps)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			foreach (var (f, n) in steps) a.Seq(Enumerable.Repeat(f, n).ToArray());
			a.Then(100, 1);
			return a;
		}
		Seq(20, "boo", 110, (fStand, 2), (fLookL, 2), (fLookR, 2), (fBooPrep, 3), (fBoo, 6), (fGiggle1, 2), (fGiggle2, 2), (fGiggle1, 2), (fGiggle2, 2), (fHappy, 2));
		Seq(21, "peekaboo", 170, (fStand, 2), (fHide, 5), (fPeek, 3), (fHide, 3), (fPeekBoo, 4), (fGiggle1, 2), (fGiggle2, 2), (fHappy, 2));
		var gg = p.A(22, "giggle").Move("0", "0").Speed("120").Loop("random/3+3", 0);
		gg.Seq(fGiggle1, fGiggle2); gg.Then(100, 1);
		var spin = p.A(23, "spin").Move("0", "0").Speed("80").Loop("random/3+3", 0);
		spin.Seq(fStand, fSideR, fBack, fSideL); spin.Then(70, 1).Then(30, 22);
		Seq(24, "hearts", 160, (fHappy, 2), (fHeart1, 3), (fHeart2, 3), (fHeart1, 3), (fHeart2, 3), (fHappy, 2));
		// 25 vanish: fades out (dither + window opacity), drifts unseen, fades back in
		p.A(25, "vanish").Move("0", "0").Speed("130").Fade(1, 0.3)
			.Seq(fHappy, fGiggle1, fFade1, fFade2, fFade3, fGone)
			.Then(100, 29);
		p.A(29, "drift").Move("-6", "0").Speed("60").Loop("random/4+4", 0).Fade(0.3, 0.3)
			.Seq(fGone, fGone)
			.Then(100, 30).OnBorder(100, 30).OnGravity(100, 30);
		p.A(30, "appear").Move("0", "0").Speed("130").Fade(0.3, 1)
			.Seq(fGone, fFade3, fFade2, fFade1, fPeekBoo, fGiggle1, fGiggle2, fStand)
			.Then(100, 1);
		var cdl = p.A(26, "candle").Move("0", "0").Speed("200").Loop("random/3+4", 0);
		cdl.Seq(fCandle1, fCandle2); cdl.Then(100, 1);
		var pmp = p.A(27, "pumpkin").Move("0", "0").Speed("220").Loop("random/3+3", 0);
		pmp.Seq(fPump1, fPump2, fPump1, fPump2); pmp.Then(100, 1);
		var shv = p.A(28, "shiver").Move("0", "0").Speed("60").Loop("8", 0);
		shv.Seq(fShiver1, fShiver2); shv.Then(100, 15);

		// ================================================================ 30 MORE (ids 40-69)
		// ---- flying around the screen (40-46) --------------------------------------
		// 40 take off: rises until the top of the screen (or a while), then cruises.
		p.A(40, "take off").Move("-2", "-5").Speed("90").Loop("random/4+6", 0)
			.Seq(fUp1, fUp2)
			.Then(100, 41)
			.OnBorder(100, 41, "horizontal").OnBorder(100, 45, "vertical");
		// 41 fly: cruises across the screen, high up; may loop, swoop, zigzag, climb or land.
		p.A(41, "fly").Move("-5", "-1", "-5", "1").Speed("80").Loop("random/4+8", 0)
			.Seq(fFlyA, fFlyA, fFlyB, fFlyB)
			.Then(30, 41).Then(14, 42).Then(14, 43).Then(14, 46).Then(10, 40).Then(18, 44)
			.OnBorder(100, 45, "vertical").OnBorder(100, 44, "horizontal");
		// 42 loop-the-loop
		p.A(42, "loop").Move("-4", "-5", "-4", "5").Speed("90")
			.Seq(fFlyA, fLoop1, fBack, fLoop2, fLoop2, fSideR, fLoop1, fFlyA)
			.Then(100, 41)
			.OnBorder(100, 45, "vertical").OnBorder(100, 44, "horizontal").OnBorder(100, 7, "taskbar").OnBorder(100, 7, "window");
		// 43 swoop: dives and climbs back
		p.A(43, "swoop").Move("-7", "5", "-7", "-5").Speed("70")
			.Seq(fSwoop, fSwoop, fSwoop, fSwoop, fFlyB, fFlyB, fUp1, fUp2)
			.Then(100, 41)
			.OnBorder(100, 45, "vertical").OnBorder(100, 7, "taskbar").OnBorder(100, 7, "window").OnBorder(100, 41, "horizontal");
		// 44 land: drifts down until it touches the taskbar or a window
		p.A(44, "fly down").Move("-3", "4").Speed("90").Loop("200", 0)
			.Seq(fDown1, fDown2)
			.Then(100, 44)
			.OnBorder(100, 7, "taskbar").OnBorder(100, 7, "window").OnBorder(100, 45, "vertical");
		// 45 turn in the air
		var airTurn = p.A(45, "air turn").Move("0", "0").Speed("90");
		airTurn.Seq(fSideL, fBack, fSideR);
		airTurn.Action = "flip";
		airTurn.Then(100, 41);
		// 46 zigzag
		p.A(46, "zigzag").Move("-5", "-3", "-5", "3").Speed("80").Loop("3", 0)
			.Seq(fZig, fZig, fZag, fZag)
			.Then(100, 41)
			.OnBorder(100, 45, "vertical").OnBorder(100, 44, "horizontal").OnBorder(100, 7, "taskbar").OnBorder(100, 7, "window");

		// ---- on the ground (47-69) ----------------------------------------------------
		Anim Show(int id, string name, int ms, params (int f, int n)[] steps)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			foreach (var (f, n) in steps) a.Seq(Enumerable.Repeat(f, n).ToArray());
			a.Then(100, 1);
			return a;
		}
		Anim Cycle(int id, string name, int ms, string loops, params int[] frames)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString()).Loop(loops, 0);
			a.Seq(frames); a.Then(100, 1);
			return a;
		}
		Cycle(47, "dance", 150, "random/3+5", fDanceA, fDanceB);
		Cycle(48, "sing", 220, "random/3+3", fSing1, fSing2, fSing1, fSing2);
		Show(49, "bubble gum", 200, (fStand, 1), (fGum1, 2), (fGum2, 2), (fGum3, 3), (fGumPop, 3), (fSurprise, 2), (fGiggle1, 1), (fGiggle2, 1), (fGiggle1, 1));
		Cycle(50, "ice cream", 240, "random/3+3", fIce1, fIce2, fIce1, fIce3);
		Cycle(51, "read", 380, "random/3+3", fRead1, fRead1, fRead2, fRead1);
		var bal = p.A(52, "balloon").Move("0", "0").Speed("260").Loop("random/3+4", 0);
		bal.Seq(fBal1, fBal2); bal.Then(55, 71).Then(45, 1);        // often lets it go at the end
		// 71 let go: the moment this starts, the app spawns the balloon child (see Childs below)
		p.A(71, "let go").Move("0", "0").Speed("160")
			.Seq(fLetGo, fLetGo, fLetGo, fLookUp, fLookUp, fLookUp, fLookUp, fWave, fWave2, fWave, fWave2, fLookUp, fLookUp, fHappy, fHappy)
			.Then(100, 1);
		// 70 balloon away: played only by the child window — rises and drifts until it leaves the screen
		p.A(70, "balloon away").Move("-1", "-3", "1", "-4").Speed("70").Loop("100", 0)
			.Seq(fBalAway1, fBalAway1, fBalAway2, fBalAway2)
			.Then(100, 70);
		p.Childs.Add((71, "imageX", "imageY", 70));
		Cycle(53, "umbrella", 160, "random/3+5", fUmb1, fUmb2);
		Cycle(54, "snowflakes", 230, "random/3+3", fSnow1, fSnow2, fSnow3, fHappy);
		Cycle(55, "juggle", 140, "random/3+4", jug);
		Cycle(56, "yo-yo", 110, "random/3+5", fYo0, fYo1, fYo2, fYo1);
		Show(57, "selfie", 200, (fStand, 1), (fSelfie, 4), (fFlash, 2), (fSelfie, 2), (fFlash, 2), (fHappy, 2));
		Show(58, "sneeze", 140, (fStand, 1), (fAh, 3), (fStand, 1), (fAh, 4), (fChoo, 3), (fFade1, 1), (fBlink, 2), (fGiggle1, 2), (fStand, 1));
		Show(59, "hiccup", 120, (fStand, 3), (fHicLow, 1), (fHicUp, 2), (fStand, 4), (fHicLow, 1), (fHicUp, 2), (fStand, 3), (fHicLow, 1), (fHicUp, 2), (fSurprise, 2), (fStand, 2));
		Show(60, "yawn", 220, (fStand, 1), (fYawn, 5), (fBlink, 2), (fStand, 1), (fBlink, 1), (fStand, 1));
		Cycle(61, "tired", 300, "random/3+3", fTired1, fTired2);
		Show(62, "blep", 200, (fHappy, 2), (fBlep, 6), (fHappy, 2));
		Show(63, "wink kiss", 180, (fStand, 2), (fWink, 3), (fStand, 1), (fKiss, 5), (fHappy, 2));
		Cycle(64, "bounce", 110, "random/3+4", fBoingLow, fStand, fBoingHigh, fStand);
		Show(65, "cloud", 150, (fHappy, 1), (fFade1, 1), (fFade2, 1), (fCloud1, 5), (fCloud2, 2), (fCloud1, 1), (fCloud2, 2), (fCloud1, 3), (fFade2, 1), (fFade1, 1), (fHappy, 2));
		Show(66, "flower", 260, (fStand, 1), (fFlower1, 3), (fFlower2, 4), (fFlower1, 2), (fFlower2, 3), (fHappy, 2));
		Cycle(67, "tiny friend", 160, "random/3+3", minis);
		Cycle(68, "jelly", 100, "random/3+5", fJelly1, fJelly2);
		Cycle(69, "glow", 220, "random/3+4", fGlow1, fGlow2);

		// ================================================================ THROWN / RELEASED THINGS (72-96)
		// A child window opens when Boo STARTS the animation listed in Childs, so each trick is split
		// into "hold" (Boo has the object) and "release" (the object is gone from his frame and the
		// child appears in its place). Child animations without a next simply close when they end,
		// and children that move off the screen are closed by the app.

		// paper plane: flies straight ahead and out of the screen
		Show(72, "hold plane", 170, (fPlaneHold, 5));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 73);
		Show(73, "throw plane", 150, (fPlaneThrow, 2), (fLookL, 6), (fHappy, 3));
		p.A(74, "plane flight").Move("-6", "-2", "-6", "1").Speed("60").Loop("30", 0)
			.Seq(fPlaneFly1, fPlaneFly1, fPlaneFly2, fPlaneFly2).Then(100, 74);
		p.Childs.Add((73, "imageX", "imageY", 74));

		// soap bubbles: two bubbles rise, wobble and pop
		Show(75, "bubbles", 200, (fBlow1, 3), (fBlow2, 2), (fGiggle1, 2), (fLookL, 4), (fHappy, 2));
		p.A(76, "big bubble").Move("-1", "-2", "1", "-3").Speed("90").Loop("10", 0)
			.Seq(fBubBig1, fBubBig2).Then(100, 78);
		p.A(77, "small bubble").Move("1", "-3", "-1", "-2").Speed("80").Loop("8", 0)
			.Seq(fBubSmall1, fBubSmall2).Then(100, 78);
		p.A(78, "bubble pop").Move("0", "0").Speed("120").Seq(fBubPop);   // no next: the child closes
		p.Childs.Add((75, "imageX", "imageY", 76));
		p.Childs.Add((75, "imageX", "imageY", 77));

		// boomerang: out and back (the speed goes from -7 to +7, so it returns), Boo catches it
		Show(81, "hold boomerang", 170, (fBoomHold, 4));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 79);
		Show(79, "throw boomerang", 160, (fBoomThrow, 2), (fBoomWatch, 10), (fBoomCatch, 4), (fHappy, 2));
		p.A(80, "boomerang flight").Move("-7", "-1", "7", "1").Speed("80")
			.Seq(boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3], boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3],
				 boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3], boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3],
				 boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3], boomSpin[0], boomSpin[1], boomSpin[2], boomSpin[3]);
		p.Childs.Add((79, "imageX", "imageY", 80));

		// snowball: an arc, then it falls and splats on the taskbar or a window
		Show(82, "hold snowball", 170, (fSnowHold, 4));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 83);
		Show(83, "throw snowball", 150, (fSnowThrow, 2), (fLookL, 6), (fGiggle1, 2), (fGiggle2, 2));
		p.A(84, "snowball arc").Move("-6", "-6", "-6", "4").Speed("60")
			.Seq(fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly, fSnowFly)
			.Then(100, 85).OnBorder(100, 86, "taskbar").OnBorder(100, 86, "window");
		p.A(85, "snowball fall").Move("-5", "6").Speed("60").Loop("200", 0)
			.Seq(fSnowFly).Then(100, 85).OnBorder(100, 86, "taskbar").OnBorder(100, 86, "window");
		p.A(86, "splat").Move("0", "0").Speed("160").Seq(fSplat1, fSplat2, fSplat2);
		p.Childs.Add((83, "imageX", "imageY", 84));

		// butterfly: rests on Boo's head, then flutters away (always upwards, so it leaves the screen)
		Show(87, "butterfly", 200, (fStand, 1), (fBflyHead1, 2), (fBflyHead2, 1), (fBflyHead1, 2), (fBflyHead2, 1), (fBflyHead1, 2));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 88);
		Show(88, "butterfly away", 180, (fLookL, 3), (fBflyBye, 2), (fWave, 2), (fWave2, 2), (fHappy, 2));
		p.A(89, "butterfly flight").Move("-3", "-4", "-2", "-1").Speed("90").Loop("12", 0)
			.Seq(fBfly1, fBfly2).Then(100, 89);
		p.Childs.Add((88, "imageX", "imageY", 89));

		// blown kiss: the heart floats forward and fades out
		Show(90, "blow kiss", 170, (fStand, 1), (fBlowKiss, 3), (fHappy, 4), (fGiggle1, 1), (fGiggle2, 1));
		p.A(91, "kiss heart").Move("-3", "-2", "-2", "-3").Speed("80").Loop("8", 0).Fade(1, 0)
			.Seq(fKissHeart1, fKissHeart2);
		p.Childs.Add((90, "imageX", "imageY", 91));

		// clone: Boo shimmers, a pale copy splits off backwards and fades away
		Show(92, "clone", 140, (fShimmer, 3), (fSurprise, 3), (fLookR, 5), (fGiggle1, 2), (fGiggle2, 2), (fStand, 1));
		p.A(93, "clone drift").Move("4", "-1", "5", "-2").Speed("90").Loop("10", 0).Fade(0.8, 0)
			.Seq(fClone1, fClone2);
		p.Childs.Add((92, "imageX", "imageY", 93));

		// pumpkin: oops, it drops and rolls away along the ground
		Show(94, "hold pumpkin", 220, (fPumpHold, 4));
		p.Anims.Last().Next.Clear(); p.Anims.Last().Then(100, 95);
		Show(95, "drop pumpkin", 150, (fPumpOops, 4), (fLookL, 6), (fCry, 3), (fBlink, 1), (fStand, 1));
		p.A(96, "pumpkin roll").Move("-4", "0").Speed("90").Loop("60", 0)
			.Seq(pumpRolls[0], pumpRolls[1], pumpRolls[2], pumpRolls[3]).Then(100, 96);
		p.Childs.Add((95, "imageX", "imageY", 96));

		// 99 kill: fades away waving
		p.A(99, "kill").Move("0", "-1").Speed("130", "220").Fade(1, 0)
			.Seq(fWave, fWave2, fFade1, fFade2, fFade3, fGone)
			.Then(100, 99);

		p.Readme = """
			# Boo the Little Ghost

			## Description

			A shy, friendly little pixel ghost: a round white sheet with big dark eyes,
			rosy cheeks and a wavy hem, floating over a soft shadow.

			He drifts along your taskbar, plays peekaboo, giggles, spins, sends hearts,
			goes invisible and reappears somewhere else, carries a candle or a pumpkin,
			naps when left alone — and sometimes jumps out with a BOO!

			## About pet

			Author: Luan Felipe

			Original pixel art, drawn from scratch for desktopPet. Original character —
			feel free to distribute.

			## Animations

			* float, turn, jump onto windows, drift down gently (soft and hard landings)
			* climb the screen border, sit and sleep on the taskbar
			* blink, look around, wave, giggle, spin, hearts, shiver
			* BOO!, peekaboo, vanish and reappear
			* candle and pumpkin
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Boo, o Fantasminha

			## Descrição

			Um fantasminha de pixel art tímido e amigável: um lençol branco redondinho,
			com olhos grandes, bochechas rosadas e a barra ondulada, flutuando sobre
			uma sombrinha.

			Ele passeia pela barra de tarefas, brinca de esconde-esconde, dá risadinhas,
			gira, manda corações, fica invisível e reaparece em outro lugar, carrega uma
			vela ou uma abóbora, tira um cochilo quando fica sozinho e às vezes aparece
			de surpresa com um BU!

			## Sobre o pet

			Autor: Luan Felipe

			Pixel art original, desenhada do zero para o desktopPet. Personagem
			original — pode distribuir à vontade.

			## Animações

			* flutuar, virar, pular sobre janelas, descer flutuando (pouso suave / brusco)
			* escalar as bordas da tela, sentar e dormir na barra de tarefas
			* piscar, olhar ao redor, dar tchau, risadinha, girar, corações, tremer de frio
			* BU!, esconde-esconde, sumir e reaparecer
			* vela e abóbora
			* arrastar e jogar com o mouse
			""";

		Frame.NewHeadroom = 0;
		return p;
	}
}
