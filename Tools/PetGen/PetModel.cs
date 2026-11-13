using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace PetGen;

/// <summary>
/// One frame, drawn with pixel primitives on a character grid
/// ('.' = transparent, other characters = palette entries).
/// The grid is 16 wide; the pet stands on rows 0..15. A pet may add "headroom":
/// extra rows above the head (y = -Headroom .. -1) for tall props such as a flag
/// or a hat. Rows are stored top-down, so the feet stay on the bottom row.
/// </summary>
public sealed class Frame
{
	public const int Size = 16;
	/// <summary>Width (and base height) given to frames created from now on (16 by default; hi-res pets use more).</summary>
	public static int NewWidth = Size;
	public readonly int W;
	/// <summary>Headroom given to frames created from now on (set by a pet builder).</summary>
	public static int NewHeadroom = 0;

	public readonly int Headroom;
	public int Width => W;
	public int Height => W + Headroom;
	/// <summary>Topmost usable y (negative when the frame has headroom).</summary>
	public int MinY => -Headroom;
	private readonly char[,] px;

	public Frame() : this(NewHeadroom, NewWidth) { }
	private Frame(int headroom, int w) { Headroom = headroom; W = w; px = new char[w, w + headroom]; Clear(); }

	public Frame Clone()
	{
		var f = new Frame(Headroom, W);
		Array.Copy(px, f.px, px.Length);
		return f;
	}

	public void Clear() { for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) px[x, y] = '.'; }

	/// <summary>Pixel by stored row (0 = top of the image), for renderers.</summary>
	public char Raw(int x, int row) => px[x, row];

	public char this[int x, int y]
	{
		get => (x < 0 || y < MinY || x >= W || y >= W) ? '.' : px[x, y + Headroom];
		set { if (x >= 0 && y >= MinY && x < W && y < W) px[x, y + Headroom] = value; }
	}

	public Frame Rect(int x, int y, int w, int h, char c)
	{
		for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) this[x + i, y + j] = c;
		return this;
	}

	public Frame Dot(int x, int y, char c) { this[x, y] = c; return this; }

	/// <summary>Move every pixel; pixels leaving the grid are lost.</summary>
	public Frame Shift(int dx, int dy)
	{
		var copy = Clone();
		Clear();
		for (int y = MinY; y < W; y++) for (int x = 0; x < W; x++)
			if (copy[x, y] != '.') this[x + dx, y + dy] = copy[x, y];
		return this;
	}

	/// <summary>Draw a small pattern given as rows of characters (spaces = keep).</summary>
	public Frame Stamp(int x, int y, params string[] rows)
	{
		for (int j = 0; j < rows.Length; j++)
			for (int i = 0; i < rows[j].Length; i++)
				if (rows[j][i] != ' ') this[x + i, y + j] = rows[j][i] == '.' ? '.' : rows[j][i];
		return this;
	}

	/// <summary>
	/// Breathing in: the upper part of the figure sinks one pixel (a row just above the middle is dropped), the feet
	/// stay where they are. Used between two equal frames so a pose held still still looks alive.
	/// </summary>
	public Frame Breathe()
	{
		int top = int.MaxValue, bottom = -1;
		for (int y = MinY; y < W; y++) for (int x = 0; x < W; x++) if (this[x, y] != '.') { top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
		var f = Clone();
		if (bottom - top < 6) return f;
		int mid = top + (bottom - top) * 55 / 100;
		for (int y = mid; y > top; y--) for (int x = 0; x < W; x++) f[x, y] = this[x, y - 1];
		for (int x = 0; x < W; x++) f[x, top] = '.';
		return f;
	}

	public Frame MirrorX()
	{
		var copy = Clone();
		for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) px[x, y] = copy.px[W - 1 - x, y];
		return this;
	}

	public void DrawTo(Bitmap bmp, int ox, int oy, IReadOnlyDictionary<char, Color> palette)
	{
		for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
			{
				char c = px[x, y];
				if (c == '.') continue;
				if (!palette.TryGetValue(c, out var col)) throw new InvalidOperationException($"No color for '{c}'");
				bmp.SetPixel(ox + x, oy + y, col);
			}
	}

	public override string ToString()
	{
		var sb = new StringBuilder();
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++) sb.Append(px[x, y]);
			sb.AppendLine();
		}
		return sb.ToString();
	}
}

/// <summary>Builder for one animation of the XML.</summary>
public sealed class Anim
{
	public int Id;
	public string Name;
	public string StartX = "0", StartY = "0", EndX = "0", EndY = "0";
	public string StartInterval = "200", EndInterval = "200";
	public int StartOffsetY, EndOffsetY;
	public double StartOpacity = 1, EndOpacity = 1;
	public int RepeatFrom;
	public string Repeat = "0";
	public List<int> Frames = new();
	public string Action;
	public List<(int prob, string only, int next)> Next = new();
	public List<(int prob, string only, int next)> Border;
	public List<(int prob, string only, int next)> Gravity;

	public Anim Move(string x, string y) { StartX = EndX = x; StartY = EndY = y; return this; }
	public Anim Move(string sx, string sy, string ex, string ey) { StartX = sx; StartY = sy; EndX = ex; EndY = ey; return this; }
	public Anim Speed(string ms) { StartInterval = EndInterval = ms; return this; }
	public Anim Speed(string start, string end) { StartInterval = start; EndInterval = end; return this; }
	public Anim Seq(params int[] f) { Frames.AddRange(f); return this; }
	public Anim Loop(string repeat, int from = 0) { Repeat = repeat; RepeatFrom = from; return this; }
	public Anim Then(int prob, int next, string only = "none") { Next.Add((prob, only, next)); return this; }
	public Anim OnBorder(int prob, int next, string only = "none") { (Border ??= new()).Add((prob, only, next)); return this; }
	public Anim OnGravity(int prob, int next, string only = "none") { (Gravity ??= new()).Add((prob, only, next)); return this; }
	public Anim Fade(double start, double end) { StartOpacity = start; EndOpacity = end; return this; }
}

/// <summary>A complete pet: palette, frames, animations and texts.</summary>
public sealed class PetDef
{
	public string Folder, Title, PetName, Author, Version, Info, Readme, ReadmePt;
	/// <summary>Frame used for the tray icon and the gallery card.</summary>
	public int IconFrame;
	public Dictionary<char, Color> Palette = new();
	public List<Frame> Frames = new();
	public List<Anim> Anims = new();
	public List<(int id, int prob, string x, string y, int next)> Spawns = new();
	/// <summary>
	/// Children: when the pet starts animation <c>anim</c>, the app opens a second, non-interactive
	/// window at (x, y) that plays animation <c>next</c> on its own (e.g. a released balloon).
	/// x/y may use imageX/imageY (the parent's position). The child closes when it leaves the screen.
	/// </summary>
	public List<(int anim, string x, string y, int next)> Childs = new();
	/// <summary>Sound effects: played when the animation starts (see Sfx and PetSounds).</summary>
	public List<PetSound> Sounds = new();
	public int Scale = 3;          // 16 px art drawn 3x = 48 px on screen
	public int TilesX = 8;

	public int Add(Frame f) { Frames.Add(f); return Frames.Count - 1; }

	/// <summary>
	/// No animation stands frozen: one that shows the same frame again and again without moving on screen (holding
	/// something before throwing it, sitting…) breathes instead (the frame alternates with <see cref="Frame.Breathe"/>).
	/// A frozen prop in a child window that ends the scene (a splat on the floor) fades out instead.
	/// </summary>
	public void Liven()
	{
		// animations played by child windows: from each child's first animation along the "next" links
		var childAnims = new HashSet<int>();
		var todo = new Queue<int>(Childs.Select(c => c.next));
		while (todo.Count > 0)
		{
			int id = todo.Dequeue();
			if (!childAnims.Add(id)) continue;
			var a = Anims.FirstOrDefault(x => x.Id == id);
			if (a != null) foreach (var n in a.Next.Concat(a.Border ?? new()).Concat(a.Gravity ?? new())) todo.Enqueue(n.next);
		}
		var breath = new Dictionary<int, int>();
		foreach (var a in Anims)
		{
			if (a.Frames.Count < 3 || a.Frames.Distinct().Count() != 1) continue;
			bool moves = a.StartX != "0" || a.StartY != "0" || a.EndX != "0" || a.EndY != "0";
			if (moves || a.StartOpacity != a.EndOpacity) continue;
			if (a.Name == "kill" || a.Name == "drag" || a.Name == "toss" || a.Name.StartsWith("fall")) continue;
			if (childAnims.Contains(a.Id))
			{
				if (a.Next.Count == 0) a.Fade(1, 0);
				continue;
			}
			int f = a.Frames[0];
			if (!breath.TryGetValue(f, out int b)) breath[f] = b = Add(Frames[f].Breathe());
			for (int i = 0; i < a.Frames.Count; i++) a.Frames[i] = (i / 2) % 2 == 0 ? f : b;
		}
	}
	public Anim A(int id, string name) { var a = new Anim { Id = id, Name = name }; Anims.Add(a); return a; }

	/// <summary>Sprite sheet (frames scaled with nearest neighbour, transparent background).</summary>
	public Bitmap Sheet()
	{
		int tilesY = (Frames.Count + TilesX - 1) / TilesX;
		int fw = Frames[0].Width, fh = Frames[0].Height;
		if (Frames.Any(f => f.Height != fh || f.Width != fw)) throw new InvalidOperationException("All frames of a pet need the same headroom");
		var small = new Bitmap(TilesX * fw, tilesY * fh, PixelFormat.Format32bppArgb);
		for (int i = 0; i < Frames.Count; i++)
			Frames[i].DrawTo(small, (i % TilesX) * fw, (i / TilesX) * fh, Palette);
		var big = new Bitmap(TilesX * fw * Scale, tilesY * fh * Scale, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(big))
		{
			g.Clear(Color.Transparent);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(small, new Rectangle(0, 0, big.Width, big.Height));
		}
		small.Dispose();
		return big;
	}

	public int TilesY => (Frames.Count + TilesX - 1) / TilesX;

	/// <summary>Multi-size .ico built from one frame (PNG-compressed entries, Vista+).</summary>
	public byte[] Icon(int frameIndex)
	{
		int[] sizes = { 16, 32, 48, 64 };
		var images = new List<byte[]>();
		foreach (int s in sizes)
		{
			using var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
			using (var tmp = new Bitmap(Frames[frameIndex].Width, Frames[frameIndex].Height, PixelFormat.Format32bppArgb))
			{
				Frames[frameIndex].DrawTo(tmp, 0, 0, Palette);
				using var g = Graphics.FromImage(bmp);
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
				g.DrawImage(tmp, new Rectangle(0, 0, s, s));
			}
			using var ms = new MemoryStream();
			bmp.Save(ms, ImageFormat.Png);
			images.Add(ms.ToArray());
		}
		using var outp = new MemoryStream();
		using var w = new BinaryWriter(outp);
		w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
		int offset = 6 + 16 * sizes.Length;
		for (int i = 0; i < sizes.Length; i++)
		{
			w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
			w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
			w.Write((byte)0); w.Write((byte)0);
			w.Write((short)1); w.Write((short)32);
			w.Write(images[i].Length);
			w.Write(offset);
			offset += images[i].Length;
		}
		foreach (var img in images) w.Write(img);
		w.Flush();
		return outp.ToArray();
	}

	public string Xml(byte[] sheetPng, byte[] ico)
	{
		var inv = CultureInfo.InvariantCulture;
		var sb = new StringBuilder();
		sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
		sb.AppendLine("<animations xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"https://esheep.petrucci.ch/ https://adrianotiger.github.io/desktopPet/Resources/animations.xsd\" xmlns=\"https://esheep.petrucci.ch/\">");
		sb.AppendLine("  <header>");
		sb.AppendLine($"    <author>{Esc(Author)}</author>");
		sb.AppendLine($"    <title>{Esc(Title)}</title>");
		sb.AppendLine($"    <petname>{Esc(PetName)}</petname>");
		sb.AppendLine($"    <version>{Esc(Version)}</version>");
		sb.AppendLine($"    <info>{Esc(Info)}</info>");
		sb.AppendLine("    <application>1</application>");
		sb.AppendLine($"    <icon><![CDATA[{Convert.ToBase64String(ico)}]]></icon>");
		sb.AppendLine("  </header>");
		sb.AppendLine("  <image>");
		sb.AppendLine($"    <tilesx>{TilesX}</tilesx>");
		sb.AppendLine($"    <tilesy>{TilesY}</tilesy>");
		sb.AppendLine($"    <png><![CDATA[{Convert.ToBase64String(sheetPng)}]]></png>");
		sb.AppendLine("    <transparency>Magenta</transparency>");
		sb.AppendLine("  </image>");
		sb.AppendLine("  <spawns>");
		foreach (var s in Spawns)
		{
			sb.AppendLine($"    <spawn id=\"{s.id}\" probability=\"{s.prob}\">");
			sb.AppendLine($"      <x>{s.x}</x>");
			sb.AppendLine($"      <y>{s.y}</y>");
			sb.AppendLine($"      <next>{s.next}</next>");
			sb.AppendLine("    </spawn>");
		}
		sb.AppendLine("  </spawns>");
		sb.AppendLine("  <animations>");
		foreach (var a in Anims)
		{
			sb.AppendLine($"    <animation id=\"{a.Id}\">");
			sb.AppendLine($"      <name>{a.Name}</name>");
			sb.AppendLine("      <start>");
			sb.AppendLine($"        <x>{a.StartX}</x>");
			sb.AppendLine($"        <y>{a.StartY}</y>");
			sb.AppendLine($"        <offsety>{a.StartOffsetY}</offsety>");
			sb.AppendLine($"        <opacity>{a.StartOpacity.ToString(inv)}</opacity>");
			sb.AppendLine($"        <interval>{a.StartInterval}</interval>");
			sb.AppendLine("      </start>");
			sb.AppendLine("      <end>");
			sb.AppendLine($"        <x>{a.EndX}</x>");
			sb.AppendLine($"        <y>{a.EndY}</y>");
			sb.AppendLine($"        <offsety>{a.EndOffsetY}</offsety>");
			sb.AppendLine($"        <opacity>{a.EndOpacity.ToString(inv)}</opacity>");
			sb.AppendLine($"        <interval>{a.EndInterval}</interval>");
			sb.AppendLine("      </end>");
			sb.AppendLine($"      <sequence repeatfrom=\"{a.RepeatFrom}\" repeat=\"{a.Repeat}\">");
			foreach (var f in a.Frames) sb.AppendLine($"        <frame>{f}</frame>");
			if (a.Action != null) sb.AppendLine($"        <action>{a.Action}</action>");
			foreach (var n in a.Next) sb.AppendLine($"        <next probability=\"{n.prob}\" only=\"{n.only}\">{n.next}</next>");
			sb.AppendLine("      </sequence>");
			if (a.Border != null)
			{
				sb.AppendLine("      <border>");
				foreach (var n in a.Border) sb.AppendLine($"        <next probability=\"{n.prob}\" only=\"{n.only}\">{n.next}</next>");
				sb.AppendLine("      </border>");
			}
			if (a.Gravity != null)
			{
				sb.AppendLine("      <gravity>");
				foreach (var n in a.Gravity) sb.AppendLine($"        <next probability=\"{n.prob}\" only=\"{n.only}\">{n.next}</next>");
				sb.AppendLine("      </gravity>");
			}
			sb.AppendLine("    </animation>");
		}
		sb.AppendLine("  </animations>");
		// XmlSerializer requires <childs> and <sounds> even when empty.
		if (Childs.Count == 0) sb.AppendLine("  <childs />");
		else
		{
			sb.AppendLine("  <childs>");
			foreach (var c in Childs)
			{
				sb.AppendLine($"    <child animationid=\"{c.anim}\">");
				sb.AppendLine($"      <x>{Esc(c.x)}</x>");
				sb.AppendLine($"      <y>{Esc(c.y)}</y>");
				sb.AppendLine($"      <next>{c.next}</next>");
				sb.AppendLine("    </child>");
			}
			sb.AppendLine("  </childs>");
		}
		if (Sounds.Count == 0) sb.AppendLine("  <sounds />");
		else
		{
			sb.AppendLine("  <sounds>");
			foreach (var s in Sounds)
			{
				sb.AppendLine($"    <sound animationid=\"{s.Anim}\">");
				sb.AppendLine($"      <probability>{s.Probability}</probability>");
				sb.AppendLine($"      <base64>{Convert.ToBase64String(s.Sound.Mp3())}</base64>");
				sb.AppendLine("    </sound>");
			}
			sb.AppendLine("  </sounds>");
		}
		sb.AppendLine("</animations>");
		return sb.ToString();
	}

	private static string Esc(string s) => System.Security.SecurityElement.Escape(s ?? "");
}
