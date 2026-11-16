using System;
using System.Buffers.Binary;

namespace Drip.Core.RocketLeague;

public sealed class UpkSummary
{
	public const uint Tag = 2653586369u;

	public int NameCount;

	public int NameOffset;

	public int ExportCount;

	public int ExportOffset;

	public int ImportCount;

	public int ImportOffset;

	public int DependsOffset;

	public int ImportExportGuidsOffset;

	public int ImportGuidsCount;

	public int ExportGuidsCount;

	public int ThumbnailTableOffset;

	public const int HeaderSizePos = 8;

	public ushort Version { get; private set; }

	public int TotalHeaderSize { get; private set; }

	public uint CompressionFlags { get; private set; }

	public int GarbageSize { get; private set; }

	public int ChunksOffset { get; private set; }

	public int LastBlockSize { get; private set; }

	public int FieldsPos { get; private set; }

	public int TexAllocPos { get; private set; }

	public int MetaPos { get; private set; }

	public static UpkSummary Parse(byte[] d)
	{
		if (d.Length < 64)
		{
			throw new UpkException("File is too small to be a package.");
		}
		UpkSummary s = new UpkSummary();
		int p = 0;
		if (U() != 2653586369u)
		{
			throw new UpkException("Not a package file (bad tag).");
		}
		s.Version = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(p, 2));
		p += 4;
		s.TotalHeaderSize = I();
		SkipString();
		U();
		s.FieldsPos = p;
		s.NameCount = I();
		s.NameOffset = I();
		s.ExportCount = I();
		s.ExportOffset = I();
		s.ImportCount = I();
		s.ImportOffset = I();
		s.DependsOffset = I();
		s.ImportExportGuidsOffset = I();
		s.ImportGuidsCount = I();
		s.ExportGuidsCount = I();
		s.ThumbnailTableOffset = I();
		p += 16;
		int num = I();
		p += num * 12;
		p += 8;
		s.CompressionFlags = U();
		int num2 = I();
		p += num2 * 16;
		p += 4;
		int num3 = I();
		for (int i = 0; i < num3; i++)
		{
			SkipString();
		}
		s.TexAllocPos = p;
		int num4 = I();
		for (int j = 0; j < num4; j++)
		{
			p += 20;
			int num5 = I();
			p += num5 * 4;
		}
		int num6 = p;
		int num7 = s.NameOffset - 12;
		int num8 = -1;
		if (Plausible(num6))
		{
			num8 = num6;
		}
		else if (Plausible(num7))
		{
			num8 = num7;
		}
		else
		{
			for (int k = Math.Max(0, num6 - 16); k <= num7; k += 4)
			{
				if (Plausible(k))
				{
					num8 = k;
					break;
				}
			}
		}
		if (num8 < 0 && Sane(num6))
		{
			num8 = num6;
		}
		else if (num8 < 0 && Sane(num7))
		{
			num8 = num7;
		}
		if (num8 < 0)
		{
			throw new UpkException("Unexpected package layout (header ends at " + num6 + ", names start at " + s.NameOffset + ").");
		}
		p = num8;
		s.MetaPos = p;
		s.GarbageSize = I();
		s.ChunksOffset = I();
		s.LastBlockSize = I();
		return s;
		int I()
		{
			int result = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
			p += 4;
			return result;
		}
		bool Plausible(int m)
		{
			if (m < 0 || m + 12 > d.Length || m + 12 > s.NameOffset)
			{
				return false;
			}
			int num9 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(m, 4));
			int num10 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(m + 4, 4));
			if (num9 >= 0 && num9 <= d.Length && num10 > 0)
			{
				return (long)s.NameOffset + (long)num10 == s.DependsOffset;
			}
			return false;
		}
		bool Sane(int m)
		{
			if (m >= 0 && m + 12 <= Math.Min(d.Length, s.NameOffset))
			{
				int num9 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(m, 4));
				if (num9 >= 0 && num9 <= d.Length)
				{
					int num10 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(m + 4, 4));
					if (num10 > 0)
					{
						return num10 <= d.Length;
					}
				}
			}
			return false;
		}
		void SkipString()
		{
			int num9 = I();
			p += ((num9 >= 0) ? num9 : (-num9 * 2));
		}
		uint U()
		{
			uint result = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(p, 4));
			p += 4;
			return result;
		}
	}
}
