using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Drip.Core.FallGuys;

namespace Drip.Core.FortniteModels;

public static class FnModelZip
{
	private static readonly string[] Preferred = new string[9] { ".obj", ".glb", ".gltf", ".fbx", ".dae", ".3ds", ".ply", ".stl", ".blend" };

	private const long MaxTotal = 838860800L;

	private const int MaxEntries = 4000;

	public static bool IsZip(string path)
	{
		return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
	}

	public static FgModelSource Load(string path)
	{
		if (!IsZip(path))
		{
			return FgModelImport.Load(path);
		}
		string text = Path.Combine(Path.GetTempPath(), "DripModel_" + Guid.NewGuid().ToString("N").Substring(0, 10));
		try
		{
			Directory.CreateDirectory(text);
			Extract(path, text);
			string path2 = FindModel(text) ?? throw new InvalidDataException("That zip doesn't have a 3D model in it (FBX, OBJ, GLB...).");
			FgModelSource fgModelSource = FgModelImport.Load(path2);
			fgModelSource.Path = path2;
			return fgModelSource;
		}
		finally
		{
			try
			{
				Directory.Delete(text, recursive: true);
			}
			catch
			{
			}
		}
	}

	public static void Extract(string zipPath, string dest)
	{
		string value = Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
		if (zipArchive.Entries.Count > 4000)
		{
			throw new InvalidDataException("That zip has too many files in it.");
		}
		long num = 0L;
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			if (!entry.FullName.EndsWith('/') && !entry.FullName.EndsWith('\\') && entry.Name.Length != 0)
			{
				num += entry.Length;
				if (num > 838860800)
				{
					throw new InvalidDataException("That zip is too big to open.");
				}
				string fullPath = Path.GetFullPath(Path.Combine(dest, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
				if (fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
					entry.ExtractToFile(fullPath, overwrite: true);
				}
			}
		}
	}

	public static string? FindModel(string dir)
	{
		List<string> source = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
		string[] preferred = Preferred;
		foreach (string ext in preferred)
		{
			string text = (from f in source
				where string.Equals(Path.GetExtension(f), ext, StringComparison.OrdinalIgnoreCase)
				orderby new FileInfo(f).Length descending
				select f).FirstOrDefault();
			if (text != null)
			{
				return text;
			}
		}
		return null;
	}
}
