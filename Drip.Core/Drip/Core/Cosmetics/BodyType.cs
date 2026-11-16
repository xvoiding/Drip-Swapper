using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drip.Core.Cosmetics;

public static class BodyType
{
	private static readonly Regex Folder = new Regex("/(Male|Female)/(Tiny|Small|Medium|Large|Huge)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex Leaf = new Regex("(?:^|_)(M|F)_(TNY|SML|MED|LRG|HUGE)(?:_|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex GenderOnly = new Regex("(?:^|_)(M|F)_", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static (string Gender, string Size) Of(string package)
	{
		Match match = Folder.Match(package);
		if (match.Success)
		{
			return (Gender: match.Groups[1].Value.ToLowerInvariant(), Size: match.Groups[2].Value.ToLowerInvariant());
		}
		string input = package.Substring(package.LastIndexOf('/') + 1);
		Match match2 = Leaf.Match(input);
		if (match2.Success)
		{
			string item = (match2.Groups[1].Value.Equals("M", StringComparison.OrdinalIgnoreCase) ? "male" : "female");
			return (Gender: item, Size: match2.Groups[2].Value.ToUpperInvariant() switch
			{
				"TNY" => "tiny", 
				"SML" => "small", 
				"MED" => "medium", 
				"LRG" => "large", 
				_ => "huge", 
			});
		}
		Match match3 = GenderOnly.Match(input);
		if (!match3.Success)
		{
			return (Gender: "", Size: "");
		}
		return (Gender: match3.Groups[1].Value.Equals("M", StringComparison.OrdinalIgnoreCase) ? "male" : "female", Size: "");
	}

	public static string? Mismatch(string basePart, string donorPart)
	{
		(string Gender, string Size) tuple = Of(basePart);
		string item = tuple.Gender;
		string item2 = tuple.Size;
		(string Gender, string Size) tuple2 = Of(donorPart);
		string item3 = tuple2.Gender;
		string item4 = tuple2.Size;
		bool num = item.Length > 0 && item3.Length > 0 && item != item3;
		bool flag = item2.Length > 0 && item4.Length > 0 && item2 != item4;
		if (num || flag)
		{
			return $"that part is made for a {Desc(item3, item4)} body but the skin it goes on is a {Desc(item, item2)} body, so it would not attach (you'd see no head).";
		}
		return null;
		static string Desc(string gender, string size)
		{
			return string.Join(" ", new string[2] { size, gender }.Where((string x) => x.Length > 0));
		}
	}
}
