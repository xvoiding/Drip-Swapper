using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Drip.Core.Install;

public static class FortniteLocator
{
	private static readonly string[] ProcessNames = new string[4] { "FortniteClient-Win64-Shipping", "FortniteLauncher", "FortniteClient-Win64-Shipping_BE", "FortniteClient-Win64-Shipping_EAC" };

	public static string? FindPaksDirectory()
	{
		List<string> list = new List<string>();
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
			if (File.Exists(path))
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
				if (jsonDocument.RootElement.TryGetProperty("InstallationList", out var value))
				{
					foreach (JsonElement item in value.EnumerateArray())
					{
						if (item.TryGetProperty("AppName", out var value2) && value2.GetString() == "Fortnite" && item.TryGetProperty("InstallLocation", out var value3))
						{
							string text = value3.GetString();
							if (text != null)
							{
								list.Add(text);
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		list.Add("C:\\Program Files\\Epic Games\\Fortnite");
		list.Add("D:\\Epic Games\\Fortnite");
		list.Add("D:\\Program Files\\Epic Games\\Fortnite");
		foreach (string item2 in list)
		{
			string text2 = Path.Combine(item2, "FortniteGame", "Content", "Paks");
			if (Directory.Exists(text2))
			{
				return text2;
			}
		}
		return null;
	}

	public static bool IsFortniteRunning()
	{
		return ProcessNames.Any((string p) => Process.GetProcessesByName(p).Length != 0);
	}

	public static void Validate(string paks)
	{
		if (!Directory.Exists(paks))
		{
			throw new DirectoryNotFoundException("Not found: " + paks);
		}
		if (!Directory.EnumerateFiles(paks, "*.utoc").Any())
		{
			throw new FileNotFoundException("No .utoc files in " + paks + ". Pick the FortniteGame\\Content\\Paks folder.");
		}
	}
}
