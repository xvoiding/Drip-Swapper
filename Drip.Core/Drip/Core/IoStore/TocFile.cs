using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Drip.Core.IoStore;

public sealed class TocFile
{
	public const int HeaderBytes = 144;

	public const int ChunkIdSize = 12;

	public const int OffsetLengthSize = 10;

	public const int BlockEntrySize = 12;

	public const byte ExportBundleDataType = 1;

	private static readonly byte[] Magic = "-==--==--==--==-"u8.ToArray();

	private byte[] _raw = Array.Empty<byte>();

	public string Path { get; private set; } = "";

	public byte Version { get; private set; }

	public uint HeaderSize { get; private set; }

	public uint EntryCount { get; private set; }

	public uint BlockCount { get; private set; }

	public uint BlockEntryBytes { get; private set; }

	public uint MethodNameCount { get; private set; }

	public uint MethodNameLength { get; private set; }

	public uint BlockSize { get; private set; }

	public uint DirectoryIndexSize { get; private set; }

	public uint PartitionCount { get; private set; }

	public ulong ContainerId { get; private set; }

	public byte[] EncryptionKeyGuid { get; private set; } = new byte[16];

	public byte Flags { get; private set; }

	public uint PerfectHashSeedsCount { get; private set; }

	public ulong PartitionSize { get; private set; } = ulong.MaxValue;

	public uint ChunksWithoutPerfectHashCount { get; private set; }

	public string[] Methods { get; private set; } = Array.Empty<string>();

	public bool Encrypted => (Flags & 2) != 0;

	public bool Signed => (Flags & 4) != 0;

	public long ChunkIdsPos => 144L;

	public long OffsetLengthsPos => ChunkIdsPos + (long)EntryCount * 12L;

	public long BlocksPos { get; private set; }

	public long SignatureSectionPos => BlocksPos + (long)BlockCount * 12L + (long)MethodNameCount * (long)MethodNameLength;

	public static TocFile Load(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		byte[] array = new byte[144];
		fileStream.ReadExactly(array);
		TocFile tocFile = new TocFile
		{
			Path = path
		};
		tocFile.ParseHeader(array);
		long num = tocFile.BlocksPos + (long)tocFile.BlockCount * 12L + (long)tocFile.MethodNameCount * (long)tocFile.MethodNameLength;
		if (num > fileStream.Length)
		{
			throw new InvalidDataException(path + ": toc truncated.");
		}
		tocFile._raw = new byte[num];
		Array.Copy(array, tocFile._raw, 144);
		fileStream.Position = 144L;
		fileStream.ReadExactly(tocFile._raw.AsSpan(144));
		tocFile.ParseMethods();
		return tocFile;
	}

	private void ParseHeader(byte[] h)
	{
		if (!((ReadOnlySpan<byte>)h.AsSpan(0, 16)).SequenceEqual((ReadOnlySpan<byte>)Magic))
		{
			throw new InvalidDataException(Path + ": bad utoc magic.");
		}
		Version = h[16];
		HeaderSize = U32(h, 20);
		EntryCount = U32(h, 24);
		BlockCount = U32(h, 28);
		BlockEntryBytes = U32(h, 32);
		MethodNameCount = U32(h, 36);
		MethodNameLength = U32(h, 40);
		BlockSize = U32(h, 44);
		DirectoryIndexSize = U32(h, 48);
		PartitionCount = U32(h, 52);
		ContainerId = BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(56));
		EncryptionKeyGuid = h[64..80];
		Flags = h[80];
		PerfectHashSeedsCount = U32(h, 84);
		if (Version >= 3)
		{
			PartitionSize = BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(88));
		}
		ChunksWithoutPerfectHashCount = U32(h, 96);
		if (BlockEntryBytes != 12)
		{
			throw new InvalidDataException($"{Path}: unexpected block entry size {BlockEntryBytes}.");
		}
		if (BlockSize == 0)
		{
			throw new InvalidDataException(Path + ": zero compression block size.");
		}
		if (PartitionSize == 0L)
		{
			PartitionSize = ulong.MaxValue;
		}
		long num = OffsetLengthsPos + (long)EntryCount * 10L;
		if (Version >= 4)
		{
			num += 4L * (long)PerfectHashSeedsCount;
		}
		if (Version >= 5)
		{
			num += 4L * (long)ChunksWithoutPerfectHashCount;
		}
		BlocksPos = num;
	}

	private void ParseMethods()
	{
		long num = BlocksPos + (long)BlockCount * 12L;
		Methods = new string[MethodNameCount];
		for (int i = 0; i < MethodNameCount; i++)
		{
			Span<byte> span = _raw.AsSpan((int)(num + i * MethodNameLength), (int)MethodNameLength);
			int num2 = ((ReadOnlySpan<byte>)span).IndexOf((byte)0);
			Methods[i] = Encoding.ASCII.GetString((num2 >= 0) ? span.Slice(0, num2) : span);
		}
	}

	public (ulong Id, byte Type) GetChunkId(int index)
	{
		int num = (int)(ChunkIdsPos + (long)index * 12L);
		return (Id: BinaryPrimitives.ReadUInt64LittleEndian(_raw.AsSpan(num)), Type: _raw[num + 11]);
	}

	public (ulong Offset, ulong Length) GetOffsetLength(int index)
	{
		int num = (int)(OffsetLengthsPos + (long)index * 10L);
		return (Offset: ReadBE40(_raw, num), Length: ReadBE40(_raw, num + 5));
	}

	public long OffsetLengthPosition(int index)
	{
		return OffsetLengthsPos + (long)index * 10L;
	}

	public long BlockPosition(int blockIndex)
	{
		return BlocksPos + (long)blockIndex * 12L;
	}

	public TocBlock GetBlock(int blockIndex)
	{
		int num = (int)BlockPosition(blockIndex);
		ulong offset = BinaryPrimitives.ReadUInt64LittleEndian(_raw.AsSpan(num)) & 0xFFFFFFFFFFL;
		uint compressedSize = (BinaryPrimitives.ReadUInt32LittleEndian(_raw.AsSpan(num + 4)) >> 8) & 0xFFFFFF;
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(_raw.AsSpan(num + 8));
		return new TocBlock(offset, compressedSize, num2 & 0xFFFFFF, (byte)(num2 >> 24));
	}

	public byte[] RawBlockBytes(int blockIndex)
	{
		return _raw.AsSpan((int)BlockPosition(blockIndex), 12).ToArray();
	}

	public byte[] RawOffsetLengthBytes(int index)
	{
		return _raw.AsSpan((int)OffsetLengthPosition(index), 10).ToArray();
	}

	public IEnumerable<int> FindChunks(ulong id)
	{
		for (int i = 0; i < EntryCount; i++)
		{
			int start = (int)(ChunkIdsPos + (long)i * 12L);
			if (BinaryPrimitives.ReadUInt64LittleEndian(_raw.AsSpan(start)) == id)
			{
				yield return i;
			}
		}
	}

	public static byte[] EncodeBlock(ulong physicalOffset, uint compressed, uint uncompressed, byte method)
	{
		if (physicalOffset > 1099511627775L)
		{
			throw new InvalidDataException("Physical offset exceeds 40 bits.");
		}
		if (compressed > 16777215 || uncompressed > 16777215)
		{
			throw new InvalidDataException("Block size exceeds 24 bits.");
		}
		byte[] array = new byte[12];
		for (int i = 0; i < 5; i++)
		{
			array[i] = (byte)(physicalOffset >> 8 * i);
		}
		for (int j = 0; j < 3; j++)
		{
			array[5 + j] = (byte)(compressed >> 8 * j);
		}
		for (int k = 0; k < 3; k++)
		{
			array[8 + k] = (byte)(uncompressed >> 8 * k);
		}
		array[11] = method;
		return array;
	}

	public static byte[] EncodeOffsetLength(ulong offset, ulong length)
	{
		byte[] array = new byte[10];
		WriteBE40(array, 0, offset);
		WriteBE40(array, 5, length);
		return array;
	}

	public static ulong ReadBE40(byte[] d, int p)
	{
		return ((ulong)d[p] << 32) | ((ulong)d[p + 1] << 24) | ((ulong)d[p + 2] << 16) | ((ulong)d[p + 3] << 8) | d[p + 4];
	}

	public static void WriteBE40(byte[] d, int p, ulong v)
	{
		d[p] = (byte)(v >> 32);
		d[p + 1] = (byte)(v >> 24);
		d[p + 2] = (byte)(v >> 16);
		d[p + 3] = (byte)(v >> 8);
		d[p + 4] = (byte)v;
	}

	public string? DescribeHeaderDifference(TocFile other)
	{
		if (Version != other.Version)
		{
			return $"version {Version} vs {other.Version}";
		}
		if (HeaderSize != other.HeaderSize)
		{
			return $"header size {HeaderSize} vs {other.HeaderSize}";
		}
		if (EntryCount != other.EntryCount)
		{
			return $"entry count {EntryCount} vs {other.EntryCount}";
		}
		if (BlockCount != other.BlockCount)
		{
			return $"compression block count {BlockCount} vs {other.BlockCount}";
		}
		if (BlockEntryBytes != other.BlockEntryBytes)
		{
			return $"block entry size {BlockEntryBytes} vs {other.BlockEntryBytes}";
		}
		if (MethodNameCount != other.MethodNameCount)
		{
			return $"compression method count {MethodNameCount} vs {other.MethodNameCount}";
		}
		if (MethodNameLength != other.MethodNameLength)
		{
			return $"compression method name length {MethodNameLength} vs {other.MethodNameLength}";
		}
		if (BlockSize != other.BlockSize)
		{
			return $"block size {BlockSize} vs {other.BlockSize}";
		}
		if (DirectoryIndexSize != other.DirectoryIndexSize)
		{
			return $"directory index size {DirectoryIndexSize} vs {other.DirectoryIndexSize}";
		}
		if (PartitionCount != other.PartitionCount)
		{
			return $"partition count {PartitionCount} vs {other.PartitionCount}";
		}
		if (ContainerId != other.ContainerId)
		{
			return "container id";
		}
		if (!((ReadOnlySpan<byte>)EncryptionKeyGuid.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)other.EncryptionKeyGuid))
		{
			return "encryption key guid";
		}
		if (Flags != other.Flags)
		{
			return $"container flags {Flags} vs {other.Flags}";
		}
		return null;
	}

	public long[] PartitionEnds()
	{
		int num = (int)Math.Max(PartitionCount, 1u);
		long[] array = new long[num];
		for (int i = 0; i < BlockCount; i++)
		{
			TocBlock block = GetBlock(i);
			int num2 = (int)((PartitionSize != ulong.MaxValue) ? (block.Offset / PartitionSize) : 0);
			long num3 = (long)(((PartitionSize == ulong.MaxValue) ? block.Offset : (block.Offset % PartitionSize)) + IoContainer.Align16(block.CompressedSize));
			if (num2 >= 0 && num2 < num && num3 > array[num2])
			{
				array[num2] = num3;
			}
		}
		return array;
	}

	public string GuidString()
	{
		return string.Concat(from i in Enumerable.Range(0, 4)
			select BinaryPrimitives.ReadUInt32LittleEndian(EncryptionKeyGuid.AsSpan(i * 4)).ToString("X8"));
	}

	private static uint U32(byte[] d, int p)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(p));
	}
}
