using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace DesktopPet
{
    /// <summary>
    /// Pets shipped inside the EXE, available instantly and offline.
    /// <para>
    /// Only the default eSheep is embedded (it is also the fallback when a download or an XML fails).
    /// Every other pet — including Claude, Capi, Mago, Nuvi, Boo and Tico — is downloaded from the
    /// gallery (Pets folder of the repository, see <see cref="AppInfo"/>), which keeps the exe small and
    /// lets a pet be updated without releasing a new exe.
    /// </para>
    /// </summary>
    public static class BuiltInPets
    {
        /// <summary>Folder names of every built-in pet.</summary>
        public static readonly string[] Folders = { "esheep64" };

        /// <summary>True if a folder is a built-in (offline) pet.</summary>
        public static bool IsBuiltIn(string folder) =>
            !string.IsNullOrEmpty(folder) && Array.IndexOf(Folders, folder) >= 0;

        /// <summary>Name shown in the gallery.</summary>
        public static string GetName(string folder) => folder == "esheep64" ? "eSheep" : folder;

        /// <summary>Return the animations.xml of a built-in pet as a string.</summary>
        public static string GetXml(string folder)
        {
            if (folder == "esheep64") return Properties.Resources.animations;
            throw new KeyNotFoundException("Unknown built-in pet: " + folder);
        }

        /// <summary>Return the icon of a built-in pet as a Bitmap.</summary>
        public static Bitmap GetIcon(string folder)
        {
            if (folder == "esheep64") return Properties.Resources.esheep;
            throw new KeyNotFoundException("Unknown built-in pet: " + folder);
        }

        /// <summary>Return the README as a list of lines (matching OptionsGit.GetReadMe).</summary>
        public static List<string> GetReadMe(string folder)
        {
            string text = folder != "esheep64" ? ""
                : Lang.Current.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? ESheepReadmePt : ESheepReadme;
            return text.Split(new[] { "<br>", "<br />", "<br/>", "\r\n", "\n" },
                              StringSplitOptions.None).ToList();   // keep blank lines: they separate paragraphs
        }

        /// <summary>Parsed XML detail document for the detail panel.</summary>
        public static XmlDocument GetDetail(string folder)
        {
            var doc = new XmlDocument();
            doc.LoadXml(GetXml(folder));
            return doc;
        }

        const string ESheepReadme = @"
# eSheep
The default pet of the program: a little sheep that walks over your desktop
and your windows, falls on the taskbar, sleeps and runs around.
It is included in the program and always works, even without internet.
";

        const string ESheepReadmePt = @"
# eSheep
O pet padrão do programa: uma ovelhinha que anda pela sua área de trabalho
e pelas janelas, cai na barra de tarefas, dorme e corre por aí.
Ela vem junto com o programa e funciona sempre, mesmo sem internet.
";
    }
}
