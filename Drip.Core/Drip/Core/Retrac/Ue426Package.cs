using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CUE4Parse.Utils;

namespace Drip.Core.Retrac;

public sealed class Ue426Package
{
	public const int SummarySize = 64;

	public const int ExportEntrySize = 72;

	private const int ExportObjectNameOffset = 16;

	private const int ExportGlobalImportOffset = 56;

	public const ulong NullIndex = ulong.MaxValue;

	private readonly byte[] _summary;

	private ulong _hashAlgorithm;

	private string[] _names = Array.Empty<string>();

	private bool[] _wide = Array.Empty<bool>();

	private ulong[] _hashes = Array.Empty<ulong>();

	private byte[] _imports = Array.Empty<byte>();

	private byte[] _exports = Array.Empty<byte>();

	private byte[] _bundles = Array.Empty<byte>();

	private byte[] _graph = Array.Empty<byte>();

	private byte[] _data = Array.Empty<byte>();

	private const ulong PackageImportBits = 9223372036854775808uL;

	private const ulong IndexMask = 4611686018427387903uL;

	public IReadOnlyList<string> Names => _names;

	public int ExportCount => _exports.Length / 72;

	public int ImportCount => _imports.Length / 8;

	public int HeaderSize => ((64 + NamesBytes() + 7) & -8) + _hashes.Length * 8 + 8 + _imports.Length + _exports.Length + _bundles.Length + _graph.Length;

	public int DataSize => _data.Length;

	public ReadOnlySpan<byte> ExportBytes => _data;

	public int PackageNameIndex => (int)BinaryPrimitives.ReadUInt32LittleEndian(_summary.AsSpan(0));

	public string PackageName => _names[PackageNameIndex];

	private Ue426Package(byte[] summary)
	{
		_summary = summary;
	}

	public IEnumerable<(int Start, int Size, string Name)> ExportRanges()
	{
		long cooked = BinaryPrimitives.ReadUInt32LittleEndian(_summary.AsSpan(20));
		for (int i = 0; i < ExportCount; i++)
		{
			ulong num = BinaryPrimitives.ReadUInt64LittleEndian(_exports.AsSpan(i * 72));
			long num2 = (long)BinaryPrimitives.ReadUInt64LittleEndian(_exports.AsSpan(i * 72 + 8));
			long num3 = (long)num - cooked;
			if (num3 >= 0 && num2 >= 0 && num3 + num2 <= _data.Length)
			{
				yield return (Start: (int)num3, Size: (int)num2, Name: ExportName(i));
			}
		}
	}

	public static Ue426Package Parse(byte[] d)
	{
		if (d.Length < 64)
		{
			throw new InvalidDataException("Package is too small to be a UE 4.26 package.");
		}
		Span<byte> span = d.AsSpan();
		int num = I32(span, 24);
		int num2 = I32(span, 28);
		int num3 = I32(span, 32);
		int num4 = I32(span, 36);
		int num5 = I32(span, 40);
		int num6 = I32(span, 44);
		int num7 = I32(span, 48);
		int num8 = I32(span, 52);
		int num9 = I32(span, 56);
		if (num != 64 || num3 != ((num + num2 + 7) & -8) || num5 != num3 + num4 || num6 < num5 || num7 < num6 || num8 < num7 || num9 < 0 || num8 + num9 > d.Length || num4 < 8 || num4 % 8 != 0 || (num6 - num5) % 8 != 0 || (num7 - num6) % 72 != 0)
		{
			throw new InvalidDataException("Package header does not look like UE 4.26 (section offsets don't line up).");
		}
		Ue426Package ue426Package = new Ue426Package(d[..64]);
		ue426Package._hashAlgorithm = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(num3));
		int num10 = num4 / 8 - 1;
		ue426Package._names = new string[num10];
		ue426Package._wide = new bool[num10];
		ue426Package._hashes = new ulong[num10];
		int num11 = num;
		int num12 = num + num2;
		for (int i = 0; i < num10; i++)
		{
			if (num11 + 2 > num12)
			{
				throw new InvalidDataException("Name map is shorter than its hash table says.");
			}
			byte b = d[num11];
			byte b2 = d[num11 + 1];
			num11 += 2;
			bool flag = (b & 0x80) != 0;
			int num13 = ((b & 0x7F) << 8) | b2;
			int num14 = (flag ? (num13 * 2) : num13);
			if (num11 + num14 > num12)
			{
				throw new InvalidDataException("Name map entry runs past the name map.");
			}
			ue426Package._names[i] = (flag ? Encoding.Unicode.GetString(d, num11, num14) : Encoding.Latin1.GetString(d, num11, num14));
			ue426Package._wide[i] = flag;
			num11 += num14;
			ue426Package._hashes[i] = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(num3 + 8 + i * 8));
		}
		if (num11 != num12)
		{
			throw new InvalidDataException("Name map has bytes left over; layout is different from what this swapper knows.");
		}
		ue426Package._imports = d[num5..num6];
		ue426Package._exports = d[num6..num7];
		ue426Package._bundles = d[num7..num8];
		ue426Package._graph = d[num8..(num8 + num9)];
		ue426Package._data = d[(num8 + num9)..];
		return ue426Package;
	}

	private string ExportName(int export)
	{
		int num = export * 72 + 16;
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(_exports.AsSpan(num));
		uint num3 = BinaryPrimitives.ReadUInt32LittleEndian(_exports.AsSpan(num + 4));
		if (num3 != 0)
		{
			return _names[num2] + "_" + (num3 - 1);
		}
		return _names[num2];
	}

	public ulong[] ExpectedPublicIds()
	{
		int exportCount = ExportCount;
		ulong[] array = new ulong[exportCount];
		for (int i = 0; i < exportCount; i++)
		{
			List<string> list = new List<string>();
			int num = i;
			int num2 = 0;
			bool flag = true;
			while (true)
			{
				list.Add(ExportName(num));
				ulong num3 = BinaryPrimitives.ReadUInt64LittleEndian(_exports.AsSpan(num * 72 + 24));
				if (num3 == ulong.MaxValue)
				{
					break;
				}
				if (num3 >> 62 != 0L || (long)(num3 & 0x3FFFFFFFFFFFFFFFL) >= (long)exportCount || ++num2 > 32)
				{
					flag = false;
					break;
				}
				num = (int)(num3 & 0x3FFFFFFFFFFFFFFFL);
			}
			if (!flag)
			{
				array[i] = 0uL;
				continue;
			}
			list.Reverse();
			string text = PackageName + "/" + string.Join("/", list);
			array[i] = (CityHash.CityHash64(Encoding.Unicode.GetBytes(text.ToLowerInvariant())) & 0x3FFFFFFFFFFFFFFFL) | 0x8000000000000000uL;
		}
		return array;
	}

	public bool[] PublicIdsFollowFormula()
	{
		ulong[] array = ExpectedPublicIds();
		bool[] array2 = new bool[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = array[i] != 0L && GetGlobalImportIndex(i) == array[i];
		}
		return array2;
	}

	public int FindName(string name)
	{
		for (int i = 0; i < _names.Length; i++)
		{
			if (_names[i] == name)
			{
				return i;
			}
		}
		return -1;
	}

	public int FindMainExport(string name)
	{
		int num = FindName(name);
		if (num < 0)
		{
			return -1;
		}
		for (int i = 0; i < ExportCount; i++)
		{
			int num2 = i * 72 + 16;
			uint num3 = BinaryPrimitives.ReadUInt32LittleEndian(_exports.AsSpan(num2));
			uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(_exports.AsSpan(num2 + 4));
			ulong num5 = BinaryPrimitives.ReadUInt64LittleEndian(_exports.AsSpan(i * 72 + 24));
			if (num3 == num && num4 == 0 && num5 == ulong.MaxValue)
			{
				return i;
			}
		}
		return -1;
	}

	public ulong GetGlobalImportIndex(int export)
	{
		return BinaryPrimitives.ReadUInt64LittleEndian(_exports.AsSpan(export * 72 + 56));
	}

	public void SetGlobalImportIndex(int export, ulong value)
	{
		BinaryPrimitives.WriteUInt64LittleEndian(_exports.AsSpan(export * 72 + 56), value);
	}

	public void RenameName(int index, string value)
	{
		_names[index] = value;
		_wide[index] = value.Any((char c) => c > '\u007f');
		_hashes[index] = HashName(value, _wide[index]);
	}

	public static ulong HashName(string name, bool wide)
	{
		string s = name.ToLowerInvariant();
		return CityHash.CityHash64(wide ? Encoding.Unicode.GetBytes(s) : Encoding.Latin1.GetBytes(s));
	}

	public bool HashesMatchNames()
	{
		for (int i = 0; i < _names.Length; i++)
		{
			if (_hashes[i] != HashName(_names[i], _wide[i]))
			{
				return false;
			}
		}
		return true;
	}

	private int NamesBytes()
	{
		int num = 0;
		for (int i = 0; i < _names.Length; i++)
		{
			num += 2 + (_wide[i] ? (_names[i].Length * 2) : _names[i].Length);
		}
		return num;
	}

	public byte[] ToBytes()
	{
		int num = NamesBytes();
		int num2 = 64;
		int num3 = (num2 + num + 7) & -8;
		int num4 = (_hashes.Length + 1) * 8;
		int num5 = num3 + num4;
		int num6 = num5 + _imports.Length;
		int num7 = num6 + _exports.Length;
		int num8 = num7 + _bundles.Length;
		byte[] array = new byte[num8 + _graph.Length + _data.Length];
		_summary.CopyTo(array, 0);
		Span<byte> s = array.AsSpan();
		WriteI32(s, 24, num2);
		WriteI32(s, 28, num);
		WriteI32(s, 32, num3);
		WriteI32(s, 36, num4);
		WriteI32(s, 40, num5);
		WriteI32(s, 44, num6);
		WriteI32(s, 48, num7);
		WriteI32(s, 52, num8);
		WriteI32(s, 56, _graph.Length);
		int num9 = num2;
		for (int i = 0; i < _names.Length; i++)
		{
			string text = _names[i];
			int length = text.Length;
			if (length > 32767)
			{
				throw new InvalidDataException("Name is too long.");
			}
			array[num9++] = (byte)((length >> 8) | (_wide[i] ? 128 : 0));
			array[num9++] = (byte)length;
			if (_wide[i])
			{
				Encoding.Unicode.GetBytes(text, 0, length, array, num9);
				num9 += length * 2;
			}
			else
			{
				Encoding.Latin1.GetBytes(text, 0, length, array, num9);
				num9 += length;
			}
		}
		BinaryPrimitives.WriteUInt64LittleEndian(s.Slice(num3), _hashAlgorithm);
		for (int j = 0; j < _hashes.Length; j++)
		{
			BinaryPrimitives.WriteUInt64LittleEndian(s.Slice(num3 + 8 + j * 8), _hashes[j]);
		}
		_imports.CopyTo(array, num5);
		_exports.CopyTo(array, num6);
		_bundles.CopyTo(array, num7);
		_graph.CopyTo(array, num8);
		_data.CopyTo(array, num8 + _graph.Length);
		return array;
	}

	private static int I32(ReadOnlySpan<byte> s, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(s.Slice(p));
	}

	private static void WriteI32(Span<byte> s, int p, int v)
	{
		BinaryPrimitives.WriteInt32LittleEndian(s.Slice(p), v);
	}

	public static byte[] CloneAs(Ue426Package wanted, string wantedPackage, Ue426Package owned, string ownedPackage)
	{
		string packageName = wanted.PackageName;
		string packageName2 = owned.PackageName;
		if (!packageName.Equals(wantedPackage, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"The file I read says it is '{packageName}', not '{wantedPackage}'.");
		}
		if (!packageName2.Equals(ownedPackage, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"The file I read says it is '{packageName2}', not '{ownedPackage}'.");
		}
		string text = Leaf(packageName);
		string text2 = Leaf(packageName2);
		int num = wanted.FindMainExport(text);
		int num2 = owned.FindMainExport(text2);
		if (num < 0)
		{
			throw new InvalidDataException(text + " has no main asset in it.");
		}
		if (num2 < 0)
		{
			throw new InvalidDataException(text2 + " has no main asset in it.");
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			[packageName] = packageName2,
			[text] = text2,
			[packageName + "." + text] = packageName2 + "." + text2,
			[text + "_C"] = text2 + "_C",
			["Default__" + text + "_C"] = "Default__" + text2 + "_C"
		};
		bool[] array = wanted.PublicIdsFollowFormula();
		for (int i = 0; i < wanted._names.Length; i++)
		{
			if (dictionary.TryGetValue(wanted._names[i], out var value))
			{
				wanted.RenameName(i, value);
			}
		}
		ulong[] array2 = wanted.ExpectedPublicIds();
		for (int j = 0; j < array2.Length; j++)
		{
			if (j != num && array[j] && array2[j] != 0L)
			{
				wanted.SetGlobalImportIndex(j, array2[j]);
			}
		}
		wanted.SetGlobalImportIndex(num, owned.GetGlobalImportIndex(num2));
		return wanted.ToBytes();
	}

	public static string Leaf(string package)
	{
		int num = package.LastIndexOf('/');
		if (num < 0)
		{
			return package;
		}
		return package.Substring(num + 1);
	}
}
