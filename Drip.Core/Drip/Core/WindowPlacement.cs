using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Drip.Core;

public static class WindowPlacement
{
	public static Placement? Fit(Placement? saved, double minW, double minH, double screenLeft, double screenTop, double screenW, double screenH)
	{
		if ((object)saved == null)
		{
			return null;
		}
		if (!double.IsFinite(saved.Left) || !double.IsFinite(saved.Top) || !double.IsFinite(saved.Width) || !double.IsFinite(saved.Height))
		{
			return null;
		}
		double num = Math.Clamp(saved.Width, minW, Math.Max(minW, screenW));
		double height = Math.Clamp(saved.Height, minH, Math.Max(minH, screenH));
		double max = screenLeft + screenW - 120.0;
		double max2 = screenTop + screenH - 60.0;
		double left = Math.Clamp(saved.Left, screenLeft - num + 120.0, max);
		double top = Math.Clamp(saved.Top, screenTop, max2);
		return new Placement(left, top, num, height);
	}

	public static string FilePath()
	{
		return Path.Combine(AppPaths.DataDir, "windows.json");
	}

	public static Placement? Load(string key, string? path = null)
	{
		try
		{
			if (path == null)
			{
				path = FilePath();
			}
			if (!File.Exists(path))
			{
				return null;
			}
			Dictionary<string, Placement> dictionary = JsonSerializer.Deserialize<Dictionary<string, Placement>>(File.ReadAllText(path));
			Placement value;
			return (dictionary != null && dictionary.TryGetValue(key, out value)) ? value : null;
		}
		catch
		{
			return null;
		}
	}

	public static void Save(string key, Placement p, string? path = null)
	{
		try
		{
			if (path == null)
			{
				path = FilePath();
			}
			Dictionary<string, Placement> dictionary;
			try
			{
				dictionary = (File.Exists(path) ? (JsonSerializer.Deserialize<Dictionary<string, Placement>>(File.ReadAllText(path)) ?? new Dictionary<string, Placement>()) : new Dictionary<string, Placement>());
			}
			catch
			{
				dictionary = new Dictionary<string, Placement>();
			}
			dictionary[key] = p;
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, JsonSerializer.Serialize(dictionary));
		}
		catch
		{
		}
	}
}
