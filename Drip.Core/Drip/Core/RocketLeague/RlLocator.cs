using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Drip.Core.RocketLeague;

public static class RlLocator
{
	private static readonly string[] ProcessNames = new string[1] { "RocketLeague" };

	public static string? FindCookedDirectory()
	{
		List<string> list = new List<string>();
		try
		{
			InlineArray5<string> buffer = default;
			buffer[0] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			buffer[1] = "Epic";
			buffer[2] = "EpicGamesLauncher";
			buffer[3] = "Data";
			buffer[4] = "Manifests";
			string path = Path.Combine(buffer);
			if (Directory.Exists(path))
			{
				foreach (string item in Directory.EnumerateFiles(path, "*.item"))
				{
					try
					{
						using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(item));
						JsonElement rootElement = jsonDocument.RootElement;
						if ((rootElement.TryGetProperty("DisplayName", out var value) ? (value.GetString() ?? "") : "").Equals("Rocket League", StringComparison.OrdinalIgnoreCase) && rootElement.TryGetProperty("InstallLocation", out var value2))
						{
							string text = value2.GetString();
							if (text != null && text.Length > 0)
							{
								list.Add(text);
							}
						}
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		foreach (string item2 in SteamFinder.CommonFolders())
		{
			list.Add(Path.Combine(item2, "rocketleague"));
		}
		string[] array = new string[4] { "C", "D", "E", "F" };
		foreach (string text2 in array)
		{
			list.Add(text2 + ":\\Program Files\\Epic Games\\rocketleague");
			list.Add(text2 + ":\\Epic Games\\rocketleague");
			list.Add(text2 + ":\\Program Files (x86)\\Steam\\steamapps\\common\\rocketleague");
			list.Add(text2 + ":\\SteamLibrary\\steamapps\\common\\rocketleague");
		}
		foreach (string item3 in list)
		{
			string text3 = Path.Combine(item3, "TAGame", "CookedPCConsole");
			if (Directory.Exists(text3))
			{
				return text3;
			}
		}
		return null;
	}

	public static string Resolve(string folder)
	{
		if (string.IsNullOrWhiteSpace(folder))
		{
			return folder;
		}
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(folder.Trim());
			int num = 0;
			while (num < 5 && directoryInfo != null)
			{
				string[] array = new string[4]
				{
					directoryInfo.FullName,
					Path.Combine(directoryInfo.FullName, "CookedPCConsole"),
					Path.Combine(directoryInfo.FullName, "TAGame", "CookedPCConsole"),
					Path.Combine(directoryInfo.FullName, "rocketleague", "TAGame", "CookedPCConsole")
				};
				foreach (string text in array)
				{
					if (Directory.Exists(text) && Directory.EnumerateFiles(text, "*_SF.upk").Take(1).Any())
					{
						return text;
					}
				}
				num++;
				directoryInfo = directoryInfo.Parent;
			}
		}
		catch
		{
		}
		return folder.Trim();
	}

	public static bool IsRunning()
	{
		return ProcessNames.Any((string p) => Process.GetProcessesByName(p).Length != 0);
	}

	public static bool IsAntiCheatRunning()
	{
		try
		{
			return Process.GetProcesses().Any((Process p) => IsAntiCheatName(p.ProcessName));
		}
		catch
		{
			return false;
		}
	}

	public static bool IsAntiCheatName(string processName)
	{
		if (!processName.StartsWith("EasyAntiCheat", StringComparison.OrdinalIgnoreCase))
		{
			return processName.Contains("_EAC", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static void Validate(string cooked)
	{
		if (!Directory.Exists(cooked))
		{
			throw new DirectoryNotFoundException("Not found: " + cooked);
		}
		if (!Directory.EnumerateFiles(cooked, "*_SF.upk").Take(1).Any())
		{
			throw new FileNotFoundException("No Rocket League packages in " + cooked + ". Pick the TAGame\\CookedPCConsole folder.");
		}
	}
}
