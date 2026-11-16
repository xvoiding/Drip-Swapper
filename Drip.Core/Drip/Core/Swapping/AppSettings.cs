using System.IO;
using System.Text.Json;

namespace Drip.Core.Swapping;

public sealed class AppSettings
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public bool AutoRevertOnExit { get; set; } = true;

	public bool ShowExperimentalCategories { get; set; }

	public bool SeenWelcome { get; set; }

	public bool SeenEpicOptionsTip { get; set; }

	public static string DefaultPath()
	{
		return Path.Combine(AppPaths.DataDir, "settings.json");
	}

	public static AppSettings Load(string? path = null)
	{
		try
		{
			if (path == null)
			{
				path = DefaultPath();
			}
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	public void Save(string? path = null)
	{
		if (path == null)
		{
			path = DefaultPath();
		}
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
	}
}
