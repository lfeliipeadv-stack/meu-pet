using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PetGen;

/// <summary>
/// Tiny sound-effect synthesizer for the pets: every sound is made from sine/square/saw waves and
/// filtered noise (nothing recorded, nothing copied), then encoded to MP3 with ffmpeg, which is what
/// the program plays. The random generator has a fixed seed, so the files are the same on every run.
/// </summary>
public sealed class Sfx
{
	public const int Rate = 22050;
	float[] buf;
	readonly Random rnd;

	public Sfx(double seconds, int seed = 1)
	{
		buf = new float[(int)(seconds * Rate)];
		rnd = new Random(seed);
	}

	public enum Wave { Sine, Square, Saw, Triangle }

	static double Osc(Wave w, double phase)
	{
		double p = phase - Math.Floor(phase);
		switch (w)
		{
			case Wave.Square: return p < 0.5 ? 0.6 : -0.6;
			case Wave.Saw: return (2 * p - 1) * 0.7;
			case Wave.Triangle: return 1 - 4 * Math.Abs(p - 0.5);
			default: return Math.Sin(2 * Math.PI * p);
		}
	}

	/// <summary>A tone gliding from f0 to f1 (exponential glide), with attack/release envelope,
	/// optional vibrato (Hz, depth as a fraction of the frequency) and amplitude wobble (growl).</summary>
	public Sfx Tone(double at, double dur, double f0, double f1, double vol, Wave w = Wave.Sine,
		double attack = 0.01, double release = 0.08, double vibHz = 0, double vibDepth = 0,
		double amHz = 0, double amDepth = 0, double harmonic = 0)
	{
		int start = (int)(at * Rate), n = (int)(dur * Rate);
		double phase = 0, phase2 = 0;
		for (int i = 0; i < n && start + i < buf.Length; i++)
		{
			double t = i / (double)Rate, k = i / (double)n;
			double f = f0 * Math.Pow(f1 / f0, k);
			if (vibHz > 0) f *= 1 + vibDepth * Math.Sin(2 * Math.PI * vibHz * t);
			phase += f / Rate;
			phase2 += 2 * f / Rate;
			double s = Osc(w, phase) + harmonic * Osc(w, phase2);
			double env = Math.Min(1, t / Math.Max(1e-4, attack)) * Math.Min(1, (dur - t) / Math.Max(1e-4, release));
			if (amHz > 0) env *= 1 - amDepth * (0.5 + 0.5 * Math.Sin(2 * Math.PI * amHz * t));
			if (start + i >= 0) buf[start + i] += (float)(s * env * vol);
		}
		return this;
	}

	/// <summary>A struck bell / chime: decaying sine with an inharmonic overtone.</summary>
	public Sfx Bell(double at, double freq, double vol, double decay = 0.6)
	{
		int start = (int)(at * Rate), n = (int)(decay * 4 * Rate);
		for (int i = 0; i < n && start + i < buf.Length; i++)
		{
			double t = i / (double)Rate;
			double e = Math.Exp(-t / decay) * Math.Min(1, t / 0.003);
			double s = Math.Sin(2 * Math.PI * freq * t) + 0.35 * Math.Sin(2 * Math.PI * freq * 2.76 * t) * Math.Exp(-t / (decay * 0.4));
			buf[start + i] += (float)(s * e * vol);
		}
		return this;
	}

	/// <summary>Filtered noise: the low-pass cutoff glides from c0 to c1 Hz; highPass removes the rumble.</summary>
	public Sfx Noise(double at, double dur, double vol, double c0 = 4000, double c1 = 4000,
		double attack = 0.005, double release = 0.1, double highPass = 0, double amHz = 0, double amDepth = 0)
	{
		int start = (int)(at * Rate), n = (int)(dur * Rate);
		double lp = 0, hpPrev = 0, hpOut = 0;
		for (int i = 0; i < n && start + i < buf.Length; i++)
		{
			double t = i / (double)Rate, k = i / (double)n;
			double cut = c0 * Math.Pow(c1 / c0, k);
			double a = 1 - Math.Exp(-2 * Math.PI * cut / Rate);
			lp += a * ((rnd.NextDouble() * 2 - 1) - lp);
			double s = lp;
			if (highPass > 0)
			{
				double rc = 1 / (2 * Math.PI * highPass), dt = 1.0 / Rate, ah = rc / (rc + dt);
				hpOut = ah * (hpOut + s - hpPrev);
				hpPrev = s;
				s = hpOut;
			}
			double env = Math.Min(1, t / Math.Max(1e-4, attack)) * Math.Min(1, (dur - t) / Math.Max(1e-4, release));
			if (amHz > 0) env *= 1 - amDepth * (0.5 + 0.5 * Math.Sin(2 * Math.PI * amHz * t));
			buf[start + i] += (float)(s * env * vol * 2.2);
		}
		return this;
	}

	/// <summary>A soft low thump (feet, falls, drums).</summary>
	public Sfx Thud(double at, double vol, double f0 = 140, double f1 = 45, double dur = 0.22)
	{
		Tone(at, dur, f0, f1, vol, Wave.Sine, 0.002, dur * 0.8);
		return Noise(at, 0.03, vol * 0.25, 1800, 600, 0.001, 0.025);
	}

	public Sfx Click(double at, double vol) => Noise(at, 0.012, vol, 7000, 3000, 0.0005, 0.01, 800);

	public Sfx Pop(double at, double vol, double f = 700)
	{
		Tone(at, 0.06, f, f * 0.35, vol, Wave.Sine, 0.001, 0.04);
		return Click(at, vol * 0.5);
	}

	/// <summary>Short notes one after the other (MIDI note numbers).</summary>
	public Sfx Notes(double at, double step, double vol, Wave w, params int[] midi)
	{
		for (int i = 0; i < midi.Length; i++)
		{
			double f = 440 * Math.Pow(2, (midi[i] - 69) / 12.0);
			Tone(at + i * step, step * 0.95, f, f, vol, w, 0.005, step * 0.4);
		}
		return this;
	}

	public static double Midi(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);

	/// <summary>Gentle low-pass over the whole sound (softens square waves).</summary>
	public Sfx Soften(double cutoff)
	{
		double a = 1 - Math.Exp(-2 * Math.PI * cutoff / Rate), y = 0;
		for (int i = 0; i < buf.Length; i++) { y += a * (buf[i] - y); buf[i] = (float)y; }
		return this;
	}

	/// <summary>Scale to a fixed peak and trim the silent tail.</summary>
	byte[] Wav(double peak)
	{
		float max = 1e-6f;
		foreach (var s in buf) max = Math.Max(max, Math.Abs(s));
		int last = buf.Length - 1;
		while (last > 0 && Math.Abs(buf[last]) < max * 0.002) last--;
		int n = Math.Min(buf.Length, last + Rate / 50);
		using (var ms = new MemoryStream())
		using (var w = new BinaryWriter(ms))
		{
			w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVE".ToCharArray());
			w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
			w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
			w.Write("data".ToCharArray()); w.Write(n * 2);
			for (int i = 0; i < n; i++)
			{
				// short fade at both ends: no click
				double fade = Math.Min(1, Math.Min(i, n - i) / (Rate * 0.004));
				w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, buf[i] / max * peak * fade)) * 32767));
			}
			w.Flush();
			return ms.ToArray();
		}
	}

	/// <summary>The sound as a small mono MP3 (ffmpeg + LAME).</summary>
	public byte[] Mp3(double peak = 0.7)
	{
		string tmp = Path.Combine(Path.GetTempPath(), "petgen-sfx");
		Directory.CreateDirectory(tmp);
		string wav = Path.Combine(tmp, Guid.NewGuid().ToString("N") + ".wav"), mp3 = Path.ChangeExtension(wav, ".mp3");
		File.WriteAllBytes(wav, Wav(peak));
		var psi = new ProcessStartInfo("ffmpeg", $"-hide_banner -loglevel error -y -i \"{wav}\" -codec:a libmp3lame -b:a 48k -ac 1 -map_metadata -1 -write_xing 0 \"{mp3}\"")
		{
			UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true,
		};
		using (var p = Process.Start(psi))
		{
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0) throw new Exception("ffmpeg: " + err);
		}
		var bytes = File.ReadAllBytes(mp3);
		File.Delete(wav); File.Delete(mp3);
		return bytes;
	}

	/// <summary>The same sound as a WAV file, to listen while designing.</summary>
	public void SaveWav(string path, double peak = 0.7) => File.WriteAllBytes(path, Wav(peak));
}

/// <summary>Sound attached to an animation: played when it starts, with this chance (percent).</summary>
public record PetSound(int Anim, int Probability, Sfx Sound);
