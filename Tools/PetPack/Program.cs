using PetPack;

// PetPack: builds pets from PNG frames (Tools\PetPack\art\<pet>\...) and writes them as pet folders.
//   args[0] = output folder for the pets (default: Pets)
string outDir = args.Length > 0 ? args[0] : "Pets";
string artRoot = Path.Combine(AppContext.BaseDirectory, "art");
if (!Directory.Exists(artRoot))   // dotnet run: art sits next to the project, not the build output
	artRoot = Path.Combine(Path.GetDirectoryName(typeof(PngPet).Assembly.Location)!, "..", "..", "..", "art");
if (!Directory.Exists(artRoot)) artRoot = Path.Combine("Tools", "PetPack", "art");

foreach (var pet in new[] { SabioPet.Build(artRoot) })
	pet.Write(Path.Combine(outDir, pet.Def.Folder), pet.FootFrame, pet.IconFrame);
