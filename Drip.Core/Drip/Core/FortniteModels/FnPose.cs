using System.IO;

namespace Drip.Core.FortniteModels;

public static class FnPose
{
	public static double[][] TPoseSkin(FnMeshInfo info)
	{
		double[][] array = info.Globals();
		int num = array.Length;
		double[][] array2 = new double[num][];
		for (int i = 0; i < num; i++)
		{
			array2[i] = (double[])array[i].Clone();
		}
		(string, double)[] array3 = new (string, double)[2]
		{
			("l", 1.0),
			("r", -1.0)
		};
		for (int j = 0; j < array3.Length; j++)
		{
			(string, double) tuple = array3[j];
			string item = tuple.Item1;
			double item2 = tuple.Item2;
			int bone = Need(info, "upperarm_" + item);
			int num2 = Need(info, "lowerarm_" + item);
			int num3 = Need(info, "hand_" + item);
			int toward = Need(info, "middle_01_" + item);
			Straighten(info, array2, bone, num2, item2);
			Straighten(info, array2, num2, num3, item2);
			Straighten(info, array2, num3, toward, item2);
		}
		double[][] array4 = new double[num][];
		for (int k = 0; k < num; k++)
		{
			array4[k] = FnMath.Mul(array2[k], FnMath.Inverse(array[k]));
		}
		return array4;
	}

	private static int Need(FnMeshInfo info, string bone)
	{
		int num = info.IndexOf(bone);
		if (num < 0)
		{
			throw new InvalidDataException("The character skeleton has no bone called " + bone + ".");
		}
		return num;
	}

	private static void Straighten(FnMeshInfo info, double[][] g, int bone, int toward, double sx)
	{
		double ax = g[toward][3] - g[bone][3];
		double ay = g[toward][7] - g[bone][7];
		double az = g[toward][11] - g[bone][11];
		double[] array = FnMath.RotAlign(ax, ay, az, sx, 0.0, 0.0);
		double num = g[bone][3];
		double num2 = g[bone][7];
		double num3 = g[bone][11];
		FnMath.Rot3(array, num, num2, num3, out var ox, out var oy, out var oz);
		double[] a = new double[16]
		{
			array[0],
			array[1],
			array[2],
			num - ox,
			array[3],
			array[4],
			array[5],
			num2 - oy,
			array[6],
			array[7],
			array[8],
			num3 - oz,
			0.0,
			0.0,
			0.0,
			1.0
		};
		int num4 = g.Length;
		bool[] array2 = new bool[num4];
		for (int i = bone; i < num4; i++)
		{
			array2[i] = i == bone || (info.Parent[i] >= 0 && array2[info.Parent[i]]);
			if (array2[i])
			{
				g[i] = FnMath.Mul(a, g[i]);
			}
		}
	}

	public static double[][] Blend(byte[] boneIdx, byte[] weights, int vertexCount, int influences, ushort[] boneMap, double[][] sm)
	{
		double[][] array = new double[vertexCount][];
		for (int i = 0; i < vertexCount; i++)
		{
			double[] array2 = new double[16];
			double num = 0.0;
			for (int j = 0; j < influences; j++)
			{
				num += (double)(int)weights[i * influences + j];
			}
			if (num <= 0.0)
			{
				array[i] = FnMath.Identity();
				continue;
			}
			for (int k = 0; k < influences; k++)
			{
				int num2 = weights[i * influences + k];
				if (num2 != 0)
				{
					int num3 = boneIdx[i * influences + k];
					int num4 = ((num3 < boneMap.Length) ? boneMap[num3] : 0);
					double[] array3 = sm[num4];
					double num5 = (double)num2 / num;
					for (int l = 0; l < 16; l++)
					{
						array2[l] += num5 * array3[l];
					}
				}
			}
			array[i] = array2;
		}
		return array;
	}
}
