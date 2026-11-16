using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Drip.Core.IoStore;

namespace Drip.Core.Swapping;

public static class PakBackup
{
	public sealed class Info
	{
		public string Container { get; set; } = "";

		public string Build { get; set; } = "";

		public string SavedUtc { get; set; } = "";

		public Dictionary<string, long> OriginalUcasLengths { get; set; } = new Dictionary<string, long>();

		public string HowToRestore { get; set; } = "Close Fortnite and the Epic Launcher. Copy the .utoc in this folder over the one in FortniteGame\\Content\\Paks, then cut each .ucas to the length listed above. Or just use Revert in the swapper, which does the same thing for you.";
	}

	public const string FolderName = "Backup Paks";

	public static string FolderFor(string stateFilePath)
	{
		return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFilePath)) ?? AppPaths.DataDir, "Backup Paks");
	}

	public static string? Save(string folder, string paksDirectory, string containerName, string build, IEnumerable<PatchRecord> records, Action<string>? log = null)
	{
		try
		{
			string text = Path.Combine(paksDirectory, containerName + ".utoc.elenr-bak");
			if (!File.Exists(text))
			{
				return null;
			}
			string text2 = Path.Combine(folder, containerName);
			string text3 = Path.Combine(text2, containerName + ".utoc");
			string path = Path.Combine(text2, "original.json");
			Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
			foreach (PatchRecord item in records.Where((PatchRecord r) => string.Equals(r.Container, containerName, StringComparison.OrdinalIgnoreCase) && r.UcasFile.Length > 0))
			{
				dictionary[item.UcasFile] = (dictionary.TryGetValue(item.UcasFile, out var value) ? Math.Min(value, item.UcasLengthBefore) : item.UcasLengthBefore);
			}
			if (File.Exists(text3) && File.Exists(path))
			{
				try
				{
					Info info = JsonSerializer.Deserialize<Info>(File.ReadAllText(path));
					if (info != null && info.Build == build && new FileInfo(text3).Length == new FileInfo(text).Length)
					{
						return text2;
					}
				}
				catch
				{
				}
			}
			Directory.CreateDirectory(text2);
			File.Copy(text, text3, overwrite: true);
			Info value2 = new Info
			{
				Container = containerName,
				Build = build,
				SavedUtc = DateTime.UtcNow.ToString("o"),
				OriginalUcasLengths = dictionary
			};
			File.WriteAllText(path, JsonSerializer.Serialize(value2, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			log?.Invoke("Saved the original " + containerName + " pak to the backup folder.");
			return text2;
		}
		catch (Exception ex)
		{
			log?.Invoke(containerName + ": couldn't save the backup copy (" + ex.Message + ").");
			return null;
		}
	}

	public static List<string> List(string folder)
	{
		try
		{
			if (!Directory.Exists(folder))
			{
				return new List<string>();
			}
			return (from d in Directory.EnumerateDirectories(folder)
				where File.Exists(Path.Combine(d, Path.GetFileName(d) + ".utoc"))
				select Path.GetFileName(d)).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
		}
		catch
		{
			return new List<string>();
		}
	}
}
