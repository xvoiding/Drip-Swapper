using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drip.Core.Install;

public static class FortniteLogCheck
{
	private static readonly Regex Sig = new Regex("Signature error detected in container '(?<c>[^']+)'", RegexOptions.Compiled);

	public const string Advice = "Fortnite's log reports a signature error in the container(s) listed above: the game checks every block of its signed files and a swapped container fails that check, which can leave the game stuck on \"Logging in\" or show \"Verification Required\". Close Fortnite and the Epic launcher, then press Undo all swaps / Revert all.";

	public static List<string> SignatureErrors(string logText)
	{
		List<string> list = new List<string>();
		foreach (Match item in Sig.Matches(logText))
		{
			if (!list.Contains(item.Groups["c"].Value, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(item.Groups["c"].Value);
			}
		}
		return list;
	}

	public static List<string> Latest(string? logsDir = null)
	{
		try
		{
			string path = Path.Combine(logsDir ?? FortniteLoadout.LogsDirectory, "FortniteGame.log");
			if (!File.Exists(path))
			{
				return new List<string>();
			}
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream);
			return SignatureErrors(streamReader.ReadToEnd());
		}
		catch
		{
			return new List<string>();
		}
	}
}
