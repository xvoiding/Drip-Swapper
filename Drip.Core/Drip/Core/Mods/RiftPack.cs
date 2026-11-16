using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Drip.Core.Mods;

public sealed class RiftPack
{
	private static readonly Regex Ver = new Regex("\\d{1,2}\\.\\d{1,2}", RegexOptions.Compiled);

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public const long MaxPakBytes = 536870912L;

	public static double CurrentBuild { get; set; } = 42.3;

	public string FilePath { get; private init; } = "";

	public string Key => Path.GetFileNameWithoutExtension(FilePath);

	public string Name { get; private set; } = "";

	public string Author { get; private set; } = "";

	public string Description { get; private set; } = "";

	public string VersionText { get; private set; } = "";

	public List<double> Builds { get; } = new List<double>();

	public List<PakFileEntry> Files { get; } = new List<PakFileEntry>();

	public long TotalBytes { get; private set; }

	public string? Problem { get; private set; }

	public int MeshCount => Enumerable.Count(Files, (PakFileEntry f) => f.Path.Contains("SK_", StringComparison.OrdinalIgnoreCase) || f.Path.Contains("/Meshes/", StringComparison.OrdinalIgnoreCase));

	public int TextureCount => Enumerable.Count(Files, (PakFileEntry f) => f.Path.Contains("/Textures/", StringComparison.OrdinalIgnoreCase));

	public int AssetCount => Enumerable.Count(Files, (PakFileEntry f) => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase));

	public RiftCompat Compat
	{
		get
		{
			if (Problem == null)
			{
				if (!Builds.Any((double b) => Math.Abs(b - CurrentBuild) < 0.001))
				{
					return RiftCompat.WrongBuild;
				}
				return RiftCompat.Works;
			}
			return RiftCompat.Unreadable;
		}
	}

	public string Status => Compat switch
	{
		RiftCompat.Unreadable => "Can't read this file: " + Problem, 
		RiftCompat.Works => "Made for this Fortnite build.", 
		_ => (Builds.Count == 0) ? ("Doesn't say which Fortnite version it was made for. Old .rift files target 2018-2019 builds, so it won't work on " + CurrentBuild.ToString("0.00") + ".") : $"Made for Fortnite {string.Join(", ", Builds.Select((double b) => b.ToString("0.00")))}. This game is {CurrentBuild:0.00} (a much newer engine), so it can't load.", 
	};

	public static RiftPack Load(string path)
	{
		RiftPack riftPack = new RiftPack
		{
			FilePath = path
		};
		try
		{
			using ZipArchive zipArchive = ZipFile.OpenRead(path);
			ZipArchiveEntry zipArchiveEntry = zipArchive.GetEntry("metadata.json") ?? zipArchive.Entries.FirstOrDefault((ZipArchiveEntry e) => e.Name.Equals("metadata.json", StringComparison.OrdinalIgnoreCase));
			string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
			riftPack.Name = name;
			if (zipArchiveEntry != null)
			{
				if (zipArchiveEntry.Length > 1000000)
				{
					throw new InvalidDataException("metadata.json is too large.");
				}
				using StreamReader streamReader = new StreamReader(zipArchiveEntry.Open());
				using JsonDocument jsonDocument = JsonDocument.Parse(streamReader.ReadToEnd(), new JsonDocumentOptions
				{
					CommentHandling = JsonCommentHandling.Skip,
					AllowTrailingCommas = true
				});
				JsonElement root = jsonDocument.RootElement;
				string text = Str("name");
				if (text.Length > 0)
				{
					riftPack.Name = Clip(text, 80);
				}
				riftPack.Author = Clip(Str("author"), 60);
				riftPack.Description = Clip(Str("description"), 300);
				riftPack.VersionText = Clip(Str("version"), 80);
				foreach (Match item in Ver.Matches(riftPack.VersionText))
				{
					if (double.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						riftPack.Builds.Add(result);
					}
				}
				string Str(string n)
				{
					if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.String)
					{
						return "";
					}
					return value.GetString() ?? "";
				}
			}
			List<ZipArchiveEntry> list = zipArchive.Entries.Where((ZipArchiveEntry e) => e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToList();
			if (list.Count == 0)
			{
				riftPack.Problem = "no .pak inside.";
				return riftPack;
			}
			foreach (ZipArchiveEntry item2 in list)
			{
				if (item2.Length > 536870912)
				{
					riftPack.Problem = "pak is too large.";
					return riftPack;
				}
				string tempFileName = Path.GetTempFileName();
				try
				{
					using (FileStream destination = File.Create(tempFileName))
					{
						using Stream stream = item2.Open();
						stream.CopyTo(destination);
					}
					using FileStream s = File.OpenRead(tempFileName);
					PakIndex.Result result2 = PakIndex.Read(s);
					riftPack.Files.AddRange(result2.Files);
				}
				finally
				{
					try
					{
						File.Delete(tempFileName);
					}
					catch
					{
					}
				}
			}
			riftPack.TotalBytes = riftPack.Files.Sum((PakFileEntry f) => f.Size);
		}
		catch (Exception ex)
		{
			riftPack.Problem = ex.Message;
			if (riftPack.Name.Length == 0)
			{
				riftPack.Name = Path.GetFileNameWithoutExtension(path);
			}
		}
		return riftPack;
	}

	private static string Clip(string s, int n)
	{
		if (s.Length > n)
		{
			return s.Substring(0, n);
		}
		return s;
	}
}
