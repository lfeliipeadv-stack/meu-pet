using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace DesktopPet
{
    /// <summary>
    /// "My pets" in one file, to take them to another computer: a zip with
    /// pets/&lt;folder&gt;/animations.xml and version.txt for every downloaded pet,
    /// plus the pet in use and the pets with sound off (settings.txt).
    /// </summary>
    internal static class PetBackup
    {
        public const string Extension = ".desktoppet";

        /// <summary>Write every downloaded pet into <paramref name="file"/>. Returns how many pets were saved.</summary>
        public static int Export(string file)
        {
            var folders = PetStore.Folders();
            string tmp = file + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                foreach (var folder in folders)
                {
                    Add(zip, "pets/" + folder + "/animations.xml", PetStore.Load(folder));
                    Add(zip, "pets/" + folder + "/version.txt", PetStore.SavedVersion(folder));
                }
                var settings = new StringBuilder();
                settings.AppendLine("current=" + Program.MyData.GetCurrentPetFolder());
                settings.AppendLine("muted=" + string.Join(";", new[] { "esheep64" }.Concat(folders).Where(f => Program.MyData.IsPetMuted(f))));
                Add(zip, "settings.txt", settings.ToString());
            }
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
            return folders.Count;
        }

        static void Add(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(entry.Open(), new UTF8Encoding(false))) w.Write(text ?? "");
        }

        /// <summary>
        /// Read a file made by <see cref="Export"/>: every valid pet goes to this computer (a pet already here is
        /// replaced). Returns the pets imported and the folder of the pet that was in use on the other computer.
        /// </summary>
        public static (List<string> imported, string current) Import(string file)
        {
            var imported = new List<string>();
            string current = "";
            using (var zip = ZipFile.OpenRead(file))
            {
                foreach (var entry in zip.Entries)
                {
                    // only "pets/<plain name>/animations.xml": nothing else is ever written to the disk
                    var parts = entry.FullName.Replace('\\', '/').Split('/');
                    if (parts.Length != 3 || parts[0] != "pets" || parts[2] != "animations.xml") continue;
                    string folder = parts[1];
                    if (folder.Length == 0 || folder.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-'))) continue;
                    if (entry.Length > 50 * 1024 * 1024) continue;
                    string xml = Read(entry);
                    try
                    {
                        var doc = new XmlDocument();
                        doc.LoadXml(xml);
                        if (doc.DocumentElement?.SelectSingleNode("*[local-name()='image']") == null) continue;
                    }
                    catch (Exception) { continue; }   // not a pet: skipped
                    var ver = zip.GetEntry("pets/" + folder + "/version.txt");
                    PetStore.Save(folder, xml, ver != null ? Read(ver).Trim() : "");
                    imported.Add(folder);
                }
                var settings = zip.GetEntry("settings.txt");
                if (settings != null)
                {
                    foreach (var line in Read(settings).Split('\n').Select(l => l.Trim()))
                    {
                        if (line.StartsWith("current=")) current = line.Substring(8);
                        if (line.StartsWith("muted="))
                            foreach (var f in line.Substring(6).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                if (f == "esheep64" || imported.Contains(f)) Program.MyData.SetPetMuted(f, true);
                    }
                }
            }
            return (imported, current);
        }

        static string Read(ZipArchiveEntry entry)
        {
            using (var r = new StreamReader(entry.Open(), Encoding.UTF8, true)) return r.ReadToEnd();
        }
    }
}
