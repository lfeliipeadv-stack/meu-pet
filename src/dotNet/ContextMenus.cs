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

			// Item: New Sheep.
			newSheepMenuItem = new ToolStripMenuItem
			{
				Text = Lang.T("tray.addDefault")
			};
			newSheepMenuItem.Click += new EventHandler(AddNewSheep_Click);
            newSheepMenuItem.Image = Resources.icon.ToBitmap();
            newSheepMenuItem.Font = new Font(newSheepMenuItem.Font, newSheepMenuItem.Font.Style | FontStyle.Bold);
            menu.Items.Add(newSheepMenuItem);

			// Item: Options.
			optionsMenuItem = item = new ToolStripMenuItem
			{
				Text = Lang.T("tray.options")
			};
			item.Click += new EventHandler(Options_Click);
            item.Image = Resources.option;
            menu.Items.Add(item);

                // Item: Separator.
            sep = new ToolStripSeparator();
            menu.Items.Add(sep);

#if PORTABLE
            // Create install class
            installForm = new Install();

            // Item: Install.
            installMenuItem = item = new ToolStripMenuItem();
            item.Click += new EventHandler(InstallApplication);
            item.Image = installForm.Icon.ToBitmap();
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
            return menu;
        }

        static ToolStripMenuItem optionsMenuItem, installMenuItem, soundMenuItem, aboutMenuItem, helpMenuItem;
        static string currentPetName;

        /// <summary>
        /// (Re)write every menu text in the current language.
        /// </summary>
        static void UpdateTexts()
        {
            if (newSheepMenuItem == null) return;
            bool hasName = !string.IsNullOrEmpty(currentPetName);
            newSheepMenuItem.Text = hasName ? Lang.T("tray.add", currentPetName) : Lang.T("tray.addDefault");
            closeSheepMenuItem.Text = hasName ? Lang.T("tray.exit", currentPetName) : Lang.T("tray.exitDefault");
            optionsMenuItem.Text = Lang.T("tray.options");
            soundMenuItem.Text = Lang.T("tray.sound");
            aboutMenuItem.Text = Lang.T("tray.about");
            helpMenuItem.Text = Lang.T("tray.help");
            if (installMenuItem != null)
                installMenuItem.Text = Program.IsApplicationInstalled() ? Lang.T("tray.repair") : Lang.T("tray.install");
        }

        private void OpenOptionWindow(string url) { }

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
            var item = sender as ToolStripMenuItem;
            if(item != null)
            {
                if(Program.MyData.Mute)
                {
					Program.MyData.Mute = false;
                    item.Image = Resources.audioon;

				}
                else
                {
					Program.MyData.Mute = true;
					item.Image = Resources.audiooff;
				}
            }
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