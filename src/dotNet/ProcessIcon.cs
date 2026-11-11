using System;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;

namespace DesktopPet
{
        /// <summary>
        /// System Tray Icon. Shows an icon on the Taskbar to allow a ContextMenu.
        /// </summary>
    public sealed class ProcessIcon : IDisposable
    {
            /// <summary>
            /// The NotifyIcon object.
            /// </summary>
        NotifyIcon ni;

            /// <summary>
            /// Initializes a new instance of the <see cref="ProcessIcon"/> class.
            /// </summary>
        public ProcessIcon()
        {
            // Instantiate the NotifyIcon object.
            ni = new NotifyIcon();
        }

            /// <summary>
            /// Displays the icon in the system tray.
            /// </summary>
        public void Display()
        {
            // Put the icon in the system tray and allow it react to mouse clicks.			
            ni.MouseClick += new MouseEventHandler(Ni_MouseClick);
            ni.MouseDoubleClick += new MouseEventHandler(Ni_MouseDoubleClick);

            ni.Text = Lang.T("tray.tooltip", "desktopPet");
            ni.Visible = true;

            // Attach a context menu.
            ni.ContextMenuStrip = new ContextMenus().Create();
            Lang.Changed += () => { if (ni != null) ni.Text = Truncate(Lang.T("tray.tooltip", petNameShown)); };
        }

        string petNameShown = "desktopPet";

        /// <summary>Tray tooltips are limited to 127 characters.</summary>
        static string Truncate(string s) => s.Length > 127 ? s.Substring(0, 127) : s;

            /// <summary>
            /// Displays the icon in the system tray.
            /// </summary>
        public void SetIcon(System.IO.MemoryStream icon, string petName, string aboutAuthor, string aboutTitle, string aboutVersion, string aboutInfo)
        {
            bool success = true;
            petNameShown = string.IsNullOrEmpty(petName) ? "desktopPet" : petName;
			try
			{
				ni.Icon = new Icon(icon, 32, 32);
				ContextMenus.UpdateIcon(ni.Icon, petName, aboutAuthor, aboutTitle, aboutVersion, aboutInfo);
				ni.Text = Truncate(Lang.T("tray.tooltip", petNameShown));
			}
			catch(Exception)
			{
                success = false;
			}
            if(!success)
            {
                try
                {
                    StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "Animation ICON is invalid (icon converter is on the webpage)");
                    ni.Icon = new Icon(Icon.ExtractAssociatedIcon(Application.ExecutablePath), 32, 32);
                    ContextMenus.UpdateIcon(ni.Icon, petName, aboutAuthor, aboutTitle, aboutVersion, aboutInfo);
                }
                catch (Exception) { } // probably thread error.
            }
        }

            /// <summary>
            /// Releases unmanaged and - optionally - managed resources
            /// </summary>
        public void Dispose()
        {
            // When the application closes, this will remove the icon from the system tray immediately.
            if (ni != null)
            {
                if (ni.Icon != null)
                {
                    ni.Icon.Dispose();
                    ni.Icon = null;
                }
                ni.Visible = false;
                ni.Dispose();
                ni = null;
            }
        }

            /// <summary>
            /// Handles the MouseClick event of the ni control.
            /// </summary>
            /// <param name="sender">The source of the event.</param>
            /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        void Ni_MouseClick(object sender, MouseEventArgs e)
        {
            // Handle mouse button clicks.
            if (e.Button == MouseButtons.Left)
            {
                // Start Windows Explorer.
                Program.Mainthread.TopMostSheeps();
            }
        }

            /// <summary>
            /// A double click will automatically start a new pet.
            /// </summary>
            /// <param name="sender">Caller as object.</param>
            /// <param name="e">Mouse event values.</param>
        void Ni_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Handle mouse button clicks.
            if (e.Button == MouseButtons.Left)
            {
                // Start Windows Explorer.
                //Process.Start("explorer", null);
                Program.Mainthread.AddSheep();
            }
        }
    }
}