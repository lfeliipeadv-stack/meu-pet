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
	/// <summary>Developer snapshots of every page (--snapshot).</summary>
	public sealed partial class FormSettings
	{
		/// <summary>
		/// Developer helper: render every page to PNG files (offscreen, nothing is shown on the desktop).
		/// Usage: DesktopPet.exe --snapshot=C:\folder
		/// </summary>
		public static async void SaveSnapshots(string folder)
		{
			var f = new FormSettings(Page.Pets) { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false };
			f.Show();
			for (int i = 0; i < 40 && f.WebPets.pets.Count < 12; i++) await Task.Delay(250);
			await Task.Delay(500);
			void save(string name)
			{
				f.Refresh();
				using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
				{
					f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.ClientSize));
					bmp.Save(System.IO.Path.Combine(folder, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
				}
			}
			save("pets");
			var keepTab = f.petsView.CurrentTab;
			f.petsView.CurrentTab = f.petsView.CurrentTab == PetsView.Tab.Mine ? PetsView.Tab.Download : PetsView.Tab.Mine;
			f.petsView.Invalidate(); save("pets-tab2");
			f.petsView.CurrentTab = keepTab;
			var first = f.WebPets.pets.FirstOrDefault(p => p.folder == "esheep64") ?? f.WebPets.pets.FirstOrDefault();
			if (first != null) { f.petsView.SelectForSnapshot(first); await f.LoadDetailsAsync(first); await Task.Delay(300); save("pets-detail"); }
			// a gallery pet that is not downloaded yet: the button must offer the download
			var notDownloaded = f.WebPets.pets.FirstOrDefault(p => p.Variants == null && !f.IsDownloaded(p));
			if (notDownloaded != null)
			{
				f.petsView.SelectForSnapshot(notDownloaded); await f.LoadDetailsAsync(notDownloaded); await Task.Delay(300); save("pets-detail-download");
				// the same button while the file arrives
				f.petsView.DownloadingFolder = notDownloaded.folder; f.petsView.DownloadProgress = 0.42; save("pets-detail-progress");
				f.petsView.DownloadingFolder = null;
			}
			// the detail of every pet: the description must stay inside the panel
			string all = System.IO.Path.Combine(folder, "details");
			System.IO.Directory.CreateDirectory(all);
			foreach (var pet in f.WebPets.pets.Where(p => p.Variants == null).ToList())
			{
				f.petsView.SelectForSnapshot(pet); await f.LoadDetailsAsync(pet); await Task.Delay(100);
				save(System.IO.Path.Combine("details", pet.folder));
			}
			f.petsView.CloseDetail();
			foreach (Page p in new[] { Page.Behaviour, Page.Application, Page.About, Page.Reminders, Page.Extras }) { f.SelectPage(p); await Task.Delay(200); save(p.ToString().ToLowerInvariant()); }
			// color group card (gSheep) with the pink color selected
			var group = f.WebPets.pets.FirstOrDefault(p => p.Variants != null);
			if (group != null)
			{
				f.SelectPage(Page.Pets);
				f.searchBox.Text = "sheep";   // bring the group card into view
				group.SelectVariant(group.Variants.FirstOrDefault(v => v?.folder == "pink_sheep") ?? group.Variants.First(v => v != null));
				f.petsView.SelectForSnapshot(group); await f.LoadDetailsAsync(group); await Task.Delay(300); save("pets-group");
				f.petsView.CloseDetail();
				f.searchBox.Text = "";
			}
			// narrow window: the page subtitle must wrap instead of being cut
			f.SelectPage(Page.Pets); f.Width = 760; await Task.Delay(300); save("pets-narrow");
			f.Close();

			// animation picker (middle-click on the pet), built from the current pet
			try
			{
				var xml = new Xml(1);
				if (xml.ReadXML())
				{
					var anims = new Animations(xml);
					xml.LoadAnimations(anims);
					var picker = new FormAnimPicker(anims, xml.AnimationXML.Header.Petname,
						xml.sprites != null && xml.sprites.Count > 0 ? xml.sprites[0] : null, id => { })
					{ Location = new Point(-5000, -5000) };
					picker.Show();
					await Task.Delay(300);
					picker.Refresh();
					using (var bmp = new Bitmap(picker.ClientSize.Width, picker.ClientSize.Height))
					{
						picker.DrawToBitmap(bmp, new Rectangle(Point.Empty, picker.ClientSize));
						bmp.Save(System.IO.Path.Combine(folder, "anim-picker.png"), System.Drawing.Imaging.ImageFormat.Png);
					}
					picker.Close();
				}
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "picker snapshot: " + ex.Message); }

			// speech bubble of a reminder, and the reminder editor
			try
			{
				await Task.Delay(1500);   // the pet is on screen
				var sample = Reminders.All.FirstOrDefault() ?? new Reminder { Text = "Test" };
				Reminders.Fire(sample);
				await Task.Delay(400);
				var bubble = Application.OpenForms.OfType<SpeechBubble>().FirstOrDefault();
				if (bubble != null)
				{
					bubble.Refresh();
					using (var bmp = new Bitmap(bubble.Width, bubble.Height))
					{
						bubble.DrawToBitmap(bmp, new Rectangle(Point.Empty, bubble.Size));
						bmp.Save(System.IO.Path.Combine(folder, "bubble.png"), System.Drawing.Imaging.ImageFormat.Png);
					}
					bubble.Close();
				}
				using (var ed = new FormReminder(sample) { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000) })
				{
					ed.Show();
					await Task.Delay(300);
					using (var bmp = new Bitmap(ed.Width, ed.Height))
					{
						ed.DrawToBitmap(bmp, new Rectangle(Point.Empty, ed.Size));
						bmp.Save(System.IO.Path.Combine(folder, "reminder-edit.png"), System.Drawing.Imaging.ImageFormat.Png);
					}
				}
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "bubble snapshot: " + ex.Message); }

			// pet menu (right click) and the PC monitor plate
			try
			{
				var pet = Application.OpenForms.OfType<FormPet>().FirstOrDefault(p => p.Visible && !p.IsChildWindow);
				if (pet != null)
				{
					bool monitorWas = PcMonitor.Enabled;
					PcMonitor.Enabled = true;
					await Task.Delay(2500);
					foreach (var hud in Application.OpenForms.Cast<Form>().Where(fm => fm.GetType().Name == "Hud").ToList())
						using (var bmp = new Bitmap(hud.Width, hud.Height))
						{
							hud.DrawToBitmap(bmp, new Rectangle(Point.Empty, hud.Size));
							bmp.Save(System.IO.Path.Combine(folder, "monitor.png"), System.Drawing.Imaging.ImageFormat.Png);
						}
					PcMonitor.Enabled = monitorWas;
					var menu = PetMenu.Show(pet, pet, new Point(0, 0), () => { });
					await Task.Delay(500);
					if (menu != null)
					{
						using (var bmp = new Bitmap(menu.Width, menu.Height))
						{
							menu.DrawToBitmap(bmp, new Rectangle(Point.Empty, menu.Size));
							bmp.Save(System.IO.Path.Combine(folder, "pet-menu.png"), System.Drawing.Imaging.ImageFormat.Png);
						}
						menu.Close();
					}
				}
			}
			catch (Exception ex) { StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "pet menu snapshot: " + ex.Message); }

			// debug window, with a warning and an error in the log
			StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.warning, "snapshot: sample warning");
			StartUp.AddDebugInfo(StartUp.DEBUG_TYPE.error, "snapshot: sample error");
			FormDebugModern.ShowWindow();
			var dbg = Application.OpenForms.OfType<FormDebugModern>().First();
			dbg.Location = new Point(-5000, -5000);
			await Task.Delay(600);
			dbg.Refresh();
			using (var bmp = new Bitmap(dbg.ClientSize.Width, dbg.ClientSize.Height))
			{
				dbg.DrawToBitmap(bmp, new Rectangle(Point.Empty, dbg.ClientSize));
				bmp.Save(System.IO.Path.Combine(folder, "debug.png"), System.Drawing.Imaging.ImageFormat.Png);
			}
			dbg.Close();
		}

	}
}
