using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Drip.Core.Retrac;

public sealed class Ue426ContainerHeader
{
	private const int EntrySize = 32;

	private readonly byte[] _raw;

	private readonly long _idsPos;

	private readonly long _blobCountPos;

	private readonly long _blobPos;

	private readonly int _blobLen;

	private readonly Dictionary<ulong, int> _index;

	public ulong ContainerId { get; }

	public int PackageCount { get; }

	private Ue426ContainerHeader(byte[] raw)
	{
		_raw = raw;
		if (raw.Length < 28)
		{
			throw new InvalidDataException("Container header is too small.");
		}
		ContainerId = BinaryPrimitives.ReadUInt64LittleEndian(raw);
		PackageCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8));
		long o = 12L;
		o = SkipBytesArray(o);
		o = SkipBytesArray(o);
		int num = ReadI32(o);
		_idsPos = o + 4;
		if (num != PackageCount)
		{
			throw new InvalidDataException("Container header: package id count doesn't match the package count.");
		}
		o = (_blobCountPos = _idsPos + 8L * (long)num);
		_blobLen = ReadI32(o);
		_blobPos = o + 4;
		if (_blobLen < PackageCount * 32 || _blobPos + _blobLen > raw.Length)
		{
			throw new InvalidDataException("Container header: store entries don't fit; this isn't the layout this swapper knows.");
		}
		_index = new Dictionary<ulong, int>(PackageCount);
		for (int i = 0; i < PackageCount; i++)
		{
			_index[BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan((int)(_idsPos + 8L * (long)i)))] = i;
		}
	}

	public static Ue426ContainerHeader Parse(byte[] raw)
	{
		return new Ue426ContainerHeader(raw);
	}

	private long SkipBytesArray(long o)
	{
		int num = ReadI32(o);
		if (num < 0 || o + 4 + num > _raw.Length)
		{
			throw new InvalidDataException("Container header: array runs off the end.");
		}
		return o + 4 + num;
	}

	private int ReadI32(long p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(_raw.AsSpan((int)p));
	}

	public bool Contains(ulong packageId)
	{
		return _index.ContainsKey(packageId);
	}

	public StoreEntry? Get(ulong packageId)
	{
		if (!_index.TryGetValue(packageId, out var value))
		{
			return null;
		}
		Span<byte> span = _raw.AsSpan((int)(_blobPos + (long)value * 32L), 32);
		uint num = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(24));
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(28));
		ulong[] array = new ulong[num];
		if (num != 0)
		{
			long num3 = _blobPos + (long)value * 32L + 24 + num2;
			if (num3 + 8L * (long)num > _blobPos + _blobLen)
			{
				throw new InvalidDataException("Container header: an import list runs off the end.");
			}
			for (int i = 0; i < num; i++)
			{
				array[i] = BinaryPrimitives.ReadUInt64LittleEndian(_raw.AsSpan((int)(num3 + 8L * (long)i)));
			}
		}
		return new StoreEntry(BinaryPrimitives.ReadUInt64LittleEndian(span), BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8)), BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12)), BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16)), BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(20)), array);
	}

	public byte[] WithEntries(IReadOnlyDictionary<ulong, StoreEntry> replacements)
	{
		List<byte> list = new List<byte>(_raw.AsSpan((int)_blobPos, _blobLen).ToArray());
		foreach (var (num2, storeEntry2) in replacements)
		{
			if (!_index.TryGetValue(num2, out var value))
			{
				throw new InvalidOperationException("Package is not in this container's header.");
			}
			int num3 = value * 32;
			StoreEntry storeEntry3 = Get(num2);
			uint num4 = (uint)storeEntry2.ImportedPackages.Length;
			uint value2 = 0u;
			if (num4 != 0)
			{
				if (((ReadOnlySpan<ulong>)storeEntry3.ImportedPackages.AsSpan()).SequenceEqual((ReadOnlySpan<ulong>)storeEntry2.ImportedPackages))
				{
					value2 = BinaryPrimitives.ReadUInt32LittleEndian(_raw.AsSpan((int)(_blobPos + num3 + 28)));
				}
				else
				{
					int count = list.Count;
					ulong[] importedPackages = storeEntry2.ImportedPackages;
					foreach (ulong value3 in importedPackages)
					{
						byte[] array = new byte[8];
						BinaryPrimitives.WriteUInt64LittleEndian(array, value3);
						list.AddRange(array);
					}
					value2 = (uint)(count - (num3 + 24));
				}
			}
			byte[] array2 = new byte[32];
			BinaryPrimitives.WriteUInt64LittleEndian(array2, storeEntry2.ExportBundlesSize);
			BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(8), storeEntry2.ExportCount);
			BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(12), storeEntry2.ExportBundleCount);
			BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(16), storeEntry2.LoadOrder);
			BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(20), storeEntry2.Pad);
			BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(24), num4);
			BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(28), value2);
			for (int j = 0; j < 32; j++)
			{
				list[num3 + j] = array2[j];
			}
		}
		long num5 = _blobPos + _blobLen;
		int num6 = list.Count - _blobLen;
		int num7 = _raw.Length - (int)num5;
		if (num6 > num7)
		{
			throw new InvalidOperationException("The container header has no room left for more swaps.");
		}
		for (int k = 0; k < num6; k++)
		{
			if (_raw[_raw.Length - 1 - k] != 0)
			{
				throw new InvalidOperationException("The container header has no free space at the end for more swaps.");
			}
		}
		byte[] array3 = new byte[_raw.Length];
		Array.Copy(_raw, array3, _blobCountPos);
		BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan((int)_blobCountPos), list.Count);
		list.CopyTo(array3, (int)_blobPos);
		Array.Copy(_raw, num5, array3, _blobPos + list.Count, num7 - num6);
		return array3;
	}
}
