using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drip.Core.Install;

public sealed class FortniteLoadout
{
	private static readonly Regex SlotLine = new Regex("\\[LoadoutSlot\\.(?<slot>[A-Za-z]+)\\]=(?<type>[A-Za-z]+):(?<id>[A-Za-z0-9_\\-\\.]+)", RegexOptions.Compiled);

	private static readonly Regex CompletedLine = new Regex("Completed WriteActiveLoadoutGroup.*?Character \\[(?<type>[A-Za-z]+):(?<id>[A-Za-z0-9_\\-\\.]+)\\]", RegexOptions.Compiled);

	private static readonly Regex PublishLine = new Regex("publishing character (?<type>[A-Za-z]+):(?<id>[A-Za-z0-9_\\-\\.]+)", RegexOptions.Compiled);

	public IReadOnlyDictionary<string, string> Slots { get; }

	public string Source { get; }

	public string? Character => Get("Character");

	public string? Pickaxe => Get("Pickaxe");

	public string? Backpack => Get("Backpack");

	public string? Glider => Get("Glider");

	public static string LogsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Logs");

	private FortniteLoadout(Dictionary<string, string> slots, string source)
	{
		Slots = slots;
		Source = source;
	}

	public string? Get(string slot)
	{
		if (!Slots.TryGetValue(slot, out string value))
		{
			return null;
		}
		return value;
	}

	public bool IsEquipped(string slot, string id)
	{
		string text = Get(slot);
		if (text != null)
		{
			return string.Equals(text, id.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static FortniteLoadout Parse(string logText, string source = "")
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = logText.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].TrimEnd('\r');
			if (text.Length < 20)
			{
				continue;
			}
			Match match = SlotLine.Match(text);
			if (match.Success)
			{
				dictionary[match.Groups["slot"].Value] = match.Groups["id"].Value.ToLowerInvariant();
				continue;
			}
			match = CompletedLine.Match(text);
			if (!match.Success)
			{
				match = PublishLine.Match(text);
			}
			if (match.Success)
			{
				dictionary["Character"] = match.Groups["id"].Value.ToLowerInvariant();
			}
		}
		return new FortniteLoadout(dictionary, source);
	}

	public static FortniteLoadout? ReadLatest(string? logsDir = null)
	{
		try
		{
			string path = logsDir ?? LogsDirectory;
			if (!Directory.Exists(path))
			{
				return null;
			}
			foreach (FileInfo item in (from f in new DirectoryInfo(path).GetFiles("FortniteGame*.log")
				orderby f.LastWriteTimeUtc descending
				select f).Take(6))
			{
				FortniteLoadout fortniteLoadout = Parse(ReadShared(item.FullName), item.Name);
				if (fortniteLoadout.Slots.Count > 0)
				{
					return fortniteLoadout;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string ReadShared(string path)
	{
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using StreamReader streamReader = new StreamReader(stream);
		return streamReader.ReadToEnd();
	}
}
