using System;
using System.Windows.Forms;
using DesktopPet.Properties;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet
{
        /// <summary>
        /// The only way to interact with the application (not the pet itself) is over the context menu.<br />
        /// This menu is available once you press on the tray icon (near the windows clock).
        /// </summary>
    class ContextMenus : IDisposable
    {
            /// <summary>
            /// Sheep Menu Item: if another pet was downloaded, the icon and text of this item will change.
            /// </summary>
        static ToolStripMenuItem newSheepMenuItem;
            /// <summary>
            /// Close Menu Item: if another pet was downloaded, the text of this item will change.
            /// </summary>
        static ToolStripMenuItem closeSheepMenuItem;

#if PORTABLE
        /// <summary>
        /// Install functions and form.
        /// </summary>
        static Install installForm;

        /// <summary>
        /// Open the install / uninstall window (used by the settings window too).
        /// </summary>
        public static void OpenInstaller()
        {
            if (installForm == null || installForm.IsDisposed) installForm = new Install();
            installForm.ShowInstallation();
        }
#endif

        /// <summary>
        /// A value to set in the About dialog: author.
        /// </summary>
        static string author;
            /// <summary>
            /// A value to set in the About dialog: animation version.
            /// </summary>
        static string version;
            /// <summary>
            /// A value to set in the About dialog: animation title.
            /// </summary>
        static string title;
            /// <summary>
            /// A value to set in the About dialog: description and information about the animation.
            /// </summary>
        static string info;
        
        LocalData MyData = new LocalData();
        bool isAboutLoaded = false;
        bool isOptionLoaded = false;

        /// <summary>
        /// Creates this instance for the tray icon.
        /// </summary>
        /// <returns>ContextMenuStrip to add in the tray icon.</returns>
        public ContextMenuStrip Create()
        {
                // Add the default menu options.
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem item;
            ToolStripSeparator sep;

			// Item: Open the pet panel (first, the main action).
			optionsMenuItem = item = new ToolStripMenuItem
			{
				Text = Lang.T("tray.panel")
			};
			item.Click += new EventHandler(Options_Click);
            item.Image = Resources.icon.ToBitmap();
            item.Font = new Font(item.Font, item.Font.Style | FontStyle.Bold);
            menu.Items.Add(item);

			// Item: Add another pet (shows the current pet's name and icon).
			newSheepMenuItem = new ToolStripMenuItem
			{
				Text = Lang.T("tray.addDefault")
			};
			newSheepMenuItem.Click += new EventHandler(AddNewSheep_Click);
            newSheepMenuItem.Image = Resources.icon.ToBitmap();
            menu.Items.Add(newSheepMenuItem);

            // Item: Hide / show every pet (also Ctrl+Alt+P).
            hideMenuItem = new ToolStripMenuItem { ShortcutKeyDisplayString = HideHotkey.Text };
            hideMenuItem.Click += (s, e) => HideHotkey.Toggle();
            menu.Items.Add(hideMenuItem);
            HideHotkey.Changed += UpdateTexts;
            HideHotkey.Apply();

            // Item: Reminders (quick "remind me in…" and the reminders page).
            remindersMenuItem = new ToolStripMenuItem();
            foreach (int m in new[] { 5, 10, 15, 30, 60 })
            {
                int minutes = m;
                var quick = new ToolStripMenuItem { Tag = minutes };
                quick.Click += (s, e) => Reminders.In(minutes);
                remindersMenuItem.DropDownItems.Add(quick);
            }
            remindersMenuItem.DropDownItems.Add(new ToolStripSeparator());
            var manage = new ToolStripMenuItem { Tag = "manage" };
            manage.Click += (s, e) => FormSettings.ShowPage(FormSettings.Page.Reminders);
            remindersMenuItem.DropDownItems.Add(manage);
            menu.Items.Add(remindersMenuItem);
            Reminders.Start();
            VirtualPet.Start();
            Mood.Start();
            PcMonitor.Apply();
            AgentBridge.Apply();

                // Item: Separator.
            sep = new ToolStripSeparator();
            menu.Items.Add(sep);

#if PORTABLE
            // Create install class
            installForm = new Install();

            // Item: Install.
            installMenuItem = item = new ToolStripMenuItem();
            item.Click += new EventHandler(InstallApplication);
            item.Image = SmallImage(Resources.install, 32);   // software box with a disc: reads as "installer"
            menu.Items.Add(item);
#endif
			// Item: Audio.
			soundMenuItem = item = new ToolStripMenuItem
			{
				Text = Lang.T("tray.sound")
			};
			item.Click += new EventHandler(Audio_Click);
			item.Image = Resources.audioon;
			menu.Items.Add(item);

			// Item: Separator.
			sep = new ToolStripSeparator();
			menu.Items.Add(sep);

			// Item: About.
			aboutMenuItem = item = new ToolStripMenuItem
			{
				Text = Lang.T("tray.about")
			};
			item.Click += new EventHandler(About_Click);
            item.Image = Resources.about;
            menu.Items.Add(item);

			// Item: Help.
			helpMenuItem = item = new ToolStripMenuItem
			{
				Text = Lang.T("tray.help")
			};
			item.Click += new EventHandler(Help_Click);
            item.Image = Resources.help;
            menu.Items.Add(item);

            // Item: Separator.
            sep = new ToolStripSeparator();
            menu.Items.Add(sep);

			// Item: Close application.
			closeSheepMenuItem = new ToolStripMenuItem
			{
				Text = Lang.T("tray.exitDefault")
			};
			closeSheepMenuItem.Click += new EventHandler(Exit_Click);
            closeSheepMenuItem.Image = Resources.exit;
            menu.Items.Add(closeSheepMenuItem);

            if(Program.MyData.IsFirstBoot())
            {
                OpenOptionWindow("xamlesheep://options");
            }

#if NETCORE
            Theme.ApplyTo(menu);
            menu.Opening += (s, e) => Theme.ApplyTo(menu); // follow light/dark changes
#endif
            UpdateTexts();
            Lang.Changed += UpdateTexts;     // follow language changes
            Program.MyData.PetSoundChanged += UpdateTexts;   // sound switched in the panel
            PetUpdates.Changed += UpdateTexts;               // "Open Panel (1 update)"
            PetUpdates.Start();
            return menu;
        }

        static ToolStripMenuItem hideMenuItem, remindersMenuItem;
        static ToolStripMenuItem optionsMenuItem, installMenuItem, soundMenuItem, aboutMenuItem, helpMenuItem;
        static string currentPetName;

        /// <summary>
        /// (Re)write every menu text in the current language.
        /// </summary>
        public static void UpdateTexts()
        {
            if (newSheepMenuItem == null) return;
            bool hasName = !string.IsNullOrEmpty(currentPetName);
            newSheepMenuItem.Text = hasName ? Lang.T("tray.add", currentPetName) : Lang.T("tray.addDefault");
            closeSheepMenuItem.Text = hasName ? Lang.T("tray.exit", currentPetName) : Lang.T("tray.exitDefault");
            if (hideMenuItem != null)
            {
                hideMenuItem.Text = HideHotkey.Hidden ? Lang.T("tray.showPets") : Lang.T("tray.hidePets");
                hideMenuItem.ShortcutKeyDisplayString = Program.MyData.GetHotkeyEnabled() ? HideHotkey.Text : "";
            }
            if (remindersMenuItem != null)
            {
                remindersMenuItem.Text = Lang.T("tray.reminders");
                foreach (ToolStripItem it in remindersMenuItem.DropDownItems)
                {
                    if (it.Tag is int m) it.Text = Lang.T("tray.remindIn", m);
                    else if (Equals(it.Tag, "manage")) it.Text = Lang.T("tray.manageReminders");
                }
            }
            int updates = PetUpdates.Outdated.Count;
            optionsMenuItem.Text = updates > 0 ? Lang.T(updates == 1 ? "tray.panelUpdateOne" : "tray.panelUpdates", updates) : Lang.T("tray.panel");
            bool muted = Program.MyData.IsPetMuted(Program.MyData.CurrentPetKey());
            soundMenuItem.Text = hasName ? Lang.T("tray.soundPet", currentPetName) : Lang.T("tray.sound");
            soundMenuItem.Checked = !muted;
            soundMenuItem.Image = muted ? Resources.audiooff : Resources.audioon;
            aboutMenuItem.Text = Lang.T("tray.about");
            helpMenuItem.Text = Lang.T("tray.help");
            if (installMenuItem != null)
                installMenuItem.Text = Program.IsApplicationInstalled() ? Lang.T("tray.repair") : Lang.T("tray.install");
        }

        private void OpenOptionWindow(string url) { }

        /// <summary>A large picture reduced smoothly to menu-icon size (the menu would scale it roughly).</summary>
        static Bitmap SmallImage(Image source, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, size, size));
            }
            return bmp;
        }

        /// <summary>
        /// Set a new icon in the context menu with the new pet and updated the info to show in the about dialog.<br />
        /// This function is called every time a new pet is loaded.
        /// </summary>
        /// <param name="newIcon">Icon with the new image.</param>
        /// <param name="petName">Name of the pet, to show in the context menu.</param>
        /// <param name="aboutAuthor">Name of the author.</param>
        /// <param name="aboutTitle">Title of the animation.</param>
        /// <param name="aboutVersion">Version of the animation.</param>
        /// <param name="aboutInfo">About the animation (copyright and author information)</param>
        static public void UpdateIcon(Icon newIcon, string petName, string aboutAuthor, string aboutTitle, string aboutVersion, string aboutInfo)
        {
            currentPetName = petName;
            newSheepMenuItem.Image = newIcon.ToBitmap();
            UpdateTexts();

            author = aboutAuthor;
            title = aboutTitle;
            version = aboutVersion;
            info = aboutInfo;
        }

            /// <summary>
            /// Handles the Click event of the Explorer control: add another pet in the desktop.
            /// </summary>
            /// <param name="sender">The source of the event.</param>
            /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void AddNewSheep_Click(object sender, EventArgs e)
        {
            //Process.Start("explorer", null);
            Program.Mainthread.AddSheep();
        }


#if PORTABLE
        /// <summary>
        /// Open the window form to install this application on the computer.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void InstallApplication(object sender, EventArgs e)
        {
            installForm.ShowInstallation();
        }
#endif

        /// <summary>
        /// Handles the Click event of the About control. Open a dialog if no other dialog is still opened.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void About_Click(object sender, EventArgs e)
        {
#if NETCORE
            FormSettings.ShowPage(FormSettings.Page.About);
#else
            if (isOptionLoaded)
            {

            }
            else if (!isAboutLoaded)
            {
                isAboutLoaded = true;
                AboutBox box = new AboutBox();
                box.FillData(author, title, version, info);
                box.ShowDialog();
                isAboutLoaded = false;
            }
#endif
        }

		/// <summary>
		/// Handles the Click event of the Audio control. It will mute the audio until deactivated or until the next start.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		void Audio_Click(object sender, EventArgs e)
		{
            // sounds are on/off per pet: this item switches the pet on screen (the panel shows the same switch)
            string pet = Program.MyData.CurrentPetKey();
            Program.MyData.SetPetMuted(pet, !Program.MyData.IsPetMuted(pet));
            UpdateTexts();
		}

		/// <summary>
		/// Handles the Click event of the Help control. Open a dialog if no other dialog is still opened.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		void Help_Click(object sender, EventArgs e)
        {
#if NETCORE
            // The old Help window used the Internet Explorer WebBrowser control: open the page in the real browser.
            Program.OpenLink(AppInfo.HelpUrl);
#else
            FormHelp help = new FormHelp();
            help.Show();
#endif
        }

        /// <summary>
        /// Handles the Click event of the Option control. Open a dialog if no other dialog is still opened.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void Options_Click(object sender, EventArgs e)
        {
#if NETCORE
            FormSettings.ShowPage(FormSettings.Page.Pets);
#else
            if (isAboutLoaded)
            {

            }
            else if (!isOptionLoaded)
            {
                isOptionLoaded = true;
                Program.OpenOptionDialog();
                isOptionLoaded = false;
            }
#endif
        }

            /// <summary>
            /// Processes a menu item. Will close the application after closing all pets.
            /// </summary>
            /// <param name="sender">The sender.</param>
            /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void Exit_Click(object sender, EventArgs e)
        {
            // Quit without further ado.
            //Application.Exit();
            Program.Mainthread.KillSheeps(true);
        }


        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        public void Dispose()
        {
#if PORTABLE
            installForm?.Dispose();
#endif
        }
    }
}