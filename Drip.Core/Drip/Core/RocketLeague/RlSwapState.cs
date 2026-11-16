using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Drip.Core.RocketLeague;

public sealed class RlSwapState
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public List<RlSwapRecord> Swaps { get; set; } = new List<RlSwapRecord>();

	public RlSwapSettings Settings { get; set; } = new RlSwapSettings();

	public static string DefaultPath()
	{
		return Path.Combine(AppPaths.DataDir, "RocketLeague", "state.json");
	}

	public static string BackupDir()
	{
		return Path.Combine(AppPaths.DataDir, "RocketLeague", "backups");
	}

	public static RlSwapState Load(string? path = null)
	{
		try
		{
			if (path == null)
			{
				path = DefaultPath();
			}
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<RlSwapState>(File.ReadAllText(path)) ?? new RlSwapState();
			}
		}
		catch
		{
		}
		return new RlSwapState();
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
