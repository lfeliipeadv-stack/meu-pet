using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Easter egg dialog: shows all animations of the current pet as clickable buttons.
    /// Triggered by right-clicking the pet sprite.
    /// </summary>
    sealed class FormAnimPicker : Form
    {
        // ---------------------------------------------------------------- data
        readonly Animations anims;
        readonly Action<int> play;
        readonly Action<int> spawn;

        // ---------------------------------------------------------------- layout constants
        const int PAD   = 16;
        const int BTN_W = 148;
        const int BTN_H = 36;
        const int COLS  = 3;
        const int GAP   = 8;
        const int TITLE_H = 48;
        const int CLOSE_W = 32;
        const int FOOTER_H = 52;

        // ---------------------------------------------------------------- state
        int hoverBtn = -1;
        int hoverClose = 0; // 0=none 1=hover
        readonly List<(Rectangle rect, int id, string label)> buttons = new();
        Rectangle closeRect;
        string petName;

        // fonts
        Font fontTitle;
        Font fontBtn;
        Font fontSmall;

        // ---------------------------------------------------------------- construction
        public FormAnimPicker(Animations animations, string currentPetName, Action<int> onPlay, Action<int> onSpawn)
        {
            anims  = animations;
            play   = onPlay;
            spawn  = onSpawn;
            petName = currentPetName ?? "Pet";

            fontTitle = new Font(Theme.FontFamilyName, 13f, FontStyle.Bold,  GraphicsUnit.Pixel);
            fontBtn   = new Font(Theme.FontFamilyName, 11f, FontStyle.Regular, GraphicsUnit.Pixel);
            fontSmall = new Font(Theme.FontFamilyName,  9f, FontStyle.Regular, GraphicsUnit.Pixel);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            TopMost         = true;
            BackColor       = Theme.Background;

            Theme.ApplyWindowChrome(this);
            BuildLayout();

            MouseMove  += OnMouseMove;
            MouseDown  += OnMouseDown;
            MouseLeave += (s, e) => { hoverBtn = -1; hoverClose = 0; Invalidate(); };
            KeyDown    += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        // ---------------------------------------------------------------- layout
        void BuildLayout()
        {
            buttons.Clear();
            if (anims?.SheepAnimations == null) return;

            // Emoji quick-labels for well-known animation names
            var emojis = new Dictionary<string, string>
            {
                ["walk"]   = "🚶", ["run"]    = "🏃", ["turn"]   = "↩️",
                ["jump"]   = "⬆️", ["fall"]   = "⬇️", ["climb"]  = "🧗",
                ["drag"]   = "✋", ["toss"]   = "🤾", ["blink"]  = "😑",
                ["sleep"]  = "💤", ["wake up"]= "☀️", ["sit"]    = "🪑",
                ["wave"]   = "👋", ["dance"]  = "💃", ["spin"]   = "🌀",
                ["eat"]    = "🍪", ["flower"] = "🌸", ["think"]  = "💭",
                ["surprise"]="😲", ["scared"] = "😱", ["angry"]  = "😡",
                ["sneak"]  = "🤫", ["stretch"]= "🤸", ["point"]  = "☝️",
                ["look around"]="👀",["look down"]="🔽",
                ["fall soft"]="🫸",["fall hard"]="💥",["fall fast"]="💨",
                ["sync"]   = "🎉", ["kill"]   = "💀",
            };

            var ordered = anims.SheepAnimations.OrderBy(kv => kv.Key).ToList();

            int col = 0, row = 0;
            foreach (var kv in ordered)
            {
                int id = kv.Key;
                string name = kv.Value.Name ?? ("ID " + id);
                emojis.TryGetValue(name, out string em);
                string label = (em != null ? em + "  " : "") + name;

                int x = PAD + col * (BTN_W + GAP);
                int y = TITLE_H + PAD + row * (BTN_H + GAP);
                buttons.Add((new Rectangle(x, y, BTN_W, BTN_H), id, label));

                col++;
                if (col >= COLS) { col = 0; row++; }
            }

            int rows   = (buttons.Count + COLS - 1) / COLS;
            int clientW = PAD * 2 + COLS * BTN_W + (COLS - 1) * GAP;
            int clientH = TITLE_H + PAD + rows * (BTN_H + GAP) - GAP + PAD + FOOTER_H;

            ClientSize = new Size(clientW, clientH);
            closeRect  = new Rectangle(clientW - CLOSE_W - 8, (TITLE_H - 24) / 2, 24, 24);
        }

        // ---------------------------------------------------------------- paint
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background
            g.Clear(Theme.Background);

            // Title bar area
            Theme.FillRounded(g, Theme.Surface, new RectangleF(0, 0, Width, TITLE_H), 10);
            g.FillRectangle(new SolidBrush(Theme.Surface), 0, TITLE_H / 2, Width, TITLE_H / 2);
            Theme.DrawText(g, "🎬  " + petName + " — Animations", fontTitle,
                new Rectangle(PAD, 0, Width - CLOSE_W - PAD * 2, TITLE_H),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Close button
            var closeBack = hoverClose == 1 ? Theme.Danger : Theme.SurfaceAlt;
            Theme.FillRounded(g, closeBack, closeRect, 6);
            Theme.DrawText(g, "✕", fontBtn, closeRect, hoverClose == 1 ? Color.White : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Thin separator below title
            g.DrawLine(new Pen(Theme.Border), 0, TITLE_H, Width, TITLE_H);

            // Animation buttons
            for (int i = 0; i < buttons.Count; i++)
            {
                var (rect, id, label) = buttons[i];
                bool hover = hoverBtn == i;
                Theme.DrawButton(g, rect, label, fontBtn, false, hover);
                // ID badge, small muted
                var badge = new Rectangle(rect.Right - 28, rect.Y + 2, 26, 14);
                Theme.DrawText(g, id.ToString(), fontSmall, badge, Theme.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // Footer hint
            var footerY = Height - FOOTER_H;
            g.DrawLine(new Pen(Theme.Border), 0, footerY, Width, footerY);
            Theme.DrawText(g, "Right-click the pet to open this window  ·  Esc to close",
                fontSmall, new Rectangle(PAD, footerY, Width - PAD * 2, FOOTER_H),
                Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }

        // ---------------------------------------------------------------- interaction
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            int prev = hoverBtn, prevC = hoverClose;
            hoverBtn = -1;
            hoverClose = closeRect.Contains(e.Location) ? 1 : 0;
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i].rect.Contains(e.Location)) { hoverBtn = i; break; }
            if (hoverBtn != prev || hoverClose != prevC) Invalidate();
        }

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (closeRect.Contains(e.Location)) { Close(); return; }
            for (int i = 0; i < buttons.Count; i++)
            {
                if (!buttons[i].rect.Contains(e.Location)) continue;
                play(buttons[i].id);
                // Flash: brief visual feedback then close
                hoverBtn = i; Invalidate(); Refresh();
                System.Threading.Thread.Sleep(80);
                Close();
                return;
            }
        }

        // Allow dragging the dialog by its title bar
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTCAPTION = 2;
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == 1)
            {
                var pt = PointToClient(new Point(m.LParam.ToInt32()));
                if (pt.Y < TITLE_H && !closeRect.Contains(pt)) m.Result = (IntPtr)HTCAPTION;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { fontTitle?.Dispose(); fontBtn?.Dispose(); fontSmall?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
