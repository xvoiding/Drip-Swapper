using System;
using System.Text;
using System.Text.RegularExpressions;
using CUE4Parse.Utils;

namespace Drip.Core.Swapping;

public static class PackageNames
{
	private static readonly Regex PluginContent = new Regex("^FortniteGame/Plugins/(?:.+/)?([^/]+)/Content/(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static string Normalize(string input)
	{
		string text = input.Trim().Replace('\\', '/');
		if (text.Length == 0)
		{
			throw new ArgumentException("Empty package path.");
		}
		int val = text.LastIndexOf('/');
		int num = text.IndexOf('.', Math.Max(val, 0));
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		Match match = PluginContent.Match(text);
		if (match.Success)
		{
			return "/" + match.Groups[1].Value + "/" + match.Groups[2].Value;
		}
		if (text.StartsWith("FortniteGame/Content/", StringComparison.OrdinalIgnoreCase))
		{
			return "/Game/" + text.Substring("FortniteGame/Content/".Length);
		}
		if (text.StartsWith("Game/", StringComparison.OrdinalIgnoreCase))
		{
			return "/" + text;
		}
		if (!text.StartsWith('/'))
		{
			return "/" + text;
		}
		return text;
	}

	public static ulong ChunkId(string packageName)
	{
		return CityHash.CityHash64(Encoding.Unicode.GetBytes(packageName.ToLowerInvariant()));
	}
}
