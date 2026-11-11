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
	/// <summary>
	/// Modern settings window: pets gallery (download from GitHub), behaviour options, application options and about.
	/// Replaces the Windows95-style <see cref="FormOptions2"/> (which is kept for reference / legacy build).
	/// All painting is custom (double buffered), colors come from <see cref="Theme"/> and follow the Windows light/dark mode.
	/// </summary>
	public sealed class FormSettings : Form
	{
		/// <summary>Pages of the window (sidebar entries).</summary>
		public enum Page { Pets, Behaviour, Application, About }

		private static FormSettings openInstance;

		/// <summary>Open the window (or bring the existing one to front).</summary>
		public static void ShowPage(Page page)
		{
			if (openInstance != null && !openInstance.IsDisposed)
			{
				openInstance.SelectPage(page);
				if (openInstance.WindowState == FormWindowState.Minimized) openInstance.WindowState = FormWindowState.Normal;
				openInstance.Activate();
				return;
			}
			openInstance = new FormSettings(page);
			openInstance.Show();
		}

		/// <summary>
		/// Developer helper: render every page to PNG files (offscreen, nothing is shown on the desktop).
		/// Usage: DesktopPet.exe --snapshot=C:\folder
		/// </summary>
		public static async void SaveSnapshots(string folder)
		{
			var f = new FormSettings(Page.Pets) { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false };
			f.Show();
			for (int i = 0; i < 40 && f.WebPets.pets.Count < 12; i++) await Task.Delay(250);
			await Task.Delay(500);
			void save(string name)
			{
				f.Refresh();
				using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
				{
					f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.ClientSize));
					bmp.Save(System.IO.Path.Combine(folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
				}
			}
			save("pets");
			var first = f.WebPets.pets.FirstOrDefault(p => p.folder == "esheep64") ?? f.WebPets.pets.FirstOrDefault();
			if (first != null) { f.petsView.SelectForSnapshot(first); await f.LoadDetailsAsync(first); await Task.Delay(300); save("pets-detail"); }
			foreach (Page p in new[] { Page.Behaviour, Page.Application, Page.About }) { f.SelectPage(p); await Task.Delay(200); save(p.ToString().ToLowerInvariant()); }
			// narrow window: the page subtitle must wrap instead of being cut
			f.SelectPage(Page.Pets); f.Width = 760; await Task.Delay(300); save("pets-narrow");
			f.Close();

			// animation picker (middle-click on the pet), built from the current pet
			try
			{
				var xml = new Xml(1);
				if (xml.ReadXML())
				{
					var anims = new Animations(xml);
					xml.LoadAnimations(anims);
					var picker = new FormAnimPicker(anims, xml.AnimationXML.Header.Petname,
						xml.sprites != null && xml.sprites.Count > 0 ? xml.sprites[0] : null, id => { })
					{ Location = new Point(-5000, -5000) };
					picker.Show();
					await Task.Delay(300);
					picker.Refresh();
					using (var bmp = new Bitmap(picker.ClientSize.Width, picker.ClientSize.Height))
					{
						picker.DrawToBitmap(bmp, new Rectangle(Point.Empty, picker.ClientSize));
						bmp.Save(System.IO.Path.Combine(folder, "anim-picker.png"), System.Drawing.Imaging.ImageFormat.Png);
					}
					picker.Close();
				}
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "picker snapshot: " + ex.Message); }
		}

		// ------------------------------------------------------------------ state

		private Page page;
		private readonly Sidebar sidebar;
		private readonly Panel header;
		private readonly Label headerTitle;
		private readonly Label headerSubtitle;
		private readonly TextBox searchBox;
		private readonly Panel content;
		private readonly Label status;

		private readonly PetsView petsView;
		private readonly OptionsView behaviourView;
		private readonly OptionsView applicationView;
		private readonly AboutView aboutView;

		private Font fontTitle, fontSubtitle, fontText, fontSmall, fontBold, fontNav, fontH2;

		/// <summary>Pets available online (filled progressively).</summary>
		public OptionsPets WebPets { get; } = new OptionsPets { pets = new List<OptionsPet>() };

		private string statusText = "";
		/// <summary>Status text on the bottom of the window.</summary>
		public string StatusText
		{
			get => statusText;
			set
			{
				statusText = value ?? "";
				if (status.InvokeRequired) status.BeginInvoke(new Action(() => status.Text = statusText));
				else status.Text = statusText;
			}
		}

		private FormSettings(Page startPage)
		{
			Text = Lang.T("win.title");
			StartPosition = FormStartPosition.CenterScreen;
			AutoScaleMode = AutoScaleMode.Dpi;
			AutoScaleDimensions = new SizeF(96F, 96F);
			MinimumSize = new Size(760, 520);
			Size = new Size(980, 660);
			ShowIcon = true;
			try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath); } catch (Exception) { }
			DoubleBuffered = true;
			KeyPreview = true;

			CreateFonts();
			Theme.ApplyWindowChrome(this);
			BackColor = Theme.Background;
			ForeColor = Theme.Text;

			// --- layout: sidebar | (header / content / status)
			sidebar = new Sidebar(this) { Dock = DockStyle.Left, Width = 220 };

			status = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 30,
				Padding = new Padding(16, 0, 16, 0),
				TextAlign = ContentAlignment.MiddleLeft,
				Font = fontSmall,
				ForeColor = Theme.TextMuted,
				BackColor = Theme.Background,
				AutoEllipsis = true
			};

			header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Background, Padding = new Padding(28, 18, 24, 6) };
			headerTitle = new Label { AutoSize = true, Font = fontTitle, ForeColor = Theme.Text, Location = new Point(26, 16) };
			// not AutoSize: the subtitle wraps to the window width (see LayoutHeader)
			headerSubtitle = new Label { AutoSize = false, Font = fontSubtitle, ForeColor = Theme.TextMuted, Location = new Point(28, 52) };
			headerSubtitle.TextChanged += (s, e) => LayoutHeader();
			searchBox = new TextBox
			{
				Font = fontText,
				BorderStyle = BorderStyle.FixedSingle,
				BackColor = Theme.Surface,
				ForeColor = Theme.Text,
				Width = 240,
				PlaceholderText = Lang.T("pets.search"),
				Anchor = AnchorStyles.Top | AnchorStyles.Right
			};
			header.Controls.Add(headerTitle);
			header.Controls.Add(headerSubtitle);
			header.Controls.Add(searchBox);
			header.Resize += (s, e) =>
			{
				searchBox.Location = new Point(header.ClientSize.Width - searchBox.Width - 26, 26);
				LayoutHeader();
			};

			content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(20, 4, 20, 8) };

			Controls.Add(content);
			Controls.Add(header);
			Controls.Add(status);
			Controls.Add(sidebar);

			petsView = new PetsView(this) { Dock = DockStyle.Fill };
			// Restore the currently adopted pet from the saved setting so the gallery
			// shows the "in use" badge on the right card even on a fresh Settings open.
			petsView.CurrentPet = Program.MyData.GetCurrentPetFolder();
			behaviourView = new OptionsView(this) { Dock = DockStyle.Fill };
			applicationView = new OptionsView(this) { Dock = DockStyle.Fill };
			aboutView = new AboutView(this) { Dock = DockStyle.Fill };
			BuildBehaviourOptions();
			BuildApplicationOptions();

			searchBox.TextChanged += (s, e) => petsView.Filter = searchBox.Text;

			Lang.Changed += OnLanguageChanged;
			SelectPage(startPage);
			_ = LoadPetsAsync();
		}

		/// <summary>
		/// Wrap the page subtitle to the header width and grow the header to fit it,
		/// so long texts (or translations) are never cut when the window is narrow.
		/// </summary>
		private bool layingOutHeader;
		private void LayoutHeader()
		{
			if (layingOutHeader || header == null || headerSubtitle == null) return;
			layingOutHeader = true;
			try
			{
				int width = Math.Max(120, header.ClientSize.Width - headerSubtitle.Left - 24);
				var size = TextRenderer.MeasureText(headerSubtitle.Text ?? "", headerSubtitle.Font, new Size(width, int.MaxValue),
					TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
				headerSubtitle.Size = new Size(width, size.Height + 4);
				int height = Math.Max(84, headerSubtitle.Bottom + 10);
				if (header.Height != height) header.Height = height;
			}
			finally { layingOutHeader = false; }
		}

		/// <summary>The language was switched: rewrite fixed texts and repaint everything.</summary>
		private void OnLanguageChanged()
		{
			if (IsDisposed) return;
			if (InvokeRequired) { BeginInvoke(new Action(OnLanguageChanged)); return; }
			Text = Lang.T("win.title");
			searchBox.PlaceholderText = Lang.T("pets.search");
			StatusText = Lang.T("status.available", WebPets.pets.Count);
			SelectPage(page);
			foreach (Control c in new Control[] { sidebar, petsView, behaviourView, applicationView, aboutView }) c.Invalidate();
		}

		private void CreateFonts()
		{
			fontTitle = Theme.CreateFont(28, FontStyle.Bold);
			fontSubtitle = Theme.CreateFont(14);
			fontText = Theme.CreateFont(14);
			fontSmall = Theme.CreateFont(12);
			fontBold = Theme.CreateFont(14, FontStyle.Bold);
			fontNav = Theme.CreateFont(14);
			fontH2 = Theme.CreateFont(18, FontStyle.Bold);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			base.OnFormClosed(e);
			Lang.Changed -= OnLanguageChanged;
			if (openInstance == this) openInstance = null;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape)
			{
				if (page == Page.Pets && petsView.DetailOpen) petsView.CloseDetail();
				else Close();
				e.Handled = true;
			}
			else if (e.Control && e.KeyCode == Keys.F && page == Page.Pets)
			{
				searchBox.Focus();
				e.Handled = true;
			}
		}

		/// <summary>Switch the visible page.</summary>
		public void SelectPage(Page p)
		{
			page = p;
			content.SuspendLayout();
			content.Controls.Clear();
			switch (p)
			{
				case Page.Pets:
					headerTitle.Text = Lang.T("page.pets.title");
					headerSubtitle.Text = Lang.T("page.pets.subtitle");
					content.Controls.Add(petsView);
					break;
				case Page.Behaviour:
					headerTitle.Text = Lang.T("page.behaviour.title");
					headerSubtitle.Text = Lang.T("page.behaviour.subtitle");
					content.Controls.Add(behaviourView);
					break;
				case Page.Application:
					headerTitle.Text = Lang.T("page.application.title");
					headerSubtitle.Text = Lang.T("page.application.subtitle");
					content.Controls.Add(applicationView);
					break;
				case Page.About:
					headerTitle.Text = Lang.T("page.about.title");
					headerSubtitle.Text = Lang.T("page.about.subtitle", Application.ProductVersion.Split('+')[0]);
					content.Controls.Add(aboutView);
					break;
			}
			searchBox.Visible = p == Page.Pets;
			content.ResumeLayout();
			sidebar.Invalidate();
		}

		// ------------------------------------------------------------------ options

		private void BuildBehaviourOptions()
		{
			// Titles and descriptions are translation keys, resolved at paint time (instant language switch).
			behaviourView.AddToggle(Resources.ico_multi, "opt.multiscreen", SettingName.Multiscreen);
			behaviourView.AddToggle(Resources.ico_smooth, "opt.smooth", SettingName.SmoothMovements);
			behaviourView.AddToggle(Resources.ico_focus, "opt.taskbar", SettingName.StealTaskbarFocus);
			behaviourView.AddToggle(Resources.ico_foreground, "opt.foreground", SettingName.WinForeground);
			behaviourView.AddSlider(Resources.ico_sound, "opt.volume", SettingName.Volume, 0, 100, v => Lang.T("opt.volume.value", v));
		}

		private void BuildApplicationOptions()
		{
			// Language: Auto + every language file found in the lang folder.
			var languages = Lang.Available();
			var codes = new List<string> { "auto" };
			codes.AddRange(languages.Select(l => l.Code));
			applicationView.AddDropDown(Resources.option, "opt.language",
				() => codes.Select(c => c == "auto" ? Lang.T("opt.language.auto") : languages.First(l => l.Code == c).Name).ToArray(),
				() => Math.Max(0, codes.FindIndex(c => string.Equals(c, Program.MyData.GetLanguage(), StringComparison.OrdinalIgnoreCase))),
				i =>
				{
					Program.MyData.SetLanguage(codes[i]);
					Lang.Use(codes[i]);
				});

			applicationView.AddSlider(Resources.ico_quantity, "opt.quantity", SettingName.AutostartPets, 0, StartUp.MAX_SHEEPS,
				v => v == 1 ? Lang.T("opt.quantity.one") : Lang.T("opt.quantity.many", v));
			applicationView.AddChoice(Resources.ico_resize, "opt.scale",
				() => Program.MyData.GetScale(),
				v => { Program.MyData.SetScale(v); Program.RestartApp(); },
				new[] { 1, 2, 4 }, new[] { "1×", "2×", "4×" });
			applicationView.AddAction(Resources.ico_restore, "opt.restore", "opt.restore.button", () =>
			{
				if (MessageBox.Show(this, Lang.T("dlg.restore.text"), Lang.T("dlg.restore.title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "restoring default XML");
				Program.MyData.SetIcon("");
				Program.MyData.SetImages("");
				Program.MyData.SetXml(Resources.animations, "esheep64");
				Program.Mainthread.LoadNewXMLFromString(Resources.animations);
				StatusText = Lang.T("status.restored");
				// Clear saved pet folder so gallery badge resets.
				Program.MyData.SetCurrentPetFolder("");
				petsView.CurrentPet = "";
				petsView.Invalidate();
			});
#if PORTABLE
			applicationView.AddAction(Resources.install, Program.IsApplicationInstalled() ? "opt.install.titleInstalled" : "opt.install", "opt.install.button",
				() => ContextMenus.OpenInstaller(), "opt.install.desc");
#endif
		}

		// ------------------------------------------------------------------ pets (download)

		private async Task LoadPetsAsync()
		{
			// --- built-in pets (embedded, always available offline) ----------------
			foreach (var folder in BuiltInPets.Folders)
			{
				var builtIn = new OptionsPet
				{
					folder = folder,
					author = "Luan Felipe",
					lastupdate = "2026-11-10",
					Image = BuiltInPets.GetIcon(folder),
					Readme = BuiltInPets.GetReadMe(folder),
					Detail = BuiltInPets.GetDetail(folder),
				};
				WebPets.pets.Add(builtIn);
			}
			petsView.PetsChanged();

			// --- online gallery (GitHub) -------------------------------------------
			StatusText = Lang.T("status.loadingList");
			try
			{
				var progress = new Progress<OptionsPet>(pet =>
				{
					// skip if the folder was already added as a built-in
					if (WebPets.pets.Any(p => p.folder == pet.folder)) return;
					WebPets.pets.Add(pet);
					petsView.PetsChanged();
					StatusText = Lang.T("status.loadingListCount", WebPets.pets.Count);
				});
				await Task.Run(() => OptionsGit.GetPetList(progress));
				StatusText = Lang.T("status.available", WebPets.pets.Count);
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.listError", ex.Message);
			}
			petsView.Loading = false;
			petsView.PetsChanged();
		}

		/// <summary>Download a pet and set it as the current one.</summary>
		public async void AdoptPet(OptionsPet pet)
		{
			if (pet == null) return;
			StatusText = Lang.T("status.downloading", pet.DisplayName);
			try
			{
				string content;
				if (BuiltInPets.IsBuiltIn(pet.folder))
				{
					content = BuiltInPets.GetXml(pet.folder);
				}
				else
				{
					content = await Task.Run(() => OptionsGit.GetPetXml(pet.folder));
				}
				var xml = new XmlDocument();
				xml.LoadXml(content);
				// Clear cached images/icon so ReadXML re-reads them from the new XML,
				// not from the stale data left by the previous pet.
				Program.MyData.SetImages("");
				Program.MyData.SetIcon("");
				Program.MyData.SetXml(xml.OuterXml, "");
				Program.Mainthread.LoadNewXMLFromString(xml.OuterXml);
				StatusText = Lang.T("status.enjoy", pet.DisplayName);
				petsView.CurrentPet = pet.folder;
				// Persist the adopted pet folder so the gallery badge survives restart.
				Program.MyData.SetCurrentPetFolder(pet.folder);
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.downloadError", pet.DisplayName, ex.Message);
			}
		}

		/// <summary>Load README and XML details for the detail panel.</summary>
		public async Task LoadDetailsAsync(OptionsPet pet)
		{
			try
			{
				if (pet.Readme.Count == 0)
				{
					if (BuiltInPets.IsBuiltIn(pet.folder))
						pet.Readme = BuiltInPets.GetReadMe(pet.folder);
					else
						pet.Readme = await Task.Run(() => OptionsGit.GetReadMe(pet.folder));
				}
			}
			catch (Exception ex) { StatusText = "README: " + ex.Message; }
			try
			{
				if (pet.Detail == null)
				{
					if (BuiltInPets.IsBuiltIn(pet.folder))
					{
						pet.Detail = BuiltInPets.GetDetail(pet.folder);
					}
					else
					{
						var content = await Task.Run(() => OptionsGit.GetPetXml(pet.folder));
						var doc = new XmlDocument();
						doc.LoadXml(content);
						pet.Detail = doc;
					}
				}
			}
			catch (Exception ex) { StatusText = "Details: " + ex.Message; }
		}

		// ================================================================== Sidebar

		private sealed class Sidebar : Control
		{
			private readonly FormSettings form;
			private readonly (Page page, string text, string glyph)[] items =
			{
				(Page.Pets, "nav.pets", ""),          // home-ish
				(Page.Behaviour, "nav.behaviour", ""), // touch/pointer
				(Page.Application, "nav.application", ""), // settings
				(Page.About, "nav.about", ""),         // info
			};
			private int hover = -1;
			private readonly Font glyphFont;

			public Sidebar(FormSettings f)
			{
				form = f;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
				glyphFont = new Font(FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 16, GraphicsUnit.Pixel);
				Cursor = Cursors.Hand;
			}

			private static bool FontExists(string name)
			{
				using (var f = new Font(name, 10)) return string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase);
			}

			private Rectangle ItemRect(int i) => new Rectangle(10, 96 + i * 44, Width - 20, 38);

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				Theme.Prepare(g);
				g.Clear(Theme.SurfaceAlt);
				using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);

				// app logo + name
				Theme.DrawPixelImage(g, Resources.esheep, new Rectangle(20, 22, 40, 40));
				Theme.DrawText(g, "eSheep", form.fontBold, new Rectangle(68, 24, Width - 70, 20), Theme.Text, TextFormatFlags.Left | TextFormatFlags.SingleLine);
				Theme.DrawText(g, Lang.T("nav.subtitle"), form.fontSmall, new Rectangle(68, 44, Width - 70, 18), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.SingleLine);

				for (int i = 0; i < items.Length; i++)
				{
					var r = ItemRect(i);
					bool selected = form.page == items[i].page;
					if (selected) Theme.FillRounded(g, Theme.Hover, r, 6);
					else if (hover == i) Theme.FillRounded(g, Color.FromArgb(120, Theme.Hover), r, 6);
					if (selected)
						Theme.FillRounded(g, Theme.Accent, new Rectangle(r.X, r.Y + 10, 3, r.Height - 20), 1.5f);
					TextRenderer.DrawText(g, items[i].glyph, glyphFont, new Rectangle(r.X + 14, r.Y, 24, r.Height), selected ? Theme.Accent : Theme.TextMuted,
						TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
					Theme.DrawText(g, Lang.T(items[i].text), form.fontNav, new Rectangle(r.X + 48, r.Y, r.Width - 50, r.Height), Theme.Text,
						TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
				}

				// bottom: quick actions
				var add = AddRect;
				Theme.DrawButton(g, add, Lang.T("nav.addPet"), form.fontText, true, hover == 100);
			}

			private Rectangle AddRect => new Rectangle(16, Height - 60, Width - 32, 38);

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				int h = -1;
				for (int i = 0; i < items.Length; i++) if (ItemRect(i).Contains(e.Location)) h = i;
				if (AddRect.Contains(e.Location)) h = 100;
				Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
				if (h != hover) { hover = h; Invalidate(); }
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				if (hover != -1) { hover = -1; Invalidate(); }
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				for (int i = 0; i < items.Length; i++)
					if (ItemRect(i).Contains(e.Location)) form.SelectPage(items[i].page);
				if (AddRect.Contains(e.Location)) Program.Mainthread.AddSheep();
			}
		}

		// ================================================================== Pets gallery

		private sealed class PetsView : ScrollSurface
		{
			private readonly FormSettings form;
			private OptionsPet selected;
			private OptionsPet hoverPet;
			private bool hoverAdopt, hoverClose;
			private string filter = "";
			private readonly List<(Rectangle rect, OptionsPet pet)> cards = new List<(Rectangle, OptionsPet)>();
			private readonly List<(Rectangle rect, string url)> links = new List<(Rectangle, string)>();
			private Rectangle adoptRect, closeRect;
			public bool Loading = true;
			public string CurrentPet;
			private const int CardW = 150, CardH = 150, Gap = 14;
			private const int DetailW = 340;

			public PetsView(FormSettings f) { form = f; }

			public bool DetailOpen => selected != null;

			public string Filter
			{
				get => filter;
				set { filter = (value ?? "").Trim(); ScrollY = 0; Invalidate(); }
			}

			public void PetsChanged() => Invalidate();

			public void CloseDetail() { selected = null; Invalidate(); }

			public void SelectForSnapshot(OptionsPet pet) { selected = pet; Invalidate(); }

			private IEnumerable<OptionsPet> FilteredPets()
			{
				var pets = form.WebPets.pets;
				if (filter.Length == 0) return pets;
				return pets.Where(p =>
					p.folder.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
					(p.author ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
			}

			private Rectangle GridArea => selected == null
				? new Rectangle(0, 0, Width - ScrollBarWidth, Height)
				: new Rectangle(0, 0, Width - DetailW - 16 - ScrollBarWidth, Height);

			protected override int PaintContent(Graphics g, int scrollY)
			{
				cards.Clear();
				var area = GridArea;
				int cols = Math.Max(1, (area.Width + Gap) / (CardW + Gap));
				int x0 = 8, y = 8 - scrollY, col = 0;

				var list = FilteredPets().ToList();
				if (list.Count == 0)
				{
					string msg = Loading ? Lang.T("pets.loading") : (filter.Length > 0 ? Lang.T("pets.noMatch", filter) : Lang.T("pets.empty"));
					Theme.DrawText(g, msg, form.fontText, new Rectangle(8, 40, area.Width - 16, 40), Theme.TextMuted, TextFormatFlags.HorizontalCenter);
				}

				foreach (var pet in list)
				{
					var r = new Rectangle(x0 + col * (CardW + Gap), y, CardW, CardH);
					cards.Add((r, pet));
					if (r.Bottom >= 0 && r.Top <= Height) DrawCard(g, r, pet);
					col++;
					if (col >= cols) { col = 0; y += CardH + Gap; }
				}
				if (col != 0) y += CardH + Gap;
				int contentHeight = y + scrollY + 8;

				if (selected != null) DrawDetail(g);
				return contentHeight;
			}

			private void DrawCard(Graphics g, Rectangle r, OptionsPet pet)
			{
				bool isSel = pet == selected, isHover = pet == hoverPet;
				Theme.FillRounded(g, isHover ? Theme.SurfaceAlt : Theme.Surface, r, 10);
				Theme.DrawRounded(g, isSel ? Theme.Accent : Theme.Border, r, 10, isSel ? 2f : 1f);

				var iconRect = new Rectangle(r.X + (r.Width - 64) / 2, r.Y + 20, 64, 64);
				if (pet.Image != null) Theme.DrawPixelImage(g, pet.Image, iconRect);
				else Theme.FillRounded(g, Theme.Hover, iconRect, 8);

				Theme.DrawText(g, pet.DisplayName, form.fontBold, new Rectangle(r.X + 8, r.Y + 94, r.Width - 16, 20), Theme.Text,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, pet.author, form.fontSmall, new Rectangle(r.X + 8, r.Y + 116, r.Width - 16, 18), Theme.TextMuted,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

				if (pet.folder == CurrentPet)
				{
					var badge = new Rectangle(r.Right - 58, r.Y + 8, 50, 20);
					Theme.FillRounded(g, Theme.AccentSoft, badge, 10);
					Theme.DrawText(g, Lang.T("pets.badgeInUse"), form.fontSmall, badge, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
				else if (IsNew(pet))
				{
					var badge = new Rectangle(r.Right - 48, r.Y + 8, 40, 20);
					Theme.FillRounded(g, Theme.AccentSoft, badge, 10);
					Theme.DrawText(g, Lang.T("pets.badgeNew"), form.fontSmall, badge, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
			}

			private static bool IsNew(OptionsPet pet)
			{
				return DateTime.TryParse(pet.lastupdate, out DateTime d) && d > DateTime.Now.AddDays(-30);
			}

			private void DrawDetail(Graphics g)
			{
				links.Clear();
				var panel = new Rectangle(Width - DetailW - 4, 8, DetailW, Height - 16);
				Theme.FillRounded(g, Theme.Surface, panel, 12);
				Theme.DrawRounded(g, Theme.Border, panel, 12);

				var pet = selected;
				closeRect = new Rectangle(panel.Right - 40, panel.Y + 10, 30, 30);
				if (hoverClose) Theme.FillRounded(g, Theme.Hover, closeRect, 6);
				Theme.DrawText(g, "✕", form.fontText, closeRect, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

				int x = panel.X + 20, w = panel.Width - 40, y = panel.Y + 20;
				// big preview
				var iconBack = new Rectangle(x, y, 96, 96);
				Theme.FillRounded(g, Theme.SurfaceAlt, iconBack, 12);
				if (pet.Image != null) Theme.DrawPixelImage(g, pet.Image, new Rectangle(x + 8, y + 8, 80, 80));
				Theme.DrawText(g, pet.DisplayName, form.fontH2, new Rectangle(x + 110, y + 10, w - 140, 48), Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("pets.by", pet.author), form.fontSmall, new Rectangle(x + 110, y + 60, w - 110, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("pets.updated", pet.lastupdate), form.fontSmall, new Rectangle(x + 110, y + 78, w - 110, 18), Theme.TextMuted, TextFormatFlags.SingleLine);
				y += 112;

				adoptRect = new Rectangle(x, y, w, 38);
				bool inUse = pet.folder == CurrentPet;
				Theme.DrawButton(g, adoptRect, inUse ? Lang.T("pets.adoptAnother") : Lang.T("pets.adopt"), form.fontBold, true, hoverAdopt);
				y += 52;

				// stats from the XML
				if (pet.Detail != null)
				{
					var stats = ReadStats(pet.Detail);
					int sw = (w - 16) / 3;
					for (int i = 0; i < stats.Count && i < 3; i++)
					{
						var sr = new Rectangle(x + i * (sw + 8), y, sw, 54);
						Theme.FillRounded(g, Theme.SurfaceAlt, sr, 8);
						Theme.DrawText(g, stats[i].value, form.fontBold, new Rectangle(sr.X, sr.Y + 8, sr.Width, 20), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
						Theme.DrawText(g, stats[i].label, form.fontSmall, new Rectangle(sr.X, sr.Y + 28, sr.Width, 18), Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
					}
					y += 68;
				}
				else
				{
					Theme.DrawText(g, Lang.T("pets.loadingDetails"), form.fontSmall, new Rectangle(x, y, w, 18), Theme.TextMuted);
					y += 28;
				}

				// readme (simple markdown)
				var clip = g.Clip;
				g.SetClip(new Rectangle(panel.X + 1, y, panel.Width - 2, panel.Bottom - y - 8));
				foreach (var raw in pet.Readme)
				{
					if (y > panel.Bottom) break;
					string line = raw.TrimEnd();
					if (line.Length == 0) continue;
					if (line.StartsWith("#"))
					{
						string t = line.TrimStart('#').Trim();
						if (t.Equals(pet.DisplayName, StringComparison.OrdinalIgnoreCase) || t.Equals(pet.folder, StringComparison.OrdinalIgnoreCase)) continue;
						y += 6;
						Theme.DrawText(g, t, form.fontBold, new Rectangle(x, y, w, 20), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
						y += 24;
						continue;
					}
					bool bullet = Regex.IsMatch(line, @"^\s*[*-]\s+");
					string text = Regex.Replace(line, @"^\s*([*-]|\d+\.)\s+", "");
					// links: keep the label, remember the first url of the line
					var m = Regex.Match(text, @"\[(.*?)\]\((.*?)\)");
					string url = m.Success ? m.Groups[2].Value : Regex.Match(text, @"https?://\S+").Value;
					text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
					text = Regex.Replace(text, @"\*\*(.*?)\*\*|\*(.*?)\*|__(.*?)__", "$1$2$3").Replace("\\_", "_");
					text = Regex.Replace(text, @"<[^>]+>", "");
					int indent = bullet ? 14 : 0;
					var size = Theme.MeasureText(g, text, form.fontSmall, w - indent);
					if (bullet) using (var b = new SolidBrush(Theme.TextMuted)) g.FillEllipse(b, x + 2, y + 6, 5, 5);
					bool isLink = !string.IsNullOrEmpty(url) && url.StartsWith("https://");
					var tr = new Rectangle(x + indent, y, w - indent, size.Height);
					Theme.DrawText(g, text, form.fontSmall, tr, isLink ? Theme.Link : Theme.TextMuted);
					if (isLink) links.Add((tr, url));
					y += size.Height + 6;
				}
				g.Clip = clip;
			}

			private static List<(string value, string label)> ReadStats(XmlDocument doc)
			{
				var res = new List<(string, string)>();
				XmlNode root = doc.DocumentElement;
				int Count(string name) => root?.SelectSingleNode("*[local-name()='" + name + "']")?.ChildNodes.Count ?? 0;
				res.Add((Count("animations").ToString(), Lang.T("pets.statAnimations")));
				res.Add((Count("sounds").ToString(), Lang.T("pets.statSounds")));
				var skins = root?.SelectSingleNode("*[local-name()='image']/*[local-name()='skins']")?.InnerText;
				res.Add((string.IsNullOrEmpty(skins) ? "1" : skins, Lang.T("pets.statSkins")));
				return res;
			}

			private OptionsPet PetAt(Point p)
			{
				foreach (var c in cards) if (c.rect.Contains(p) && GridArea.Contains(p)) return c.pet;
				return null;
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var hp = PetAt(e.Location);
				bool ha = selected != null && adoptRect.Contains(e.Location);
				bool hc = selected != null && closeRect.Contains(e.Location);
				bool hl = selected != null && links.Any(l => l.rect.Contains(e.Location));
				Cursor = (hp != null || ha || hc || hl) ? Cursors.Hand : Cursors.Default;
				if (hp != hoverPet || ha != hoverAdopt || hc != hoverClose)
				{
					hoverPet = hp; hoverAdopt = ha; hoverClose = hc;
					Invalidate();
				}
				if (hl) form.StatusText = links.First(l => l.rect.Contains(e.Location)).url;
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hoverPet = null; hoverAdopt = hoverClose = false;
				Invalidate();
			}

			protected override async void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				if (selected != null)
				{
					if (closeRect.Contains(e.Location)) { CloseDetail(); return; }
					if (adoptRect.Contains(e.Location))
					{
						if (selected.folder == CurrentPet) Program.Mainthread.AddSheep();
						else form.AdoptPet(selected);
						return;
					}
					foreach (var l in links) if (l.rect.Contains(e.Location)) { Program.OpenLink(l.url); return; }
				}
				var pet = PetAt(e.Location);
				if (pet == null) return;
				selected = pet;
				Invalidate();
				await form.LoadDetailsAsync(pet);
				if (selected == pet) Invalidate();
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);
				var pet = PetAt(e.Location);
				if (pet != null) form.AdoptPet(pet);
			}
		}

		// ================================================================== Options list

		private sealed class OptionsView : ScrollSurface
		{
			private abstract class Row
			{
				public Image Icon;
				/// <summary>Translation key: "{Key}.title" and "{Key}.desc" are shown.</summary>
				public string Key;
				/// <summary>Optional key overriding the description key.</summary>
				public string DescKey;
				public string Title => Lang.T(Key.EndsWith(".titleInstalled") ? Key : Key + ".title");
				public string Description => Lang.T(DescKey ?? Key + ".desc");
				public Rectangle Rect, Control;
			}
			private sealed class ToggleRow : Row { public string Setting; }
			private sealed class SliderRow : Row { public string Setting; public int Min, Max, Value; public Func<int, string> Format; }
			private sealed class ChoiceRow : Row { public Func<int> Get; public Action<int> Set; public int[] Values; public string[] Texts; public Rectangle[] Segments; }
			private sealed class ActionRow : Row { public string ButtonKey; public Action Run; }
			private sealed class DropDownRow : Row { public Func<string[]> Items; public Func<int> Get; public Action<int> Set; }

			private readonly FormSettings form;
			private readonly List<Row> rows = new List<Row>();
			private SliderRow dragging;
			private object hover;
			private int hoverSegment = -1;

			public OptionsView(FormSettings f) { form = f; }

			public void AddToggle(Image icon, string key, string setting) =>
				rows.Add(new ToggleRow { Icon = icon, Key = key, Setting = setting });

			public void AddSlider(Image icon, string key, string setting, int min, int max, Func<int, string> format) =>
				rows.Add(new SliderRow { Icon = icon, Key = key, Setting = setting, Min = min, Max = max, Format = format, Value = Program.MyData.GetIntValue(setting) });

			public void AddChoice(Image icon, string key, Func<int> get, Action<int> set, int[] values, string[] texts) =>
				rows.Add(new ChoiceRow { Icon = icon, Key = key, Get = get, Set = set, Values = values, Texts = texts });

			public void AddAction(Image icon, string key, string buttonKey, Action run, string descKey = null) =>
				rows.Add(new ActionRow { Icon = icon, Key = key, ButtonKey = buttonKey, Run = run, DescKey = descKey });

			public void AddDropDown(Image icon, string key, Func<string[]> items, Func<int> get, Action<int> set) =>
				rows.Add(new DropDownRow { Icon = icon, Key = key, Items = items, Get = get, Set = set });

			protected override int PaintContent(Graphics g, int scrollY)
			{
				int y = 8 - scrollY;
				int w = Width - ScrollBarWidth - 16;
				foreach (var row in rows)
				{
					int controlW = row is SliderRow ? 260 : row is ChoiceRow cr ? cr.Values.Length * 56 : row is ActionRow ? 120 : row is DropDownRow ? 220 : 130;
					int textW = w - controlW - 110;
					var descSize = Theme.MeasureText(g, row.Description, form.fontSmall, textW);
					int h = Math.Max(76, 40 + descSize.Height + 16);
					row.Rect = new Rectangle(8, y, w, h);
					bool isHover = hover == row;
					Theme.FillRounded(g, isHover ? Theme.SurfaceAlt : Theme.Surface, row.Rect, 10);
					Theme.DrawRounded(g, Theme.Border, row.Rect, 10);

					var iconBack = new Rectangle(row.Rect.X + 16, row.Rect.Y + (h - 44) / 2, 44, 44);
					Theme.FillRounded(g, Theme.AccentSoft, iconBack, 10);
					if (row.Icon != null) g.DrawImage(row.Icon, new Rectangle(iconBack.X + 6, iconBack.Y + 6, 32, 32));

					int tx = row.Rect.X + 76;
					Theme.DrawText(g, row.Title, form.fontBold, new Rectangle(tx, row.Rect.Y + 16, textW, 22), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					Theme.DrawText(g, row.Description, form.fontSmall, new Rectangle(tx, row.Rect.Y + 40, textW, descSize.Height), Theme.TextMuted);

					int cx = row.Rect.Right - controlW - 20;
					int cy = row.Rect.Y + h / 2;
					switch (row)
					{
						case ToggleRow t:
							t.Control = new Rectangle(row.Rect.Right - 20 - 44, cy - 11, 44, 22);
							bool on = Program.MyData.GetIntValue(t.Setting) > 0;
							Theme.DrawToggle(g, t.Control, on);
							Theme.DrawText(g, on ? Lang.T("opt.on") : Lang.T("opt.off"), form.fontSmall, new Rectangle(t.Control.X - 90, cy - 9, 82, 18), Theme.TextMuted, TextFormatFlags.Right | TextFormatFlags.SingleLine);
							break;
						case SliderRow s:
							s.Control = new Rectangle(cx, cy - 12, controlW - 70, 24);
							Theme.DrawSlider(g, s.Control, s.Value - s.Min, s.Max - s.Min);
							Theme.DrawText(g, s.Format(s.Value), form.fontBold, new Rectangle(s.Control.Right + 12, cy - 10, 62, 20), Theme.Text, TextFormatFlags.Right | TextFormatFlags.SingleLine);
							break;
						case ChoiceRow c:
							int current = c.Get();
							c.Control = new Rectangle(row.Rect.Right - 20 - controlW, cy - 16, controlW, 32);
							Theme.FillRounded(g, Theme.SurfaceAlt, c.Control, 8);
							Theme.DrawRounded(g, Theme.Border, c.Control, 8);
							c.Segments = new Rectangle[c.Values.Length];
							for (int i = 0; i < c.Values.Length; i++)
							{
								var seg = new Rectangle(c.Control.X + 3 + i * 56, c.Control.Y + 3, 50, 26);
								c.Segments[i] = seg;
								bool sel = c.Values[i] == current;
								if (sel) Theme.FillRounded(g, Theme.Accent, seg, 6);
								else if (hover == c && hoverSegment == i) Theme.FillRounded(g, Theme.Hover, seg, 6);
								Theme.DrawText(g, c.Texts[i], form.fontBold, seg, sel ? Theme.AccentText : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
							}
							break;
						case ActionRow a:
							a.Control = new Rectangle(row.Rect.Right - 20 - controlW, cy - 17, controlW, 34);
							Theme.DrawButton(g, a.Control, Lang.T(a.ButtonKey), form.fontText, false, hover == a && a.Control.Contains(PointToClient(MousePosition)));
							break;
						case DropDownRow d:
							d.Control = new Rectangle(row.Rect.Right - 20 - controlW, cy - 17, controlW, 34);
							var items = d.Items();
							int idx = Math.Max(0, Math.Min(items.Length - 1, d.Get()));
							bool hot = hover == d && d.Control.Contains(PointToClient(MousePosition));
							Theme.FillRounded(g, hot ? Theme.Hover : Theme.Surface, d.Control, 6);
							Theme.DrawRounded(g, Theme.Border, d.Control, 6);
							Theme.DrawText(g, items.Length > 0 ? items[idx] : "", form.fontText, new Rectangle(d.Control.X + 12, d.Control.Y, d.Control.Width - 40, d.Control.Height), Theme.Text,
								TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
							Theme.DrawText(g, "▾", form.fontText, new Rectangle(d.Control.Right - 28, d.Control.Y, 20, d.Control.Height), Theme.TextMuted,
								TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
							break;
					}
					y += h + 10;
				}
				return y + scrollY;
			}

			/// <summary>Open a themed menu under the drop-down to pick an item.</summary>
			private void OpenDropDown(DropDownRow d)
			{
				var menu = new ContextMenuStrip();
				var items = d.Items();
				int current = d.Get();
				for (int i = 0; i < items.Length; i++)
				{
					int index = i;
					var item = new ToolStripMenuItem(items[i]) { Checked = i == current };
					item.Click += (s, e) => { d.Set(index); Invalidate(); };
					menu.Items.Add(item);
				}
				Theme.ApplyTo(menu);
				menu.MinimumSize = new Size(d.Control.Width, 0);
				menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
				menu.Show(this, new Point(d.Control.X, d.Control.Bottom + 2));
			}

			private Row RowAt(Point p) => rows.FirstOrDefault(r => r.Rect.Contains(p));

			private void SetSliderFromMouse(SliderRow s, int x)
			{
				float pct = Math.Max(0, Math.Min(1, (x - s.Control.X) / (float)s.Control.Width));
				int v = s.Min + (int)Math.Round(pct * (s.Max - s.Min));
				if (v != s.Value) { s.Value = v; Invalidate(); }
			}

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				var row = RowAt(e.Location);
				switch (row)
				{
					case ToggleRow t:
						int v = Program.MyData.GetIntValue(t.Setting) > 0 ? 0 : 1;
						Program.MyData.SetIntValue(t.Setting, v);
						form.StatusText = Lang.T(v > 0 ? "status.enabled" : "status.disabled", t.Title);
						Invalidate();
						break;
					case SliderRow s:
						var hit = s.Control; hit.Inflate(12, 10);
						if (hit.Contains(e.Location)) { dragging = s; Capture = true; SetSliderFromMouse(s, e.X); }
						break;
					case ChoiceRow c:
						for (int i = 0; c.Segments != null && i < c.Segments.Length; i++)
						{
							if (c.Segments[i].Contains(e.Location) && c.Values[i] != c.Get())
							{
								if (MessageBox.Show(form, Lang.T("dlg.restart.text"), c.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
									c.Set(c.Values[i]);
							}
						}
						break;
					case ActionRow a:
						if (a.Control.Contains(e.Location)) a.Run();
						break;
					case DropDownRow d:
						if (d.Control.Contains(e.Location)) OpenDropDown(d);
						break;
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				if (dragging != null) { SetSliderFromMouse(dragging, e.X); return; }
				var row = RowAt(e.Location);
				int seg = -1;
				if (row is ChoiceRow c && c.Segments != null)
					for (int i = 0; i < c.Segments.Length; i++) if (c.Segments[i].Contains(e.Location)) seg = i;
				bool clickable = row is ToggleRow || (row is SliderRow s && s.Control.Contains(e.Location)) || seg >= 0
					|| (row is ActionRow a && a.Control.Contains(e.Location)) || (row is DropDownRow d && d.Control.Contains(e.Location));
				Cursor = clickable ? Cursors.Hand : Cursors.Default;
				if (row != hover || seg != hoverSegment) { hover = row; hoverSegment = seg; Invalidate(); }
				else if (row is ActionRow || row is DropDownRow) Invalidate();
			}

			protected override void OnMouseUp(MouseEventArgs e)
			{
				base.OnMouseUp(e);
				if (dragging != null)
				{
					Program.MyData.SetIntValue(dragging.Setting, dragging.Value);
					form.StatusText = Lang.T("status.setTo", dragging.Title, dragging.Format(dragging.Value));
					dragging = null;
					Capture = false;
				}
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hover = null; hoverSegment = -1;
				Invalidate();
			}
		}

		// ================================================================== About

		private sealed class AboutView : ScrollSurface
		{
			private readonly FormSettings form;
			private readonly List<(Rectangle rect, string url)> links = new List<(Rectangle, string)>();

			public AboutView(FormSettings f) { form = f; }

			private string petTitle = "", petAuthor = "", petVersion = "", petInfo = "";

			/// <summary>Take the header of the pet currently running (no XML parsing, no settings written).</summary>
			protected override void OnVisibleChanged(EventArgs e)
			{
				base.OnVisibleChanged(e);
				if (!Visible) return;
				var header = Program.Mainthread?.CurrentHeader;
				if (header == null) return;
				petTitle = header.Title ?? "";
				petAuthor = header.Author ?? "";
				petVersion = header.Version ?? "";
				petInfo = CleanInfo(header.Info);
			}

			protected override int PaintContent(Graphics g, int scrollY)
			{
				links.Clear();
				int w = Math.Min(760, Width - ScrollBarWidth - 16);
				int x = 8, y = 8 - scrollY;

				var infoSize = Theme.MeasureText(g, petInfo, form.fontSmall, w - 40);
				var card = new Rectangle(x, y, w, 110 + infoSize.Height);
				Theme.FillRounded(g, Theme.Surface, card, 12);
				Theme.DrawRounded(g, Theme.Border, card, 12);
				Theme.DrawText(g, Lang.T("about.currentPet"), form.fontSmall, new Rectangle(card.X + 20, card.Y + 16, 300, 16), Theme.Accent, TextFormatFlags.SingleLine);
				Theme.DrawText(g, petTitle, form.fontH2, new Rectangle(card.X + 20, card.Y + 34, w - 40, 26), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, Lang.T("about.petLine", petAuthor, petVersion), form.fontSmall, new Rectangle(card.X + 20, card.Y + 62, w - 40, 18), Theme.TextMuted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				Theme.DrawText(g, petInfo, form.fontSmall, new Rectangle(card.X + 20, card.Y + 90, w - 40, infoSize.Height), Theme.Text);
				y = card.Bottom + 16;

				// credits: card removed for now, it comes back when the project is finished

				// links
				var linkList = new[] {
					(Lang.T("about.link.github"), AppInfo.RepoUrl),
					(Lang.T("about.link.gallery"), AppInfo.PetsWebUrl),
					(Lang.T("about.link.help"), AppInfo.HelpUrl) };
				var lr = new Rectangle(x, y, w, 52 + linkList.Length * 30);
				Theme.FillRounded(g, Theme.Surface, lr, 12);
				Theme.DrawRounded(g, Theme.Border, lr, 12);
				Theme.DrawText(g, Lang.T("about.links"), form.fontSmall, new Rectangle(lr.X + 20, lr.Y + 16, 300, 16), Theme.Accent, TextFormatFlags.SingleLine);
				int ly = lr.Y + 42;
				foreach (var l in linkList)
				{
					var r = new Rectangle(lr.X + 20, ly, w - 40, 22);
					Theme.DrawText(g, l.Item1 + "  ↗", form.fontText, r, Theme.Link, TextFormatFlags.SingleLine);
					links.Add((new Rectangle(r.X, r.Y, Theme.MeasureText(g, l.Item1 + "  ↗", form.fontText).Width, r.Height), l.Item2));
					ly += 30;
				}
				y = lr.Bottom + 16;
				return y + scrollY;
			}

			private static string CleanInfo(string info)
			{
				if (string.IsNullOrEmpty(info)) return "";
				info = info.Replace("[br]", "\n");
				info = Regex.Replace(info, @"\[link:(.*?)\]", "$1");
				return info.Trim();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				var l = links.FirstOrDefault(x => x.rect.Contains(e.Location));
				Cursor = l.url != null ? Cursors.Hand : Cursors.Default;
				if (l.url != null) form.StatusText = l.url;
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				var l = links.FirstOrDefault(x => x.rect.Contains(e.Location));
				if (l.url != null) Program.OpenLink(l.url);
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
				return new Rectangle(Width - ScrollBarWidth + 2, ty, 6, th);
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
				if (!t.IsEmpty && e.X >= Width - ScrollBarWidth - 4)
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
