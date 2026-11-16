using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Drip.Core.IoStore;

/// <summary>
/// Result of re-sealing one patched chunk. Every field is a position plus the bytes that were
/// there before and after, so Revert can put the container back exactly as it was.
/// </summary>
public sealed class TocSeal
{
	public long HashRegionPos { get; init; } = -1;

	public string OriginalHashes { get; init; } = "";

	public string PatchedHashes { get; init; } = "";

	public long SignaturePos { get; init; } = -1;

	public string OriginalSignature { get; init; } = "";

	public string PatchedSignature { get; init; } = "";

	public long MetaPos { get; init; } = -1;

	public string OriginalMeta { get; init; } = "";

	public string PatchedMeta { get; init; } = "";

	public bool Ok { get; init; }

	public string Error { get; init; } = "";
}

/// <summary>
/// Re-seals a signed .utoc after one of its chunks has been rewritten.
///
/// A signed container stores, immediately after the compression method names:
///
///     uint32                 hash size (20)
///     byte[hash size]        SHA-1 over the toc header
///     byte[hash size]        SHA-1 over the whole block hash array
///     byte[block count * 20] one SHA-1 per compressed block, over the bytes as stored in the .ucas
///
/// and, immediately after the directory index, one entry per chunk:
///
///     byte[32]               SHA-1 of the uncompressed chunk, zero padded
///     byte                   meta flags
///
/// None of that is asymmetric - no private key is involved - so everything can be recomputed
/// locally. Patching a block changes the bytes it hashes, which is what makes Fortnite log
/// "Signature error detected in container '...'" and drop the session. Seal() puts the hashes
/// back in step with the new content.
///
/// The exact byte span each hash covers is not documented, so the first seal on a container
/// probes untouched blocks and the header: whatever reproduces the hashes the game shipped is
/// the convention that gets used. If nothing reproduces, the default is used and the seal is
/// reported as unproven rather than skipped, because a stale signature fails anyway.
/// </summary>
public sealed class TocSignature
{
	public const int Sha1Size = 20;

	public const int MetaHashSize = 32;

	public const int MetaEntrySize = MetaHashSize + 1;

	public const int MinHeaderBytes = 144;

	public const int MaxHeaderBytes = 4096;

	private const int ProbeSamples = 16;

	private enum BlockSpan
	{
		Exact,
		Aligned
	}

	private enum HeaderSpan
	{
		Full,
		FirstHeaderOnly,
		DirectorySizeZeroed
	}

	private static readonly ConcurrentDictionary<string, TocSignature?> Cache = new();

	public int HashSize { get; }

	public long TocSignaturePos { get; }

	public long BlockSignaturePos { get; }

	public long BlockHashesPos { get; }

	public long MetaPos { get; }

	public bool Proven { get; private set; }

	public string ProvenHow { get; private set; } = "";

	private BlockSpan _blockSpan = BlockSpan.Aligned;

	private HeaderSpan _headerSpan = HeaderSpan.Full;

	private int _headerBytes = MinHeaderBytes;

	private TocSignature(IoContainer c, int hashSize, long tocSignaturePos, long blockSignaturePos, long blockHashesPos, long metaPos)
	{
		HashSize = hashSize;
		TocSignaturePos = tocSignaturePos;
		BlockSignaturePos = blockSignaturePos;
		BlockHashesPos = blockHashesPos;
		MetaPos = metaPos;
	}

	public static TocSignature? For(IoContainer c)
	{
		if (!c.Toc.Signed)
		{
			return null;
		}
		string path = c.LiveUtocPath;
		long length;
		long ticks;
		try
		{
			FileInfo info = new FileInfo(path);
			length = info.Length;
			ticks = info.LastWriteTimeUtc.Ticks;
		}
		catch
		{
			return null;
		}
		string key = $"{path}|{length}|{ticks}";
		return Cache.GetOrAdd(key, delegate
		{
			try
			{
				return Open(c);
			}
			catch
			{
				return null;
			}
		});
	}

	private static TocSignature? Open(IoContainer c)
	{
		string path = c.LiveUtocPath;
		long length = new FileInfo(path).Length;
		long pos = c.Toc.SignatureSectionPos;
		if (pos <= 0 || pos + 4 > length)
		{
			return null;
		}
		byte[] sizeBytes = ReadAt(path, pos, 4);
		int hashSize = BinaryPrimitives.ReadInt32LittleEndian(sizeBytes);
		if (hashSize != Sha1Size)
		{
			return null;
		}
		long tocSignaturePos = pos + 4;
		long blockSignaturePos = tocSignaturePos + hashSize;
		long blockHashesPos = blockSignaturePos + hashSize;
		long afterHashes = blockHashesPos + (long)c.Toc.BlockCount * Sha1Size;
		long metaPos = afterHashes + c.Toc.DirectoryIndexSize;
		if (metaPos + (long)c.Toc.EntryCount * MetaEntrySize > length)
		{
			return null;
		}
		if (c.Toc.EntryCount == 0 || c.Toc.BlockCount == 0)
		{
			return null;
		}
		TocSignature sig = new TocSignature(c, hashSize, tocSignaturePos, blockSignaturePos, blockHashesPos, metaPos)
		{
			_headerBytes = Math.Clamp((int)c.Toc.HeaderSize, MinHeaderBytes, MaxHeaderBytes)
		};
		if (sig._headerBytes > length)
		{
			sig._headerBytes = MinHeaderBytes;
		}
		sig.Probe(c);
		return sig;
	}

	/// <summary>
	/// Works out which byte span each hash covers by reproducing hashes the game shipped.
	/// </summary>
	private void Probe(IoContainer c)
	{
		string path = c.LiveUtocPath;
		byte[] header = ReadAt(path, 0L, _headerBytes);
		byte[] wantTocSig = ReadAt(path, TocSignaturePos, HashSize);
		if (Sha1(header).AsSpan().SequenceEqual(wantTocSig))
		{
			_headerSpan = HeaderSpan.Full;
			Proven = true;
			ProvenHow = "header hash reproduced over the toc header";
		}
		else if (_headerBytes != MinHeaderBytes && Sha1(header.AsSpan(0, MinHeaderBytes).ToArray()).AsSpan().SequenceEqual(wantTocSig))
		{
			_headerSpan = HeaderSpan.FirstHeaderOnly;
			_headerBytes = MinHeaderBytes;
			Proven = true;
			ProvenHow = "header hash reproduced over the first 144 header bytes";
		}
		else
		{
			byte[] zeroed = (byte[])header.Clone();
			if (zeroed.Length >= 52)
			{
				Array.Clear(zeroed, 48, 4);
				if (Sha1(zeroed).AsSpan().SequenceEqual(wantTocSig))
				{
					_headerSpan = HeaderSpan.DirectorySizeZeroed;
					Proven = true;
					ProvenHow = "header hash reproduced with the directory index size zeroed";
				}
			}
		}
		byte[] allHashes = ReadAt(path, BlockHashesPos, (int)((long)c.Toc.BlockCount * Sha1Size));
		byte[] wantBlockSig = ReadAt(path, BlockSignaturePos, HashSize);
		bool arrayOk = Sha1(allHashes).AsSpan().SequenceEqual(wantBlockSig);
		if (arrayOk && Proven)
		{
			ProvenHow += "; block hash array reproduced";
		}
		int exactHits = 0;
		int alignedHits = 0;
		int tested = 0;
		int step = Math.Max(c.Toc.BlockCount / ProbeSamples, 1);
		for (int i = 0; i < c.Toc.BlockCount && tested < ProbeSamples; i += step)
		{
			tested++;
			byte[] stored;
			try
			{
				stored = c.ReadStored(i);
			}
			catch
			{
				continue;
			}
			TocBlock block = c.Toc.GetBlock(i);
			byte[] want = allHashes.AsSpan(i * Sha1Size, Sha1Size).ToArray();
			int exact = Math.Min((int)block.CompressedSize, stored.Length);
			if (Sha1(stored.AsSpan(0, exact).ToArray()).AsSpan().SequenceEqual(want))
			{
				exactHits++;
			}
			if (Sha1(stored).AsSpan().SequenceEqual(want))
			{
				alignedHits++;
			}
		}
		if (alignedHits > exactHits)
		{
			_blockSpan = BlockSpan.Aligned;
		}
		else if (exactHits > alignedHits)
		{
			_blockSpan = BlockSpan.Exact;
		}
		if (alignedHits + exactHits > 0)
		{
			Proven = true;
			ProvenHow = (ProvenHow.Length > 0) ? (ProvenHow + "; block hashes reproduced") : "block hashes reproduced";
		}
		else if (ProvenHow.Length == 0)
		{
			ProvenHow = "nothing reproduced, defaults used";
		}
	}

	/// <summary>
	/// Rewrites every hash that the freshly written blocks invalidated.
	/// </summary>
	public static TocSeal? Seal(IoContainer c, int firstBlock, int[] blockLengths, int chunkIndex, byte[] stored, byte[] uncompressed)
	{
		if (!c.Toc.Signed || blockLengths.Length == 0)
		{
			return null;
		}
		TocSignature? sig = For(c);
		if (sig == null)
		{
			return new TocSeal
			{
				Ok = false,
				Error = "the signature section of " + c.Name + " could not be located, so it was left stale"
			};
		}
		try
		{
			return sig.SealCore(c, firstBlock, blockLengths, chunkIndex, stored, uncompressed);
		}
		catch (Exception ex)
		{
			return new TocSeal
			{
				Ok = false,
				Error = "re-sealing " + c.Name + " failed (" + ex.Message + ")"
			};
		}
	}

	private TocSeal SealCore(IoContainer c, int firstBlock, int[] blockLengths, int chunkIndex, byte[] stored, byte[] uncompressed)
	{
		string path = c.LiveUtocPath;
		int blockSize = (int)c.Toc.BlockSize;
		byte[] newHashes = new byte[blockLengths.Length * Sha1Size];
		for (int i = 0; i < blockLengths.Length; i++)
		{
			int at = i * blockSize;
			int len = ((_blockSpan == BlockSpan.Exact) ? Math.Min(blockLengths[i], stored.Length - at) : Math.Min((int)IoContainer.Align16((uint)blockLengths[i]), stored.Length - at));
			if (len <= 0)
			{
				throw new InvalidDataException("block " + (firstBlock + i) + " has no bytes to hash");
			}
			Sha1(stored.AsSpan(at, len)).CopyTo(newHashes.AsSpan(i * Sha1Size));
		}
		long hashPos = BlockHashesPos + (long)firstBlock * Sha1Size;
		long metaPos = MetaPos + (long)chunkIndex * MetaEntrySize;
		using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		byte[] originalHashes = ReadFrom(fs, hashPos, newHashes.Length);
		byte[] originalSignature = ReadFrom(fs, TocSignaturePos, HashSize * 2);
		byte[] originalMeta = ReadFrom(fs, metaPos, MetaEntrySize);
		WriteTo(fs, hashPos, newHashes);
		byte[] allHashes = ReadFrom(fs, BlockHashesPos, (int)((long)c.Toc.BlockCount * Sha1Size));
		byte[] blockSignature = Sha1(allHashes);
		WriteTo(fs, BlockSignaturePos, blockSignature);
		byte[] header = ReadFrom(fs, 0L, _headerBytes);
		if (_headerSpan == HeaderSpan.DirectorySizeZeroed && header.Length >= 52)
		{
			Array.Clear(header, 48, 4);
		}
		else if (_headerSpan == HeaderSpan.FirstHeaderOnly && header.Length > MinHeaderBytes)
		{
			header = header.AsSpan(0, MinHeaderBytes).ToArray();
		}
		byte[] tocSignature = Sha1(header);
		WriteTo(fs, TocSignaturePos, tocSignature);
		byte[] meta = new byte[MetaEntrySize];
		Sha1(uncompressed).CopyTo(meta.AsSpan(0, Sha1Size));
		meta[MetaEntrySize - 1] = originalMeta[MetaEntrySize - 1];
		WriteTo(fs, metaPos, meta);
		fs.Flush(flushToDisk: true);
		byte[] newSignature = new byte[HashSize * 2];
		tocSignature.CopyTo(newSignature, 0);
		blockSignature.CopyTo(newSignature, HashSize);
		return new TocSeal
		{
			HashRegionPos = hashPos,
			OriginalHashes = Convert.ToHexString(originalHashes),
			PatchedHashes = Convert.ToHexString(newHashes),
			SignaturePos = TocSignaturePos,
			OriginalSignature = Convert.ToHexString(originalSignature),
			PatchedSignature = Convert.ToHexString(newSignature),
			MetaPos = metaPos,
			OriginalMeta = Convert.ToHexString(originalMeta),
			PatchedMeta = Convert.ToHexString(meta),
			Ok = true
		};
	}

	/// <summary>
	/// True when the live hashes match what Seal wrote, i.e. nothing re-broke the seal.
	/// </summary>
	public static bool IsSealed(string utocPath, PatchRecord rec)
	{
		if (rec.HashRegionPos < 0 || rec.PatchedHashes.Length == 0)
		{
			return true;
		}
		try
		{
			byte[] want = Convert.FromHexString(rec.PatchedHashes);
			return ReadAt(utocPath, rec.HashRegionPos, want.Length).AsSpan().SequenceEqual(want);
		}
		catch
		{
			return false;
		}
	}

	public string Describe()
	{
		return Proven ? ("signature convention: " + ProvenHow) : "signature convention unproven";
	}

	private static byte[] Sha1(ReadOnlySpan<byte> data)
	{
		return SHA1.HashData(data);
	}

	private static byte[] ReadAt(string path, long pos, int count)
	{
		using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		return ReadFrom(fs, pos, count);
	}

	private static byte[] ReadFrom(FileStream fs, long pos, int count)
	{
		if (pos < 0 || pos + count > fs.Length)
		{
			throw new InvalidDataException($"{Path.GetFileName(fs.Name)}: region {pos}+{count} is outside the file ({fs.Length} bytes).");
		}
		byte[] buffer = new byte[count];
		fs.Position = pos;
		fs.ReadExactly(buffer);
		return buffer;
	}

	private static void WriteTo(FileStream fs, long pos, byte[] data)
	{
		fs.Position = pos;
		fs.Write(data);
	}
}
