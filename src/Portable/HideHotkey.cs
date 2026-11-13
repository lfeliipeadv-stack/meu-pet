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
        const uint MOD_SHIFT = 0x4;
        static bool askRegistered;

        /// <summary>The shortcuts that can open "Ask the pet" (another program may already use one of them).</summary>
        static readonly (uint mods, Keys key)[] AskKeys =
        {
            (MOD_CONTROL | MOD_ALT, Keys.Space),
            (MOD_CONTROL | MOD_SHIFT, Keys.Space),
            (MOD_CONTROL | MOD_ALT, Keys.A),
            (MOD_CONTROL | MOD_ALT, Keys.Q),
        };

        public static int AskKeyCount => AskKeys.Length;

        /// <summary>Name of a shortcut in the language in use ("Ctrl+Alt+Espaço").</summary>
        public static string AskKeyText(int i)
        {
            var (mods, key) = AskKeys[Math.Max(0, Math.Min(AskKeys.Length - 1, i))];
            return ((mods & MOD_CONTROL) != 0 ? "Ctrl+" : "") + ((mods & MOD_SHIFT) != 0 ? "Shift+" : "") + ((mods & MOD_ALT) != 0 ? "Alt+" : "")
                + (key == Keys.Space ? Lang.T("key.space") : key.ToString());
        }

        /// <summary>The shortcut chosen now, as text.</summary>
        public static string AskText => AskKeyText(AskKey);

        /// <summary>Which of <see cref="AskKeys"/> (default Ctrl+Alt+Space).</summary>
        public static int AskKey
        {
            get => Math.Max(0, Math.Min(AskKeys.Length - 1, Program.MyData.GetSettingInt("AskKey", 0)));
            set { Program.MyData.SetSetting("AskKey", value); ApplyAsk(); }
        }

        /// <summary>The shortcut opens "Ask the pet" (when the AI is set up). Off until the user turns it on.</summary>
        public static bool AskEnabled
        {
            get => Program.MyData.GetSettingBool("AskHotkey", false);
            set { Program.MyData.SetSetting("AskHotkey", value); ApplyAsk(); }
        }

        /// <summary>The shortcut is on but another program already has it.</summary>
        public static bool AskBusy => AskEnabled && !askRegistered;

        public static bool ApplyAsk()
        {
            if (window == null) window = new Receiver();
            if (askRegistered) { UnregisterHotKey(window.Handle, AskId); askRegistered = false; }
            if (!AskEnabled) return true;
            var (mods, key) = AskKeys[AskKey];
            askRegistered = RegisterHotKey(window.Handle, AskId, mods | MOD_NOREPEAT, (uint)key);
            if (!askRegistered) StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, AskText + " is used by another program");
            return askRegistered;
        }

        /// <summary>
        /// A while after the program opens: if the AI is set up and the shortcut is taken by another program, the pet
        /// says so once (for that shortcut) and offers to choose another one.
        /// </summary>
        public static void WarnIfBusyLater()
        {
            var t = new Timer { Interval = 25000 };
            t.Tick += (s, e) =>
            {
                t.Stop(); t.Dispose();
                if (!AskBusy || Onboarding.Disabled) return;
                string warned = Program.MyData.GetSetting("AskKeyWarned");
                if (warned == AskKey.ToString()) return;
                Program.MyData.SetSetting("AskKeyWarned", AskKey.ToString());
                SpeechBubble.Notify(Lang.T("ask.keyBusy", AskText), 0,
                    new SpeechBubble.Choice(Lang.T("ask.keyChoose"), () => FormSettings.ShowPage(FormSettings.Page.Ai), true),
                    new SpeechBubble.Choice(Lang.T("vpet.later"), null));
            };
            t.Start();
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
                try { pet.Visible = !hidden; } catch (Exception skipped) { LogFile.Ignored(skipped); }
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
                if (m.Msg == WM_HOTKEY && (int)m.WParam == AskId && !(Form.ActiveForm is FormAsk) && !(Form.ActiveForm is FormChat)) FormAsk.Open();
                base.WndProc(ref m);
            }
        }
    }
}
