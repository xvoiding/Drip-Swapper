using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Drip.Core.Zen;

namespace Drip.Core.FortniteModels;

public sealed class FnMeshInfo
{
	public string[] BoneNames = Array.Empty<string>();

	public int[] Parent = Array.Empty<int>();

	public double[][] Local = Array.Empty<double[]>();

	public Dictionary<int, ushort[]> BoneMaps = new Dictionary<int, ushort[]>();

	public static FnMeshInfo Read(byte[] type1, IReadOnlyList<FnLod> lods)
	{
		ZenPackage zen = ZenPackage.Parse(type1);
		FnMeshInfo fnMeshInfo = new FnMeshInfo();
		ReadSkeleton(type1, zen, fnMeshInfo);
		int nBones = fnMeshInfo.BoneNames.Length;
		for (int i = 0; i < lods.Count; i++)
		{
			ushort[] array = FindBoneMap(type1, lods[i].VertexCount, lods[i].Influences, nBones);
			if (array != null)
			{
				fnMeshInfo.BoneMaps[i] = array;
			}
		}
		return fnMeshInfo;
	}

	private static void ReadSkeleton(byte[] t, ZenPackage zen, FnMeshInfo info)
	{
		int count = zen.Names.Count;
		for (int i = 8; i + 8 < t.Length; i++)
		{
			int num = BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(i));
			if (num < 20 || num > 1500)
			{
				continue;
			}
			long num2 = (long)i + 4L + 12L * (long)num;
			if (num2 + 4 + 80L * (long)num > t.Length || BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(i + 4 + 8)) != -1 || BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(i + 4 + 12 + 8)) != 0 || BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan((int)num2)) != num)
			{
				continue;
			}
			bool flag = true;
			string[] array = new string[num];
			int[] array2 = new int[num];
			for (int j = 0; (j < num) & flag; j++)
			{
				int num3 = i + 4 + 12 * j;
				uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(t.AsSpan(num3)) & 0x3FFFFFFF;
				int num5 = BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(num3 + 8));
				if (num4 >= count || num5 < -1 || num5 >= j)
				{
					flag = false;
					break;
				}
				array[j] = zen.Names[(int)num4].Value;
				array2[j] = num5;
			}
			if (!flag)
			{
				continue;
			}
			double[][] array3 = new double[num][];
			int num6 = (int)num2 + 4;
			for (int k = 0; k < num; k++)
			{
				double[] array4 = new double[10];
				for (int l = 0; l < 10; l++)
				{
					array4[l] = BitConverter.ToDouble(t, num6 + 80 * k + 8 * l);
				}
				double num7 = Math.Sqrt(array4[0] * array4[0] + array4[1] * array4[1] + array4[2] * array4[2] + array4[3] * array4[3]);
				if (num7 < 0.99 || num7 > 1.01)
				{
					flag = false;
					break;
				}
				array3[k] = FnMath.FromQuatTrs(array4);
			}
			if (flag)
			{
				info.BoneNames = array;
				info.Parent = array2;
				info.Local = array3;
				return;
			}
		}
		throw new InvalidDataException("Couldn't find the skeleton inside the mesh package.");
	}

	private static ushort[]? FindBoneMap(byte[] t, int numVerts, int influences, int nBones)
	{
		for (int i = 8; i + 8 < t.Length; i++)
		{
			if (BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(i)) != numVerts || BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(i + 4)) != influences)
			{
				continue;
			}
			for (int j = 1; j <= nBones; j++)
			{
				int num = i - 2 * j - 4;
				if (num < 0)
				{
					break;
				}
				if (BinaryPrimitives.ReadInt32LittleEndian(t.AsSpan(num)) != j)
				{
					continue;
				}
				ushort[] array = new ushort[j];
				HashSet<ushort> hashSet = new HashSet<ushort>();
				bool flag = true;
				for (int k = 0; k < j; k++)
				{
					array[k] = BinaryPrimitives.ReadUInt16LittleEndian(t.AsSpan(num + 4 + 2 * k));
					if (array[k] >= nBones || !hashSet.Add(array[k]))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return array;
				}
			}
		}
		return null;
	}

	public int IndexOf(string bone)
	{
		return Array.IndexOf(BoneNames, bone);
	}

	public double[][] Globals()
	{
		double[][] array = new double[BoneNames.Length][];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ((Parent[i] < 0) ? Local[i] : FnMath.Mul(array[Parent[i]], Local[i]));
		}
		return array;
	}
}
