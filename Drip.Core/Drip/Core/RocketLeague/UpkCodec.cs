using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace Drip.Core.RocketLeague;

public static class UpkCodec
{
	private const int BlockSize = 131072;

	private const int BaseEntrySize = 24;

	private static int Round16(int n)
	{
		return (n + 15) & -16;
	}

	private static int RegionLength(UpkSummary s)
	{
		return Round16(s.TotalHeaderSize - s.GarbageSize - s.NameOffset);
	}

	public static bool KeyMatches(byte[] file, UpkSummary s, byte[] key)
	{
		try
		{
			int num = key.Length;
			if ((num != 16 && num != 24 && num != 32) || 1 == 0)
			{
				return false;
			}
			if ((s.CompressionFlags & 1) == 0)
			{
				return false;
			}
			int num2 = RegionLength(s);
			int chunksOffset = s.ChunksOffset;
			int num3 = chunksOffset - chunksOffset % 16;
			if (chunksOffset < 0 || s.NameOffset + num3 + 32 > file.Length || num3 + 32 > num2 + 16)
			{
				return false;
			}
			byte[] array = new byte[32];
			Array.Copy(file, s.NameOffset + num3, array, 0, 32);
			byte[] array2 = Ecb(key).DecryptEcb(array, PaddingMode.None);
			int num4 = chunksOffset % 16;
			int num5 = BinaryPrimitives.ReadInt32LittleEndian(array2.AsSpan(num4, 4));
			int num6 = BinaryPrimitives.ReadInt32LittleEndian(array2.AsSpan(num4 + 4, 4));
			return num5 >= 1 && num6 == s.DependsOffset;
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	private static Aes Ecb(byte[] key)
	{
		Aes aes = Aes.Create();
		aes.Key = key;
		return aes;
	}

	public static UpkImage Decrypt(byte[] file, byte[] key)
	{
		UpkSummary upkSummary = UpkSummary.Parse(file);
		if ((upkSummary.CompressionFlags & 1) == 0)
		{
			throw new UpkException("This package isn't compressed the way the swapper expects.");
		}
		int num = RegionLength(upkSummary);
		if (upkSummary.NameOffset + num > file.Length)
		{
			throw new UpkException("Package is cut short.");
		}
		byte[] array = new byte[num];
		Array.Copy(file, upkSummary.NameOffset, array, 0, num);
		byte[] array2 = Ecb(key).DecryptEcb(array, PaddingMode.None);
		int num2 = EntrySizeFor(array2, upkSummary);
		List<UpkChunk> list = ReadChunkTable(array2, upkSummary.ChunksOffset, num2);
		if (list.Count == 0 || list[0].UncompressedOffset != upkSummary.DependsOffset)
		{
			throw new UpkException("Decryption key doesn't match this package.");
		}
		UpkChunk upkChunk = list[list.Count - 1];
		long num3 = upkChunk.UncompressedOffset + upkChunk.UncompressedSize;
		if (num3 <= 0 || num3 > int.MaxValue)
		{
			throw new UpkException("Package size is not valid.");
		}
		byte[] array3 = new byte[num3];
		Array.Copy(file, 0, array3, 0, upkSummary.NameOffset);
		Array.Copy(array2, 0, array3, upkSummary.NameOffset, (int)Math.Min(array2.Length, num3 - upkSummary.NameOffset));
		foreach (UpkChunk item in list)
		{
			long compressedOffset = item.CompressedOffset;
			if (compressedOffset < 0 || compressedOffset + 16 > file.Length)
			{
				throw new UpkException("Chunk points outside the file.");
			}
			int num4 = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan((int)compressedOffset + 12, 4));
			List<(int, int)> list2 = new List<(int, int)>();
			long num5 = compressedOffset + 16;
			long num6 = 0L;
			while (num6 < num4)
			{
				int num7 = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan((int)num5, 4));
				int num8 = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan((int)num5 + 4, 4));
				if (num7 <= 0 || num8 <= 0)
				{
					throw new UpkException("Bad compression block.");
				}
				list2.Add((num7, num8));
				num6 += num8;
				num5 += 8;
			}
			long num9 = item.UncompressedOffset;
			foreach (var (num10, num11) in list2)
			{
				if (num5 + num10 > file.Length || num9 + num11 > array3.Length)
				{
					throw new UpkException("Compression block runs past the end.");
				}
				using ZLibStream zLibStream = new ZLibStream(new MemoryStream(file, (int)num5, num10), CompressionMode.Decompress);
				int num12;
				for (int i = 0; i < num11; i += num12)
				{
					num12 = zLibStream.Read(array3, (int)num9 + i, num11 - i);
					if (num12 <= 0)
					{
						throw new UpkException("Compression block is shorter than it says.");
					}
				}
				num9 += num11;
				num5 += num10;
			}
		}
		int num13 = Math.Max(0, upkSummary.TotalHeaderSize - upkSummary.GarbageSize);
		int num14 = Math.Min(upkSummary.GarbageSize, Math.Max(0, file.Length - num13));
		byte[] array4 = new byte[upkSummary.GarbageSize];
		if (num14 > 0)
		{
			Array.Copy(file, num13, array4, 0, num14);
		}
		return new UpkImage
		{
			Data = array3,
			Summary = upkSummary,
			Chunks = list,
			RegionPlain = array2,
			Gap = array4,
			ChunkEntrySize = num2
		};
	}

	private static int EntrySizeFor(byte[] plain, UpkSummary s)
	{
		int chunksOffset = s.ChunksOffset;
		if (chunksOffset < 0 || chunksOffset + 4 > plain.Length)
		{
			return 24;
		}
		int num = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(chunksOffset, 4));
		int num2 = s.TotalHeaderSize - s.GarbageSize - s.NameOffset - chunksOffset - 4;
		if (num < 1 || num2 < num * 24 || num2 % num != 0)
		{
			return 24;
		}
		int num3 = num2 / num;
		if (num3 < 24 || num3 > 64)
		{
			return 24;
		}
		return num3;
	}

	private static List<UpkChunk> ReadChunkTable(byte[] plain, int c, int entrySize)
	{
		List<UpkChunk> list = new List<UpkChunk>();
		if (c < 0 || c + 4 > plain.Length)
		{
			return list;
		}
		int num = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(c, 4));
		if (num < 1 || num > 4096 || c + 4 + (long)num * (long)entrySize > plain.Length)
		{
			return list;
		}
		for (int i = 0; i < num; i++)
		{
			Span<byte> span = plain.AsSpan(c + 4 + i * entrySize, 24);
			list.Add(new UpkChunk(BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0, 8)), BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4)), BinaryPrimitives.ReadInt64LittleEndian(span.Slice(12, 8)), BinaryPrimitives.ReadInt32LittleEndian(span.Slice(20, 4))));
		}
		return list;
	}

	public static byte[] Encrypt(UpkImage template, byte[] data, byte[] outKey)
	{
		return Encrypt(template, data, outKey, null);
	}

	public static byte[] Encrypt(UpkImage template, byte[] data, byte[] outKey, (long Start, int OldLen, int NewLen) edit)
	{
		return Encrypt(template, data, outKey, new (long, int, int)[1] { edit });
	}

	public static byte[] Encrypt(UpkImage template, byte[] data, byte[] outKey, IReadOnlyList<(long Start, int OldLen, int NewLen)>? edits)
	{
		UpkSummary upkSummary = UpkSummary.Parse(data);
		int nameOffset = upkSummary.NameOffset;
		int dependsOffset = upkSummary.DependsOffset;
		int num = dependsOffset - nameOffset;
		long shift = dependsOffset - template.Chunks[0].UncompressedOffset;
		int count = template.Chunks.Count;
		int chunkEntrySize = template.ChunkEntrySize;
		int num2 = 4 + chunkEntrySize * count;
		int num3 = num + num2;
		int num4 = Round16(num3);
		int num5 = nameOffset + num3 + template.Summary.GarbageSize;
		byte[] array = new byte[num4];
		Array.Copy(template.RegionPlain, 0, array, 0, Math.Min(template.RegionPlain.Length, num4));
		Array.Clear(array, num, num4 - num);
		Array.Copy(data, nameOffset, array, 0, num);
		List<byte[]> list = new List<byte[]>(count);
		List<UpkChunk> list2 = new List<UpkChunk>(count);
		long num6 = num5;
		long num7 = -1L;
		for (int i = 0; i < count; i++)
		{
			long num8 = ((i == 0) ? Map(template.Chunks[0].UncompressedOffset) : Math.Max(Map(template.Chunks[i].UncompressedOffset), num7 + 1));
			long num9 = ((i + 1 < count) ? Math.Max(Map(template.Chunks[i + 1].UncompressedOffset), num8 + 1) : data.Length);
			num7 = num8;
			if (num8 < 0 || num9 > data.Length || num9 < num8)
			{
				throw new UpkException("Chunk layout is inconsistent.");
			}
			byte[] array2 = CompressChunk(data.AsSpan((int)num8, (int)(num9 - num8)));
			list.Add(array2);
			list2.Add(new UpkChunk(num8, (int)(num9 - num8), num6, array2.Length));
			num6 += array2.Length;
		}
		BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num, 4), count);
		for (int j = 0; j < count; j++)
		{
			Span<byte> span = array.AsSpan(num + 4 + j * chunkEntrySize, chunkEntrySize);
			int num10 = template.Summary.ChunksOffset + 4 + j * chunkEntrySize + 24;
			if (chunkEntrySize > 24 && num10 + (chunkEntrySize - 24) <= template.RegionPlain.Length)
			{
				Array.Copy(template.RegionPlain, num10, array, num + 4 + j * chunkEntrySize + 24, chunkEntrySize - 24);
			}
			BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, 8), list2[j].UncompressedOffset);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), list2[j].UncompressedSize);
			BinaryPrimitives.WriteInt64LittleEndian(span.Slice(12, 8), list2[j].CompressedOffset);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(20, 4), list2[j].CompressedSize);
		}
		byte[] array3 = Ecb(outKey).EncryptEcb(array, PaddingMode.None);
		byte[] array4 = new byte[nameOffset];
		Array.Copy(data, 0, array4, 0, nameOffset);
		BinaryPrimitives.WriteInt32LittleEndian(array4.AsSpan(8, 4), num5);
		BinaryPrimitives.WriteInt32LittleEndian(array4.AsSpan(upkSummary.MetaPos + 4, 4), num);
		using (MemoryStream memoryStream = new MemoryStream())
		{
			memoryStream.Write(array4);
			memoryStream.Write(array3);
			int num11 = Math.Min(num4 - num3, template.Gap.Length);
			memoryStream.Write(template.Gap, num11, template.Gap.Length - num11);
			foreach (byte[] item in list)
			{
				memoryStream.Write(item);
			}
			return memoryStream.ToArray();
		}
		long Map(long o)
		{
			long num12 = o;
			if (edits != null)
			{
				foreach (var edit in edits)
				{
					if (num12 > edit.Start)
					{
						num12 = ((num12 < edit.Start + edit.OldLen) ? (edit.Start + Math.Min(num12 - edit.Start, edit.NewLen)) : (num12 + (edit.NewLen - edit.OldLen)));
					}
				}
			}
			return num12 + shift;
		}
	}

	private static byte[] CompressChunk(ReadOnlySpan<byte> raw)
	{
		List<byte[]> list = new List<byte[]>();
		for (int i = 0; i < raw.Length; i += 131072)
		{
			ReadOnlySpan<byte> buffer = raw.Slice(i, Math.Min(131072, raw.Length - i));
			using MemoryStream memoryStream = new MemoryStream();
			using (ZLibStream zLibStream = new ZLibStream(memoryStream, CompressionLevel.SmallestSize, leaveOpen: true))
			{
				zLibStream.Write(buffer);
			}
			list.Add(memoryStream.ToArray());
		}
		int v = list.Sum((byte[] b) => b.Length);
		MemoryStream o2 = new MemoryStream();
		byte[] w;
		try
		{
			w = new byte[4];
			Put(-1641380927);
			Put(131072);
			Put(v);
			Put(raw.Length);
			int num = raw.Length;
			foreach (byte[] item in list)
			{
				int num2 = Math.Min(131072, num);
				Put(item.Length);
				Put(num2);
				num -= num2;
			}
			foreach (byte[] item2 in list)
			{
				o2.Write(item2);
			}
			return o2.ToArray();
		}
		finally
		{
			if (o2 != null)
			{
				((IDisposable)o2).Dispose();
			}
		}
		void Put(int value)
		{
			BinaryPrimitives.WriteInt32LittleEndian(w, value);
			o2.Write(w);
		}
	}
}
