using System;
using System.Collections.Generic;

namespace Drip.Core.Retrac;

public static class RetracPaths
{
	private static readonly Dictionary<string, string[]> Folders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["outfit"] = new string[1] { "Characters" },
		["backpack"] = new string[1] { "BackPacks" },
		["pickaxe"] = new string[1] { "Pickaxes" },
		["glider"] = new string[1] { "Gliders" },
		["emote"] = new string[5] { "Dances", "Dances/PapayaComms", "Dances/HolidayCracker", "VictoryPoses", "ConsumableEmotes" },
		["wrap"] = new string[1] { "ItemWraps" },
		["contrail"] = new string[1] { "Contrails" },
		["spray"] = new string[1] { "Sprays" },
		["emoji"] = new string[1] { "Dances/Emoji" },
		["loadingscreen"] = new string[1] { "LoadingScreens" },
		["music"] = new string[2] { "MusicPacks", "MusicPacks/StW" },
		["toy"] = new string[1] { "Toys" },
		["petcarrier"] = new string[2] { "PetCarriers", "Pets" },
		["shoe"] = new string[1] { "Shoes" }
	};

	public static IEnumerable<string> Candidates(string id, string type)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (Folders.TryGetValue(type, out string[] value))
		{
			string[] array = value;
			foreach (string text in array)
			{
				if (seen.Add(text))
				{
					yield return "/Game/Athena/Items/Cosmetics/" + text + "/" + id;
				}
			}
		}
		foreach (string[] value2 in Folders.Values)
		{
			string[] array = value2;
			foreach (string text2 in array)
			{
				if (seen.Add(text2))
				{
					yield return "/Game/Athena/Items/Cosmetics/" + text2 + "/" + id;
				}
			}
		}
	}
}
