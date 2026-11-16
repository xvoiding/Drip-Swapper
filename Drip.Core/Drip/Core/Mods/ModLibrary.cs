using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.Mods;

public sealed class ModLibrary
{
	public string Directory { get; }

	public ModLibrary(string? dir = null)
	{
		Directory = dir ?? Path.Combine(AppPaths.DataDir, "mods");
	}

	public IEnumerable<ModPack> List(Action<string>? skipped = null)
	{
		if (!System.IO.Directory.Exists(Directory))
		{
			yield break;
		}
		foreach (string item in (from f in System.IO.Directory.EnumerateFiles(Directory)
			where ModPack.IsAddonExtension(Path.GetExtension(f)) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
			select f).OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
		{
			ModPack modPack = null;
			try
			{
				modPack = ModPack.Load(item);
			}
			catch (Exception ex)
			{
				skipped?.Invoke(Path.GetFileName(item) + ": " + ex.Message);
			}
			if (modPack != null)
			{
				yield return modPack;
			}
		}
	}

	public ModPack Import(string path)
	{
		List<string> list = ModPack.Load(path).Validate();
		if (list.Count > 0)
		{
			throw new InvalidDataException(string.Join(" ", list));
		}
		System.IO.Directory.CreateDirectory(Directory);
		string text = Path.GetExtension(path).ToLowerInvariant();
		string text2 = new string(Path.GetFileNameWithoutExtension(path).Select((char c) =>
		{
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == '-' || c == '_') ? true : false);
				flag = flag2;
			}
			return (!flag) ? '_' : c;
		}).ToArray());
		if (text2.Length == 0)
		{
			text2 = "mod";
		}
		string text3 = Path.Combine(Directory, text2 + text);
		int num = 2;
		while (File.Exists(text3))
		{
			text3 = Path.Combine(Directory, $"{text2}-{num}{text}");
			num++;
		}
		File.Copy(path, text3);
		return ModPack.Load(text3);
	}

	public void Remove(ModPack mod)
	{
		try
		{
			File.Delete(mod.FilePath);
		}
		catch
		{
		}
	}

	public IEnumerable<RiftPack> ListRifts()
	{
		if (!System.IO.Directory.Exists(Directory))
		{
			yield break;
		}
		foreach (string item in System.IO.Directory.EnumerateFiles(Directory, "*.rift").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
		{
			yield return RiftPack.Load(item);
		}
	}

	public RiftPack ImportRift(string path)
	{
		RiftPack riftPack = RiftPack.Load(path);
		if (riftPack.Problem != null)
		{
			throw new InvalidDataException(riftPack.Problem);
		}
		System.IO.Directory.CreateDirectory(Directory);
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
		if (text.Length == 0)
		{
			text = "model";
		}
		string text2 = Path.Combine(Directory, text + ".rift");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(Directory, $"{text}-{num}.rift");
			num++;
		}
		File.Copy(path, text2);
		return RiftPack.Load(text2);
	}

	public void Remove(RiftPack rift)
	{
		try
		{
			File.Delete(rift.FilePath);
		}
		catch
		{
		}
	}
}
