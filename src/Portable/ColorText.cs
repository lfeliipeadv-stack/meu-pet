using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Text with colour emoji. GDI (TextRenderer) draws emoji in black and white; DirectWrite draws them in colour, like
	/// the rest of Windows 11. Only text that has an emoji takes this path (everything else stays GDI, crisp ClearType):
	/// Direct2D draws it into a transparent picture (a WIC bitmap), which is then drawn like any image, so it works on
	/// every surface (the see-through bubbles too). Pictures are cached. If DirectWrite fails, the caller falls back to GDI.
	/// Plain COM calls through the interface tables (no extra library): the slot numbers come from d2d1.h, dwrite.h, wincodec.h.
	/// </summary>
	internal static unsafe class ColorText
	{
		/// <summary>Is there an emoji (or a pictograph that has a colour version) in the text?</summary>
		public static bool HasEmoji(string s)
		{
			if (string.IsNullOrEmpty(s)) return false;
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (char.IsHighSurrogate(c) && i + 1 < s.Length)
				{
					int cp = char.ConvertToUtf32(c, s[i + 1]);
					if (cp >= 0x1F000 && cp <= 0x1FAFF) return true;
					i++;
				}
				// symbols that are emoji only with the "emoji style" mark (☀️ ✉️ ❤️ ⚙️), or always (✅ ✨ ⚡ ❌…); plain ✕ ✓ ★ stay text
				else if (i + 1 < s.Length && s[i + 1] == '️') return true;
				else if (EmojiStyle.Contains(c)) return true;
			}
			return false;
		}

		static readonly HashSet<char> EmojiStyle = new HashSet<char>("⌚⌛⏩⏪⏫⏬⏰⏳◽◾☔☕♿⚓⚡⚪⚫⚽⚾⛄⛅⛎⛔⛪⛲⛳⛵⛺⛽✅✊✋✨❌❎❓❔❕❗➕➖➗➰➿⬛⬜⭐⭕");

		static bool failed;
		static IntPtr d2d, dwrite, wic;

		static bool Init()
		{
			if (failed) return false;
			if (d2d != IntPtr.Zero) return true;
			try
			{
				var iidD2D = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
				Check(D2D1CreateFactory(0 /* single threaded */, ref iidD2D, IntPtr.Zero, out d2d));
				var iidDW = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
				Check(DWriteCreateFactory(0 /* shared */, ref iidDW, out dwrite));
				var clsid = new Guid("cacaf262-9370-4615-a13b-9f5539da4c0a");
				var iidWic = new Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
				Check(CoCreateInstance(ref clsid, IntPtr.Zero, 1 /* in proc */, ref iidWic, out wic));
				return true;
			}
			catch (Exception ex)
			{
				failed = true;
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "colour emoji off: " + ex.Message);
				return false;
			}
		}

		// ------------------------------------------------------------------ public: like TextRenderer, with colour emoji

		/// <summary>Draw like TextRenderer.DrawText. False if it could not (then draw it with GDI).</summary>
		public static bool Draw(Graphics g, string text, Font font, Rectangle r, Color color, TextFormatFlags flags)
		{
			if (r.Width <= 0 || r.Height <= 0) return true;
			var bmp = Render(text, font, r.Size, color, flags, g.DpiY);
			if (bmp == null) return false;
			var old = g.CompositingMode;
			g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
			g.DrawImage(bmp, new Rectangle(r.X, r.Y, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
			g.CompositingMode = old;
			return true;
		}

		/// <summary>Size like TextRenderer.MeasureText, or Size.Empty if it could not.</summary>
		public static Size Measure(string text, Font font, int maxWidth, TextFormatFlags flags, float dpi)
		{
			if (!Init()) return Size.Empty;
			IntPtr format = IntPtr.Zero, layout = IntPtr.Zero;
			try
			{
				bool single = (flags & TextFormatFlags.SingleLine) != 0 || (flags & TextFormatFlags.WordBreak) == 0;
				(format, layout) = Layout(text, font, single || maxWidth == int.MaxValue ? 100000f : maxWidth, 100000f, flags, dpi);
				Metrics m;
				Check(((delegate* unmanaged[Stdcall]<IntPtr, Metrics*, int>)Slot(layout, 60))(layout, &m));
				// like GDI: a little room on the sides
				return new Size((int)Math.Ceiling(m.widthIncludingTrailingWhitespace) + Pad(font, dpi) * 2, (int)Math.Ceiling(m.height));
			}
			catch (Exception) { return Size.Empty; }
			finally { Release(layout); Release(format); }
		}

		// ------------------------------------------------------------------ rendering, cached

		readonly struct Key : IEquatable<Key>
		{
			public readonly string Text, Font; public readonly Size Size; public readonly int Color, Flags; public readonly float Dpi;
			public Key(string t, Font f, Size s, Color c, TextFormatFlags fl, float dpi) { Text = t; Font = f.Name + "|" + f.SizeInPoints + "|" + (int)f.Style; Size = s; Color = c.ToArgb(); Flags = (int)fl; Dpi = dpi; }
			public bool Equals(Key o) => Text == o.Text && Font == o.Font && Size == o.Size && Color == o.Color && Flags == o.Flags && Dpi == o.Dpi;
			public override bool Equals(object o) => o is Key k && Equals(k);
			public override int GetHashCode() => HashCode.Combine(Text, Font, Size, Color, Flags, Dpi);
		}

		static readonly Dictionary<Key, Bitmap> cache = new Dictionary<Key, Bitmap>();
		static readonly Queue<Key> order = new Queue<Key>();

		static Bitmap Render(string text, Font font, Size size, Color color, TextFormatFlags flags, float dpi)
		{
			var key = new Key(text, font, size, color, flags, dpi);
			if (cache.TryGetValue(key, out var hit)) return hit;
			if (!Init()) return null;
			var bmp = RenderNow(text, font, size, color, flags, dpi);
			if (bmp == null) return null;
			cache[key] = bmp;
			order.Enqueue(key);
			while (order.Count > 150) { var old = order.Dequeue(); if (cache.TryGetValue(old, out var b)) { cache.Remove(old); b.Dispose(); } }
			return bmp;
		}

		static Bitmap RenderNow(string text, Font font, Size size, Color color, TextFormatFlags flags, float dpi)
		{
			IntPtr wicBmp = IntPtr.Zero, target = IntPtr.Zero, brush = IntPtr.Zero, format = IntPtr.Zero, layout = IntPtr.Zero;
			try
			{
				int w = size.Width, h = size.Height;
				// the picture: w × h, 32-bit premultiplied, transparent
				var pbgra = new Guid("6fddc324-4e03-4bfe-b185-3d77768dc910");
				Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, Guid*, int, IntPtr*, int>)Slot(wic, 17))(wic, (uint)w, (uint)h, &pbgra, 2 /* cache on load */, &wicBmp));
				var props = new RenderTargetProperties { type = 1 /* software */, format = 87 /* B8G8R8A8_UNORM */, alphaMode = 1 /* premultiplied */, dpiX = 96, dpiY = 96 };
				Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, RenderTargetProperties*, IntPtr*, int>)Slot(d2d, 13))(d2d, wicBmp, &props, &target));
				// grey smoothing: ClearType needs an opaque background
				((delegate* unmanaged[Stdcall]<IntPtr, int, void>)Slot(target, 34))(target, 2 /* grayscale */);
				var c = new ColorF { r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f };
				Check(((delegate* unmanaged[Stdcall]<IntPtr, ColorF*, IntPtr, IntPtr*, int>)Slot(target, 8))(target, &c, IntPtr.Zero, &brush));

				int pad = Pad(font, dpi);
				bool single = (flags & TextFormatFlags.SingleLine) != 0;
				(format, layout) = Layout(text, font, Math.Max(1, w - pad * 2), h, flags, dpi);

				((delegate* unmanaged[Stdcall]<IntPtr, void>)Slot(target, 48))(target);   // BeginDraw
				var clear = new ColorF();
				((delegate* unmanaged[Stdcall]<IntPtr, ColorF*, void>)Slot(target, 47))(target, &clear);
				var origin = new PointF2 { x = pad, y = 0 };
				// DrawTextLayout(origin, layout, brush, ENABLE_COLOR_FONT | CLIP)
				((delegate* unmanaged[Stdcall]<IntPtr, PointF2, IntPtr, IntPtr, int, void>)Slot(target, 28))(target, origin, layout, brush, 4 | 2);
				ulong t1, t2;
				Check(((delegate* unmanaged[Stdcall]<IntPtr, ulong*, ulong*, int>)Slot(target, 49))(target, &t1, &t2));   // EndDraw

				// copy the pixels into a System.Drawing picture (same layout: premultiplied BGRA)
				var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
				var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
				try
				{
					int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, IntPtr, int>)Slot(wicBmp, 7))(wicBmp, IntPtr.Zero, (uint)data.Stride, (uint)(data.Stride * h), data.Scan0);
					if (hr < 0) { bmp.UnlockBits(data); data = null; bmp.Dispose(); Check(hr); }
				}
				finally { if (data != null) bmp.UnlockBits(data); }
				return bmp;
			}
			catch (Exception ex)
			{
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "colour emoji: " + ex.Message);
				return null;
			}
			finally { Release(layout); Release(format); Release(brush); Release(target); Release(wicBmp); }
		}

		/// <summary>GDI leaves a little space before and after the text; the same here so both line up.</summary>
		static int Pad(Font font, float dpi) => (int)Math.Round(font.SizeInPoints * dpi / 72f / 4f);

		/// <summary>A DirectWrite text layout with the font, size, wrapping and alignment of the GDI flags.</summary>
		static (IntPtr format, IntPtr layout) Layout(string text, Font font, float maxW, float maxH, TextFormatFlags flags, float dpi)
		{
			IntPtr format = IntPtr.Zero, layout = IntPtr.Zero;
			float px = font.SizeInPoints * dpi / 72f;   // pixels (the picture is 96 DPI: 1 DIP = 1 pixel)
			int weight = font.Bold ? 700 : 400;
			int style = font.Italic ? 2 : 0;
			fixed (char* family = font.FontFamily.Name)
			fixed (char* locale = "")
			{
				Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, int, int, int, float, char*, IntPtr*, int>)Slot(dwrite, 15))
					(dwrite, family, IntPtr.Zero, weight, style, 5 /* normal stretch */, px, locale, &format));
			}
			bool single = (flags & TextFormatFlags.SingleLine) != 0 || (flags & TextFormatFlags.WordBreak) == 0;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(format, 5))(format, single ? 1 : 0);   // word wrapping
			int align = (flags & TextFormatFlags.HorizontalCenter) != 0 ? 2 : (flags & TextFormatFlags.Right) != 0 ? 1 : 0;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(format, 3))(format, align);
			int para = (flags & TextFormatFlags.VerticalCenter) != 0 ? 2 : (flags & TextFormatFlags.Bottom) != 0 ? 1 : 0;
			((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(format, 4))(format, para);
			if ((flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis)) != 0)
			{
				var trim = new Trimming { granularity = 1 /* character */ };
				((delegate* unmanaged[Stdcall]<IntPtr, Trimming*, IntPtr, int>)Slot(format, 9))(format, &trim, IntPtr.Zero);
			}
			string s = text ?? "";
			fixed (char* p = s)
			{
				Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, uint, IntPtr, float, float, IntPtr*, int>)Slot(dwrite, 18))
					(dwrite, p, (uint)s.Length, format, maxW, maxH, &layout));
			}
			return (format, layout);
		}

		// ------------------------------------------------------------------ COM plumbing

		static IntPtr Slot(IntPtr obj, int index) => (*(IntPtr**)obj)[index];

		static void Release(IntPtr obj)
		{
			if (obj != IntPtr.Zero) ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(obj, 2))(obj);
		}

		static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

		[StructLayout(LayoutKind.Sequential)] struct ColorF { public float r, g, b, a; }
		[StructLayout(LayoutKind.Sequential)] struct PointF2 { public float x, y; }
		[StructLayout(LayoutKind.Sequential)] struct Trimming { public int granularity; public uint delimiter, delimiterCount; }
		[StructLayout(LayoutKind.Sequential)]
		struct RenderTargetProperties { public int type, format, alphaMode; public float dpiX, dpiY; public int usage, minLevel; }
		[StructLayout(LayoutKind.Sequential)]
		struct Metrics { public float left, top, width, widthIncludingTrailingWhitespace, height, layoutWidth, layoutHeight; public uint maxBidiReorderingDepth, lineCount; }

		[DllImport("d2d1.dll")] static extern int D2D1CreateFactory(int type, ref Guid riid, IntPtr options, out IntPtr factory);
		[DllImport("dwrite.dll")] static extern int DWriteCreateFactory(int type, ref Guid riid, out IntPtr factory);
		[DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int context, ref Guid riid, out IntPtr obj);
	}
}
