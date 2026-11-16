using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Drip.Core.Stellar;

/// <summary>One cosmetic definition found in the game's paks.</summary>
public sealed record StellarItem(string Category, string Id, string PakFile, string UassetPath);

/// <summary>Make <see cref="TargetId"/> (what you equip) show up as <see cref="SourceId"/> (the look you want).</summary>
public sealed record StellarSwap(string Category, string TargetId, string SourceId);

public sealed record StellarResult(bool Ok, string Message);

/// <summary>
/// Swaps cosmetics in Project Stellar's Fortnite 12.41 by writing a small extra pak next to the game's own paks.
/// Nothing in the game's original pak files is edited: Undo just deletes the extra pak.
/// </summary>
public sealed class StellarSwapper
{
	public const string PatchPakName = "pakchunk99-WindowsClient_P.pak";
	public const string Mount = "../../../FortniteGame/Content/";
	private const string ContentPrefix = "FortniteGame/Content/";

	public static readonly string[] Categories = { "skin", "pickaxe", "emote", "glider" };

	private static readonly Regex DefinitionPath = new Regex(
		"^FortniteGame/Content/Athena/Items/Cosmetics/(Characters/CID_|Pickaxes/Pickaxe_ID_|Dances/EID_|Gliders/Glider_ID_)[^/]+\\.uasset$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private readonly string _paks;

	public string PaksDir => _paks;
	public string PatchPakPath => Path.Combine(_paks, PatchPakName);
	private static string ManifestPath => Path.Combine(AppPaths.DataDir, "stellar-swaps.json");
	private static string CatalogPath => Path.Combine(AppPaths.DataDir, "stellar-catalog.json");

	public StellarSwapper(string paksDir)
	{
		_paks = paksDir;
	}

	/// <summary>Accepts the Paks folder, the game's root folder, or the FortniteGame folder and returns the Paks folder.</summary>
	public static string? FindPaks(string picked)
	{
		if (string.IsNullOrWhiteSpace(picked) || !Directory.Exists(picked))
		{
			return null;
		}
		foreach (string rel in new[] { "", "Content\\Paks", "FortniteGame\\Content\\Paks" })
		{
			string p = Path.Combine(picked, rel);
			if (Directory.Exists(p) && string.Equals(Path.GetFileName(p.TrimEnd('\\', '/')), "Paks", StringComparison.OrdinalIgnoreCase) && Directory.EnumerateFiles(p, "*.pak").Any())
			{
				return p;
			}
		}
		return null;
	}

	public static string? CategoryOf(string path)
	{
		Match m = DefinitionPath.Match(path);
		if (!m.Success)
		{
			return null;
		}
		return m.Groups[1].Value.ToLowerInvariant() switch
		{
			"characters/cid_" => "skin",
			"pickaxes/pickaxe_id_" => "pickaxe",
			"dances/eid_" => "emote",
			_ => "glider"
		};
	}

	private static bool IsDefinitionFile(string path)
	{
		if (path.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase))
		{
			return CategoryOf(path.Substring(0, path.Length - 5) + ".uasset") != null;
		}
		return CategoryOf(path) != null;
	}

	private IEnumerable<string> GamePaks()
	{
		return Directory.EnumerateFiles(_paks, "*.pak")
			.Where(p => !string.Equals(Path.GetFileName(p), PatchPakName, StringComparison.OrdinalIgnoreCase))
			.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>Finds every skin, pickaxe, emote and glider definition in the game's paks (cached between runs).</summary>
	public List<StellarItem> Scan(Action<string>? progress = null)
	{
		string[] paks = GamePaks().ToArray();
		string signature = string.Join("|", paks.Select(p => { FileInfo f = new FileInfo(p); return f.Name + ":" + f.Length; }));
		try
		{
			if (File.Exists(CatalogPath))
			{
				CatalogCache? cache = JsonSerializer.Deserialize<CatalogCache>(File.ReadAllText(CatalogPath));
				if (cache != null && cache.Signature == signature && cache.Items.Count > 0)
				{
					return cache.Items;
				}
			}
		}
		catch
		{
		}
		List<StellarItem> items = new List<StellarItem>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int done = 0;
		foreach (string pak in paks)
		{
			done++;
			progress?.Invoke("Reading " + Path.GetFileName(pak) + " (" + done + "/" + paks.Length + ")");
			try
			{
				using PakV10Reader r = new PakV10Reader(pak, null, p => CategoryOf(p) != null);
				foreach (PakEntryInfo e in r.Entries.Values)
				{
					string? cat = CategoryOf(e.Path);
					string id = Path.GetFileNameWithoutExtension(e.Path);
					if (cat != null && seen.Add(cat + "/" + id))
					{
						items.Add(new StellarItem(cat, id, Path.GetFileName(pak), e.Path));
					}
				}
			}
			catch (Exception)
			{
				// Not a pak this key opens (for example the dynamic-key chunk). Skip it.
			}
		}
		try
		{
			Directory.CreateDirectory(AppPaths.DataDir);
			File.WriteAllText(CatalogPath, JsonSerializer.Serialize(new CatalogCache { Signature = signature, Items = items }));
		}
		catch
		{
		}
		return items;
	}

	private sealed class CatalogCache
	{
		public string Signature { get; set; } = "";
		public List<StellarItem> Items { get; set; } = new List<StellarItem>();
	}

	public List<StellarSwap> Current()
	{
		try
		{
			if (File.Exists(ManifestPath))
			{
				ManifestFile? m = JsonSerializer.Deserialize<ManifestFile>(File.ReadAllText(ManifestPath));
				if (m != null && string.Equals(m.Paks, _paks, StringComparison.OrdinalIgnoreCase) && File.Exists(PatchPakPath))
				{
					return m.Swaps;
				}
			}
		}
		catch
		{
		}
		return new List<StellarSwap>();
	}

	private sealed class ManifestFile
	{
		public string Paks { get; set; } = "";
		public List<StellarSwap> Swaps { get; set; } = new List<StellarSwap>();
	}

	/// <summary>Writes the patch pak so that exactly <paramref name="swaps"/> are active (an empty list removes it).</summary>
	public StellarResult Apply(IReadOnlyList<StellarSwap> swaps, IReadOnlyList<StellarItem> catalog)
	{
		List<StellarSwap> clean = swaps
			.Where(s => !string.Equals(s.TargetId, s.SourceId, StringComparison.OrdinalIgnoreCase))
			.GroupBy(s => s.Category + "/" + s.TargetId, StringComparer.OrdinalIgnoreCase)
			.Select(g => g.Last())
			.ToList();
		if (clean.Count == 0)
		{
			return Undo();
		}
		Dictionary<string, StellarItem> byKey = catalog.ToDictionary(i => i.Category + "/" + i.Id, StringComparer.OrdinalIgnoreCase);
		Dictionary<string, PakV10Reader> readers = new Dictionary<string, PakV10Reader>(StringComparer.OrdinalIgnoreCase);
		try
		{
			List<(string, byte[])> files = new List<(string, byte[])>();
			foreach (StellarSwap swap in clean)
			{
				if (!byKey.TryGetValue(swap.Category + "/" + swap.TargetId, out StellarItem? target))
				{
					return new StellarResult(false, swap.TargetId + " isn't in your Stellar files.");
				}
				if (!byKey.TryGetValue(swap.Category + "/" + swap.SourceId, out StellarItem? source))
				{
					return new StellarResult(false, swap.SourceId + " isn't in your Stellar files.");
				}
				if (!readers.TryGetValue(source.PakFile, out PakV10Reader? reader))
				{
					string path = Path.Combine(_paks, source.PakFile);
					reader = new PakV10Reader(path, null, IsDefinitionFile);
					readers[source.PakFile] = reader;
				}
				string uexpPath = source.UassetPath.Substring(0, source.UassetPath.Length - 7) + ".uexp";
				byte[] uasset;
				byte[] uexp;
				try
				{
					uasset = reader.Read(source.UassetPath);
					uexp = reader.Read(uexpPath);
				}
				catch (Exception ex)
				{
					return new StellarResult(false, "Couldn't read " + swap.SourceId + " from your game files: " + ex.Message);
				}
				byte[] renamed;
				try
				{
					renamed = LegacyPackage.RenameAsset(uasset, source.Id, target.Id);
				}
				catch (Exception ex)
				{
					return new StellarResult(false, "Couldn't prepare " + swap.SourceId + " as " + swap.TargetId + ": " + ex.Message);
				}
				string tgtBase = target.UassetPath.Substring(ContentPrefix.Length, target.UassetPath.Length - ContentPrefix.Length - 7);
				files.Add((tgtBase + ".uasset", renamed));
				files.Add((tgtBase + ".uexp", uexp));
			}
			string temp = PatchPakPath + ".tmp";
			PakV10Writer.Write(temp, Mount, files, PakV10Reader.Key1241, new[] { "", "", "", "", "" });
			try
			{
				File.Move(temp, PatchPakPath, overwrite: true);
			}
			catch (IOException)
			{
				try { File.Delete(temp); } catch { }
				return new StellarResult(false, "Couldn't write the swap file. Close Fortnite first, then try again.");
			}
			SaveManifest(clean);
			return new StellarResult(true, clean.Count == 1 ? "Swap is in." : clean.Count + " swaps are in.");
		}
		finally
		{
			foreach (PakV10Reader r in readers.Values)
			{
				r.Dispose();
			}
		}
	}

	/// <summary>Removes the patch pak so the game is exactly as Stellar installed it.</summary>
	public StellarResult Undo()
	{
		try
		{
			if (File.Exists(PatchPakPath))
			{
				File.Delete(PatchPakPath);
			}
			if (File.Exists(ManifestPath))
			{
				File.Delete(ManifestPath);
			}
			return new StellarResult(true, "All swaps are undone. Your Stellar files are back to normal.");
		}
		catch (Exception ex)
		{
			return new StellarResult(false, "Couldn't remove the swap file (" + ex.Message + "). Close Fortnite first, then try again.");
		}
	}

	private void SaveManifest(List<StellarSwap> swaps)
	{
		try
		{
			Directory.CreateDirectory(AppPaths.DataDir);
			File.WriteAllText(ManifestPath, JsonSerializer.Serialize(new ManifestFile { Paks = _paks, Swaps = swaps }));
		}
		catch
		{
		}
	}
}
