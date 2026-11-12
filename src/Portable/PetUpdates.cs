using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Looks in the background (a little after the start, then every 6 hours) whether a downloaded pet has a newer
    /// version in the gallery. If so, the tray menu says "Open Panel (n updates)" and a notification appears once
    /// for each new version; clicking it opens the panel.
    /// </summary>
    internal static class PetUpdates
    {
        static Timer timer;
        static List<OptionsPet> gallery = new List<OptionsPet>();

        /// <summary>Downloaded pets that have a newer version in the gallery (names).</summary>
        public static List<string> Outdated { get; private set; } = new List<string>();

        /// <summary>The list of outdated pets changed (tray menu follows it).</summary>
        public static event Action Changed;

        public static void Start()
        {
            if (timer != null) return;
            timer = new Timer { Interval = 15000 };   // first check 15 s after the start
            timer.Tick += async (s, e) =>
            {
                timer.Interval = 6 * 60 * 60 * 1000;
                await CheckAsync(notify: true);
            };
            timer.Start();
        }

        /// <summary>Fetch the gallery list and compare it with the pets on this computer.</summary>
        public static async Task CheckAsync(bool notify)
        {
            try
            {
                string json;
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "DesktopPet");
                    json = await client.GetStringAsync(OptionsGit.BaseGitUrl + "pets.json");
                }
                gallery = JsonText.Deserialize<OptionsPets>(json).pets ?? new List<OptionsPet>();
            }
            catch (Exception) { return; }   // offline: try again later
            Recount(notify);
        }

        /// <summary>Compare again with what is on disk (after a download or a removal).</summary>
        public static void Recount(bool notify = false)
        {
            var outdated = gallery.Where(p => PetStore.Has(p.folder))
                .Where(p => { string v = PetStore.SavedVersion(p.folder); return v.Length > 0 && !string.IsNullOrEmpty(p.lastupdate) && v != p.lastupdate; })
                .ToList();
            Outdated = outdated.Select(p => string.IsNullOrEmpty(p.name) ? p.folder : p.name).ToList();
            Changed?.Invoke();
            if (!notify || outdated.Count == 0) return;

            // one notification per new version, not at every start
            var told = Program.MyData.GetNotifiedUpdates();
            var fresh = outdated.Where(p => !told.TryGetValue(p.folder, out var v) || v != p.lastupdate).ToList();
            if (fresh.Count == 0) return;
            foreach (var p in fresh) told[p.folder] = p.lastupdate;
            Program.MyData.SetNotifiedUpdates(told);
            string names = string.Join(", ", outdated.Select(p => string.IsNullOrEmpty(p.name) ? p.folder : p.name));
            ProcessIcon.Current?.Notify(Lang.T(outdated.Count == 1 ? "update.petTitleOne" : "update.petTitleMany", outdated.Count),
                Lang.T("update.petText", names), () => FormSettings.ShowPage(FormSettings.Page.Pets));
        }
    }
}
