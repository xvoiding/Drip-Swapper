using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.FallGuys;

public sealed class FgCostume
{
	public string Season { get; init; } = "";

	public string Id { get; init; } = "";

	public string Name { get; init; } = "";

	public List<FgPiece> Pieces { get; } = new List<FgPiece>();

	public string Key => Season + "/" + Id;

	public string Display => Name + "  ·  " + SeasonLabel;

	public string SeasonLabel => Season.Replace('_', '.');

	public bool HasTop => Pieces.Any((FgPiece p) => p.Role == FgRole.Top);

	public bool HasBottom => Pieces.Any((FgPiece p) => p.Role == FgRole.Bottom);

	public bool HasFull => Pieces.Any((FgPiece p) => p.Role == FgRole.Full);

	public int VariantCount => (from p in Pieces
		where p.Variant > 0
		select p.Variant).Distinct().Count();

	public string Kind
	{
		get
		{
			if (!HasFull)
			{
				if (!HasTop || !HasBottom)
				{
					if (!HasTop)
					{
						if (!HasBottom)
						{
							return "Other";
						}
						return "Bottom";
					}
					return "Top";
				}
				return "Top + Bottom";
			}
			return "Full body";
		}
	}

	public override string ToString()
	{
		return Display;
	}
}
