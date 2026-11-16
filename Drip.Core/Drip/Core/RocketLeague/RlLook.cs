using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Drip.Core.RocketLeague;

public static class RlLook
{
	public sealed class Manifest
	{
		public string Pack { get; set; } = "";

		public string PresetFile { get; set; } = "";

		public List<string> Files { get; set; } = new List<string>();

		public bool HadIni { get; set; }

		public string? OriginalIni { get; set; }
	}

	public const string ManifestName = "drip-look.json";

	public const string PresetFolder = "reshade-presets";

	public const string ShaderFolder = "reshade-shaders";

	private static readonly string[] HookDlls = new string[5] { "dxgi.dll", "d3d11.dll", "d3d10.dll", "d3d9.dll", "opengl32.dll" };

	public static string? Win64From(string? cookedDirectory)
	{
		if (string.IsNullOrWhiteSpace(cookedDirectory))
		{
			return null;
		}
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(cookedDirectory.TrimEnd(new char[2] { '\\', '/' }));
			int num = 0;
			while (num < 4 && directoryInfo != null)
			{
				string text = Path.Combine(directoryInfo.FullName, "Binaries", "Win64");
				if (Directory.Exists(text) && File.Exists(Path.Combine(text, "RocketLeague.exe")))
				{
					return text;
				}
				num++;
				directoryInfo = directoryInfo.Parent;
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool HasReShade(string win64)
	{
		string[] hookDlls = HookDlls;
		foreach (string path in hookDlls)
		{
			string text = Path.Combine(win64, path);
			if (!File.Exists(text))
			{
				continue;
			}
			try
			{
				FileInfo fileInfo = new FileInfo(text);
				if (fileInfo.Length < 200000 || fileInfo.Length > 60000000 || !Contains(File.ReadAllBytes(text), "ReShade"))
				{
					continue;
				}
				return true;
			}
			catch
			{
			}
		}
		return false;
	}

	private static bool Contains(byte[] data, string ascii)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(ascii);
		return ((ReadOnlySpan<byte>)data.AsSpan()).IndexOf((ReadOnlySpan<byte>)bytes) >= 0;
	}

	public static Manifest? Installed(string win64)
	{
		try
		{
			string path = Path.Combine(win64, "drip-look.json");
			return File.Exists(path) ? JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	private static void Guard()
	{
		if (RlLocator.IsRunning())
		{
			throw new InvalidOperationException("Close Rocket League first.");
		}
		if (RlLocator.IsAntiCheatRunning())
		{
			throw new InvalidOperationException("Anti-cheat is running. Close Rocket League and its launcher first; the look is for offline play with anti-cheat off.");
		}
	}

	public static int Install(string win64, string packDir, string presetName, bool checkGame = true)
	{
		if (checkGame)
		{
			Guard();
		}
		if (!Directory.Exists(win64))
		{
			throw new DirectoryNotFoundException("Rocket League's Win64 folder wasn't found.");
		}
		if (!HasReShade(win64))
		{
			throw new InvalidOperationException("ReShade isn't installed for Rocket League yet. Run the official ReShade installer first and pick RocketLeague.exe.");
		}
		string text = Path.Combine(packDir, presetName);
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("The look pack has no preset file.", text);
		}
		if (Installed(win64) != null)
		{
			Remove(win64, checkGame: false);
		}
		Manifest mf = new Manifest
		{
			Pack = Path.GetFileName(packDir.TrimEnd(new char[2] { '\\', '/' })),
			PresetFile = Path.Combine("reshade-presets", presetName)
		};
		int count = 0;
		string text2 = Path.Combine(win64, mf.PresetFile);
		Directory.CreateDirectory(Path.GetDirectoryName(text2));
		File.Copy(text, text2, overwrite: true);
		mf.Files.Add(mf.PresetFile);
		count++;
		string text3 = Path.Combine(packDir, "reshade-shaders");
		if (Directory.Exists(text3))
		{
			foreach (string item in Directory.EnumerateFiles(text3, "*", SearchOption.AllDirectories))
			{
				Copy(item, Path.Combine("reshade-shaders", Path.GetRelativePath(text3, item)));
			}
		}
		string path = Path.Combine(win64, "ReShade.ini");
		mf.HadIni = File.Exists(path);
		if (mf.HadIni)
		{
			mf.OriginalIni = File.ReadAllText(path);
		}
		File.WriteAllText(path, PatchIni(mf.OriginalIni ?? "", mf.PresetFile), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		File.WriteAllText(Path.Combine(win64, "drip-look.json"), JsonSerializer.Serialize(mf, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		return count;
		void Copy(string src, string rel)
		{
			string text4 = Path.Combine(win64, rel);
			Directory.CreateDirectory(Path.GetDirectoryName(text4));
			if (File.Exists(text4))
			{
				File.SetAttributes(text4, FileAttributes.Normal);
			}
			if (!File.Exists(text4))
			{
				File.Copy(src, text4);
				mf.Files.Add(rel);
				count++;
			}
		}
	}

	public static int Remove(string win64, bool checkGame = true)
	{
		if (checkGame)
		{
			Guard();
		}
		Manifest manifest = Installed(win64);
		if (manifest == null)
		{
			return 0;
		}
		int num = 0;
		foreach (string file in manifest.Files)
		{
			string path = Path.Combine(win64, file);
			try
			{
				if (File.Exists(path))
				{
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
					num++;
				}
			}
			catch
			{
			}
		}
		string[] array = new string[2] { "reshade-shaders", "reshade-presets" };
		foreach (string path2 in array)
		{
			string text = Path.Combine(win64, path2);
			if (Directory.Exists(text))
			{
				PruneEmpty(text);
			}
		}
		string path3 = Path.Combine(win64, "ReShade.ini");
		if (manifest.HadIni && manifest.OriginalIni != null)
		{
			File.WriteAllText(path3, manifest.OriginalIni, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		else if (File.Exists(path3))
		{
			File.Delete(path3);
		}
		File.Delete(Path.Combine(win64, "drip-look.json"));
		return num;
	}

	private static void PruneEmpty(string dir)
	{
		string[] directories = Directory.GetDirectories(dir);
		for (int i = 0; i < directories.Length; i++)
		{
			PruneEmpty(directories[i]);
		}
		if (!Directory.EnumerateFileSystemEntries(dir).Any())
		{
			try
			{
				Directory.Delete(dir);
			}
			catch
			{
			}
		}
	}

	public static string PatchIni(string text, string presetRel)
	{
		string text2 = ((text.Contains("\r\n") || text.Length == 0) ? "\r\n" : "\n");
		List<string> lines = ((text.Length == 0) ? new List<string>() : text.Replace("\r\n", "\n").Split('\n').ToList());
		string preset = ".\\" + presetRel.Replace('/', '\\');
		string shaders = ".\\reshade-shaders\\Shaders\\**";
		string textures = ".\\reshade-shaders\\Textures\\**";
		int g = lines.FindIndex((string l) => l.Trim().Equals("[GENERAL]", StringComparison.OrdinalIgnoreCase));
		if (g < 0)
		{
			lines.Add("[GENERAL]");
			g = lines.Count - 1;
		}
		int end = lines.FindIndex(g + 1, (string l) => l.TrimStart().StartsWith('['));
		if (end < 0)
		{
			end = lines.Count;
		}
		SetKey("PresetPath", (string? _) => preset);
		SetKey("EffectSearchPaths", (string? c) => Merge(c, shaders));
		SetKey("TextureSearchPaths", (string? c) => Merge(c, textures));
		return string.Join(text2, lines).TrimEnd(new char[2] { '\n', '\r' }) + text2;
		static string Merge(string? cur, string add)
		{
			List<string> list = (cur ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
			if (!list.Contains(add, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(add);
			}
			return string.Join(',', list);
		}
		void SetKey(string key, Func<string?, string> make)
		{
			int num = lines.FindIndex(g + 1, end - g - 1, (string l) => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
			if (num >= 0)
			{
				lines[num] = key + "=" + make(lines[num].Substring(lines[num].IndexOf('=') + 1));
			}
			else
			{
				lines.Insert(end, key + "=" + make(null));
				end++;
			}
		}
	}
}
