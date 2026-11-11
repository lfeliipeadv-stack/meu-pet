using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using PetGen;

// PetGen: draws the built-in pets of DesktopPet and writes them as eSheep pet folders.
//   args[0] = output folder for the pets      (default: pets)
//   args[1] = optional folder for preview PNGs (not shipped)
string outDir = args.Length > 0 ? args[0] : "pets";
string previewDir = args.Length > 1 ? args[1] : null;

foreach (var pet in new[] { ClaudePet.Build(), CapiPet.Build(), MagoPet.Build(), NuviPet.Build(), BooPet.Build(), PingoPet.Build(), RexPet.Build() })
{
	string dir = Path.Combine(outDir, pet.Folder);
	Directory.CreateDirectory(dir);

	byte[] sheet;
	using (var bmp = pet.Sheet())
	using (var ms = new MemoryStream())
	{
		bmp.Save(ms, ImageFormat.Png);
		sheet = ms.ToArray();
	}
	byte[] ico = Art.Ico(pet, pet.IconFrame);
	File.WriteAllText(Path.Combine(dir, "animations.xml"), pet.Xml(sheet, ico), new UTF8Encoding(false));
	using (var icon = Art.Render(pet, pet.IconFrame, 64)) icon.Save(Path.Combine(dir, "icon.png"), ImageFormat.Png);
	File.WriteAllText(Path.Combine(dir, "README.md"), pet.Readme, new UTF8Encoding(false));
	if (pet.ReadmePt != null) File.WriteAllText(Path.Combine(dir, "README.pt-BR.md"), pet.ReadmePt, new UTF8Encoding(false));

	if (previewDir != null)
	{
		Directory.CreateDirectory(previewDir);
		using var prev = Art.Preview(pet);
		prev.Save(Path.Combine(previewDir, pet.Folder + "-frames.png"), ImageFormat.Png);
	}
	Console.WriteLine($"{pet.Folder}: {pet.Frames.Count} frames, {pet.Anims.Count} animations, sheet {sheet.Length / 1024} KB");
}

/// <summary>Image helpers: icon rendering, .ico writer, preview sheet.</summary>
static class Art
{
	/// <summary>Frame cropped to its content, centered in a square, scaled by an integer factor (crisp pixels).</summary>
	public static Bitmap Render(PetDef pet, int frame, int size)
	{
		var f = pet.Frames[frame];
		int minX = f.Width, minY = f.Height, maxX = -1, maxY = -1;
		minY = f.Height;
		for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++)
			if (f.Raw(x, y) != '.') { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
		int w = maxX - minX + 1, h = maxY - minY + 1, side = Math.Max(w, h);
		int scale = Math.Max(1, size / side);
		int ox = (size - w * scale) / 2, oy = (size - h * scale) / 2;
		var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
		{
			char c = f.Raw(x, y);
			if (c == '.') continue;
			var col = pet.Palette[c];
			for (int j = 0; j < scale; j++) for (int i = 0; i < scale; i++)
			{
				int px = ox + (x - minX) * scale + i, py = oy + (y - minY) * scale + j;
				if (px >= 0 && py >= 0 && px < size && py < size) bmp.SetPixel(px, py, col);
			}
		}
		return bmp;
	}

	/// <summary>Classic .ico with 32-bit DIB entries (16, 32, 48, 64 px): readable by every Windows API.</summary>
	public static byte[] Ico(PetDef pet, int frame)
	{
		int[] sizes = { 16, 32, 48, 64 };
		var entries = new List<byte[]>();
		foreach (int s in sizes)
		{
			using var bmp = Render(pet, frame, s);
			int maskStride = (s + 31) / 32 * 4;
			var data = new byte[40 + s * s * 4 + maskStride * s];
			BitConverter.GetBytes(40).CopyTo(data, 0);
			BitConverter.GetBytes(s).CopyTo(data, 4);
			BitConverter.GetBytes(s * 2).CopyTo(data, 8);
			BitConverter.GetBytes((short)1).CopyTo(data, 12);
			BitConverter.GetBytes((short)32).CopyTo(data, 14);
			BitConverter.GetBytes(s * s * 4).CopyTo(data, 20);
			int o = 40;
			for (int y = s - 1; y >= 0; y--)
				for (int x = 0; x < s; x++)
				{
					var c = bmp.GetPixel(x, y);
					data[o++] = c.B; data[o++] = c.G; data[o++] = c.R; data[o++] = c.A;
				}
			for (int y = s - 1; y >= 0; y--)
			{
				for (int x = 0; x < s; x++)
					if (bmp.GetPixel(x, y).A < 128) data[o + x / 8] |= (byte)(0x80 >> (x % 8));
				o += maskStride;
			}
			entries.Add(data);
		}
		using var outp = new MemoryStream();
		using var w = new BinaryWriter(outp);
		w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
		int offset = 6 + 16 * sizes.Length;
		for (int i = 0; i < sizes.Length; i++)
		{
			w.Write((byte)sizes[i]); w.Write((byte)sizes[i]);
			w.Write((byte)0); w.Write((byte)0);
			w.Write((short)1); w.Write((short)32);
			w.Write(entries[i].Length); w.Write(offset);
			offset += entries[i].Length;
		}
		foreach (var e in entries) w.Write(e);
		w.Flush();
		return outp.ToArray();
	}

	/// <summary>All frames, big, on a light and a dark strip, with their index: to check the art.</summary>
	public static Bitmap Preview(PetDef pet)
	{
		const int cols = 8;
		int fw = pet.Frames[0].Width, fh = pet.Frames[0].Height;
		int scale = fw <= 16 ? 6 : 3;
		int cellW = fw * scale + 8, cellH = fh * scale + 8 + 14;
		int rows = (pet.Frames.Count + cols - 1) / cols;
		var bmp = new Bitmap(cols * cellW, rows * cellH * 2);
		using var g = Graphics.FromImage(bmp);
		g.Clear(Color.White);
		using var font = new Font("Segoe UI", 8);
		for (int half = 0; half < 2; half++)
		{
			int baseY = half * rows * cellH;
			if (half == 1) g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 34)), 0, baseY, bmp.Width, rows * cellH);
			for (int i = 0; i < pet.Frames.Count; i++)
			{
				int cx = (i % cols) * cellW, cy = baseY + (i / cols) * cellH;
				using (var p = new Pen(Color.FromArgb(half == 0 ? 225 : 70, half == 0 ? 225 : 70, half == 0 ? 225 : 70)))
					g.DrawRectangle(p, cx + 3, cy + 3, fw * scale + 1, fh * scale + 1);
				var f = pet.Frames[i];
				for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++)
				{
					char c = f.Raw(x, y);
					if (c == '.') continue;
					using var b = new SolidBrush(pet.Palette[c]);
					g.FillRectangle(b, cx + 4 + x * scale, cy + 4 + y * scale, scale, scale);
				}
				g.DrawString(i.ToString(), font, half == 0 ? Brushes.Gray : Brushes.LightGray, cx + 4, cy + fh * scale + 5);
			}
		}
		return bmp;
	}
}
