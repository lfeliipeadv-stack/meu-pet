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
			// a gallery pet that is not downloaded yet: the button must offer the download
			var notDownloaded = f.WebPets.pets.FirstOrDefault(p => p.Variants == null && !f.IsDownloaded(p));
			if (notDownloaded != null) { f.petsView.SelectForSnapshot(notDownloaded); await f.LoadDetailsAsync(notDownloaded); await Task.Delay(300); save("pets-detail-download"); }
			foreach (Page p in new[] { Page.Behaviour, Page.Application, Page.About }) { f.SelectPage(p); await Task.Delay(200); save(p.ToString().ToLowerInvariant()); }
			// color group card (gSheep) with the pink color selected
			var group = f.WebPets.pets.FirstOrDefault(p => p.Variants != null);
			if (group != null)
			{
				f.SelectPage(Page.Pets);
				f.searchBox.Text = "sheep";   // bring the group card into view
				group.SelectVariant(group.Variants.FirstOrDefault(v => v?.folder == "pink_sheep") ?? group.Variants.First(v => v != null));
				f.petsView.SelectForSnapshot(group); await f.LoadDetailsAsync(group); await Task.Delay(300); save("pets-group");
				f.petsView.CloseDetail();
				f.searchBox.Text = "";
			}
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
		private readonly SearchField searchField;
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
			// rounded search field with a magnifier and a clear button (same look as the cards)
			searchField = new SearchField(fontText) { Size = new Size(260, 36), Anchor = AnchorStyles.Top | AnchorStyles.Right };
			searchBox = searchField.Box;
			searchBox.PlaceholderText = Lang.T("pets.search");
			header.Controls.Add(headerTitle);
			header.Controls.Add(headerSubtitle);
			header.Controls.Add(searchField);
			header.Resize += (s, e) =>
			{
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
			Theme.Changed += OnThemeChanged;
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

				// search field: right side of the title row, always above the subtitle (never covered by it),
				// computed from the real (DPI scaled) sizes
				if (searchField != null)
				{
					int top = Math.Max(4, Math.Min(headerTitle.Top + (headerTitle.Height - searchField.Height) / 2, headerSubtitle.Top - searchField.Height - 6));
					searchField.Location = new Point(header.ClientSize.Width - searchField.Width - 26, top);
					searchField.BringToFront();
				}
			}
			finally { layingOutHeader = false; }
		}

		/// <summary>The color theme changed (setting or Windows): recolor the native controls and repaint.</summary>
		private void OnThemeChanged()
		{
			if (IsDisposed) return;
			if (InvokeRequired) { BeginInvoke(new Action(OnThemeChanged)); return; }
			Theme.ApplyWindowChrome(this);
			BackColor = Theme.Background; ForeColor = Theme.Text;
			status.BackColor = Theme.Background; status.ForeColor = Theme.TextMuted;
			header.BackColor = Theme.Background; content.BackColor = Theme.Background;
			headerTitle.ForeColor = Theme.Text; headerSubtitle.ForeColor = Theme.TextMuted;
			searchField.Recolor();
			foreach (Control c in new Control[] { sidebar, petsView, behaviourView, applicationView, aboutView })
			{
				c.BackColor = Theme.Background;
				c.Invalidate();
			}
			header.Invalidate(true);
			Invalidate(true);
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
			Theme.Changed -= OnThemeChanged;
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
					headerSubtitle.Text = Lang.T("page.about.subtitle", Updater.CurrentVersion.ToString(3));
					content.Controls.Add(aboutView);
					break;
			}
			searchField.Visible = p == Page.Pets;
			content.ResumeLayout();
			sidebar.Invalidate();
		}

		// ------------------------------------------------------------------ options

		private void BuildBehaviourOptions()
		{
			// Titles and descriptions are translation keys, resolved at paint time (instant language switch).
			behaviourView.AddToggle("", /* monitor */ "opt.multiscreen", SettingName.Multiscreen);
			behaviourView.AddToggle("", /* stopwatch */ "opt.smooth", SettingName.SmoothMovements);
			behaviourView.AddToggle("", /* taskbar */ "opt.taskbar", SettingName.StealTaskbarFocus);
			behaviourView.AddToggle("", /* window to front */ "opt.foreground", SettingName.WinForeground);
			behaviourView.AddSlider("", /* volume */ "opt.volume", SettingName.Volume, 0, 100, v => Lang.T("opt.volume.value", v));
		}

		private void BuildApplicationOptions()
		{
			// Language: Auto + every language file found in the lang folder.
			var languages = Lang.Available();
			var codes = new List<string> { "auto" };
			codes.AddRange(languages.Select(l => l.Code));
			applicationView.AddDropDown("", /* globe */ "opt.language",
				() => codes.Select(c => c == "auto" ? Lang.T("opt.language.auto") : languages.First(l => l.Code == c).Name).ToArray(),
				() => Math.Max(0, codes.FindIndex(c => string.Equals(c, Program.MyData.GetLanguage(), StringComparison.OrdinalIgnoreCase))),
				i =>
				{
					Program.MyData.SetLanguage(codes[i]);
					Lang.Use(codes[i]);
				});

			// Theme: automatic (Windows), light or dark — applied at once to every window.
			var themeModes = new[] { "auto", "light", "dark" };
			applicationView.AddChoice("", /* color palette */ "opt.theme",
				() => Math.Max(0, Array.IndexOf(themeModes, Theme.Mode)),
				i =>
				{
					Program.MyData.SetTheme(themeModes[i]);
					Theme.Mode = themeModes[i];
				},
				new[] { 0, 1, 2 }, new[] { "opt.theme.auto", "opt.theme.light", "opt.theme.dark" });

			applicationView.AddSlider("", /* start */ "opt.quantity", SettingName.AutostartPets, 0, StartUp.MAX_SHEEPS,
				v => v == 1 ? Lang.T("opt.quantity.one") : Lang.T("opt.quantity.many", v));
			applicationView.AddChoice("", /* size */ "opt.scale",
				() => Program.MyData.GetScale(),
				v => { Program.MyData.SetScale(v); Program.RestartApp(); },
				new[] { 1, 2, 4 }, new[] { "1×", "2×", "4×" }, confirmRestart: true);
			applicationView.AddAction("", /* undo */ "opt.restore", "opt.restore.button", () =>
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
			applicationView.AddAction("", /* download */ Program.IsApplicationInstalled() ? "opt.install.titleInstalled" : "opt.install", "opt.install.button",
				() => ContextMenus.OpenInstaller(), "opt.install.desc");
#endif
		}

		// ------------------------------------------------------------------ pets (download)

		/// <summary>
		/// Pets that only differ in color are shown as one card with a color picker.
		/// (folder, Lang key of the color name, swatch color), in display order.
		/// </summary>
		private static readonly (string name, (string folder, string colorKey, Color swatch)[] variants)[] PetGroups =
		{
			("gSheep", new[]
			{
				("blue_sheep",   "color.blue",   Color.FromArgb(79, 134, 232)),
				("green_sheep",  "color.green",  Color.FromArgb(93, 187, 99)),
				("yellow_sheep", "color.yellow", Color.FromArgb(242, 201, 76)),
				("orange_sheep", "color.orange", Color.FromArgb(240, 146, 58)),
				("red_sheep",    "color.red",    Color.FromArgb(224, 82, 77)),
				("pink_sheep",   "color.pink",   Color.FromArgb(242, 122, 181)),
				("purple_sheep", "color.purple", Color.FromArgb(155, 109, 214)),
			}),
		};

		// ------------------------------------------------------------------ "New" badge

		/// <summary>Pets already seen (folder → lastupdate). Null until the gallery was loaded once.</summary>
		private Dictionary<string, string> seenPets = Program.MyData.GetSeenPets();

		private IEnumerable<OptionsPet> RealPets(OptionsPet pet) =>
			pet.Variants != null ? pet.Variants.Where(v => v != null) : new[] { pet };

		/// <summary>A pet is new if it was never opened/adopted, or was updated since then.</summary>
		private bool IsNewPet(OptionsPet pet)
		{
			if (seenPets == null) return false;
			return RealPets(pet).Any(p => !seenPets.TryGetValue(p.folder ?? "", out string d) || d != (p.lastupdate ?? ""));
		}

		/// <summary>Remember that the user has seen this pet (and all its colors).</summary>
		private void MarkSeen(OptionsPet pet)
		{
			if (seenPets == null || pet == null || !IsNewPet(pet)) return;
			foreach (var p in RealPets(pet)) seenPets[p.folder ?? ""] = p.lastupdate ?? "";
			Program.MyData.SetSeenPets(seenPets);
		}

		/// <summary>First time the gallery is loaded: everything that exists now counts as already seen.</summary>
		private void InitSeenPets()
		{
			if (seenPets != null) return;
			seenPets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var pet in WebPets.pets)
				foreach (var p in RealPets(pet)) seenPets[p.folder ?? ""] = p.lastupdate ?? "";
			Program.MyData.SetSeenPets(seenPets);
		}

		/// <summary>Put a downloaded pet into its color group card. Returns false if it belongs to no group.</summary>
		private bool AddToGroup(OptionsPet pet)
		{
			foreach (var group in PetGroups)
			{
				int index = Array.FindIndex(group.variants, v => v.folder == pet.folder);
				if (index < 0) continue;

				var card = WebPets.pets.FirstOrDefault(p => p.GroupName == group.name);
				if (card == null)
				{
					card = new OptionsPet
					{
						GroupName = group.name,
						Variants = new OptionsPet[group.variants.Length],
						VariantNames = group.variants.Select(v => v.colorKey).ToArray(),
						VariantColors = group.variants.Select(v => v.swatch).ToArray(),
					};
					WebPets.pets.Add(card);
				}
				card.Variants[index] = pet;
				// show the color in use; otherwise the first color of the list that is loaded
				int shown = card.SelectedVariant;
				bool inUse = pet.folder == petsView.CurrentPet;
				bool shownInUse = shown >= 0 && card.Variants[shown].folder == petsView.CurrentPet;
				if (shown < 0 || inUse || (!shownInUse && index < shown)) card.SelectVariant(pet);
				return true;
			}
			return false;
		}

		private async Task LoadPetsAsync()
		{
			// --- built-in pets (embedded, always available offline) ----------------
			foreach (var folder in BuiltInPets.Folders)
			{
				var detail = BuiltInPets.GetDetail(folder);
				var builtIn = new OptionsPet
				{
					folder = folder,
					name = BuiltInPets.GetName(folder),
					author = detail.DocumentElement?.SelectSingleNode("*[local-name()='header']/*[local-name()='author']")?.InnerText,
					lastupdate = "2026-11-11",
					Image = BuiltInPets.GetIcon(folder),
					Readme = BuiltInPets.GetReadMe(folder),
					Detail = detail,
				};
				WebPets.pets.Add(builtIn);
			}
			// The pet in use was adopted before downloads were kept on disk: keep a copy, so it stays "downloaded".
			string current = Program.MyData.GetCurrentPetFolder();
			if (!string.IsNullOrEmpty(current) && !BuiltInPets.IsBuiltIn(current) && !PetStore.Has(current))
			{
				try { PetStore.Save(current, Program.MyData.GetXml(), ""); } catch (Exception) { }
			}
			petsView.PetsChanged();

			// --- online gallery (GitHub) -------------------------------------------
			StatusText = Lang.T("status.loadingList");
			try
			{
				var progress = new Progress<OptionsPet>(pet =>
				{
					// skip if the folder was already added as a built-in
					if (WebPets.pets.Any(p => p.folder == pet.folder && p.GroupName == null)) return;
					if (!AddToGroup(pet)) WebPets.pets.Add(pet);
					petsView.PetsChanged();
					StatusText = Lang.T("status.loadingListCount", WebPets.pets.Count);
				});
				await Task.Run(() => OptionsGit.GetPetList(progress));
				StatusText = Lang.T("status.available", WebPets.pets.Count);
				InitSeenPets();
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.listError", ex.Message);
			}
			petsView.Loading = false;
			petsView.PetsChanged();
		}

		/// <summary>The eSheep comes with the program; any other pet is available once downloaded.</summary>
		public bool IsDownloaded(OptionsPet pet) => pet != null && (BuiltInPets.IsBuiltIn(pet.folder) || PetStore.Has(pet.folder));

		/// <summary>Downloaded, but the gallery has a newer version.</summary>
		public bool IsOutdated(OptionsPet pet)
		{
			if (pet == null || BuiltInPets.IsBuiltIn(pet.folder) || !PetStore.Has(pet.folder)) return false;
			string saved = PetStore.SavedVersion(pet.folder);
			return saved.Length > 0 && !string.IsNullOrEmpty(pet.lastupdate) && saved != pet.lastupdate;
		}

		/// <summary>What the main button of a pet does: add another, download (or update), or adopt.</summary>
		public void PrimaryAction(OptionsPet pet)
		{
			if (pet == null) return;
			if (pet.folder == petsView.CurrentPet && !IsOutdated(pet)) Program.Mainthread.AddSheep();
			else if (!IsDownloaded(pet) || IsOutdated(pet)) DownloadPet(pet);
			else AdoptPet(pet);
		}

		/// <summary>Download a pet from the gallery and keep it on disk (it is not adopted yet).</summary>
		public async void DownloadPet(OptionsPet pet)
		{
			if (pet == null || BuiltInPets.IsBuiltIn(pet.folder)) return;
			StatusText = Lang.T("status.downloading", pet.DisplayName);
			try
			{
				string content = await Task.Run(() => OptionsGit.GetPetXml(pet.folder));
				var xml = new XmlDocument();
				xml.LoadXml(content);                       // refuse a broken file before saving it
				PetStore.Save(pet.folder, xml.OuterXml, pet.lastupdate ?? "");
				MarkSeen(pet);
				StatusText = Lang.T("status.downloaded", pet.DisplayName);
				// the pet in use was updated: reload it right away
				if (pet.folder == petsView.CurrentPet) AdoptPet(pet);
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.downloadError", pet.DisplayName, ex.Message);
			}
			petsView.PetsChanged();
		}

		/// <summary>Set a pet (built in or already downloaded) as the current one.</summary>
		public async void AdoptPet(OptionsPet pet)
		{
			if (pet == null) return;
			try
			{
				string content;
				if (BuiltInPets.IsBuiltIn(pet.folder))
				{
					content = BuiltInPets.GetXml(pet.folder);
				}
				else if (PetStore.Has(pet.folder))
				{
					content = PetStore.Load(pet.folder);
				}
				else
				{
					StatusText = Lang.T("status.downloading", pet.DisplayName);
					content = await Task.Run(() => OptionsGit.GetPetXml(pet.folder));
					PetStore.Save(pet.folder, content, pet.lastupdate ?? "");
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
				MarkSeen(pet);
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
			// group card: load into the selected variant (its own object), then show it if still selected
			if (pet.Variants != null)
			{
				int i = pet.SelectedVariant;
				if (i < 0) return;
				var variant = pet.Variants[i];
				await LoadDetailsAsync(variant);
				if (pet.folder == variant.folder) pet.SelectVariant(variant);
				return;
			}
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
				Theme.DrawPixelImage(g, Resources.logo, new Rectangle(24, 26, 32, 32));   // 64 px art at exactly 1/2: crisp pixels
				Theme.DrawText(g, "desktopPet", form.fontBold, new Rectangle(68, 24, Width - 70, 20), Theme.Text, TextFormatFlags.Left | TextFormatFlags.SingleLine);
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
			private readonly List<(Rectangle rect, int index)> swatches = new List<(Rectangle, int)>();
			private int hoverSwatch = -1;
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
				bool Has(string s) => (s ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
				return pets.Where(p =>
					Has(p.folder) || Has(p.author) || Has(p.DisplayName) ||
					// group card: any of its colors, by folder ("pink_sheep") or color name ("rosa")
					(p.Variants != null && p.Variants.Where(v => v != null).Any(v => Has(v.folder))) ||
					(p.VariantNames != null && p.VariantNames.Any(k => Has(Lang.T(k)))));
			}

			private Rectangle GridArea => selected == null
				? new Rectangle(0, 0, Width - ScrollBarWidth, Height)
				: new Rectangle(0, 0, Width - DetailW - 16 - ScrollBarWidth, Height);

			/// <summary>Order of the gallery cards.</summary>
			private enum SortMode { Recent, Name, Author }
			private SortMode sort = SortMode.Recent;
			private readonly List<(Rectangle rect, SortMode mode)> sortChips = new List<(Rectangle, SortMode)>();
			private int hoverChip = -1;
			private const int MaxCardW = 190;

			private IEnumerable<OptionsPet> Sorted(IEnumerable<OptionsPet> pets)
			{
				switch (sort)
				{
					case SortMode.Name: return pets.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
					case SortMode.Author: return pets.OrderBy(p => p.author ?? "", StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
					default: return pets.OrderByDescending(p => p.lastupdate ?? "").ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
				}
			}

			protected override int PaintContent(Graphics g, int scrollY)
			{
				cards.Clear();
				sortChips.Clear();
				var area = GridArea;
				int y = 4 - scrollY;

				// --- sort chips: "Ordenar: Recentes | Nome | Autor"
				int cx = 8;
				Theme.DrawText(g, Lang.T("pets.sortBy"), form.fontSmall, new Rectangle(cx, y, 120, 28), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				cx += Theme.MeasureText(g, Lang.T("pets.sortBy"), form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 8;
				var modes = new[] { (SortMode.Recent, "pets.sort.recent"), (SortMode.Name, "pets.sort.name"), (SortMode.Author, "pets.sort.author") };
				for (int i = 0; i < modes.Length; i++)
				{
					string text = Lang.T(modes[i].Item2);
					int w = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 24;
					var chip = new Rectangle(cx, y + 2, w, 24);
					bool on = sort == modes[i].Item1;
					Theme.FillRounded(g, on ? Theme.AccentSoft : hoverChip == i ? Theme.Hover : Theme.Surface, chip, 12);
					Theme.DrawRounded(g, on ? Theme.Accent : Theme.Border, chip, 12);
					Theme.DrawText(g, text, form.fontSmall, chip, on ? Theme.Accent : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
					sortChips.Add((chip, modes[i].Item1));
					cx += w + 6;
				}
				y += 38;

				// --- cards fill the width: as many columns as fit, cards stretched up to MaxCardW, grid centered
				int usable = area.Width - 16;
				int cols = Math.Max(1, (usable + Gap) / (CardW + Gap));
				int cardW = Math.Min(MaxCardW, (usable - (cols - 1) * Gap) / cols);
				int x0 = 8 + (usable - (cols * cardW + (cols - 1) * Gap)) / 2;

				var list = FilteredPets().ToList();
				if (list.Count == 0)
				{
					string msg = Loading ? Lang.T("pets.loading") : (filter.Length > 0 ? Lang.T("pets.noMatch", filter) : Lang.T("pets.empty"));
					Theme.DrawText(g, msg, form.fontText, new Rectangle(8, y + 32, area.Width - 16, 40), Theme.TextMuted, TextFormatFlags.HorizontalCenter);
				}

				// --- one list: the eSheep (comes with the program) first, then every pet of the gallery
				var ordered = list.Where(p => p.Variants == null && BuiltInPets.IsBuiltIn(p.folder))
					.Concat(Sorted(list.Where(p => p.Variants != null || !BuiltInPets.IsBuiltIn(p.folder))))
					.ToList();
				int col = 0;
				foreach (var pet in ordered)
				{
					var r = new Rectangle(x0 + col * (cardW + Gap), y, cardW, CardH);
					cards.Add((r, pet));
					if (r.Bottom >= 0 && r.Top <= Height) DrawCard(g, r, pet);
					col++;
					if (col >= cols) { col = 0; y += CardH + Gap; }
				}
				if (col != 0) y += CardH + Gap;
				y += 10;
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
				if (pet.Variants != null)
				{
					// color group: a row of dots instead of the author, the shown color is bigger
					int n = pet.Variants.Length, d = 9, gap = 5, sel = pet.SelectedVariant;
					int total = n * d + (n - 1) * gap, dx = r.X + (r.Width - total) / 2, dy = r.Y + 120;
					var old = g.SmoothingMode;
					g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					for (int i = 0; i < n; i++)
					{
						var dot = new Rectangle(dx + i * (d + gap), dy, d, d);
						if (i == sel) dot.Inflate(2, 2);
						using (var b = new SolidBrush(pet.Variants[i] == null ? Theme.Hover : pet.VariantColors[i])) g.FillEllipse(b, dot);
					}
					g.SmoothingMode = old;
				}
				else
				{
					Theme.DrawText(g, pet.author, form.fontSmall, new Rectangle(r.X + 8, r.Y + 116, r.Width - 16, 18), Theme.TextMuted,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}

				// badge: in use > new (or updated) > downloaded
				string badgeKey = pet.Contains(CurrentPet) ? "pets.badgeInUse"
					: form.IsNewPet(pet) ? "pets.badgeNew"
					: (form.IsDownloaded(pet) && !BuiltInPets.IsBuiltIn(pet.folder)) ? "pets.badgeDownloaded"
					: null;
				if (badgeKey != null)
				{
					string text = Lang.T(badgeKey);
					int bw = Theme.MeasureText(g, text, form.fontSmall, int.MaxValue, TextFormatFlags.SingleLine).Width + 18;
					var badge = new Rectangle(r.Right - bw - 8, r.Y + 8, bw, 20);
					bool muted = badgeKey == "pets.badgeDownloaded";
					Theme.FillRounded(g, muted ? Theme.SurfaceAlt : Theme.AccentSoft, badge, 10);
					Theme.DrawText(g, text, form.fontSmall, badge, muted ? Theme.TextMuted : Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
				}
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

				// color picker of a group card
				swatches.Clear();
				if (pet.Variants != null)
				{
					int sel = pet.SelectedVariant;
					string colorName = sel >= 0 ? Lang.T(pet.VariantNames[sel]) : "";
					Theme.DrawText(g, Lang.T("pets.color", colorName), form.fontBold, new Rectangle(x, y, w, 20), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
					y += 26;
					int n = pet.Variants.Length, gap = 6;
					int size = Math.Min(40, (w - gap * (n - 1)) / n);
					for (int i = 0; i < n; i++)
					{
						var v = pet.Variants[i];
						var sr = new Rectangle(x + i * (size + gap), y, size, size);
						bool isSel = i == sel, isHover = i == hoverSwatch;
						Theme.FillRounded(g, isSel ? Theme.AccentSoft : isHover ? Theme.Hover : Theme.SurfaceAlt, sr, 8);
						if (v?.Image != null) Theme.DrawPixelImage(g, v.Image, Rectangle.Inflate(sr, -4, -4));
						// color bar under the icon, so similar colors are easy to tell apart
						Theme.FillRounded(g, v == null ? Theme.Hover : pet.VariantColors[i], new Rectangle(sr.X + 8, sr.Bottom - 6, sr.Width - 16, 3), 1.5f);
						Theme.DrawRounded(g, isSel ? Theme.Accent : isHover ? Theme.Border : Theme.SurfaceAlt, sr, 8, isSel ? 2f : 1f);
						if (v != null) swatches.Add((sr, i));
					}
					y += size + 14;
				}

				adoptRect = new Rectangle(x, y, w, 38);
				bool inUse = pet.folder == CurrentPet;
				string action = form.IsOutdated(pet) ? Lang.T("pets.downloadUpdate")
					: inUse ? Lang.T("pets.adoptAnother")
					: !form.IsDownloaded(pet) ? Lang.T("pets.download")
					: Lang.T("pets.adopt");
				Theme.DrawButton(g, adoptRect, action, form.fontBold, true, hoverAdopt);
				y += 52;

				// stats from the XML
				if (pet.Detail != null)
				{
					var stats = ReadStats(pet.Detail);
					// color group: the third box counts the colors to choose from, not the skins of one file
					if (pet.Variants != null && stats.Count >= 3)
						stats[2] = (pet.Variants.Length.ToString(), Lang.T("pets.statColors"));
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
				int hs = selected == null ? -1 : swatches.Where(s => s.rect.Contains(e.Location)).Select(s => s.index).DefaultIfEmpty(-1).First();
				int hch = sortChips.FindIndex(c => c.rect.Contains(e.Location));
				Cursor = (hp != null || ha || hc || hl || hs >= 0 || hch >= 0) ? Cursors.Hand : Cursors.Default;
				if (hp != hoverPet || ha != hoverAdopt || hc != hoverClose || hs != hoverSwatch || hch != hoverChip)
				{
					hoverPet = hp; hoverAdopt = ha; hoverClose = hc; hoverSwatch = hs; hoverChip = hch;
					Invalidate();
				}
				if (hs >= 0 && selected?.VariantNames != null)
					form.StatusText = Lang.T("pets.color", Lang.T(selected.VariantNames[hs]));
				if (hl) form.StatusText = links.First(l => l.rect.Contains(e.Location)).url;
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hoverPet = null; hoverAdopt = hoverClose = false; hoverSwatch = -1; hoverChip = -1;
				Invalidate();
			}

			protected override async void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);
				foreach (var c in sortChips)
				{
					if (!c.rect.Contains(e.Location)) continue;
					sort = c.mode;
					Invalidate();
					return;
				}
				if (selected != null)
				{
					if (closeRect.Contains(e.Location)) { CloseDetail(); return; }
					if (adoptRect.Contains(e.Location))
					{
						form.PrimaryAction(selected);
						return;
					}
					foreach (var l in links) if (l.rect.Contains(e.Location)) { Program.OpenLink(l.url); return; }
					foreach (var s in swatches)
					{
						if (!s.rect.Contains(e.Location)) continue;
						var card = selected;
						card.SelectVariant(card.Variants[s.index]);
						Invalidate();
						await form.LoadDetailsAsync(card);
						if (selected == card) Invalidate();
						return;
					}
				}
				var pet = PetAt(e.Location);
				if (pet == null) return;
				selected = pet;
				form.MarkSeen(pet);
				Invalidate();
				await form.LoadDetailsAsync(pet);
				if (selected == pet) Invalidate();
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);
				var pet = PetAt(e.Location);
				if (pet != null) form.PrimaryAction(pet);
			}
		}

		// ================================================================== Options list

		private sealed class OptionsView : ScrollSurface
		{
			private abstract class Row
			{
				public string Glyph;   // Segoe Fluent Icons code point
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
			private sealed class ChoiceRow : Row { public bool ConfirmRestart; public Func<int> Get; public Action<int> Set; public int[] Values; public string[] Texts; public Rectangle[] Segments; }
			private sealed class ActionRow : Row { public string ButtonKey; public Action Run; }
			private sealed class DropDownRow : Row { public Func<string[]> Items; public Func<int> Get; public Action<int> Set; }

			private readonly FormSettings form;
			private readonly List<Row> rows = new List<Row>();
			private SliderRow dragging;
			private object hover;
			private int hoverSegment = -1;

			public OptionsView(FormSettings f) { form = f; }

			/// <summary>Windows 11 icon font (Segoe Fluent Icons, or Segoe MDL2 Assets on Windows 10).</summary>
			private Font glyphFont;
			private Font GlyphFont => glyphFont ?? (glyphFont = new Font(new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(n => { using (var f = new Font(n, 10)) return string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase); }) ?? Theme.FontFamilyName, 20, GraphicsUnit.Pixel));

			public void AddToggle(string glyph, string key, string setting) =>
				rows.Add(new ToggleRow { Glyph = glyph, Key = key, Setting = setting });

			public void AddSlider(string glyph, string key, string setting, int min, int max, Func<int, string> format) =>
				rows.Add(new SliderRow { Glyph = glyph, Key = key, Setting = setting, Min = min, Max = max, Format = format, Value = Program.MyData.GetIntValue(setting) });

			public void AddChoice(string glyph, string key, Func<int> get, Action<int> set, int[] values, string[] texts, bool confirmRestart = false) =>
				rows.Add(new ChoiceRow { Glyph = glyph, ConfirmRestart = confirmRestart, Key = key, Get = get, Set = set, Values = values, Texts = texts });

			public void AddAction(string glyph, string key, string buttonKey, Action run, string descKey = null) =>
				rows.Add(new ActionRow { Glyph = glyph, Key = key, ButtonKey = buttonKey, Run = run, DescKey = descKey });

			public void AddDropDown(string glyph, string key, Func<string[]> items, Func<int> get, Action<int> set) =>
				rows.Add(new DropDownRow { Glyph = glyph, Key = key, Items = items, Get = get, Set = set });

			protected override int PaintContent(Graphics g, int scrollY)
			{
				int y = 8 - scrollY;
				int w = Width - ScrollBarWidth - 16;
				foreach (var row in rows)
				{
					// segments of a choice grow with their text ("1×" … "Automático")
					int segW = row is ChoiceRow crw ? Math.Max(50, crw.Texts.Max(t => Theme.MeasureText(g, Lang.T(t), form.fontBold, int.MaxValue, TextFormatFlags.SingleLine).Width) + 22) : 0;
					int controlW = row is SliderRow ? 260 : row is ChoiceRow cr ? cr.Values.Length * (segW + 6) : row is ActionRow ? 120 : row is DropDownRow ? 220 : 130;
					int textW = w - controlW - 110;
					var descSize = Theme.MeasureText(g, row.Description, form.fontSmall, textW);
					int h = Math.Max(76, 40 + descSize.Height + 16);
					row.Rect = new Rectangle(8, y, w, h);
					bool isHover = hover == row;
					Theme.FillRounded(g, isHover ? Theme.SurfaceAlt : Theme.Surface, row.Rect, 10);
					Theme.DrawRounded(g, Theme.Border, row.Rect, 10);

					var iconBack = new Rectangle(row.Rect.X + 16, row.Rect.Y + (h - 44) / 2, 44, 44);
					Theme.FillRounded(g, Theme.AccentSoft, iconBack, 10);
					if (!string.IsNullOrEmpty(row.Glyph))
						TextRenderer.DrawText(g, row.Glyph, GlyphFont, iconBack, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

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
								var seg = new Rectangle(c.Control.X + 3 + i * (segW + 6), c.Control.Y + 3, segW, 26);
								c.Segments[i] = seg;
								bool sel = c.Values[i] == current;
								if (sel) Theme.FillRounded(g, Theme.Accent, seg, 6);
								else if (hover == c && hoverSegment == i) Theme.FillRounded(g, Theme.Hover, seg, 6);
								// texts may be translation keys (instant language switch); plain texts pass through
								Theme.DrawText(g, Lang.T(c.Texts[i]), form.fontBold, seg, sel ? Theme.AccentText : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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
								// only options that really restart the program ask first (pet size); the theme applies at once
									if (!c.ConfirmRestart || MessageBox.Show(form, Lang.T("dlg.restart.text"), c.Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
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
	/// <summary>
	/// Pets downloaded from the gallery, kept on disk so they can be adopted later (also offline):
	/// %LocalAppData%\DesktopPet\pets\<folder>\animations.xml (+ version.txt with the gallery's lastupdate).
	/// </summary>
	internal static class PetStore
	{
		static string Root => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "pets");

		static string Dir(string folder)
		{
			// a folder name from the gallery is a plain name; never let it point outside the store
			string safe = string.Concat((folder ?? "").Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
			if (safe.Length == 0) throw new ArgumentException("Invalid pet folder");
			return System.IO.Path.Combine(Root, safe);
		}

		public static bool Has(string folder)
		{
			try { return System.IO.File.Exists(System.IO.Path.Combine(Dir(folder), "animations.xml")); }
			catch (Exception) { return false; }
		}

		public static string Load(string folder) => System.IO.File.ReadAllText(System.IO.Path.Combine(Dir(folder), "animations.xml"), System.Text.Encoding.UTF8);

		public static string SavedVersion(string folder)
		{
			try
			{
				string p = System.IO.Path.Combine(Dir(folder), "version.txt");
				return System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p).Trim() : "";
			}
			catch (Exception) { return ""; }
		}

		public static void Save(string folder, string xml, string version)
		{
			string dir = Dir(folder);
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "animations.xml"), xml, new System.Text.UTF8Encoding(false));
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "version.txt"), version ?? "");
		}
	}
}
