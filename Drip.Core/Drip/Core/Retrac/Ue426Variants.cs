using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.Retrac;

public static class Ue426Variants
{
	public sealed record Option(string Tag, List<(string Swap, string Use)> Materials);

	private const string TagPrefix = "Cosmetics.Variant.Property.";

	public static List<Option> Parse(Ue426Package p)
	{
		List<Option> list = new List<Option>();
		IReadOnlyList<string> names = p.Names;
		ReadOnlySpan<byte> exportBytes = p.ExportBytes;
		foreach (var item4 in p.ExportRanges())
		{
			int item = item4.Start;
			int item2 = item4.Size;
			string item3 = item4.Name;
			if (!item3.Contains("Variant", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int num = item + item2;
			List<(int, string, string)> list2 = new List<(int, string, string)>();
			List<(int, string)> list3 = new List<(int, string)>();
			List<int> list4 = new List<int>();
			int num2 = item;
			while (num2 + 8 <= num)
			{
				string text = NameAt(exportBytes, names, num2);
				if (text == null)
				{
					num2++;
					continue;
				}
				if (text.StartsWith("Cosmetics.Variant.Property.", StringComparison.Ordinal))
				{
					list3.Add((num2, text.Substring("Cosmetics.Variant.Property.".Length)));
					num2 += 8;
					continue;
				}
				if (IsAsset(text) && text.StartsWith("/Game/UI/", StringComparison.OrdinalIgnoreCase))
				{
					list4.Add(num2);
					num2 += 8;
					continue;
				}
				if (IsAsset(text) && num2 + 20 <= num && BinaryPrimitives.ReadInt32LittleEndian(exportBytes.Slice(num2 + 8)) == 0)
				{
					string text2 = NameAt(exportBytes, names, num2 + 12);
					if (text2 != null && (text2 == "None" || IsAsset(text2)))
					{
						string text3 = Package(text);
						string text4 = ((text2 == "None") ? "" : Package(text2));
						if (!IsPart(text3) && (text4.Length == 0 || !IsPart(text4)))
						{
							list2.Add((num2, text3, text4));
						}
						num2 += 20;
						continue;
					}
				}
				num2++;
			}
			int last = -1;
			foreach (var item5 in list3)
			{
				var (at, tag) = item5;
				if (!list4.Any((int i) => i - at > 0 && i - at <= 90))
				{
					continue;
				}
				if (list.Any((Option r) => r.Tag == tag))
				{
					last = at;
					continue;
				}
				list.Add(new Option(tag, list2.Where(((int At, string Swap, string Use) x) => x.At > last && x.At < at && x.Use.Length > 0 && !x.Use.Equals(x.Swap, StringComparison.OrdinalIgnoreCase)).Select(((int At, string Swap, string Use) x) => (Swap: x.Swap, Use: x.Use)).ToList()));
				last = at;
			}
		}
		return list;
	}

	public static Option? Pick(List<Option> options, string tag, string styleName)
	{
		List<string> words = (from w in styleName.Split(new char[5] { ' ', '-', '_', '&', '/' }, StringSplitOptions.RemoveEmptyEntries)
			select w.ToLowerInvariant()).Where((string w) =>
		{
			if (w.Length >= 3)
			{
				switch (w)
				{
				default:
					return !(w == "the");
				case "style":
				case "default":
				case "original":
				case "stage":
					return false;
				}
			}
			return false;
		}).ToList();
		if (words.Count > 0)
		{
			List<(Option, int)> list = (from o in options
				where o.Materials.Count > 0
				select (o: o, score: Enumerable.Count(words, (string w) => o.Materials.Any(((string Swap, string Use) m) => m.Use.Contains(w, StringComparison.OrdinalIgnoreCase)))) into x
				where x.score > 0
				orderby x.score descending
				select x).ToList();
			if (list.Count == 1 || (list.Count > 1 && list[0].Item2 > list[1].Item2))
			{
				return list[0].Item1;
			}
		}
		return options.FirstOrDefault((Option o) => o.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
	}

	private static string? NameAt(ReadOnlySpan<byte> data, IReadOnlyList<string> names, int o)
	{
		uint num = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(o));
		if (num >= names.Count || BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(o + 4)) != 0)
		{
			return null;
		}
		return names[(int)num];
	}

	private static bool IsAsset(string n)
	{
		if (!n.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return n.Substring(n.LastIndexOf('/') + 1).Contains('.');
	}

	private static string Package(string asset)
	{
		return asset.Substring(0, asset.LastIndexOf('.'));
	}

	private static bool IsPart(string package)
	{
		if (!package.Contains("/CharacterParts/", StringComparison.OrdinalIgnoreCase))
		{
			return package.Substring(package.LastIndexOf('/') + 1).StartsWith("CP_", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
