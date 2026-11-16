using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drip.Core.Mods;

public sealed class PakPack
{
	private static readonly Regex SkinPath = new Regex("(?i)(?:^|/)Skins/([^/]+)/(.+)$", RegexOptions.Compiled);

	private static readonly Regex Strip = new Regex("[^a-z0-9]", RegexOptions.Compiled);

	public string FilePath { get; private init; } = "";

	public string Name { get; private set; } = "";

	public int Version { get; private set; }

	public int FileCount { get; private set; }

	public long TotalBytes { get; private set; }

	public bool HasSig { get; private set; }

	public string? Problem { get; private set; }

	public List<PakSkin> Skins { get; } = new List<PakSkin>();

	public int EmoteFiles { get; private set; }

	public int MusicFiles { get; private set; }

	public static PakPack Load(string path)
	{
		PakPack pakPack = new PakPack
		{
			FilePath = path
		};
		pakPack.Name = CleanName(Path.GetFileNameWithoutExtension(path));
		try
		{
			pakPack.HasSig = File.Exists(Path.ChangeExtension(path, ".sig"));
			LegacyPak legacyPak = LegacyPak.Open(path);
			pakPack.Version = legacyPak.Version;
			pakPack.FileCount = legacyPak.Entries.Count;
			pakPack.TotalBytes = legacyPak.Entries.Sum((PakEntry e) => e.Size);
			Dictionary<string, PakSkin> dictionary = new Dictionary<string, PakSkin>(StringComparer.OrdinalIgnoreCase);
			List<string> list = new List<string>();
			foreach (PakEntry entry in legacyPak.Entries)
			{
				string name = entry.Name;
				if (name.Contains("/Emotes/", StringComparison.OrdinalIgnoreCase))
				{
					pakPack.EmoteFiles++;
				}
				if (name.Contains("/MusicPacks/", StringComparison.OrdinalIgnoreCase))
				{
					pakPack.MusicFiles++;
				}
				if (name.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase) && name.Contains("/UI/Skins/", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(name);
				}
				Match match = SkinPath.Match(name);
				if (match.Success && !name.Contains("/UI/", StringComparison.OrdinalIgnoreCase))
				{
					string value = match.Groups[1].Value;
					if (!dictionary.TryGetValue(value, out var value2))
					{
						PakSkin pakSkin = new PakSkin
						{
							Folder = value,
							Name = Pretty(value)
						};
						value2 = pakSkin;
						dictionary[value] = pakSkin;
					}
					value2.Files++;
					value2.Bytes += entry.Size;
					string value3 = match.Groups[2].Value;
					if (value3.StartsWith("Meshes/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
					{
						value2.Meshes++;
					}
					else if (value3.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
					{
						value2.Textures++;
					}
					else if (value3.StartsWith("Mat", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
					{
						value2.Materials++;
					}
				}
			}
			foreach (PakSkin item in dictionary.Values.OrderBy((PakSkin s) => s.Name, StringComparer.OrdinalIgnoreCase))
			{
				item.IconEntry = MatchIcon(item.Folder, list);
				pakPack.Skins.Add(item);
			}
		}
		catch (Exception ex)
		{
			pakPack.Problem = ex.Message;
		}
		return pakPack;
	}

	public IconPixels? ReadIcon(LegacyPak pak, PakSkin skin)
	{
		if (skin.IconEntry == null)
		{
			return null;
		}
		try
		{
			return PakTexture.TryDecode(pak.Read(skin.IconEntry, 16777216L));
		}
		catch
		{
			return null;
		}
	}

	private static string CleanName(string stem)
	{
		string input = Regex.Replace(stem, "(?i)^pakchunk", "");
		input = Regex.Replace(input, "(?i)-WindowsClient$", "");
		input = Regex.Replace(input, "^[0-9]+-?", "");
		input = input.Replace('_', ' ').Trim();
		if (input.Length != 0)
		{
			return input;
		}
		return stem;
	}

	public static string Pretty(string folder)
	{
		return Regex.Replace(Regex.Replace(folder, "(?i)^[MF]_(MED|LRG|SML|LGE)_", "").Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ").Trim();
	}

	private static string Token(string folder)
	{
		return Strip.Replace(Regex.Replace(folder, "(?i)^[MF]_(MED|LRG|SML|LGE)_", "").ToLowerInvariant(), "");
	}

	public static string? MatchIcon(string folder, IReadOnlyList<string> iconUexps)
	{
		string text = Token(folder);
		if (text.Length < 2)
		{
			return null;
		}
		string result = null;
		int num = 0;
		foreach (string iconUexp in iconUexps)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(iconUexp);
			string text2 = Strip.Replace(fileNameWithoutExtension.ToLowerInvariant(), "");
			int num2 = 0;
			if (text2.Contains(text))
			{
				num2 = 100 - Math.Min(50, text2.Length - text.Length);
			}
			else
			{
				string text3 = Regex.Replace(text2, "(l|preview|ui)$", "");
				int num3 = Distance(text, text3);
				if (num3 <= 2 && text3.Length >= 4)
				{
					num2 = 60 - num3 * 10;
				}
			}
			if (num2 > num)
			{
				num = num2;
				result = iconUexp;
			}
		}
		return result;
	}

	private static int Distance(string a, string b)
	{
		int[,] array = new int[a.Length + 1, b.Length + 1];
		for (int i = 0; i <= a.Length; i++)
		{
			array[i, 0] = i;
		}
		for (int j = 0; j <= b.Length; j++)
		{
			array[0, j] = j;
		}
		for (int k = 1; k <= a.Length; k++)
		{
			for (int l = 1; l <= b.Length; l++)
			{
				array[k, l] = Math.Min(Math.Min(array[k - 1, l] + 1, array[k, l - 1] + 1), array[k - 1, l - 1] + ((a[k - 1] != b[l - 1]) ? 1 : 0));
			}
		}
		return array[a.Length, b.Length];
	}
}
