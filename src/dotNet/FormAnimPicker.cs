using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Easter egg window: every animation of the current pet as a tile; click a tile to play it.
    /// Opened by middle-clicking the pet. It stays open so several animations can be tried in a row;
    /// Esc, the close button or clicking elsewhere closes it.
    /// </summary>
    sealed class FormAnimPicker : Form
    {
        // ---------------------------------------------------------------- data
        readonly Action<int> play;
        readonly Image petIcon;
        readonly string petName;
        readonly List<Tile> allTiles = new List<Tile>();
        List<Tile> tiles = new List<Tile>();
        int playingId = -1;

        sealed class Tile
        {
            public int Id;
            public string Name;
            public string Emoji;
            public Rectangle Rect;      // in content coordinates (before scrolling)
        }

        // ---------------------------------------------------------------- layout (96 dpi units, scaled by S)
        const int WIDTH = 468, PAD = 16, HEADER_H = 68, SEARCH_H = 34, FOOTER_H = 34;
        const int COLS = 3, TILE_H = 44, GAP = 8, MAX_ROWS = 7;

        readonly float scale;
        int S(int v) => (int)Math.Round(v * scale);

        Rectangle closeRect, searchRect, listRect;
        int contentHeight, scrollY;
        int hoverTile = -1;
        bool hoverClose;

        readonly TextBox search;
        readonly Font fontTitle, fontSub, fontTile, fontSmall, fontEmoji, fontSearch;

        static readonly Dictionary<string, string> Emojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["walk"] = "🚶", ["run"] = "🏃", ["turn"] = "↩", ["jump"] = "⤴", ["leap"] = "🦘",
            ["fall"] = "⬇", ["fall soft"] = "🪶", ["fall hard"] = "💥", ["fall fast"] = "💨",
            ["climb"] = "🧗", ["drag"] = "✋", ["toss"] = "🤾", ["blink"] = "😑", ["sleep"] = "💤",
            ["wake up"] = "☀", ["sit"] = "🪑", ["wave"] = "👋", ["dance"] = "💃", ["spin"] = "🌀",
            ["eat"] = "🍪", ["flower"] = "🌸", ["think"] = "💭", ["surprise"] = "😲", ["scared"] = "😱",
            ["angry"] = "😡", ["sneak"] = "🤫", ["stretch"] = "🤸", ["point"] = "☝", ["look around"] = "👀",
            ["look down"] = "🔽", ["lean look"] = "🫣", ["sync"] = "🎉", ["kill"] = "💀", ["flag"] = "🚩",
            ["confetti"] = "🎊", ["gym"] = "🏋", ["laptop"] = "💻", ["top hat"] = "🎩", ["coffee"] = "☕",
            ["music"] = "🎵", ["chef"] = "🍳", ["skate"] = "🛹", ["builder"] = "🔨", ["detective"] = "🔍",
            ["idea"] = "💡", ["spell"] = "🪄", ["magic"] = "🪄", ["teleport"] = "✨", ["book"] = "📖",
            ["read"] = "📖", ["crystal ball"] = "🔮", ["type"] = "⌨", ["typing"] = "⌨", ["code"] = "⌨",
            ["loading"] = "⏳", ["error"] = "⚠", ["talk"] = "💬", ["rain"] = "🌧", ["nap"] = "💤",
        };

        // ---------------------------------------------------------------- construction
        public FormAnimPicker(Animations animations, string currentPetName, Image icon, Action<int> onPlay)
        {
            play = onPlay;
            petIcon = icon;
            petName = string.IsNullOrWhiteSpace(currentPetName) ? "Pet" : currentPetName;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.None;
            Text = Lang.T("picker.title", petName);

            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            fontTitle = Theme.CreateFont(16 * scale, FontStyle.Bold);
            fontSub = Theme.CreateFont(12 * scale);
            fontTile = Theme.CreateFont(13 * scale);
            fontSmall = Theme.CreateFont(11 * scale);
            fontSearch = Theme.CreateFont(13 * scale);
            fontEmoji = new Font("Segoe UI Emoji", 15 * scale, FontStyle.Regular, GraphicsUnit.Pixel);

            var childOnly = ChildOnlyAnimations(animations);
            if (animations?.SheepAnimations != null)
                foreach (var kv in animations.SheepAnimations.OrderBy(kv => kv.Key))
                {
                    if (childOnly.Contains(kv.Key)) continue;   // played by a child window, not by the pet itself
                    string name = string.IsNullOrWhiteSpace(kv.Value.Name) ? "#" + kv.Key : kv.Value.Name;
                    Emojis.TryGetValue(name, out string emoji);
                    allTiles.Add(new Tile { Id = kv.Key, Name = name, Emoji = emoji ?? "✨" });
                }

            search = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = fontSearch,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                PlaceholderText = Lang.T("picker.search"),
            };
            search.TextChanged += (s, e) => { scrollY = 0; Filter(); };
            search.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && tiles.Count > 0) { Play(tiles[0]); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { ScrollBy(S(TILE_H + GAP)); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { ScrollBy(-S(TILE_H + GAP)); e.SuppressKeyPress = true; }
            };
            search.GotFocus += (s, e) => Invalidate();
            search.LostFocus += (s, e) => Invalidate();
            Controls.Add(search);

            Theme.ApplyWindowChrome(this);
            Filter();

            MouseMove += OnMouseMove;
            MouseDown += OnMouseDown;
            MouseWheel += (s, e) => ScrollBy(-e.Delta / 120 * S(TILE_H + GAP));
            MouseLeave += (s, e) => { hoverTile = -1; hoverClose = false; Invalidate(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Deactivate += (s, e) => { if (!IsDisposed) BeginInvoke(new Action(Close)); };
            // The search box is not focused at first, so its placeholder stays visible;
            // typing anywhere in the window starts the search.
            Shown += (s, e) => ActiveControl = null;
            KeyPress += (s, e) =>
            {
                if (search.Focused || char.IsControl(e.KeyChar)) return;
                search.Focus();
                search.AppendText(e.KeyChar.ToString());
                e.Handled = true;
            };
        }

        /// <summary>
        /// Animations that only a child window plays (a released balloon, a thrown boomerang...):
        /// the "next" of each child and everything those animations chain to, unless a normal
        /// animation of the pet also leads there. Playing one of them on the pet itself would turn
        /// the pet into the object, so they are left out of the picker.
        /// </summary>
        static HashSet<int> ChildOnlyAnimations(Animations animations)
        {
            var result = new HashSet<int>();
            if (animations?.SheepChild == null || animations.SheepAnimations == null) return result;

            IEnumerable<int> NextIds(TAnimation a) =>
                (a.EndAnimation ?? new List<TNextAnimation>())
                .Concat(a.EndBorder ?? new List<TNextAnimation>())
                .Concat(a.EndGravity ?? new List<TNextAnimation>())
                .Select(n => n.ID);

            var stack = new Stack<int>(animations.SheepChild.Values.SelectMany(l => l).Select(c => c.Next));
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (!animations.SheepAnimations.TryGetValue(id, out var anim) || !result.Add(id)) continue;
                foreach (int next in NextIds(anim)) stack.Push(next);
            }
            // keep anything a normal (non child) animation can also reach
            foreach (var kv in animations.SheepAnimations)
                if (!result.Contains(kv.Key))
                    foreach (int next in NextIds(kv.Value)) result.Remove(next);
            return result;
        }

        // ---------------------------------------------------------------- layout
        void Filter()
        {
            string q = search.Text.Trim();
            tiles = q.Length == 0 ? allTiles.ToList()
                : allTiles.Where(t => t.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || t.Id.ToString() == q).ToList();
            Layout_();
            Invalidate();
        }

        void Layout_()
        {
            int w = S(WIDTH);
            int tileW = (w - S(PAD) * 2 - S(GAP) * (COLS - 1)) / COLS;
            for (int i = 0; i < tiles.Count; i++)
            {
                int col = i % COLS, row = i / COLS;
                tiles[i].Rect = new Rectangle(col * (tileW + S(GAP)), row * (S(TILE_H) + S(GAP)), tileW, S(TILE_H));
            }

            // the window keeps the height of the full list so it does not jump while searching
            int rowsAll = Math.Max(1, Math.Min(MAX_ROWS, (allTiles.Count + COLS - 1) / COLS));
            int rows = (tiles.Count + COLS - 1) / COLS;
            contentHeight = Math.Max(0, rows * (S(TILE_H) + S(GAP)) - S(GAP));
            int listH = rowsAll * (S(TILE_H) + S(GAP)) - S(GAP);

            closeRect = new Rectangle(w - S(PAD) - S(28), S(18), S(28), S(28));
            searchRect = new Rectangle(S(PAD), S(HEADER_H), w - S(PAD) * 2, S(SEARCH_H));
            listRect = new Rectangle(S(PAD), searchRect.Bottom + S(12), w - S(PAD) * 2, listH);
            ClientSize = new Size(w, listRect.Bottom + S(10) + S(FOOTER_H));

            int textH = search.PreferredHeight;
            search.SetBounds(searchRect.X + S(34), searchRect.Y + (searchRect.Height - textH) / 2, searchRect.Width - S(44), textH);
            scrollY = Math.Max(0, Math.Min(scrollY, MaxScroll));
        }

        int MaxScroll => Math.Max(0, contentHeight - listRect.Height);

        void ScrollBy(int delta)
        {
            int old = scrollY;
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY + delta));
            if (old != scrollY) { UpdateHover(PointToClient(Cursor.Position)); Invalidate(); }
        }

        Rectangle ScreenRect(Tile t) => new Rectangle(listRect.X + t.Rect.X, listRect.Y + t.Rect.Y - scrollY, t.Rect.Width, t.Rect.Height);

        // ---------------------------------------------------------------- paint
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(Theme.Background);

            // header: pet icon, title, counter, close button
            var iconBox = new Rectangle(S(PAD), S(14), S(40), S(40));
            Theme.FillRounded(g, Theme.AccentSoft, iconBox, S(10));
            if (petIcon != null)
            {
                var inner = Rectangle.Inflate(iconBox, -S(5), -S(5));
                Theme.DrawPixelImage(g, petIcon, inner);
            }
            else
            {
                Theme.DrawText(g, "🎬", fontEmoji, iconBox, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int tx = iconBox.Right + S(12);
            Theme.DrawText(g, Lang.T("picker.title", petName), fontTitle, new Rectangle(tx, S(13), closeRect.Left - tx - S(8), S(24)),
                Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            Theme.DrawText(g, Lang.T("picker.count", allTiles.Count), fontSub, new Rectangle(tx, S(37), closeRect.Left - tx - S(8), S(18)),
                Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

            if (hoverClose) Theme.FillRounded(g, Theme.Danger, closeRect, S(8));
            Theme.DrawText(g, "✕", fontTile, closeRect, hoverClose ? Color.White : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // search field
            Theme.FillRounded(g, Theme.Surface, searchRect, S(8));
            Theme.DrawRounded(g, search.Focused ? Theme.Accent : Theme.Border, searchRect, S(8));
            Theme.DrawText(g, "🔍", fontSmall, new Rectangle(searchRect.X + S(10), searchRect.Y, S(20), searchRect.Height), Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // tiles
            var oldClip = g.Clip;
            g.SetClip(Rectangle.Inflate(listRect, 2, 2));
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                var r = ScreenRect(t);
                if (r.Bottom < listRect.Top || r.Top > listRect.Bottom) continue;
                bool hover = i == hoverTile, active = t.Id == playingId;

                Theme.FillRounded(g, active ? Theme.AccentSoft : hover ? Theme.Hover : Theme.Surface, r, S(10));
                Theme.DrawRounded(g, active || hover ? Theme.Accent : Theme.Border, r, S(10), active ? 1.6f : 1f);

                var em = new Rectangle(r.X + S(6), r.Y + S(6), r.Height - S(12), r.Height - S(12));
                Theme.FillRounded(g, active ? Theme.Accent : Theme.SurfaceAlt, em, S(8));
                Theme.DrawText(g, t.Emoji, fontEmoji, em, active ? Theme.AccentText : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                var nameRect = new Rectangle(em.Right + S(8), r.Y, r.Right - em.Right - S(12), r.Height);
                Theme.DrawText(g, t.Name, fontTile, new Rectangle(nameRect.X, r.Y + S(5), nameRect.Width, S(20)),
                    Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
                Theme.DrawText(g, active ? "▶ " + Lang.T("picker.playing") : "#" + t.Id, fontSmall,
                    new Rectangle(nameRect.X, r.Y + S(23), nameRect.Width, S(16)),
                    active ? Theme.Accent : Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            }
            g.Clip = oldClip;

            if (tiles.Count == 0)
                Theme.DrawText(g, Lang.T("picker.empty"), fontSub, listRect, Theme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // thin scrollbar
            if (MaxScroll > 0)
            {
                int trackH = listRect.Height;
                int thumbH = Math.Max(S(24), trackH * listRect.Height / contentHeight);
                int thumbY = listRect.Y + (trackH - thumbH) * scrollY / MaxScroll;
                Theme.FillRounded(g, Theme.Border, new Rectangle(listRect.Right + S(5), thumbY, S(4), thumbH), S(2));
            }

            // fade at the bottom when more tiles are below
            if (scrollY < MaxScroll)
            {
                var fade = new Rectangle(listRect.X, listRect.Bottom - S(18), listRect.Width, S(18) + 1);
                using (var b = new LinearGradientBrush(fade, Color.FromArgb(0, Theme.Background), Theme.Background, LinearGradientMode.Vertical))
                    g.FillRectangle(b, fade);
            }

            // footer hint
            int fy = ClientSize.Height - S(FOOTER_H);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, fy, ClientSize.Width, fy);
            Theme.DrawText(g, Lang.T("picker.hint"), fontSmall, new Rectangle(S(PAD), fy, ClientSize.Width - S(PAD) * 2, S(FOOTER_H)),
                Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // ---------------------------------------------------------------- interaction
        void UpdateHover(Point p)
        {
            int prev = hoverTile; bool prevC = hoverClose;
            hoverClose = closeRect.Contains(p);
            hoverTile = -1;
            if (listRect.Contains(p))
                for (int i = 0; i < tiles.Count; i++)
                    if (ScreenRect(tiles[i]).Contains(p)) { hoverTile = i; break; }
            Cursor = hoverTile >= 0 || hoverClose ? Cursors.Hand : Cursors.Default;
            if (prev != hoverTile || prevC != hoverClose) Invalidate();
        }

        void OnMouseMove(object sender, MouseEventArgs e) => UpdateHover(e.Location);

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (closeRect.Contains(e.Location)) { Close(); return; }
            if (hoverTile >= 0 && hoverTile < tiles.Count) Play(tiles[hoverTile]);
        }

        void Play(Tile t)
        {
            playingId = t.Id;
            Invalidate();
            play(t.Id);
        }

        // drag the window by its header
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2;
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                var pt = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
                if (pt.Y < S(HEADER_H) - S(8) && !closeRect.Contains(pt)) m.Result = (IntPtr)HTCAPTION;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fontTitle?.Dispose(); fontSub?.Dispose(); fontTile?.Dispose();
                fontSmall?.Dispose(); fontEmoji?.Dispose(); fontSearch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
