using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Drip.Core.Mods;

public sealed class PakLibrary
{
	private sealed class Ref
	{
		public string Path { get; set; } = "";
	}

	public string Directory { get; }

	public PakLibrary(string? dir = null)
	{
		Directory = dir ?? Path.Combine(AppPaths.DataDir, "paks");
	}

	public IEnumerable<(string RefFile, string PakPath)> List()
	{
		if (!System.IO.Directory.Exists(Directory))
		{
			yield break;
		}
		foreach (string item in System.IO.Directory.EnumerateFiles(Directory, "*.pakref").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
		{
			string text = null;
			try
			{
				text = JsonSerializer.Deserialize<Ref>(File.ReadAllText(item))?.Path;
			}
			catch
			{
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				yield return (RefFile: item, PakPath: text);
			}
		}
	}

	public PakPack Add(string path)
	{
		PakPack pakPack = PakPack.Load(path);
		if (pakPack.Problem != null)
		{
			throw new InvalidDataException(pakPack.Problem);
		}
		System.IO.Directory.CreateDirectory(Directory);
		string full = Path.GetFullPath(path);
		if (List().Any(((string RefFile, string PakPath) r) => string.Equals(r.PakPath, full, StringComparison.OrdinalIgnoreCase)))
		{
			return pakPack;
		}
		string text = new string(Path.GetFileNameWithoutExtension(path).Select((char c) =>
		{
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == '-' || c == '_') ? true : false);
				flag = flag2;
			}
			return (!flag) ? '_' : c;
		}).ToArray());
		string path2 = Path.Combine(Directory, text + ".pakref");
		int num = 2;
		while (File.Exists(path2))
		{
			path2 = Path.Combine(Directory, $"{text}-{num}.pakref");
			num++;
		}
		File.WriteAllText(path2, JsonSerializer.Serialize(new Ref
		{
			Path = full
		}));
		return pakPack;
	}

	public void Remove(string refFile)
	{
		try
		{
			File.Delete(refFile);
		}
		catch
		{
		}
	}
}
