using System;
using System.IO;

namespace Drip.Core;

public static class AppPaths
{
	public const string FolderName = "DripSwapper";

	public const string OldFolderName = "ElenrSwapper";

	public static string DataDir { get; } = Resolve();

	private static string Resolve()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string text = Path.Combine(folderPath, "DripSwapper");
		string text2 = Path.Combine(folderPath, "ElenrSwapper");
		if (!Migrate(text2, text))
		{
			if (!Directory.Exists(text2) || Directory.Exists(text))
			{
				return text;
			}
			return text2;
		}
		return text;
	}

	public static bool Migrate(string oldDir, string newDir)
	{
		if (Directory.Exists(newDir))
		{
			return true;
		}
		if (!Directory.Exists(oldDir))
		{
			return true;
		}
		string text = newDir + ".migrating";
		try
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			CopyAll(oldDir, text);
			Directory.Move(text, newDir);
			return true;
		}
		catch
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
			return false;
		}
	}

	private static void CopyAll(string from, string to)
	{
		Directory.CreateDirectory(to);
		string[] files = Directory.GetFiles(from);
		foreach (string text in files)
		{
			File.Copy(text, Path.Combine(to, Path.GetFileName(text)), overwrite: false);
		}
		files = Directory.GetDirectories(from);
		foreach (string text2 in files)
		{
			CopyAll(text2, Path.Combine(to, Path.GetFileName(text2)));
		}
	}
}
