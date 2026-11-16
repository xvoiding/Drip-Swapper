using System;
using System.Collections.Generic;

namespace Drip.Core.FortniteModels;

public sealed class FnPart
{
	public double[] Pos = Array.Empty<double>();

	public double[] Nrm = Array.Empty<double>();

	public double[] Uv = Array.Empty<double>();

	public int[] Mat = Array.Empty<int>();

	public int[] Tri = Array.Empty<int>();

	public int VertexCount => Pos.Length / 3;

	public int TriCount => Tri.Length / 3;

	public FnPart Clone()
	{
		return new FnPart
		{
			Pos = (double[])Pos.Clone(),
			Nrm = (double[])Nrm.Clone(),
			Uv = (double[])Uv.Clone(),
			Mat = (int[])Mat.Clone(),
			Tri = (int[])Tri.Clone()
		};
	}

	public static FnPart From(FnTarget t)
	{
		int num = t.Pos.Length / 3;
		int[] array = new int[num];
		bool[] array2 = new bool[num];
		for (int i = 0; i < t.TriMaterial.Length; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				int num2 = t.Tri[i * 3 + j];
				if (!array2[num2])
				{
					array[num2] = t.TriMaterial[i];
					array2[num2] = true;
				}
			}
		}
		int[] array3 = (int[])t.Tri.Clone();
		if (t.InwardWinding)
		{
			for (int k = 0; k + 2 < array3.Length; k += 3)
			{
				ref int reference = ref array3[k + 1];
				ref int reference2 = ref array3[k + 2];
				int num3 = array3[k + 2];
				int num4 = array3[k + 1];
				reference = num3;
				reference2 = num4;
			}
		}
		return new FnPart
		{
			Pos = (double[])t.Pos.Clone(),
			Nrm = (double[])t.VertexNormal.Clone(),
			Uv = (double[])t.Uv.Clone(),
			Mat = array,
			Tri = array3
		}.Compact();
	}

	public FnPart Compact()
	{
		int vertexCount = VertexCount;
		List<int> list = new List<int>(Tri.Length);
		for (int i = 0; i + 2 < Tri.Length; i += 3)
		{
			int num = Tri[i];
			int num2 = Tri[i + 1];
			int num3 = Tri[i + 2];
			if (num != num2 && num2 != num3 && num != num3 && !(Area2(num, num2, num3) < 1E-14))
			{
				list.Add(num);
				list.Add(num2);
				list.Add(num3);
			}
		}
		int[] array = new int[vertexCount];
		Array.Fill(array, -1);
		int num4 = 0;
		foreach (int item in list)
		{
			if (array[item] < 0)
			{
				array[item] = num4++;
			}
		}
		FnPart fnPart = new FnPart
		{
			Pos = new double[num4 * 3],
			Nrm = new double[num4 * 3],
			Uv = new double[num4 * 2],
			Mat = new int[num4],
			Tri = new int[list.Count]
		};
		for (int j = 0; j < vertexCount; j++)
		{
			int num5 = array[j];
			if (num5 >= 0)
			{
				Array.Copy(Pos, j * 3, fnPart.Pos, num5 * 3, 3);
				Array.Copy(Nrm, j * 3, fnPart.Nrm, num5 * 3, 3);
				Array.Copy(Uv, j * 2, fnPart.Uv, num5 * 2, 2);
				fnPart.Mat[num5] = Mat[j];
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			fnPart.Tri[k] = array[list[k]];
		}
		return fnPart;
	}

	private double Area2(int a, int b, int c)
	{
		double num = Pos[b * 3] - Pos[a * 3];
		double num2 = Pos[b * 3 + 1] - Pos[a * 3 + 1];
		double num3 = Pos[b * 3 + 2] - Pos[a * 3 + 2];
		double num4 = Pos[c * 3] - Pos[a * 3];
		double num5 = Pos[c * 3 + 1] - Pos[a * 3 + 1];
		double num6 = Pos[c * 3 + 2] - Pos[a * 3 + 2];
		double num7 = num2 * num6 - num3 * num5;
		double num8 = num3 * num4 - num * num6;
		double num9 = num * num5 - num2 * num4;
		return num7 * num7 + num8 * num8 + num9 * num9;
	}
}
