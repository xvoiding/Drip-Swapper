using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Drip.Core;

public static class SteamFinder
{
	public static IReadOnlyList<string> CommonFolders()
	{
		List<string> libs = new List<string>();
		List<string> list = new List<string>();
		(RegistryKey, string, string)[] array = new (RegistryKey, string, string)[3]
		{
			(Registry.CurrentUser, "Software\\Valve\\Steam", "SteamPath"),
			(Registry.LocalMachine, "SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"),
			(Registry.LocalMachine, "SOFTWARE\\Valve\\Steam", "InstallPath")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (registryKey, name, name2) = array[i];
			try
			{
				if (registryKey.OpenSubKey(name)?.GetValue(name2) is string { Length: >0 } text)
				{
					list.Add(text);
				}
			}
			catch
			{
			}
		}
		string[] array2 = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
		};
		foreach (string text2 in array2)
		{
			if (!string.IsNullOrEmpty(text2))
			{
				list.Add(Path.Combine(text2, "Steam"));
			}
		}
		foreach (string item in list.ToList())
		{
			Add(item);
			try
			{
				string path = Path.Combine(item, "steamapps", "libraryfolders.vdf");
				if (!File.Exists(path))
				{
					continue;
				}
				foreach (string item2 in ParseLibraryFolders(File.ReadAllText(path)))
				{
					Add(item2);
				}
			}
			catch
			{
			}
		}
		try
		{
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				bool flag = !driveInfo.IsReady;
				if (!flag)
				{
					DriveType driveType = driveInfo.DriveType;
					bool flag2 = (uint)(driveType - 2) <= 1u;
					flag = !flag2;
				}
				if (flag)
				{
					continue;
				}
				string fullName = driveInfo.RootDirectory.FullName;
				array2 = new string[5] { "SteamLibrary", "Steam", "Program Files (x86)\\Steam", "Games\\Steam", "Games\\SteamLibrary" };
				foreach (string path2 in array2)
				{
					if (Directory.Exists(Path.Combine(fullName, path2, "steamapps")))
					{
						Add(Path.Combine(fullName, path2));
					}
				}
			}
		}
		catch
		{
		}
		return libs.Select((string l) => Path.Combine(l, "steamapps", "common")).Where(Directory.Exists).ToList();
		void Add(string? p)
		{
			if (!string.IsNullOrWhiteSpace(p))
			{
				try
				{
					p = Path.GetFullPath(p.Trim().Replace('/', '\\'));
				}
				catch
				{
					return;
				}
				if (!libs.Contains(p, StringComparer.OrdinalIgnoreCase))
				{
					libs.Add(p);
				}
			}
		}
	}

	public static IEnumerable<string> ParseLibraryFolders(string vdf)
	{
		foreach (Match item in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
		{
			yield return item.Groups[1].Value.Replace("\\\\", "\\");
		}
	}
}
