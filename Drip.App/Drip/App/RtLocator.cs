using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Drip.App;

public static class RtLocator
{
	private static readonly string[] Skip = new string[11]
	{
		"windows", "$recycle.bin", "system volume information", "node_modules", ".git", "winsxs", "programdata\\microsoft", "microsoft", "packages", "temp",
		"steamlibrary"
	};

	public static bool LooksOfficial(string path)
	{
		string text = path.Replace('/', '\\').ToLowerInvariant();
		if (text.Contains("\\epic games\\fortnite"))
		{
			return true;
		}
		try
		{
			string path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
			if (File.Exists(path2))
			{
				string text2 = File.ReadAllText(path2);
				int num = text2.IndexOf("\"AppName\": \"Fortnite\"", StringComparison.OrdinalIgnoreCase);
				if (num >= 0)
				{
					int num2 = text2.IndexOf("InstallLocation", num, StringComparison.OrdinalIgnoreCase);
					if (num2 >= 0)
					{
						int num3 = text2.IndexOf(':', num2);
						int num4 = text2.IndexOf('"', num3 + 1);
						int num5 = text2.IndexOf('"', num4 + 1);
						if (num4 > 0 && num5 > num4)
						{
							int num6 = num4 + 1;
							string text3 = text2.Substring(num6, num5 - num6).Replace("\\\\", "\\").TrimEnd('\\')
								.ToLowerInvariant();
							if (text3.Length > 3 && text.StartsWith(text3))
							{
								return true;
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static string? PaksFrom(string picked)
	{
		if (string.IsNullOrWhiteSpace(picked) || !Directory.Exists(picked))
		{
			return null;
		}
		string[] array = new string[4] { "", "Content\\Paks", "FortniteGame\\Content\\Paks", "Paks" };
		foreach (string path in array)
		{
			string text = Path.Combine(picked, path);
			if (Directory.Exists(text) && string.Equals(Path.GetFileName(text.TrimEnd('\\')), "Paks", StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return null;
	}

	public static List<string> Search(CancellationToken ct)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string[] array = new string[6] { "Desktop", "Documents", "Downloads", "Games", "AppData\\Local", "AppData\\Roaming" };
		foreach (string path in array)
		{
			list2.Add(Path.Combine(folderPath, path));
		}
		DriveInfo[] drives = DriveInfo.GetDrives();
		foreach (DriveInfo driveInfo in drives)
		{
			try
			{
				if (driveInfo.DriveType == DriveType.Fixed && driveInfo.IsReady)
				{
					list2.Add(driveInfo.RootDirectory.FullName);
				}
			}
			catch
			{
			}
		}
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		array = new string[5]
		{
			Path.Combine(folderPath, "AppData\\Local"),
			Path.Combine(folderPath, "AppData\\Roaming"),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
		};
		foreach (string path2 in array)
		{
			try
			{
				foreach (string item in Directory.EnumerateDirectories(path2))
				{
					if (Path.GetFileName(item).Contains("retrac", StringComparison.OrdinalIgnoreCase))
					{
						if (ct.IsCancellationRequested)
						{
							break;
						}
						Walk(item, 0, list, seen, ct, deep: true);
					}
				}
			}
			catch
			{
			}
		}
		foreach (string item2 in list2)
		{
			if (ct.IsCancellationRequested)
			{
				break;
			}
			Walk(item2, 0, list, seen, ct);
		}
		return list;
	}

	private static void Walk(string dir, int depth, List<string> found, HashSet<string> seen, CancellationToken ct, bool deep = false)
	{
		if (ct.IsCancellationRequested || depth > (deep ? 8 : 4) || !seen.Add(dir))
		{
			return;
		}
		try
		{
			string text = Path.Combine(dir, "FortniteGame", "Content", "Paks");
			if (Directory.Exists(text) && !LooksOfficial(text))
			{
				found.Add(text);
				return;
			}
			foreach (string item in Directory.EnumerateDirectories(dir))
			{
				string text2 = Path.GetFileName(item).ToLowerInvariant();
				if (!Skip.Contains(text2) && !text2.StartsWith('.') && (deep || depth < 2 || !(text2 != "retrac") || text2.Contains("fortnite") || text2.Contains("retrac") || text2.Contains("ogfn") || text2.Contains("games") || text2.Contains("content") || text2.Contains("builds") || text2.Contains("downloads") || text2.Contains("program")))
				{
					Walk(item, depth + 1, found, seen, ct, deep);
					if (ct.IsCancellationRequested)
					{
						break;
					}
				}
			}
		}
		catch
		{
		}
	}
}
