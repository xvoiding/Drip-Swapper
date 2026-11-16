using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Drip.Core.Mods;
using Drip.Core.RocketLeague.Custom;
using Drip.Core.Swapping;

namespace Drip.Core.FortniteModels;

public static class BubbleBuilds
{
	public const string Name = "Bubblewraps Builds";

	private const byte ExportBundleChunk = 1;

	public static readonly string[] AllMaterials = new string[3] { "wood", "brick", "metal" };

	private const string Shared = "/Game/Packages/Fortress_SharedMaterials/";

	private static readonly Dictionary<string, string[]> ByMaterial = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["wood"] = new string[3] { "/Game/Packages/Fortress_SharedMaterials/BluePrintTexture04", "/Game/Packages/Fortress_SharedMaterials/BluePrintTexture02", "/Game/Packages/User_Built_Props/Wall/Metal/blueprintWoodlv1_outline" },
		["brick"] = new string[1] { "/Game/Packages/Fortress_SharedMaterials/BrickBlueprintTexture" },
		["metal"] = new string[1] { "/Game/Packages/Fortress_SharedMaterials/BlueprintTextureMetal" }
	};

	private const string Outlines = "/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1";

	public static readonly string[] Packages;

	private static readonly int[] Sizes;

	public static string[] PackagesFor(IEnumerable<string>? materials)
	{
		List<string> list = (materials ?? Array.Empty<string>()).Where((string m) => ByMaterial.ContainsKey(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			list = AllMaterials.ToList();
		}
		List<string> list2 = new List<string>();
		list2.AddRange(list.SelectMany((string m) => ByMaterial[m]));
		list2.Add("/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1");
		return list2.ToArray();
	}

	public static TexImage Generate(int size)
	{
		byte[] array = new byte[size * size * 4];
		int num = Math.Max(4, size / 64);
		double num2 = (double)size / (double)num;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				double num3 = (Math.Floor((double)j / num2) + 0.5) * num2;
				double num4 = (Math.Floor((double)i / num2) + 0.5) * num2;
				double num5 = ((double)j + 0.5 - num3) / (num2 * 0.46);
				double num6 = ((double)i + 0.5 - num4) / (num2 * 0.46);
				double num7 = num5 * num5 + num6 * num6;
				double num8;
				double num9;
				double num10;
				if (num7 >= 1.0)
				{
					num8 = 214.0;
					num9 = 70.0;
					num10 = 150.0;
				}
				else
				{
					double num11 = Math.Sqrt(1.0 - num7);
					num8 = 255.0 - 40.0 * (1.0 - num11);
					num9 = 150.0 + 70.0 * num11;
					num10 = 205.0 + 30.0 * num11;
					double num12 = num5 + 0.38;
					double num13 = num6 + 0.42;
					double num14 = Math.Max(0.0, 1.0 - Math.Sqrt(num12 * num12 + num13 * num13 * 1.6) / 0.34);
					num8 += (255.0 - num8) * num14;
					num9 += (255.0 - num9) * num14;
					num10 += (255.0 - num10) * num14;
					if (num7 > 0.82)
					{
						double num15 = (num7 - 0.82) / 0.18;
						num8 -= 28.0 * num15;
						num9 -= 46.0 * num15;
						num10 -= 30.0 * num15;
					}
				}
				int num16 = (i * size + j) * 4;
				array[num16] = (byte)Math.Clamp(num8, 0.0, 255.0);
				array[num16 + 1] = (byte)Math.Clamp(num9, 0.0, 255.0);
				array[num16 + 2] = (byte)Math.Clamp(num10, 0.0, 255.0);
				array[num16 + 3] = byte.MaxValue;
			}
		}
		return new TexImage
		{
			Width = size,
			Height = size,
			Rgba = array
		};
	}

	public static BubbleBuild Build(Func<string, byte, byte[]> read, Func<string, byte, long> capacity, TexImage? custom, Action<string>? log = null, IEnumerable<string>? materials = null, string style = "bubble")
	{
		BubbleBuild bubbleBuild = new BubbleBuild();
		BuildLook buildLook = BuildLooks.Find(style) ?? BuildLooks.All[0];
		Dictionary<int, TexImage> dictionary = new Dictionary<int, TexImage>();
		string[] array = PackagesFor(materials);
		foreach (string text in array)
		{
			string text2 = text.Split('/')[^1];
			try
			{
				byte[] array2 = read(text, 1);
				FnTexturePackage.Info info = FnTexturePackage.Read(array2);
				if (info.PixelFormat != "PF_DXT1")
				{
					bubbleBuild.Notes.Add(text2 + ": skipped (it's " + info.PixelFormat + ", only DXT1 pictures can be repainted).");
					continue;
				}
				long num = capacity(text, 1);
				byte[] array3 = null;
				int value = 0;
				int[] sizes = Sizes;
				foreach (int num2 in sizes)
				{
					if ((long)num2 * (long)num2 / 2 + 256 <= num)
					{
						if (!dictionary.TryGetValue(num2, out var value2))
						{
							value2 = (dictionary[num2] = ((custom == null) ? buildLook.Make(num2) : custom.Resample(num2, num2)));
						}
						byte[] array4 = FnTexturePackage.Build(array2, value2, trimToSlots: true);
						if (Align16(array4.Length) <= num)
						{
							array3 = array4;
							value = num2;
							break;
						}
					}
				}
				if (array3 == null)
				{
					bubbleBuild.Notes.Add($"{text2}: skipped (its slot only holds {num} bytes, even a 64x64 picture doesn't fit).");
					continue;
				}
				bubbleBuild.Patches.Add(new BubblePatch(text, array3, $"{text2} ({value}x{value})"));
				log?.Invoke($"{text2}: {value}x{value} picture, {array3.Length} of {num} bytes.");
			}
			catch (Exception ex) when ((ex is InvalidOperationException || ex is InvalidDataException) ? true : false)
			{
				bubbleBuild.Notes.Add(text2 + ": skipped (" + ex.Message + ")");
			}
		}
		if (bubbleBuild.Patches.Count == 0)
		{
			throw new InvalidDataException("None of the build blueprint pictures could be repainted in this Fortnite version." + ((bubbleBuild.Notes.Count > 0) ? (" " + string.Join(" ", bubbleBuild.Notes.Take(2))) : ""));
		}
		return bubbleBuild;
	}

	private static long Align16(int n)
	{
		return ((long)n + 15L) & -16;
	}

	public static void WriteFile(string dest, string name, string author, byte[]? picture, string description = "", IEnumerable<string>? materials = null, string style = "bubble")
	{
		TexImage texImage = null;
		if (picture != null)
		{
			texImage = TexImage.Load(picture);
			if (texImage.Width < 4 || texImage.Height < 4)
			{
				throw new InvalidDataException("That picture is too small.");
			}
		}
		ModManifest value = new ModManifest
		{
			Name = ((!string.IsNullOrWhiteSpace(name)) ? name.Trim() : (BuildLooks.Find(style)?.Name ?? "Bubblewraps Builds")),
			Author = (author ?? ""),
			Description = ((description.Length > 0) ? description : ((picture == null) ? ("Paints the build blueprints: " + (BuildLooks.Find(style)?.Blurb ?? "bubble wrap.")) : "Paints the build blueprints with your own picture.")),
			FnBuilds = new ModFnBuilds
			{
				Style = ((picture != null) ? "picture" : (BuildLooks.Find(style)?.Id ?? "bubble")),
				Picture = ((picture == null) ? "" : "builds/picture.png"),
				Materials = (from m in materials ?? AllMaterials
					where ByMaterial.ContainsKey(m)
					select m.ToLowerInvariant()).Distinct().ToList()
			}
		};
		string text = dest + ".tmp";
		using (FileStream stream = File.Create(text))
		{
			using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
			using (StreamWriter streamWriter = new StreamWriter(zipArchive.CreateEntry("mod.json").Open()))
			{
				streamWriter.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions
				{
					WriteIndented = true
				}));
			}
			if (texImage != null)
			{
				TexImage texImage2 = ((texImage.Width > 2048 || texImage.Height > 2048) ? texImage.Resample(Math.Min(texImage.Width, 2048), Math.Min(texImage.Height, 2048)) : texImage);
				using Stream stream2 = zipArchive.CreateEntry("builds/picture.png").Open();
				byte[] array = texImage2.ToPng();
				stream2.Write(array, 0, array.Length);
			}
		}
		File.Move(text, dest, overwrite: true);
	}

	public static IEnumerable<string> Validate(ModPack mod)
	{
		ModFnBuilds fb = mod.Manifest.FnBuilds;
		if (mod.Manifest.Base.Length > 0 || mod.Manifest.Packages.Count > 0 || mod.Manifest.PartSwaps.Count > 0 || mod.Manifest.SlotSwaps.Count > 0)
		{
			yield return "A Builds drip can't also swap skins or carry packages.";
		}
		if ((object)BuildLooks.Find(fb.Style) == null && !fb.Style.Equals("picture", StringComparison.OrdinalIgnoreCase))
		{
			yield return "Unknown builds style '" + fb.Style + "'.";
		}
		foreach (string material in fb.Materials)
		{
			if (!ByMaterial.ContainsKey(material))
			{
				yield return "Unknown build material '" + material + "' (use wood, brick or metal).";
			}
		}
		if (!fb.Style.Equals("picture", StringComparison.OrdinalIgnoreCase))
		{
			yield break;
		}
		if (!ModPack.SafeEntry(fb.Picture))
		{
			yield return "Bad picture path '" + fb.Picture + "'.";
		}
		else if (ModPack.IsZipForm(mod.FilePath))
		{
			string text = null;
			try
			{
				using ZipArchive zipArchive = ZipFile.OpenRead(mod.FilePath);
				ZipArchiveEntry entry = zipArchive.GetEntry(fb.Picture.Replace('\\', '/'));
				if (entry == null)
				{
					text = "The picture '" + fb.Picture + "' is missing from the drip.";
				}
				else if (entry.Length > 16777216)
				{
					text = "The picture is too large.";
				}
			}
			catch (Exception ex)
			{
				text = "Can't read the drip: " + ex.Message;
			}
			if (text != null)
			{
				yield return text;
			}
		}
		else
		{
			yield return "A picture needs the .dripFN zip form.";
		}
	}

	public static byte[] PreviewPng(ModPack mod)
	{
		TexImage texImage = PictureOf(mod);
		return ((texImage == null) ? (BuildLooks.Find(mod.Manifest.FnBuilds.Style) ?? BuildLooks.All[0]).Make(256) : texImage.Resample(256, 256)).ToPng();
	}

	public static TexImage? PictureOf(ModPack mod)
	{
		ModFnBuilds modFnBuilds = mod.Manifest.FnBuilds ?? throw new InvalidOperationException("That drip has no builds look in it.");
		if (!modFnBuilds.Style.Equals("picture", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return TexImage.Load(mod.ReadEntry(modFnBuilds.Picture, 16777216L));
	}

	public static List<string> Apply(SwapEngine engine, ModPack mod, Action<string>? log = null, IEnumerable<string>? materials = null)
	{
		TexImage custom = PictureOf(mod);
		BubbleBuild bubbleBuild = Build((string pkg, byte type) => engine.ReadPackageChunk(pkg, type), (string pkg, byte type) => engine.ChunkCapacity(pkg, type), custom, log, materials ?? mod.Manifest.FnBuilds.Materials, mod.Manifest.FnBuilds.Style);
		foreach (string note in bubbleBuild.Notes)
		{
			log?.Invoke(note);
		}
		string[] source = PackagesFor(materials ?? mod.Manifest.FnBuilds.Materials);
		engine.RevertPackages(source.Where((string p) => !p.Equals("/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1", StringComparison.OrdinalIgnoreCase)));
		bool flag = engine.IsActive(new _003C_003Ez__ReadOnlySingleElementList<string>("/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1"));
		List<string> list = new List<string>();
		try
		{
			foreach (BubblePatch patch in bubbleBuild.Patches)
			{
				if (!flag || !patch.Package.Equals("/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1", StringComparison.OrdinalIgnoreCase))
				{
					SwapResult swapResult = engine.PatchPackageChunk(patch.Package, 1, patch.Data, mod.Manifest.Name);
					list.Add(swapResult.Source);
					log?.Invoke($"OK  {patch.What}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
				}
			}
			List<string> list2 = engine.MissingPackages(list);
			if (list2.Count > 0)
			{
				throw new InvalidOperationException("The builds look was written but the game files don't show it afterwards (" + string.Join(", ", list2.Select((string m) => m.Split('/')[^1])) + "). Close the Epic Launcher and try again.");
			}
			return list;
		}
		catch
		{
			try
			{
				engine.RevertPackages(list);
			}
			catch
			{
			}
			throw;
		}
	}

	static BubbleBuilds()
	{
		List<string> list = new List<string>();
		list.AddRange(ByMaterial.Values.SelectMany((string[] x) => x));
		list.Add("/Game/Packages/Fortress_SharedMaterials/Textures/bluePrintOutlines1");
		Packages = list.ToArray();
		Sizes = new int[6] { 2048, 1024, 512, 256, 128, 64 };
	}
}
