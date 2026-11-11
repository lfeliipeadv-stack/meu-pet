#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Plays an MP3 with the Windows MCI API (winmm.dll), so no audio library has to ship with the exe.
    /// <para>
    /// MCI needs a file, so the sound bytes are written once to %TEMP%\DesktopPet\sounds (named by content hash,
    /// so reloading the same pet reuses the same file and the same MCI device). Every call must happen on the
    /// UI thread, which is where the pets play their sounds.
    /// </para>
    /// </summary>
    internal sealed class Mp3Player
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern bool mciGetErrorString(int error, StringBuilder text, int length);

        private const int MM_MCINOTIFY = 0x03B9;
        private const int MCI_NOTIFY_SUCCESSFUL = 0x0001;

        /// <summary>One MCI device per sound content (hash → player).</summary>
        private static readonly Dictionary<string, Mp3Player> cache = new Dictionary<string, Mp3Player>();
        private static NotifyWindow notifyWindow;
        private static int nextId;

        private readonly string alias;
        private double volume;
        private int repeatsLeft;

        private Mp3Player(string alias) { this.alias = alias; }

        /// <summary>Open an MP3 from memory. Throws with the MCI error text if Windows can't open it.</summary>
        public static Mp3Player Load(byte[] mp3)
        {
            string hash;
            using (var sha = SHA1.Create()) hash = Convert.ToHexString(sha.ComputeHash(mp3));
            if (cache.TryGetValue(hash, out var existing)) return existing;

            string folder = Path.Combine(Path.GetTempPath(), "DesktopPet", "sounds");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, hash + ".mp3");
            if (!File.Exists(file) || new FileInfo(file).Length != mp3.Length) File.WriteAllBytes(file, mp3);

            if (notifyWindow == null)
            {
                notifyWindow = new NotifyWindow();
                Application.ApplicationExit += (s, e) => Send("close all");
            }

            var player = new Mp3Player("dpsnd" + (++nextId));
            Check(Send("open \"" + file + "\" type mpegvideo alias " + player.alias));
            cache[hash] = player;
            return player;
        }

        /// <summary>
        /// Play from the beginning. <paramref name="volume"/> goes from 0.0 to 1.0,
        /// <paramref name="repeats"/> is how many extra times the sound is played after the first one.
        /// </summary>
        public void Play(double volume, int repeats)
        {
            this.volume = volume;
            repeatsLeft = repeats;
            PlayOnce();
        }

        private void PlayOnce()
        {
            int v = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, volume)) * 1000);
            Check(Send("setaudio " + alias + " volume to " + v));
            Check(Send("play " + alias + " from 0 notify", notifyWindow.Handle));
        }

        private static int Send(string command, IntPtr callback = default)
        {
            return mciSendString(command, null, 0, callback);
        }

        private static void Check(int error)
        {
            if (error == 0) return;
            var text = new StringBuilder(256);
            throw new InvalidOperationException(mciGetErrorString(error, text, text.Capacity) ? text.ToString() : "MCI error " + error);
        }

        /// <summary>Hidden window that receives the "playback finished" notification from MCI.</summary>
        private sealed class NotifyWindow : NativeWindow
        {
            public NotifyWindow() { CreateHandle(new CreateParams()); }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == MM_MCINOTIFY && (int)m.WParam == MCI_NOTIFY_SUCCESSFUL)
                {
                    // lParam is the MCI device id: find the player that owns it.
                    foreach (var p in cache.Values)
                    {
                        if (mciGetDeviceId(p.alias) != (int)m.LParam) continue;
                        if (p.repeatsLeft-- > 0)
                        {
                            try { p.PlayOnce(); } catch (InvalidOperationException) { p.repeatsLeft = 0; }
                        }
                        break;
                    }
                    return;
                }
                base.WndProc(ref m);
            }
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetDeviceIDW")]
        private static extern int mciGetDeviceId(string device);
    }
}
#endif
