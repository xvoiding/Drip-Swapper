using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class FnTexturePackage
{
	public sealed class Info
	{
		public int OffsetCount;

		public int HeaderSize;

		public int BulkMapPos;

		public int BulkEntries;

		public int PlatformStart;

		public int SkipPos;

		public int Width;

		public int Height;

		public int FirstMip;

		public int MipCount;

		public string PixelFormat = "";

		public int ExportSize;

		public int[] Offsets = Array.Empty<int>();
	}

	private static uint U32(byte[] b, int p)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
	}

	private static ulong U64(byte[] b, int p)
	{
		return BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(p));
	}

	public static Info Read(byte[] b)
	{
		Exception ex = null;
		int[] array = new int[3] { 9, 8, 7 };
		foreach (int k in array)
		{
			try
			{
				return Read(b, k);
			}
			catch (Exception ex2) when ((ex2 is InvalidDataException || ex2 is ArgumentOutOfRangeException || ex2 is IndexOutOfRangeException) ? true : false)
			{
				ex = ex2;
			}
		}
		throw new InvalidDataException("This isn't a texture package Drip Swapper understands. " + ex?.Message);
	}

	private static Info Read(byte[] b, int k)
	{
		if (U32(b, 0) != 0)
		{
			throw new InvalidDataException("versioned package");
		}
		int num = (int)U32(b, 4);
		int[] array = new int[k];
		for (int i = 0; i < k; i++)
		{
			array[i] = (int)U32(b, 24 + 4 * i);
		}
		if (num <= 0 || num > b.Length)
		{
			throw new InvalidDataException("bad header size");
		}
		int num2 = 24 + 4 * k;
		int num3 = (int)U32(b, num2);
		if (num3 <= 0 || num3 > 100000)
		{
			throw new InvalidDataException("bad name count");
		}
		int num4 = num2 + 4;
		int num5 = (int)U32(b, num4);
		num4 += 12 + 8 * num3 + 2 * num3 + num5;
		int num6 = -1;
		for (int j = num4; j < num4 + 40; j++)
		{
			ulong num7 = U64(b, j);
			if (num7 % 32 == 0L && num7 < 100000 && (long)(j + 8) + (long)num7 == array[0])
			{
				num6 = j;
				break;
			}
		}
		if (num6 < 0)
		{
			throw new InvalidDataException("no bulk data map");
		}
		Info info = new Info
		{
			OffsetCount = k,
			HeaderSize = num,
			BulkMapPos = num6,
			BulkEntries = (int)(U64(b, num6) / 32),
			Offsets = array
		};
		info.ExportSize = (int)U64(b, array[2] + 8);
		if (num + info.ExportSize != b.Length)
		{
			throw new InvalidDataException("export size doesn't reach the end of the chunk");
		}
		int num8 = IndexOf(b, "PF_", num);
		if (num8 < 0)
		{
			throw new InvalidDataException("no pixel format");
		}
		int num9 = num8 - 4;
		int num10 = (int)U32(b, num9);
		if (num10 < 4 || num10 > 40 || b[num9 + 4 + num10 - 1] != 0)
		{
			throw new InvalidDataException("bad pixel format string");
		}
		info.PlatformStart = num9;
		info.PixelFormat = Encoding.ASCII.GetString(b, num8, num10 - 1);
		info.Width = (int)U32(b, num9 - 12);
		info.Height = (int)U32(b, num9 - 8);
		int num11 = num9 + 4 + num10;
		info.FirstMip = (int)U32(b, num11);
		info.MipCount = (int)U32(b, num11 + 4);
		if (info.MipCount < 1 || info.MipCount > 16)
		{
			throw new InvalidDataException("bad mip count");
		}
		for (int l = num; l < num9 - 12; l++)
		{
			if ((long)(l + 8) + (long)U64(b, l) == b.Length && U64(b, l) > 64)
			{
				info.SkipPos = l;
				break;
			}
		}
		if (info.SkipPos == 0)
		{
			throw new InvalidDataException("no skip offset");
		}
		return info;
	}

	private static int IndexOf(byte[] b, string s, int from)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(s);
		for (int i = from; i + bytes.Length <= b.Length; i++)
		{
			int j;
			for (j = 0; j < bytes.Length && b[i + j] == bytes[j]; j++)
			{
			}
			if (j == bytes.Length)
			{
				return i;
			}
		}
		return -1;
	}

	public static byte[] Build(byte[] original, TexImage image)
	{
		return Build(original, image, trimToSlots: false);
	}

	public static byte[] Build(byte[] original, TexImage image, bool trimToSlots)
	{
		Info info = Read(original);
		if (info.PixelFormat != "PF_DXT1")
		{
			throw new InvalidDataException("Only DXT1 colour textures can be replaced (this one is " + info.PixelFormat + ").");
		}
		List<Bc1.Mip> list = Bc1.EncodeChain(image);
		if (trimToSlots && list.Count > info.BulkEntries)
		{
			list = list.Take(info.BulkEntries).ToList();
		}
		return BuildMips(original, info, image.Width, image.Height, list);
	}

	public static byte[] BuildSolid(byte[] original, byte r, byte g, byte b)
	{
		Info info = Read(original);
		List<Bc1.Mip> list = new List<Bc1.Mip>();
		for (int num = 4; num >= 1; num /= 2)
		{
			string pixelFormat = info.PixelFormat;
			byte[] data;
			if (!(pixelFormat == "PF_DXT1"))
			{
				if (!(pixelFormat == "PF_BC5"))
				{
					throw new InvalidDataException("A one-colour picture can't be written into a " + info.PixelFormat + " texture.");
				}
				data = new byte[16]
				{
					r, r, 0, 0, 0, 0, 0, 0, g, g,
					0, 0, 0, 0, 0, 0
				};
			}
			else
			{
				ushort num2 = (ushort)(((r * 31 + 127) / 255 << 11) | ((g * 63 + 127) / 255 << 5) | ((b * 31 + 127) / 255));
				data = new byte[8]
				{
					(byte)num2,
					(byte)(num2 >> 8),
					(byte)num2,
					(byte)(num2 >> 8),
					0,
					0,
					0,
					0
				};
			}
			list.Add(new Bc1.Mip(num, num, data));
		}
		return BuildMips(original, info, 4, 4, list);
	}

	private static byte[] BuildMips(byte[] original, Info info, int width, int height, IReadOnlyList<Bc1.Mip> mips)
	{
		if (mips.Count > info.BulkEntries)
		{
			throw new InvalidDataException("The texture has fewer slots than the new picture needs.");
		}
		int headerSize = info.HeaderSize;
		int platformStart = info.PlatformStart;
		byte[] array = original.AsSpan(headerSize, platformStart - 12 - headerSize).ToArray();
		int num = info.SkipPos - headerSize;
		int num2 = -1;
		for (int i = 0; i + 8 <= Math.Min(array.Length, 24); i++)
		{
			if (BinaryPrimitives.ReadInt32LittleEndian(array.AsSpan(i)) == info.Width && BinaryPrimitives.ReadInt32LittleEndian(array.AsSpan(i + 4)) == info.Height)
			{
				num2 = i;
				break;
			}
		}
		if (num2 >= 0)
		{
			BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num2), width);
			BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num2 + 4), height);
			int num3 = num2 + 8 + 16;
			if (num - num3 == 29)
			{
				BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num3), 0);
			}
		}
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(array);
		binaryWriter.Write((uint)width);
		binaryWriter.Write((uint)height);
		binaryWriter.Write(U32(original, platformStart - 4));
		int num4 = (int)U32(original, platformStart);
		binaryWriter.Write(original.AsSpan(platformStart, 4 + num4));
		binaryWriter.Write(0u);
		binaryWriter.Write((uint)mips.Count);
		long[] array2 = new long[mips.Count];
		for (int j = 0; j < mips.Count; j++)
		{
			binaryWriter.Write((uint)j);
			array2[j] = memoryStream.Position;
			binaryWriter.Write(mips[j].Data);
			binaryWriter.Write((uint)mips[j].Width);
			binaryWriter.Write((uint)mips[j].Height);
			binaryWriter.Write(1u);
		}
		int num5 = platformStart + 4 + num4 + 8;
		for (int k = 0; k < info.MipCount; k++)
		{
			uint num6 = U32(original, num5);
			num5 += 4;
			int num7 = (int)U64(original, info.BulkMapPos + 8 + (int)(32 * num6) + 16);
			if ((U32(original, info.BulkMapPos + 8 + (int)(32 * num6) + 24) & 0x40) != 0)
			{
				num5 += num7;
			}
			num5 += 12;
		}
		binaryWriter.Write(original.AsSpan(num5));
		binaryWriter.Flush();
		byte[] array3 = memoryStream.ToArray();
		BinaryPrimitives.WriteUInt64LittleEndian(array3.AsSpan(num), (ulong)(array3.Length - (num + 8)));
		byte[] array4 = new byte[headerSize + array3.Length];
		Array.Copy(original, 0, array4, 0, headerSize);
		Array.Copy(array3, 0, array4, headerSize, array3.Length);
		BinaryPrimitives.WriteUInt64LittleEndian(array4.AsSpan(info.Offsets[2] + 8), (ulong)array3.Length);
		int num8 = info.BulkMapPos + 8;
		for (int l = 0; l < info.BulkEntries; l++)
		{
			int num9 = Math.Min(l, mips.Count - 1);
			int num10 = num8 + 32 * l;
			BinaryPrimitives.WriteUInt64LittleEndian(array4.AsSpan(num10), (ulong)array2[num9]);
			BinaryPrimitives.WriteUInt64LittleEndian(array4.AsSpan(num10 + 8), ulong.MaxValue);
			BinaryPrimitives.WriteUInt64LittleEndian(array4.AsSpan(num10 + 16), (ulong)mips[num9].Data.Length);
			BinaryPrimitives.WriteUInt32LittleEndian(array4.AsSpan(num10 + 24), 72u);
			BinaryPrimitives.WriteUInt32LittleEndian(array4.AsSpan(num10 + 28), 0u);
		}
		return array4;
	}
}
