using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using PetGen;

namespace PetPack;

/// <summary>
/// A pet made of ready-made PNG frames. Frames are put on one common canvas, cropped to the area
/// every frame uses (feet on the bottom row), packed into a sprite sheet and written with the
/// animation graph of <see cref="Def"/> (the same <see cref="PetDef"/>/<see cref="Anim"/> model as PetGen).
/// </summary>
public sealed class PngPet
{
	/// <summary>Texts, spawns and animations. Its Frames list only carries the frame count (see <see cref="Write"/>).</summary>
	public readonly PetDef Def = new PetDef { TilesX = 8 };
	private readonly List<Bitmap> frames = new();
	private readonly Dictionary<string, int> byKey = new();
	private readonly string artDir;
	private readonly int canvas;

	/// <param name="artDir">Folder with one sub-folder per animation (frame_NNN.png).</param>
	/// <param name="canvas">Common square canvas; smaller source frames are centered on it.</param>
	public PngPet(string artDir, int canvas) { this.artDir = artDir; this.canvas = canvas; }

	/// <summary>Frame whose lowest pixel is the ground line (rows below it are cut).</summary>
	public int FootFrame;
	/// <summary>Frame used for the tray icon and the gallery card.</summary>
	public int IconFrame;

	/// <summary>Index of frame <paramref name="index"/> of animation <paramref name="anim"/> (loaded once).</summary>
	public int F(string anim, int index)
	{
		string key = anim + "/" + index;
		if (byKey.TryGetValue(key, out int id)) return id;
		string file = Path.Combine(artDir, anim, $"frame_{index:000}.png");
		using var src = new Bitmap(file);
		var bmp = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(bmp)) g.DrawImageUnscaled(src, (canvas - src.Width) / 2, (canvas - src.Height) / 2);
		frames.Add(bmp);
		return byKey[key] = frames.Count - 1;
	}

	/// <summary>Several frames of one animation.</summary>
	public int[] Fs(string anim, params int[] indexes) => indexes.Select(i => F(anim, i)).ToArray();

	/// <summary>
	/// Frame of a smaller companion drawn at <paramref name="divisor"/>x size (e.g. a pet generated at double size
	/// for quality): shrunk by keeping the most common colour of each block, centered, feet on the ground line
	/// <paramref name="groundY"/> of the common canvas.
	/// </summary>
	public int Small(string anim, int index, int divisor, int groundY)
	{
		string key = anim + "/" + index + "/" + divisor;
		if (byKey.TryGetValue(key, out int id)) return id;
		using var src = new Bitmap(Path.Combine(artDir, anim, $"frame_{index:000}.png"));
		int w = src.Width / divisor, h = src.Height / divisor;
		using var small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				// majority vote inside the block; transparent wins only if it is the majority
				var votes = new Dictionary<int, int>();
				for (int j = 0; j < divisor; j++)
					for (int i = 0; i < divisor; i++)
					{
						var c = src.GetPixel(x * divisor + i, y * divisor + j);
						int k = c.A < 128 ? 0 : c.ToArgb() | unchecked((int)0xFF000000);
						votes[k] = votes.TryGetValue(k, out int v) ? v + 1 : 1;
					}
				int best = votes.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key == 0 ? 1 : 0).First().Key;
				small.SetPixel(x, y, Color.FromArgb(best));
			}
		// lowest opaque row of the small frame sits on the ground line
		int bottom = -1;
		for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (small.GetPixel(x, y).A > 0) bottom = Math.Max(bottom, y);
		var bmp = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(bmp)) g.DrawImageUnscaled(small, (canvas - w) / 2, groundY - bottom);
		frames.Add(bmp);
		return byKey[key] = frames.Count - 1;
	}

	/// <summary>Several frames of a smaller companion (see <see cref="Small"/>).</summary>
	public int[] Smalls(string anim, int divisor, int groundY, params int[] indexes) => indexes.Select(i => Small(anim, i, divisor, groundY)).ToArray();

	/// <summary>A fully transparent frame (used while the pet is invisible).</summary>
	public int Empty()
	{
		if (byKey.TryGetValue("empty", out int id)) return id;
		frames.Add(new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb));
		return byKey["empty"] = frames.Count - 1;
	}

	/// <summary>
	/// Write animations.xml, icon.png and the READMEs into <paramref name="dir"/>.
	/// <paramref name="footFrame"/> sets the ground line: rows below its lowest pixel are cut, so the pet stands on the bottom.
	/// </summary>
	public void Write(string dir, int footFrame, int iconFrame)
	{
		// common crop: union of all pixels, bottom = feet of the standing frame
		int minX = canvas, minY = canvas, maxX = -1, maxY = -1;
		foreach (var f in frames) Bounds(f, ref minX, ref minY, ref maxX, ref maxY);
		int fx0 = canvas, fy0 = canvas, fx1 = -1, footY = -1;
		Bounds(frames[footFrame], ref fx0, ref fy0, ref fx1, ref footY);
		var crop = Rectangle.FromLTRB(minX, minY, maxX + 1, footY + 1);

		int tilesY = (frames.Count + Def.TilesX - 1) / Def.TilesX;
		using var sheet = new Bitmap(Def.TilesX * crop.Width, tilesY * crop.Height, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(sheet))
			for (int i = 0; i < frames.Count; i++)
				g.DrawImage(frames[i], new Rectangle((i % Def.TilesX) * crop.Width, (i / Def.TilesX) * crop.Height, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
		byte[] sheetPng; using (var ms = new MemoryStream()) { sheet.Save(ms, ImageFormat.Png); sheetPng = ms.ToArray(); }

		// PetDef.Xml reads the tile count from its Frames list: give it one placeholder per real frame.
		Def.Frames.Clear();
		for (int i = 0; i < frames.Count; i++) Def.Frames.Add(new Frame());

		using var icon = Icon(frames[iconFrame], 64);
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "animations.xml"), Def.Xml(sheetPng, Ico(frames[iconFrame])), new UTF8Encoding(false));
		icon.Save(Path.Combine(dir, "icon.png"), ImageFormat.Png);
		File.WriteAllText(Path.Combine(dir, "README.md"), Def.Readme, new UTF8Encoding(false));
		if (Def.ReadmePt != null) File.WriteAllText(Path.Combine(dir, "README.pt-BR.md"), Def.ReadmePt, new UTF8Encoding(false));
		Console.WriteLine($"{Def.Folder}: {frames.Count} frames of {crop.Width}x{crop.Height}, {Def.Anims.Count} animations, sheet {sheetPng.Length / 1024} KB");
	}

	private static void Bounds(Bitmap b, ref int minX, ref int minY, ref int maxX, ref int maxY)
	{
		for (int y = 0; y < b.Height; y++)
			for (int x = 0; x < b.Width; x++)
				if (b.GetPixel(x, y).A > 0) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
	}

	/// <summary>Frame cropped to its content and centered in a square (crisp, no smoothing).</summary>
	private static Bitmap Icon(Bitmap f, int size)
	{
		int minX = f.Width, minY = f.Height, maxX = -1, maxY = -1;
		Bounds(f, ref minX, ref minY, ref maxX, ref maxY);
		int w = maxX - minX + 1, h = maxY - minY + 1, side = Math.Max(w, h);
		var sq = new Bitmap(side, side, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(sq)) g.DrawImage(f, new Rectangle((side - w) / 2, (side - h) / 2, w, h), new Rectangle(minX, minY, w, h), GraphicsUnit.Pixel);
		var outp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(outp))
		{
			g.InterpolationMode = side > size ? System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic : System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			g.DrawImage(sq, new Rectangle(0, 0, size, size));
		}
		sq.Dispose();
		return outp;
	}

	/// <summary>Multi-size .ico (PNG entries, Vista+).</summary>
	private static byte[] Ico(Bitmap frame)
	{
		int[] sizes = { 16, 32, 48, 64 };
		var images = sizes.Select(s =>
		{
			using var bmp = Icon(frame, s);
			using var ms = new MemoryStream();
			bmp.Save(ms, ImageFormat.Png);
			return ms.ToArray();
		}).ToList();
		using var outp = new MemoryStream();
		using var w = new BinaryWriter(outp);
		w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
		int offset = 6 + 16 * sizes.Length;
		for (int i = 0; i < sizes.Length; i++)
		{
			w.Write((byte)sizes[i]); w.Write((byte)sizes[i]); w.Write((byte)0); w.Write((byte)0);
			w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset);
			offset += images[i].Length;
		}
		foreach (var img in images) w.Write(img);
		w.Flush();
		return outp.ToArray();
	}
}
