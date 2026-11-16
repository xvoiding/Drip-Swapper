using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drip.Core.Cosmetics;

public static class CosmeticRank
{
	public const int RarestPinned = 48;

	public static readonly IReadOnlyDictionary<string, string[]> CommunityWanted = new Dictionary<string, string[]>
	{
		["Outfit"] = new string[34]
		{
			"Renegade Raider", "Black Knight", "Skull Trooper", "Ghoul Trooper", "Travis Scott", "Aerial Assault Trooper", "Galaxy", "Wonder", "Sparkle Specialist", "Royale Knight",
			"Omega", "Carbide", "Eon", "The Reaper", "John Wick", "Leviathan", "Raven", "Drift", "Blue Squire", "Red Knight",
			"Fishstick", "Peely", "Ikonik", "Mancake", "Catalyst", "Midas", "Lynx", "Meowscles", "Jonesy the First", "Brite Bomber",
			"Pickle Rick", "Darth Vader", "Kratos", "SypherPK"
		},
		["Pickaxe"] = new string[25]
		{
			"Reaper", "Raider's Revenge", "The Axe of Champions", "AC/DC", "Candy Axe", "Disco Brawl", "Pink Flamingo", "Death Valley", "Dragon Axe", "Plunja",
			"Tat Axe", "Axeroni", "Anarchy Axe", "Silver Fang", "Spectral Axe", "Pulse Axe", "Axecalibur", "Cliffhanger", "Close Shave", "Tooth Pick",
			"Spiky", "Lollipopper", "Pick Squeak", "Trusty No. 2", "Rainbow Smash"
		},
		["Backpack"] = new string[24]
		{
			"Black Shield", "Royale Shield", "Red Shield", "Squire Shield", "Dark Void", "Dark Matter", "Raptor Satchel", "Backup Plan", "Cuddle Bow", "Hatchling",
			"Love Wings", "Royale Flags", "Scaly", "Six String", "Skirmish", "Iron Cage", "Fish Tank", "Dark Wings", "Pursuit", "Catalyst",
			"Battle Shroud", "Crested Cape", "Last Gasp", "Offworld Rig"
		},
		["Emote"] = new string[39]
		{
			"Floss", "Take The L", "Orange Justice", "Hype", "Dab", "Best Mates", "Tidy", "Billy Bounce", "Pump Up the Jam", "Ride The Pony",
			"Rage", "Electro Shuffle", "Boogie Down", "Zany", "Fresh", "Smooth Moves", "Gangnam Style", "Out West", "Hot Marat", "Poki",
			"Infinite Dab", "Rambunctious", "Get Griddy", "The Worm", "Wiggle", "Breakin'", "Disco Fever", "Flapper", "Boneless", "Popcorn",
			"True Heart", "Rock Out", "Squat Kick", "The Robot", "Baller", "Hula", "Breakdown", "Bombastic", "Pop Lock"
		},
		["Glider"] = new string[26]
		{
			"Mako", "Aerial Assault One", "Renegade Rider", "Sky Serpent", "Sunrise", "Royale Dragon", "Rainbow Rider", "Cloud Strike", "Cozy Coaster", "Bear Force One",
			"Prismatic", "Voyager", "Zephyr", "Half Shell", "Gum Drop", "Get Down!", "Deep Space Lander", "Orbital Shuttle", "Planetary Probe", "High Octane",
			"Pterodactyl", "Flying Saucer", "Hot Ride", "Laser Chomp", "Frostwing", "Founder's Umbrella"
		}
	};

	private static string Norm(string text)
	{
		return new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
	}

	public static int SeasonIndex(string? introduced)
	{
		Match match = Regex.Match(introduced ?? "", "^C(\\d+)S(\\d+)$");
		if (!match.Success)
		{
			return int.MaxValue;
		}
		return (int.Parse(match.Groups[1].Value) - 1) * 10 + int.Parse(match.Groups[2].Value);
	}

	public static int Score(Cosmetic c)
	{
		int num = SeasonIndex(c.Introduced);
		int num2;
		if (num <= 10)
		{
			num2 = 4;
		}
		else if (num <= 20)
		{
			num2 = 3;
		}
		else
		{
			num2 = ((num <= 30) ? 2 : ((num <= 40) ? 1 : 0));
		}
		string text = (c.Rarity ?? "").ToLowerInvariant();
		int num3;
		if (text.Contains("series") && !text.Contains("crew"))
		{
			num3 = 4;
		}
		else if (text.Contains("legendary") || text.Contains("mythic"))
		{
			num3 = 4;
		}
		else
		{
			num3 = (text.Contains("epic") ? 2 : (text.Contains("rare") ? 1 : 0));
		}
		return num2 + num3;
	}

	private static bool Placeholder(Cosmetic c)
	{
		if (!c.Id.Contains("Default", StringComparison.OrdinalIgnoreCase) && !c.Id.Contains("Random", StringComparison.OrdinalIgnoreCase) && !c.Name.Contains("TBD", StringComparison.OrdinalIgnoreCase) && !c.Name.Contains("Test", StringComparison.OrdinalIgnoreCase))
		{
			return c.Name.Equals("HIDDEN", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static List<Cosmetic> Rank(string catKey, IReadOnlyList<Cosmetic> newestFirst, IEnumerable<string>? extraWanted = null)
	{
		HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<Cosmetic> list = new List<Cosmetic>(newestFirst.Count);
		IEnumerable<string> enumerable = (CommunityWanted.TryGetValue(catKey, out string[] value) ? value : Array.Empty<string>()).Concat(extraWanted ?? Array.Empty<string>());
		Dictionary<string, Cosmetic> dictionary = new Dictionary<string, Cosmetic>();
		Dictionary<string, Cosmetic> dictionary2 = new Dictionary<string, Cosmetic>(StringComparer.OrdinalIgnoreCase);
		foreach (Cosmetic item in newestFirst)
		{
			dictionary2.TryAdd(item.Id, item);
			if (!Placeholder(item))
			{
				dictionary.TryAdd(Norm(item.Name), item);
			}
		}
		foreach (string item2 in enumerable)
		{
			Cosmetic cosmetic;
			if (dictionary2.TryGetValue(item2, out var value2))
			{
				cosmetic = value2;
			}
			else
			{
				cosmetic = (dictionary.TryGetValue(Norm(item2), out var value3) ? value3 : null);
			}
			if ((object)cosmetic != null && taken.Add(cosmetic.Id))
			{
				list.Add(cosmetic);
			}
		}
		foreach (Cosmetic item3 in newestFirst.Where((Cosmetic c) => !Placeholder(c) && !taken.Contains(c.Id)).OrderByDescending(Score).ThenBy((Cosmetic c) => SeasonIndex(c.Introduced))
			.ThenBy((Cosmetic c) => c.Name, StringComparer.OrdinalIgnoreCase)
			.Take(48))
		{
			if (taken.Add(item3.Id))
			{
				list.Add(item3);
			}
		}
		foreach (Cosmetic item4 in newestFirst)
		{
			if (taken.Add(item4.Id))
			{
				list.Add(item4);
			}
		}
		return list;
	}
}
