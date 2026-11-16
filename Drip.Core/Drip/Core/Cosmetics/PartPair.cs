namespace Drip.Core.Cosmetics;

public sealed record PartPair(string Slot, string SourcePackage, string TargetPackage, ulong SourceChunk = 0uL, ulong TargetChunk = 0uL)
{
	public string SourceLeaf => Leaf(SourcePackage);

	public string TargetLeaf => Leaf(TargetPackage);

	private static string Leaf(string p)
	{
		return LeafOf(p);
	}

	public static string LeafOf(string p)
	{
		int num = p.LastIndexOf('/');
		if (num < 0)
		{
			return p;
		}
		return p.Substring(num + 1);
	}
}
