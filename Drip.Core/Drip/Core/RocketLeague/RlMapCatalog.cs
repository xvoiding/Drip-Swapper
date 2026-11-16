using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.RocketLeague;

public static class RlMapCatalog
{
	private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Stadium_P"] = "DFH Stadium",
		["Park_P"] = "Beckwith Park",
		["Wasteland_P"] = "Wasteland",
		["Labs_Circle_P"] = "Pillars (Labs)",
		["Park_Night_P"] = "Beckwith Park (Night)",
		["Park_Rainy_P"] = "Beckwith Park (Stormy)",
		["Park_Snowy_P"] = "Beckwith Park (Snowy)",
		["Wasteland_Night_P"] = "Wasteland (Night)",
		["Utopia_Retro_P"] = "Utopia Retro",
		["EuroStadium_P"] = "Mannfield",
		["EuroStadium_Night_P"] = "Mannfield (Night)",
		["EuroStadium_Rainy_P"] = "Mannfield (Stormy)",
		["HoopsStadium_P"] = "Dunk House",
		["Underwater_P"] = "Aquadome",
		["TrainStation_P"] = "Urban Central",
		["Stadium_Foggy_P"] = "DFH Stadium (Stormy)",
		["Stadium_Day_P"] = "DFH Stadium (Day)",
		["Stadium_Winter_P"] = "DFH Stadium (Snowy)",
		["Stadium_Race_Day_P"] = "DFH Stadium (Circuit)",
		["TrainStation_Night_P"] = "Urban Central (Night)",
		["TrainStation_Dawn_P"] = "Urban Central (Dawn)",
		["UtopiaStadium_P"] = "Utopia Coliseum",
		["UtopiaStadium_Dusk_P"] = "Utopia Coliseum (Dusk)",
		["UtopiaStadium_Snow_P"] = "Utopia Coliseum (Snowy)",
		["NeoTokyo_Standard_P"] = "Neo Tokyo",
		["NeoTokyo_Toon_P"] = "Neo Tokyo (Comic)",
		["cs_P"] = "Champions Field",
		["cs_Day_P"] = "Champions Field (Day)",
		["cs_HW_P"] = "Champions Field (Nike FC)",
		["ARC_Standard_P"] = "Starbase ARC",
		["ARC_Darc_P"] = "Starbase ARC (Aftermath)",
		["Beach_P"] = "Salty Shores",
		["Beach_Night_P"] = "Salty Shores (Night)",
		["Beach_Night_GRS_P"] = "Salty Shores (Night)",
		["CHN_Stadium_P"] = "Forbidden Temple",
		["CHN_Stadium_Day_P"] = "Forbidden Temple (Day)",
		["Farm_P"] = "Farmstead",
		["Farm_Night_P"] = "Farmstead (Night)",
		["Farm_UpsideDown_P"] = "Farmstead (Upside Down)",
		["Outlaw_P"] = "Deadeye Canyon",
		["Outlaw_Oasis_P"] = "Deadeye Canyon (Oasis)",
		["Street_P"] = "Sovereign Heights",
		["Wasteland_Night_S_P"] = "Wasteland (Night)"
	};

	public static bool IsListed(string file)
	{
		string stem = Path.GetFileNameWithoutExtension(file);
		if (Known.ContainsKey(stem) && !stem.StartsWith("Utopia_Retro", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return new string[15]
		{
			"Stadium_", "EuroStadium_", "Park_", "TrainStation_", "UtopiaStadium_", "Wasteland", "NeoTokyo_", "cs_", "ARC_", "Beach_",
			"CHN_Stadium", "Farm_", "Outlaw_", "Street_", "HoopsStadium"
		}.Any((string f) => stem.StartsWith(f, StringComparison.OrdinalIgnoreCase));
	}

	public static string Pretty(string file)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
		if (Known.TryGetValue(fileNameWithoutExtension, out string value))
		{
			return value;
		}
		string text;
		if (!fileNameWithoutExtension.EndsWith("_P", StringComparison.OrdinalIgnoreCase))
		{
			text = fileNameWithoutExtension;
		}
		else
		{
			string text2 = fileNameWithoutExtension;
			text = text2.Substring(0, text2.Length - 2);
		}
		string text3 = text;
		bool flag = text3.StartsWith("Labs_", StringComparison.OrdinalIgnoreCase);
		if (flag)
		{
			text3 = text3.Substring(5);
		}
		text3 = text3.Replace('_', ' ').Trim();
		if (!flag)
		{
			return text3;
		}
		return text3 + " (Labs)";
	}

	public static IEnumerable<string> Discover(IEnumerable<string> files)
	{
		return (from n in files.Select(Path.GetFileName)
			where n != null && n.EndsWith("_P.upk", StringComparison.OrdinalIgnoreCase) && !n.Contains("_T_", StringComparison.OrdinalIgnoreCase) && IsListed(n)
			select (n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string n) => Pretty(n), StringComparer.OrdinalIgnoreCase);
	}
}
