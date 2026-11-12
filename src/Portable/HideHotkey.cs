using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Ctrl+Alt+P anywhere in Windows hides every pet (and its sounds) or shows them again —
    /// handy in a meeting or while sharing the screen. Can be turned off in the settings.
    /// </summary>
    internal static class HideHotkey
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int Id = 0x5045;   // "PE"
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        public const string Text = "Ctrl+Alt+P";

        static Receiver window;
        static bool registered;

        /// <summary>Pets are hidden right now.</summary>
        public static bool Hidden { get; private set; }

        /// <summary>Pets were hidden or shown (the tray menu follows it).</summary>
        public static event Action Changed;

        /// <summary>Register or release the shortcut, following the setting. Returns false if another program has it.</summary>
        const int AskId = 0x5046;
        public const string AskText = "Ctrl+Alt+Space";
        static bool askRegistered;

        /// <summary>Ctrl+Alt+Space opens "Ask the pet" (when the AI is set up). On unless turned off.</summary>
        public static bool AskEnabled
        {
            get => Program.MyData.GetSettingBool("AskHotkey", true);
            set { Program.MyData.SetSetting("AskHotkey", value); ApplyAsk(); }
        }

        public static bool ApplyAsk()
        {
            if (window == null) window = new Receiver();
            if (askRegistered) { UnregisterHotKey(window.Handle, AskId); askRegistered = false; }
            if (!AskEnabled) return true;
            askRegistered = RegisterHotKey(window.Handle, AskId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.Space);
            if (!askRegistered) StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, AskText + " is used by another program");
            return askRegistered;
        }

        public static bool Apply()
        {
            if (window == null) window = new Receiver();
            ApplyAsk();
            if (registered) { UnregisterHotKey(window.Handle, Id); registered = false; }
            if (!Program.MyData.GetHotkeyEnabled()) return true;
            registered = RegisterHotKey(window.Handle, Id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.P);
            if (!registered) StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, Text + " is used by another program");
            return registered;
        }

        public static void Toggle() => SetHidden(!Hidden);

        public static void SetHidden(bool hidden)
        {
            Hidden = hidden;
            // every pet window, children included (balloons, planes…)
            foreach (var pet in Application.OpenForms.OfType<FormPet>().ToList())
            {
                try { pet.Visible = !hidden; } catch (Exception) { }
            }
            Changed?.Invoke();
        }

        sealed class Receiver : NativeWindow
        {
            public Receiver() { CreateHandle(new CreateParams()); }

            protected override void WndProc(ref Message m)
            {
                const int WM_HOTKEY = 0x0312;
                if (m.Msg == WM_HOTKEY && (int)m.WParam == Id) Toggle();
                if (m.Msg == WM_HOTKEY && (int)m.WParam == AskId && !(Form.ActiveForm is FormAsk)) FormAsk.Open();
                base.WndProc(ref m);
            }
        }
    }
}
