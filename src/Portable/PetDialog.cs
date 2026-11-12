using System;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// Base of the program's little windows where you type something (question, shortcut, reminder, AI, calendar…):
	/// while one is open, every pet stands still (see <see cref="FormPet.Hold"/>), and they go on when it closes.
	/// </summary>
	public class PetDialog : Form
	{
		bool holding;

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			if (!holding) { holding = true; FormPet.Hold(true); }
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			if (holding) { holding = false; FormPet.Hold(false); }
			base.OnFormClosed(e);
		}

		/// <summary>Self-test: the windows of this kind that are open.</summary>
		internal static int OpenCount { get { int n = 0; foreach (Form f in Application.OpenForms) if (f is PetDialog) n++; return n; } }
	}
}
