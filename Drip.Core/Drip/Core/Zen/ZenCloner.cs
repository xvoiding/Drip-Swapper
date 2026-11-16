using System.IO;

namespace Drip.Core.Zen;

public static class ZenCloner
{
	public static void CloneAs(ZenPackage target, string targetPackage, ZenPackage source, string sourcePackage)
	{
		Rename(target, targetPackage, source, sourcePackage);
		string text = Leaf(targetPackage);
		string text2 = Leaf(sourcePackage);
		int num = FindWithClassSuffix(target, text, out string found);
		int num2 = FindWithClassSuffix(source, text2, out string found2);
		if (num < 0)
		{
			throw new InvalidDataException($"Target package has no name entry for '{text}' (or '{text}_C').");
		}
		if (num2 < 0)
		{
			throw new InvalidDataException($"Source package has no name entry for '{text2}' (or '{text2}_C').");
		}
		int num3 = target.FindExportByName(found);
		int num4 = source.FindExportByName(found2);
		target.CopyNameFrom(num, source, num2);
		if (num3 < 0)
		{
			num3 = target.ExportCount - 1;
		}
		if (num4 < 0)
		{
			num4 = source.ExportCount - 1;
		}
		if (target.ExportCount == 0 || source.ExportCount == 0)
		{
			throw new InvalidDataException("Package has no exports.");
		}
		target.SetExportPublicHash(num3, source.GetExportPublicHash(num4));
	}

	private static void Rename(ZenPackage target, string targetPackage, ZenPackage source, string sourcePackage)
	{
		int num = target.FindName(targetPackage);
		int num2 = source.FindName(sourcePackage);
		if (num < 0)
		{
			throw new InvalidDataException("Target name map has no entry '" + targetPackage + "'.");
		}
		if (num2 < 0)
		{
			throw new InvalidDataException("Source name map has no entry '" + sourcePackage + "'.");
		}
		target.CopyNameFrom(num, source, num2);
	}

	private static int FindWithClassSuffix(ZenPackage p, string leaf, out string found)
	{
		found = leaf;
		int num = p.FindName(leaf);
		if (num >= 0)
		{
			return num;
		}
		found = leaf + "_C";
		return p.FindName(found);
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
}
