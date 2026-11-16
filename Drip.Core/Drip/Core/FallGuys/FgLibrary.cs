using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Drip.Core.FallGuys;

public sealed class FgLibrary
{
	private static readonly Regex Addr = new Regex("^(?<season>S[^/]*)/(?<id>CH_[^/]+)/Prefabs/(?<file>[^/]+)\\.prefab$", RegexOptions.Compiled);

	private static readonly Regex Rest = new Regex("^(?:(?<team>Team)_)?(?:Variant(?<var>\\d*)_)?(?:(?<team2>Team)_)?(?<role>Top|Bottom|Full|LOD_OPT|LOD)$", RegexOptions.Compiled);

	private readonly Dictionary<string, FgCostume> _byKey;

	private readonly Dictionary<string, FgEmote> _emoteByKey;

	private static readonly Regex EmoteFile = new Regex("^CH_Fall(?:G|F)u[yg]s?[@_]FG_(?<n>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public IReadOnlyList<FgCostume> Costumes { get; }

	public IReadOnlyList<FgEmote> Emotes { get; }

	public FgLibrary(FgCatalog cat)
	{
		Dictionary<string, FgCostume> dictionary = new Dictionary<string, FgCostume>(StringComparer.Ordinal);
		foreach (FgAsset asset in cat.Assets)
		{
			if (asset.Extension != "prefab")
			{
				continue;
			}
			(string, string, FgRole, bool, int, string)? tuple = ParseAddress(asset.Address);
			if (tuple.HasValue)
			{
				(string, string, FgRole, bool, int, string) value = tuple.Value;
				string item = value.Item1;
				string item2 = value.Item2;
				FgRole item3 = value.Item3;
				bool item4 = value.Item4;
				int item5 = value.Item5;
				string item6 = value.Item6;
				string key = item + "/" + item2;
				if (!dictionary.TryGetValue(key, out var value2))
				{
					FgCostume fgCostume = new FgCostume
					{
						Season = item,
						Id = item2,
						Name = Pretty(item2)
					};
					value2 = fgCostume;
					dictionary[key] = fgCostume;
				}
				value2.Pieces.Add(new FgPiece(asset, item3, item4, item5, item6));
			}
		}
		Costumes = dictionary.Values.Where((FgCostume c) => c.Pieces.Count > 0).OrderBy((FgCostume c) => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy((FgCostume c) => c.Season, StringComparer.Ordinal)
			.ToList();
		_byKey = Costumes.ToDictionary((FgCostume c) => c.Key, StringComparer.Ordinal);
		List<FgEmote> list = new List<FgEmote>();
		foreach (FgAsset asset2 in cat.Assets)
		{
			if (asset2.TypeName.EndsWith("AnimationClip", StringComparison.Ordinal))
			{
				string text = EmoteName(asset2.BaseName);
				if (text != null)
				{
					string season = (asset2.Address.Contains('/') ? asset2.Address.Substring(0, asset2.Address.IndexOf('/')) : "");
					list.Add(new FgEmote(asset2, text, season));
				}
			}
		}
		Emotes = list.OrderBy((FgEmote e) => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy((FgEmote e) => e.Season, StringComparer.Ordinal).ToList();
		_emoteByKey = Emotes.ToDictionary((FgEmote e) => e.Key, StringComparer.Ordinal);
	}

	public FgEmote? FindEmote(string address)
	{
		return _emoteByKey.GetValueOrDefault(address);
	}

	internal static string? EmoteName(string baseName)
	{
		Match match = EmoteFile.Match(baseName);
		if (!match.Success)
		{
			return null;
		}
		string value = match.Groups["n"].Value;
		value = Regex.Replace(value, "^emote[_@]?", "", RegexOptions.IgnoreCase);
		value = Regex.Replace(value, "(_\\d+)?_A$", "", RegexOptions.IgnoreCase);
		value = Regex.Replace(value, "_\\d+$", "");
		if (value.Length == 0)
		{
			return null;
		}
		string text = Pretty("CH_" + value);
		if (text.Length <= 0)
		{
			return text;
		}
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}

	internal static (string Season, string Id, FgRole Role, bool Team, int Variant, string Tag)? ParseAddress(string address)
	{
		Match match = Addr.Match(address);
		if (!match.Success)
		{
			return null;
		}
		string value = match.Groups["id"].Value;
		string value2 = match.Groups["file"].Value;
		string text = (value2.StartsWith(value + "_", StringComparison.Ordinal) ? value2.Substring(value.Length + 1) : value2);
		Match match2 = Rest.Match(text);
		if (!match2.Success)
		{
			return (match.Groups["season"].Value, value, FgRole.Other, false, 0, text);
		}
		FgRole item = match2.Groups["role"].Value switch
		{
			"Top" => FgRole.Top, 
			"Bottom" => FgRole.Bottom, 
			"Full" => FgRole.Full, 
			"LOD_OPT" => FgRole.LodOpt, 
			_ => FgRole.Lod, 
		};
		int item2 = (match2.Groups["var"].Success ? ((match2.Groups["var"].Value.Length <= 0) ? 1 : int.Parse(match2.Groups["var"].Value)) : 0);
		return (match.Groups["season"].Value, value, item, match2.Groups["team"].Success || match2.Groups["team2"].Success, item2, "");
	}

	public string Dump()
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(45, 2, stringBuilder2);
		handler.AppendLiteral("Drip Swapper costume list: ");
		handler.AppendFormatted(Costumes.Count);
		handler.AppendLiteral(" costumes, ");
		handler.AppendFormatted(Emotes.Count);
		handler.AppendLiteral(" emotes");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("Format: season | internal id | name shown in the app | pieces");
		stringBuilder.AppendLine();
		foreach (FgCostume item in Costumes.OrderBy((FgCostume c) => c.Season, StringComparer.Ordinal).ThenBy((FgCostume c) => c.Id, StringComparer.Ordinal))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 4, stringBuilder2);
			handler.AppendFormatted(item.Season);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(item.Id);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(item.Name);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(string.Join(", ", item.Pieces.Select((FgPiece p) => p.Asset.FileName.Replace(".prefab", "")).Take(8)));
			stringBuilder4.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	public FgCostume? Find(string key)
	{
		return _byKey.GetValueOrDefault(key);
	}

	public IEnumerable<FgCostume> Search(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Costumes;
		}
		string[] words = text.Split(new char[3] { ' ', '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
		return Costumes.Where((FgCostume c) => words.All((string w) => c.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || c.Season.Replace('_', '.').Contains(w, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(w, StringComparison.OrdinalIgnoreCase) || c.Pieces.Any((FgPiece p) => p.Asset.FileName.Contains(w, StringComparison.OrdinalIgnoreCase))));
	}

	public static string Pretty(string id)
	{
		string text = (id.StartsWith("CH_", StringComparison.Ordinal) ? id.Substring(3) : id);
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '_')
			{
				stringBuilder.Append(' ');
				continue;
			}
			if (i > 0 && char.IsUpper(c) && (char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsLower(text[i + 1]) && char.IsUpper(text[i - 1]))))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString();
	}

	public static List<FgSwapPair> Plan(FgCostume target, FgCostume source, bool includeTop = true, bool includeBottom = true)
	{
		List<FgSwapPair> list = new List<FgSwapPair>();
		foreach (FgPiece piece in target.Pieces)
		{
			if ((piece.Role != FgRole.Top || includeTop) && (piece.Role != FgRole.Bottom || includeBottom))
			{
				FgPiece fgPiece = Pick(source, piece);
				if (fgPiece != null)
				{
					list.Add(new FgSwapPair(piece.Asset, fgPiece.Asset, piece.Slot));
				}
			}
		}
		return list;
	}

	private static FgPiece? Pick(FgCostume source, FgPiece t)
	{
		if (t.Role == FgRole.Other)
		{
			return source.Pieces.FirstOrDefault((FgPiece p) => p.Role == FgRole.Other && p.Tag == t.Tag);
		}
		(bool, int)[] array = new (bool, int)[4]
		{
			(t.Team, t.Variant),
			(t.Team, 0),
			(false, t.Variant),
			(false, 0)
		};
		for (int num = 0; num < array.Length; num++)
		{
			(bool, int) tuple = array[num];
			bool item = tuple.Item1;
			int item2 = tuple.Item2;
			FgPiece fgPiece = Find(t.Role, item, item2);
			if (fgPiece != null)
			{
				return fgPiece;
			}
		}
		if (t.Role == FgRole.Top)
		{
			array = new (bool, int)[3]
			{
				(t.Team, t.Variant),
				(t.Team, 0),
				(false, 0)
			};
			for (int num = 0; num < array.Length; num++)
			{
				(bool, int) tuple2 = array[num];
				bool item3 = tuple2.Item1;
				int item4 = tuple2.Item2;
				FgPiece fgPiece2 = Find(FgRole.Full, item3, item4);
				if (fgPiece2 != null)
				{
					return fgPiece2;
				}
			}
		}
		FgRole role = t.Role;
		if ((uint)(role - 3) <= 1u)
		{
			FgPiece fgPiece3 = Find((t.Role == FgRole.Lod) ? FgRole.LodOpt : FgRole.Lod, team: false, 0) ?? Find(t.Role, team: false, 0);
			if (fgPiece3 != null)
			{
				return fgPiece3;
			}
		}
		return null;
		FgPiece? Find(FgRole fgRole, bool team, int variant)
		{
			return source.Pieces.FirstOrDefault((FgPiece p) => p.Role == fgRole && p.Team == team && p.Variant == variant);
		}
	}
}
