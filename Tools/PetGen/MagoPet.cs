using System.Drawing;

namespace PetGen;

/// <summary>
/// "Mago" — an original pixel-art wizard for DesktopPet: a kindly old wizard with a
/// long white beard, bushy brows and a blue star-spangled robe and pointed hat.
/// Drawn from scratch on a 16-wide grid with 4 rows of headroom for the tall hat.
/// </summary>
static class MagoPet
{
	const char Robe = 'R', RobeDark = 'r', Star = 'Y', Beard = 'W', BeardShade = 'g';
	const char Skin = 'F', SkinDark = 'k', Ink = 'K', Nose = 'P', Shoe = 'U', Wood = 'w';
	const char Cyan = 'C', Magenta = 'M', Paper = 'p', Glass = 'b', GlassLight = 'l', Zz = 'z';
	const char Green = 'G', DarkGreen = 'd', Cauldron = 'Q', Smoke = 'q', Red = 'X', Orange = 'O', OwlBrown = 'o', OwlLight = 'i', PaleYellow = 'y';

	// The wizard, front view. Row 0 of this array is y = -4 (headroom); the shoes stand on y = 15.
	static readonly string[] BaseArt =
	{
		"........rR......",   // -4 hat tip
		".......rRRR.....",   // -3
		"......rRRYRR....",   // -2 star on the hat
		"......rRRRRR....",   // -1
		".....rRRRRRRR...",   //  0
		".....rRYYRRRR...",   //  1 moon
		"....rRRRRRRRRR..",   //  2
		"..rrrrrrrrrrrr..",   //  3 brim
		"....WWFFFFWW....",   //  4 bushy brows
		"....FKFFFFKF....",   //  5 eyes
		"...gWWWPPWWWg...",   //  6 nose and moustache
		"..RgWWWWWWWWgR..",   //  7 beard
		"..RgWWWWWWWWgR..",   //  8
		"..RRgWWWWWWgRR..",   //  9
		"..RRRgWWWWgRRR..",   // 10
		"..RYRRgWWgRRYR..",   // 11 stars on the robe
		"..RRRRRggRRRRR..",   // 12
		"..RRRRRRRRRYRR..",   // 13
		"..rRRYRRRRRRRr..",   // 14 hem
		"...UUU....UUU...",   // 15 shoes
	};

	public static PetDef Build()
	{
		Frame.NewHeadroom = 4;
		var p = new PetDef
		{
			Folder = "mago",
			Title = "Mago the Wizard",
			PetName = "Mago",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Mago is a kindly old pixel wizard with a long white beard.[br]" +
				   "He casts sparkly spells, reads his spellbook, gazes into a crystal ball[br]" +
				   "and teleports around your desktop. Original character — feel free to distribute.",
			Palette = new Dictionary<char, Color>
			{
				[Robe] = Color.FromArgb(58, 92, 204),
				[RobeDark] = Color.FromArgb(34, 54, 142),
				[Star] = Color.FromArgb(250, 210, 70),
				[Beard] = Color.FromArgb(246, 246, 242),
				[BeardShade] = Color.FromArgb(186, 188, 200),
				[Skin] = Color.FromArgb(240, 198, 164),
				[SkinDark] = Color.FromArgb(196, 140, 112),
				[Ink] = Color.FromArgb(30, 26, 32),
				[Nose] = Color.FromArgb(228, 138, 126),
				[Shoe] = Color.FromArgb(112, 70, 40),
				[Wood] = Color.FromArgb(150, 100, 56),
				[Cyan] = Color.FromArgb(120, 222, 255),
				[Magenta] = Color.FromArgb(232, 118, 232),
				[Paper] = Color.FromArgb(250, 240, 214),
				[Glass] = Color.FromArgb(150, 196, 240),
				[GlassLight] = Color.FromArgb(226, 244, 255),
				[Zz] = Color.FromArgb(200, 214, 255),
				[Green] = Color.FromArgb(104, 204, 96),
				[DarkGreen] = Color.FromArgb(46, 128, 60),
				[Cauldron] = Color.FromArgb(54, 54, 66),
				[Smoke] = Color.FromArgb(150, 150, 160),
				[Red] = Color.FromArgb(226, 80, 72),
				[Orange] = Color.FromArgb(240, 150, 50),
				[OwlBrown] = Color.FromArgb(150, 104, 64),
				[OwlLight] = Color.FromArgb(222, 192, 150),
				[PaleYellow] = Color.FromArgb(252, 236, 160),
			},
			Scale = 3,
			TilesX = 8,
		};

		// ------------------------------------------------------------ helpers
		Frame Base()
		{
			var f = new Frame();
			f.Stamp(0, -4, BaseArt);
			return f;
		}
		// 0 open, 1 closed, 2 look left, 3 look right, 4 wide (surprised)
		void Eyes(Frame f, int mode)
		{
			f.Dot(5, 5, Skin).Dot(10, 5, Skin).Dot(4, 5, Skin).Dot(11, 5, Skin);
			switch (mode)
			{
				case 0: f.Dot(5, 5, Ink).Dot(10, 5, Ink); break;
				case 1: f.Dot(5, 5, SkinDark).Dot(10, 5, SkinDark).Dot(4, 5, SkinDark).Dot(11, 5, SkinDark); break;
				case 2: f.Dot(4, 5, Ink).Dot(9, 5, Ink); break;
				case 3: f.Dot(6, 5, Ink).Dot(11, 5, Ink); break;
				case 4: f.Dot(5, 5, Ink).Dot(10, 5, Ink).Dot(4, 4, Beard).Dot(11, 4, Beard).Dot(5, 4, Skin).Dot(10, 4, Skin); break;
			}
		}
		void Mouth(Frame f) { f.Rect(7, 7, 2, 1, Ink); }                         // "o" in the beard
		// arm poses: 0 down, 1 raised, 2 forward/out, 3 on the beard
		void Arm(Frame f, int side, int pose)
		{
			int x = side < 0 ? 1 : 14, o = side < 0 ? 0 : 15;
			switch (pose)
			{
				case 0: f.Rect(x, 8, 1, 2, Robe).Dot(x, 10, Skin); break;
				case 1: f.Rect(x, 5, 1, 3, Robe).Dot(x, 4, Skin); break;
				case 2: f.Dot(x, 8, Robe).Dot(o, 8, Robe).Dot(o, 7, Skin); break;
				case 3: f.Dot(x, 9, Robe).Dot(side < 0 ? 3 : 12, 9, Skin).Dot(side < 0 ? 2 : 13, 9, Robe); break;
			}
		}
		void WandDown(Frame f) { f.Rect(15, 7, 1, 3, Wood).Dot(15, 6, Star); f.Rect(14, 8, 1, 2, Robe).Dot(14, 10, Skin); }
		void WandUp(Frame f, bool glow)
		{
			Arm(f, 1, 1);
			f.Rect(14, 1, 1, 3, Wood).Dot(14, 0, Star);
			if (glow) f.Dot(13, -1, Cyan).Dot(15, -1, Magenta).Dot(14, -2, Star).Dot(15, 1, Cyan);
		}
		void Shoes(Frame f, string row) { f.Rect(0, 15, 16, 1, '.'); f.Stamp(0, 15, row.Replace('.', ' ')); }
		// Squash: everything above the shoes drops `n` rows (crouch, landing, sitting).
		Frame Squash(Frame f, int n)
		{
			var top = f.Clone();
			for (int y = f.MinY; y <= 14; y++) for (int x = 0; x < 16; x++) f[x, y] = '.';
			for (int y = f.MinY; y <= 14 - n; y++) for (int x = 0; x < 16; x++) if (top[x, y] != '.') f[x, y + n] = top[x, y];
			return f;
		}
		void Sparkle(Frame f, int x, int y, char c)
		{
			f.Dot(x, y, c).Dot(x - 1, y, c == Star ? Paper : c).Dot(x + 1, y, c == Star ? Paper : c).Dot(x, y - 1, Paper).Dot(x, y + 1, Paper);
		}

		// ------------------------------------------------------------ frames
		var st = Base(); Eyes(st, 0); Arm(st, -1, 0); WandDown(st);
		int fStand = p.Add(st);

		var bl = Base(); Eyes(bl, 1); Arm(bl, -1, 0); WandDown(bl);
		int fBlink = p.Add(bl);

		Frame Walk(int phase)
		{
			var f = Base(); Eyes(f, 2); Arm(f, -1, 0); WandDown(f);
			if (phase == 1) { Shoes(f, "..UUU......UU..."); }
			if (phase == 2) { Shoes(f, "....UU....UUU..."); }
			if (phase == 3) { Shoes(f, "...UU.....UUU..."); Squash(f, 1); Shoes(f, "...UU.....UUU..."); }
			if (phase == 4) { Shoes(f, "..UUU.....UU...."); Squash(f, 1); Shoes(f, "..UUU.....UU...."); }
			return f;
		}
		int fWalk1 = p.Add(Walk(1)), fWalk2 = p.Add(Walk(3)), fWalk3 = p.Add(Walk(2)), fWalk4 = p.Add(Walk(4));

		var hp = Base(); Eyes(hp, 1); Arm(hp, -1, 1); WandUp(hp, false);
		int fHappy = p.Add(hp);

		var wv = Base(); Eyes(wv, 1); Arm(wv, -1, 1); WandDown(wv);
		int fWave = p.Add(wv);
		var wv2 = Base(); Eyes(wv2, 1); Arm(wv2, -1, 1); WandDown(wv2); wv2.Dot(1, 4, '.').Dot(0, 4, Skin).Dot(1, 5, Robe);
		int fWave2 = p.Add(wv2);

		var sp = Base(); Eyes(sp, 4); Mouth(sp); Arm(sp, -1, 1); WandUp(sp, false);
		int fSurprise = p.Add(sp);

		var cr = Base(); Eyes(cr, 0); Arm(cr, -1, 0); WandDown(cr); Squash(cr, 2);
		int fCrouch = p.Add(cr);

		var jp = Base(); Eyes(jp, 4); Arm(jp, -1, 1); WandUp(jp, false);
		jp.Shift(0, -1); Shoes(jp, "....UU....UU....");
		int fJump = p.Add(jp);

		var fl = Base(); Eyes(fl, 4); Mouth(fl); Arm(fl, -1, 1); Arm(fl, 1, 1);
		fl.Dot(14, 4, Skin); Shoes(fl, "..UU........UU..");
		int fFall = p.Add(fl);

		var ld = Base(); Eyes(ld, 1); Arm(ld, -1, 0); WandDown(ld); Squash(ld, 3);
		int fLand = p.Add(ld);

		Frame Drag(int k)
		{
			var f = Base(); Eyes(f, 4); Mouth(f); Arm(f, -1, 1); Arm(f, 1, 1);
			Shoes(f, k == 0 ? "...UU.....UU...." : "....UU...UU.....");
			if (k == 1) f.Shift(1, 0);
			return f;
		}
		int fDrag1 = p.Add(Drag(0)), fDrag2 = p.Add(Drag(1));

		Frame Climb(int k)
		{
			var f = Base(); Eyes(f, 3); Arm(f, -1, k == 0 ? 1 : 0); Arm(f, 1, k == 0 ? 0 : 1);
			Shoes(f, k == 0 ? "...UU......UUU.." : "...UUU.....UU...");
			return f;
		}
		int fClimb1 = p.Add(Climb(0)), fClimb2 = p.Add(Climb(1));

		var sit = Base(); Eyes(sit, 0); Arm(sit, -1, 0); WandDown(sit); Squash(sit, 3); Shoes(sit, ".UUU........UUU.");
		int fSit = p.Add(sit);

		Frame Sleep(int z)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 0); WandDown(f); Squash(f, 3); Shoes(f, ".UUU........UUU.");
			if (z >= 1) f.Dot(14, -1, Zz).Dot(15, -2, Zz);
			if (z >= 2) f.Rect(13, -4, 3, 1, Zz).Dot(14, -3, Zz).Dot(13, -2, Zz).Rect(13, -2, 3, 1, Zz);
			return f;
		}
		int fSleep1 = p.Add(Sleep(0)), fSleep2 = p.Add(Sleep(1)), fSleep3 = p.Add(Sleep(2));

		// Spell: points the wand, raises it, a burst of stars and sparkles.
		var c1 = Base(); Eyes(c1, 3); Arm(c1, -1, 0); Arm(c1, 1, 2); c1.Rect(15, 4, 1, 3, Wood).Dot(15, 3, Star);
		int fCast1 = p.Add(c1);
		var c2 = Base(); Eyes(c2, 0); Arm(c2, -1, 1); WandUp(c2, true);
		int fCast2 = p.Add(c2);
		Frame Burst(int k)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 1); WandUp(f, true);
			var spots = k == 0
				? new[] { (11, -3, Star), (2, -2, Cyan), (13, 6, Magenta) }
				: new[] { (2, -3, Magenta), (12, -3, Cyan), (1, 1, Star), (14, 7, Star) };
			foreach (var (x, y, c) in spots) Sparkle(f, x, y, c);
			return f;
		}
		int fBurst1 = p.Add(Burst(0)), fBurst2 = p.Add(Burst(1));

		// Poof (teleport): dissolves into sparkles, a blank frame, then reappears.
		Frame Poof(int k)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 1); WandUp(f, true);
			for (int y = f.MinY; y <= 15; y++)
				for (int x = 0; x < 16; x++)
					if (f[x, y] != '.' && ((x * 7 + y * 3) % 5) < k + 1) f[x, y] = '.';
			var dots = new[] { (2, 0, Cyan), (13, 2, Magenta), (4, 10, Star), (11, 12, Cyan), (7, -2, Magenta), (1, 13, Star), (14, 9, Cyan) };
			for (int i = 0; i < dots.Length; i++)
				if (i % 3 <= k) { var (x, y, c) = dots[i]; Sparkle(f, x, y - k, c); }
			return f;
		}
		int fPoof1 = p.Add(Poof(1)), fPoof2 = p.Add(Poof(2)), fPoof3 = p.Add(Poof(3));
		var empty = new Frame();
		empty.Dot(8, 6, Cyan).Dot(6, 9, Magenta).Dot(10, 3, Star);        // last glimmer
		int fGone = p.Add(empty);
		int fEmpty = p.Add(new Frame());

		// Spellbook: reads an open book, turns a page.
		Frame Book(int page)
		{
			var f = Base(); Eyes(f, 0); f.Dot(5, 5, Skin).Dot(10, 5, Skin).Dot(5, 6, Ink).Dot(10, 6, Ink);  // eyes down
			f.Rect(3, 9, 10, 1, Wood).Rect(4, 8, 4, 2, Paper).Rect(8, 8, 4, 2, Paper).Rect(3, 10, 10, 1, Wood);
			f.Dot(5, 8, BeardShade).Dot(10, 8, BeardShade);                  // lines of text
			f.Dot(2, 9, Skin).Dot(13, 9, Skin).Dot(1, 8, Robe).Dot(14, 8, Robe);
			if (page == 1) f.Rect(8, 6, 2, 2, Paper).Dot(9, 7, BeardShade);  // page lifting
			if (page == 2) f.Rect(6, 6, 2, 2, Paper);
			return f;
		}
		int fBook1 = p.Add(Book(0)), fBook2 = p.Add(Book(1)), fBook3 = p.Add(Book(2));
		var bk = Base(); Eyes(bk, 4); Mouth(bk); bk.Rect(3, 9, 10, 1, Wood).Rect(4, 8, 4, 2, Paper).Rect(8, 8, 4, 2, Paper).Rect(3, 10, 10, 1, Wood);
		bk.Dot(2, 9, Skin).Dot(13, 9, Skin); Sparkle(bk, 8, 5, Star);
		int fBookAha = p.Add(bk);

		// Crystal ball: gazes into a glowing ball held in both hands.
		Frame Ball(int glow)
		{
			var f = Base(); Eyes(f, glow == 2 ? 4 : 0);
			f.Rect(6, 8, 4, 1, Glass).Rect(5, 9, 6, 2, Glass).Rect(6, 11, 4, 1, Glass);
			f.Dot(6, 9, GlassLight).Dot(7, 8, GlassLight);
			f.Rect(5, 12, 6, 1, Wood);                                       // stand
			f.Dot(4, 10, Skin).Dot(11, 10, Skin).Dot(3, 9, Robe).Dot(12, 9, Robe);
			if (glow >= 1) f.Dot(8, 10, glow == 1 ? Cyan : Magenta).Dot(9, 9, Cyan);
			if (glow == 2) { Sparkle(f, 3, 6, Cyan); Sparkle(f, 12, 6, Magenta); }
			return f;
		}
		int fBall1 = p.Add(Ball(0)), fBall2 = p.Add(Ball(1)), fBall3 = p.Add(Ball(2));

		// Levitate: floats with sparkles under the shoes.
		Frame Float(int k)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 2); Arm(f, 1, 2);
			f.Shift(0, -1);
			f.Dot(k == 0 ? 4 : 5, 15, Cyan).Dot(k == 0 ? 11 : 10, 15, Magenta).Dot(8, 15, k == 0 ? Star : Cyan);
			return f;
		}
		int fFloat1 = p.Add(Float(0)), fFloat2 = p.Add(Float(1));

		// Thinking: strokes the beard.
		Frame Think(int k)
		{
			var f = Base(); Eyes(f, k == 0 ? 3 : 1); Arm(f, -1, 0); Arm(f, 1, 3);
			f.Dot(12, 9 + k, Skin).Dot(12, 9 + (1 - k), BeardShade);
			if (k == 1) f.Dot(14, 1, Ink).Dot(15, 0, Ink);
			return f;
		}
		int fThink1 = p.Add(Think(0)), fThink2 = p.Add(Think(1));

		// Kill: a last poof.
		int fDie1 = fPoof2, fDie2 = fPoof3;

		// ================================================================ MORE TRICKS
		// Shared helpers for the extra animations.
		void Clear(Frame f, int y0, int y1) { for (int y = y0; y <= y1; y++) for (int x = 0; x < 16; x++) f[x, y] = '.'; }
		void NoHat(Frame f)                        // bald head with white tufts
		{
			Clear(f, f.MinY, 3);
			f.Rect(5, 2, 6, 1, Skin).Rect(4, 3, 8, 1, Skin).Dot(3, 4, Beard).Dot(12, 4, Beard).Dot(4, 3, Beard).Dot(11, 3, Beard);
		}
		void HatAt(Frame f, int dx, int dy)       // the normal hat, moved
		{
			for (int j = 0; j < 8; j++)
				for (int i = 0; i < 16; i++)
					if (BaseArt[j][i] != '.') f[i + dx, j - 4 + dy] = BaseArt[j][i];
		}
		void BigHat(Frame f, int brimY, int hw, int h, int lean = 0)
		{
			Clear(f, f.MinY, brimY);                                  // everything above the brim is inside the hat
			f.Rect(8 - hw, brimY, 2 * hw, 1, RobeDark);
			for (int k = 1; k < h; k++)
			{
				double t = (double)k / h;
				int half = Math.Max(1, (int)Math.Round((hw - 1) * (1 - t)));
				int cx = 8 + (int)Math.Round(lean * t * t);
				f.Rect(cx - half, brimY - k, 2 * half, 1, Robe).Dot(cx - half, brimY - k, RobeDark);
			}
			f.Dot(8 - hw / 3, brimY - h / 3, Star).Dot(8 + hw / 3, brimY - h / 2, Star).Dot(8, brimY - 2 * h / 3, Star).Dot(9, brimY - 2 * h / 3, Star);
		}
		void UpsideHat(Frame f, int y)            // hat held upside down in front
		{
			f.Rect(3, y, 10, 1, RobeDark).Rect(4, y + 1, 8, 1, Robe).Rect(5, y + 2, 6, 1, Robe).Rect(6, y + 3, 4, 1, Robe).Dot(7, y + 2, Star);
			f.Dot(2, y, Skin).Dot(13, y, Skin);
		}
		Frame Stand(int eyes = 0)
		{
			var f = Base(); Eyes(f, eyes); Arm(f, -1, 0); WandDown(f); return f;
		}
		void Recolor(Frame f, char from, char to) { for (int y = f.MinY; y <= 15; y++) for (int x = 0; x < 16; x++) if (f[x, y] == from) f[x, y] = to; }

		// ---- 1 hat swallow: the hat grows, gulps the wizard, shrinks back, he pops out
		var hw0 = Stand(); Clear(hw0, hw0.MinY, 3); HatAt(hw0, 1, 0);
		int hsWobble1 = p.Add(hw0);
		var hw1 = Stand(); Clear(hw1, hw1.MinY, 3); HatAt(hw1, -1, 0);
		int hsWobble2 = p.Add(hw1);
		Frame Swallow(int brimY, int hw, int h, int lean, bool eyes)
		{
			var f = Base(); Eyes(f, 4); Arm(f, -1, 1); Arm(f, 1, 1);
			BigHat(f, brimY, hw, h, lean);
			if (eyes) f.Dot(5, brimY + 2, Ink).Dot(10, brimY + 2, Ink);
			return f;
		}
		int hsGrow1 = p.Add(Swallow(5, 7, 10, 1, false));
		int hsGrow2 = p.Add(Swallow(8, 8, 12, -1, false));
		int hsGrow3 = p.Add(Swallow(11, 8, 15, 2, false));
		int hsGulp = p.Add(Swallow(15, 8, 19, 0, false));
		var gs = new Frame(); BigHat(gs, 15, 8, 15, -2); gs.Dot(7, 9, Star).Dot(6, 10, Star);   // gulp: squashed hat
		int hsSquash = p.Add(gs);
		Frame LoneHat(int brimY, int hw, int h, int lean)
		{
			var f = new Frame(); BigHat(f, brimY, hw, h, lean); return f;
		}
		int hsShrink1 = p.Add(LoneHat(15, 7, 13, 1));
		int hsShrink2 = p.Add(LoneHat(15, 6, 9, 0));
		int hsHop = p.Add(LoneHat(13, 6, 9, -1));
		int hsWiggle = p.Add(LoneHat(15, 6, 9, 2));
		var po = Base(); Eyes(po, 4); Mouth(po); Arm(po, -1, 1); Arm(po, 1, 1); Clear(po, po.MinY, 3); HatAt(po, 0, -3);
		Sparkle(po, 2, 6, Star); Sparkle(po, 13, 6, Cyan);
		int hsPop = p.Add(po);
		var dz = Stand(1); dz.Dot(3, 0, Star).Dot(12, -1, Cyan).Dot(8, -4, Magenta);   // dizzy stars
		int hsDizzy1 = p.Add(dz);
		var dz2 = Stand(1); dz2.Dot(12, 0, Star).Dot(3, -1, Magenta).Dot(9, -3, Cyan);
		int hsDizzy2 = p.Add(dz2);

		// ---- 2 rabbit out of the hat
		Frame Rabbit(int stage)
		{
			var f = Base(); Eyes(f, stage >= 2 ? 4 : 0); NoHat(f);
			if (stage == 0) { WandUp(f, true); f.Dot(1, 9, Skin); UpsideHat(f, 9); return f; }
			UpsideHat(f, 10);
			if (stage >= 1) f.Rect(6, 7, 1, 3, Beard).Rect(9, 7, 1, 3, Beard).Dot(6, 8, Nose).Dot(9, 8, Nose);     // ears
			if (stage >= 2) f.Rect(6, 9, 4, 1, Beard).Dot(7, 9, Ink).Dot(8, 9, Ink);                                 // head
			if (stage == 3)
			{
				Clear(f, 7, 9); f.Stamp(0, 6, "...gWWWPPWWWg...", "..RgWWWWWWWWgR..", "..RgWWWWWWWWgR..", "..RRgWWWWWWgRR..");
				Eyes(f, 1);
				f.Stamp(12, 10, "g.g.", "WgWg", "WPWP", "gWWg", "KWKW", "gWWg");   // bunny on the floor, grey outline
			}
			return f;
		}
		int rbTap = p.Add(Rabbit(0)), rbEars = p.Add(Rabbit(1)), rbHead = p.Add(Rabbit(2)), rbOut = p.Add(Rabbit(3));

		// ---- 3 juggling magic orbs
		(int x, int y)[] orbPath = { (2, 6), (4, 0), (8, -3), (12, 0), (14, 6) };
		Frame Juggle(int k)
		{
			var f = Base(); Eyes(f, k % 2 == 0 ? 0 : 3); Arm(f, -1, k % 2 == 0 ? 1 : 0); Arm(f, 1, k % 2 == 0 ? 0 : 1);
			char[] orbs = { Cyan, Magenta, Star };
			for (int i = 0; i < 3; i++)
			{
				var (x, y) = orbPath[(k + i * 2) % orbPath.Length];
				f.Rect(x - 1, y - 1, 2, 2, orbs[i]).Dot(x - 1, y - 1, GlassLight);
			}
			return f;
		}
		var juggle = new int[5];
		for (int i = 0; i < 5; i++) juggle[i] = p.Add(Juggle(i));

		// ---- 4 brewing a potion in a cauldron
		Frame Brew(int k)
		{
			var f = Base(); Eyes(f, 3); Arm(f, -1, 0);
			f.Rect(1, 10, 14, 1, Cauldron).Rect(2, 10, 12, 1, Green).Rect(1, 11, 14, 4, Cauldron).Dot(3, 12, Smoke);
			f.Dot(2, 15, Cauldron).Dot(13, 15, Cauldron);
			f.Rect(14, 6, 1, 2, Robe).Dot(14, 8, Skin).Rect(k % 2 == 0 ? 12 : 11, 7, 1, 3, Wood);    // stirring stick
			int[][] bub = { new[] { 5, 9, 9, 8 }, new[] { 7, 8, 4, 7 }, new[] { 9, 7, 6, 9 }, new[] { 4, 6, 10, 9 } };
			var b = bub[k % 4];
			f.Dot(b[0], b[1], Green).Dot(b[2], b[3], DarkGreen);
			if (k % 2 == 1) f.Dot(6, 4 - k % 3, Green).Dot(10, 3, DarkGreen);
			return f;
		}
		var brew = new int[4];
		for (int i = 0; i < 4; i++) brew[i] = p.Add(Brew(i));

		// ---- 5 drinks a potion and turns green for a moment
		Frame Potion(int stage)
		{
			var f = Base(); Arm(f, -1, 0);
			if (stage == 0) { Eyes(f, 3); f.Rect(14, 6, 1, 2, Robe).Dot(14, 8, Skin).Rect(14, 5, 2, 2, Green).Dot(15, 4, Glass); }
			if (stage == 1) { Eyes(f, 1); f.Rect(13, 6, 1, 2, Robe).Rect(10, 6, 2, 2, Green).Dot(12, 6, Glass).Dot(12, 7, Skin); }
			if (stage >= 2) { Eyes(f, 4); Mouth(f); WandDown(f); Recolor(f, Robe, Green); Recolor(f, RobeDark, DarkGreen); }
			if (stage == 3) { f.Dot(13, 4, GlassLight).Dot(14, 2, GlassLight).Dot(13, 0, GlassLight); }   // hiccup bubbles
			return f;
		}
		int ptLook = p.Add(Potion(0)), ptDrink = p.Add(Potion(1)), ptGreen = p.Add(Potion(2)), ptHic = p.Add(Potion(3));

		// ---- 6 sneeze: the hat flies off
		var sn1 = Stand(1); sn1.Shift(0, -1); Shoes(sn1, "...UUU....UUU...");
		int snAh = p.Add(sn1);
		var sn2 = Base(); Eyes(sn2, 1); Mouth(sn2); Arm(sn2, -1, 2); WandDown(sn2); Clear(sn2, sn2.MinY, 3); HatAt(sn2, 1, -4);
		sn2.Rect(4, 4, 8, 1, Skin).Dot(4, 4, Beard).Dot(11, 4, Beard);
		Sparkle(sn2, 1, 7, Cyan); sn2.Dot(0, 5, Magenta).Dot(2, 3, Star);
		int snChoo = p.Add(sn2);
		var sn3 = Stand(4); NoHat(sn3); HatAt(sn3, 0, -2); Clear(sn3, -4, -4);
		int snHatFall = p.Add(sn3);

		// ---- 7 broomstick ride
		Frame Broom(int k)
		{
			var f = Base(); Eyes(f, 2); Arm(f, -1, 2); Arm(f, 1, 0);
			Squash(f, 2); Clear(f, 15, 15);
			f.Rect(0, 13, 13, 1, Wood).Stamp(12, 12, "YYYY", "YYYY", "YYY.").Dot(12, 13, Shoe);
			f.Dot(4, 14, Shoe).Dot(9, 14, Shoe);
			f.Dot(15, 11 + k, Paper).Dot(14, 15 - k, Paper);                                      // dust trail
			if (k == 1) f.Shift(0, -1);
			return f;
		}
		int brA = p.Add(Broom(0)), brB = p.Add(Broom(1));

		// ---- 8 rain cloud over his head
		Frame Rain(int k, bool sad)
		{
			var f = Stand(sad ? 1 : 3);
			f.Rect(3, -4, 10, 2, Smoke).Rect(4, -5 + 1, 3, 1, Smoke).Dot(3, -4, '.').Dot(12, -4, '.');
			for (int i = 0; i < 4; i++) f.Dot(4 + i * 3, -2 + (k + i) % 3, Cyan);
			if (sad) f.Dot(4, 6, Cyan);
			return f;
		}
		int rnCloud = p.Add(Rain(0, false)), rnA = p.Add(Rain(1, true)), rnB = p.Add(Rain(2, true));
		var sh = Stand(1); sh.Shift(1, 0); sh.Dot(0, 2, Cyan).Dot(15, 4, Cyan).Dot(1, 9, Cyan);   // shakes off
		int rnShake = p.Add(sh);

		// ---- 9 dance with a sparkle trail
		Frame Dance(int k)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, k == 0 ? 1 : 0);
			if (k == 0) WandDown(f); else WandUp(f, false);
			Shoes(f, k == 0 ? "..UU.....UUU...." : "....UUU....UU...");
			if (k == 1) f.Shift(1, 0);
			f.Dot(k == 0 ? 0 : 15, 3, Star).Dot(k == 0 ? 1 : 14, 1, Cyan).Dot(k == 0 ? 0 : 15, -1, Magenta);
			return f;
		}
		int dcA = p.Add(Dance(0)), dcB = p.Add(Dance(1));

		// ---- 10 fireworks
		Frame Firework(int k)
		{
			var f = Base(); Eyes(f, k >= 2 ? 1 : 3); Arm(f, -1, 0); WandUp(f, k == 0);
			if (k == 1) f.Dot(11, -2, Star).Dot(11, -1, Orange);
			if (k == 2) foreach (var (x, y) in new[] { (5, -4), (3, -2), (7, -2), (5, 0), (4, -3), (6, -3), (4, -1), (6, -1) }) f.Dot(x, y, x % 2 == 0 ? Red : Star);
			if (k == 3) foreach (var (x, y) in new[] { (5, -4), (1, -2), (9, -2), (5, 1), (2, -4), (8, -4), (2, 0), (8, 0) }) f.Dot(x, y, y % 2 == 0 ? Magenta : Cyan);
			return f;
		}
		var fw = new int[4];
		for (int i = 0; i < 4; i++) fw[i] = p.Add(Firework(i));

		// ---- 11 oops, a frog
		Frame Frog(int k)
		{
			var f = Stand(k == 0 ? 3 : 4);
			if (k == 0) { Clear(f, 4, 10); f.Stamp(0, 4, "....WWFFFFWW....", "....FKFFFFKF....", "...gWWWPPWWWg...", "..RgWWWWWWWWgR..", "..RgWWWWWWWWgR..", "..RRgWWWWWWgRR..", "..RRRgWWWWgRRR.."); Eyes(f, 3); WandDown(f); Sparkle(f, 14, 13, Green); return f; }
			Mouth(f);
			int fx = k == 1 ? 12 : 13, fy = k == 2 ? 10 : 12;
			f.Stamp(fx, fy, "GKG.", "GGGG", "dGGd").Dot(fx, fy + 3, DarkGreen).Dot(fx + 3, fy + 3, DarkGreen);
			return f;
		}
		int fgPoof = p.Add(Frog(0)), fgSit = p.Add(Frog(1)), fgHop = p.Add(Frog(2));

		// ---- 12 sweeping bow with the hat
		var bw1 = Base(); Eyes(bw1, 1); NoHat(bw1); Arm(bw1, -1, 0); Squash(bw1, 2); UpsideHat(bw1, 11);   // bows low, hat held to the chest
		int bwDown = p.Add(bw1);
		var bw2 = Base(); Eyes(bw2, 1); NoHat(bw2); Arm(bw2, -1, 0); Arm(bw2, 1, 1); HatAt(bw2, 4, -2);
		int bwUp = p.Add(bw2);

		// ---- 13 an owl lands on his arm
		void Owl(Frame f, int x, int y, bool wings)
		{
			f.Stamp(x, y, ".oo.", "iKKi", "oiio", ".oo.");
			if (wings) f.Dot(x - 1, y + 1, OwlBrown).Dot(x + 4, y + 1, OwlBrown).Dot(x - 1, y, OwlBrown).Dot(x + 4, y, OwlBrown);
		}
		var ow1 = Stand(2); Owl(ow1, 0, -4, true);
		int owFly = p.Add(ow1);
		var ow2 = Base(); Eyes(ow2, 2); Arm(ow2, -1, 1); WandDown(ow2); Owl(ow2, 0, 0, false);
		int owPerch = p.Add(ow2);
		var ow3 = Base(); Eyes(ow3, 1); Arm(ow3, -1, 1); WandDown(ow3); Owl(ow3, 0, 0, false); ow3.Dot(1, 1, OwlLight).Dot(2, 1, OwlLight);  // blinks
		int owBlink = p.Add(ow3);

		// ---- 14 stargazing with a telescope
		Frame Scope(int k)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 0); f.Dot(10, 5, Ink);
			for (int i = 0; i < 5; i++) f.Dot(11 + i, 4 - i, i < 2 ? Orange : Star);
			f.Dot(13, 6, Skin).Dot(14, 7, Robe);
			if (k == 1) f.Dot(2, -3, Star).Dot(1, -3, PaleYellow).Dot(3, -3, PaleYellow).Dot(2, -4, PaleYellow).Dot(2, -2, PaleYellow);
			if (k == 0) f.Dot(2, -3, PaleYellow);
			return f;
		}
		int scA = p.Add(Scope(0)), scB = p.Add(Scope(1));

		// ---- 15 yawn and stretch
		var yw = Base(); Eyes(yw, 1); Arm(yw, -1, 1); Arm(yw, 1, 1); yw.Rect(7, 7, 2, 2, Ink); yw.Shift(0, -1); Shoes(yw, "...UUU....UUU...");
		int ywStretch = p.Add(yw);
		var yw2 = Stand(1); Squash(yw2, 1); yw2.Rect(7, 8, 2, 1, Ink);
		int ywRelax = p.Add(yw2);

		// ---- 16 blowing magic bubbles
		Frame Bubbles(int k)
		{
			var f = Base(); Eyes(f, 3); Arm(f, -1, 0);
			f.Rect(14, 6, 1, 2, Robe).Dot(13, 5, Skin).Rect(13, 3, 1, 2, Wood);
			f.Rect(12, 0, 3, 1, Glass).Dot(12, 1, Glass).Dot(14, 1, Glass).Rect(12, 2, 3, 1, Glass);   // wand loop
			int[][] bubs = { new[] { 10, -2, 3, 0 }, new[] { 8, -4, 4, -2 }, new[] { 6, -4, 13, -4 } };
			var b = bubs[k % 3];
			f.Dot(b[0], b[1], GlassLight).Dot(b[0] + 1, b[1] + 1, Glass).Dot(b[2], b[3], Glass).Dot(b[2] + 1, b[3], GlassLight);
			return f;
		}
		var bubbles = new int[3];
		for (int i = 0; i < 3; i++) bubbles[i] = p.Add(Bubbles(i));

		// ---- 17 spell backfires: zap, soot, smoke, dazed
		var zp = Base(); Eyes(zp, 4); Mouth(zp); Arm(zp, -1, 1); WandUp(zp, false);
		foreach (var (x, y) in new[] { (14, -1), (13, -2), (14, -3), (12, 0), (11, 1) }) zp.Dot(x, y, Star);
		int bfZap = p.Add(zp);
		Frame Soot(int k)
		{
			var f = Stand(k == 0 ? 4 : 1);
			Recolor(f, Beard, BeardShade); Recolor(f, Robe, RobeDark);
			f.Dot(4, -1 - k, Smoke).Dot(5, -2 - k, Smoke).Dot(11, -2 + k, Smoke).Dot(12, -3 + k, Smoke).Dot(8, -4 + k, Smoke);
			if (k == 1) f.Dot(5, 5, Ink).Dot(6, 4, '.').Dot(10, 5, Ink);
			return f;
		}
		int bfSoot1 = p.Add(Soot(0)), bfSoot2 = p.Add(Soot(1));

		// ---- 18 reading a long scroll
		Frame Scroll(int len)
		{
			var f = Base(); f.Dot(5, 5, Skin).Dot(10, 5, Skin).Dot(5, 6, Ink).Dot(10, 6, Ink);
			f.Rect(3, 8, 10, 1, Wood).Dot(2, 8, Skin).Dot(13, 8, Skin);
			f.Rect(4, 9, 8, len, Paper);
			for (int y = 10; y < 9 + len; y += 2) f.Rect(5, y, 4 + (y % 3), 1, BeardShade);
			f.Rect(3, 9 + len, 10, 1, Wood);
			return f;
		}
		int slShort = p.Add(Scroll(2)), slMid = p.Add(Scroll(4)), slLong = p.Add(Scroll(6));

		// ---- 19 holding a lantern
		Frame Lantern(int k)
		{
			var f = Base(); Eyes(f, 3); Arm(f, -1, 0);
			f.Rect(14, 6, 1, 2, Robe).Dot(14, 8, Skin).Dot(14, 9, Wood);
			f.Rect(13, 10, 3, 1, Wood).Rect(13, 11, 3, 2, Star).Dot(14, 11, k == 0 ? PaleYellow : Orange).Rect(13, 13, 3, 1, Wood);
			var glow = k == 0 ? new[] { (12, 11), (14, 14), (15, 9) } : new[] { (12, 12), (12, 9), (15, 14) };
			foreach (var (x, y) in glow) f.Dot(x, y, PaleYellow);
			return f;
		}
		int ltA = p.Add(Lantern(0)), ltB = p.Add(Lantern(1));

		// ---- 20 shrinks to a tiny wizard and grows back
		Frame Tiny(int hop)
		{
			var f = new Frame();
			f.Stamp(4, 7 - hop, "...rR...", "..rRRR..", ".rRYRRR.", "rrrrrrrr", ".WKFFKW.", ".RWWWWR.", ".RRWWRR.", ".rRYRRr.", "..UU.UU.");
			if (hop == 1) Clear(f, 15, 15);
			return f;
		}
		int tnStand = p.Add(Tiny(0)), tnHop = p.Add(Tiny(1));

		// ---- 21 rainbow spell
		Frame Rainbow(int reach)
		{
			var f = Base(); Eyes(f, 1); Arm(f, -1, 0); WandUp(f, true);
			char[] bands = { Red, Orange, Star, Green, Cyan, Magenta };
			for (int b = 0; b < bands.Length; b++)
				for (int x = 0; x <= reach; x++)
				{
					int y = -4 + b + Math.Abs(x - 7) / 3;
					if (f[x, y] == '.') f.Dot(x, y, bands[b]);
				}
			return f;
		}
		int rbw1 = p.Add(Rainbow(5)), rbw2 = p.Add(Rainbow(10)), rbw3 = p.Add(Rainbow(15));

		p.IconFrame = fStand;

		// ------------------------------------------------------------ animations
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 35, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 20, "random*(screenW-imageW-50)/100+25", "areaH-imageH", 23));

		p.A(1, "walk").Move("-2", "0").Speed("170").Loop("random/4+8")
			.Seq(fWalk1, fWalk2, fWalk3, fWalk4)
			.Then(50, 1).Then(10, 2).Then(10, 11).Then(6, 20).Then(4, 21).Then(5, 24).Then(4, 25).Then(3, 26).Then(4, 28)
			.Then(4, 37).Then(3, 31).Then(3, 39).Then(2, 36).Then(2, 45)
			.Then(8, 16, "taskbar").Then(6, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window")
			.OnGravity(100, 5);

		var turn = p.A(2, "turn").Move("0", "0").Speed("130");
		turn.Seq(fStand, fBlink, fStand);
		turn.Action = "flip";
		turn.Then(100, 1);

		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("45").Loop("30").Seq(fJump, fFall, fLand, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fFall).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("120", "200").Seq(fLand, fLand, fCrouch, fStand, fStand).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("150", "260").Seq(fLand, fLand, fLand, fSurprise, fSurprise, fStand).Then(80, 1).Then(20, 13);

		p.A(9, "jump").Move("-5", "-9", "-5", "9").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		p.A(10, "sync").Move("0", "0").Speed("100", "150").Loop("6").Seq(fHappy, fCast2, fBurst1, fBurst2).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("450", "120")
			.Seq(fStand, fBlink, fStand, fBlink, fStand)
			.Then(50, 1).Then(10, 20).Then(8, 21).Then(10, 24).Then(8, 25).Then(6, 26).Then(8, 28)
			.Then(5, 31).Then(4, 32).Then(4, 33).Then(4, 34).Then(3, 35).Then(3, 36).Then(4, 38).Then(4, 39).Then(4, 40)
			.Then(3, 41).Then(3, 42).Then(4, 43).Then(3, 44).Then(4, 45).Then(4, 46).Then(3, 47).Then(3, 48).Then(3, 49).Then(3, 50).Then(4, 51);

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

		p.A(15, "look around").Move("0", "0").Speed("260")
			.Seq(fStand, fWalk3, fStand, fBlink, fStand)
			.Then(100, 1);
		p.A(16, "sit").Move("0", "0").Speed("600", "400").Loop("random/3+4", 1)
			.Seq(fSit, fSit, fSit, fSit)
			.Then(60, 1).Then(40, 13);
		p.A(17, "wave").Move("0", "0").Speed("160").Loop("4", 1)
			.Seq(fStand, fWave, fWave2, fWave, fWave2)
			.Then(100, 1);

		// --- wizard specials ---------------------------------------------------
		// 20 cast a spell
		p.A(20, "spell").Move("0", "0").Speed("120")
			.Seq(fStand, fCast1, fCast1, fCast2, fCast2, fBurst1, fBurst2, fBurst1, fBurst2, fBurst1, fHappy, fHappy, fStand)
			.Then(85, 1).Then(15, 17);

		// 21-23 teleport: poof out, travel unseen, poof in somewhere else
		p.A(21, "poof out").Move("0", "0").Speed("110")
			.Seq(fStand, fCast2, fBurst1, fPoof1, fPoof2, fPoof3, fGone)
			.Then(100, 22);
		p.A(22, "teleport").Move("-24", "0").Speed("40").Loop("random/4+3", 0)
			.Seq(fEmpty, fEmpty)
			.Then(100, 23)
			.OnBorder(100, 23).OnGravity(100, 23);
		p.A(23, "poof in").Move("0", "0").Speed("110")
			.Seq(fGone, fPoof3, fPoof2, fPoof1, fHappy, fHappy, fStand)
			.Then(100, 1);

		// 24 spellbook
		p.A(24, "spellbook").Move("0", "0").Speed("220").Loop("3", 1)
			.Seq(fStand, fBook1, fBook1, fBook1, fBook2, fBook3, fBook1, fBook1)
			.Then(60, 1).Then(40, 30);
		p.A(30, "aha").Move("0", "0").Speed("150")
			.Seq(fBookAha, fBookAha, fBookAha, fHappy, fStand)
			.Then(100, 20);

		// 25 crystal ball
		p.A(25, "crystal ball").Move("0", "0").Speed("200").Loop("3", 1)
			.Seq(fStand, fBall1, fBall2, fBall1, fBall2, fBall3, fBall3, fBall2)
			.Then(100, 1);

		// 26-27 levitate up, then back down to the ground
		p.A(26, "levitate").Move("0", "-2").Speed("110")
			.Seq(fCast2, fFloat1, fFloat2, fFloat1, fFloat2, fFloat1, fFloat2, fFloat1, fFloat2)
			.Then(100, 27);
		p.A(27, "land").Move("0", "2").Speed("110")
			.Seq(fFloat2, fFloat1, fFloat2, fFloat1, fFloat2, fFloat1, fFloat2, fFloat1, fStand)
			.Then(100, 1);

		// 28 stroke the beard, thinking
		p.A(28, "think").Move("0", "0").Speed("260").Loop("3", 0)
			.Seq(fThink1, fThink2, fThink1, fThink2)
			.Then(70, 1).Then(30, 20);

		// ================================================================ MORE TRICKS (31-51)
		Anim Seq(int id, string name, int ms, params (int f, int n)[] steps)
		{
			var a = p.A(id, name).Move("0", "0").Speed(ms.ToString());
			foreach (var (f, n) in steps) a.Seq(Enumerable.Repeat(f, n).ToArray());
			a.Then(100, 1);
			return a;
		}

		Seq(31, "hat swallow", 110,
			(fStand, 3), (hsWobble1, 2), (hsWobble2, 2), (hsWobble1, 2), (fSurprise, 3),
			(hsGrow1, 3), (hsGrow2, 3), (hsGrow3, 3), (hsGulp, 4), (hsSquash, 3), (hsGulp, 2),
			(hsShrink1, 3), (hsShrink2, 4), (hsHop, 2), (hsShrink2, 3), (hsWiggle, 2), (hsShrink2, 2), (hsWiggle, 2), (hsShrink2, 4),
			(hsPop, 4), (hsDizzy1, 3), (hsDizzy2, 3), (hsDizzy1, 3), (fHappy, 3), (fStand, 2));

		Seq(32, "rabbit", 150, (fStand, 2), (rbTap, 3), (rbEars, 4), (rbHead, 5), (rbOut, 8), (fHappy, 3));

		var jg = p.A(33, "juggle").Move("0", "0").Speed("130").Loop("random/3+3", 0);
		jg.Seq(juggle); jg.Then(100, 1);

		var bwr = p.A(34, "brew").Move("0", "0").Speed("160").Loop("random/3+4", 0);
		bwr.Seq(brew); bwr.Then(80, 1).Then(20, 35);

		Seq(35, "potion", 160, (ptLook, 3), (ptDrink, 5), (ptGreen, 4), (ptHic, 2), (ptGreen, 2), (ptHic, 2), (fSurprise, 2), (ptGreen, 1), (fStand, 3));

		Seq(36, "sneeze", 130, (fStand, 2), (snAh, 3), (fStand, 1), (snAh, 4), (snChoo, 4), (snHatFall, 3), (fSurprise, 2), (fBlink, 2), (fStand, 2));

		p.A(37, "broom").Move("-6", "0").Speed("90").Loop("random/3+8", 0)
			.Seq(brA, brA, brB, brB)
			.Then(100, 1)
			.OnBorder(100, 2);

		Seq(38, "rain cloud", 170, (fCast1, 2), (rnCloud, 3), (rnA, 2), (rnB, 2), (rnA, 2), (rnB, 2), (rnA, 2), (rnB, 2), (rnShake, 2), (fStand, 1), (rnShake, 2), (fBlink, 2));

		var dc = p.A(39, "dance").Move("0", "0").Speed("150").Loop("random/3+5", 0);
		dc.Seq(dcA, dcB); dc.Then(100, 1);

		Seq(40, "fireworks", 140, (fw[0], 3), (fw[1], 2), (fw[2], 3), (fw[3], 3), (fw[1], 2), (fw[2], 3), (fw[3], 3), (fHappy, 3));

		Seq(41, "frog", 150, (fCast1, 3), (fgPoof, 3), (fgSit, 4), (fgHop, 2), (fgSit, 3), (fgHop, 2), (fgSit, 2), (fSurprise, 2), (fBlink, 2), (fStand, 2));

		Seq(42, "bow", 170, (fStand, 2), (bwUp, 3), (bwDown, 5), (bwUp, 3), (fHappy, 2), (fStand, 2));

		Seq(43, "owl", 180, (fStand, 2), (owFly, 3), (owPerch, 4), (owBlink, 1), (owPerch, 4), (owBlink, 1), (owPerch, 3), (owFly, 2), (fWave, 2), (fStand, 2));

		var sc = p.A(44, "telescope").Move("0", "0").Speed("260").Loop("3", 0);
		sc.Seq(scA, scA, scB, scB); sc.Then(100, 1);

		Seq(45, "yawn", 200, (fStand, 2), (ywStretch, 5), (ywRelax, 3), (fBlink, 2), (fStand, 2));

		var bb = p.A(46, "bubbles").Move("0", "0").Speed("180").Loop("random/3+3", 0);
		bb.Seq(bubbles); bb.Then(100, 1);

		Seq(47, "backfire", 130, (fCast1, 3), (fCast2, 2), (bfZap, 3), (bfSoot1, 4), (bfSoot2, 5), (bfSoot1, 2), (fBlink, 2), (fStand, 2));

		Seq(48, "scroll", 230, (fStand, 1), (slShort, 2), (slMid, 2), (slLong, 6), (slMid, 1), (slShort, 1), (fStand, 2));

		var lt = p.A(49, "lantern").Move("0", "0").Speed("200").Loop("random/3+4", 0);
		lt.Seq(ltA, ltB); lt.Then(100, 1);

		Seq(50, "shrink", 120, (fCast2, 2), (fPoof1, 2), (fPoof2, 2), (tnStand, 4), (tnHop, 2), (tnStand, 2), (tnHop, 2), (tnStand, 3), (fPoof2, 2), (fPoof1, 2), (fSurprise, 2), (fStand, 2));

		Seq(51, "rainbow", 160, (fCast1, 2), (fCast2, 2), (rbw1, 2), (rbw2, 2), (rbw3, 8), (fHappy, 3), (fStand, 2));

		// 99 kill (the application is closing): one last poof
		p.A(99, "kill").Move("0", "0").Speed("120", "200").Fade(1, 0)
			.Seq(fWave, fCast2, fBurst1, fDie1, fDie2, fGone)
			.Then(100, 99);

		p.Readme = """
			# Mago the Wizard

			## Description

			A kindly old pixel wizard with a long white beard, bushy eyebrows and a blue
			robe and pointed hat covered in stars.

			He strolls along your taskbar, casts sparkly spells, reads his spellbook,
			gazes into a crystal ball, levitates, naps when left alone and sometimes
			teleports to another spot in a puff of sparkles.

			## About pet

			Author: Luan Felipe

			Original pixel art, drawn from scratch for desktopPet. Original character —
			feel free to distribute.

			## Animations

			* walk, turn, jump onto windows, fall (soft and hard landings)
			* climb the screen border, sit and sleep on the taskbar
			* blink, look around, wave, think (strokes his beard)
			* cast a spell, teleport (poof out / poof in), levitate
			* read the spellbook, gaze into the crystal ball
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Mago

			## Descrição

			Um velhinho mago de pixel art, com barba branca comprida, sobrancelhas
			grossas e manto e chapéu pontudo azuis cheios de estrelas.

			Ele passeia pela barra de tarefas, lança feitiços brilhantes, lê o livro de
			magia, olha a bola de cristal, levita, tira um cochilo quando fica sozinho
			e às vezes se teletransporta para outro lugar numa nuvem de brilhos.

			## Sobre o pet

			Autor: Luan Felipe

			Pixel art original, desenhada do zero para o desktopPet. Personagem
			original — pode distribuir à vontade.

			## Animações

			* andar, virar, pular sobre janelas, cair (pouso suave / brusco)
			* escalar as bordas da tela, sentar e dormir na barra de tarefas
			* piscar, olhar ao redor, dar tchau, pensar (alisando a barba)
			* lançar feitiço, teletransporte (some / aparece), levitar
			* ler o livro de magia, olhar a bola de cristal
			* arrastar e jogar com o mouse
			""";

		Frame.NewHeadroom = 0;
		return p;
	}
}
