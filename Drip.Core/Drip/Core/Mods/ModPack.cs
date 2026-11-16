using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Drip.Core.FortniteModels;

namespace Drip.Core.Mods;

public sealed class ModPack
{
	private static readonly Regex IconUrl = new Regex("^https://fortnite-api\\.com/images/[A-Za-z0-9_\\-/\\.]{1,200}\\.png$", RegexOptions.Compiled);

	private static readonly Regex SlotName = new Regex("^[A-Za-z]{2,24}$", RegexOptions.Compiled);

	private static readonly Regex Id = new Regex("^[A-Za-z0-9_]{1,120}$", RegexOptions.Compiled);

	private static readonly Regex PackagePath = new Regex("^/[A-Za-z0-9_\\-/\\.]{1,300}$", RegexOptions.Compiled);

	public const long MaxFileBytes = 16777216L;

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public ModManifest Manifest { get; private init; } = new ModManifest();

	public string FilePath { get; private init; } = "";

	public string Key => Path.GetFileNameWithoutExtension(FilePath);

	public bool HasAnyIcon
	{
		get
		{
			if (Manifest.Icon.Length <= 0)
			{
				return ReadEmbeddedIcon() != null;
			}
			return true;
		}
	}

	public static ModPack Load(string path)
	{
		string json;
		if (Path.GetExtension(path).ToLowerInvariant() == ".json")
		{
			json = File.ReadAllText(path);
		}
		else
		{
			using ZipArchive zipArchive = ZipFile.OpenRead(path);
			ZipArchiveEntry? zipArchiveEntry = zipArchive.GetEntry("mod.json") ?? throw new InvalidDataException("No mod.json inside the mod.");
			if (zipArchiveEntry.Length > 1000000)
			{
				throw new InvalidDataException("mod.json is too large.");
			}
			using StreamReader streamReader = new StreamReader(zipArchiveEntry.Open());
			json = streamReader.ReadToEnd();
		}
		ModManifest manifest = JsonSerializer.Deserialize<ModManifest>(json, Json) ?? throw new InvalidDataException("mod.json is empty.");
		return new ModPack
		{
			Manifest = manifest,
			FilePath = path
		};
	}

	public List<string> Validate()
	{
		List<string> list = new List<string>();
		ModManifest manifest = Manifest;
		if (manifest.Format != 1)
		{
			list.Add($"Unsupported mod format {manifest.Format}.");
		}
		if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 80)
		{
			list.Add("The mod needs a name (80 characters max).");
		}
		if (manifest.Base.Length > 0 && !Id.IsMatch(manifest.Base))
		{
			list.Add("'base' must be a cosmetic id such as Character_Foo.");
		}
		if (manifest.Icon.Length > 0 && (!IconUrl.IsMatch(manifest.Icon) || manifest.Icon.Contains("..")))
		{
			list.Add("'icon' must be an https://fortnite-api.com/images/... .png address.");
		}
		if (manifest.PartSwaps.Count > 64)
		{
			list.Add("Too many part swaps (64 max).");
		}
		if (manifest.Packages.Count > 16)
		{
			list.Add("Too many packages (16 max).");
		}
		if (manifest.SlotSwaps.Count > 16)
		{
			list.Add("Too many slot swaps (16 max).");
		}
		if ((manifest.PartSwaps.Count > 0 || manifest.SlotSwaps.Count > 0) && manifest.Base.Length == 0)
		{
			list.Add("partSwaps / slotSwaps need a 'base' skin to apply to.");
		}
		foreach (ModSlotSwap slotSwap in manifest.SlotSwaps)
		{
			if (!SlotName.IsMatch(slotSwap.Slot ?? ""))
			{
				list.Add("Bad slot name '" + slotSwap.Slot + "' (use Head, Body, Hat, Face...).");
			}
			if (!Id.IsMatch(slotSwap.From ?? ""))
			{
				list.Add("Slot swap '" + slotSwap.Slot + "': 'from' must be a cosmetic id such as Character_Foo.");
			}
			else if (string.Equals(slotSwap.From, manifest.Base, StringComparison.OrdinalIgnoreCase))
			{
				list.Add("Slot swap '" + slotSwap.Slot + "' takes it from the base skin itself, so it does nothing.");
			}
		}
		if (manifest.HasDuplicateSlots())
		{
			list.Add("Each slot can only be swapped once.");
		}
		if (manifest.Base.Length == 0 && manifest.Packages.Count == 0 && manifest.FnModel == null && manifest.FnBuilds == null && manifest.FnItem == null)
		{
			list.Add("The mod does nothing: add a base skin or packages.");
		}
		if (manifest.FnModel != null)
		{
			list.AddRange(FnModelFile.Validate(this));
		}
		if (manifest.FnBuilds != null)
		{
			list.AddRange(BubbleBuilds.Validate(this));
		}
		if (manifest.FnItem != null)
		{
			list.AddRange(FnItemFile.Validate(this));
		}
		foreach (ModPartSwap partSwap in manifest.PartSwaps)
		{
			if (!ValidPackage(partSwap.From) || !ValidPackage(partSwap.To))
			{
				list.Add($"Bad part swap path: '{partSwap.From}' -> '{partSwap.To}'.");
			}
			else if (string.Equals(partSwap.From, partSwap.To, StringComparison.OrdinalIgnoreCase))
			{
				list.Add("Part swap does nothing: " + partSwap.From + ".");
			}
		}
		foreach (ModPackageFile package in manifest.Packages)
		{
			if (!ValidPackage(package.Target))
			{
				list.Add("Bad package target '" + package.Target + "'.");
			}
			if (!SafeEntry(package.File))
			{
				list.Add("Bad file path '" + package.File + "'.");
			}
		}
		if (FilePath.Length > 0 && IsZipForm(FilePath) && manifest.Packages.Count > 0)
		{
			try
			{
				using ZipArchive zipArchive = ZipFile.OpenRead(FilePath);
				foreach (ModPackageFile package2 in manifest.Packages)
				{
					if (SafeEntry(package2.File))
					{
						ZipArchiveEntry entry = zipArchive.GetEntry(package2.File.Replace('\\', '/'));
						if (entry == null)
						{
							list.Add("File '" + package2.File + "' is missing from the mod.");
						}
						else if (entry.Length > 16777216)
						{
							list.Add("File '" + package2.File + "' is too large.");
						}
					}
				}
			}
			catch (Exception ex)
			{
				list.Add("Can't read the mod: " + ex.Message);
			}
		}
		else if (manifest.Packages.Count > 0)
		{
			list.Add("Packages need the .drip zip form (a bare .json can't carry files).");
		}
		return list;
	}

	public byte[]? ReadEmbeddedIcon()
	{
		try
		{
			if (FilePath.Length == 0 || string.Equals(Path.GetExtension(FilePath), ".json", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			using ZipArchive zipArchive = ZipFile.OpenRead(FilePath);
			ZipArchiveEntry entry = zipArchive.GetEntry("icon.png");
			if (entry == null || entry.Length == 0L || entry.Length > 2000000)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			using (Stream stream = entry.Open())
			{
				stream.CopyTo(memoryStream);
			}
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
	}

	public string EmbeddedIconFile(string dir)
	{
		try
		{
			byte[] array = ReadEmbeddedIcon();
			if (array == null)
			{
				return "";
			}
			Directory.CreateDirectory(dir);
			string text = Path.Combine(dir, Key + ".icon.png");
			if (!File.Exists(text) || new FileInfo(text).Length != array.Length)
			{
				File.WriteAllBytes(text, array);
			}
			return text;
		}
		catch
		{
			return "";
		}
	}

	public byte[] ReadPackageFile(ModPackageFile f)
	{
		return ReadEntry(f.File, 16777216L);
	}

	public byte[] ReadEntry(string entry, long maxBytes = 16777216L)
	{
		if (!SafeEntry(entry))
		{
			throw new InvalidDataException("Bad file path '" + entry + "'.");
		}
		using ZipArchive zipArchive = ZipFile.OpenRead(FilePath);
		ZipArchiveEntry zipArchiveEntry = zipArchive.GetEntry(entry.Replace('\\', '/')) ?? throw new FileNotFoundException("'" + entry + "' is missing from the mod.");
		if (zipArchiveEntry.Length > maxBytes)
		{
			throw new InvalidDataException("'" + entry + "' is too large.");
		}
		using MemoryStream memoryStream = new MemoryStream();
		using (Stream stream = zipArchiveEntry.Open())
		{
			stream.CopyTo(memoryStream);
		}
		return memoryStream.ToArray();
	}

	public static bool IsAddonExtension(string ext)
	{
		if (!ext.Equals(".drip", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".dripFN", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".elenr", StringComparison.OrdinalIgnoreCase))
		{
			return ext.Equals(".elenrmod", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool IsZipForm(string path)
	{
		return IsAddonExtension(Path.GetExtension(path));
	}

	public static bool ValidPackage(string s)
	{
		if (!string.IsNullOrEmpty(s) && PackagePath.IsMatch(s) && !s.Contains(".."))
		{
			return !s.Contains("//");
		}
		return false;
	}

	public static bool SafeEntry(string s)
	{
		if (string.IsNullOrWhiteSpace(s) || s.Length > 200)
		{
			return false;
		}
		string text = s.Replace('\\', '/');
		if (!text.StartsWith('/') && !text.Contains("..") && !text.Contains(':'))
		{
			return !text.EndsWith('/');
		}
		return false;
	}
}
