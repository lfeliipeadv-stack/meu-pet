using System.Drawing;

namespace PetGen;

/// <summary>
/// "Capi" — a freely distributable original pixel-art capybara for DesktopPet.
/// A calm, chubby pixel rodent with tiny ears and a wide nose. No similarities
/// to any existing mascot are intended; this is a fresh original character.
/// </summary>
static class CapiPet
{
	const char Brown = 'B';   // main fur
	const char Dark = 'D';    // darker fur / shade
	const char Tan = 'T';     // lighter belly and face
	const char Nose = 'N';    // dark nose / eyes
	const char Pink = 'P';    // inner ear
	const char White = 'W';   // eye shine, z
	const char Leaf = 'L';    // small plant (hat)
	const char Flower = 'F';  // flower on head

	public static PetDef Build()
	{
		var p = new PetDef
		{
			Folder = "capi",
			Title = "Capi the Capybara",
			PetName = "Capi",
			Author = "Luan Felipe",
			Version = "1.0",
			Info = "Capi is a chubby pixel capybara walking across your desktop.[br]" +
				   "She is calm, friendly, and sometimes falls asleep on your taskbar.[br]" +
				   "Original character — feel free to distribute.",
			Palette = new Dictionary<char, Color>
			{
				[Brown] = Color.FromArgb(154, 112, 76),
				[Dark] = Color.FromArgb(104, 72, 44),
				[Tan] = Color.FromArgb(206, 170, 130),
				[Nose] = Color.FromArgb(32, 22, 16),
				[Pink] = Color.FromArgb(224, 160, 170),
				[White] = Color.FromArgb(248, 244, 240),
				[Leaf] = Color.FromArgb(74, 148, 80),
				[Flower] = Color.FromArgb(252, 220, 60),
			},
			Scale = 3,
			TilesX = 8,
		};

		// --- helpers ----------------------------------------------------------
		// Body: 13 wide, 7 tall block, two tiny ears (1×2), stubby legs below.
		// Capybara silhouette: wider back than front, low-slung.
		Frame CapyBase(int bodyTop = 5, int sqY = 0)
		{
			var f = new Frame();
			int x0 = 2, w = 13, h = 7 - sqY, y0 = bodyTop;
			f.Rect(x0, y0, w, h, Brown);
			// soft corners
			f.Dot(x0, y0, '.').Dot(x0 + w - 1, y0, '.').Dot(x0, y0 + h - 1, '.').Dot(x0 + w - 1, y0 + h - 1, '.');
			// belly tan on bottom half
			f.Rect(x0 + 2, y0 + h / 2, w - 4, h / 2, Tan);
			// back is a tad higher (hump)
			f.Dot(x0 + w - 2, y0 - 1, Brown).Dot(x0 + w - 3, y0 - 1, Brown);
			// top shade ridge
			f.Rect(x0 + 1, y0, 2, 1, Dark);
			return f;
		}

		void Ears(Frame f, int y)
		{
			f.Dot(3, y, Brown).Dot(3, y - 1, Brown).Dot(3, y, Pink);
			f.Dot(5, y - 1, Brown);
		}

		void Eyes(Frame f, int y, bool closed = false, bool happy = false)
		{
			if (closed) { f.Dot(5, y, Nose).Dot(6, y, Nose); return; }
			if (happy) { f.Dot(5, y, Nose).Dot(5, y - 1, Nose).Dot(6, y - 1, Nose).Dot(6, y, Nose); return; }
			f.Dot(5, y, Nose).Dot(5, y - 1, Nose);
			f.Dot(5, y - 1, White);  // tiny shine
		}

		void Nose_(Frame f, int y) { f.Rect(5, y, 3, 2, Nose); f.Dot(6, y, Tan); }

		void Legs(Frame f, int bottom, int l1 = 0, int l2 = 0, int l3 = 0, int l4 = 0)
		{
			int[] lx = { 3, 5, 9, 11 };
			int[] lifts = { l1, l2, l3, l4 };
			for (int i = 0; i < 4; i++)
			{
				int len = 3 - lifts[i];
				for (int j = 0; j < len; j++) f.Dot(lx[i], bottom + j, j == len - 1 ? Dark : Brown);
			}
		}

		// --- 0 stand
		var stand = CapyBase();
		Ears(stand, 5); Eyes(stand, 7); Nose_(stand, 8);
		Legs(stand, 12);
		int fStand = p.Add(stand);

		// 1 blink
		var blink = CapyBase();
		Ears(blink, 5); Eyes(blink, 7, closed: true); Nose_(blink, 8);
		Legs(blink, 12);
		int fBlink = p.Add(blink);

		// 2,3,4,5 walk cycle
		int[] legLift = { 0, 1, 1, 0 };
		int[] walkFrames = new int[4];
		for (int k = 0; k < 4; k++)
		{
			bool bob = k % 2 == 1;
			var wf = CapyBase(bob ? 6 : 5);
			Ears(wf, bob ? 6 : 5); Eyes(wf, bob ? 8 : 7); Nose_(wf, bob ? 9 : 8);
			Legs(wf, bob ? 13 : 12, legLift[k], legLift[(k + 1) % 4], legLift[(k + 2) % 4], legLift[(k + 3) % 4]);
			walkFrames[k] = p.Add(wf);
		}

		// 6,7 run (body leaned, legs blur)
		var run1 = CapyBase(); run1.Rect(1, 6, 1, 3, Dark);
		Ears(run1, 5); Eyes(run1, 7); Nose_(run1, 8);
		Legs(run1, 12, 0, 2, 0, 2);
		int fRun1 = p.Add(run1);

		var run2 = CapyBase(6); run2.Rect(1, 7, 1, 3, Dark);
		Ears(run2, 6); Eyes(run2, 8); Nose_(run2, 9);
		Legs(run2, 13, 2, 0, 2, 0);
		int fRun2 = p.Add(run2);

		// 8,9 crouch + jump
		var crouch = CapyBase(7, 2);
		Ears(crouch, 7); Eyes(crouch, 8, closed: true); Nose_(crouch, 9);
		Legs(crouch, 12, 1, 1, 1, 1);
		int fCrouch = p.Add(crouch);

		var jump = CapyBase(3);
		Ears(jump, 3); Eyes(jump, 5, happy: true); Nose_(jump, 6);
		jump.Dot(1, 8, Brown).Dot(14, 8, Brown);
		Legs(jump, 11, 2, 0, 0, 2);
		int fJump = p.Add(jump);

		// 10 fall
		var fall = CapyBase(4);
		Ears(fall, 4); Eyes(fall, 6); Nose_(fall, 7);
		fall.Dot(1, 8, Brown).Dot(14, 8, Brown);
		Legs(fall, 12, 2, 0, 0, 2);
		int fFall = p.Add(fall);

		// 11 land squash
		var land = CapyBase(7, 3);
		Ears(land, 7); Eyes(land, 8, closed: true); Nose_(land, 9);
		Legs(land, 12, 2, 2, 2, 2);
		int fLand = p.Add(land);

		// 12,13,14 sleep (slow breathe, z)
		var sl1 = CapyBase(6, 1); Ears(sl1, 6); Eyes(sl1, 7, closed: true); Nose_(sl1, 8); Legs(sl1, 12, 2, 2, 2, 2); int fSl1 = p.Add(sl1);
		var sl2 = sl1.Clone(); sl2.Dot(14, 3, White).Dot(15, 2, White); int fSl2 = p.Add(sl2);
		var sl3 = CapyBase(7, 2); Ears(sl3, 7); Eyes(sl3, 8, closed: true); Nose_(sl3, 9); Legs(sl3, 12, 2, 2, 2, 2);
		sl3.Dot(14, 4, White).Dot(15, 3, White).Dot(15, 1, White); int fSl3 = p.Add(sl3);

		// 15 happy, 16 sit, 17 wave
		var happy = CapyBase(); Ears(happy, 5); Eyes(happy, 7, happy: true); Nose_(happy, 8); Legs(happy, 12); int fHappy = p.Add(happy);
		var sit = CapyBase(6, 1); Ears(sit, 6); Eyes(sit, 7); Nose_(sit, 8); Legs(sit, 12, 2, 2, 2, 2); int fSit = p.Add(sit);
		var wave = CapyBase(); Ears(wave, 5); Eyes(wave, 7, happy: true); Nose_(wave, 8);
		wave.Dot(14, 3, Brown).Dot(14, 4, Brown).Dot(13, 5, Brown);
		Legs(wave, 12); int fWave = p.Add(wave);

		// 18 flower hat
		var fl = CapyBase(); Ears(fl, 5); Eyes(fl, 7, happy: true); Nose_(fl, 8);
		fl.Dot(7, 3, Leaf).Dot(6, 2, Flower).Dot(7, 2, Flower).Dot(8, 2, Flower);
		Legs(fl, 12); int fFlower = p.Add(fl);

		// 19,20 drag / toss
		var drag1 = CapyBase(4); Ears(drag1, 4); Eyes(drag1, 6); Nose_(drag1, 7);
		drag1.Dot(3, 11, Brown).Dot(5, 12, Brown).Dot(9, 12, Brown).Dot(11, 11, Brown); int fDrag1 = p.Add(drag1);
		var drag2 = CapyBase(4); Ears(drag2, 4); Eyes(drag2, 6); Nose_(drag2, 7);
		drag2.Dot(3, 12, Brown).Dot(5, 11, Brown).Dot(9, 11, Brown).Dot(11, 12, Brown); int fDrag2 = p.Add(drag2);

		// 21 die (sink)
		var die1 = CapyBase(7, 2); Ears(die1, 7); Eyes(die1, 8, closed: true); Nose_(die1, 9); Legs(die1, 12, 2, 2, 2, 2); int fDie1 = p.Add(die1);
		var die2 = CapyBase(9, 4); Eyes(die2, 9, closed: true); Nose_(die2, 10); int fDie2 = p.Add(die2);

		p.IconFrame = fHappy;

		// --- spawns
		p.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 1));
		p.Spawns.Add((2, 45, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		p.Spawns.Add((3, 10, "0", "areaH-imageH", 2));

		// --- animations (same set as Claude)
		p.A(1, "walk").Move("-3", "0").Speed("170").Loop("random/4+8")
			.Seq(walkFrames[0], walkFrames[1], walkFrames[2], walkFrames[3])
			.Then(60, 1).Then(10, 2).Then(8, 10).Then(6, 15).Then(5, 17).Then(5, 18).Then(6, 16, "taskbar")
			.OnBorder(100, 2).OnBorder(20, 12, "vertical").OnBorder(30, 9, "window").OnBorder(40, 19, "windowSide")
			.OnGravity(100, 5);

		p.A(2, "turn").Move("0", "0").Speed("130").Seq(fStand, fStand).Then(100, 1); p.Anims.Last().Action = "flip";

		p.A(3, "drag").Move("0", "0").Speed("60").Loop("40").Seq(fDrag1, fDrag1, fDrag2, fDrag2).Then(100, 3);
		p.A(4, "toss").Move("0", "0").Speed("40").Loop("30").Seq(fJump, fFall, fLand, fFall).Then(100, 4);
		p.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fFall).Then(100, 6).OnBorder(100, 7);
		p.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fFall, fFall, fFall).Then(100, 6).OnBorder(100, 8);
		p.A(7, "fall soft").Move("0", "0").Speed("130", "200").Seq(fLand, fLand, fCrouch, fStand, fStand).Then(100, 1);
		p.A(8, "fall hard").Move("0", "0").Speed("160", "270").Seq(fLand, fLand, fLand, fStand, fStand).Then(90, 1).Then(10, 13);

		p.A(9, "jump").Move("-5", "-8", "-5", "8").Speed("70")
			.Seq(fCrouch, fCrouch, fJump, fJump, fJump, fFall, fFall, fLand, fStand)
			.Then(100, 1).OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window");

		p.A(10, "sync").Move("0", "0").Speed("100", "160").Loop("6").Seq(fHappy, fWave, fHappy, fJump).Then(100, 1);

		p.A(11, "blink").Move("0", "0").Speed("600", "160").Seq(fStand, fBlink, fStand, fBlink, fStand).Then(80, 1).Then(20, 24);

		p.A(12, "climb").Move("0", "-3").Speed("160").Loop("5000", 2)
			.Seq(walkFrames[0], walkFrames[2], walkFrames[0], walkFrames[2]).Then(100, 12).OnBorder(100, 2);

		p.A(13, "sleep").Move("0", "0").Speed("1000", "500").Loop("random/4+8", 2)
			.Seq(fSit, fSl1, fSl1, fSl2, fSl3, fSl2).Then(100, 14);
		p.A(14, "wake up").Move("0", "0").Speed("240", "180")
			.Seq(fSl1, fSit, fHappy, fStand).Then(100, 1);

		p.A(15, "look around").Move("0", "0").Speed("350", "240").Seq(fStand, fBlink, fHappy, fStand).Then(100, 1);
		p.A(16, "sit").Move("0", "0").Speed("800", "600").Loop("random/3+5", 2).Seq(fSit, fSit, fBlink, fSit).Then(60, 1).Then(40, 13);
		p.A(17, "wave").Move("0", "0").Speed("190").Loop("3", 1).Seq(fHappy, fWave, fHappy, fWave).Then(100, 1);
		p.A(18, "flower").Move("0", "0").Speed("450").Loop("3", 1).Seq(fStand, fFlower, fFlower, fHappy).Then(100, 1);
		p.A(19, "look side").Move("0", "0").Speed("220", "300").Seq(fStand, fHappy, fBlink, fStand).Then(60, 2).Then(40, 9);

		p.A(20, "run").Move("-7", "0").Speed("90").Loop("random/3+10").Seq(fRun1, fRun2)
			.Then(70, 1).Then(30, 20).OnBorder(100, 2).OnGravity(100, 5);

		p.A(24, "think").Move("0", "0").Speed("350").Loop("3", 1).Seq(fStand, fBlink, fHappy, fStand).Then(100, 1);

		p.A(99, "kill").Move("0", "1").Speed("130", "230").Fade(1, 0).Seq(fHappy, fDie1, fDie1, fDie2, fDie2).Then(100, 99);

		p.Readme = """
			# Capi the Capybara

			## Description

			A calm, chubby pixel-art capybara who wanders your desktop, sits on the
			taskbar and naps in the sun. She is completely original — feel free to
			distribute, modify, or use her in your own projects.

			## About pet

			Author: Luan Felipe

			Original character. No existing mascot or sprite was used as a base.
			Licensed under the MIT License — see [LICENSE](../LICENSE.md).

			## Animations

			* walk, run, turn
			* jump onto windows, fall (soft / hard landings)
			* climb screen borders
			* blink, look around, think, wave
			* flower hat (idle treat)
			* sit on the taskbar, sleep and wake up
			* drag and toss with the mouse
			""";
		p.ReadmePt = """
			# Capi a Capivara

			## Descrição

			Uma capivara de pixel art gordinha e tranquila que passeia pela sua área
			de trabalho, senta na barra de tarefas e tira uma soneca. É um personagem
			100 % original — pode distribuir, modificar e usar à vontade.

			## Sobre o pet

			Autor: Luan Felipe

			Personagem original. Nenhum mascote ou sprite existente foi usado de base.
			Licenciada sob a Licença MIT — veja [LICENSE](../LICENSE.md).

			## Animações

			* andar, correr, virar
			* pular sobre janelas, cair (pouso suave / brusco)
			* escalar as bordas da tela
			* piscar, olhar ao redor, pensar, dar tchau
			* chapéu de flor (mimo aleatório)
			* sentar na barra de tarefas, dormir e acordar
			* arrastar e jogar com o mouse
			""";

		return p;
	}
}
