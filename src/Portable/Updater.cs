#if NETCORE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopPet
{
    /// <summary>
    /// Updates the application from the releases of this repository (<see cref="AppInfo.LatestReleaseApi"/>).
    /// <para>
    /// A release must contain <c>DesktopPet.exe</c>, optionally <c>lang.zip</c>, and <c>SHA256SUMS</c>
    /// ("hash  file name" per line). The tag is the version ("v2.1.0"). Only a <b>higher</b> version is offered,
    /// and every file is checked against its SHA-256 before anything is replaced.
    /// Tools\release.ps1 builds such a release.
    /// </para>
    /// </summary>
    internal static class Updater
    {
#if SELFCONTAINED
        // the "full" exe (no .NET needed) updates to the full exe, never to the one that needs .NET
        private const string ExeAsset = "DesktopPet-completo.exe";
#else
        private const string ExeAsset = "DesktopPet.exe";
#endif
        private const string LangAsset = "lang.zip";
        private const string SumsAsset = "SHA256SUMS";

        private static readonly HttpClient http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopPet-Updater");
            return client;
        }

        /// <summary>Information about the latest published release.</summary>
        public sealed class Release
        {
            public Version Version;
            public string Notes = "";
            public Dictionary<string, string> Assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Version of the running application (major.minor.build).</summary>
        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        /// <summary>"v2.1.0", "2.1", "release-2.1.0" → 2.1.0. Returns null if there is no version in the text.</summary>
        public static Version ParseTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var digits = new string(tag.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
            if (!Version.TryParse(digits.Contains('.') ? digits : digits + ".0", out var v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        /// <summary>Read a release as returned by the GitHub API.</summary>
        public static Release ParseRelease(string json)
        {
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                var release = new Release
                {
                    Version = ParseTag(root.GetProperty("tag_name").GetString()),
                    Notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                };
                if (root.TryGetProperty("assets", out var assets))
                    foreach (var asset in assets.EnumerateArray())
                        release.Assets[asset.GetProperty("name").GetString()] = asset.GetProperty("browser_download_url").GetString();
                return release.Version == null ? null : release;
            }
        }

        /// <summary>Latest release of this repository, or null if nothing was published yet.</summary>
        public static async Task<Release> GetLatestAsync()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.LatestReleaseApi))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await http.SendAsync(request))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                    response.EnsureSuccessStatusCode();
                    return ParseRelease(await response.Content.ReadAsStringAsync());
                }
            }
        }

        /// <summary>Parse a SHA256SUMS file ("hash  name" or "hash *name" per line).</summary>
        public static Dictionary<string, string> ParseSums(string text)
        {
            var sums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length < 66) continue;
                var hash = line.Substring(0, 64);
                var name = line.Substring(64).Trim().TrimStart('*');
                if (hash.All(Uri.IsHexDigit) && name.Length > 0) sums[name] = hash.ToLowerInvariant();
            }
            return sums;
        }

        /// <summary>SHA-256 of a byte array as lowercase hex.</summary>
        public static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        /// <summary>
        /// Check for an update and, if the user agrees, install it and restart.
        /// Errors are shown only when <paramref name="silent"/> is false.
        /// </summary>
        public static async Task CheckAndInstallAsync(IWin32Window owner, bool silent)
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            string appDir = Path.GetDirectoryName(exePath);
            string oldExe = Path.Combine(appDir, "DesktopPet_old.exe");
            try { if (File.Exists(oldExe)) File.Delete(oldExe); } catch (Exception) { }   // leftover of the previous update

            try
            {
                var release = await GetLatestAsync();
                if (release == null || release.Version <= CurrentVersion) return;
                if (!release.Assets.ContainsKey(ExeAsset) || !release.Assets.ContainsKey(SumsAsset))
                    throw new InvalidOperationException(Lang.T("inst.incompleteRelease", release.Version));

                string notes = release.Notes.Length > 1500 ? release.Notes.Substring(0, 1500) + "…" : release.Notes;
                if (MessageBox.Show(owner, Lang.T("inst.newVersion", release.Version, notes),
                        Lang.T("inst.versionTitle", "DesktopPet", CurrentVersion),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                // download everything and check every hash first: nothing is touched if one file is wrong
                var sums = ParseSums(await http.GetStringAsync(release.Assets[SumsAsset]));
                byte[] exe = await DownloadVerifiedAsync(release, ExeAsset, sums);
                byte[] lang = release.Assets.ContainsKey(LangAsset) ? await DownloadVerifiedAsync(release, LangAsset, sums) : null;

                // a running exe can't be overwritten, but it can be renamed
                File.Move(exePath, oldExe, true);
                try
                {
                    File.WriteAllBytes(exePath, exe);
                }
                catch
                {
                    File.Move(oldExe, exePath, true);
                    throw;
                }

                if (lang != null)
                {
                    try
                    {
                        using (var zip = new ZipArchive(new MemoryStream(lang)))
                            zip.ExtractToDirectory(Path.Combine(appDir, "lang"), true);
                    }
                    catch (Exception) { }   // the new exe still works with its built-in English
                }

                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                MessageBox.Show(owner, Lang.T("inst.updated"));
                Application.Exit();
            }
            catch (Exception ex)
            {
                StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "Update failed: " + ex.Message);
                if (!silent) MessageBox.Show(owner, Lang.T("inst.updateFailed", ex.Message));
            }
        }

        private static async Task<byte[]> DownloadVerifiedAsync(Release release, string name, Dictionary<string, string> sums)
        {
            if (!sums.TryGetValue(name, out var expected))
                throw new InvalidOperationException(Lang.T("inst.hashMissing", name));
            byte[] data = await http.GetByteArrayAsync(release.Assets[name]);
            if (Sha256(data) != expected)
                throw new InvalidOperationException(Lang.T("inst.hashMismatch", name));
            return data;
        }
    }
}
#endif
