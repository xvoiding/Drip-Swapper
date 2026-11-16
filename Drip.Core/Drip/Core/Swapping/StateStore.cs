using System.IO;
using System.Text.Json;
using Drip.Core.IoStore;

namespace Drip.Core.Swapping;

public sealed class StateStore
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public string FilePath { get; }

	public SwapState State { get; private set; } = new SwapState();

	public StateStore(string? filePath = null)
	{
		FilePath = filePath ?? Path.Combine(AppPaths.DataDir, "state.json");
		Load();
	}

	private void Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				State = JsonSerializer.Deserialize<SwapState>(File.ReadAllText(FilePath)) ?? new SwapState();
			}
		}
		catch
		{
			State = new SwapState();
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
		File.WriteAllText(FilePath, JsonSerializer.Serialize(State, Json));
	}

	public void Add(PatchRecord rec)
	{
		State.Patches.RemoveAll((PatchRecord p) => p.Container == rec.Container && p.ChunkIndex == rec.ChunkIndex);
		State.Patches.Add(rec);
		Save();
	}

	public void Clear()
	{
		State.Patches.Clear();
		Save();
	}
}
