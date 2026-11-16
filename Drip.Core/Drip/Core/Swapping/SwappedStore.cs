using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Drip.Core.Swapping;

public sealed class SwappedStore
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public string FilePath { get; }

	public List<SwappedItem> Items { get; private set; } = new List<SwappedItem>();

	public SwappedStore(string? filePath = null)
	{
		FilePath = filePath ?? Path.Combine(AppPaths.DataDir, "swaps.json");
		try
		{
			if (File.Exists(FilePath))
			{
				Items = JsonSerializer.Deserialize<List<SwappedItem>>(File.ReadAllText(FilePath)) ?? new List<SwappedItem>();
			}
		}
		catch
		{
			Items = new List<SwappedItem>();
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
		File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, Json));
	}

	public void Add(SwappedItem item)
	{
		Items.RemoveAll((SwappedItem i) => string.Equals(i.SourceId, item.SourceId, StringComparison.OrdinalIgnoreCase));
		Items.Insert(0, item);
		Save();
	}

	public void Remove(SwappedItem item)
	{
		Items.Remove(item);
		Save();
	}

	public void Clear()
	{
		Items.Clear();
		Save();
	}
}
