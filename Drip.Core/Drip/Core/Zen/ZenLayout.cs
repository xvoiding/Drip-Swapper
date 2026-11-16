using System.Collections.Generic;

namespace Drip.Core.Zen;

public readonly record struct ZenLayout(int Exports, int BundleEntries, int Imports, int PackageImports, int PublicHashes)
{
	public override string ToString()
	{
		return $"{Exports} exports · {BundleEntries} bundle entries · {PackageImports} package refs · {Imports} imports";
	}

	public static List<string> Differences(ZenLayout original, ZenLayout replacement)
	{
		List<string> list = new List<string>();
		if (original.Exports != replacement.Exports)
		{
			list.Add($"exports {original.Exports} vs {replacement.Exports}");
		}
		if (original.BundleEntries != replacement.BundleEntries)
		{
			list.Add($"load steps {original.BundleEntries} vs {replacement.BundleEntries}");
		}
		if (original.PackageImports != replacement.PackageImports)
		{
			list.Add($"linked assets {original.PackageImports} vs {replacement.PackageImports}");
		}
		if (original.PublicHashes != replacement.PublicHashes)
		{
			list.Add($"public refs {original.PublicHashes} vs {replacement.PublicHashes}");
		}
		return list;
	}
}
