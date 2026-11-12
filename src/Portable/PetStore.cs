using DesktopPet.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static DesktopPet.LocalData;

namespace DesktopPet
{
	/// <summary>
	/// Pets downloaded from the gallery, kept on disk so they can be adopted later (also offline):
	/// %LocalAppData%\DesktopPet\pets\<folder>\animations.xml (+ version.txt with the gallery's lastupdate).
	/// </summary>
	internal static class PetStore
	{
		static string Root => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "pets");

		static string Dir(string folder)
		{
			// a folder name from the gallery is a plain name; never let it point outside the store
			string safe = string.Concat((folder ?? "").Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
			if (safe.Length == 0) throw new ArgumentException("Invalid pet folder");
			return System.IO.Path.Combine(Root, safe);
		}

		public static bool Has(string folder)
		{
			try { return System.IO.File.Exists(System.IO.Path.Combine(Dir(folder), "animations.xml")); }
			catch (Exception) { return false; }
		}

		public static string Load(string folder) => System.IO.File.ReadAllText(System.IO.Path.Combine(Dir(folder), "animations.xml"), System.Text.Encoding.UTF8);

		public static string SavedVersion(string folder)
		{
			try
			{
				string p = System.IO.Path.Combine(Dir(folder), "version.txt");
				return System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p).Trim() : "";
			}
			catch (Exception) { return ""; }
		}

		/// <summary>Folders of every pet kept on disk.</summary>
		public static List<string> Folders()
		{
			try
			{
				if (!System.IO.Directory.Exists(Root)) return new List<string>();
				return System.IO.Directory.GetDirectories(Root).Select(d => System.IO.Path.GetFileName(d))
					.Where(f => Has(f)).ToList();
			}
			catch (Exception) { return new List<string>(); }
		}

		/// <summary>Remove a downloaded pet from the disk (it can be downloaded again).</summary>
		public static void Delete(string folder)
		{
			string dir = Dir(folder);
			if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
		}

		public static void Save(string folder, string xml, string version)
		{
			string dir = Dir(folder);
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "animations.xml"), xml, new System.Text.UTF8Encoding(false));
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "version.txt"), version ?? "");
		}
	}
}
