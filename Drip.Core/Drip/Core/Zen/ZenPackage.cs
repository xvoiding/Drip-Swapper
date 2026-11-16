using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CUE4Parse.Utils;

namespace Drip.Core.Zen;

public sealed class ZenPackage
{
	public const int FixedSummarySize = 24;

	public const int ExportEntrySize = 72;

	public const int ExportPublicHashOffset = 56;

	public const int ExportObjectNameOffset = 16;

	private uint _origHeaderSize;

	private uint _origCookedHeaderSize;

	private int[] _origOffsets = Array.Empty<int>();

	private int _origNameMapLength;

	public uint NameIndexRaw { get; private set; }

	public uint NameNumber { get; private set; }

	public uint PackageFlags { get; private set; }

	public int OffsetCount { get; private set; }

	public ulong HashVersion { get; private set; }

	public List<ZenName> Names { get; } = new List<ZenName>();

	public byte[] Tail { get; private set; } = Array.Empty<byte>();

	public int HashMismatches { get; private set; }

	public int NameMapStart => 24 + 4 * OffsetCount;

	private int TailStart => NameMapStart + _origNameMapLength;

	public string PackageName => Names[(int)(NameIndexRaw & 0x3FFFFFFF)].Value;

	public int ExportCount => (_origOffsets[3] - _origOffsets[2]) / 72;

	public ZenLayout Layout
	{
		get
		{
			int num = _origOffsets[0];
			int num2 = _origOffsets[1];
			int num3 = _origOffsets[2];
			int num4 = _origOffsets[3];
			int bundleEntries = ((_origOffsets.Length > 4) ? Math.Max(0, (_origOffsets[4] - num4) / 8) : 0);
			int num5 = 0;
			int num6 = num2 - TailStart;
			int num7 = Math.Max(0, (num3 - num2) / 8);
			for (int i = 0; i < num7; i++)
			{
				int num8 = num6 + i * 8;
				if (num8 < 0 || num8 + 8 > Tail.Length)
				{
					break;
				}
				if (BinaryPrimitives.ReadUInt64LittleEndian(Tail.AsSpan(num8)) >> 62 == 2)
				{
					num5++;
				}
			}
			return new ZenLayout(ExportCount, bundleEntries, num7, num5, Math.Max(0, (num2 - num) / 8));
		}
	}

	public static ulong HashName(string value, bool wide)
	{
		string s = value.ToLowerInvariant();
		return CityHash.CityHash64(wide ? Encoding.Unicode.GetBytes(s) : Encoding.ASCII.GetBytes(s));
	}

	public static ZenPackage Parse(byte[] data)
	{
		List<string> list = new List<string>();
		int[] array = new int[5] { 7, 5, 8, 6, 9 };
		foreach (int num in array)
		{
			if (TryParse(data, num, out ZenPackage result, out string error))
			{
				return result;
			}
			list.Add($"K={num}: {error}");
		}
		throw new InvalidDataException("Unrecognised Zen package header layout. " + string.Join(" | ", list));
	}

	private static bool TryParse(byte[] d, int k, out ZenPackage? result, out string error)
	{
		result = null;
		error = "";
		int num = 24 + 4 * k;
		if (d.Length < num + 4)
		{
			error = "too small";
			return false;
		}
		if (U32(d, 0) != 0)
		{
			error = "versioning info present (unsupported)";
			return false;
		}
		uint num2 = U32(d, 4);
		int[] array = new int[k];
		for (int i = 0; i < k; i++)
		{
			array[i] = (int)U32(d, 24 + 4 * i);
		}
		if (num2 < num || num2 > d.Length)
		{
			error = "bad HeaderSize";
			return false;
		}
		for (int j = 0; j < 4; j++)
		{
			if (array[j] <= 0 || array[j] > d.Length)
			{
				error = $"offset {j} out of range";
				return false;
			}
		}
		if (array[0] > array[1] || array[1] > array[2] || array[2] > array[3])
		{
			error = "offsets not ascending";
			return false;
		}
		if (array[3] > num2)
		{
			error = "offset beyond HeaderSize";
			return false;
		}
		if ((array[3] - array[2]) % 72 != 0)
		{
			error = "export map size not multiple of 72";
			return false;
		}
		int num3 = (int)U32(d, num);
		if (num3 < 0 || num3 > 4000000)
		{
			error = "bad name count";
			return false;
		}
		ZenPackage zenPackage = new ZenPackage
		{
			OffsetCount = k,
			_origHeaderSize = num2,
			NameIndexRaw = U32(d, 8),
			NameNumber = U32(d, 12),
			PackageFlags = U32(d, 16),
			_origCookedHeaderSize = U32(d, 20),
			_origOffsets = array
		};
		int num4 = num + 4;
		if (num3 > 0)
		{
			long num5 = 12 + (long)num3 * 8L + (long)num3 * 2L;
			if (num4 + num5 > d.Length)
			{
				error = "name map truncated";
				return false;
			}
			uint num6 = U32(d, num4);
			num4 += 4;
			zenPackage.HashVersion = U64(d, num4);
			num4 += 8;
			ulong[] array2 = new ulong[num3];
			for (int l = 0; l < num3; l++)
			{
				array2[l] = U64(d, num4);
				num4 += 8;
			}
			int[] array3 = new int[num3];
			bool[] array4 = new bool[num3];
			long num7 = 0L;
			for (int m = 0; m < num3; m++)
			{
				byte b = d[num4];
				byte b2 = d[num4 + 1];
				num4 += 2;
				array4[m] = (b & 0x80) != 0;
				array3[m] = ((b & 0x7F) << 8) | b2;
				num7 += (array4[m] ? ((long)array3[m] * 2L) : array3[m]);
			}
			if (num7 != num6)
			{
				error = "string byte count mismatch";
				return false;
			}
			if (num4 + num7 > d.Length)
			{
				error = "strings truncated";
				return false;
			}
			for (int n = 0; n < num3; n++)
			{
				string value;
				if (array4[n])
				{
					value = Encoding.Unicode.GetString(d, num4, array3[n] * 2);
					num4 += array3[n] * 2;
				}
				else
				{
					value = Encoding.UTF8.GetString(d, num4, array3[n]);
					num4 += array3[n];
				}
				ZenName item = new ZenName
				{
					Value = value,
					Hash = array2[n],
					Wide = array4[n]
				};
				if (HashName(value, array4[n]) != array2[n])
				{
					zenPackage.HashMismatches++;
				}
				zenPackage.Names.Add(item);
			}
		}
		if (num4 > array[0])
		{
			error = "name map overlaps first offset";
			return false;
		}
		zenPackage._origNameMapLength = num4 - num;
		zenPackage.Tail = d[num4..];
		if ((zenPackage.NameIndexRaw & 0x3FFFFFFF) >= zenPackage.Names.Count)
		{
			error = "package name index out of range";
			return false;
		}
		result = zenPackage;
		return true;
	}

	public int FindName(string value)
	{
		for (int i = 0; i < Names.Count; i++)
		{
			if (string.Equals(Names[i].Value, value, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	public void ReplaceName(int index, string newValue)
	{
		ZenName zenName = Names[index];
		zenName.Value = newValue;
		zenName.Wide = newValue.Any((char c) => c > '\u007f');
		zenName.Hash = HashName(newValue, zenName.Wide);
	}

	public void CopyNameFrom(int index, ZenPackage other, int otherIndex)
	{
		ZenName zenName = other.Names[otherIndex];
		ZenName zenName2 = Names[index];
		zenName2.Value = zenName.Value;
		zenName2.Hash = zenName.Hash;
		zenName2.Wide = zenName.Wide;
	}

	private int ExportPos(int i)
	{
		if (i < 0 || i >= ExportCount)
		{
			throw new ArgumentOutOfRangeException("i");
		}
		return _origOffsets[2] - TailStart + i * 72;
	}

	public ulong GetExportPublicHash(int i)
	{
		return U64(Tail, ExportPos(i) + 56);
	}

	public void SetExportPublicHash(int i, ulong value)
	{
		BinaryPrimitives.WriteUInt64LittleEndian(Tail.AsSpan(ExportPos(i) + 56), value);
	}

	public string? GetExportName(int i)
	{
		uint num = U32(Tail, ExportPos(i) + 16) & 0x3FFFFFFF;
		if (num >= Names.Count)
		{
			return null;
		}
		return Names[(int)num].Value;
	}

	public int FindExportByName(string name)
	{
		for (int i = 0; i < ExportCount; i++)
		{
			if (string.Equals(GetExportName(i), name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private byte[] WriteNameMap()
	{
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(Names.Count);
		if (Names.Count == 0)
		{
			return memoryStream.ToArray();
		}
		long num = 0L;
		foreach (ZenName name in Names)
		{
			num += (name.Wide ? ((long)name.Value.Length * 2L) : Encoding.UTF8.GetByteCount(name.Value));
		}
		binaryWriter.Write((uint)num);
		binaryWriter.Write(HashVersion);
		foreach (ZenName name2 in Names)
		{
			binaryWriter.Write(name2.Hash);
		}
		foreach (ZenName name3 in Names)
		{
			if (name3.Value.Length > 32767)
			{
				throw new InvalidDataException("Name too long: " + name3.Value);
			}
			binaryWriter.Write((byte)((name3.Value.Length >> 8) | (name3.Wide ? 128 : 0)));
			binaryWriter.Write((byte)(name3.Value.Length & 0xFF));
		}
		foreach (ZenName name4 in Names)
		{
			binaryWriter.Write(name4.Wide ? Encoding.Unicode.GetBytes(name4.Value) : Encoding.UTF8.GetBytes(name4.Value));
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	public byte[] Serialize()
	{
		byte[] array = WriteNameMap();
		int num = array.Length - _origNameMapLength;
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(0u);
		binaryWriter.Write(_origHeaderSize + (uint)num);
		binaryWriter.Write(NameIndexRaw);
		binaryWriter.Write(NameNumber);
		binaryWriter.Write(PackageFlags);
		binaryWriter.Write(_origCookedHeaderSize + (uint)num);
		int[] origOffsets = _origOffsets;
		foreach (int num2 in origOffsets)
		{
			binaryWriter.Write(num2 + num);
		}
		binaryWriter.Write(array);
		binaryWriter.Write(Tail);
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	private static uint U32(byte[] d, int p)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(p));
	}

	private static ulong U64(byte[] d, int p)
	{
		return BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(p));
	}
}
