using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Drip.App;

public static class RarityBrushes
{
	private static readonly Dictionary<string, Brush> Map = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
	{
		["Common"] = Make("#9AA0A6"),
		["Uncommon"] = Make("#60AA3A"),
		["Rare"] = Make("#37A8FF"),
		["Epic"] = Make("#B15BE2"),
		["Legendary"] = Make("#F3AF19"),
		["Mythic"] = Make("#F5D547"),
		["Exotic"] = Make("#7CE8F6"),
		["Icon Series"] = Make("#2BD6D0"),
		["Gaming Legends Series"] = Make("#7A6CF0"),
		["Marvel Series"] = Make("#EE3E3E"),
		["DC Series"] = Make("#4C7DFF"),
		["Star Wars Series"] = Make("#F2D13B"),
		["Dark Series"] = Make("#E040FB"),
		["Shadow Series"] = Make("#8A8F98"),
		["Lava Series"] = Make("#FF7A2B"),
		["Frozen Series"] = Make("#8FD8FF"),
		["Slurp Series"] = Make("#3DD6E2")
	};

	private static readonly Brush Default = Make("#6B7280");

	private static Brush Make(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Brush For(string rarity)
	{
		if (!Map.TryGetValue(rarity, out Brush value))
		{
			return Default;
		}
		return value;
	}
}
