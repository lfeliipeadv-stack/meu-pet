using System.Drawing;

namespace PetGen;

/// <summary>
/// "Claude" — an original pixel-art tribute to the Claude Code mascot, drawn from scratch
/// on a 16x16 grid: a rounded terracotta block with two dark eyes and four little legs.
/// No pixel is copied from any existing sprite; only the style (small block creature,
/// terracotta tone, dark eyes, four legs) is borrowed.
/// </summary>
static class ClaudePet
{
	// Palette. 'K' is the dark ink of eyes and mouth, 'B'/'S'/'H' the body tones.
	const char Body = 'B';   // main terracotta
	const char Shade = 'S';  // darker edge, under-body
	const char High = 'H';   // lighter top
	const char Ink = 'K';    // eyes
	const char White = 'W';  // eye shine, z of sleeping
	const char Leaf = 'L';   // flower/leaf accents
	const char Pink = 'P';   // flower petals
	const char Grey = 'G';   // flag pole, dumbbell plates
	const char Yellow = 'Y'; // confetti
	const char Blue = 'C';   // confetti
	const char Red = 'R';    // confetti
	const char Navy = 'N';   // gym headband
	const char LightGrey = 'g'; // laptop lid, coffee steam
	const char Orange = 'O';    // soup, hard hat brim
	const char Slate = 'D';     // cooking pot
	const char Wood = 'U';      // spoon, hammer handle, fedora
	const char Maroon = 'M';    // skateboard deck
	const char LightBlue = 'c'; // magnifying glass lens
	const char PaleYellow = 'y'; // sparkles, light rays

	public static PetDef Build()
	{
		// 4 rows of headroom above the head for tall props (flag, top hat, notes...).
		Frame.NewHeadroom = 4;
		var p = new PetDef
		{
			Folder = "claude",
			Title = "Claude",
			PetName = "Claude",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "A little desktop companion inspired by the Claude Code mascot.[br]" +
				   "Original pixel art, drawn from scratch for desktopPet.[br]" +
				   "Unofficial tribute, not affiliated with Anthropic.",
			Palette = new Dictionary<char, Color>
			{
				[Body] = Color.FromArgb(214, 119, 86),
				[Shade] = Color.FromArgb(168, 83, 58),
				[High] = Color.FromArgb(236, 160, 128),
				[Ink] = Color.FromArgb(26, 22, 20),
				[White] = Color.FromArgb(250, 246, 242),
				[Leaf] = Color.FromArgb(96, 158, 96),
				[Pink] = Color.FromArgb(240, 142, 170),
				[Grey] = Color.FromArgb(120, 112, 108),
				[Yellow] = Color.FromArgb(245, 200, 66),
				[Blue] = Color.FromArgb(104, 150, 226),
				[Red] = Color.FromArgb(222, 92, 88),
				[Navy] = Color.FromArgb(34, 62, 128),
				[LightGrey] = Color.FromArgb(196, 192, 188),
				[Orange] = Color.FromArgb(232, 140, 52),
				[Slate] = Color.FromArgb(66, 68, 82),
				[Wood] = Color.FromArgb(150, 98, 54),
				[Maroon] = Color.FromArgb(92, 26, 50),
				[LightBlue] = Color.FromArgb(196, 228, 240),
				[PaleYellow] = Color.FromArgb(252, 232, 150),
			},
			Scale = 3,
			TilesX = 8,
		};

		// ---------------------------------------------------------------- frames
		// Legs: four little stubs. legPose moves each leg up (0) or down (1..).
		int[] legX = { 4, 6, 9, 11 };

		Frame Body0(int bodyTop = 4, int squash = 0)
		{
			// Rounded block body: 11 wide, 8 tall (minus squash), soft corners.
			var f = new Frame();
			int x0 = 3, w = 11, h = 8 - squash, y0 = bodyTop;
			f.Rect(x0, y0, w, h, Body);
			// soft corners
			f.Dot(x0, y0, '.').Dot(x0 + w - 1, y0, '.').Dot(x0, y0 + h - 1, '.').Dot(x0 + w - 1, y0 + h - 1, '.');
			// top highlight and bottom shade
			f.Rect(x0 + 1, y0, w - 2, 1, High);
			f.Rect(x0 + 1, y0 + h - 1, w - 2, 1, Shade);
			return f;
		}

		void Legs(Frame f, int bodyBottom, params int[] lift)
		{
			for (int i = 0; i < 4; i++)
			{
				int len = 3 - (lift != null && i < lift.Length ? lift[i] : 0);
				for (int j = 0; j < len; j++) f.Dot(legX[i], bodyBottom + j, j == len - 1 ? Shade : Body);
			}
		}

		void Eyes(Frame f, int y, int dx = 0, bool closed = false, bool wide = false, bool happy = false)
		{
			int lx = 5 + dx, rx = 9 + dx;
			if (closed)
			{
				f.Rect(lx, y + 1, 2, 1, Ink);
				f.Rect(rx, y + 1, 2, 1, Ink);
				return;
			}
			if (happy)
			{
				// ^ ^ shape
				f.Dot(lx, y + 1, Ink).Dot(lx + 1, y, Ink).Dot(lx + 2, y + 1, Ink);
				f.Dot(rx - 1, y + 1, Ink).Dot(rx, y, Ink).Dot(rx + 1, y + 1, Ink);
				return;
			}
			int h = wide ? 3 : 2;
			f.Rect(lx, y, 2, h, Ink);
			f.Rect(rx, y, 2, h, Ink);
			if (wide) { f.Dot(lx, y, White); f.Dot(rx, y, White); }
		}

		// 0,1: stand / blink
		var stand = Body0();
		Eyes(stand, 6);
		Legs(stand, 12, 0, 0, 0, 0);
		int fStand = p.Add(stand);

		var blink = Body0();
		Eyes(blink, 6, closed: true);
		Legs(blink, 12, 0, 0, 0, 0);
		int fBlink = p.Add(blink);

		// 2,3,4,5: walk cycle (legs alternate, body bobs 1 px)
		var walk1 = Body0();
		Eyes(walk1, 6);
		Legs(walk1, 12, 0, 1, 0, 1);
		int fWalk1 = p.Add(walk1);

		var walk2 = Body0(5);
		Eyes(walk2, 7);
		Legs(walk2, 13, 1, 0, 1, 0);
		int fWalk2 = p.Add(walk2);

		var walk3 = Body0();
		Eyes(walk3, 6);
		Legs(walk3, 12, 1, 0, 1, 0);
		int fWalk3 = p.Add(walk3);

		var walk4 = Body0(5);
		Eyes(walk4, 7);
		Legs(walk4, 13, 0, 1, 0, 1);
		int fWalk4 = p.Add(walk4);

		// 6,7: run (leaning forward, legs blurred into two pairs)
		var run1 = Body0();
		Eyes(run1, 6, -1);
		run1.Rect(2, 7, 1, 3, Shade);         // lean trail
		Legs(run1, 12, 0, 2, 0, 2);
		int fRun1 = p.Add(run1);

		var run2 = Body0(5);
		Eyes(run2, 7, -1);
		run2.Rect(2, 8, 1, 3, Shade);
		Legs(run2, 13, 2, 0, 2, 0);
		int fRun2 = p.Add(run2);

		// 8,9: crouch (gathering momentum) and stretched jump
		var crouch = Body0(6, 2);
		Eyes(crouch, 8);
		Legs(crouch, 12, 1, 1, 1, 1);
		int fCrouch = p.Add(crouch);

		var jump = Body0(3);
		Eyes(jump, 5, wide: true);
		Legs(jump, 11, 2, 1, 1, 2);
		int fJump = p.Add(jump);

		// 10: falling (eyes wide, legs out)
		var fall = Body0(4);
		Eyes(fall, 6, wide: true);
		fall.Dot(2, 9, Body).Dot(13, 9, Body);     // arms out
		Legs(fall, 12, 2, 0, 0, 2);
		int fFall = p.Add(fall);

		// 11: landing squash
		var land = Body0(7, 3);
		Eyes(land, 8, closed: true);
		Legs(land, 12, 2, 2, 2, 2);
		int fLand = p.Add(land);

		// 12,13,14: sleeping (closed eyes, slow breathing, z)
		var sleep1 = Body0(5, 1);
		Eyes(sleep1, 7, closed: true);
		Legs(sleep1, 12, 2, 2, 2, 2);
		int fSleep1 = p.Add(sleep1);

		var sleep2 = sleep1.Clone();
		sleep2.Dot(13, 3, White).Dot(14, 2, White);
		int fSleep2 = p.Add(sleep2);

		var sleep3 = Body0(6, 2);
		Eyes(sleep3, 8, closed: true);
		Legs(sleep3, 12, 2, 2, 2, 2);
		sleep3.Dot(13, 4, White).Dot(14, 3, White).Dot(14, 1, White);
		int fSleep3 = p.Add(sleep3);

		// 15,16: happy / wave (one arm up)
		var happy = Body0();
		Eyes(happy, 6, happy: true);
		Legs(happy, 12, 0, 0, 0, 0);
		int fHappy = p.Add(happy);

		var wave = Body0();
		Eyes(wave, 6, happy: true);
		wave.Dot(14, 3, Body).Dot(14, 4, Body).Dot(13, 5, Body);   // raised arm
		Legs(wave, 12, 0, 0, 0, 0);
		int fWave = p.Add(wave);

		// 17: surprised (wide eyes, small marks above)
		var surprise = Body0();
		Eyes(surprise, 6, wide: true);
		surprise.Dot(4, 2, Ink).Dot(8, 1, Ink).Dot(12, 2, Ink);
		Legs(surprise, 12, 0, 0, 0, 0);
		int fSurprise = p.Add(surprise);

		// 18,19: looking down over an edge
		var lookDown = Body0(5);
		Eyes(lookDown, 9);
		Legs(lookDown, 13, 0, 0, 0, 0);
		int fLookDown = p.Add(lookDown);

		var lookDown2 = Body0(6);
		Eyes(lookDown2, 10);
		Legs(lookDown2, 14, 0, 0, 1, 1);
		int fLookDown2 = p.Add(lookDown2);

		// 20,21: climbing (vertical walk, legs gripping the side)
		var climb1 = Body0();
		Eyes(climb1, 6, 1);
		climb1.Dot(2, 5, Body).Dot(2, 9, Body);
		Legs(climb1, 12, 1, 2, 1, 2);
		int fClimb1 = p.Add(climb1);

		var climb2 = Body0();
		Eyes(climb2, 6, 1);
		climb2.Dot(2, 6, Body).Dot(2, 10, Body);
		Legs(climb2, 12, 2, 1, 2, 1);
		int fClimb2 = p.Add(climb2);

		// 22,23: dragged by the mouse (dangling legs)
		var drag1 = Body0(4);
		Eyes(drag1, 6, wide: true);
		drag1.Dot(4, 12, Body).Dot(6, 13, Body).Dot(9, 13, Body).Dot(11, 12, Body);
		int fDrag1 = p.Add(drag1);

		var drag2 = Body0(4);
		Eyes(drag2, 6, wide: true);
		drag2.Dot(4, 13, Body).Dot(6, 12, Body).Dot(9, 12, Body).Dot(11, 13, Body);
		int fDrag2 = p.Add(drag2);

		// 24,25: thinking (dots above the head)
		var think1 = Body0();
		Eyes(think1, 6, closed: true);
		think1.Dot(13, 2, Ink);
		Legs(think1, 12, 0, 0, 0, 0);
		int fThink1 = p.Add(think1);

		var think2 = Body0();
		Eyes(think2, 6, closed: true);
		think2.Dot(13, 2, Ink).Dot(14, 1, Ink);
		Legs(think2, 12, 0, 0, 0, 0);
		int fThink2 = p.Add(think2);

		// 26: holding a little flower (idle treat)
		var flower = Body0();
		Eyes(flower, 6, happy: true);
		flower.Dot(14, 6, Leaf).Dot(14, 5, Pink).Dot(13, 4, Pink).Dot(15, 4, Pink).Dot(14, 3, Pink);
		Legs(flower, 12, 0, 0, 0, 0);
		int fFlower = p.Add(flower);

		// 27,28: dying/kill (sinks and fades)
		var die1 = Body0(6, 2);
		Eyes(die1, 8, closed: true);
		Legs(die1, 12, 2, 2, 2, 2);
		int fDie1 = p.Add(die1);

		var die2 = Body0(8, 4);
		Eyes(die2, 9, closed: true);
		int fDie2 = p.Add(die2);

		// 29: sitting on the taskbar, legs tucked
		var sit = Body0(5, 1);
		Eyes(sit, 7);
		Legs(sit, 12, 2, 2, 2, 2);
		int fSit = p.Add(sit);

		// ---- NEW FRAMES (30 onwards) -----------------------------------------

		// 30: scared — crouched, closed eyes, tiny trembling marks
		var scared1 = Body0(6, 2);
		Eyes(scared1, 8, closed: true);
		Legs(scared1, 12, 1, 1, 1, 1);
		scared1.Dot(2, 6, Ink).Dot(14, 6, Ink);     // vibration lines
		int fScared1 = p.Add(scared1);

		var scared2 = Body0(6, 2);
		Eyes(scared2, 8, closed: true);
		Legs(scared2, 12, 1, 1, 1, 1);
		scared2.Dot(2, 7, Ink).Dot(14, 7, Ink);
		int fScared2 = p.Add(scared2);

		// 32: angry — normal body, furrowed brow marks above eyes
		var angry = Body0();
		Eyes(angry, 6);
		angry.Dot(5, 5, Ink).Dot(6, 5, Ink);        // left brow slash
		angry.Dot(10, 5, Ink).Dot(11, 5, Ink);      // right brow slash
		Legs(angry, 12, 0, 0, 0, 0);
		int fAngry = p.Add(angry);

		// 33,34: headbopping / dance — body bobs side to side
		var dance1 = Body0(4);
		Eyes(dance1, 6, happy: true);
		dance1.Dot(2, 8, Body).Dot(2, 9, Body);     // left arm out
		Legs(dance1, 12, 0, 1, 1, 0);
		int fDance1 = p.Add(dance1);

		var dance2 = Body0(4);
		Eyes(dance2, 6, happy: true);
		dance2.Dot(13, 8, Body).Dot(13, 9, Body);   // right arm out
		Legs(dance2, 12, 1, 0, 0, 1);
		int fDance2 = p.Add(dance2);

		// 35,36,37: eating — nibbles at a tiny pixel snack
		var eat1 = Body0();
		Eyes(eat1, 6, happy: true);
		eat1.Dot(14, 7, Ink).Dot(14, 8, Ink);       // snack piece
		Legs(eat1, 12, 0, 0, 0, 0);
		int fEat1 = p.Add(eat1);

		var eat2 = Body0();
		Eyes(eat2, 6);
		eat2.Dot(14, 8, Ink);                        // half eaten
		Legs(eat2, 12, 0, 0, 0, 0);
		int fEat2 = p.Add(eat2);

		var eat3 = Body0();
		Eyes(eat3, 6, happy: true);
		Legs(eat3, 12, 0, 0, 0, 0);                 // all gone, satisfied
		int fEat3 = p.Add(eat3);

		// 38,39: stretching — body elongates vertically then relaxes
		var stretch1 = Body0(3, -1); // squash=-1 → h=9, taller
		Eyes(stretch1, 5);
		Legs(stretch1, 12, 0, 0, 0, 0);
		int fStretch1 = p.Add(stretch1);

		var stretch2 = Body0(3);    // normal height, still up high
		Eyes(stretch2, 5, wide: true);
		Legs(stretch2, 11, 2, 2, 2, 2);
		int fStretch2 = p.Add(stretch2);

		// 40,41: spinning/dizzy — two frames that alternate fast (use flip action trick)
		var spin1 = Body0();
		Eyes(spin1, 6);
		spin1.Dot(2, 8, Body).Dot(13, 9, Body);     // arms windmill
		Legs(spin1, 12, 0, 1, 1, 0);
		int fSpin1 = p.Add(spin1);

		var spin2 = Body0();
		Eyes(spin2, 6, happy: true);
		spin2.Dot(2, 9, Body).Dot(13, 8, Body);
		Legs(spin2, 12, 1, 0, 0, 1);
		int fSpin2 = p.Add(spin2);

		// 42,43: sneaking — body low, slow cautious walk
		var sneak1 = Body0(6, 2);
		Eyes(sneak1, 8);
		Legs(sneak1, 12, 0, 1, 1, 0);
		int fSneak1 = p.Add(sneak1);

		var sneak2 = Body0(6, 2);
		Eyes(sneak2, 8);
		Legs(sneak2, 12, 1, 0, 0, 1);
		int fSneak2 = p.Add(sneak2);

		// 44: pointing up — one arm raised very high, eyes look up
		var point = Body0();
		Eyes(point, 6, 1);
		point.Dot(14, 2, Body).Dot(14, 3, Body).Dot(14, 4, Body); // arm stretched up
		Legs(point, 12, 0, 0, 0, 0);
		int fPoint = p.Add(point);

		// 45: confused — head tilted look, one side-mark
		var confused = Body0();
		Eyes(confused, 6, wide: true);
		confused.Dot(14, 1, Ink);  // question-mark-ish dot
		confused.Dot(14, 3, Ink);
		Legs(confused, 12, 0, 0, 0, 0);
		int fConfused = p.Add(confused);

		// ---- MOTION-STUDY FRAMES -----------------------------------------------
		// Timings and motion beats follow the breakdown of the official mascot clips
		// published on Codrops (lean, leap, flag wave, confetti stomp, gym reps);
		// the pixels are redrawn from scratch on this pet's own 16x16 grid.

		// Shear the upper rows sideways: a pixel-art "tilt". Rows above y1 move by
		// 2*dir, rows between y1 and y2 by dir; legs stay planted.
		Frame Lean(Frame f, int dir, int y1, int y2)
		{
			var src = f.Clone();
			for (int y = f.MinY; y < y2; y++)
			{
				int d = y < y1 ? 2 * dir : dir;
				for (int x = 0; x < Frame.Size; x++) f[x, y] = '.';
				for (int x = 0; x < Frame.Size; x++) if (src[x, y] != '.') f[x + d, y] = src[x, y];
			}
			return f;
		}

		// Lean look: body tilts, eyes lead the motion, legs stretched into the floor.
		Frame LeanFrame(int dir, bool deep)
		{
			var f = Body0(deep ? 5 : 4);
			Eyes(f, deep ? 7 : 6, dir);
			Legs(f, deep ? 13 : 12, 0, 0, 0, 0);
			return deep ? Lean(f, dir, 7, 10) : Lean(f, dir, 0, 8);
		}
		int fLeanL = p.Add(LeanFrame(-1, false));
		int fLeanL2 = p.Add(LeanFrame(-1, true));
		int fLeanR = p.Add(LeanFrame(1, false));
		int fLeanR2 = p.Add(LeanFrame(1, true));

		// Leap: fast crouch with arms dropped, stretched take-off, hands overshoot on landing.
		var gather = Body0(7, 3);
		Eyes(gather, 9);
		gather.Dot(2, 11, Body).Dot(14, 11, Body);
		Legs(gather, 12, 2, 2, 2, 2);
		int fGather = p.Add(gather);

		var launch = Body0(2, -1);
		Eyes(launch, 4, -1, wide: true);
		launch.Dot(2, 4, Body).Dot(14, 4, Body);
		Legs(launch, 11, 0, 0, 0, 0);
		int fLaunch = p.Add(launch);

		var touch = Body0(5, 1);
		Eyes(touch, 7, happy: true);
		touch.Dot(2, 11, Body).Dot(14, 11, Body);   // hands overshoot down
		Legs(touch, 12, 1, 1, 1, 1);
		int fTouch = p.Add(touch);

		// Flag: right hand raised high, long black pole, big checkered cloth in the headroom
		// above the head. Each shape is a mask around the pole top (column 5 = pole); the
		// black/white checker is applied when drawing.
		string[][] clothShapes =
		{
			new[] { "      X   ", "      X   ", "      X   ", "      X   ", "      X   " }, // 0 limp, hanging on the pole
			new[] { "      XX  ", "      XXX ", "      XX  ", "      X   " },               // 1 catching the wind
			new[] { "      XXXX", "      XXXX", "      XXXX", "      XXXX" },               // 2 flat to the right
			new[] { " XXXX     ", "XXXXX     ", "XXXXX     ", "   XX     " },               // 3 swung left, curled
			new[] { "       XXX", "      XXXX", "      XXXX", "      XX  " },               // 4 right, tip up
			new[] { "    X X   ", "    X X   ", "    X X   ", "    X X   ", "      X   " }, // 5 wrapped around the pole
			new[] { "      XXX ", "      XXXX", "       XXX", "        XX" },               // 6 right, rippling down
			new[] { "XXXXX     ", "XXXXX     ", "XXXXX     ", "XXXXX     " },               // 7 flat to the left
			new[] { "XXX       ", "XXXXX     ", " XXXX     ", "   XX     " },               // 8 left, tip up
		};
		int[] flagOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 5, 2, 8, 6 };
		int[] flagSway = { 0, 0, -1, -1, 0, 1, 1, 1, 0, 0, -1, -1 };
		Frame FlagFrame(int shape, int sway)
		{
			var f = Body0(4);
			Eyes(f, 6, happy: true);
			Legs(f, 12, 0, 0, 0, 0);
			f.Shift(-2, 0);                                  // body x1..11: room for the cloth on both sides
			const int pole = 11, top = -4;
			f.Dot(pole, 4, Body).Rect(pole, 2, 2, 2, Body);  // raised hand
			f.Rect(pole, top, 1, 6, Ink);                    // pole
			var rows = clothShapes[shape];
			for (int j = 0; j < rows.Length; j++)
				for (int i = 0; i < rows[j].Length; i++)
					if (rows[j][i] == 'X')
					{
						int x = pole - 5 + i, y = top + j;
						f.Dot(x, y, (x + y) % 2 == 0 ? Ink : White);
					}
			if (sway != 0) Lean(f, sway, 0, 13);
			return f;
		}
		var flag = new int[flagOrder.Length];
		for (int i = 0; i < flag.Length; i++) flag[i] = p.Add(FlagFrame(flagOrder[i], flagSway[i]));

		// Confetti stomp: the body tilts with one side raised (legs on that side stretch to
		// keep touching the floor) and that hand goes up; red/blue bits burst on both sides.
		Frame Tilt(int dir, int amount, bool armUp)
		{
			var f = new Frame();
			const int x0 = 3, w = 11, top = 6, h = 7;
			int Off(int x) => (int)Math.Round((dir > 0 ? x - x0 : x0 + w - 1 - x) * amount / 10.0);
			for (int x = x0; x < x0 + w; x++)
			{
				int o = Off(x);
				f.Rect(x, top - o, 1, h, Body);
				f.Dot(x, top - o, High).Dot(x, top + h - 1 - o, Shade);
			}
			int xl = x0, xr = x0 + w - 1;
			f.Dot(xl, top - Off(xl), '.').Dot(xr, top - Off(xr), '.');
			f.Dot(xl, top + h - 1 - Off(xl), '.').Dot(xr, top + h - 1 - Off(xr), '.');
			foreach (int ex in new[] { 5, 9 })   // each eye stays a clean 2x2 block, riding the tilt
				f.Rect(ex, top + 2 - Off(dir > 0 ? ex + 1 : ex), 2, 2, Ink);
			foreach (int lx in legX)
				for (int y = top + h - Off(lx); y <= 14; y++) f.Dot(lx, y, y == 14 ? Shade : Body);
			// arms: the raised side reaches up, the other one sticks out at the side
			int up = dir > 0 ? xr : xl, low = dir > 0 ? xl : xr;
			int upX = up + (dir > 0 ? 1 : -1), lowX = low + (dir > 0 ? -1 : 1);
			if (armUp) f.Rect(upX, top - Off(up) - 2, 1, 3, Body);
			else f.Dot(upX, top + 3 - Off(up), Body);
			f.Dot(lowX, top + 3 - Off(low), Body);
			return f;
		}
		Frame Squat()
		{
			var f = Body0(7, 2);
			Eyes(f, 9);
			Legs(f, 13, 1, 1, 1, 1);
			f.Dot(2, 9, Body).Dot(14, 9, Body).Dot(1, 9, Body).Dot(15, 9, Body);   // arms out flat
			return f;
		}
		(int dx, int dy, char c)[] bits =
			{ (-2, 0, Red), (0, -1, Blue), (2, 0, Red), (-1, 1, Blue), (1, 1, Red), (3, 0, Blue), (-3, 1, Red) };
		int[] burstY = { 1, 0, -1, -1, 0, 1, 2, 3 };   // shoots up, peaks, falls back
		void Burst(Frame f, int ox, int side, int age)
		{
			double spread = 0.7 + age * 0.3;
			int n = age < 4 ? bits.Length : Math.Max(0, bits.Length - (age - 3) * 2);
			for (int i = 0; i < n; i++)
			{
				var (dx, dy, c) = bits[i];
				int x = ox + side * (int)Math.Round(dx * spread), y = burstY[age] + dy + (age >= 5 ? i % 2 : 0);
				if (f[x, y] == '.') f.Dot(x, y, c);
			}
		}
		Func<Frame>[] stompBody =
		{
			() => Tilt(1, 2, false), () => Tilt(1, 3, true), () => Tilt(1, 2, true), Squat,
			() => Tilt(-1, 2, false), () => Tilt(-1, 3, true), () => Tilt(-1, 2, true), Squat,
		};
		var stomp = new int[8];
		for (int i = 0; i < 8; i++)
		{
			var f = stompBody[i]();
			Burst(f, 12, 1, (i + 7) % 8);    // right burst fires on beat 1
			Burst(f, 4, -1, (i + 3) % 8);    // left burst fires on beat 5
			stomp[i] = p.Add(f);
		}

		// Gym: headband, flex, two dumbbells pressed overhead one arm at a time.
		// side = +1 right arm, -1 left arm (x mirrored around the body center).
		int M(int side, int x) => side > 0 ? x : 16 - x;
		Frame Up(Frame f) => f.Shift(0, -1);            // gym frames sit one row higher
		Frame GymBody(bool band, bool happy = false, bool strain = false)
		{
			var f = Body0(5);
			if (band) f.Rect(3, 6, 11, 1, Navy);
			Eyes(f, 7, closed: strain, happy: happy);
			Legs(f, 13, 0, 0, 0, 0);
			return f;
		}
		void ArmUp(Frame f, int s) => f.Rect(M(s, 14), 3, 1, 3, Body);
		void ArmDown(Frame f, int s) => f.Dot(M(s, 14), 9, Body);
		void Press(Frame f, int s)             // arm straight up, dumbbell overhead
		{
			f.Rect(M(s, 14), 2, 1, 4, Body);
			f.Rect(M(s, 11), 0, 1, 2, Ink).Rect(M(s, 15), 0, 1, 2, Ink);
			for (int x = 12; x <= 14; x++) f.Dot(M(s, x), 1, Grey);
		}
		void Curl(Frame f, int s)              // dumbbell at head height
		{
			f.Rect(M(s, 14), 5, 1, 3, Body);
			f.Rect(M(s, 11), 3, 1, 2, Ink).Rect(M(s, 15), 3, 1, 2, Ink);
			for (int x = 12; x <= 14; x++) f.Dot(M(s, x), 4, Grey);
		}
		void Hang(Frame f, int s)              // dumbbell hanging at the side
		{
			f.Rect(M(s, 14), 9, 1, 2, Body);
			f.Rect(Math.Min(M(s, 14), M(s, 15)), 11, 2, 2, Ink);
		}
		void Ring(Frame f, int x, int y)       // headband in the air (thin ellipse)
		{
			f.Rect(x + 1, y, 5, 1, Navy).Dot(x, y + 1, Navy).Dot(x + 6, y + 1, Navy).Rect(x + 1, y + 2, 5, 1, Navy);
		}

		var gi = GymBody(false);
		int gIdle = p.Add(Up(gi));

		var gh = GymBody(false);
		gh.Rect(2, 9, 1, 2, Body).Rect(0, 9, 2, 2, Navy);      // headband in the left hand
		int gBandHand = p.Add(Up(gh));

		var gt = GymBody(false, happy: true);
		ArmUp(gt, -1);
		Ring(gt, 5, 0);
		int gBandToss = p.Add(Up(gt));

		var gl = GymBody(false);
		gl.Rect(5, 3, 7, 1, Navy).Rect(4, 4, 9, 1, Navy);     // dropping onto the head
		int gBandLand = p.Add(Up(gl));

		var gfu = GymBody(true, happy: true);
		ArmUp(gfu, 1); ArmUp(gfu, -1);
		int gFlexUp = p.Add(Up(gfu));

		var gfd = GymBody(true);
		ArmDown(gfd, 1); ArmDown(gfd, -1);
		int gFlexDown = p.Add(Up(gfd));

		var gg = Body0(8, 2);
		gg.Rect(3, 9, 11, 1, Navy);
		Eyes(gg, 10);
		Legs(gg, 14, 1, 1, 1, 1);
		gg.Rect(2, 12, 1, 2, Body).Rect(14, 12, 1, 2, Body);
		gg.Rect(0, 14, 2, 2, Ink).Rect(14, 14, 2, 2, Ink);    // dumbbells on the floor
		int gGrab = p.Add(Up(gg));

		Frame Rep(int s, bool top)
		{
			var f = GymBody(true, strain: top);
			if (top) Press(f, s); else Curl(f, s);
			Hang(f, -s);
			return f;
		}
		int gCurlR = p.Add(Up(Rep(1, false))), gPressR = p.Add(Up(Rep(1, true)));
		int gCurlL = p.Add(Up(Rep(-1, false))), gPressL = p.Add(Up(Rep(-1, true)));

		var gr = GymBody(true);
		Hang(gr, 1); Hang(gr, -1);
		int gRest = p.Add(Up(gr));

		var go = GymBody(false, happy: true);
		ArmUp(go, 1);
		RingOff(go);
		int gBandOff = p.Add(Up(go));
		void RingOff(Frame f)            // headband flung off to the side
		{
			f.Rect(10, 0, 4, 1, Navy).Dot(9, 1, Navy).Dot(14, 1, Navy).Rect(10, 2, 4, 1, Navy);
		}

		// ---- PROPS: laptop, top hat, coffee, music -----------------------------

		// Laptop: winks, pulls a laptop out of thin air, sets it down on its left, opens it,
		// turns to it and types (screen light flickering on the hand), then closes it.
		// The body sits 2 px to the right so the laptop fits on the left.
		Frame Desk(bool eyesToLaptop = false, bool closedEyes = false, bool wink = false)
		{
			var f = Body0();
			if (wink) { f.Rect(5, 6, 2, 2, Ink); f.Rect(9, 7, 2, 1, Ink); }
			else Eyes(f, 6, eyesToLaptop ? -1 : 0, closed: closedEyes);
			Legs(f, 12, 0, 0, 0, 0);
			f.Shift(2, 0);                                   // body x5..15
			if (eyesToLaptop) f.Rect(15, 5, 1, 6, Shade);    // three-quarter turn: far side in shade
			return f;
		}
		void LaptopClosed(Frame f, int x, int y) { f.Rect(x, y, 5, 1, LightGrey).Rect(x, y + 1, 5, 1, Grey); }
		void LaptopOpen(Frame f)
		{
			f.Rect(1, 14, 5, 1, Grey);                       // base / keyboard
			f.Dot(1, 13, Grey).Dot(1, 12, Grey).Dot(0, 11, Grey).Dot(0, 10, Grey);   // lid, leaning back
		}
		Frame Typing(int hand, bool glow, bool blink = false)
		{
			var f = Desk(eyesToLaptop: true, closedEyes: blink);
			LaptopOpen(f);
			f.Dot(4, 11, Body).Dot(hand == 0 ? 3 : 4, 12, Body).Dot(hand == 0 ? 3 : 2, 13, Body); // arm on the keys
			if (glow) f.Dot(hand == 0 ? 3 : 2, 13, Yellow).Dot(1, 11, Blue);     // screen light
			else f.Dot(0, 12, Blue);
			return f;
		}
		int lpIdle = p.Add(Desk());
		int lpWink = p.Add(Desk(wink: true));
		var lpp = Desk(eyesToLaptop: true);
		lpp.Rect(4, 3, 1, 3, Body);                         // arm up, grabbing the laptop from the air
		LaptopClosed(lpp, 1, 1);
		int lpPull = p.Add(lpp);
		var lps = Desk(eyesToLaptop: true);
		lps.Dot(4, 11, Body).Dot(4, 12, Body);
		LaptopClosed(lps, 0, 13);
		int lpSet = p.Add(lps);
		var lpo = Desk(eyesToLaptop: true);
		lpo.Dot(4, 11, Body).Dot(3, 12, Body);
		LaptopOpen(lpo);
		int lpOpen = p.Add(lpo);
		int lpType1 = p.Add(Typing(0, true));
		int lpType2 = p.Add(Typing(1, false));
		int lpType3 = p.Add(Typing(0, false));
		int lpType4 = p.Add(Typing(1, true));
		int lpBlink = p.Add(Typing(0, true, blink: true));
		var lpc = Desk(eyesToLaptop: true);
		lpc.Dot(4, 11, Body).Dot(3, 12, Body);
		LaptopClosed(lpc, 0, 13);
		int lpClose = p.Add(lpc);

		// Top hat: pulls a top hat out of the air, puts it on, tips it with a bow,
		// dances with it, lifts it to reveal a flower on its head, then puts it away.
		void Hat(Frame f, int cx, int brimY)
		{
			f.Rect(cx - 4, brimY, 9, 1, Ink);                // brim
			f.Rect(cx - 2, brimY - 1, 5, 1, Red);            // band
			f.Rect(cx - 2, brimY - 5, 5, 4, Ink);            // crown
			f.Dot(cx - 1, brimY - 4, Grey).Dot(cx - 1, brimY - 3, Grey);   // shine
		}
		Frame Hatted(int bodyTop = 4, int squash = 0, bool happy = true, bool closed = false)
		{
			var f = Body0(bodyTop, squash);
			Eyes(f, bodyTop + 2, happy: happy && !closed, closed: closed);
			Legs(f, 12, squash > 0 ? 1 : 0, squash > 0 ? 1 : 0, squash > 0 ? 1 : 0, squash > 0 ? 1 : 0);
			return f;
		}
		var thg = Hatted(happy: false);
		thg.Rect(14, 1, 1, 4, Body);                         // arm up, hat in hand
		Hat(thg, 12, 0);
		int thGrab = p.Add(thg);
		var thon = Hatted();
		Hat(thon, 8, 3);
		int thOn = p.Add(thon);
		var thb = Hatted(happy: false);
		Eyes(thb, 6, closed: true);
		Hat(thb, 8, 3);
		int thBlink = p.Add(thb);
		var tht = Hatted(5, 1, closed: true);                // bow: body dips
		tht.Rect(13, 1, 1, 4, Body);                         // hand lifting the brim
		Hat(tht, 8, 1);
		int thTip = p.Add(tht);
		var tht2 = Hatted(6, 2, closed: true);               // deeper bow, hat held up
		tht2.Rect(13, 0, 1, 6, Body);
		Hat(tht2, 8, 0);
		int thTip2 = p.Add(tht2);
		Frame HatDance(int side)
		{
			var f = Hatted();
			if (side < 0) f.Dot(2, 8, Body).Dot(2, 9, Body); else f.Dot(14, 8, Body).Dot(14, 9, Body);
			Legs(f, 12, side < 0 ? 0 : 1, side < 0 ? 1 : 0, side < 0 ? 1 : 0, side < 0 ? 0 : 1);
			Hat(f, side < 0 ? 7 : 9, 3);                     // hat wobbles with the step
			return f;
		}
		int thDance1 = p.Add(HatDance(-1));
		int thDance2 = p.Add(HatDance(1));
		var thf = Hatted();
		thf.Rect(13, -1, 1, 6, Body);                        // hat raised high...
		Hat(thf, 9, -2);
		thf.Dot(8, 3, Leaf).Dot(8, 2, Leaf).Dot(8, 1, Pink).Dot(7, 0, Pink).Dot(9, 0, Pink).Dot(8, -1, Pink).Dot(8, 0, Yellow); // ...a flower!
		int thFlower = p.Add(thf);

		// Coffee: a blue mug in the right hand, steam curling up; sips, then a happy sigh.
		void Mug(Frame f, int x, int y, int steam)
		{
			f.Rect(x, y, 3, 3, Blue).Dot(x, y, White);      // mug with a shine
			if (steam == 1) f.Dot(x + 1, y - 2, LightGrey).Dot(x + 2, y - 3, LightGrey).Dot(x + 1, y - 4, LightGrey);
			if (steam == 2) f.Dot(x + 2, y - 2, LightGrey).Dot(x + 1, y - 3, LightGrey).Dot(x + 2, y - 4, LightGrey);
		}
		Frame Coffee(int steam, bool sip = false, bool sigh = false)
		{
			var f = Body0();
			Eyes(f, 6, closed: sip, happy: sigh);
			Legs(f, 12, 0, 0, 0, 0);
			if (sip) { f.Dot(14, 9, Body).Dot(14, 8, Body); Mug(f, 12, 6, 0); }     // mug at the mouth
			else { f.Dot(14, 9, Body); Mug(f, 13, 7, steam); }
			return f;
		}
		int cfHold1 = p.Add(Coffee(1));
		int cfHold2 = p.Add(Coffee(2));
		int cfSip = p.Add(Coffee(0, sip: true));
		int cfSigh = p.Add(Coffee(1, sigh: true));
		int cfSigh2 = p.Add(Coffee(2, sigh: true));

		// Music: headphones on, eyes closed, bobbing to the beat while notes float up.
		void Note(Frame f, int x, int y) { f.Rect(x + 1, y, 1, 3, Ink).Dot(x + 2, y, Ink).Rect(x, y + 2, 2, 1, Ink); }
		Frame Music(int beat)
		{
			bool down = beat % 2 == 1;
			var f = Body0(down ? 5 : 4, down ? 1 : 0);
			Eyes(f, down ? 7 : 6, happy: true);
			Legs(f, 12, 0, 0, 0, 0);
			int top = down ? 5 : 4;
			f.Rect(4, top - 1, 9, 1, Ink).Dot(3, top, Ink).Dot(13, top, Ink);       // headband
			f.Rect(2, top + 2, 2, 3, Red).Rect(13, top + 2, 2, 3, Red);             // ear cups
			int[][] notes = { new[] { 13, 1 }, new[] { 14, -1 }, new[] { 12, -3 }, new[] { 1, 0 } };
			var a = notes[beat % 4]; Note(f, a[0], a[1]);
			var b = notes[(beat + 2) % 4]; Note(f, b[0], b[1] + 1);
			if (beat % 4 >= 2) Lean(f, beat % 4 == 2 ? 1 : -1, 0, 12);
			return f;
		}
		var music = new int[4];
		for (int i = 0; i < 4; i++) music[i] = p.Add(Music(i));

		// ---- MORE PROPS: chef, skateboard, builder, detective, idea ------------
		// Standard pose: body rows 4..11, legs 12..14 (row 14 = ground).

		// Chef: white toque, wooden spoon, a bubbling pot on the fire; stirs with a focused
		// face, tastes, then beams with sparkles.
		void Toque(Frame f)
		{
			f.Rect(5, -3, 7, 1, LightGrey);
			f.Rect(4, -2, 9, 3, White).Dot(4, -2, LightGrey).Dot(12, -2, LightGrey);
			f.Dot(7, -1, LightGrey).Dot(10, -1, LightGrey);              // puff folds
			f.Rect(5, 1, 7, 2, White).Rect(5, 3, 7, 1, LightGrey);        // band
		}
		void Pot(Frame f, int fire, int bubbles)
		{
			f.Rect(1, 9, 14, 1, Grey).Rect(2, 9, 12, 1, Orange);           // rim and soup
			f.Rect(2, 10, 12, 3, Slate).Dot(3, 11, Grey).Dot(4, 11, Grey); // pot with a shine
			f.Dot(0, 10, Ink).Dot(15, 10, Ink);                            // handles
			for (int x = 3; x <= 12; x++) f.Dot(x, 13, (x + fire) % 2 == 0 ? Red : Yellow);   // flames
			if (bubbles == 1) f.Dot(5, 8, White).Dot(9, 8, White);
			if (bubbles == 2) f.Dot(4, 7, LightGrey).Dot(7, 8, White).Dot(11, 7, LightGrey);
			if (bubbles == 3) f.Dot(6, 8, White).Dot(10, 8, White).Dot(3, 6, LightGrey);
		}
		void FocusEyes(Frame f) { f.Dot(5, 6, Ink).Dot(6, 7, Ink).Dot(5, 8, Ink); f.Dot(10, 6, Ink).Dot(9, 7, Ink).Dot(10, 8, Ink); }
		Frame Chef(int stir, int fire, int bubbles)
		{
			var f = Body0();
			FocusEyes(f);
			Legs(f, 12, 0, 0, 0, 0);
			Toque(f);
			Pot(f, fire, bubbles);
			f.Rect(14, 6, 1, 2, Body);                                     // arm on the pot's edge
			if (stir == 0)   // spoon leaning out, stirring the far side
				f.Dot(15, 4, Wood).Dot(14, 5, Wood).Dot(13, 6, Wood).Dot(13, 7, Wood).Dot(12, 8, Wood);
			else             // spoon upright, stirring the near side
				f.Dot(13, 4, Wood).Dot(13, 5, Wood).Dot(12, 6, Wood).Dot(12, 7, Wood).Dot(11, 8, Wood);
			return f;
		}
		var chefIdle = Body0();
		Eyes(chefIdle, 6);
		Legs(chefIdle, 12, 0, 0, 0, 0);
		Toque(chefIdle);
		chefIdle.Rect(2, 5, 1, 4, Body).Rect(1, 1, 1, 6, Wood).Rect(1, 0, 2, 2, Wood);   // spoon held up
		int chIdle = p.Add(chefIdle);
		int chStir1 = p.Add(Chef(0, 0, 1)), chStir2 = p.Add(Chef(1, 1, 2)), chStir3 = p.Add(Chef(0, 0, 3)), chStir4 = p.Add(Chef(1, 1, 0));
		Frame Taste(bool yum)
		{
			var f = Body0();
			if (yum) Eyes(f, 6, happy: true); else Eyes(f, 6, wide: true);
			Legs(f, 12, 0, 0, 0, 0);
			Toque(f);
			Pot(f, yum ? 1 : 0, 1);
			f.Rect(13, 7, 2, 1, Body).Rect(10, 8, 3, 1, Wood).Dot(9, 8, Orange);  // spoon at the mouth
			if (yum)
			{
				foreach (var (sx, sy) in new[] { (14, 0), (1, 2), (13, -3) })
					f.Dot(sx, sy, Yellow).Dot(sx - 1, sy, PaleYellow).Dot(sx + 1, sy, PaleYellow).Dot(sx, sy - 1, PaleYellow).Dot(sx, sy + 1, PaleYellow);
			}
			return f;
		}
		int chTaste = p.Add(Taste(false)), chYum = p.Add(Taste(true));

		// Skateboard: rides along, pushes with one leg, then an ollie with a kickflip.
		void Board(Frame f, int dy, int flip)
		{
			int y = 13 + dy;
			switch (flip)
			{
				case 0:   // flat, wheels down
					f.Rect(2, y, 12, 1, Maroon).Dot(1, y - 1, Maroon).Dot(14, y - 1, Maroon);
					f.Dot(4, y + 1, Yellow).Dot(11, y + 1, Yellow);
					break;
				case 1:   // tilted
					for (int i = 0; i < 10; i++) f.Dot(3 + i, y - i / 3, Maroon);
					f.Dot(5, y + 1, Yellow).Dot(11, y - 2, Yellow);
					break;
				case 2:   // mid-spin: deck seen at an angle, wheels on both sides
					f.Rect(4, y, 8, 1, Maroon).Dot(5, y - 1, Yellow).Dot(10, y + 1, Yellow);
					break;
				case 3:   // upside down, wheels up
					f.Rect(2, y, 12, 1, Maroon).Dot(4, y - 1, Yellow).Dot(11, y - 1, Yellow);
					break;
			}
		}
		Frame Skate(int dy, int flip, bool push = false, bool crouch = false, bool happy = false)
		{
			var f = crouch ? Body0(5, 1) : Body0(3);
			Eyes(f, crouch ? 7 : 5, happy: happy, wide: dy < 0 && !happy);
			if (push) { Legs(f, 11, 1, 1, 2, 2); f.Rect(4, 11, 1, 4, Body).Dot(4, 14, Shade); }   // back leg on the ground
			else if (crouch) Legs(f, 12, 2, 2, 2, 2); else Legs(f, 11, 1, 1, 1, 1);
			if (dy != 0) f.Shift(0, dy);
			Board(f, dy, flip);
			return f;
		}
		int skRide = p.Add(Skate(0, 0)), skPush = p.Add(Skate(0, 0, push: true));
		int skCrouch = p.Add(Skate(0, 0, crouch: true));
		int skAir1 = p.Add(Skate(-3, 1)), skAir2 = p.Add(Skate(-4, 2)), skAir3 = p.Add(Skate(-4, 3)), skAir4 = p.Add(Skate(-3, 2));
		int skCatch = p.Add(Skate(-2, 0, happy: true));
		int skLand = p.Add(Skate(0, 0, crouch: true, happy: true));

		// Builder: yellow hard hat, hammers a nail into the ground (it sinks with each hit).
		void HardHat(Frame f)
		{
			f.Rect(6, 0, 5, 1, Yellow).Rect(5, 1, 7, 2, Yellow).Rect(3, 3, 11, 1, Orange);
			f.Dot(8, 0, Orange).Dot(8, 1, Orange).Dot(8, 2, Orange).Dot(6, 1, PaleYellow);
		}
		Frame Hammer(int pose, int nail, bool hit = false)
		{
			var f = Body0();
			Eyes(f, 6, 1, closed: hit);
			Legs(f, 12, 0, 0, 0, 0);
			HardHat(f);
			if (nail > 0) f.Rect(15, 15 - nail, 1, nail, Grey).Dot(15, 14 - nail, Ink);   // nail sticking out
			switch (pose)
			{
				case 0:   // raised
					f.Rect(14, 2, 1, 4, Body).Rect(14, 0, 1, 2, Wood).Rect(13, -1, 3, 1, Grey);
					break;
				case 1:   // swinging
					f.Rect(14, 5, 1, 3, Body).Dot(15, 4, Wood).Dot(15, 3, Wood).Rect(14, 2, 2, 1, Grey);
					break;
				case 2:   // down on the nail
					f.Rect(14, 7, 1, 3, Body).Rect(14, 10, 1, 1, Wood).Rect(14, 11, 2, 1, Grey);
					if (hit) f.Dot(13, 11, Yellow).Dot(12, 12, PaleYellow).Dot(15, 9, PaleYellow);
					break;
			}
			return f;
		}
		int bdUp3 = p.Add(Hammer(0, 3)), bdSw3 = p.Add(Hammer(1, 3)), bdHit2 = p.Add(Hammer(2, 2, true));
		int bdUp2 = p.Add(Hammer(0, 2)), bdSw2 = p.Add(Hammer(1, 2)), bdHit1 = p.Add(Hammer(2, 1, true));
		int bdUp1 = p.Add(Hammer(0, 1)), bdSw1 = p.Add(Hammer(1, 1)), bdHit0 = p.Add(Hammer(2, 0, true));
		var bdDone = Body0();
		Eyes(bdDone, 6, happy: true);
		Legs(bdDone, 12, 0, 0, 0, 0);
		HardHat(bdDone);
		bdDone.Rect(14, 2, 1, 4, Body).Dot(14, 1, Body);                   // thumbs up
		int bdOk = p.Add(bdDone);

		// Detective: fedora and a magnifying glass; peers through it (the eye looks huge),
		// checks the floor, the other side, then spots something.
		void Fedora(Frame f)
		{
			f.Rect(3, 3, 11, 1, Ink).Rect(5, 0, 7, 3, Wood).Rect(5, 2, 7, 1, Ink).Dot(8, 0, '.');
		}
		void Lens(Frame f, int x, int y, bool bigEye, int hdx, int hdy)
		{
			f.Rect(x, y, 4, 1, Navy).Rect(x, y + 3, 4, 1, Navy).Rect(x, y, 1, 4, Navy).Rect(x + 3, y, 1, 4, Navy);
			f.Rect(x + 1, y + 1, 2, 2, bigEye ? Ink : LightBlue);
			if (bigEye) f.Dot(x + 1, y + 1, White); else f.Dot(x + 1, y + 1, White);
			f.Dot(x + 3 + hdx, y + 3 + hdy, Navy).Dot(x + 3 + 2 * hdx, y + 3 + 2 * hdy, Navy);   // handle
		}
		Frame Detective(int look)
		{
			var f = Body0();
			Legs(f, 12, 0, 0, 0, 0);
			Fedora(f);
			switch (look)
			{
				case 0: Eyes(f, 6); f.Dot(14, 8, Body); Lens(f, 12, 9, false, 1, 1); break;         // lens in hand
				case 1: f.Rect(5, 7, 2, 1, Ink); Lens(f, 9, 5, true, 1, 1); f.Dot(14, 9, Body); break; // right eye
				case 2: f.Rect(4, 7, 2, 1, Ink); Lens(f, 8, 5, true, 1, 1); f.Dot(13, 9, Body); break; // scanning
				case 3: Eyes(f, 7); Lens(f, 12, 10, false, -1, -1); f.Dot(14, 9, Body); break;      // floor
				case 4: Eyes(f, 6, wide: true); f.Rect(14, -3, 1, 3, Ink).Dot(14, 1, Ink); f.Dot(14, 8, Body); Lens(f, 12, 9, false, 1, 1); break; // "!"
			}
			return f;
		}
		var det = new int[5];
		for (int i = 0; i < 5; i++) det[i] = p.Add(Detective(i));

		// Idea: thinks, then a light bulb flickers on above its head and it hops with joy.
		void Bulb(Frame f, bool lit, int dy = 0)
		{
			char g = lit ? Yellow : LightGrey;
			f.Rect(7, -4 + dy, 3, 3, g).Dot(6, -3 + dy, g).Dot(10, -3 + dy, g);
			if (lit) f.Dot(8, -3 + dy, PaleYellow);
			f.Rect(7, -1 + dy, 3, 1, Grey).Dot(8, 0 + dy, Grey);
			if (lit) f.Dot(4, -4 + dy, PaleYellow).Dot(12, -4 + dy, PaleYellow).Dot(4, -1 + dy, PaleYellow).Dot(12, -1 + dy, PaleYellow);
		}
		var ideaDim = Body0();
		Eyes(ideaDim, 6, 1, wide: true);
		Legs(ideaDim, 12, 0, 0, 0, 0);
		Bulb(ideaDim, false, 1);
		int idDim = p.Add(ideaDim);
		var ideaLit = Body0();
		Eyes(ideaLit, 6, wide: true);
		Legs(ideaLit, 12, 0, 0, 0, 0);
		Bulb(ideaLit, true, 1);
		int idLit = p.Add(ideaLit);
		var ideaHop = Body0(3);
		Eyes(ideaHop, 5, happy: true);
		ideaHop.Dot(2, 5, Body).Dot(14, 5, Body).Dot(2, 4, Body).Dot(14, 4, Body);
		Legs(ideaHop, 11, 1, 1, 1, 1);
		Bulb(ideaHop, true, 0);
		int idHop = p.Add(ideaHop);

		p.IconFrame = fHappy;

		// ---------------------------------------------------------------- animations
		// Spawns: from the right walking, falling from the top, or already sitting.
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 45, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 10, "screenW", "areaH-imageH", 20));

		// 1 walk — now may transition into sneak, dance or scared occasionally
		p.A(1, "walk").Move("-3", "0").Speed("160").Loop("random/4+8")
			.Seq(fWalk1, fWalk2, fWalk3, fWalk4)
			.Then(50, 1).Then(10, 2).Then(8, 11).Then(5, 15).Then(5, 17).Then(5, 19)
			.Then(4, 25).Then(3, 29).Then(3, 26).Then(3, 27).Then(3, 30)
			.Then(3, 31).Then(2, 34).Then(4, 41)
			.Then(10, 16, "taskbar").Then(8, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window")
			.OnBorder(40, 18, "windowSide").OnBorder(30, 17, "petSide")
			.OnGravity(100, 5);

		// 2 turn around (flip)
		var turn = p.A(2, "turn").Move("0", "0").Speed("120");
		turn.Seq(fStand, fLeanR, fLeanR2, fLeanR, fStand);   // leans to one side as it turns round
		turn.Action = "flip";
		turn.Then(100, 1);

		// 3 drag (held by the mouse)
		p.A(3, "drag").Move("0", "0").Speed("60").Loop("40")
			.Seq(fDrag1, fDrag1, fDrag2, fDrag2)
			.Then(100, 3);

		// 4 toss (thrown): spinning body, then fall
		p.A(4, "toss").Move("0", "0").Speed("40").Loop("30")
			.Seq(fJump, fFall, fLand, fFall)
			.Then(100, 4);

		// 5 fall, 6 fall fast
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20")
			.Seq(fFall)
			.Then(100, 6)
			.OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10")
			.Seq(fFall, fFall, fFall)
			.Then(100, 6)
			.OnBorder(100, 8);

		// 7 fall soft, 8 fall hard (names the application knows)
		p.A(7, "fall soft").Move("0", "0").Speed("120", "200")
			.Seq(fLand, fLand, fCrouch, fStand, fStand)
			.Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("150", "260")
			.Seq(fLand, fLand, fLand, fSurprise, fSurprise, fStand)
			.Then(70, 1).Then(30, 13);

		// 9 jump up onto a window
		p.A(9, "jump").Move("-6", "-9", "-6", "9").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		// 10 sync (all pets celebrate together)
		p.A(10, "sync").Move("0", "0").Speed("90", "140").Loop("6")
			.Seq(fHappy, fWave, fHappy, fJump)
			.Then(100, 1);

		// 11 blink / idle — may now transition into new idle animations
		p.A(11, "blink").Move("0", "0").Speed("400", "100")
			.Seq(fStand, fBlink, fStand, fBlink, fStand)
			.Then(55, 1).Then(10, 25).Then(10, 24).Then(10, 28).Then(8, 23).Then(7, 30)
			.Then(6, 31).Then(4, 33).Then(4, 34).Then(3, 35)
			.Then(4, 36).Then(4, 37).Then(4, 38).Then(4, 39)
			.Then(3, 40).Then(3, 41).Then(3, 42).Then(3, 43).Then(3, 44);

		// 12 climb up, 13 sleep, 14 wake up
		p.A(12, "climb").Move("0", "-3").Speed("130").Loop("5000", 2)
			.Seq(fClimb1, fClimb2, fClimb1, fClimb2)
			.Then(100, 12)
			.OnBorder(100, 2);

		p.A(13, "sleep").Move("0", "0").Speed("700", "400").Loop("random/4+5", 2)
			.Seq(fSit, fSleep1, fSleep1, fSleep2, fSleep3, fSleep2)
			.Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("200", "150")
			.Seq(fSleep1, fSit, fSurprise, fStand, fHappy, fStand)
			.Then(100, 1);

		// 15 look around, 16 wave, 17 surprise, 18 flower, 19 think
		p.A(15, "look around").Move("0", "0").Speed("220", "180")
			.Seq(fStand, fLookDown, fStand, fHappy, fStand)
			.Then(100, 1);
		p.A(16, "wave").Move("0", "0").Speed("150").Loop("3", 1)
			.Seq(fHappy, fWave, fHappy, fWave)
			.Then(100, 1);
		p.A(17, "surprise").Move("0", "0").Speed("120", "220")
			.Seq(fSurprise, fSurprise, fStand, fSurprise, fStand)
			.Then(60, 1).Then(40, 2);
		p.A(18, "flower").Move("0", "0").Speed("280").Loop("3", 1)
			.Seq(fStand, fFlower, fFlower, fHappy)
			.Then(100, 1);
		p.A(19, "think").Move("0", "0").Speed("240").Loop("2", 1)
			.Seq(fStand, fThink1, fThink2, fThink1)
			.Then(100, 1);

		// 20 sit on the taskbar and watch, 21 run
		p.A(20, "sit").Move("0", "0").Speed("500", "350").Loop("random/3+4", 1)
			.Seq(fSit, fSit, fBlink, fSit)
			.Then(60, 1).Then(40, 13);
		p.A(21, "run").Move("-7", "0").Speed("90").Loop("random/3+10")
			.Seq(fRun1, fRun2)
			.Then(70, 1).Then(30, 21)
			.OnBorder(100, 2).OnBorder(40, 9, "window")
			.OnGravity(100, 5);

		// 22 look down over an edge (window border)
		p.A(22, "look down").Move("0", "0").Speed("160", "240")
			.Seq(fLookDown, fLookDown2, fLookDown2, fLookDown, fStand)
			.Then(60, 2).Then(40, 9);

		// --- NEW ANIMATIONS -----------------------------------------------------

		// 23 scared — trembles in place when startled
		p.A(23, "scared").Move("0", "0").Speed("70").Loop("6", 0)
			.Seq(fScared1, fScared2, fScared1, fScared2)
			.Then(60, 1).Then(40, 17);

		// 24 angry — furrowed brow, holds for a beat
		p.A(24, "angry").Move("0", "0").Speed("140", "200").Loop("3", 0)
			.Seq(fStand, fAngry, fAngry, fStand)
			.Then(70, 1).Then(30, 2);

		// 25 dance — happy bobbing side to side
		p.A(25, "dance").Move("0", "0").Speed("120").Loop("5", 0)
			.Seq(fDance1, fDance2, fDance1, fDance2, fHappy, fWave)
			.Then(100, 1);

		// 26 eat — nibbles a pixel snack, enjoys it
		p.A(26, "eat").Move("0", "0").Speed("180").Loop("2", 0)
			.Seq(fEat1, fEat2, fEat1, fEat2, fEat3)
			.Then(100, 1);

		// 27 stretch — body elongates upward, then relaxes back
		p.A(27, "stretch").Move("0", "0").Speed("160", "220")
			.Seq(fStand, fStretch1, fStretch1, fStretch2, fStand)
			.Then(100, 1);

		// 28 spin — windmill arms, fast alternating frames
		p.A(28, "spin").Move("0", "0").Speed("60").Loop("8", 0)
			.Seq(fSpin1, fSpin2, fSpin1, fSpin2)
			.Then(60, 1).Then(40, 17);

		// 29 sneak — low crouched shuffle, slow and cautious
		p.A(29, "sneak").Move("-2", "0").Speed("180").Loop("random/3+5", 0)
			.Seq(fSneak1, fSneak2, fSneak1, fSneak2)
			.Then(70, 1).Then(30, 2)
			.OnBorder(100, 2).OnGravity(100, 5);

		// 30 point — looks up and points, holds
		p.A(30, "point").Move("0", "0").Speed("180", "240")
			.Seq(fStand, fPoint, fPoint, fConfused, fStand)
			.Then(100, 1);

		// --- MOTION-STUDY ANIMATIONS (beats from the Codrops breakdown) ------------

		// 31 lean look — tilts left, holds, tilts right, holds, peeks down; may leap after.
		p.A(31, "lean look").Move("0", "0").Speed("140")
			.Seq(fStand, fLeanL, fLeanL2, fLeanL2, fLeanL2, fLeanL, fStand, fStand,
				 fLeanR, fLeanR2, fLeanR2, fLeanR2, fLeanR, fStand, fLookDown, fLookDown, fStand)
			.Then(50, 32).Then(50, 1);

		// 32 leap — 0.1 s crouch, arc across (fast up, slower fall), hands bounce on landing.
		p.A(32, "leap").Move("-8", "-10", "-8", "10").Speed("70")
			.Seq(fGather, fGather, fLaunch, fLaunch, fJump, fJump, fFall, fFall, fLand, fTouch, fStand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		// 33 flag — flag rises once (frames 0-2), then the wave loops from frame 3;
		// the upper body sways against the cloth while the feet stay planted.
		p.A(33, "flag").Move("0", "0").Speed("110").Loop("random/3+4", 3)
			.Seq(flag)
			.Then(100, 1);

		// 34 confetti — 8-beat stomp at 125 ms. Each raised hand fires a burst
		// (right on beat 1, left on beat 5) that rises, peaks and rains down.
		p.A(34, "confetti").Move("0", "0").Speed("125").Loop("random/3+3", 0)
			.Seq(stomp)
			.Then(100, 1);

		// 35 gym — headband toss, flex, grab two dumbbells, alternate overhead presses
		// (hold at the top), put them down, flex, throw the headband away, proud pause.
		var gym = p.A(35, "gym").Move("0", "0").Speed("85");
		void G(int f, int n) => gym.Seq(Enumerable.Repeat(f, n).ToArray());
		G(gIdle, 4); G(gBandHand, 4); G(gBandToss, 3); G(gBandLand, 2);
		G(gFlexUp, 4); G(gFlexDown, 2); G(gFlexUp, 3); G(gGrab, 4);
		for (int r = 0; r < 3; r++)
		{
			G(gCurlR, 1); G(gPressR, 3); G(gCurlR, 1); G(gRest, 2);
			G(gCurlL, 1); G(gPressL, 3); G(gCurlL, 1); G(gRest, 2);
		}
		G(gGrab, 4); G(gFlexUp, 3); G(gBandOff, 4); G(gIdle, 12);
		gym.Then(100, 1);

		// 36 laptop — wink, pull it from the air, open, type (with pauses and a blink), close.
		var lap = p.A(36, "laptop").Move("0", "0").Speed("110");
		void L(int f, int n) => lap.Seq(Enumerable.Repeat(f, n).ToArray());
		L(lpIdle, 3); L(lpWink, 3); L(lpIdle, 2); L(lpPull, 3); L(lpSet, 2); L(lpOpen, 3);
		for (int r = 0; r < 6; r++)
		{
			lap.Seq(lpType1, lpType2, lpType3, lpType4, lpType1, lpType2);
			if (r == 2) L(lpBlink, 2);
			if (r == 4) L(lpOpen, 5);            // reads what it wrote
		}
		L(lpOpen, 3); L(lpClose, 3); L(lpPull, 2); L(lpIdle, 3);
		lap.Then(100, 1);

		// 37 top hat — grab, put on, tip with a bow, dance, flower reveal, put away.
		var hat = p.A(37, "top hat").Move("0", "0").Speed("130");
		void T(int f, int n) => hat.Seq(Enumerable.Repeat(f, n).ToArray());
		T(fStand, 2); T(thGrab, 3); T(thOn, 4); T(thBlink, 1); T(thOn, 2);
		T(thTip, 2); T(thTip2, 4); T(thTip, 2); T(thOn, 3);
		for (int r = 0; r < 4; r++) hat.Seq(thDance1, thDance2);
		T(thOn, 2); T(thFlower, 6); T(thOn, 3); T(thGrab, 2); T(fHappy, 3);
		hat.Then(100, 1);

		// 38 coffee — steam curls, sips, happy sigh, sips again.
		p.A(38, "coffee").Move("0", "0").Speed("180")
			.Seq(cfHold1, cfHold2, cfHold1, cfHold2, cfSip, cfSip, cfSip, cfSigh, cfSigh2, cfSigh,
				 cfHold1, cfHold2, cfSip, cfSip, cfSip, cfSigh, cfSigh2, cfSigh, cfSigh2, fStand)
			.Then(100, 1);

		// 39 music — headphones on, bobbing to the beat.
		p.A(39, "music").Move("0", "0").Speed("160").Loop("random/3+4", 0)
			.Seq(music[0], music[1], music[2], music[1], music[0], music[1], music[3], music[1])
			.Then(100, 1);

		// 40 chef — spoon up, stirs the bubbling pot with a focused face, tastes, beams.
		var chef = p.A(40, "chef").Move("0", "0").Speed("120");
		void C(int f, int n) => chef.Seq(Enumerable.Repeat(f, n).ToArray());
		C(chIdle, 4);
		for (int r = 0; r < 5; r++) chef.Seq(chStir1, chStir2, chStir3, chStir4);
		C(chTaste, 5); C(chYum, 8);
		for (int r = 0; r < 2; r++) chef.Seq(chStir1, chStir2, chStir3, chStir4);
		C(chYum, 4); C(chIdle, 3);
		chef.Then(100, 1);

		// 41 skate — rides along (pushing now and then) and pulls an ollie with a kickflip.
		p.A(41, "skate").Move("-5", "0").Speed("90").Loop("random/3+2", 0)
			.Seq(skRide, skRide, skPush, skPush, skRide, skRide, skRide, skPush, skPush, skRide,
				 skCrouch, skCrouch, skAir1, skAir2, skAir3, skAir4, skCatch, skLand, skLand, skRide)
			.Then(100, 1)
			.OnBorder(100, 2).OnGravity(100, 5);

		// 42 builder — hard hat on, three hammer strikes drive the nail in, thumbs up.
		var bd = p.A(42, "builder").Move("0", "0").Speed("110");
		void Bd(int f, int n) => bd.Seq(Enumerable.Repeat(f, n).ToArray());
		Bd(bdUp3, 4); Bd(bdSw3, 1); Bd(bdHit2, 3);
		Bd(bdUp2, 3); Bd(bdSw2, 1); Bd(bdHit1, 3);
		Bd(bdUp1, 3); Bd(bdSw1, 1); Bd(bdHit0, 4);
		Bd(bdOk, 10);
		bd.Then(100, 1);

		// 43 detective — fedora and magnifying glass: peers, scans, checks the floor, "!".
		var dt = p.A(43, "detective").Move("0", "0").Speed("150");
		void D(int f, int n) => dt.Seq(Enumerable.Repeat(f, n).ToArray());
		D(det[0], 4); D(det[1], 5); D(det[2], 4); D(det[1], 2); D(det[3], 6); D(det[1], 3); D(det[4], 7); D(det[0], 3);
		dt.Then(100, 1);

		// 44 idea — thinks, the bulb flickers and lights up, hops with joy.
		var idea = p.A(44, "idea").Move("0", "0").Speed("140");
		void I(int f, int n) => idea.Seq(Enumerable.Repeat(f, n).ToArray());
		I(fStand, 2); I(fThink1, 3); I(fThink2, 3); I(fThink1, 2);
		idea.Seq(idDim, idLit, idDim, idLit, idDim);
		I(idLit, 5); I(idHop, 2); I(idLit, 2); I(idHop, 2); I(idLit, 4);
		idea.Then(100, 1);

		// 99 kill (the application is closing)
		p.A(99, "kill").Move("0", "1").Speed("120", "220").Fade(1, 0)
			.Seq(fSurprise, fDie1, fDie1, fDie2, fDie2)
			.Then(100, 99);

		p.Readme = """
			# Claude

			## Description

			A little desktop companion inspired by the Claude Code mascot: a rounded
			terracotta block with two dark eyes and four short legs.

			He walks along your taskbar and over your windows, climbs the screen
			borders, peeks over edges, waves, naps when left alone and sometimes
			shows up with a tiny flower.

			## About pet

			Author: Luan Felipe

			Original pixel art, drawn from scratch for desktopPet. Nothing is traced
			or copied from existing sprites: only the style (small block creature,
			terracotta tone, dark eyes, four legs) is borrowed.

			**Unofficial tribute.** The Claude Code mascot and the name "Claude" belong
			to Anthropic PBC. This pet is a fan tribute and is not affiliated with,
			endorsed by, or sponsored by Anthropic.

			## Animations

			* walk, run, turn, sneak
			* jump onto windows, fall (soft and hard landings)
			* climb the screen border, look down over an edge
			* blink, look around, think, wave, surprise, flower
			* sit on the taskbar, sleep and wake up
			* dance, eat, stretch, spin
			* scared, angry, point, confused
			* lean look and leap, flag wave, confetti stomp, gym curls
			* laptop typing, top hat (with a flower trick), coffee break, music
			* chef stirring a pot, skateboard kickflip, builder with a hammer, detective, light-bulb idea
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Claude

			## Descrição

			Um companheiro de área de trabalho inspirado no mascote do Claude Code:
			um blocão arredondado em tom terracota, com dois olhos escuros e quatro
			perninhas.

			Ele anda pela barra de tarefas e por cima das suas janelas, escala as
			bordas da tela, espia dos cantos, dá tchau, tira uma soneca quando
			fica sozinho e às vezes aparece com uma florzinha.

			## Sobre o pet

			Autor: Luan Felipe

			Pixel art original, desenhada do zero para o desktopPet. Nada foi copiado
			de sprites existentes: só o estilo (bichinho em bloco, tom terracota,
			olhos escuros, quatro perninhas).

			**Homenagem não oficial.** O mascote do Claude Code e o nome "Claude"
			pertencem à Anthropic PBC. Este pet é uma homenagem de fã e não tem
			vínculo com a Anthropic.

			## Animações

			* andar, correr, virar, se esgueirar
			* pular sobre janelas, cair (pouso suave / brusco)
			* escalar as bordas da tela
			* piscar, olhar ao redor, pensar, dar tchau, surpresa, florzinha
			* sentar na barra de tarefas, dormir e acordar
			* dançar, comer, se esticar, girar
			* assustar, braveza, apontar, confuso
			* inclinar e espiar, salto longo, agitar bandeira, confete, academia
			* notebook, cartola (com truque da flor), pausa pro café, música
			* chef mexendo a panela, manobra de skate, pedreiro com martelo, detetive, ideia (lâmpada)
			* arrastar e jogar com o mouse
			""";

		Frame.NewHeadroom = 0;
		return p;
	}
}
