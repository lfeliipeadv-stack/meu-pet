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
	public sealed partial class FormSettings : Form
	{
		/// <summary>Pages of the window (sidebar entries).</summary>
		/// <remarks>Behaviour now lives in Application (Settings) and Extras in Plugins; the names stay for old links.</remarks>
		public enum Page { Pets, Behaviour, Application, About, Reminders, Extras, Ai, Home, Plugins, Integrations }

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
		private readonly RemindersView remindersView;
		private readonly HomeView homeView;
		private readonly CardsView pluginsView;
		private readonly CardsView integrationsView;
		private readonly OptionsView aiView;

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
			try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath); } catch (Exception skipped) { LogFile.Ignored(skipped); }
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
			// one Settings page: the pet's behaviour first, then the program
			applicationView = new OptionsView(this) { Dock = DockStyle.Fill };
			behaviourView = applicationView;
			aboutView = new AboutView(this) { Dock = DockStyle.Fill };
			remindersView = new RemindersView(this) { Dock = DockStyle.Fill };
			homeView = new HomeView(this) { Dock = DockStyle.Fill };
			pluginsView = new CardsView(this, BuildPlugins, "day", "care", "fun", "pc") { Dock = DockStyle.Fill };
			integrationsView = new CardsView(this, BuildIntegrations) { Dock = DockStyle.Fill };
			aiView = new OptionsView(this) { Dock = DockStyle.Fill };
			applicationView.AddHeader("set.petSection");
			BuildBehaviourOptions();
			applicationView.AddHeader("set.appSection");
			BuildApplicationOptions();
			BuildAiOptions();

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
			foreach (Control c in new Control[] { sidebar, petsView, applicationView, aboutView, remindersView, aiView, homeView, pluginsView, integrationsView })
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
			foreach (Control c in new Control[] { sidebar, petsView, applicationView, aboutView, remindersView, aiView, homeView, pluginsView, integrationsView }) c.Invalidate();
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
			// old pages: Behaviour is in Settings, Extras became Plugins
			if (p == Page.Behaviour) p = Page.Application;
			if (p == Page.Extras) p = Page.Plugins;
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
				case Page.Home:
					headerTitle.Text = Lang.T("page.home.title");
					headerSubtitle.Text = Lang.T("page.home.subtitle");
					content.Controls.Add(homeView);
					break;
				case Page.Plugins:
					headerTitle.Text = Lang.T("page.plugins.title");
					headerSubtitle.Text = Lang.T("page.plugins.subtitle");
					pluginsView.Reload();
					content.Controls.Add(pluginsView);
					break;
				case Page.Integrations:
					headerTitle.Text = Lang.T("page.integrations.title");
					headerSubtitle.Text = Lang.T("page.integrations.subtitle");
					integrationsView.Reload();
					content.Controls.Add(integrationsView);
					break;
				case Page.Application:
					headerTitle.Text = Lang.T("page.application.title");
					headerSubtitle.Text = Lang.T("page.application.subtitle");
					content.Controls.Add(applicationView);
					break;
				case Page.Ai:
					headerTitle.Text = Lang.T("page.ai.title");
					headerSubtitle.Text = Lang.T("page.ai.subtitle");
					content.Controls.Add(aiView);
					break;
				case Page.Reminders:
					headerTitle.Text = Lang.T("page.reminders.title");
					headerSubtitle.Text = Lang.T("page.reminders.subtitle");
					content.Controls.Add(remindersView);
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

	}
}
