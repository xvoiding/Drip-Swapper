using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Drip.Core.FallGuys;
using Drip.Core.Mods;
using Drip.Core.RocketLeague;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core;

public static class Preinstalled
{
	public sealed record Result(int Added, int Skipped, List<string> Problems);

	private static readonly string[] RetiredFallGuys = new string[6] { "Beach Ball", "Crate", "Disco Ball", "Donut", "Gold Bean", "Traffic Cone" };

	public static string Root(string? baseDir = null)
	{
		return Path.Combine(baseDir ?? AppContext.BaseDirectory, "Drips");
	}

	public static string FortniteDir(string? baseDir = null)
	{
		return Path.Combine(Root(baseDir), "Fortnite");
	}

	public static string RocketLeagueDir(string? baseDir = null)
	{
		return Path.Combine(Root(baseDir), "Rocket League");
	}

	public static string FallGuysDir(string? baseDir = null)
	{
		return Path.Combine(Root(baseDir), "Fall Guys");
	}

	public static string BuildsDir(string? baseDir = null)
	{
		return Path.Combine(Root(baseDir), "Builds");
	}

	public static string SeenFile()
	{
		return Path.Combine(AppPaths.DataDir, "preinstalled.json");
	}

	public static Result SeedFortnite(ModLibrary library, string? baseDir = null, string? seenFile = null)
	{
		return SeedDrips(library, FortniteDir(baseDir), "fn", seenFile);
	}

	public static Result SeedBuilds(ModLibrary library, string? baseDir = null, string? seenFile = null)
	{
		return SeedDrips(library, BuildsDir(baseDir), "fnb", seenFile);
	}

	private static Result SeedDrips(ModLibrary library, string dir, string keyKind, string? seenFile)
	{
		Result result = new Result(0, 0, new List<string>());
		if (!Directory.Exists(dir))
		{
			return result;
		}
		if (seenFile == null)
		{
			seenFile = SeenFile();
		}
		HashSet<string> hashSet = LoadSeen(seenFile);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, ModPack> dictionary = new Dictionary<string, ModPack>(StringComparer.OrdinalIgnoreCase);
		foreach (ModPack item2 in library.List())
		{
			string text = item2.Manifest.Name + "|" + item2.Manifest.Base;
			hashSet2.Add(text);
			if (IsOldBundled(item2.Manifest))
			{
				dictionary[text] = item2;
			}
		}
		int num = 0;
		int num2 = 0;
		bool flag = false;
		foreach (string item3 in Files(dir, ".dripfn", ".drip"))
		{
			string item = Key(keyKind, item3);
			if (hashSet.Contains(item))
			{
				num2++;
				continue;
			}
			try
			{
				ModPack modPack = ModPack.Load(item3);
				string text2 = modPack.Manifest.Name + "|" + modPack.Manifest.Base;
				if (hashSet2.Contains(text2))
				{
					if (dictionary.Remove(text2, out var value) && !IsOldBundled(modPack.Manifest))
					{
						library.Remove(value);
						library.Import(item3);
					}
					num2++;
				}
				else
				{
					library.Import(item3);
					hashSet2.Add(text2);
					num++;
				}
			}
			catch (Exception ex)
			{
				result.Problems.Add(Path.GetFileName(item3) + ": " + ex.Message);
			}
			hashSet.Add(item);
			flag = true;
		}
		if (flag)
		{
			SaveSeen(seenFile, hashSet);
		}
		return result with
		{
			Added = num,
			Skipped = num2
		};
	}

	private static bool IsOldBundled(ModManifest m)
	{
		if (!string.Equals(m.Author, "Tozo", StringComparison.OrdinalIgnoreCase))
		{
			return m.Description.Contains("Written over a skin you own", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static Result SeedRocketLeague(string? baseDir = null, string? libraryDir = null, string? seenFile = null)
	{
		Result result = new Result(0, 0, new List<string>());
		string text = RocketLeagueDir(baseDir);
		if (!Directory.Exists(text))
		{
			return result;
		}
		if (seenFile == null)
		{
			seenFile = SeenFile();
		}
		HashSet<string> hashSet = LoadSeen(seenFile);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (RlCustom.Entry item2 in RlCustom.List(libraryDir))
		{
			hashSet2.Add(Id(item2.Model));
		}
		int num = 0;
		int num2 = 0;
		bool flag = false;
		foreach (string item3 in Files(text, ".driprl"))
		{
			string item = Key("rl", item3);
			if (hashSet.Contains(item))
			{
				num2++;
				continue;
			}
			try
			{
				DripRlFile dripRlFile = DripRlFile.Load(item3);
				if (hashSet2.Contains(Id(dripRlFile)))
				{
					num2++;
				}
				else
				{
					RlCustom.Add(dripRlFile, libraryDir);
					hashSet2.Add(Id(dripRlFile));
					num++;
				}
			}
			catch (Exception ex)
			{
				result.Problems.Add(Path.GetFileName(item3) + ": " + ex.Message);
			}
			hashSet.Add(item);
			flag = true;
		}
		if (flag)
		{
			SaveSeen(seenFile, hashSet);
		}
		return result with
		{
			Added = num,
			Skipped = num2
		};
	}

	public static Result SeedFallGuys(string? baseDir = null, FgModelLibrary? library = null, string? seenFile = null)
	{
		Result result = new Result(0, 0, new List<string>());
		string text = FallGuysDir(baseDir);
		if (!Directory.Exists(text))
		{
			return result;
		}
		if (library == null)
		{
			library = new FgModelLibrary();
		}
		if (seenFile == null)
		{
			seenFile = SeenFile();
		}
		HashSet<string> hashSet = LoadSeen(seenFile);
		HashSet<string> hashSet2 = new HashSet<string>(from e in library.List()
			select e.File.Name, StringComparer.OrdinalIgnoreCase);
		int num = 0;
		int num2 = 0;
		bool flag = false;
		if (!hashSet.Contains("fg-retired|2"))
		{
			foreach (FgModelLibrary.Entry item2 in library.List())
			{
				if (RetiredFallGuys.Contains(item2.File.Name, StringComparer.OrdinalIgnoreCase) && (string.Equals(item2.File.Author, "Drip Swapper", StringComparison.OrdinalIgnoreCase) || string.Equals(item2.File.Author, "Drip", StringComparison.OrdinalIgnoreCase)))
				{
					try
					{
						library.Remove(item2.Path);
						hashSet2.Remove(item2.File.Name);
					}
					catch
					{
					}
				}
			}
			hashSet.Add("fg-retired|2");
			flag = true;
		}
		foreach (string item3 in Files(text, ".dripfg"))
		{
			string item = Key("fg", item3);
			if (hashSet.Contains(item))
			{
				num2++;
				continue;
			}
			try
			{
				FgDripFile fgDripFile = FgDripFile.Load(item3);
				if (hashSet2.Contains(fgDripFile.Name))
				{
					num2++;
				}
				else
				{
					library.Add(fgDripFile);
					hashSet2.Add(fgDripFile.Name);
					num++;
				}
			}
			catch (Exception ex)
			{
				result.Problems.Add(Path.GetFileName(item3) + ": " + ex.Message);
			}
			hashSet.Add(item);
			flag = true;
		}
		if (flag)
		{
			SaveSeen(seenFile, hashSet);
		}
		return result with
		{
			Added = num,
			Skipped = num2
		};
	}

	private static string Id(DripRlFile m)
	{
		return $"{m.Slot}|{m.Mesh.VertexCount}|{m.Mesh.TriangleCount}|{m.Picture.Length}";
	}

	private static string Key(string kind, string file)
	{
		return $"{kind}|{Path.GetFileName(file).ToLowerInvariant()}|{new FileInfo(file).Length}";
	}

	private static IEnumerable<string> Files(string dir, params string[] exts)
	{
		return (from f in Directory.EnumerateFiles(dir)
			where exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
			select f).OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase);
	}

	private static HashSet<string> LoadSeen(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				return new HashSet<string>(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
			}
		}
		catch
		{
		}
		return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	}

	private static void SaveSeen(string path, HashSet<string> seen)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, JsonSerializer.Serialize(seen.OrderBy((string s) => s).ToArray(), new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}
}
