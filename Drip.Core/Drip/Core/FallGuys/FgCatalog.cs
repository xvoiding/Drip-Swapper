using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Drip.Core.FallGuys;

public sealed class FgCatalog
{
	private string _json = "";

	private string[] _ids = Array.Empty<string>();

	private int[] _e = Array.Empty<int>();

	private byte[] _extra = Array.Empty<byte>();

	private (string Key, int[] Entries)[] _buckets = Array.Empty<(string, int[])>();

	private string[] _typeNames = Array.Empty<string>();

	private Dictionary<int, List<string>> _keysOfEntry = new Dictionary<int, List<string>>();

	private const int Fields = 7;

	public int EntryCount => _e.Length / 7;

	public IReadOnlyList<FgAsset> Assets { get; private set; } = Array.Empty<FgAsset>();

	public IReadOnlyDictionary<string, FgAsset> ByAddress { get; private set; } = new Dictionary<string, FgAsset>();

	public static FgCatalog Load(string catalogBundlePath)
	{
		AssetsManager assetsManager = new AssetsManager();
		BundleFileInstance bunInst = assetsManager.LoadBundleFile(catalogBundlePath);
		AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bunInst, 0);
		string text = null;
		using (List<AssetFileInfo>.Enumerator enumerator = assetsFileInstance.file.GetAssetsOfType(AssetClassID.TextAsset).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				AssetFileInfo current = enumerator.Current;
				text = ReadTextAsset(assetsFileInstance, current, out string _);
			}
		}
		assetsManager.UnloadAll();
		if (text == null)
		{
			throw new InvalidDataException("The catalog bundle has no catalog inside.");
		}
		FgCatalog fgCatalog = new FgCatalog();
		fgCatalog.Parse(text);
		return fgCatalog;
	}

	private static string ReadTextAsset(AssetsFileInstance afi, AssetFileInfo info, out string name)
	{
		AssetsFileReader reader = afi.file.Reader;
		reader.Position = info.GetAbsoluteByteOffset(afi.file);
		name = reader.ReadCountStringInt32();
		reader.Align();
		int count = reader.ReadInt32();
		byte[] bytes = reader.ReadBytes(count);
		return Encoding.UTF8.GetString(bytes);
	}

	private static byte[] BuildTextAsset(string name, string text)
	{
		using MemoryStream memoryStream = new MemoryStream();
		AssetsFileWriter assetsFileWriter = new AssetsFileWriter(memoryStream);
		assetsFileWriter.WriteCountStringInt32(name);
		assetsFileWriter.Align();
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		assetsFileWriter.Write(bytes.Length);
		assetsFileWriter.Write(bytes);
		assetsFileWriter.Align();
		return memoryStream.ToArray();
	}

	private void Parse(string json)
	{
		_json = json;
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement rootElement = jsonDocument.RootElement;
		_ids = (from x in rootElement.GetProperty("m_InternalIds").EnumerateArray()
			select x.GetString() ?? "").ToArray();
		_typeNames = (from x in rootElement.GetProperty("m_resourceTypes").EnumerateArray()
			select x.GetProperty("m_ClassName").GetString() ?? "").ToArray();
		byte[] k = Convert.FromBase64String(rootElement.GetProperty("m_KeyDataString").GetString());
		byte[] value = Convert.FromBase64String(rootElement.GetProperty("m_BucketDataString").GetString());
		byte[] array = Convert.FromBase64String(rootElement.GetProperty("m_EntryDataString").GetString());
		_extra = Convert.FromBase64String(rootElement.GetProperty("m_ExtraDataString").GetString());
		int num = BitConverter.ToInt32(value, 0);
		int num2 = 4;
		_buckets = new (string, int[])[num];
		for (int num3 = 0; num3 < num; num3++)
		{
			int off = BitConverter.ToInt32(value, num2);
			int num4 = BitConverter.ToInt32(value, num2 + 4);
			num2 += 8;
			int[] array2 = new int[num4];
			for (int num5 = 0; num5 < num4; num5++)
			{
				array2[num5] = BitConverter.ToInt32(value, num2 + 4 * num5);
			}
			num2 += 4 * num4;
			_buckets[num3] = (Key: ReadKey(k, off), Entries: array2);
		}
		int num6 = BitConverter.ToInt32(array, 0);
		_e = new int[num6 * 7];
		Buffer.BlockCopy(array, 4, _e, 0, num6 * 7 * 4);
		_keysOfEntry = new Dictionary<int, List<string>>();
		(string, int[])[] buckets = _buckets;
		for (int num7 = 0; num7 < buckets.Length; num7++)
		{
			(string, int[]) tuple = buckets[num7];
			string item = tuple.Item1;
			int[] item2 = tuple.Item2;
			foreach (int key in item2)
			{
				if (!_keysOfEntry.TryGetValue(key, out List<string> value2))
				{
					value2 = (_keysOfEntry[key] = new List<string>());
				}
				value2.Add(item);
			}
		}
		BuildAssets();
	}

	private static string ReadKey(byte[] k, int off)
	{
		switch (k[off])
		{
		case 0:
		{
			int count2 = BitConverter.ToInt32(k, off + 1);
			return Encoding.UTF8.GetString(k, off + 5, count2);
		}
		case 1:
		{
			int count = BitConverter.ToInt32(k, off + 1);
			return Encoding.Unicode.GetString(k, off + 5, count);
		}
		case 2:
			return BitConverter.ToUInt32(k, off + 1).ToString();
		case 3:
			return BitConverter.ToUInt16(k, off + 1).ToString();
		case 4:
			return BitConverter.ToUInt64(k, off + 1).ToString();
		default:
			return "?";
		}
	}

	private int F(int entry, int field)
	{
		return _e[entry * 7 + field];
	}

	private void BuildAssets()
	{
		List<FgAsset> list = new List<FgAsset>();
		Dictionary<string, FgAsset> dictionary = new Dictionary<string, FgAsset>(StringComparer.Ordinal);
		for (int i = 0; i < EntryCount; i++)
		{
			string text = _ids[F(i, 0)];
			if (!text.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) && _keysOfEntry.TryGetValue(i, out List<string> value))
			{
				string text2 = value.FirstOrDefault((string k) => k.Contains('/') && !k.StartsWith("{", StringComparison.Ordinal) && k.Contains('.'));
				if (text2 != null)
				{
					string bundleFile = BundleFileOf(i);
					string typeName = ((F(i, 6) >= 0 && F(i, 6) < _typeNames.Length) ? _typeNames[F(i, 6)] : "");
					FgAsset fgAsset = new FgAsset(i, text2, text, bundleFile, typeName);
					list.Add(fgAsset);
					dictionary[text2] = fgAsset;
				}
			}
		}
		Assets = list;
		ByAddress = dictionary;
	}

	public string? BundleOfKey(string key)
	{
		(string, int[])[] buckets = _buckets;
		for (int i = 0; i < buckets.Length; i++)
		{
			var (text, array) = buckets[i];
			if (!(text == key))
			{
				continue;
			}
			int[] array2 = array;
			foreach (int entry in array2)
			{
				if (!_ids[F(entry, 0)].EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
				{
					string text2 = BundleFileOf(entry);
					if (text2.Length > 0)
					{
						return text2;
					}
				}
			}
		}
		return null;
	}

	public string BundleFileOf(int entry)
	{
		int num = F(entry, 2);
		if (num < 0 || num >= _buckets.Length)
		{
			return "";
		}
		int[] item = _buckets[num].Entries;
		if (item.Length == 0)
		{
			return "";
		}
		string text = _ids[F(item[0], 0)];
		return text.Substring(text.LastIndexOfAny(new char[2] { '\\', '/' }) + 1);
	}

	public IReadOnlyList<string> DependencyBundles(int entry)
	{
		int num = F(entry, 2);
		List<string> list = new List<string>();
		if (num < 0 || num >= _buckets.Length)
		{
			return list;
		}
		int[] item = _buckets[num].Entries;
		foreach (int entry2 in item)
		{
			string text = _ids[F(entry2, 0)];
			list.Add(text.Substring(text.LastIndexOfAny(new char[2] { '\\', '/' }) + 1));
		}
		return list;
	}

	public void Retarget(int dst, int src)
	{
		int[] array = new int[6] { 0, 1, 2, 3, 4, 6 };
		foreach (int num in array)
		{
			_e[dst * 7 + num] = _e[src * 7 + num];
		}
	}

	public int ZeroCrc(IEnumerable<string> bundleFiles)
	{
		HashSet<string> hashSet = new HashSet<string>(bundleFiles, StringComparer.OrdinalIgnoreCase);
		int num = 0;
		for (int i = 0; i < EntryCount; i++)
		{
			string text = _ids[F(i, 0)];
			if (!text.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string item = text.Substring(text.LastIndexOfAny(new char[2] { '\\', '/' }) + 1);
			if (hashSet.Contains(item))
			{
				int num2 = F(i, 4);
				if (num2 >= 0 && num2 < _extra.Length && _extra[num2] == 7 && PatchCrcAt(num2))
				{
					num++;
				}
			}
		}
		return num;
	}

	private bool PatchCrcAt(int off)
	{
		int q = off + 1;
		q = SkipString(q);
		q = SkipString(q);
		int num = BitConverter.ToInt32(_extra, q);
		q += 4;
		if (num <= 0 || q + num > _extra.Length)
		{
			return false;
		}
		string text = Encoding.Unicode.GetString(_extra, q, num);
		int num2 = text.IndexOf("\"m_Crc\":", StringComparison.Ordinal);
		if (num2 < 0)
		{
			return false;
		}
		int num3 = num2 + "\"m_Crc\":".Length;
		int i;
		for (i = num3; i < text.Length && char.IsDigit(text[i]); i++)
		{
		}
		if (i == num3)
		{
			return false;
		}
		if (i - num3 == 1 && text[num3] == '0')
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(text);
		stringBuilder[num3] = '0';
		for (int j = num3 + 1; j < i; j++)
		{
			stringBuilder[j] = ' ';
		}
		byte[] bytes = Encoding.Unicode.GetBytes(stringBuilder.ToString());
		if (bytes.Length != num)
		{
			return false;
		}
		Buffer.BlockCopy(bytes, 0, _extra, q, num);
		return true;
		int SkipString(int num6)
		{
			int num4 = 0;
			int num5 = 0;
			byte b;
			do
			{
				b = _extra[num6++];
				num4 |= (b & 0x7F) << num5;
				num5 += 7;
			}
			while ((b & 0x80) != 0);
			return num6 + num4;
		}
	}

	public void Save(string originalBundlePath, string outPath)
	{
		byte[] array = new byte[4 + _e.Length * 4];
		BitConverter.GetBytes(EntryCount).CopyTo(array, 0);
		Buffer.BlockCopy(_e, 0, array, 4, _e.Length * 4);
		string json = SetJsonString(_json, "m_EntryDataString", Convert.ToBase64String(array));
		json = SetJsonString(json, "m_ExtraDataString", Convert.ToBase64String(_extra));
		AssetsManager assetsManager = new AssetsManager();
		BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(originalBundlePath);
		AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
		bool flag = false;
		using (List<AssetFileInfo>.Enumerator enumerator = assetsFileInstance.file.GetAssetsOfType(AssetClassID.TextAsset).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				AssetFileInfo current = enumerator.Current;
				ReadTextAsset(assetsFileInstance, current, out string name);
				current.SetNewData(BuildTextAsset(name, json));
				flag = true;
			}
		}
		if (!flag)
		{
			throw new InvalidDataException("The catalog bundle has no catalog inside.");
		}
		FgBundleIo.WriteBundle(bundleFileInstance, assetsFileInstance, outPath);
		assetsManager.UnloadAll();
	}

	private static string SetJsonString(string json, string key, string value)
	{
		string text = "\"" + key + "\":\"";
		int num = json.IndexOf(text, StringComparison.Ordinal);
		if (num < 0)
		{
			throw new InvalidDataException("Unexpected catalog layout (" + key + ").");
		}
		int num2 = num + text.Length;
		int start = json.IndexOf('"', num2);
		return string.Concat(json.AsSpan(0, num2), value, json.AsSpan(start));
	}
}
