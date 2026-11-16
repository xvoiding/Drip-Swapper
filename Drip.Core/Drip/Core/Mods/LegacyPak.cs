using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Drip.Core.Mods;

public sealed class LegacyPak
{
	private sealed class Rd
	{
		private readonly byte[] _b;

		private int _p;

		public Rd(byte[] b)
		{
			_b = b;
		}

		private void Need(long n)
		{
			if (n < 0 || _p + n > _b.Length)
			{
				throw new InvalidDataException("The pak's file list is damaged.");
			}
		}

		public void Skip(long n)
		{
			Need(n);
			_p += (int)n;
		}

		public int I32()
		{
			Need(4L);
			int result = BitConverter.ToInt32(_b, _p);
			_p += 4;
			return result;
		}

		public long I64()
		{
			Need(8L);
			long result = BitConverter.ToInt64(_b, _p);
			_p += 8;
			return result;
		}

		public string FString()
		{
			int num = I32();
			if (num == 0)
			{
				return "";
			}
			if (num < 0)
			{
				long num2 = -num * 2;
				Need(num2);
				string result = Encoding.Unicode.GetString(_b, _p, (int)num2 - 2);
				_p += (int)num2;
				return result;
			}
			Need(num);
			string result2 = Encoding.Latin1.GetString(_b, _p, num - 1);
			_p += num;
			return result2;
		}
	}

	private const uint Magic = 1517228769u;

	private readonly List<PakEntry> _entries = new List<PakEntry>();

	private readonly Dictionary<string, PakEntry> _byName = new Dictionary<string, PakEntry>(StringComparer.OrdinalIgnoreCase);

	public string FilePath { get; }

	public int Version { get; }

	public string MountPoint { get; }

	public IReadOnlyList<PakEntry> Entries => _entries;

	private LegacyPak(string path, int version, string mount)
	{
		FilePath = path;
		Version = version;
		MountPoint = mount;
	}

	public static LegacyPak Open(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		long length = fileStream.Length;
		int num = (int)Math.Min(length, 400L);
		byte[] array = new byte[num];
		fileStream.Seek(length - num, SeekOrigin.Begin);
		fileStream.ReadExactly(array);
		int num2 = -1;
		for (int num3 = num - 4; num3 >= 0; num3--)
		{
			if (BitConverter.ToUInt32(array, num3) == 1517228769)
			{
				num2 = num3;
				break;
			}
		}
		if (num2 < 0 || num2 + 24 > num)
		{
			throw new InvalidDataException("Not an Unreal .pak file (no footer found).");
		}
		int num4 = BitConverter.ToInt32(array, num2 + 4);
		long num5 = BitConverter.ToInt64(array, num2 + 8);
		long num6 = BitConverter.ToInt64(array, num2 + 16);
		if (num4 < 3)
		{
			throw new NotSupportedException($"Pak version {num4} is older than this reader supports.");
		}
		if (num4 > 7)
		{
			throw new NotSupportedException($"Pak version {num4} uses a newer index layout that isn't supported here.");
		}
		if (num4 >= 4 && num2 >= 1 && array[num2 - 1] != 0)
		{
			throw new NotSupportedException("This pak's index is encrypted, so its contents can't be listed.");
		}
		if (num5 < 0 || num6 <= 0 || num5 + num6 > length || num6 > 268435456)
		{
			throw new InvalidDataException("The pak's file list is damaged or out of range.");
		}
		byte[] array2 = new byte[num6];
		fileStream.Seek(num5, SeekOrigin.Begin);
		fileStream.ReadExactly(array2);
		Rd rd = new Rd(array2);
		string mount = rd.FString();
		int num7 = rd.I32();
		if (num7 < 0 || num7 > 2000000)
		{
			throw new InvalidDataException("The pak's file list is damaged.");
		}
		LegacyPak legacyPak = new LegacyPak(path, num4, mount);
		for (int i = 0; i < num7; i++)
		{
			string text = rd.FString();
			long num8 = rd.I64();
			long compressedSize = rd.I64();
			long size = rd.I64();
			uint num9 = (uint)rd.I32();
			rd.Skip(20L);
			(long, long)[] array3 = Array.Empty<(long, long)>();
			if (num9 != 0)
			{
				int num10 = rd.I32();
				if (num10 < 0 || num10 > 1000000)
				{
					throw new InvalidDataException("The pak's file list is damaged.");
				}
				array3 = new (long, long)[num10];
				for (int j = 0; j < num10; j++)
				{
					array3[j] = (rd.I64(), rd.I64());
				}
			}
			rd.Skip(5L);
			if (num4 >= 5)
			{
				for (int k = 0; k < array3.Length; k++)
				{
					array3[k] = (array3[k].Item1 + num8, array3[k].Item2 + num8);
				}
			}
			PakEntry pakEntry = new PakEntry(text, num8, compressedSize, size, num9, array3);
			legacyPak._entries.Add(pakEntry);
			legacyPak._byName[text] = pakEntry;
		}
		return legacyPak;
	}

	public bool Contains(string name)
	{
		return _byName.ContainsKey(name);
	}

	public byte[] Read(string name, long maxBytes = 67108864L)
	{
		if (!_byName.TryGetValue(name, out PakEntry value))
		{
			throw new FileNotFoundException(name);
		}
		if (value.Size > maxBytes)
		{
			throw new InvalidDataException("'" + name + "' is too large to read here.");
		}
		using FileStream fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		if (value.Method == 0)
		{
			fileStream.Seek(value.Offset + 48 + 5, SeekOrigin.Begin);
			byte[] array = new byte[value.Size];
			fileStream.ReadExactly(array);
			return array;
		}
		if (value.Method != 1)
		{
			throw new NotSupportedException("'" + name + "' uses a compression method other than zlib.");
		}
		MemoryStream memoryStream = new MemoryStream((int)value.Size);
		(long, long)[] blocks = value.Blocks;
		for (int i = 0; i < blocks.Length; i++)
		{
			var (num, num2) = blocks[i];
			if (num < 0 || num2 < num || num2 > fileStream.Length)
			{
				throw new InvalidDataException("A compressed block points outside the pak.");
			}
			fileStream.Seek(num, SeekOrigin.Begin);
			byte[] array2 = new byte[num2 - num];
			fileStream.ReadExactly(array2);
			using ZLibStream zLibStream = new ZLibStream(new MemoryStream(array2), CompressionMode.Decompress);
			zLibStream.CopyTo(memoryStream);
			if (memoryStream.Length > value.Size + 1)
			{
				throw new InvalidDataException("Decompressed data is larger than the pak says.");
			}
		}
		return memoryStream.ToArray();
	}
}
