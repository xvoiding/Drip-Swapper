using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Drip.Core.Stellar;

/// <summary>Where one file sits inside a v10 pak.</summary>
public sealed record PakEntryInfo(string Path, long HeaderOffset, long Size, long StoredSize, bool Encrypted, uint Compression, int BlockCount, uint EncodedOffset);

/// <summary>
/// Reads Fortnite 12.41 style pak v10 files (the ones Project Stellar uses): AES-ECB encrypted
/// index, bit-packed entries. Only stored (uncompressed) entries can be read back, which is
/// what cosmetic definitions are.
/// </summary>
public sealed class PakV10Reader : IDisposable
{
	public const int FooterSize = 221;
	public const uint Magic = 0x5A6F12E1;

	/// <summary>The AES key for Fortnite 12.41 (CL 12905909).</summary>
	public static readonly byte[] Key1241 = Convert.FromHexString("2713E24A338C7E8BF1A50E3F1987F33BB151F04B192E89E940A623AB34F8502F");

	private readonly FileStream _fs;
	private readonly byte[] _key;

	private readonly Func<string, bool>? _filter;
	private readonly Dictionary<string, PakEntryInfo> _entries = new Dictionary<string, PakEntryInfo>(StringComparer.OrdinalIgnoreCase);

	public string FilePath { get; }
	public string MountPoint { get; private set; } = "";
	public uint Version { get; private set; }
	public bool IndexEncrypted { get; private set; }
	public byte[] FooterGuid { get; private set; } = new byte[16];
	public string[] CompressionNames { get; private set; } = new string[5];
	public IReadOnlyDictionary<string, PakEntryInfo> Entries => _entries;

	public PakV10Reader(string path, byte[]? key = null, Func<string, bool>? filter = null)
	{
		FilePath = path;
		_filter = filter;
		_key = key ?? Key1241;
		_fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
		try
		{
			Load();
		}
		catch
		{
			_fs.Dispose();
			throw;
		}
	}

	public void Dispose()
	{
		_fs.Dispose();
	}

	public static byte[] AesEcb(byte[] data, byte[] key, bool encrypt)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		aes.Mode = CipherMode.ECB;
		aes.Padding = PaddingMode.None;
		using ICryptoTransform t = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
		return t.TransformFinalBlock(data, 0, data.Length);
	}

	private byte[] ReadAt(long offset, long size)
	{
		if (offset < 0 || size < 0 || offset + size > _fs.Length)
		{
			throw new InvalidDataException("Pak index points outside the file.");
		}
		byte[] buf = new byte[size];
		_fs.Seek(offset, SeekOrigin.Begin);
		_fs.ReadExactly(buf);
		return buf;
	}

	private void Load()
	{
		if (_fs.Length < FooterSize)
		{
			throw new InvalidDataException("File is too small to be a pak.");
		}
		byte[] f = ReadAt(_fs.Length - FooterSize, FooterSize);
		FooterGuid = f[..16];
		IndexEncrypted = f[16] != 0;
		if (BinaryPrimitives.ReadUInt32LittleEndian(f.AsSpan(17)) != Magic)
		{
			throw new InvalidDataException("Not a pak (bad magic).");
		}
		Version = BinaryPrimitives.ReadUInt32LittleEndian(f.AsSpan(21));
		if (Version != 10)
		{
			throw new NotSupportedException("Pak version " + Version + " isn't the v10 layout this swapper knows.");
		}
		long indexOffset = BinaryPrimitives.ReadInt64LittleEndian(f.AsSpan(25));
		long indexSize = BinaryPrimitives.ReadInt64LittleEndian(f.AsSpan(33));
		CompressionNames = new string[5];
		for (int i = 0; i < 5; i++)
		{
			CompressionNames[i] = Encoding.ASCII.GetString(f, 61 + 32 * i, 32).TrimEnd('\0');
		}
		byte[] idx = ReadAt(indexOffset, indexSize);
		if (IndexEncrypted)
		{
			idx = AesEcb(idx, _key, encrypt: false);
		}
		Cursor c = new Cursor(idx);
		MountPoint = c.FString();
		if (MountPoint.Length > 0 && !MountPoint.StartsWith("../", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Index didn't decrypt (wrong AES key for this pak).");
		}
		c.Int32();
		c.UInt64();
		bool hasPathHash = c.Int32() != 0;
		if (hasPathHash)
		{
			c.Int64();
			c.Int64();
			c.Skip(20);
		}
		bool hasFullDir = c.Int32() != 0;
		long fdOffset = 0;
		long fdSize = 0;
		if (hasFullDir)
		{
			fdOffset = c.Int64();
			fdSize = c.Int64();
			c.Skip(20);
		}
		int blobSize = c.Int32();
		byte[] blob = c.Bytes(blobSize);
		if (!hasFullDir)
		{
			throw new NotSupportedException("This pak has no full directory index.");
		}
		byte[] fd = AesEcb(ReadAt(fdOffset, fdSize), _key, encrypt: false);
		Cursor d = new Cursor(fd);
		int dirCount = d.Int32();
		string mountClean = MountPoint.StartsWith("../../../", StringComparison.Ordinal) ? MountPoint.Substring(9) : MountPoint;
		for (int i = 0; i < dirCount; i++)
		{
			string dir = d.FString();
			int files = d.Int32();
			for (int j = 0; j < files; j++)
			{
				string name = d.FString();
				int encodedOffset = d.Int32();
				string relative = dir == "/" ? name : dir + name;
				string full = mountClean + relative;
				if (_filter != null && !_filter(full))
				{
					continue;
				}
				_entries[full] = DecodeEntry(blob, encodedOffset, full);
			}
		}
	}

	private static PakEntryInfo DecodeEntry(byte[] blob, int offset, string path)
	{
		Cursor c = new Cursor(blob) { Position = offset };
		uint v = (uint)c.Int32();
		uint comp = (v >> 23) & 0x3f;
		bool enc = ((v >> 22) & 1) != 0;
		int blocks = (int)((v >> 6) & 0xffff);
		if ((v & 0x3f) == 0x3f)
		{
			c.Int32();
		}
		long off = (v & 0x80000000u) != 0 ? (uint)c.Int32() : c.Int64();
		long usize = (v & 0x40000000u) != 0 ? (uint)c.Int32() : c.Int64();
		long csize = usize;
		if (comp != 0)
		{
			csize = (v & 0x20000000u) != 0 ? (uint)c.Int32() : c.Int64();
		}
		return new PakEntryInfo(path, off, usize, csize, enc, comp, blocks, (uint)offset);
	}

	/// <summary>Reads a stored file. Throws for compressed entries.</summary>
	public byte[] Read(string path)
	{
		if (!_entries.TryGetValue(path, out PakEntryInfo? e))
		{
			throw new FileNotFoundException(path + " isn't in " + Path.GetFileName(FilePath));
		}
		if (e.Compression != 0)
		{
			throw new NotSupportedException(path + " is compressed inside the pak; this swapper only reads stored definitions.");
		}
		byte[] hdr = ReadAt(e.HeaderOffset, 53);
		long dataStart = e.HeaderOffset + 53;
		long len = e.Size;
		if (e.Encrypted)
		{
			long padded = (len + 15) / 16 * 16;
			return AesEcb(ReadAt(dataStart, padded), _key, encrypt: false)[..(int)len];
		}
		return ReadAt(dataStart, len);
	}

	private sealed class Cursor
	{
		private readonly byte[] _b;
		public int Position;
		public Cursor(byte[] b) { _b = b; }
		public int Int32() { int v = BinaryPrimitives.ReadInt32LittleEndian(_b.AsSpan(Position)); Position += 4; return v; }
		public long Int64() { long v = BinaryPrimitives.ReadInt64LittleEndian(_b.AsSpan(Position)); Position += 8; return v; }
		public ulong UInt64() { ulong v = BinaryPrimitives.ReadUInt64LittleEndian(_b.AsSpan(Position)); Position += 8; return v; }
		public void Skip(int n) { Position += n; }
		public byte[] Bytes(int n) { byte[] r = _b.AsSpan(Position, n).ToArray(); Position += n; return r; }
		public string FString()
		{
			int n = Int32();
			if (n == 0) return "";
			if (n < 0)
			{
				int chars = -n;
				string s = Encoding.Unicode.GetString(_b, Position, (chars - 1) * 2);
				Position += chars * 2;
				return s;
			}
			string a = Encoding.Latin1.GetString(_b, Position, n - 1);
			Position += n;
			return a;
		}
	}
}

/// <summary>
/// Writes a small pak in the plain "v8" layout (stored entries, AES-encrypted flat index). Used for the Stellar
/// patch pak, so the game's own pak files never need to be edited. The flat index needs no path hashes.
/// </summary>
public static class PakV10Writer
{
	public const uint WrittenVersion = 8;

	public static void Write(string path, string mountPoint, IReadOnlyList<(string RelativePath, byte[] Data)> files, byte[] aesKey, IReadOnlyList<string> compressionNames)
	{
		using MemoryStream body = new MemoryStream();
		using MemoryStream idx = new MemoryStream();
		WriteFString(idx, mountPoint);
		WriteI32(idx, files.Count);
		foreach (var (rel, data) in files)
		{
			long headerOffset = body.Position;
			byte[] sha = SHA1.HashData(data);
			// In-file header (offset is 0 there), then the data.
			WriteI64(body, 0);
			WriteI64(body, data.Length);
			WriteI64(body, data.Length);
			WriteI32(body, 0);
			body.Write(sha);
			body.WriteByte(0);
			WriteI32(body, 0);
			body.Write(data);
			// Index record (offset is real there).
			WriteFString(idx, rel);
			WriteI64(idx, headerOffset);
			WriteI64(idx, data.Length);
			WriteI64(idx, data.Length);
			WriteI32(idx, 0);
			idx.Write(sha);
			idx.WriteByte(0);
			WriteI32(idx, 0);
		}
		byte[] plain = PadTo16(idx.ToArray());
		byte[] indexHash = SHA1.HashData(plain);
		byte[] enc = PakV10Reader.AesEcb(plain, aesKey, encrypt: true);
		long indexOffset = body.Length;

		using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
		body.Position = 0;
		body.CopyTo(fs);
		fs.Write(enc);
		byte[] footer = new byte[PakV10Reader.FooterSize];
		footer[16] = 1;
		BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(17), PakV10Reader.Magic);
		BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(21), WrittenVersion);
		BinaryPrimitives.WriteInt64LittleEndian(footer.AsSpan(25), indexOffset);
		BinaryPrimitives.WriteInt64LittleEndian(footer.AsSpan(33), enc.Length);
		indexHash.CopyTo(footer, 41);
		for (int i = 0; i < 5 && i < compressionNames.Count; i++)
		{
			byte[] n = Encoding.ASCII.GetBytes(compressionNames[i]);
			Array.Copy(n, 0, footer, 61 + 32 * i, Math.Min(32, n.Length));
		}
		fs.Write(footer);
	}

	private static byte[] PadTo16(byte[] data)
	{
		int padded = (data.Length + 15) / 16 * 16;
		if (padded == data.Length) return data;
		byte[] r = new byte[padded];
		Array.Copy(data, r, data.Length);
		for (int i = data.Length; i < padded; i++) r[i] = data[(i - data.Length) % data.Length];
		return r;
	}

	private static void WriteI32(Stream s, int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); s.Write(b); }
	private static void WriteI64(Stream s, long v) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); s.Write(b); }
	private static void WriteFString(Stream s, string v)
	{
		byte[] a = Encoding.Latin1.GetBytes(v);
		WriteI32(s, a.Length + 1);
		s.Write(a);
		s.WriteByte(0);
	}
}
