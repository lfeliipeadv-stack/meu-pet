using PetGen;

namespace PetPack;

/// <summary>
/// Sábio, the old grey wizard. Frames generated with PixelLab from the reference sprite
/// (art\sabio\reference.png), 3/4 view facing left ("south-west"); the app mirrors them to walk right.
/// The animation graph follows the Mago pet of PetGen.
/// </summary>
public static class SabioPet
{
	public static PngPet Build(string artRoot)
	{
		// PixelLab custom animations come on a 92x92 canvas (the character drawn at 64 px inside it).
		var p = new PngPet(Path.Combine(artRoot, "sabio"), 92);
		var d = p.Def;
		d.Folder = "sabio";
		d.Title = "Sábio, the Grey Wizard";
		d.PetName = "Sábio";
		d.Author = "Luan Felipe";
		d.Version = "1.0";
		d.Info = "An old grey wizard with a long white beard and a wooden staff.[br]He walks around, casts spells, strokes his beard, naps and vanishes in a puff of smoke.";
		d.Readme = "# Sábio\n\nAn old grey wizard with a long white beard, a wide pointed hat and a wooden staff with a curled top.\n\n" +
			"He walks over your desktop and windows, casts spells with his staff, strokes his beard while thinking, waves, " +
			"takes a nap on the taskbar and sometimes vanishes in a puff of smoke to reappear somewhere else.\n\n" +
			"Pixel art made with PixelLab from an original reference sprite.\n";
		d.ReadmePt = "# Sábio\n\nUm velho mago cinzento de barba branca comprida, chapéu pontudo de aba larga e cajado de madeira com a ponta enrolada.\n\n" +
			"Ele anda pela área de trabalho e pelas janelas, lança feitiços com o cajado, alisa a barba enquanto pensa, acena, " +
			"tira uma soneca na barra de tarefas e às vezes some numa nuvem de fumaça para reaparecer em outro lugar.\n\n" +
			"Pixel art feito com o PixelLab a partir de um sprite de referência original.\n";

		// ------------------------------------------------------------ frames
		// idle and walk are custom PixelLab animations too: its template clips cut the robe and feet off
		int[] idle = p.Fs("idle", 0, 1, 2, 3, 4);
		int stand = idle[0];
		int[] walk = p.Fs("walk", 1, 2, 3, 4, 5, 6, 7, 8);
		int[] drag = p.Fs("drag", 2, 3, 4);
		int[] fall = p.Fs("fall", 2, 3, 4);
		int[] land = p.Fs("land", 1, 2, 3, 4, 5, 6);
		int[] spell = p.Fs("spell", 1, 2, 3, 4, 5, 6, 7, 8);
		int[] sleep = p.Fs("sleep", 1, 2, 3, 4, 5, 6, 7, 8);
		int[] think = p.Fs("think", 1, 2, 3, 4, 5, 6, 7, 8);
		int[] wave = p.Fs("wave", 1, 2, 3, 4, 5, 6);
		// smoke puff: the PixelLab frames 4-6 turned into solid blobs, only the start and the fading wisps are used
		int[] poof = p.Fs("poof", 1, 2, 3, 7, 8);
		int empty = p.Empty();

		// the cat (Sábio's own pet): generated at double size for quality, shrunk to half, feet on the same ground line
		const int ground = 77, half = 2;
		int[] catWalk = p.Smalls("cat_walk", half, ground, 0, 1, 2, 3, 4, 5, 6, 7);
		int[] catIdle = p.Smalls("cat_idle", half, ground, 0, 1, 2, 3, 4, 5, 6, 7);
		int[] catRest = p.Smalls("cat_rest", half, ground, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
		int[] catLick = p.Smalls("cat_lick", half, ground, 0, 1, 2, 3, 2, 1, 8);
		int[] catSleep = p.Smalls("cat_sleep", half, ground, 0, 1, 2, 3, 4, 5, 6, 7, 8);
		int[] catAppear = p.Smalls("cat_appear", half, ground, 0, 1, 2, 3, 4, 5, 6, 7, 8);

		// ------------------------------------------------------------ animations
		// spawn 1 walks in from the right edge with the cat right behind him (animation 30)
		d.Spawns.Add((1, 45, "screenW+10", "areaH-imageH", 30));
		d.Spawns.Add((2, 35, "random*(screenW-imageW-50)/100+25", "-imageH-20", 5));
		d.Spawns.Add((3, 20, "random*(screenW-imageW-50)/100+25", "areaH-imageH", 23));

		d.A(1, "walk").Move("-2", "0").Speed("120").Loop("random/4+5").Seq(walk)
			.Then(46, 1).Then(10, 2).Then(12, 11).Then(6, 20).Then(4, 21).Then(6, 28).Then(5, 17).Then(3, 31)
			.Then(8, 13, "taskbar")
			.OnBorder(100, 2).OnBorder(30, 9, "window")
			.OnGravity(100, 5);

		var turn = d.A(2, "turn").Move("0", "0").Speed("140").Seq(stand, idle[1], stand);
		turn.Action = "flip";
		turn.Then(100, 1);

		d.A(3, "drag").Move("0", "0").Speed("90").Loop("40").Seq(drag[0], drag[1], drag[2], drag[1]).Then(100, 3);
		d.A(4, "toss").Move("0", "0").Speed("60").Loop("30").Seq(fall[0], fall[1], fall[2], fall[1]).Then(100, 4);
		d.A(5, "fall").Move("0", "2", "0", "9").Speed("90", "40").Loop("20").Seq(fall[1], fall[2]).Then(100, 6).OnBorder(100, 7);
		d.A(6, "fall fast").Move("0", "9").Speed("40").Loop("10").Seq(fall[1], fall[2]).Then(100, 6).OnBorder(100, 8);
		d.A(7, "fall soft").Move("0", "0").Speed("110", "160").Seq(land[1], land[2], land[3], land[4], land[5], stand).Then(100, 1);
		d.A(8, "fall hard").Move("0", "0").Speed("150", "240").Seq(land[2], land[3], land[3], land[3], land[4], land[5], stand).Then(80, 1).Then(20, 13);

		// jump built from the landing crouch and the falling frames (the template jump lost the staff)
		d.A(9, "jump").Move("-5", "-9", "-5", "9").Speed("70")
			.Seq(land[2], land[3], fall[0], fall[1], fall[1], fall[2], fall[2], land[3], land[4], stand)
			.Then(100, 1)
			.OnBorder(80, 7, "taskbar").OnBorder(40, 7, "window").OnBorder(20, 5, "horizontal+");

		d.A(10, "sync").Move("0", "0").Speed("110").Seq(spell).Then(100, 1);

		d.A(11, "idle").Move("0", "0").Speed("300").Seq(idle).Seq(idle)
			.Then(44, 1).Then(12, 20).Then(10, 28).Then(8, 17).Then(6, 21).Then(6, 31).Then(10, 13, "taskbar");

		// 13 sit down and sleep (the last three frames repeat), 14 get up again
		d.A(13, "sleep").Move("0", "0").Speed("380", "520").Loop("random/4+5", 5).Seq(sleep).Then(100, 14);
		d.A(14, "wake up").Move("0", "0").Speed("200").Seq(sleep[4], sleep[3], sleep[2], sleep[1], sleep[0], stand).Then(100, 1);

		d.A(17, "wave").Move("0", "0").Speed("140").Seq(wave).Then(100, 1);

		d.A(20, "spell").Move("0", "0").Speed("110").Seq(spell).Seq(stand).Then(85, 1).Then(15, 17);

		// 21-23 vanish in smoke, travel unseen, reappear
		d.A(21, "poof out").Move("0", "0").Speed("120").Seq(poof).Seq(empty).Then(100, 22);
		d.A(22, "teleport").Move("-24", "0").Speed("40").Loop("random/4+3").Seq(empty, empty)
			.Then(100, 23).OnBorder(100, 23).OnGravity(100, 23);
		d.A(23, "poof in").Move("0", "0").Speed("120").Seq(empty, poof[4], poof[3], poof[2], poof[1], poof[0], stand).Then(100, 1);

		// strokes the beard and looks around, then turns back
		d.A(28, "think").Move("0", "0").Speed("230")
			.Seq(think).Seq(think[7], think[7], think[5], think[3], think[1], stand)
			.Then(100, 1);

		d.A(99, "kill").Move("0", "0").Speed("120", "200").Fade(1, 0).Seq(poof).Seq(empty);

		// ------------------------------------------------------------ the cat
		// The cat lives in "child" windows: when the Sábio starts animation 30 or 31 the app opens a second,
		// non-interactive window that plays the cat's own chain (40-47). The chain ends with the cat walking
		// off the screen (46, no border rule): the app then closes that window.

		// 30 arrives walking with the cat right behind him, then carries on like a normal walk
		d.A(30, "arrive").Move("-2", "0").Speed("120").Loop("random/4+5").Seq(walk)
			.Then(100, 1)
			.OnBorder(100, 2).OnGravity(100, 5);
		d.Childs.Add((30, "imageX+imageW*0.65", "imageY", 40));

		// 31 raises the staff and summons the cat in a burst of sparkles in front of him
		d.A(31, "summon cat").Move("0", "0").Speed("110").Seq(spell[0], spell[1], spell[2], spell[3], spell[4], spell[5], stand)
			.Then(100, 11);
		d.Childs.Add((31, "imageX-imageW*0.6", "imageY", 47));

		d.A(40, "cat walk").Move("-2", "0").Speed("120").Loop("random/4+4").Seq(catWalk)
			.Then(30, 41).Then(20, 42).Then(12, 43).Then(20, 40).Then(18, 46)
			.OnBorder(100, 46).OnGravity(100, 45);
		d.A(41, "cat sit").Move("0", "0").Speed("230").Seq(catIdle).Seq(catRest)
			.Then(45, 40).Then(30, 42).Then(25, 43);
		d.A(42, "cat lick").Move("0", "0").Speed("170").Loop("2").Seq(catLick)
			.Then(60, 41).Then(40, 40);
		d.A(43, "cat nap").Move("0", "0").Speed("260", "520").Loop("random/4+6", 6).Seq(catSleep)
			.Then(100, 44);
		d.A(44, "cat wake").Move("0", "0").Speed("180")
			.Seq(catSleep[6], catSleep[5], catSleep[4], catSleep[3], catSleep[2], catSleep[1], catSleep[0], catIdle[0])
			.Then(100, 40);
		d.A(45, "cat fall").Move("0", "9").Speed("40").Loop("20").Seq(catIdle[0]).Then(100, 45).OnBorder(100, 41);
		// walks away and leaves the screen: no next animation, so the child window closes
		d.A(46, "cat leave").Move("-2", "0").Speed("120").Loop("80").Seq(catWalk);
		d.A(47, "cat appear").Move("0", "0").Speed("110").Seq(catAppear).Then(100, 41);

		p.FootFrame = stand;
		p.IconFrame = stand;
		return p;
	}
}
