using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.FallGuys;

public sealed class FgModelLibrary
{
	public sealed record Entry(string Path, FgDripFile File);

	public string Dir { get; }

	public FgModelLibrary(string? dir = null)
	{
		Dir = dir ?? Path.Combine(AppPaths.DataDir, "FallGuys", "models");
		Directory.CreateDirectory(Dir);
	}

	public List<Entry> List()
	{
		List<Entry> list = new List<Entry>();
		foreach (string item in Directory.EnumerateFiles(Dir, "*.dripFG").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
		{
			try
			{
				list.Add(new Entry(item, FgDripFile.Load(item)));
			}
			catch
			{
			}
		}
		return list;
	}

	public string Add(FgDripFile file)
	{
		string text = string.Concat(file.Name.Select((char c) => (!Path.GetInvalidFileNameChars().Contains(c)) ? c : '_')).Trim();
		if (text.Length == 0)
		{
			text = "model";
		}
		string text2 = Path.Combine(Dir, text + ".dripFG");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(Dir, $"{text} ({num}){".dripFG"}");
			num++;
		}
		file.Save(text2);
		return text2;
	}

	public FgDripFile Import(string path)
	{
		FgDripFile fgDripFile = FgDripFile.Load(path);
		Add(fgDripFile);
		return fgDripFile;
	}

	public void Remove(string path)
	{
		string text = Path.Combine(Dir, "removed");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, Path.GetFileName(path));
		if (File.Exists(text2))
		{
			text2 = Path.Combine(text, DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(path));
		}
		File.Move(path, text2);
	}
}
