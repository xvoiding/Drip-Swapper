using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.RocketLeague;

public sealed class RlKeys
{
	private int _lastHit;

	public IReadOnlyList<byte[]> All { get; }

	public RlKeys(IEnumerable<byte[]> keys)
	{
		All = keys.ToList();
	}

	public static string DefaultPath()
	{
		return Path.Combine(AppPaths.DataDir, "RocketLeague", "keys.txt");
	}

	public static RlKeys Parse(string text)
	{
		List<byte[]> keys = new List<byte[]>();
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (text2.Length == 0 || text2.StartsWith('#') || text2.StartsWith("//"))
			{
				continue;
			}
			if (text2.Contains(','))
			{
				string[] array2 = text2.Split(',');
				for (int j = 0; j < array2.Length; j++)
				{
					string text3 = array2[j].Trim().Trim('"').Trim();
					if (text3.Length >= 22)
					{
						Add(One(text3));
					}
				}
			}
			else
			{
				Add(One(text2));
			}
		}
		return new RlKeys(keys);
		void Add(byte[]? k)
		{
			bool flag;
			if (k != null)
			{
				int num = k.Length;
				if (num == 16 || num == 24 || num == 32)
				{
					flag = true;
					goto IL_0033;
				}
			}
			flag = false;
			goto IL_0033;
			IL_0033:
			if (flag && !keys.Any((byte[] x) => ((ReadOnlySpan<byte>)x.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)k)))
			{
				keys.Add(k);
			}
		}
		static byte[]? One(string s)
		{
			byte[] array3 = null;
			try
			{
				array3 = Convert.FromBase64String(s);
			}
			catch (FormatException)
			{
			}
			bool flag;
			if (array3 != null)
			{
				int num = array3.Length;
				if (num == 16 || num == 24 || num == 32)
				{
					flag = true;
					goto IL_002a;
				}
			}
			flag = false;
			goto IL_002a;
			IL_002a:
			bool flag2 = !flag;
			if (flag2)
			{
				int length = s.Length;
				bool flag3 = ((length == 32 || length == 48 || length == 64) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				try
				{
					array3 = Convert.FromHexString(s);
				}
				catch (FormatException)
				{
				}
			}
			return array3;
		}
	}

	public static RlKeys? LoadSaved()
	{
		try
		{
			string path = DefaultPath();
			return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
		}
		catch
		{
			return null;
		}
	}

	public static RlKeys Import(string path)
	{
		RlKeys rlKeys = Parse(File.ReadAllText(path));
		if (rlKeys.All.Count == 0)
		{
			throw new InvalidDataException("No keys found in that file. It should have one base64 key per line.");
		}
		string text = DefaultPath();
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(text), StringComparison.OrdinalIgnoreCase))
		{
			File.Copy(path, text, overwrite: true);
		}
		return rlKeys;
	}

	public static IReadOnlyList<string> AppKeyFolders()
	{
		string text = AppContext.BaseDirectory.TrimEnd(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		});
		return new _003C_003Ez__ReadOnlyArray<string>(new string[2]
		{
			text,
			Path.Combine(text, "keys")
		});
	}

	private static bool Usable(string f, ref DateTime bestTime)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(f);
			long length = fileInfo.Length;
			bool flag = ((length > 8000000 || length == 0L) ? true : false);
			if (flag || fileInfo.LastWriteTimeUtc <= bestTime)
			{
				return false;
			}
			if (Parse(File.ReadAllText(f)).All.Count == 0)
			{
				return false;
			}
			bestTime = fileInfo.LastWriteTimeUtc;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string? Newest(string root, bool recurse)
	{
		string result = null;
		DateTime bestTime = DateTime.MinValue;
		try
		{
			EnumerationOptions enumerationOptions = new EnumerationOptions
			{
				RecurseSubdirectories = recurse,
				MaxRecursionDepth = 3,
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.System
			};
			foreach (string item in Directory.EnumerateFiles(root, "keys.txt", enumerationOptions).Concat(Directory.EnumerateFiles(root, "products.csv", enumerationOptions)))
			{
				if (Usable(item, ref bestTime))
				{
					result = item;
				}
			}
		}
		catch
		{
		}
		return result;
	}

	public static string? FindCandidate(IEnumerable<string>? extraRoots = null)
	{
		foreach (string item in AppKeyFolders())
		{
			if (Directory.Exists(item))
			{
				string text = Newest(item, recurse: false);
				if (text != null)
				{
					return text;
				}
			}
		}
		List<string> list = new List<string>();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		list.Add(Path.Combine(folderPath, "Downloads"));
		list.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
		list.Add(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
		if (extraRoots != null)
		{
			list.AddRange(extraRoots);
		}
		string result = null;
		DateTime bestTime = DateTime.MinValue;
		foreach (string item2 in list.Where((string r) => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct())
		{
			string text2 = Newest(item2, recurse: true);
			if (text2 != null && Usable(text2, ref bestTime))
			{
				result = text2;
			}
		}
		return result;
	}

	public static string? FindAutoLoad(bool haveKeys)
	{
		string text = FindCandidate();
		if (text == null)
		{
			return null;
		}
		if (!haveKeys)
		{
			return text;
		}
		string dir = Path.GetDirectoryName(text);
		bool flag = AppKeyFolders().Any((string r) => string.Equals(r, dir, StringComparison.OrdinalIgnoreCase));
		try
		{
			string path = DefaultPath();
			return (flag && File.Exists(path) && File.GetLastWriteTimeUtc(text) > File.GetLastWriteTimeUtc(path)) ? text : null;
		}
		catch
		{
			return null;
		}
	}

	public byte[]? Find(byte[] file, UpkSummary summary)
	{
		if (All.Count == 0)
		{
			return null;
		}
		int num = Math.Clamp(_lastHit, 0, All.Count - 1);
		for (int i = 0; i < All.Count; i++)
		{
			int num2 = (num + i) % All.Count;
			if (UpkCodec.KeyMatches(file, summary, All[num2]))
			{
				_lastHit = num2;
				return All[num2];
			}
		}
		return null;
	}
}
