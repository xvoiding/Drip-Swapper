using System;
using System.Collections.Generic;
using System.Linq;
using Drip.Core.Zen;

namespace Drip.Core.Swapping;

public static class StyleTweaks
{
	public static int Apply(ZenPackage pkg, IEnumerable<StyleRename> renames)
	{
		int num = 0;
		foreach (StyleRename rename in renames)
		{
			num += Replace(pkg, rename.OldPackage, rename.NewPackage);
			num += Replace(pkg, Leaf(rename.OldPackage), Leaf(rename.NewPackage));
		}
		return num;
	}

	private static int Replace(ZenPackage pkg, string oldValue, string newValue)
	{
		if (oldValue.Length == 0 || string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < pkg.Names.Count; i++)
		{
			if (string.Equals(pkg.Names[i].Value, oldValue, StringComparison.OrdinalIgnoreCase))
			{
				pkg.ReplaceName(i, newValue);
				num++;
			}
		}
		return num;
	}

	public static string Leaf(string package)
	{
		int num = package.LastIndexOf('/');
		if (num < 0)
		{
			return package;
		}
		return package.Substring(num + 1);
	}

	public static string SlotOf(string package)
	{
		string text = Leaf(package);
		string[] array = new string[11]
		{
			"FaceAcc", "Head", "Body", "Hat", "Hair", "Backpack", "Charm", "Tail", "Gunspec", "Shoes",
			"Gloves"
		};
		foreach (string text2 in array)
		{
			if (text.Contains(text2, StringComparison.OrdinalIgnoreCase))
			{
				return text2;
			}
		}
		return "?";
	}

	public static List<StyleRename> Pair(IReadOnlyCollection<string> defaults, IReadOnlyCollection<string> styleParts)
	{
		List<StyleRename> list = new List<StyleRename>();
		HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string sp in styleParts)
		{
			if (defaults.Contains(sp, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}
			string slot = SlotOf(sp);
			List<string> list2 = defaults.Where((string d) => !used.Contains(d) && SlotOf(d) == slot).ToList();
			if (list2.Count != 0)
			{
				string text = list2.OrderByDescending((string d) => CommonPrefix(Leaf(d), Leaf(sp))).First();
				used.Add(text);
				list.Add(new StyleRename(text, sp));
			}
		}
		return list;
	}

	private static int CommonPrefix(string a, string b)
	{
		int i;
		for (i = 0; i < a.Length && i < b.Length && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i]); i++)
		{
		}
		return i;
	}
}
