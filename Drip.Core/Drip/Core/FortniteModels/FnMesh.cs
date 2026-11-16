using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.FortniteModels;

public static class FnMesh
{
	private static int I32(byte[] b, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
	}

	private static uint U32(byte[] b, int p)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
	}

	public static List<FnLod> FindLods(byte[] b)
	{
		List<FnLod> list = new List<FnLod>();
		int num = 0;
		for (int i = 0; i < b.Length - 40; i++)
		{
			if (i < num || b[i] != 5 || b[i + 1] != 0)
			{
				continue;
			}
			int num2 = b[i + 2];
			if ((num2 != 2 && num2 != 4) || I32(b, i + 3) != num2)
			{
				continue;
			}
			int num3 = I32(b, i + 7);
			if (num3 <= 0 || num3 > 5000000 || num3 % 3 != 0)
			{
				continue;
			}
			long num4 = (long)i + 11L + (long)num2 * (long)num3;
			if (num4 + 16 < b.Length && I32(b, (int)num4) == 12 && I32(b, (int)num4 + 4) == I32(b, (int)num4 + 12))
			{
				FnLod fnLod = TryParse(b, i);
				if (fnLod != null)
				{
					list.Add(fnLod);
					num = fnLod.End;
				}
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidDataException("This isn't a mesh layout Drip Swapper understands (no LODs found).");
		}
		return list;
	}

	private static FnLod? TryParse(byte[] b, int o)
	{
		try
		{
			FnLod fnLod = new FnLod
			{
				Start = o
			};
			int num = o + 2 + 1;
			int num2 = I32(b, num);
			int num3 = I32(b, num + 4);
			num = (fnLod.IndexOffset = num + 8);
			fnLod.IndexSize = num2;
			fnLod.IndexCount = num3;
			num += num2 * num3;
			num += 8;
			int num4 = I32(b, num);
			int num5 = I32(b, num + 4);
			num += 8;
			if (num4 != 12 || num5 <= 0)
			{
				return null;
			}
			fnLod.PosOffset = num;
			fnLod.VertexCount = num5;
			num += num4 * num5;
			num += 2;
			int num6 = I32(b, num);
			int num7 = I32(b, num + 4);
			num += 8;
			int num8 = I32(b, num);
			int num9 = I32(b, num + 4);
			num += 8;
			if (num7 != num5 || num8 != 0 || num9 != 0 || num6 < 1 || num6 > 4)
			{
				return null;
			}
			fnLod.NumTexCoords = num6;
			int num10 = I32(b, num);
			int num11 = I32(b, num + 4);
			num += 8;
			if (num10 != 8 || num11 != num5)
			{
				return null;
			}
			fnLod.TanOffset = num;
			num += num10 * num11;
			int num12 = I32(b, num);
			int num13 = I32(b, num + 4);
			num += 8;
			if (num12 != 4 * num6 || num13 != num5)
			{
				return null;
			}
			fnLod.UvOffset = num;
			fnLod.UvStride = num12;
			num += num12 * num13;
			num += 2;
			num += 4;
			uint num14 = U32(b, num);
			num += 4;
			num += 16;
			int num15 = I32(b, num);
			int num16 = I32(b, num + 4);
			num += 8;
			if (num14 < 1 || num14 > 12)
			{
				return null;
			}
			long num17 = (long)num15 * (long)num16;
			if (num17 % num5 != 0L)
			{
				return null;
			}
			fnLod.SkinStride = (int)(num17 / num5);
			if (fnLod.SkinStride != 2 * num14)
			{
				return null;
			}
			fnLod.SkinOffset = num;
			fnLod.Influences = (int)num14;
			num += (int)num17;
			if (num > b.Length)
			{
				return null;
			}
			fnLod.End = num;
			for (int i = num; i < num + 48 && i + 16 + 4L * (long)num5 <= b.Length; i++)
			{
				if (I32(b, i) == 4 && I32(b, i + 4) == num5 && I32(b, i + 8) == 4 && I32(b, i + 12) == num5)
				{
					fnLod.ColorOffset = i + 16;
					break;
				}
			}
			return fnLod;
		}
		catch (ArgumentOutOfRangeException)
		{
			return null;
		}
	}

	public static double[] ReadPositions(byte[] b, FnLod l)
	{
		double[] array = new double[l.VertexCount * 3];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = BitConverter.ToSingle(b, l.PosOffset + 4 * i);
		}
		return array;
	}

	public static (double[] X, double[] Z, sbyte[] Sign) ReadFrames(byte[] b, FnLod l)
	{
		int vertexCount = l.VertexCount;
		double[] array = new double[vertexCount * 3];
		double[] array2 = new double[vertexCount * 3];
		sbyte[] array3 = new sbyte[vertexCount];
		for (int i = 0; i < vertexCount; i++)
		{
			int num = l.TanOffset + 8 * i;
			for (int j = 0; j < 3; j++)
			{
				array[i * 3 + j] = (double)(sbyte)b[num + j] / 127.0;
				array2[i * 3 + j] = (double)(sbyte)b[num + 4 + j] / 127.0;
			}
			array3[i] = (sbyte)b[num + 7];
		}
		return (X: array, Z: array2, Sign: array3);
	}

	public static double[] ReadUv0(byte[] b, FnLod l)
	{
		int vertexCount = l.VertexCount;
		double[] array = new double[vertexCount * 2];
		for (int i = 0; i < vertexCount; i++)
		{
			int num = l.UvOffset + l.UvStride * i;
			array[i * 2] = (double)BitConverter.ToHalf(b, num);
			array[i * 2 + 1] = (double)BitConverter.ToHalf(b, num + 2);
		}
		return array;
	}

	public static int[] ReadTriangles(byte[] b, FnLod l)
	{
		int[] array = new int[l.IndexCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (int)((l.IndexSize == 2) ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(l.IndexOffset + 2 * i)) : U32(b, l.IndexOffset + 4 * i));
		}
		return array;
	}

	public static (byte[] Bone, byte[] Weight) ReadSkin(byte[] b, FnLod l)
	{
		int vertexCount = l.VertexCount;
		int influences = l.Influences;
		byte[] array = new byte[vertexCount * influences];
		byte[] array2 = new byte[vertexCount * influences];
		for (int i = 0; i < vertexCount; i++)
		{
			Array.Copy(b, l.SkinOffset + l.SkinStride * i, array, i * influences, influences);
			Array.Copy(b, l.SkinOffset + l.SkinStride * i + influences, array2, i * influences, influences);
		}
		return (Bone: array, Weight: array2);
	}

	public static void WritePositions(byte[] b, FnLod l, float[] pos)
	{
		if (pos.Length != l.VertexCount * 3)
		{
			throw new ArgumentException("wrong position count");
		}
		for (int i = 0; i < pos.Length; i++)
		{
			BitConverter.TryWriteBytes(b.AsSpan(l.PosOffset + 4 * i), pos[i]);
		}
	}

	public static void WriteTangents(byte[] b, FnLod l, byte[] tan)
	{
		if (tan.Length != l.VertexCount * 8)
		{
			throw new ArgumentException("wrong tangent size");
		}
		Array.Copy(tan, 0, b, l.TanOffset, tan.Length);
	}

	public static void WriteSingleBone(byte[] b, FnLod l)
	{
		int influences = l.Influences;
		for (int i = 0; i < l.VertexCount; i++)
		{
			int num = l.SkinOffset + l.SkinStride * i;
			for (int j = 0; j < influences; j++)
			{
				b[num + j] = 0;
				b[num + influences + j] = (byte)((j == 0) ? byte.MaxValue : 0);
			}
		}
	}

	public static byte[]? ReadColorRed(byte[] b, FnLod l)
	{
		if (l.ColorOffset < 0)
		{
			return null;
		}
		byte[] array = new byte[l.VertexCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = b[l.ColorOffset + 4 * i + 2];
		}
		return array;
	}

	public static (byte B, byte G, byte R, byte A)? CommonColor(byte[] b, FnLod l)
	{
		if (l.ColorOffset < 0)
		{
			return null;
		}
		Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
		for (int i = 0; i < l.VertexCount; i++)
		{
			uint key = BitConverter.ToUInt32(b, l.ColorOffset + 4 * i);
			dictionary[key] = dictionary.GetValueOrDefault(key) + 1;
		}
		uint key2 = dictionary.OrderByDescending((KeyValuePair<uint, int> kv) => kv.Value).First().Key;
		return ((byte)key2, (byte)(key2 >> 8), (byte)(key2 >> 16), (byte)(key2 >> 24));
	}

	public static void FillColors(byte[] b, FnLod l, byte r, byte g, byte bl, byte a)
	{
		if (l.ColorOffset >= 0)
		{
			for (int i = 0; i < l.VertexCount; i++)
			{
				int num = l.ColorOffset + 4 * i;
				b[num] = bl;
				b[num + 1] = g;
				b[num + 2] = r;
				b[num + 3] = a;
			}
		}
	}

	public static void WriteTriangles(byte[] b, FnLod l, int[] tri)
	{
		if (tri.Length != l.IndexCount)
		{
			throw new ArgumentException("wrong index count");
		}
		for (int i = 0; i < tri.Length; i++)
		{
			if (l.IndexSize == 2)
			{
				BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(l.IndexOffset + 2 * i), checked((ushort)tri[i]));
			}
			else
			{
				BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(l.IndexOffset + 4 * i), (uint)tri[i]);
			}
		}
	}

	public static void WriteUv(byte[] b, FnLod l, int vertex, double u, double v)
	{
		for (int i = 0; i < l.NumTexCoords; i++)
		{
			int num = l.UvOffset + l.UvStride * vertex + 4 * i;
			BitConverter.TryWriteBytes(b.AsSpan(num), (Half)u);
			BitConverter.TryWriteBytes(b.AsSpan(num + 2), (Half)v);
		}
	}

	public static void WriteSkin(byte[] b, FnLod l, int vertex, ReadOnlySpan<byte> bone, ReadOnlySpan<byte> weight)
	{
		int influences = l.Influences;
		int num = l.SkinOffset + l.SkinStride * vertex;
		for (int i = 0; i < influences; i++)
		{
			b[num + i] = (byte)((i < bone.Length) ? bone[i] : 0);
			b[num + influences + i] = (byte)((i < weight.Length) ? weight[i] : 0);
		}
	}

	public static byte SnormByte(double v)
	{
		return (byte)(sbyte)Math.Clamp((int)Math.Round(v * 127.0), -127, 127);
	}
}
