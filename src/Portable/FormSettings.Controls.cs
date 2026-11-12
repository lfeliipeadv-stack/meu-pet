using DesktopPet.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static DesktopPet.LocalData;

namespace DesktopPet
{
	/// <summary>Shared painted controls: the search field and the scrolling surface.</summary>
	public sealed partial class FormSettings
	{
		// ================================================================== search field

		/// <summary>Rounded search field: magnifier, borderless text box and a clear button.</summary>
		private sealed class SearchField : Control
		{
			public readonly TextBox Box;
			private readonly Font glyphFont;
			private Rectangle clearRect;
			private bool hoverClear;

			public SearchField(Font font)
			{
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
					ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
				string iconFont = new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(n =>
				{
					using (var f = new Font(n, 10)) return string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase);
				}) ?? Theme.FontFamilyName;
				glyphFont = new Font(iconFont, 13, GraphicsUnit.Pixel);
				Box = new TextBox { BorderStyle = BorderStyle.None, Font = font };
				Box.TextChanged += (s, e) => Invalidate();
				Box.GotFocus += (s, e) => Invalidate();
				Box.LostFocus += (s, e) => Invalidate();
				Controls.Add(Box);
				Cursor = Cursors.IBeam;
				Recolor();
			}

			public void Recolor()
			{
				BackColor = Theme.Background;
				Box.BackColor = Theme.Surface;
				Box.ForeColor = Theme.Text;
				Invalidate();
			}

			protected override void OnLayout(LayoutEventArgs e)
			{
				base.OnLayout(e);
				int h = Box.PreferredHeight;
				Box.SetBounds(34, (Height - h) / 2, Width - 34 - 32, h);
				clearRect = new Rectangle(Width - 30, (Height - 22) / 2, 22, 22);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				var r = new Rectangle(0, 0, Width - 1, Height - 1);
				Theme.FillRounded(g, Theme.Surface, r, 9);
				Theme.DrawRounded(g, Box.Focused ? Theme.Accent : Theme.Border, r, 9, Box.Focused ? 1.5f : 1f);
				TextRenderer.DrawText(g, "", glyphFont, new Rectangle(10, 0, 20, Height), Theme.TextMuted,
					TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
				if (Box.TextLength > 0)
				{
					if (hoverClear) Theme.FillRounded(g, Theme.Hover, clearRect, 6);
					TextRenderer.DrawText(g, "", glyphFont, clearRect, Theme.TextMuted,
						TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				bool h = Box.TextLength > 0 && clearRect.Contains(e.Location);
				Cursor = h ? Cursors.Hand : Cursors.IBeam;
				if (h != hoverClear) { hoverClear = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverClear = false; Invalidate(); }

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (Box.TextLength > 0 && clearRect.Contains(e.Location)) Box.Clear();
				Box.Focus();
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing) glyphFont.Dispose();
				base.Dispose(disposing);
			}
		}

		// ================================================================== scrolling surface

		/// <summary>
		/// Double buffered, custom painted surface with a slim overlay scrollbar and smooth wheel scrolling.
		/// </summary>
		private abstract class ScrollSurface : Control
		{
			protected const int ScrollBarWidth = 10;
			private int contentHeight;
			private int scrollY;
			private bool draggingThumb;
			private int dragOffset;

			protected ScrollSurface()
			{
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
				BackColor = Theme.Background;
			}

			protected int ScrollY
			{
				get => scrollY;
				set
				{
					int max = Math.Max(0, contentHeight - Height);
					int v = Math.Max(0, Math.Min(max, value));
					if (v != scrollY) { scrollY = v; Invalidate(); }
				}
			}

			protected abstract int PaintContent(Graphics g, int scrollY);

			/// <summary>Left edge of the scroll bar lane (a view with a side panel moves it next to its content).</summary>
			protected virtual int ScrollBarX => Width - ScrollBarWidth;

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.Background);
				int h = PaintContent(g, scrollY);
				if (h != contentHeight)
				{
					contentHeight = h;
					int max = Math.Max(0, contentHeight - Height);
					if (scrollY > max) { scrollY = max; Invalidate(); }
				}
				var thumb = Thumb();
				if (!thumb.IsEmpty)
					Theme.FillRounded(g, draggingThumb ? Theme.TextMuted : Theme.Border, thumb, 3);
			}

			private Rectangle Thumb()
			{
				if (contentHeight <= Height || Height <= 0) return Rectangle.Empty;
				int th = Math.Max(40, Height * Height / contentHeight);
				int max = contentHeight - Height;
				int ty = (int)((Height - th) * (scrollY / (double)max));
				return new Rectangle(ScrollBarX + 2, ty, 6, th);
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				ScrollY -= e.Delta / 120 * 60;
			}

			protected override void OnMouseDown(MouseEventArgs e)
			{
				Focus();
				var t = Thumb();
				if (!t.IsEmpty && e.X >= ScrollBarX - 4 && e.X <= ScrollBarX + ScrollBarWidth)
				{
					draggingThumb = true;
					dragOffset = t.Contains(e.Location) ? e.Y - t.Y : t.Height / 2;
					Capture = true;
					DragThumb(e.Y);
					return;
				}
				base.OnMouseDown(e);
			}

			private void DragThumb(int y)
			{
				var t = Thumb();
				int max = contentHeight - Height;
				double pct = (y - dragOffset) / (double)Math.Max(1, Height - t.Height);
				ScrollY = (int)(pct * max);
				Invalidate();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				if (draggingThumb) { DragThumb(e.Y); return; }
				base.OnMouseMove(e);
			}

			protected override void OnMouseUp(MouseEventArgs e)
			{
				if (draggingThumb) { draggingThumb = false; Capture = false; Invalidate(); return; }
				base.OnMouseUp(e);
			}

			protected override bool IsInputKey(Keys keyData) =>
				keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.PageUp || keyData == Keys.PageDown || base.IsInputKey(keyData);

			protected override void OnKeyDown(KeyEventArgs e)
			{
				base.OnKeyDown(e);
				switch (e.KeyCode)
				{
					case Keys.Down: ScrollY += 40; break;
					case Keys.Up: ScrollY -= 40; break;
					case Keys.PageDown: ScrollY += Height - 40; break;
					case Keys.PageUp: ScrollY -= Height - 40; break;
					case Keys.Home: ScrollY = 0; break;
					case Keys.End: ScrollY = int.MaxValue; break;
				}
			}
		}
	}
}
