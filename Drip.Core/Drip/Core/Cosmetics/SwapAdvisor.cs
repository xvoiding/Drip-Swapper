using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.Cosmetics;

public static class SwapAdvisor
{
	public static string SlotWord(string slot)
	{
		string text = slot.ToLowerInvariant();
		switch (text)
		{
		case "hat":
			return "hat / headgear";
		case "head":
			return "head";
		case "body":
			return "body";
		case "faceacc":
		case "faceaccessory":
			return "face accessory";
		case "backpack":
			return "back bling";
		case "charm":
			return "charm";
		case "gloves":
			return "gloves";
		case "unknown":
		case "":
			return "extra";
		default:
			return text;
		}
	}

	public static List<string> ForOutfit(string ownedName, string wantedName, IReadOnlyCollection<CharacterPart> owned, IReadOnlyCollection<CharacterPart> wanted, bool wholeDefinition)
	{
		List<string> list = new List<string>();
		HashSet<string> oSlots = owned.Select((CharacterPart p) => p.Slot).ToHashSet(StringComparer.OrdinalIgnoreCase);
		HashSet<string> wSlots = wanted.Select((CharacterPart p) => p.Slot).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (string slot in oSlots.Where((string s) => !wSlots.Contains(s)).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			int num = Enumerable.Count(owned, (CharacterPart p) => p.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase));
			list.Add($"{ownedName} has {((num > 1) ? $"{num} " : "a ")}{SlotWord(slot)} piece{((num > 1) ? "s" : "")} that {wantedName} doesn't, so {((num > 1) ? "they aren't" : "it isn't")} swapped and will stay on the new look.");
		}
		foreach (string item5 in wSlots.Where((string s) => !oSlots.Contains(s)).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			list.Add($"{wantedName} has a {SlotWord(item5)} piece that {ownedName} has no spot for, so you won't see it.");
		}
		CharacterPart characterPart = owned.FirstOrDefault((CharacterPart p) => p.Slot.Equals("Body", StringComparison.OrdinalIgnoreCase));
		CharacterPart characterPart2 = wanted.FirstOrDefault((CharacterPart p) => p.Slot.Equals("Body", StringComparison.OrdinalIgnoreCase));
		if ((object)characterPart != null && (object)characterPart2 != null)
		{
			(string Gender, string Size) tuple = BodyType.Of(characterPart.Package);
			string item = tuple.Gender;
			string item2 = tuple.Size;
			(string Gender, string Size) tuple2 = BodyType.Of(characterPart2.Package);
			string item3 = tuple2.Gender;
			string item4 = tuple2.Size;
			bool num2 = item.Length > 0 && item3.Length > 0 && item != item3;
			bool flag = item2.Length > 0 && item4.Length > 0 && item2 != item4;
			if (num2 | flag)
			{
				list.Add($"{ownedName} is a {Desc(item, item2)} body and {wantedName} is a {Desc(item3, item4)} body, so the fit can look off (stretched or clipping).");
			}
		}
		if (wholeDefinition)
		{
			list.Add("This one swaps the whole skin definition instead of just its pieces, so the Locker may show the other skin's name or styles.");
		}
		return list;
	}

	public static List<string> ForOther(string kind, bool layoutDiffers)
	{
		List<string> list = new List<string>();
		if (layoutDiffers)
		{
			list.Add("These two " + kind.ToLowerInvariant() + "s are built differently, which can crash Fortnite on launch. Revert it if the game won't start.");
		}
		return list;
	}

	private static string Desc(string gender, string size)
	{
		string text = string.Join(" ", new string[2] { size, gender }.Where((string x) => x.Length > 0)).Trim();
		if (text == null || text.Length <= 0)
		{
			return "different";
		}
		return text;
	}
}
