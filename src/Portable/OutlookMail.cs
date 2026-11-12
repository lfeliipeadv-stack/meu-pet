using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
	/// <summary>
	/// "You have a new e-mail in Outlook, open it?": while the classic Outlook (desktop) is open, the pet looks every
	/// 30 seconds at how many messages the Inbox has (only the numbers: never the sender, subject or text) and tells
	/// you when a new unread one arrives. It never starts Outlook and lets it go at once after looking.
	/// The new Outlook (and Outlook on the web) have no local interface for this.
	/// </summary>
	public static class OutlookMail
	{
		[DllImport("ole32.dll")] static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);
		[DllImport("oleaut32.dll")] static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object unk);

		const int olFolderInbox = 6;
		static Timer timer;
		static int lastTotal = -1, lastUnread = -1;

		public static bool Enabled
		{
			get => Program.MyData.GetSettingBool("OutlookMail", true);
			set { Program.MyData.SetSetting("OutlookMail", value); lastTotal = lastUnread = -1; }
		}

		public static void Start()
		{
			if (timer != null) return;
			timer = new Timer { Interval = 30000 };
			timer.Tick += (s, e) => Check();
			timer.Start();
		}

		/// <summary>The classic Outlook, only if it is already running (never started by us), or null.</summary>
		static object RunningOutlook()
		{
			if (!Process.GetProcessesByName("OUTLOOK").Any()) return null;
			if (CLSIDFromProgID("Outlook.Application", out Guid clsid) != 0) return null;
			return GetActiveObject(ref clsid, IntPtr.Zero, out object app) == 0 ? app : null;
		}

		static void Release(object o) { try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch (Exception) { } }

		/// <summary>(messages in the Inbox, unread ones), or null when the classic Outlook is not open.</summary>
		public static (int total, int unread)? Counts()
		{
			object app = null, ns = null, inbox = null, items = null;
			try
			{
				app = RunningOutlook();
				if (app == null) return null;
				dynamic a = app;
				ns = a.GetNamespace("MAPI");
				inbox = ((dynamic)ns).GetDefaultFolder(olFolderInbox);
				items = ((dynamic)inbox).Items;
				int total = ((dynamic)items).Count;
				int unread = ((dynamic)inbox).UnReadItemCount;
				return (total, unread);
			}
			catch (Exception ex)
			{
				StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.info, "outlook: " + ex.Message);
				return null;
			}
			finally { Release(items); Release(inbox); Release(ns); Release(app); }
		}

		static void Check()
		{
			if (!Enabled) return;
			var c = Counts();
			if (c == null) { lastTotal = lastUnread = -1; return; }
			var (total, unread) = c.Value;
			bool first = lastTotal < 0;
			int newOnes = total - lastTotal;
			bool arrived = !first && newOnes > 0 && unread > lastUnread;
			lastTotal = total; lastUnread = unread;
			if (!arrived) return;
			Program.Mainthread?.ReactAll("wave", "hello", "look around");
			SpeechBubble.Notify(newOnes == 1 ? Lang.T("mail.newOne") : Lang.T("mail.newMany", newOnes), 15,
				new SpeechBubble.Choice(Lang.T("mail.open"), Open, true),
				new SpeechBubble.Choice(Lang.T("mail.later"), null));
		}

		/// <summary>Bring the Outlook Inbox to the front.</summary>
		public static void Open()
		{
			object app = null, ns = null, inbox = null;
			try
			{
				app = RunningOutlook();
				if (app == null) { Process.Start(new ProcessStartInfo("outlook.exe") { UseShellExecute = true }); return; }
				ns = ((dynamic)app).GetNamespace("MAPI");
				inbox = ((dynamic)ns).GetDefaultFolder(olFolderInbox);
				((dynamic)inbox).Display();
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "outlook open: " + ex.Message); }
			finally { Release(inbox); Release(ns); Release(app); }
		}

		/// <summary>True if only the new Outlook is open (the pet cannot see its e-mails).</summary>
		public static bool OnlyNewOutlook => Process.GetProcessesByName("olk").Any() && !Process.GetProcessesByName("OUTLOOK").Any();
	}
}
