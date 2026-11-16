using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Drip.Core.FallGuys;

public static class FgLocator
{
	private static readonly string[] ProcessNames = new string[3] { "FallGuys_client_game", "FallGuys_client", "RunFallGuys" };

	public static FgInstall? Find()
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
						if ((rootElement.TryGetProperty("DisplayName", out var value) ? (value.GetString() ?? "") : "").Replace(" ", "").Equals("FallGuys", StringComparison.OrdinalIgnoreCase) && rootElement.TryGetProperty("InstallLocation", out var value2))
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
			list.Add(Path.Combine(item2, "Fall Guys"));
		}
		string[] array = new string[4] { "C", "D", "E", "F" };
		foreach (string text2 in array)
		{
			list.Add(text2 + ":\\Program Files\\Epic Games\\FallGuys");
			list.Add(text2 + ":\\Epic Games\\FallGuys");
			list.Add(text2 + ":\\Program Files (x86)\\Steam\\steamapps\\common\\Fall Guys");
			list.Add(text2 + ":\\SteamLibrary\\steamapps\\common\\Fall Guys");
		}
		foreach (string item3 in list)
		{
			FgInstall fgInstall = new FgInstall(item3);
			if (fgInstall.IsValid)
			{
				return fgInstall;
			}
		}
		return null;
	}

	public static FgInstall? FromFolder(string folder)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(folder);
		int num = 0;
		while (num < 6 && directoryInfo != null)
		{
			FgInstall fgInstall = new FgInstall(directoryInfo.FullName);
			if (fgInstall.IsValid)
			{
				return fgInstall;
			}
			num++;
			directoryInfo = directoryInfo.Parent;
		}
		return null;
	}

	public static bool IsRunning()
	{
		return ProcessNames.Any((string p) => Process.GetProcessesByName(p).Length != 0);
	}

	public static bool IsAntiCheatRunning()
	{
		try
		{
			return Process.GetProcesses().Any((Process p) => p.ProcessName.StartsWith("EasyAntiCheat", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Contains("_EAC", StringComparison.OrdinalIgnoreCase));
		}
		catch
		{
			return false;
		}
	}
}
