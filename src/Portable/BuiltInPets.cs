using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace DesktopPet
{
    /// <summary>
    /// Pets shipped inside the EXE (embedded resources), so they appear in the
    /// gallery instantly with no internet connection and no GitHub download.
    /// </summary>
    public static class BuiltInPets
    {
        /// <summary>Folder names of every built-in pet.</summary>
        public static readonly string[] Folders = { "claude", "capi", "mago", "nuvi" };

        /// <summary>True if a folder is a built-in (offline) pet.</summary>
        public static bool IsBuiltIn(string folder) =>
            !string.IsNullOrEmpty(folder) && Array.IndexOf(Folders, folder) >= 0;

        /// <summary>Return the animations.xml of a built-in pet as a string.</summary>
        public static string GetXml(string folder)
        {
            return folder switch
            {
                "claude" => Properties.Resources.animations_claude,
                "capi" => Properties.Resources.animations_capi,
                "mago" => Properties.Resources.animations_mago,
                "nuvi" => Properties.Resources.animations_nuvi,
                _ => throw new KeyNotFoundException("Unknown built-in pet: " + folder)
            };
        }

        /// <summary>Return the icon.png of a built-in pet as a Bitmap.</summary>
        public static Bitmap GetIcon(string folder)
        {
            return folder switch
            {
                "claude" => Properties.Resources.icon_claude,
                "capi" => Properties.Resources.icon_capi,
                "mago" => Properties.Resources.icon_mago,
                "nuvi" => Properties.Resources.icon_nuvi,
                _ => throw new KeyNotFoundException("Unknown built-in pet: " + folder)
            };
        }

        /// <summary>Return the README as a list of lines (matching OptionsGit.GetReadMe).</summary>
        public static List<string> GetReadMe(string folder)
        {
            string text = folder switch
            {
                "claude" => ClaudeReadme,
                "capi" => CapiReadme,
                "mago" => MagoReadme,
                "nuvi" => NuviReadme,
                _ => ""
            };
            return text.Split(new[] { "<br>", "<br />", "<br/>", "\r\n", "\n" },
                              StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        /// <summary>Parsed XML detail document for the detail panel.</summary>
        public static XmlDocument GetDetail(string folder)
        {
            var doc = new XmlDocument();
            doc.LoadXml(GetXml(folder));
            return doc;
        }

        const string ClaudeReadme = @"
# Claude
A little desktop companion inspired by the Claude Code mascot: a rounded
terracotta block with two dark eyes and four short legs.
Original pixel art, drawn from scratch for desktopPet.
Unofficial tribute, not affiliated with Anthropic.
";

        const string CapiReadme = @"
# Capi the Capybara
A calm, chubby pixel-art capybara who wanders your desktop,
sits on the taskbar and naps in the sun.
Original pixel art by Luan Felipe.
";

        const string MagoReadme = @"
# Mago the Wizard
A kindly old pixel wizard with a long white beard and a blue,
star-spangled robe and hat. He casts spells, reads his spellbook,
gazes into a crystal ball and teleports around your desktop.
Original pixel art by Luan Felipe.
";

        const string NuviReadme = @"
# Nuvi the Cloud Terminal
A little blue cloud robot with a terminal screen for a face.
It talks with a blinking >_ prompt, types code, shows a loading bar,
throws the odd error and naps on your taskbar.
Original pixel art by Luan Felipe.
";
    }
}
