namespace PetGen;

using W = Sfx.Wave;

/// <summary>
/// Sound effects of the pets made by PetGen. Each one is short and soft (the program also has a
/// volume setting), and is attached to an animation with a chance in percent: frequent animations
/// get a lower chance, so the pet is not noisy.
/// </summary>
static class PetSounds
{
	public static List<PetSound> For(string folder) => folder switch
	{
		"rex" => Rex(),
		"boo" => Boo(),
		"pingo" => Pingo(),
		"mago" => Mago(),
		"nuvi" => Nuvi(),
		"claude" => Claude(),
		_ => new List<PetSound>(),
	};

	// ------------------------------------------------------------------ shared effects

	/// <summary>Cartoon roar: a growling saw wave with breath noise. size 1 = small, 2 = big.</summary>
	static Sfx Roar(double size, int seed)
	{
		double dur = 0.7 + 0.5 * size;
		var s = new Sfx(dur + 0.2, seed);
		double f0 = 300 / size, f1 = 170 / size;
		s.Tone(0, dur, f0, f1, 0.55, W.Saw, 0.06, 0.3, vibHz: 7, vibDepth: 0.04, amHz: 32, amDepth: 0.55, harmonic: 0.4);
		s.Tone(0, dur, f0 * 1.5, f1 * 1.4, 0.18, W.Square, 0.08, 0.3, amHz: 28, amDepth: 0.6);
		s.Noise(0, dur, 0.22, 2400, 900, 0.05, 0.3, highPass: 200, amHz: 30, amDepth: 0.5);
		return s.Soften(2600);
	}

	/// <summary>"Ah… choo!": rising breath, then a noise burst. pitch 1 = normal, 1.5 = small creature.</summary>
	static Sfx Sneeze(double pitch, int seed)
	{
		var s = new Sfx(1.0, seed);
		s.Tone(0, 0.38, 260 * pitch, 420 * pitch, 0.22, W.Triangle, 0.08, 0.06, vibHz: 6, vibDepth: 0.02);
		s.Noise(0, 0.36, 0.08, 1500 * pitch, 2500 * pitch, 0.1, 0.05, highPass: 300);
		s.Noise(0.45, 0.28, 0.6, 7000, 2500, 0.004, 0.2, highPass: 900 * pitch);
		s.Tone(0.45, 0.12, 520 * pitch, 300 * pitch, 0.25, W.Triangle, 0.003, 0.08);
		return s;
	}

	static Sfx Yawn(double pitch, int seed)
	{
		var s = new Sfx(1.3, seed);
		s.Tone(0, 1.1, 330 * pitch, 220 * pitch, 0.4, W.Triangle, 0.25, 0.4, vibHz: 4.5, vibDepth: 0.03);
		s.Noise(0, 1.1, 0.05, 1200, 700, 0.3, 0.4, highPass: 200);
		return s.Soften(2200);
	}

	static Sfx Hiccup(double pitch, int seed)
	{
		var s = new Sfx(0.3, seed);
		s.Tone(0, 0.09, 420 * pitch, 900 * pitch, 0.6, W.Sine, 0.003, 0.04);
		s.Click(0, 0.3);
		return s;
	}

	static Sfx Giggle(double pitch, int seed)
	{
		var s = new Sfx(0.9, seed);
		double[] f = { 880, 820, 900, 780, 840 };
		for (int i = 0; i < f.Length; i++)
			s.Tone(i * 0.13, 0.09, f[i] * pitch, f[i] * 0.8 * pitch, 0.45, W.Triangle, 0.004, 0.05, vibHz: 18, vibDepth: 0.04);
		return s;
	}

	static Sfx Whoosh(double dur, int seed)
	{
		var s = new Sfx(dur + 0.1, seed);
		s.Noise(0, dur, 0.45, 600, 3500, dur * 0.45, dur * 0.5, highPass: 250);
		return s;
	}

	static Sfx Splat(int seed)
	{
		var s = new Sfx(0.5, seed);
		s.Noise(0, 0.32, 0.6, 2200, 400, 0.002, 0.25);
		s.Thud(0, 0.4, 110, 50, 0.18);
		return s;
	}

	static Sfx BubblePop(int seed) => new Sfx(0.2, seed).Pop(0, 0.7, 900);

	static Sfx Ding(int seed, int note = 84) => new Sfx(1.4, seed).Bell(0, Sfx.Midi(note), 0.6, 0.35).Bell(0.12, Sfx.Midi(note + 7), 0.4, 0.3);

	static Sfx Chime(int seed) => new Sfx(1.2, seed).Bell(0, Sfx.Midi(81), 0.45, 0.25).Bell(0.14, Sfx.Midi(85), 0.4, 0.25).Bell(0.28, Sfx.Midi(88), 0.4, 0.35);

	static Sfx Sparkle(double dur, bool up, int seed)
	{
		var s = new Sfx(dur + 0.6, seed);
		var r = new Random(seed);
		int n = (int)(dur * 22);
		for (int i = 0; i < n; i++)
		{
			double k = i / (double)n;
			double f = up ? 1400 + 1800 * k : 3200 - 1800 * k;
			s.Bell(i * dur / n, f * (0.9 + 0.2 * r.NextDouble()), 0.18, 0.12);
		}
		return s;
	}

	static Sfx Poof(int seed)
	{
		var s = new Sfx(0.7, seed);
		s.Noise(0, 0.5, 0.6, 3000, 300, 0.005, 0.4, highPass: 120);
		s.Tone(0, 0.2, 180, 70, 0.3, W.Sine, 0.003, 0.15);
		return s;
	}

	static Sfx Arpeggio(int seed, W wave, double step, params int[] midi) => new Sfx(step * midi.Length + 0.4, seed).Notes(0, step, 0.35, wave, midi).Soften(3500);

	static Sfx Boing(int seed)
	{
		var s = new Sfx(0.7, seed);
		s.Tone(0, 0.55, 180, 520, 0.5, W.Sine, 0.003, 0.25, vibHz: 14, vibDepth: 0.12);
		return s;
	}

	static Sfx Keys(double dur, int seed)
	{
		var s = new Sfx(dur + 0.1, seed);
		var r = new Random(seed);
		for (double t = 0; t < dur; t += 0.06 + r.NextDouble() * 0.08) s.Click(t, 0.35 + 0.25 * r.NextDouble());
		return s;
	}

	static Sfx Snore(int seed)
	{
		var s = new Sfx(1.6, seed);
		s.Noise(0, 1.0, 0.35, 500, 350, 0.35, 0.4, amHz: 22, amDepth: 0.6);
		s.Tone(0, 1.0, 90, 75, 0.15, W.Saw, 0.35, 0.4, amHz: 22, amDepth: 0.6);
		return s.Soften(900);
	}

	// ------------------------------------------------------------------ pets

	static List<PetSound> Rex()
	{
		var drumRoll = new Sfx(1.6, 41);
		for (int i = 0; i < 14; i++) drumRoll.Noise(i * 0.07, 0.06, 0.35 + i * 0.02, 5000, 2500, 0.001, 0.05, highPass: 400).Thud(i * 0.07, 0.15, 180, 120, 0.05);
		drumRoll.Thud(1.0, 0.6, 120, 50, 0.3).Noise(1.0, 0.55, 0.4, 8000, 4000, 0.002, 0.5, highPass: 2000);

		var hero = Whoosh(0.8, 42);
		hero.Notes(0.1, 0.12, 0.25, W.Square, 67, 72, 76, 79).Soften(3000);

		return new List<PetSound>
		{
			new(20, 70, Roar(1, 1)),                        // roar
			new(81, 100, Roar(1.3, 2)),                     // ROAR
			new(90, 100, Roar(1.7, 3)),                     // big roar
			new(85, 100, Sneeze(1, 4)),                     // achoo
			new(19, 60, Yawn(0.9, 5)),                      // yawn
			new(13, 35, Snore(6)),                          // sleep
			new(22, 50, new Sfx(0.5, 7).Thud(0, 0.7, 120, 40, 0.3)),   // stomp
			new(100, 90, new Sfx(0.4, 8).Thud(0, 0.6, 160, 60, 0.15).Click(0, 0.5)),   // kick
			new(79, 80, Whoosh(0.45, 9)),                   // shoot
			new(75, 100, drumRoll),                         // drumming
			new(23, 70, Arpeggio(10, W.Square, 0.09, 72, 76, 79, 84)),   // cheer
			new(53, 100, hero),                             // hero takeoff
			new(30, 60, new Sfx(0.6, 11).Tone(0, 0.12, 300, 160, 0.4, W.Triangle, 0.005, 0.06).Tone(0.2, 0.12, 300, 160, 0.4, W.Triangle, 0.005, 0.06)),   // eat meat (chomp chomp)
			new(91, 100, Chime(12)),                        // in love
			new(97, 100, Ding(13, 79)),                     // flower!
			new(31, 80, Boing(14)),                         // shocked
		};
	}

	static List<PetSound> Boo()
	{
		var boo = new Sfx(1.6, 21);
		boo.Tone(0, 1.3, 330, 230, 0.45, W.Sine, 0.2, 0.5, vibHz: 6, vibDepth: 0.05, harmonic: 0.25);
		boo.Tone(0, 1.3, 495, 350, 0.12, W.Triangle, 0.25, 0.5, vibHz: 6.5, vibDepth: 0.05);
		boo.Soften(2500);

		var drop = new Sfx(0.5, 22).Thud(0, 0.6, 150, 60, 0.25);

		return new List<PetSound>
		{
			new(20, 100, boo),                              // boo
			new(21, 80, Giggle(1.15, 23)),                  // peekaboo
			new(22, 100, Giggle(1.25, 24)),                 // giggle
			new(25, 100, Sparkle(0.45, false, 25)),         // vanish
			new(30, 100, Sparkle(0.45, true, 26)),          // appear
			new(24, 70, Chime(27)),                         // hearts
			new(58, 100, Sneeze(1.6, 28)),                  // sneeze
			new(59, 100, Hiccup(1.3, 29)),                  // hiccup
			new(60, 60, Yawn(1.4, 30)),                     // yawn
			new(40, 60, Whoosh(0.5, 31)),                   // take off
			new(78, 100, BubblePop(32)),                    // bubble pop
			new(86, 100, Splat(33)),                        // splat
			new(95, 80, drop),                              // drop pumpkin
			new(91, 90, Ding(34, 88)),                      // kiss heart
			new(92, 90, Sparkle(0.35, true, 35)),           // clone
		};
	}

	static List<PetSound> Pingo()
	{
		var flap = new Sfx(0.7, 51).Noise(0, 0.55, 0.5, 1800, 1500, 0.02, 0.1, highPass: 300, amHz: 14, amDepth: 0.9);

		var squawk = new Sfx(0.5, 52);
		squawk.Tone(0, 0.28, 650, 1050, 0.45, W.Saw, 0.005, 0.1, amHz: 45, amDepth: 0.5).Soften(3000);

		var burp = new Sfx(0.7, 53);
		burp.Tone(0, 0.45, 140, 95, 0.6, W.Saw, 0.02, 0.15, amHz: 26, amDepth: 0.7).Soften(900);

		var clap = new Sfx(0.9, 54);
		for (int i = 0; i < 4; i++) clap.Noise(i * 0.18, 0.05, 0.6, 6000, 3000, 0.001, 0.04, highPass: 900);

		var belly = new Sfx(1.0, 55);
		for (int i = 0; i < 5; i++) belly.Thud(i * 0.15, 0.55, 150, 70, 0.12);

		var trumpet = new Sfx(1.6, 56);
		int[] tune = { 67, 72, 76, 79, 76, 79 };
		double[] len = { 0.14, 0.14, 0.14, 0.3, 0.14, 0.5 };
		double at = 0;
		for (int i = 0; i < tune.Length; i++) { double f = Sfx.Midi(tune[i]); trumpet.Tone(at, len[i], f, f, 0.35, W.Saw, 0.02, 0.06, vibHz: 5.5, vibDepth: 0.01, harmonic: 0.3); at += len[i]; }
		trumpet.Soften(2400);

		var hatch = new Sfx(1.0, 57);
		hatch.Click(0, 0.6).Click(0.18, 0.6).Noise(0.32, 0.08, 0.4, 5000, 2000, 0.001, 0.06, highPass: 700);
		hatch.Tone(0.5, 0.1, 2000, 2400, 0.35, W.Sine, 0.005, 0.05).Tone(0.68, 0.1, 2100, 2500, 0.35, W.Sine, 0.005, 0.05);

		var slide = new Sfx(0.9, 58).Noise(0, 0.8, 0.3, 900, 500, 0.05, 0.3, highPass: 150);
		var sing = Arpeggio(59, W.Triangle, 0.16, 72, 74, 76, 79, 76, 72);

		return new List<PetSound>
		{
			new(22, 80, flap),                              // flap
			new(40, 100, squawk),                           // startled
			new(26, 100, Sneeze(1.4, 60)),                  // sneeze
			new(41, 100, Hiccup(1.1, 61)),                  // hiccup
			new(42, 100, burp),                             // burp
			new(33, 100, Giggle(1.05, 62)),                 // giggle
			new(45, 100, clap),                             // clap
			new(44, 100, belly),                            // belly drum
			new(77, 100, trumpet),                          // trumpet
			new(21, 70, slide),                             // belly slide
			new(31, 60, Yawn(1.2, 63)),                     // yawn
			new(49, 70, sing),                              // sing
			new(85, 100, hatch),                            // chick hatches
			new(112, 100, BubblePop(64)),                   // bubble pop
			new(103, 100, Splat(65)),                       // snow splat
			new(39, 100, Ding(66)),                         // idea
		};
	}

	static List<PetSound> Mago()
	{
		var fireworks = new Sfx(1.8, 71);
		fireworks.Tone(0, 0.55, 500, 1700, 0.25, W.Sine, 0.02, 0.05);
		var r = new Random(71);
		for (int i = 0; i < 8; i++) fireworks.Noise(0.6 + i * 0.09 + r.NextDouble() * 0.05, 0.12, 0.35, 6000, 2500, 0.001, 0.1, highPass: 600);
		fireworks.Thud(0.58, 0.5, 120, 50, 0.3);

		var boom = new Sfx(1.0, 72);
		boom.Noise(0, 0.8, 0.8, 1500, 200, 0.003, 0.6).Thud(0, 0.8, 90, 35, 0.5);

		var gulp = new Sfx(0.6, 73).Tone(0, 0.14, 320, 150, 0.5, W.Sine, 0.005, 0.08).Tone(0.22, 0.14, 300, 130, 0.5, W.Sine, 0.005, 0.08);
		var shrink = new Sfx(0.8, 74).Tone(0, 0.65, 1000, 160, 0.3, W.Square, 0.01, 0.15, vibHz: 12, vibDepth: 0.05).Soften(3000);
		var rainbow = Arpeggio(75, W.Sine, 0.08, 72, 76, 79, 84, 88, 91, 96);

		return new List<PetSound>
		{
			new(20, 100, Sparkle(0.6, true, 76)),           // spell
			new(21, 100, Poof(77)),                         // poof out
			new(23, 100, Poof(78)),                         // poof in
			new(30, 100, Ding(79)),                         // aha
			new(36, 100, Sneeze(1.0, 80)),                  // sneeze
			new(45, 60, Yawn(0.85, 81)),                    // yawn
			new(40, 100, fireworks),                        // fireworks
			new(47, 100, boom),                             // backfire
			new(31, 100, gulp),                             // hat swallow
			new(50, 100, shrink),                           // shrink
			new(51, 100, rainbow),                          // rainbow
			new(32, 80, Sparkle(0.35, true, 82)),           // rabbit
			new(35, 80, new Sfx(0.9, 83).Tone(0, 0.12, 400, 700, 0.4, W.Sine, 0.004, 0.06).Tone(0.15, 0.12, 450, 750, 0.4, W.Sine, 0.004, 0.06).Tone(0.3, 0.12, 500, 820, 0.4, W.Sine, 0.004, 0.06)),   // potion (bubbling)
		};
	}

	static List<PetSound> Nuvi()
	{
		var error = new Sfx(0.7, 91).Tone(0, 0.2, 150, 150, 0.4, W.Square, 0.005, 0.03).Tone(0.27, 0.3, 120, 120, 0.4, W.Square, 0.005, 0.05).Soften(2200);
		var hello = new Sfx(0.6, 92).Tone(0, 0.14, 660, 880, 0.35, W.Square, 0.005, 0.04).Tone(0.18, 0.2, 880, 520, 0.35, W.Square, 0.005, 0.06).Soften(3000);
		var thunder = new Sfx(2.0, 93).Noise(0, 1.7, 0.9, 500, 120, 0.02, 1.0, amHz: 3, amDepth: 0.5).Thud(0, 0.6, 70, 30, 0.6);
		var loading = new Sfx(0.9, 94).Tone(0, 0.08, 880, 880, 0.3, W.Square, 0.003, 0.03).Tone(0.25, 0.08, 880, 880, 0.3, W.Square, 0.003, 0.03).Tone(0.5, 0.08, 1175, 1175, 0.3, W.Square, 0.003, 0.03).Soften(3200);
		var battery = new Sfx(0.9, 95).Tone(0, 0.18, 600, 600, 0.35, W.Square, 0.005, 0.04).Tone(0.3, 0.3, 400, 380, 0.35, W.Square, 0.005, 0.08).Soften(2600);
		var staticNoise = new Sfx(0.8, 96).Noise(0, 0.7, 0.35, 8000, 6000, 0.01, 0.1, highPass: 1500, amHz: 17, amDepth: 0.5);
		var dizzy = new Sfx(1.1, 97).Tone(0, 1.0, 520, 520, 0.35, W.Triangle, 0.05, 0.2, vibHz: 5, vibDepth: 0.25);

		return new List<PetSound>
		{
			new(18, 45, Keys(0.9, 98)),                     // code
			new(21, 60, loading),                           // loading
			new(22, 100, error),                            // error
			new(23, 100, Arpeggio(99, W.Square, 0.08, 72, 76, 79, 84)),   // happy
			new(26, 100, thunder),                          // thunder
			new(30, 80, Arpeggio(100, W.Square, 0.07, 60, 64, 67, 72, 76)),   // download
			new(31, 80, Arpeggio(101, W.Square, 0.07, 76, 72, 67, 64, 60)),   // upload
			new(34, 100, hello),                            // hello
			new(35, 100, staticNoise),                      // static
			new(37, 100, dizzy),                            // dizzy
			new(38, 100, battery),                          // battery low
			new(40, 100, Ding(102)),                        // idea
			new(41, 60, Keys(0.6, 103)),                    // compile
			new(43, 100, new Sfx(1.2, 104).Bell(0, Sfx.Midi(88), 0.5, 0.3).Bell(0.15, Sfx.Midi(93), 0.5, 0.4)),   // commit
		};
	}

	static List<PetSound> Claude()
	{
		var confetti = new Sfx(1.2, 111);
		var r = new Random(111);
		for (int i = 0; i < 6; i++) confetti.Pop(i * 0.07 + r.NextDouble() * 0.03, 0.4, 600 + r.Next(600));
		confetti.Notes(0.45, 0.09, 0.25, W.Square, 72, 76, 79, 84).Soften(3200);

		var fanfare = new Sfx(1.6, 112);
		int[] tune = { 67, 72, 76, 79 };
		double[] len = { 0.15, 0.15, 0.15, 0.6 };
		double at = 0;
		for (int i = 0; i < tune.Length; i++) { double f = Sfx.Midi(tune[i]); fanfare.Tone(at, len[i], f, f, 0.35, W.Saw, 0.02, 0.08, vibHz: 5, vibDepth: 0.01, harmonic: 0.3); at += len[i]; }
		fanfare.Soften(2400);

		var tada = new Sfx(1.4, 113);
		foreach (int n in new[] { 72, 76, 79 }) tada.Tone(0, 0.15, Sfx.Midi(n), Sfx.Midi(n), 0.2, W.Square, 0.005, 0.05);
		foreach (int n in new[] { 72, 76, 79, 84 }) tada.Tone(0.22, 0.8, Sfx.Midi(n), Sfx.Midi(n), 0.2, W.Square, 0.005, 0.4);
		tada.Soften(3000);

		var yelp = new Sfx(0.4, 114).Tone(0, 0.22, 950, 620, 0.45, W.Triangle, 0.004, 0.1);
		var music = Arpeggio(115, W.Triangle, 0.15, 67, 71, 74, 79, 74, 71, 67);
		var gym = new Sfx(0.6, 116).Tone(0, 0.35, 200, 260, 0.35, W.Saw, 0.05, 0.12, amHz: 20, amDepth: 0.4).Soften(1200);

		return new List<PetSound>
		{
			new(17, 100, Boing(117)),                       // surprise
			new(23, 80, yelp),                              // scared
			new(33, 100, fanfare),                          // flag
			new(34, 100, confetti),                         // confetti
			new(36, 50, Keys(1.0, 118)),                    // laptop
			new(37, 100, tada),                             // top hat
			new(39, 80, music),                             // music
			new(44, 100, Ding(119)),                        // idea
			new(35, 60, gym),                               // gym (effort)
			new(18, 60, Chime(120)),                        // flower
		};
	}
}
