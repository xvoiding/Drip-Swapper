using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace Drip.Core.IoStore;

public sealed class IoContainer
{
	public const string BackupSuffix = ".elenr-bak";

	public string Name { get; }

	public string PaksDirectory { get; }

	public string LiveUtocPath => Path.Combine(PaksDirectory, Name + ".utoc");

	public string BackupUtocPath => LiveUtocPath + ".elenr-bak";

	public TocFile Toc { get; }

	public byte[]? Key { get; }

	public string LastUcasPath => UcasPath((int)(Math.Max(Toc.PartitionCount, 1u) - 1));

	public IoContainer(string paksDirectory, string name, Func<string, byte[]?> keyForGuid, bool preferBackup = true)
	{
		PaksDirectory = paksDirectory;
		Name = name;
		Toc = TocFile.Load((preferBackup && File.Exists(BackupUtocPath)) ? BackupUtocPath : LiveUtocPath);
		Key = (Toc.Encrypted ? keyForGuid(Toc.GuidString()) : null);
	}

	public IEnumerable<string> FilesToLock()
	{
		yield return LiveUtocPath;
		yield return UcasPath((int)(Math.Max(Toc.PartitionCount, 1u) - 1));
	}

	private string UcasPath(int partition)
	{
		return Path.Combine(PaksDirectory, (partition == 0) ? (Name + ".ucas") : $"{Name}_s{partition}.ucas");
	}

	public byte[] ReadChunk(int index)
	{
		var (num, num2) = Toc.GetOffsetLength(index);
		if (num2 == 0L)
		{
			return Array.Empty<byte>();
		}
		uint blockSize = Toc.BlockSize;
		long num3 = (long)(num / blockSize);
		long num4 = (long)((num + num2 - 1) / blockSize);
		using MemoryStream memoryStream = new MemoryStream((int)num2);
		for (long num5 = num3; num5 <= num4; num5++)
		{
			TocBlock block = Toc.GetBlock((int)num5);
			byte[] array = ReadPhysical(block.Offset, Align16(block.CompressedSize));
			if (Toc.Encrypted)
			{
				if (Key == null)
				{
					throw new InvalidOperationException(Name + ": container is encrypted but no AES key is available (guid " + Toc.GuidString() + ").");
				}
				array = Decrypt(array, Key);
			}
			memoryStream.Write(Decompress(array, block));
		}
		byte[] array2 = memoryStream.ToArray();
		int num6 = (int)((long)num - num3 * blockSize);
		if (num6 + (int)num2 > array2.Length)
		{
			throw new InvalidDataException($"{Name}: chunk {index} shorter than its table entry.");
		}
		return array2.AsSpan(num6, (int)num2).ToArray();
	}

	private byte[] Decompress(byte[] raw, TocBlock blk)
	{
		if (blk.Method == 0)
		{
			return raw.AsSpan(0, (int)blk.UncompressedSize).ToArray();
		}
		string text = ((blk.Method - 1 < Toc.Methods.Length) ? Toc.Methods[blk.Method - 1] : "?");
		Span<byte> span = raw.AsSpan(0, (int)blk.CompressedSize);
		switch (text.ToLowerInvariant())
		{
		case "none":
			return raw.AsSpan(0, (int)blk.UncompressedSize).ToArray();
		case "zlib":
		{
			using ZLibStream zLibStream = new ZLibStream(new MemoryStream(span.ToArray()), CompressionMode.Decompress);
			byte[] array2 = new byte[blk.UncompressedSize];
			zLibStream.ReadExactly(array2);
			return array2;
		}
		case "gzip":
		{
			using GZipStream gZipStream = new GZipStream(new MemoryStream(span.ToArray()), CompressionMode.Decompress);
			byte[] array = new byte[blk.UncompressedSize];
			gZipStream.ReadExactly(array);
			return array;
		}
		case "oodle":
			return OodleBridge.Decompress(span, (int)blk.UncompressedSize);
		default:
			throw new NotSupportedException(Name + ": compression method '" + text + "' is not supported.");
		}
	}

	private byte[] ReadPhysical(ulong physicalOffset, uint length)
	{
		ulong partitionSize = Toc.PartitionSize;
		int partition = (int)((partitionSize != ulong.MaxValue) ? (physicalOffset / partitionSize) : 0);
		long position = (long)((partitionSize == ulong.MaxValue) ? physicalOffset : (physicalOffset % partitionSize));
		using FileStream fileStream = new FileStream(UcasPath(partition), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		fileStream.Position = position;
		byte[] array = new byte[length];
		fileStream.ReadExactly(array);
		return array;
	}

	public void EnsureBackup()
	{
		if (!File.Exists(BackupUtocPath))
		{
			File.Copy(LiveUtocPath, BackupUtocPath, overwrite: false);
		}
	}

	public static bool BackupIsCurrent(string paksDirectory, string name, IEnumerable<PatchRecord> records)
	{
		string text = Path.Combine(paksDirectory, name + ".utoc");
		string text2 = text + ".elenr-bak";
		if (!File.Exists(text) || !File.Exists(text2))
		{
			return false;
		}
		if (new FileInfo(text).Length != new FileInfo(text2).Length)
		{
			return false;
		}
		byte[] array = File.ReadAllBytes(text);
		byte[] array2 = File.ReadAllBytes(text2);
		foreach (PatchRecord item in records.Where((PatchRecord r) => r.Container == name))
		{
			byte[] array3 = Convert.FromHexString(item.OriginalBlock);
			byte[] array4 = Convert.FromHexString(item.OriginalOffsetLength);
			if (item.BlockPos < 0 || item.BlockPos + array3.Length > array.Length)
			{
				return false;
			}
			if (item.OffsetLengthPos < 0 || item.OffsetLengthPos + array4.Length > array.Length)
			{
				return false;
			}
			array3.CopyTo(array, (int)item.BlockPos);
			array4.CopyTo(array, (int)item.OffsetLengthPos);
		}
		return ((ReadOnlySpan<byte>)array.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array2);
	}

	public static bool RecordIsApplied(string paksDirectory, PatchRecord rec)
	{
		string path = Path.Combine(paksDirectory, rec.Container + ".utoc");
		if (!File.Exists(path))
		{
			return false;
		}
		byte[] array = Convert.FromHexString(rec.PatchedBlock);
		byte[] array2 = Convert.FromHexString(rec.PatchedOffsetLength);
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			if (rec.BlockPos < 0 || rec.BlockPos + array.Length > fileStream.Length || rec.OffsetLengthPos < 0 || rec.OffsetLengthPos + array2.Length > fileStream.Length)
			{
				return false;
			}
			byte[] array3 = new byte[array.Length];
			byte[] array4 = new byte[array2.Length];
			fileStream.Position = rec.BlockPos;
			fileStream.ReadExactly(array3);
			fileStream.Position = rec.OffsetLengthPos;
			fileStream.ReadExactly(array4);
			return ((ReadOnlySpan<byte>)array3.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array) && ((ReadOnlySpan<byte>)array4.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array2);
		}
		catch
		{
			return false;
		}
	}

	public long Capacity(int chunkIndex)
	{
		return (long)((Toc.GetOffsetLength(chunkIndex).Length + Toc.BlockSize - 1) / Toc.BlockSize * Toc.BlockSize);
	}

	public PatchRecord PatchChunk(int chunkIndex, byte[] newData, bool exactLength = false)
	{
		if (newData.Length == 0)
		{
			throw new ArgumentException("Empty chunk.", "newData");
		}
		uint blockSize = Toc.BlockSize;
		ulong item = Toc.GetOffsetLength(chunkIndex).Offset;
		if (item % blockSize != 0L)
		{
			throw new InvalidOperationException($"{Name}: chunk offset {item} is not block aligned; refusing to patch.");
		}
		int num = (int)(item / blockSize);
		byte[] array = new byte[Align16((uint)newData.Length)];
		newData.CopyTo(array, 0);
		long num2 = Capacity(chunkIndex);
		if (array.Length > num2)
		{
			throw new InvalidOperationException($"{Name}: new chunk is {array.Length} bytes but this slot only has room for {num2} ({num2 / blockSize} block(s) of {blockSize}).");
		}
		int num3 = (int)((array.Length + blockSize - 1) / blockSize);
		byte[] array2 = array;
		if (Toc.Encrypted)
		{
			if (Key == null)
			{
				throw new InvalidOperationException(Name + ": encrypted container without a key.");
			}
			array2 = Encrypt(array, Key);
		}
		int num4 = (int)(Math.Max(Toc.PartitionCount, 1u) - 1);
		string text = UcasPath(num4);
		long ticks = File.GetLastWriteTimeUtc(LiveUtocPath).Ticks;
		long ticks2 = File.GetLastWriteTimeUtc(text).Ticks;
		long length = new FileInfo(text).Length;
		long num5;
		using (FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
		{
			long length2 = fileStream.Length;
			num5 = (length2 + 15) & -16;
			ulong partitionSize = Toc.PartitionSize;
			if (partitionSize != ulong.MaxValue && (ulong)(num5 + array2.Length) > partitionSize)
			{
				throw new InvalidOperationException(Name + ": " + Path.GetFileName(text) + " has no room left in its partition.");
			}
			fileStream.Position = length2;
			if (num5 > length2)
			{
				fileStream.Write(new byte[num5 - length2]);
			}
			fileStream.Write(array2);
			fileStream.Flush(flushToDisk: true);
		}
		ulong num6 = (ulong)(((Toc.PartitionSize == ulong.MaxValue) ? 0 : ((long)num4 * (long)Toc.PartitionSize)) + num5);
		List<byte> list = new List<byte>(num3 * 12);
		List<byte> list2 = new List<byte>(num3 * 12);
		for (int i = 0; i < num3; i++)
		{
			uint num7 = (uint)Math.Min(blockSize, (exactLength ? newData.Length : array.Length) - i * blockSize);
			list.AddRange(TocFile.EncodeBlock(num6 + (ulong)(i * blockSize), num7, num7, 0));
			list2.AddRange(Toc.RawBlockBytes(num + i));
		}
		byte[] array3 = list.ToArray();
		byte[] array4 = TocFile.EncodeOffsetLength(item, (ulong)(exactLength ? newData.Length : array.Length));
		PatchRecord patchRecord = new PatchRecord
		{
			Container = Name,
			ChunkIndex = chunkIndex,
			BlockIndex = num,
			BlockCount = num3,
			BlockPos = Toc.BlockPosition(num),
			OffsetLengthPos = Toc.OffsetLengthPosition(chunkIndex),
			OriginalBlock = Convert.ToHexString(list2.ToArray()),
			OriginalOffsetLength = Convert.ToHexString(Toc.RawOffsetLengthBytes(chunkIndex)),
			PatchedBlock = Convert.ToHexString(array3),
			PatchedOffsetLength = Convert.ToHexString(array4),
			UcasFile = Path.GetFileName(text),
			UcasLengthBefore = length,
			UcasLengthAfter = num5 + array2.Length,
			UtocWriteUtcTicks = ticks,
			UcasWriteUtcTicks = ticks2
		};
		using FileStream fileStream2 = new FileStream(LiveUtocPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		fileStream2.Position = patchRecord.BlockPos;
		fileStream2.Write(array3);
		fileStream2.Position = patchRecord.OffsetLengthPos;
		fileStream2.Write(array4);
		fileStream2.Flush(flushToDisk: true);
		return patchRecord;
	}

	public static bool IsPatched(string paksDirectory, PatchRecord rec)
	{
		try
		{
			string path = Path.Combine(paksDirectory, rec.Container + ".utoc");
			if (!File.Exists(path))
			{
				return false;
			}
			byte[] array = Convert.FromHexString(rec.PatchedBlock);
			byte[] array2 = Convert.FromHexString(rec.PatchedOffsetLength);
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			byte[] array3 = new byte[array.Length];
			byte[] array4 = new byte[array2.Length];
			fileStream.Position = rec.BlockPos;
			fileStream.ReadExactly(array3);
			fileStream.Position = rec.OffsetLengthPos;
			fileStream.ReadExactly(array4);
			return ((ReadOnlySpan<byte>)array3.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array) && ((ReadOnlySpan<byte>)array4.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array2);
		}
		catch
		{
			return false;
		}
	}

	public static bool Revert(string paksDirectory, PatchRecord rec)
	{
		string path = Path.Combine(paksDirectory, rec.Container + ".utoc");
		if (!File.Exists(path))
		{
			return false;
		}
		byte[] array = Convert.FromHexString(rec.PatchedBlock);
		byte[] array2 = Convert.FromHexString(rec.PatchedOffsetLength);
		using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
		{
			byte[] array3 = new byte[array.Length];
			byte[] array4 = new byte[array2.Length];
			fileStream.Position = rec.BlockPos;
			fileStream.ReadExactly(array3);
			fileStream.Position = rec.OffsetLengthPos;
			fileStream.ReadExactly(array4);
			if (!((ReadOnlySpan<byte>)array3.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array) || !((ReadOnlySpan<byte>)array4.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array2))
			{
				return false;
			}
			fileStream.Position = rec.BlockPos;
			fileStream.Write(Convert.FromHexString(rec.OriginalBlock));
			fileStream.Position = rec.OffsetLengthPos;
			fileStream.Write(Convert.FromHexString(rec.OriginalOffsetLength));
			fileStream.Flush(flushToDisk: true);
		}
		if (rec.UcasFile.Length > 0)
		{
			string text = Path.Combine(paksDirectory, rec.UcasFile);
			try
			{
				if (File.Exists(text) && new FileInfo(text).Length == rec.UcasLengthAfter && rec.UcasLengthBefore > 0)
				{
					using FileStream fileStream2 = new FileStream(text, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
					fileStream2.SetLength(rec.UcasLengthBefore);
				}
			}
			catch
			{
			}
			if (rec.UcasWriteUtcTicks > 0)
			{
				TrySetTime(text, rec.UcasWriteUtcTicks);
			}
		}
		if (rec.UtocWriteUtcTicks > 0)
		{
			TrySetTime(path, rec.UtocWriteUtcTicks);
		}
		return true;
	}

	private static void TrySetTime(string path, long utcTicks)
	{
		try
		{
			if (utcTicks > 0)
			{
				File.SetLastWriteTimeUtc(path, new DateTime(utcTicks, DateTimeKind.Utc));
			}
		}
		catch
		{
		}
	}

	public static uint Align16(uint v)
	{
		return (v + 15) & 0xFFFFFFF0u;
	}

	public static byte[] Decrypt(byte[] data, byte[] key)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		return aes.DecryptEcb(data, PaddingMode.None);
	}

	public static byte[] Encrypt(byte[] data, byte[] key)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		return aes.EncryptEcb(data, PaddingMode.None);
	}
}
