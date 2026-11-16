using System.Collections.Generic;

namespace Drip.Core.Cosmetics;

public sealed record Cosmetic(string Id, string Name, string Rarity, string IconUrl, string Introduced, string Added = "", IReadOnlyList<CosmeticStyle>? Styles = null)
{
	public bool HasStyles
	{
		get
		{
			IReadOnlyList<CosmeticStyle> styles = Styles;
			if (styles != null)
			{
				return styles.Count > 0;
			}
			return false;
		}
	}

	public override string ToString()
	{
		return Name + "  (" + Id + ")";
	}
}
