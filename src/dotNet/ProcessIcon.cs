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
            Current = this;
            ni.BalloonTipClicked += (s, e) => { var a = balloonClick; balloonClick = null; a?.Invoke(); };
        }

        /// <summary>The tray icon of the program (for notifications).</summary>
        public static ProcessIcon Current { get; private set; }

        Action balloonClick;

        /// <summary>A Windows notification from the tray icon; <paramref name="onClick"/> runs if it is clicked.</summary>
        public void Notify(string title, string text, Action onClick)
        {
            if (ni == null) return;
            balloonClick = onClick;
            ni.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
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
            ni.Icon = AppIcon();   // always the program's own icon, whatever pet is on screen
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
            // The tray icon stays the program's icon; the pet's icon only appears next to
            // "Add another <pet>" in the menu, and its name in the tooltip.
            petNameShown = string.IsNullOrEmpty(petName) ? "desktopPet" : petName;
            Icon petIcon;
            try
            {
                petIcon = new Icon(icon, 32, 32);
            }
            catch (Exception)
            {
                StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "Animation ICON is invalid (icon converter is on the webpage)");
                petIcon = AppIcon();
            }
            try
            {
                ContextMenus.UpdateIcon(petIcon, petName, aboutAuthor, aboutTitle, aboutVersion, aboutInfo);
                ni.Text = Truncate(Lang.T("tray.tooltip", petNameShown));
            }
            catch (Exception skipped) { LogFile.Ignored(skipped); } // probably thread error.
        }

        /// <summary>The program's own icon, at tray size.</summary>
        static Icon AppIcon()
        {
            try { return new Icon(Properties.Resources.icon, 32, 32); }
            catch (Exception) { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
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