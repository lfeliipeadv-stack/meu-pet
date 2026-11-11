using System.Drawing;

namespace PetGen;

/// <summary>
/// "Tico" — an original baby dinosaur for DesktopPet, drawn in the crisp eSheep style:
/// 40 x 40 pixels shown at 1:1, a 1 px dark outline around every part and 4-tone shading
/// (light from the top left). Round mint-green dino seen from the side (facing left),
/// yellow belly, purple back plates and spots, stubby tail and tiny arm.
/// Every frame is built from shaded ellipses and polygons by <see cref="Painter"/>.
/// </summary>
static class TicoPet
{
	const int N = 40;   // frame size

	// palette chars: ramps go highlight, light, base, shade
	static readonly char[] Mint = { 'a', 'b', 'c', 'd' };
	static readonly char[] MintFar = { 'c', 'd', 'e', 'e' };
	static readonly char[] Belly = { 'f', 'g', 'h', 'i' };
	static readonly char[] Purple = { 'j', 'k', 'l', 'm' };
	static readonly char[] EyeW = { 'w', 'w', 'w', 'x' };
	static readonly char[] Pupil = { 'K', 'K', 'K', 'K' };
	static readonly char[] MouthIn = { 'r', 'r', 'r', 'r' };
	static readonly char[] Tongue = { 'q', 'q', 'q', 'q' };
	static readonly char[] CheekP = { 'P', 'P', 'P', 'P' };
	static readonly char[] AppleR = { 'B', 'A', 'A', 'C' };
	static readonly char[] LeafG = { 'G', 'G', 'G', 'G' };
	static readonly char[] EggR = { 'E', 'E', 'F', 'H' };
	const char Out = 'O', Spark = 'S', Zz = 'Z', White = 'W';

	public static PetDef Build()
	{
		Frame.NewHeadroom = 0;
		Frame.NewWidth = N;
		var p = new PetDef
		{
			Folder = "tico",
			Title = "Tico the Little Dino",
			PetName = "Tico",
			Author = "Luan Felipe",
			Version = "2.0",
			Info = "Tico is a round little mint-green dinosaur with purple back plates.[br]" +
				   "He runs, hops, roars (very softly), munches apples and hatches eggs.[br]" +
				   "Original character — feel free to distribute.",
			Palette = new Dictionary<char, Color>
			{
				['a'] = Color.FromArgb(196, 246, 212), ['b'] = Color.FromArgb(128, 220, 166),
				['c'] = Color.FromArgb(78, 186, 132), ['d'] = Color.FromArgb(44, 138, 98), ['e'] = Color.FromArgb(28, 102, 72),
				['f'] = Color.FromArgb(255, 248, 200), ['g'] = Color.FromArgb(254, 232, 146),
				['h'] = Color.FromArgb(242, 204, 100), ['i'] = Color.FromArgb(206, 160, 70),
				['j'] = Color.FromArgb(214, 178, 252), ['k'] = Color.FromArgb(172, 124, 232),
				['l'] = Color.FromArgb(132, 86, 196), ['m'] = Color.FromArgb(94, 56, 150),
				['w'] = Color.FromArgb(255, 255, 255), ['x'] = Color.FromArgb(200, 208, 222),
				['K'] = Color.FromArgb(22, 22, 32),
				['r'] = Color.FromArgb(150, 40, 62), ['q'] = Color.FromArgb(240, 112, 134), ['P'] = Color.FromArgb(250, 154, 164),
				['A'] = Color.FromArgb(226, 52, 52), ['B'] = Color.FromArgb(255, 132, 120), ['C'] = Color.FromArgb(164, 30, 40),
				['G'] = Color.FromArgb(84, 172, 72), ['n'] = Color.FromArgb(110, 72, 40),
				['E'] = Color.FromArgb(255, 251, 234), ['F'] = Color.FromArgb(238, 226, 192), ['H'] = Color.FromArgb(200, 180, 140),
				[Out] = Color.FromArgb(18, 26, 24),
				[Spark] = Color.FromArgb(255, 214, 64), [Zz] = Color.FromArgb(150, 180, 255), [White] = Color.FromArgb(255, 255, 255),
			},
			Scale = 1,     // drawn at screen resolution, like eSheep
			TilesX = 8,
		};

		// ------------------------------------------------------------ frames
		int F(Pose pose) => p.Add(Draw(pose));

		int fStand = F(new Pose());
		int fBlink = F(new Pose { Eye = 1 });
		int fHappy = F(new Pose { Eye = 2 });
		int fLookBack = F(new Pose { Eye = 4 });

		int[] walk =
		{
			F(new Pose { LegF = (-3, 0), LegB = (3, 0) }),
			F(new Pose { BodyDy = -1, LegF = (0, 0), LegB = (0, 1), Tail = 1 }),
			F(new Pose { LegF = (3, 0), LegB = (-3, 0) }),
			F(new Pose { BodyDy = -1, LegF = (0, 1), LegB = (0, 0) }),
		};

		int fRun1 = F(new Pose { HeadDx = -1, LegF = (-4, 0), LegB = (4, 1), Tail = 2, Arm = 2 });
		int fRun2 = F(new Pose { HeadDx = -1, BodyDy = -2, LegF = (2, 1), LegB = (-2, 0), Tail = 1, Arm = 2 });

		int fCrouch = F(new Pose { Squash = 2, Eye = 1 });
		int fJump = F(new Pose { BodyDy = -2, Eye = 2, Arm = 1, LegF = (-2, 2), LegB = (2, 2), Tail = 2 });
		int fFall = F(new Pose { Eye = 3, Mouth = 1, Arm = 1, LegF = (-1, 1), LegB = (1, 1), Tail = 2 });
		int fLand = F(new Pose { Squash = 3, Eye = 1 });

		int fSit = F(new Pose { Squash = 2 });
		int fSl1 = F(new Pose { Squash = 2, Eye = 1 });
		int fSl2 = F(new Pose { Squash = 2, Eye = 1, Z = 1 });
		int fSl3 = F(new Pose { Squash = 3, Eye = 1, Z = 2 });

		int fWave1 = F(new Pose { Eye = 2, Arm = 1 });
		int fWave2 = F(new Pose { Eye = 2, Arm = 2 });

		int fRoar1 = F(new Pose { Eye = 1, Mouth = 1, HeadDx = -1 });
		int fRoar2 = F(new Pose { Eye = 3, Mouth = 2, HeadDx = -2, HeadDy = -1, Arm = 1, Roar = true });

		int fEat1 = F(new Pose { Eye = 3, Apple = 1 });
		int fEat2 = F(new Pose { Mouth = 2, HeadDx = -1, Apple = 2 });
		int fChew1 = F(new Pose { Eye = 1, Mouth = 3 });
		int fChew2 = F(new Pose { Eye = 2, Mouth = 3, HeadDy = 1 });

		int fWag = F(new Pose { Eye = 2, Tail = 2 });

		int fEgg1 = F(new Pose { Eye = 4, Egg = 1 });
		int fEgg2 = F(new Pose { Eye = 4, Egg = 2 });
		int fEgg3 = F(new Pose { Eye = 3, Egg = 3 });
		int fEgg4 = F(new Pose { Eye = 2, Egg = 4, Arm = 1 });

		int fThink = F(new Pose { Eye = 4, HeadDy = -1, Think = true });

		int fDrag1 = F(new Pose { Eye = 3, Mouth = 1, LegF = (-2, 1), LegB = (2, 0), Arm = 1 });
		int fDrag2 = F(new Pose { Eye = 3, Mouth = 1, LegF = (2, 0), LegB = (-2, 1), Arm = 1, Tail = 2 });
		int fDie1 = F(new Pose { Eye = 1, Squash = 3 });
		int fDie2 = F(new Pose { Eye = 1, Squash = 5 });

		p.IconFrame = fHappy;
		Frame.NewWidth = Frame.Size;   // leave the default for any pet built after this one

		// ------------------------------------------------------------ spawns
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 45, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 10, "0", "areaH-imageH", 2));

		// ------------------------------------------------------------ animations
		p.A(1, "walk").Move("-3", "0").Speed("160").Loop("random/4+8")
			.Seq(walk[0], walk[1], walk[2], walk[3])
			.Then(50, 1).Then(10, 2).Then(8, 9).Then(6, 15).Then(5, 17).Then(6, 18).Then(5, 21).Then(5, 22).Then(4, 23).Then(4, 25).Then(6, 20)
			.Then(6, 16, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window").OnBorder(40, 19, "windowSide")
			.OnGravity(100, 5);

		p.A(2, "turn").Move("0", "0").Speed("130").Seq(fLookBack, fStand).Then(100, 1); p.Anims.Last().Action = "flip";

		p.A(3, "drag").Move("0", "0").Speed("70").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("40").Loop("30").Seq(fJump, fFall, fLand, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fFall).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("130", "200").Seq(fLand, fLand, fCrouch, fStand, fStand).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("160", "270").Seq(fLand, fLand, fLand, fBlink, fStand).Then(85, 1).Then(15, 13);

		p.A(9, "jump").Move("-5", "-8", "-5", "8").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1).OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window");

		p.A(10, "sync").Move("0", "0").Speed("100", "160").Loop("6").Seq(fHappy, fWave1, fHappy, fJump).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("600", "160").Seq(fStand, fBlink, fStand, fBlink, fStand).Then(80, 1).Then(20, 24);

		p.A(12, "climb").Move("0", "-3").Speed("160").Loop("5000", 2)
			.Seq(walk[0], walk[2], walk[0], walk[2]).Then(100, 12).OnBorder(100, 2);

		p.A(13, "sleep").Move("0", "0").Speed("1000", "500").Loop("random/4+8", 2)
			.Seq(fSit, fSl1, fSl1, fSl2, fSl3, fSl2).Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("240", "180").Seq(fSl1, fSit, fBlink, fHappy, fStand).Then(100, 1);

		p.A(15, "look around").Move("0", "0").Speed("350", "240").Seq(fStand, fLookBack, fBlink, fStand).Then(100, 1);
		p.A(16, "sit").Move("0", "0").Speed("800", "600").Loop("random/3+5", 2).Seq(fSit, fSit, fSl1, fSit).Then(60, 1).Then(40, 13);
		p.A(17, "wave").Move("0", "0").Speed("190").Loop("3", 1).Seq(fHappy, fWave1, fWave2, fWave1).Then(100, 1);
		p.A(18, "eat").Move("0", "0").Speed("300", "220").Seq(fEat1, fEat1, fEat2, fChew1, fChew2, fChew1, fChew2, fHappy).Then(80, 1).Then(20, 22);
		p.A(19, "look side").Move("0", "0").Speed("220", "300").Seq(fStand, fLookBack, fBlink, fStand).Then(60, 2).Then(40, 9);

		p.A(20, "run").Move("-7", "0").Speed("90").Loop("random/3+10").Seq(fRun1, fRun2)
			.Then(70, 1).Then(30, 20).OnBorder(100, 2).OnGravity(100, 5);

		p.A(21, "roar").Move("0", "0").Speed("200", "300").Seq(fStand, fRoar1, fRoar2, fRoar2, fRoar1, fHappy).Then(100, 1);
		p.A(22, "tail wag").Move("0", "0").Speed("160").Loop("4").Seq(fHappy, fWag).Then(100, 1);
		p.A(23, "egg").Move("0", "0").Speed("320", "260")
			.Seq(fLookBack, fEgg1, fEgg2, fEgg1, fEgg2, fEgg3, fEgg3, fEgg4, fEgg4, fHappy).Then(100, 1);

		p.A(24, "think").Move("0", "0").Speed("380").Loop("3", 1).Seq(fStand, fThink, fBlink, fThink).Then(100, 1);
		p.A(25, "hop").Move("0", "-4", "0", "4").Speed("90").Loop("3").Seq(fCrouch, fJump, fJump, fLand).Then(100, 1);

		p.A(99, "kill").Move("0", "1").Speed("130", "230").Fade(1, 0).Seq(fHappy, fDie1, fDie1, fDie2, fDie2).Then(100, 99);

		p.Readme = """
			# Tico the Little Dino

			## Description

			A round little mint-green dinosaur with a yellow belly and purple back plates,
			drawn in crisp 40 x 40 pixel art with outlines and shading. He trots across your
			desktop, hops, roars (very softly), munches apples, wags his tail and even
			hatches an egg now and then. Completely original — feel free to distribute,
			modify, or use him in your own projects.

			## About pet

			Author: Luan Felipe

			Original character. No existing mascot or sprite was used as a base.
			Licensed under the MIT License — see [LICENSE](../LICENSE.md).

			## Animations

			* walk, run, turn, hop
			* jump onto windows, fall (soft / hard landings)
			* climb screen borders
			* blink, look around, think, wave, tail wag
			* roar, eat an apple, hatch an egg
			* sit on the taskbar, sleep and wake up
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Tico o Dinossaurinho

			## Descrição

			Um dinossaurinho redondo verde-menta, de barriga amarela e placas roxas nas
			costas, desenhado em pixel art nítida de 40 x 40 com contorno e sombreado.
			Ele trota pela sua área de trabalho, dá pulinhos, ruge (bem baixinho), come
			maçãs, abana o rabo e de vez em quando choca um ovo. Personagem 100 %
			original — pode distribuir, modificar e usar à vontade.

			## Sobre o pet

			Autor: Luan Felipe

			Personagem original. Nenhum mascote ou sprite existente foi usado de base.
			Licenciado sob a Licença MIT — veja [LICENSE](../LICENSE.md).

			## Animações

			* andar, correr, virar, pulinhos
			* pular sobre janelas, cair (pouso suave / brusco)
			* escalar as bordas da tela
			* piscar, olhar ao redor, pensar, dar tchau, abanar o rabo
			* rugir, comer maçã, chocar um ovo
			* sentar na barra de tarefas, dormir e acordar
			* arrastar e jogar com o mouse
			""";

		return p;
	}

	// ================================================================ pose → frame

	sealed class Pose
	{
		public int BodyDy, HeadDx, HeadDy, Squash;
		public (int dx, int lift) LegF, LegB;
		public int Tail;      // 0 rest, 1 a bit up, 2 up
		public int Arm;       // 0 down, 1 raised, 2 forward
		public int Eye;       // 0 open, 1 closed, 2 happy ^, 3 wide, 4 look back
		public int Mouth;     // 0 smile, 1 open, 2 wide open, 3 chewing
		public int Apple;     // 0 none, 1 in front, 2 in the mouth
		public int Egg;       // 0 none, 1 egg, 2 wobble, 3 cracked, 4 hatched
		public int Z;         // sleep z's
		public bool Roar, Think;
	}

	static Frame Draw(Pose s)
	{
		var pt = new Painter(new Frame());
		int bd = s.BodyDy + (int)MathF.Round(s.Squash * 1.4f);   // body offset (squash lowers it)
		int hx = s.HeadDx, hy = s.HeadDy + bd;       // head follows the body

		// far (back) leg, darker
		Leg(pt, 27 + s.LegB.dx, bd, s.LegB.lift, MintFar);

		// back plates (behind the body and head)
		var plates = new (float x, float y, float dx, float dy, float size)[]
		{
			(18.5f + hx, 6.5f + hy, 0.2f, -1f, 4.6f), (23 + hx, 9.5f + hy, 0.65f, -0.8f, 4.8f),
			(26.5f, 16.5f + bd, 0.55f, -0.85f, 5f), (31, 20 + bd, 0.75f, -0.65f, 4.6f),
			(34.5f, 24.5f + bd - s.Tail, 0.85f, -0.5f, 3.8f), (37.2f, 28.3f + bd - s.Tail * 1.5f, 0.9f, -0.4f, 3f),
		};
		foreach (var (x, y, dx, dy, size) in plates)
		{
			float nx = -dy, ny = dx;    // base direction
			pt.Poly(Purple, 1, true,
				(x + nx * size * 0.6f, y + ny * size * 0.6f),
				(x + dx * size, y + dy * size),
				(x - nx * size * 0.6f, y - ny * size * 0.6f));
		}

		// body + tail as one blob
		float t = s.Tail;
		int body = pt.Blob(Mint, true, -1,
			(22, 27 + bd, 10.5f, 9.5f),
			(31, 29 + bd - t * 0.5f, 5.5f, 4.6f),
			(35.5f, 30.5f + bd - t * 1.4f, 3.8f, 3.2f),
			(38.2f, 31.2f + bd - t * 2.4f, 2.3f, 2.1f));
		// belly and spots
		pt.Blob(Belly, false, body, (16.5f, 30 + bd, 7f, 7.5f));
		pt.Blob(Purple, false, body, (26.5f, 22 + bd, 1.7f, 1.4f));
		pt.Blob(Purple, false, body, (29.5f, 27 + bd, 1.4f, 1.2f));
		pt.Blob(Purple, false, body, (25, 26.5f + bd, 1.1f, 1f));

		// near (front) leg
		Leg(pt, 17 + s.LegF.dx, bd, s.LegF.lift, Mint);

		// head: skull + snout blob
		int head = pt.Blob(Mint, true, -1, (14 + hx, 13 + hy, 9.6f, 8.4f), (7.5f + hx, 16.5f + hy, 7f, 5f));
		pt.Blob(CheekP, false, head, (14.5f + hx, 18.3f + hy, 2.2f, 1.3f));
		pt.Dot(2 + hx, 14 + hy, Out); pt.Dot(3 + hx, 14 + hy, Out);          // nostril

		// eye
		float ex = 13 + hx, ey = 11 + hy;
		switch (s.Eye)
		{
			case 0: case 3: case 4:
				float r = s.Eye == 3 ? 3.9f : 3.3f;
				pt.Blob(EyeW, true, -1, (ex, ey, r * 0.9f, r));
				float px = s.Eye == 4 ? ex + 1 : ex - 1;
				pt.Blob(Pupil, false, -1, (px, ey + 0.5f, s.Eye == 3 ? 1.2f : 1.6f, s.Eye == 3 ? 1.5f : 2.1f));
				pt.Dot((int)px - 1 + (s.Eye == 4 ? 1 : 0), (int)ey - 1, White);
				break;
			case 1:   // closed: a soft downward curve
				pt.Line(Out, (ex - 3, ey + 0.5f), (ex - 1, ey + 1.5f), (ex + 1, ey + 1.5f), (ex + 3, ey + 0.5f));
				break;
			case 2:   // happy ^
				pt.Line(Out, (ex - 3, ey + 1.5f), (ex - 1, ey - 0.5f), (ex + 1, ey - 0.5f), (ex + 3, ey + 1.5f));
				break;
		}

		// mouth
		switch (s.Mouth)
		{
			case 0:
				pt.Line(Out, (2 + hx, 19 + hy), (8 + hx, 19.5f + hy), (11 + hx, 18.5f + hy));
				break;
			case 1: case 2:
				float ry = s.Mouth == 2 ? 3.6f : 2.4f;
				int m = pt.Blob(MouthIn, true, -1, (6.5f + hx, 20 + hy, 5.2f, ry));
				pt.Blob(Tongue, false, m, (7.5f + hx, 20.5f + hy + ry * 0.5f, 3f, 1.4f));
				break;
			case 3:
				pt.Line(Out, (2 + hx, 19 + hy), (6 + hx, 20 + hy), (10 + hx, 19 + hy));
				pt.Blob(CheekP, false, head, (13.5f + hx, 18.5f + hy, 3f, 2f));
				break;
		}

		// arm (in front of the belly)
		switch (s.Arm)
		{
			case 0: pt.Blob(Mint, true, -1, (14, 28 + bd, 2f, 3.2f)); break;
			case 1: pt.Blob(Mint, true, -1, (11.5f, 22.5f + bd, 1.9f, 3.6f)); break;
			case 2: pt.Blob(Mint, true, -1, (9.5f, 27 + bd, 3.6f, 1.9f)); break;
		}

		// props
		if (s.Apple == 1) Apple(pt, 4.5f, 33.5f);
		if (s.Apple == 2) Apple(pt, 3.5f, 21.5f + hy);
		if (s.Egg > 0) Egg(pt, s.Egg);
		if (s.Z >= 1) pt.Pattern(Zz, 31, 3, "####", "..#.", ".#..", "####");
		if (s.Z >= 2) pt.Pattern(Zz, 36, 0, "###", ".#.", "###");
		if (s.Think) pt.Pattern(Spark, 29, 0, ".###.", "#...#", "...#.", "..#..", ".....", "..#..");
		if (s.Roar)
		{
			pt.Line(Spark, (1, 6 + hy), (3, 8 + hy)); pt.Line(Spark, (5, 2 + hy), (6, 5 + hy)); pt.Line(Spark, (0, 11 + hy), (2, 11.5f + hy));
		}
		return pt.F;
	}

	static void Leg(Painter pt, float x, int bd, int lift, char[] ramp)
	{
		float hip = 32 + bd, foot = 37.6f - lift * 2.5f;
		if (foot < hip + 2) foot = hip + 2;
		pt.Blob(ramp, true, -1, (x, (hip + foot) / 2, 3.4f, (foot - hip) / 2 + 1.5f), (x - 1.6f, foot, 4.4f, 2f));
	}

	static void Apple(Painter pt, float x, float y)
	{
		pt.Blob(AppleR, true, -1, (x, y, 3.3f, 3f));
		pt.Dot((int)x, (int)(y - 3.5f), 'n');
		pt.Blob(LeafG, true, -1, (x + 2f, y - 4f, 1.8f, 1.1f));
	}

	static void Egg(Painter pt, int stage)
	{
		float x = stage == 2 ? 34 : 35, y = 34.5f;
		if (stage == 4)
		{
			// bottom half of the shell, sparkles above
			int e = pt.Blob(EggR, true, -1, (x, y + 1.5f, 3.8f, 3f));
			pt.Line(Out, (x - 3.8f, y), (x - 2, y - 1), (x - 0.5f, y + 0.5f), (x + 1, y - 1), (x + 2.5f, y + 0.5f), (x + 3.8f, y - 0.5f));
			pt.Pattern(Spark, (int)x - 1, 26, ".#.", "###", ".#.");
			pt.Dot((int)x + 3, 29, Spark); pt.Dot((int)x - 4, 28, Spark);
			return;
		}
		int egg = pt.Blob(EggR, true, -1, (x, y, 3.8f, 4.8f));
		pt.Blob(Purple, false, egg, (x + 1, y + 1.5f, 1.2f, 1f));
		pt.Blob(Purple, false, egg, (x - 1.5f, y - 1.5f, 1f, 0.9f));
		if (stage == 3) pt.Line(Out, (x - 3, y - 1), (x - 1.5f, y - 2.5f), (x, y - 0.5f), (x + 1.5f, y - 2.5f), (x + 3, y - 1));
	}

	// ================================================================ painter

	/// <summary>
	/// Draws shaded shapes on a frame. Each shape gets a 1 px outline on its own edge,
	/// so a shape drawn on top of another leaves a dark line where they meet (eSheep look).
	/// Shading: normal of the ellipse vs. a light from the top left → 4 tones of the ramp.
	/// </summary>
	sealed class Painter
	{
		public readonly Frame F;
		readonly int[,] owner = new int[N, N];
		int nextId = 1;
		static readonly (float x, float y, float z) L = Norm(-0.5f, -0.7f, 0.55f);

		public Painter(Frame f) { F = f; }

		static (float, float, float) Norm(float x, float y, float z) { float l = MathF.Sqrt(x * x + y * y + z * z); return (x / l, y / l, z / l); }

		static char Tone(char[] ramp, float light) =>
			light > 0.86f ? ramp[0] : light > 0.58f ? ramp[1] : light > 0.22f ? ramp[2] : ramp[3];

		/// <summary>Union of ellipses (cx, cy, rx, ry). clip = id of a shape to stay inside (no outline then).</summary>
		public int Blob(char[] ramp, bool outline, int clip, params (float cx, float cy, float rx, float ry)[] es)
		{
			int id = nextId++;
			bool Inside(int x, int y)
			{
				foreach (var e in es)
				{
					float u = (x + 0.5f - e.cx) / e.rx, v = (y + 0.5f - e.cy) / e.ry;
					if (u * u + v * v <= 1f) return true;
				}
				return false;
			}
			var set = new List<(int x, int y, char c)>();
			for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
			{
				if (!Inside(x, y)) continue;
				if (clip >= 0 && (owner[x, y] != clip || F[x, y] == Out)) continue;
				bool edge = outline && (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1));
				char c;
				if (edge) c = Out;
				else
				{
					// light from the ellipse whose centre is closest (smoothest normal)
					float best = float.MaxValue, light = 0;
					foreach (var e in es)
					{
						float u = (x + 0.5f - e.cx) / e.rx, v = (y + 0.5f - e.cy) / e.ry, d = u * u + v * v;
						if (d > 1f || d >= best) continue;
						best = d;
						float z = MathF.Sqrt(1 - d);
						light = u * L.x + v * L.y + z * L.z;
					}
					if (best == float.MaxValue) light = 0.4f;
					c = Tone(ramp, light);
				}
				set.Add((x, y, c));
			}
			foreach (var (x, y, c) in set) { F[x, y] = c; owner[x, y] = clip >= 0 ? clip : id; }
			return id;
		}

		/// <summary>Filled polygon with outline; shaded top-light to bottom-dark.</summary>
		public int Poly(char[] ramp, int unused, bool outline, params (float x, float y)[] pts)
		{
			int id = nextId++;
			bool Inside(int x, int y)
			{
				float px = x + 0.5f, py = y + 0.5f; bool inside = false;
				for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
					if ((pts[i].y > py) != (pts[j].y > py) &&
						px < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
				return inside;
			}
			float minY = pts.Min(q => q.y), maxY = pts.Max(q => q.y), h = Math.Max(1, maxY - minY);
			for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
			{
				if (!Inside(x, y)) continue;
				bool edge = outline && (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1));
				float k = (y + 0.5f - minY) / h;
				F[x, y] = edge ? Out : Tone(ramp, 0.95f - k * 0.8f);
				owner[x, y] = id;
			}
			return id;
		}

		/// <summary>Polyline, 1 px wide.</summary>
		public void Line(char c, params (float x, float y)[] pts)
		{
			for (int i = 0; i + 1 < pts.Length; i++)
			{
				var (x0, y0) = pts[i]; var (x1, y1) = pts[i + 1];
				int n = (int)MathF.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))) + 1;
				for (int k = 0; k <= n; k++)
				{
					float tt = k / (float)n;
					F[(int)MathF.Floor(x0 + (x1 - x0) * tt), (int)MathF.Floor(y0 + (y1 - y0) * tt)] = c;
				}
			}
		}

		public void Dot(int x, int y, char c) => F[x, y] = c;

		/// <summary>Small glyph ('#' = colour c) with a dark outline around it.</summary>
		public void Pattern(char c, int x, int y, params string[] rows)
		{
			bool On(int i, int j) => j >= 0 && j < rows.Length && i >= 0 && i < rows[j].Length && rows[j][i] == '#';
			for (int j = -1; j <= rows.Length; j++) for (int i = -1; i <= rows[0].Length; i++)
			{
				if (On(i, j)) continue;
				if (On(i - 1, j) || On(i + 1, j) || On(i, j - 1) || On(i, j + 1)) F[x + i, y + j] = Out;
			}
			for (int j = 0; j < rows.Length; j++) for (int i = 0; i < rows[j].Length; i++) if (On(i, j)) F[x + i, y + j] = c;
		}
	}
}
