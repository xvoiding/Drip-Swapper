using System;
using System.IO;
using System.Text.Json;

namespace Drip.App;

public sealed class RtSettings
{
	public string GamePath { get; set; } = "";

	private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drip Swapper", "retrac.json");

	public static RtSettings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<RtSettings>(File.ReadAllText(FilePath)) ?? new RtSettings();
			}
		}
		catch
		{
		}
		return new RtSettings();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
		}
		catch
		{
		}
	}
}
