using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Drip.Core.Fun;

public sealed class FunStore
{
	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _gate = new object();

	public string FilePath { get; }

	public FunData Data { get; private set; } = new FunData();

	public FunStore(string? filePath = null)
	{
		FilePath = filePath ?? Path.Combine(AppPaths.DataDir, "fun.json");
		try
		{
			if (File.Exists(FilePath))
			{
				Data = JsonSerializer.Deserialize<FunData>(File.ReadAllText(FilePath)) ?? new FunData();
			}
		}
		catch
		{
			Data = new FunData();
		}
	}

	public void Save()
	{
		lock (_gate)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, Json));
			}
			catch
			{
			}
		}
	}

	public bool IsFavorite(string game, string id)
	{
		if (Data.Favorites.TryGetValue(game, out List<string> value))
		{
			return value.Contains(id, StringComparer.OrdinalIgnoreCase);
		}
		return false;
	}

	public IReadOnlyList<string> Favorites(string game)
	{
		if (!Data.Favorites.TryGetValue(game, out List<string> value))
		{
			return Array.Empty<string>();
		}
		return value;
	}

	public bool ToggleFavorite(string game, string id)
	{
		if (!Data.Favorites.TryGetValue(game, out List<string> value))
		{
			value = (Data.Favorites[game] = new List<string>());
		}
		int num = value.FindIndex((string x) => x.Equals(id, StringComparison.OrdinalIgnoreCase));
		bool flag = num < 0;
		if (flag)
		{
			value.Add(id);
		}
		else
		{
			value.RemoveAt(num);
		}
		Save();
		return flag;
	}

	public IReadOnlyList<Fit> FitsFor(string game)
	{
		return (from f in Data.Fits
			where f.Game == game
			orderby f.Saved descending
			select f).ToList();
	}

	public Fit SaveFit(string game, string name, IEnumerable<FitEntry> entries)
	{
		name = CleanName(name);
		List<FitEntry> list = entries.Take(40).ToList();
		if (list.Count == 0)
		{
			throw new ArgumentException("A loadout needs at least one swap.");
		}
		Data.Fits.RemoveAll((Fit f) => f.Game == game && f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		Fit fit = new Fit
		{
			Game = game,
			Name = name,
			Entries = list
		};
		Data.Fits.Add(fit);
		Save();
		return fit;
	}

	public void DeleteFit(Fit fit)
	{
		Data.Fits.Remove(fit);
		Save();
	}

	public static string CleanName(string? name)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = (name ?? "").Trim();
		foreach (char c in text)
		{
			if (!char.IsControl(c))
			{
				stringBuilder.Append(c);
			}
		}
		string text2 = stringBuilder.ToString().Trim();
		if (text2.Length == 0)
		{
			text2 = "My loadout";
		}
		if (text2.Length <= 40)
		{
			return text2;
		}
		return text2.Substring(0, 40);
	}

	public string ShareCode(Fit fit)
	{
		return FitCode.Encode(fit);
	}

	public Fit ImportCode(string code)
	{
		Fit fit = FitCode.Decode(code);
		Data.Fits.RemoveAll((Fit f) => f.Game == fit.Game && f.Name.Equals(fit.Name, StringComparison.OrdinalIgnoreCase));
		fit.Saved = DateTime.UtcNow;
		Data.Fits.Add(fit);
		Save();
		return fit;
	}
}
