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
	/// <summary>Pets page: gallery list, download, adopt, remove and details loading.</summary>
	public sealed partial class FormSettings
	{
		// ------------------------------------------------------------------ pets (download)

		/// <summary>
		/// Pets that only differ in color are shown as one card with a color picker.
		/// (folder, Lang key of the color name, swatch color), in display order.
		/// </summary>
		private static readonly (string name, (string folder, string colorKey, Color swatch)[] variants)[] PetGroups =
		{
			("gSheep", new[]
			{
				("blue_sheep",   "color.blue",   Color.FromArgb(79, 134, 232)),
				("green_sheep",  "color.green",  Color.FromArgb(93, 187, 99)),
				("yellow_sheep", "color.yellow", Color.FromArgb(242, 201, 76)),
				("orange_sheep", "color.orange", Color.FromArgb(240, 146, 58)),
				("red_sheep",    "color.red",    Color.FromArgb(224, 82, 77)),
				("pink_sheep",   "color.pink",   Color.FromArgb(242, 122, 181)),
				("purple_sheep", "color.purple", Color.FromArgb(155, 109, 214)),
			}),
		};

		// ------------------------------------------------------------------ "New" badge

		/// <summary>Pets already seen (folder → lastupdate). Null until the gallery was loaded once.</summary>
		private Dictionary<string, string> seenPets = Program.MyData.GetSeenPets();

		private IEnumerable<OptionsPet> RealPets(OptionsPet pet) =>
			pet.Variants != null ? pet.Variants.Where(v => v != null) : new[] { pet };

		/// <summary>A pet is new if it was never opened/adopted, or was updated since then.</summary>
		private bool IsNewPet(OptionsPet pet)
		{
			if (seenPets == null) return false;
			return RealPets(pet).Any(p => !seenPets.TryGetValue(p.folder ?? "", out string d) || d != (p.lastupdate ?? ""));
		}

		/// <summary>Remember that the user has seen this pet (and all its colors).</summary>
		private void MarkSeen(OptionsPet pet)
		{
			if (seenPets == null || pet == null || !IsNewPet(pet)) return;
			foreach (var p in RealPets(pet)) seenPets[p.folder ?? ""] = p.lastupdate ?? "";
			Program.MyData.SetSeenPets(seenPets);
		}

		/// <summary>First time the gallery is loaded: everything that exists now counts as already seen.</summary>
		private void InitSeenPets()
		{
			if (seenPets != null) return;
			seenPets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var pet in WebPets.pets)
				foreach (var p in RealPets(pet)) seenPets[p.folder ?? ""] = p.lastupdate ?? "";
			Program.MyData.SetSeenPets(seenPets);
		}

		/// <summary>Put a downloaded pet into its color group card. Returns false if it belongs to no group.</summary>
		private bool AddToGroup(OptionsPet pet)
		{
			foreach (var group in PetGroups)
			{
				int index = Array.FindIndex(group.variants, v => v.folder == pet.folder);
				if (index < 0) continue;

				var card = WebPets.pets.FirstOrDefault(p => p.GroupName == group.name);
				if (card == null)
				{
					card = new OptionsPet
					{
						GroupName = group.name,
						Variants = new OptionsPet[group.variants.Length],
						VariantNames = group.variants.Select(v => v.colorKey).ToArray(),
						VariantColors = group.variants.Select(v => v.swatch).ToArray(),
					};
					WebPets.pets.Add(card);
				}
				card.Variants[index] = pet;
				// show the color in use; otherwise the first color of the list that is loaded
				int shown = card.SelectedVariant;
				bool inUse = pet.folder == petsView.CurrentPet;
				bool shownInUse = shown >= 0 && card.Variants[shown].folder == petsView.CurrentPet;
				if (shown < 0 || inUse || (!shownInUse && index < shown)) card.SelectVariant(pet);
				return true;
			}
			return false;
		}

		private bool loadingPets;

		/// <summary>Fetch the gallery again (the "Try again" of the no-internet notice).</summary>
		public async void ReloadPets()
		{
			if (loadingPets) return;
			petsView.CloseDetail();
			WebPets.pets.Clear();
			petsView.Offline = false;
			petsView.Loading = true;
			petsView.PetsChanged();
			await LoadPetsAsync();
		}

		private async Task LoadPetsAsync()
		{
			if (loadingPets) return;
			loadingPets = true;
			try { await LoadPetsCore(); }
			finally { loadingPets = false; }
		}

		private async Task LoadPetsCore()
		{
			// --- built-in pets (embedded, always available offline) ----------------
			foreach (var folder in BuiltInPets.Folders)
			{
				var detail = BuiltInPets.GetDetail(folder);
				var builtIn = new OptionsPet
				{
					folder = folder,
					name = BuiltInPets.GetName(folder),
					author = detail.DocumentElement?.SelectSingleNode("*[local-name()='header']/*[local-name()='author']")?.InnerText,
					lastupdate = "2026-11-11",
					Image = BuiltInPets.GetIcon(folder),
					Readme = BuiltInPets.GetReadMe(folder),
					ReadmeLang = Lang.Current,
					Detail = detail,
				};
				WebPets.pets.Add(builtIn);
			}
			// The pet in use was adopted before downloads were kept on disk: keep a copy, so it stays "downloaded".
			string current = Program.MyData.GetCurrentPetFolder();
			if (!string.IsNullOrEmpty(current) && !BuiltInPets.IsBuiltIn(current) && !PetStore.Has(current))
			{
				try { PetStore.Save(current, Program.MyData.GetXml(), ""); } catch (Exception) { }
			}
			// --- pets created on this computer ("Criar meu pet")
			foreach (var folder in PetStore.Folders())
				if (folder.StartsWith(PetCreator.Prefix, StringComparison.OrdinalIgnoreCase)) AddLocalPet(folder);
			petsView.PetsChanged();

			// --- online gallery (GitHub) -------------------------------------------
			StatusText = Lang.T("status.loadingList");
			try
			{
				var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var progress = new Progress<OptionsPet>(pet =>
				{
					listed.Add(pet.folder);
					// without internet only the pets already on this computer are shown: nothing to download
					if (!OptionsGit.Online && !PetStore.Has(pet.folder)) return;
					// skip if the folder was already added as a built-in
					if (WebPets.pets.Any(p => p.folder == pet.folder && p.GroupName == null)) return;
					if (!AddToGroup(pet)) WebPets.pets.Add(pet);
					petsView.PetsChanged();
					StatusText = Lang.T("status.loadingListCount", WebPets.pets.Count);
				});
				await Task.Run(() => OptionsGit.GetPetList(progress));
				await Task.Yield();   // let the last progress reports arrive
				petsView.Offline = !OptionsGit.Online;
				if (OptionsGit.Online) ForgetRemovedPets(listed);
				// offline, the pet in use is always shown, even if the saved list does not have it
				string inUse = petsView.CurrentPet;
				if (!OptionsGit.Online && !string.IsNullOrEmpty(inUse) && !listed.Contains(inUse) && PetStore.Has(inUse))
					AddLocalPet(inUse);
				StatusText = OptionsGit.Online ? Lang.T("status.available", WebPets.pets.Count) : Lang.T("status.offline");
				if (OptionsGit.Online) InitSeenPets();
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.listError", ex.Message);
			}
			petsView.Loading = false;
			petsView.PetsChanged();
		}

		/// <summary>
		/// Pets taken out of the gallery (no longer in pets.json) leave the panel and the disk.
		/// The one in use stays, so it can still be seen and replaced; it goes once another pet is adopted.
		/// </summary>
		private void ForgetRemovedPets(HashSet<string> listed)
		{
			foreach (var folder in PetStore.Folders())
			{
				if (listed.Contains(folder) || BuiltInPets.IsBuiltIn(folder)) continue;
				if (folder.StartsWith(PetCreator.Prefix, StringComparison.OrdinalIgnoreCase)) continue;   // made on this computer
				if (string.Equals(folder, petsView.CurrentPet, StringComparison.OrdinalIgnoreCase)) { AddLocalPet(folder); continue; }
				try { PetStore.Delete(folder); } catch (Exception) { }
			}
		}

		/// <summary>A card for a pet that is on disk but not in the gallery list (name and icon from its own file).</summary>
		private void AddLocalPet(string folder)
		{
			if (WebPets.pets.Any(p => string.Equals(p.folder, folder, StringComparison.OrdinalIgnoreCase))) return;
			try
			{
				var doc = new XmlDocument();
				doc.LoadXml(PetStore.Load(folder));
				var header = doc.DocumentElement?.SelectSingleNode("*[local-name()='header']");
				var pet = new OptionsPet
				{
					folder = folder,
					name = header?.SelectSingleNode("*[local-name()='petname']")?.InnerText ?? folder,
					author = header?.SelectSingleNode("*[local-name()='author']")?.InnerText ?? "",
					lastupdate = PetStore.SavedVersion(folder),
					Detail = doc,
					Readme = new List<string> { Lang.T(folder.StartsWith(PetCreator.Prefix, StringComparison.OrdinalIgnoreCase) ? "pets.createdHere" : "pets.notInGallery") },
				};
				try
				{
					string icon = header?.SelectSingleNode("*[local-name()='icon']")?.InnerText;
					string png = System.IO.Path.Combine(OptionsGit.BaseLocalPath, folder + ".png");
					if (System.IO.File.Exists(png)) using (var img = Image.FromFile(png)) pet.Image = new Bitmap(img);
					else if (!string.IsNullOrWhiteSpace(icon))
						using (var ms = new System.IO.MemoryStream(Convert.FromBase64String(icon.Trim())))
						using (var ico = new Icon(ms, 64, 64))
							pet.Image = ico.ToBitmap();
				}
				catch (Exception) { }
				WebPets.pets.Add(pet);
			}
			catch (Exception) { }
		}

		/// <summary>The eSheep comes with the program; any other pet is available once downloaded.</summary>
		public bool IsDownloaded(OptionsPet pet) => pet != null && (BuiltInPets.IsBuiltIn(pet.folder) || PetStore.Has(pet.folder));

		/// <summary>Downloaded, but the gallery has a newer version.</summary>
		public bool IsOutdated(OptionsPet pet)
		{
			if (pet == null || BuiltInPets.IsBuiltIn(pet.folder) || !PetStore.Has(pet.folder)) return false;
			string saved = PetStore.SavedVersion(pet.folder);
			return saved.Length > 0 && !string.IsNullOrEmpty(pet.lastupdate) && saved != pet.lastupdate;
		}

		/// <summary>What the main button of a pet does: add another, download (or update), or adopt.</summary>
		public void PrimaryAction(OptionsPet pet)
		{
			if (pet == null) return;
			if (pet.folder == petsView.CurrentPet && !IsOutdated(pet)) Program.Mainthread.AddSheep();
			else if (!IsDownloaded(pet) || IsOutdated(pet)) DownloadPet(pet);
			else AdoptPet(pet);
		}

		/// <summary>Download a pet from the gallery and keep it on disk (it is not adopted yet).</summary>
		public async void DownloadPet(OptionsPet pet)
		{
			if (pet == null || BuiltInPets.IsBuiltIn(pet.folder) || petsView.DownloadingFolder != null) return;
			StatusText = Lang.T("status.downloading", pet.DisplayName);
			petsView.DownloadingFolder = pet.folder;
			petsView.FailedFolder = null;
			petsView.DownloadProgress = 0;
			petsView.PetsChanged();
			downloadCancel = new System.Threading.CancellationTokenSource();
			try
			{
				// the button fills up while the file arrives (reports come back on the UI thread)
				var progress = new Progress<double>(v => { petsView.DownloadProgress = v; petsView.PetsChanged(); });
				string content = await OptionsGit.DownloadPetXml(pet.folder, pet.size, progress, downloadCancel.Token);
				var xml = new XmlDocument();
				xml.LoadXml(content);                       // refuse a broken file before saving it
				if (xml.DocumentElement?.SelectSingleNode("*[local-name()='image']") == null
					|| xml.DocumentElement.SelectSingleNode("*[local-name()='animations']") == null)
					throw new System.IO.InvalidDataException(Lang.T("pets.errNotPet"));
				PetStore.Save(pet.folder, xml.OuterXml, pet.lastupdate ?? "");
				pet.Detail = xml;                           // real counts for the detail panel
				MarkSeen(pet);
				StatusText = Lang.T("status.downloaded", pet.DisplayName);
				PetUpdates.Recount();
				petsView.DownloadingFolder = null;
				// the pet in use was updated: reload it right away
				if (pet.folder == petsView.CurrentPet) AdoptPet(pet);
			}
			catch (OperationCanceledException) when (downloadCancel.IsCancellationRequested)
			{
				StatusText = Lang.T("status.downloadCanceled", pet.DisplayName);
			}
			catch (Exception ex)
			{
				// the button becomes "Try again"
				petsView.FailedFolder = pet.folder;
				StatusText = Lang.T("status.downloadError", pet.DisplayName, ex.Message);
			}
			downloadCancel.Dispose();
			downloadCancel = null;
			petsView.DownloadingFolder = null;
			petsView.PetsChanged();
		}

		private System.Threading.CancellationTokenSource downloadCancel;

		/// <summary>Stop the download in progress (the ✕ next to the progress bar).</summary>
		public void CancelDownload() => downloadCancel?.Cancel();

		/// <summary>Save every downloaded pet into one file (to take them to another computer).</summary>
		private void ExportPets()
		{
			if (PetStore.Folders().Count == 0) { StatusText = Lang.T("status.exportNone"); return; }
			using (var dlg = new SaveFileDialog
			{
				Filter = Lang.T("dlg.petsFile") + " (*" + PetBackup.Extension + ")|*" + PetBackup.Extension,
				FileName = Lang.T("dlg.petsFileName") + PetBackup.Extension,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			})
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				try
				{
					int n = PetBackup.Export(dlg.FileName);
					StatusText = Lang.T("status.exported", n, System.IO.Path.GetFileName(dlg.FileName));
				}
				catch (Exception ex) { StatusText = Lang.T("status.exportError", ex.Message); }
			}
		}

		/// <summary>"Criar meu pet": pictures become a pet, kept in "Meus pets" and offered for adoption.</summary>
		private void CreatePet()
		{
			using (var f = new FormPetCreator())
			{
				if (f.ShowDialog(this) != DialogResult.OK || f.Created == null) return;
				WebPets.pets.RemoveAll(p => string.Equals(p.folder, f.Created, StringComparison.OrdinalIgnoreCase));
				AddLocalPet(f.Created);
				petsView.CurrentTab = PetsView.Tab.Mine;
				petsView.PetsChanged();
				var pet = WebPets.pets.FirstOrDefault(p => p.folder == f.Created);
				StatusText = Lang.T("status.created", pet?.DisplayName ?? f.Created);
				if (pet != null && MessageBox.Show(this, Lang.T("dlg.createdAdopt", pet.DisplayName), Lang.T("create.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					AdoptPet(pet);
			}
		}

		/// <summary>Bring the pets of an exported file to this computer.</summary>
		private void ImportPets()
		{
			using (var dlg = new OpenFileDialog
			{
				Filter = Lang.T("dlg.petsFile") + " (*" + PetBackup.Extension + ")|*" + PetBackup.Extension,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			})
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				try
				{
					var (imported, current) = PetBackup.Import(dlg.FileName);
					StatusText = imported.Count == 0 ? Lang.T("status.importNone") : Lang.T("status.imported", imported.Count);
					// reminders, shortcuts, mood, needs and settings (they replace the ones of this computer: ask)
					if (PetBackup.HasMyThings(dlg.FileName) &&
						MessageBox.Show(this, Lang.T("dlg.importMyThings"), Lang.T("opt.import.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
					{
						int n = PetBackup.RestoreMyThings(dlg.FileName);
						Theme.Mode = Program.MyData.GetTheme();
						Lang.Use(Program.MyData.GetLanguage());
						PcMonitor.Apply(); ComputerEvents.Apply(); HideHotkey.Apply();
						ContextMenus.UpdateTexts();
						StatusText = Lang.T("status.restoredMyThings", imported.Count, n);
					}
					PetUpdates.Recount();
					// show them in "My pets"
					foreach (var pet in WebPets.pets) pet.Detail = null;
					petsView.CurrentTab = PetsView.Tab.Mine;
					ReloadPets();
					// the pet that was in use on the other computer: offer it
					if (imported.Contains(current) && current != petsView.CurrentPet &&
						MessageBox.Show(this, Lang.T("dlg.importAdopt", current), Lang.T("opt.import.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
						AdoptPet(new OptionsPet { folder = current, name = current });
				}
				catch (Exception ex) { StatusText = Lang.T("status.importError", ex.Message); }
			}
		}

		/// <summary>Delete a downloaded pet from the computer (not the one in use).</summary>
		public void RemovePet(OptionsPet pet)
		{
			if (pet == null || BuiltInPets.IsBuiltIn(pet.folder) || pet.folder == petsView.CurrentPet || !PetStore.Has(pet.folder)) return;
			var answer = MessageBox.Show(this, Lang.T("pets.removeConfirm", pet.DisplayName), Lang.T("pets.remove"),
				MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
			if (answer != DialogResult.Yes) return;
			try
			{
				PetStore.Delete(pet.folder);
				pet.Detail = null;   // the counts come from the gallery again
				if (!OptionsGit.Online && !BuiltInPets.IsBuiltIn(pet.folder))
				{
					// offline the gallery shows only what is on disk: the card goes away
					petsView.CloseDetail();
					WebPets.pets.Remove(pet);
				}
				StatusText = Lang.T("status.removed", pet.DisplayName);
				PetUpdates.Recount();
			}
			catch (Exception ex) { StatusText = Lang.T("status.removeError", pet.DisplayName, ex.Message); }
			petsView.PetsChanged();
		}

		/// <summary>Set a pet (built in or already downloaded) as the current one.</summary>
		public async void AdoptPet(OptionsPet pet)
		{
			if (pet == null) return;
			try
			{
				string content;
				if (BuiltInPets.IsBuiltIn(pet.folder))
				{
					content = BuiltInPets.GetXml(pet.folder);
				}
				else if (PetStore.Has(pet.folder))
				{
					content = PetStore.Load(pet.folder);
				}
				else
				{
					StatusText = Lang.T("status.downloading", pet.DisplayName);
					content = await Task.Run(() => OptionsGit.GetPetXml(pet.folder));
					PetStore.Save(pet.folder, content, pet.lastupdate ?? "");
				}
				var xml = new XmlDocument();
				xml.LoadXml(content);
				// Clear cached images/icon so ReadXML re-reads them from the new XML,
				// not from the stale data left by the previous pet.
				Program.MyData.SetImages("");
				Program.MyData.SetIcon("");
				Program.MyData.SetXml(xml.OuterXml, "");
				Program.Mainthread.LoadNewXMLFromString(xml.OuterXml);
				StatusText = Lang.T("status.enjoy", pet.DisplayName);
				petsView.CurrentPet = pet.folder;
				MarkSeen(pet);
				// Persist the adopted pet folder so the gallery badge survives restart.
				Program.MyData.SetCurrentPetFolder(pet.folder);
				ContextMenus.UpdateTexts();   // "Sounds of <pet>" follows the new pet
				// the new pet says hello once it is on screen
				var hello = new Timer { Interval = 1800 };
				hello.Tick += (s, e) => { hello.Stop(); hello.Dispose(); SpeechBubble.Say(Lang.T("bubble.hello", pet.DisplayName), 6); };
				hello.Start();
			}
			catch (Exception ex)
			{
				StatusText = Lang.T("status.downloadError", pet.DisplayName, ex.Message);
			}
		}

		/// <summary>Load README and XML details for the detail panel.</summary>
		public async Task LoadDetailsAsync(OptionsPet pet)
		{
			// group card: load into the selected variant (its own object), then show it if still selected
			if (pet.Variants != null)
			{
				int i = pet.SelectedVariant;
				if (i < 0) return;
				var variant = pet.Variants[i];
				await LoadDetailsAsync(variant);
				if (pet.folder == variant.folder) pet.SelectVariant(variant);
				return;
			}
			try
			{
				// (again after a language change: the Portuguese README is used in Portuguese)
				if (pet.Readme.Count == 0 || (pet.ReadmeLang != null && pet.ReadmeLang != Lang.Current))
				{
					string lang = Lang.Current;
					if (BuiltInPets.IsBuiltIn(pet.folder))
						pet.Readme = BuiltInPets.GetReadMe(pet.folder);
					else
						pet.Readme = await Task.Run(() => OptionsGit.GetReadMe(pet.folder));
					pet.ReadmeLang = lang;
				}
				// the pet moving, for the detail (also before downloading)
				if (!pet.PreviewTried)
				{
					pet.PreviewTried = true;
					string folder = pet.folder;
					pet.Preview = await Task.Run(() => OptionsGit.GetPreview(folder));
				}
			}
			catch (Exception ex) { StatusText = "README: " + ex.Message; }
			try
			{
				// Counts come from the file only when it is already here (built in or downloaded):
				// opening the details must not download the whole pet. Otherwise pets.json has them.
				if (pet.Detail == null)
				{
					if (BuiltInPets.IsBuiltIn(pet.folder))
					{
						pet.Detail = BuiltInPets.GetDetail(pet.folder);
					}
					else if (PetStore.Has(pet.folder))
					{
						var doc = new XmlDocument();
						doc.LoadXml(PetStore.Load(pet.folder));
						pet.Detail = doc;
					}
				}
				// size for the download button: pets.json, or ask the server (no download)
				if (pet.size <= 0 && !IsDownloaded(pet))
					pet.size = await OptionsGit.GetPetSize(pet.folder);
			}
			catch (Exception ex) { StatusText = "Details: " + ex.Message; }
			petsView.Invalidate();
		}

	}
}
