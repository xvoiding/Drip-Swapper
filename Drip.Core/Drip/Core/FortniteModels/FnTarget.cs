using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Drip.Core.FallGuys;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public sealed class FnTarget
{
	public const double CharacterHeight = 165.8;

	public const double TorsoY = -2.0;

	public double[] Pos = Array.Empty<double>();

	public int[] Tri = Array.Empty<int>();

	public double[] VertexNormal = Array.Empty<double>();

	public double[] Uv = Array.Empty<double>();

	public int[] TriMaterial = Array.Empty<int>();

	public List<FgSourceMaterial> Materials = new List<FgSourceMaterial>();

	public bool HasUv;

	public bool InwardWinding;

	public TexImage? Override;

	private SurfaceIndex? _index;

	private readonly object _indexLock = new object();

	public (double X, double Y, double Z) Min;

	public (double X, double Y, double Z) Max;

	public SurfaceIndex Index
	{
		get
		{
			lock (_indexLock)
			{
				return _index ?? (_index = new SurfaceIndex(Pos, Tri, 0.25, 3000000L));
			}
		}
	}

	internal static Vector3 ToUp(Vector3 v, FgUpAxis up)
	{
		return Up(v, up);
	}

	private static Vector3 Up(Vector3 v, FgUpAxis up)
	{
		return up switch
		{
			FgUpAxis.Y => v, 
			FgUpAxis.NegY => new Vector3(0f - v.X, 0f - v.Y, v.Z), 
			FgUpAxis.Z => new Vector3(v.X, v.Z, 0f - v.Y), 
			FgUpAxis.NegZ => new Vector3(v.X, 0f - v.Z, v.Y), 
			FgUpAxis.X => new Vector3(0f - v.Y, v.X, v.Z), 
			FgUpAxis.NegX => new Vector3(v.Y, 0f - v.X, v.Z), 
			_ => v, 
		};
	}

	private static bool IsHelper(FgSourceMaterial m)
	{
		if (m.Picture == null)
		{
			return m.Name.StartsWith("wire", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static FnTarget Build(FgModelSource src, FnFitOptions fit)
	{
		int vertexCount = src.VertexCount;
		List<int> list = new List<int>();
		for (int i = 0; i + 2 < src.Idx.Length; i += 3)
		{
			int index = Math.Clamp(src.Mat[src.Idx[i]], 0, src.Materials.Count - 1);
			if (!IsHelper(src.Materials[index]))
			{
				list.Add(i);
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidDataException("That file has no usable triangles in it.");
		}
		Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, fit.Yaw * (float)Math.PI / 180f);
		Vector3[] array = new Vector3[vertexCount];
		bool[] array2 = new bool[vertexCount];
		foreach (int item in list)
		{
			int num = src.Idx[item];
			bool flag;
			array2[src.Idx[item + 1]] = (flag = (array2[src.Idx[item + 2]] = true));
			array2[num] = flag;
		}
		double num2 = double.MaxValue;
		double num3 = double.MaxValue;
		double num4 = double.MaxValue;
		double num5 = double.MinValue;
		double num6 = double.MinValue;
		double num7 = double.MinValue;
		for (int j = 0; j < vertexCount; j++)
		{
			Vector3 value = Up(src.Pos[j], fit.Up);
			if (fit.Mirror)
			{
				value.X = 0f - value.X;
			}
			value = (array[j] = Vector3.Transform(value, rotation));
			if (array2[j])
			{
				num2 = Math.Min(num2, value.X);
				num5 = Math.Max(num5, value.X);
				num3 = Math.Min(num3, value.Y);
				num6 = Math.Max(num6, value.Y);
				num4 = Math.Min(num4, value.Z);
				num7 = Math.Max(num7, value.Z);
			}
		}
		double num8 = Math.Max(num6 - num3, 0.0001);
		double num9 = 165.8 / num8 * (double)fit.Scale;
		double num10 = (num2 + num5) / 2.0;
		double num11 = double.MaxValue;
		double num12 = double.MinValue;
		for (int k = 0; k < vertexCount; k++)
		{
			if (array2[k])
			{
				double num13 = ((double)array[k].Y - num3) / num8;
				if (!(num13 < 0.4) && !(num13 > 0.7))
				{
					num11 = Math.Min(num11, array[k].Z);
					num12 = Math.Max(num12, array[k].Z);
				}
			}
		}
		double num14 = ((num11 <= num12) ? ((num11 + num12) / 2.0) : ((num4 + num7) / 2.0));
		double[] array3 = new double[vertexCount * 3];
		for (int l = 0; l < vertexCount; l++)
		{
			array3[l * 3] = ((double)array[l].X - num10) * num9 * (double)fit.Width + (double)fit.OffsetX;
			array3[l * 3 + 1] = ((double)array[l].Z - num14) * num9 + -2.0 + (double)fit.OffsetY;
			array3[l * 3 + 2] = ((double)array[l].Y - num3) * num9 + (double)fit.OffsetZ;
		}
		int[] array4 = new int[list.Count * 3];
		int[] array5 = new int[list.Count];
		for (int m = 0; m < list.Count; m++)
		{
			int num15 = list[m];
			array4[m * 3] = src.Idx[num15];
			array4[m * 3 + 1] = src.Idx[num15 + 1];
			array4[m * 3 + 2] = src.Idx[num15 + 2];
			array5[m] = Math.Clamp(src.Mat[src.Idx[num15]], 0, src.Materials.Count - 1);
		}
		double[] array6 = new double[vertexCount * 2];
		for (int n = 0; n < vertexCount; n++)
		{
			array6[n * 2] = src.Uv[n].X;
			array6[n * 2 + 1] = src.Uv[n].Y;
		}
		return FromPlaced(array3, array4, array6, array5, src.Materials, src.HasUv, fit.PictureOverride);
	}

	public static FnTarget FromPlaced(double[] pos, int[] tri, double[] uv, int[] triMat, List<FgSourceMaterial> materials, bool hasUv, TexImage? pictureOverride = null)
	{
		int num = pos.Length / 3;
		if (num == 0 || tri.Length == 0 || tri.Length % 3 != 0 || uv.Length != num * 2 || triMat.Length != tri.Length / 3 || materials.Count == 0)
		{
			throw new InvalidDataException("The model data is incomplete.");
		}
		int[] array = tri;
		foreach (int num2 in array)
		{
			if (num2 < 0 || num2 >= num)
			{
				throw new InvalidDataException("The model data points at a vertex that doesn't exist.");
			}
		}
		array = triMat;
		foreach (int num3 in array)
		{
			if (num3 < 0 || num3 >= materials.Count)
			{
				throw new InvalidDataException("The model data points at a material that doesn't exist.");
			}
		}
		FnTarget fnTarget = new FnTarget
		{
			Pos = pos,
			Tri = tri,
			Uv = uv,
			TriMaterial = triMat,
			Materials = materials,
			HasUv = hasUv,
			Override = pictureOverride
		};
		fnTarget.ComputeNormals();
		double num4 = double.MaxValue;
		double num5 = double.MaxValue;
		double num6 = double.MaxValue;
		double num7 = double.MinValue;
		double num8 = double.MinValue;
		double num9 = double.MinValue;
		array = tri;
		foreach (int num10 in array)
		{
			double val = pos[num10 * 3];
			double val2 = pos[num10 * 3 + 1];
			double val3 = pos[num10 * 3 + 2];
			num4 = Math.Min(num4, val);
			num7 = Math.Max(num7, val);
			num5 = Math.Min(num5, val2);
			num8 = Math.Max(num8, val2);
			num6 = Math.Min(num6, val3);
			num9 = Math.Max(num9, val3);
		}
		fnTarget.Min = (X: num4, Y: num5, Z: num6);
		fnTarget.Max = (X: num7, Y: num8, Z: num9);
		return fnTarget;
	}

	private void ComputeNormals()
	{
		int num = Pos.Length / 3;
		int num2 = Tri.Length / 3;
		double[] array = new double[num2 * 3];
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		for (int i = 0; i < num; i++)
		{
			num3 += Pos[i * 3];
			num4 += Pos[i * 3 + 1];
			num5 += Pos[i * 3 + 2];
		}
		num3 /= (double)num;
		num4 /= (double)num;
		num5 /= (double)num;
		double num6 = 0.0;
		for (int j = 0; j < num2; j++)
		{
			int num7 = Tri[j * 3] * 3;
			int num8 = Tri[j * 3 + 1] * 3;
			int num9 = Tri[j * 3 + 2] * 3;
			double num10 = Pos[num9] - Pos[num7];
			double num11 = Pos[num9 + 1] - Pos[num7 + 1];
			double num12 = Pos[num9 + 2] - Pos[num7 + 2];
			double num13 = Pos[num8] - Pos[num7];
			double num14 = Pos[num8 + 1] - Pos[num7 + 1];
			double num15 = Pos[num8 + 2] - Pos[num7 + 2];
			double num16 = num11 * num15 - num12 * num14;
			double num17 = num12 * num13 - num10 * num15;
			double num18 = num10 * num14 - num11 * num13;
			array[j * 3] = num16;
			array[j * 3 + 1] = num17;
			array[j * 3 + 2] = num18;
			double num19 = (Pos[num7] + Pos[num8] + Pos[num9]) / 3.0 - num3;
			double num20 = (Pos[num7 + 1] + Pos[num8 + 1] + Pos[num9 + 1]) / 3.0 - num4;
			double num21 = (Pos[num7 + 2] + Pos[num8 + 2] + Pos[num9 + 2]) / 3.0 - num5;
			num6 += num16 * num19 + num17 * num20 + num18 * num21;
		}
		double num22 = ((num6 >= 0.0) ? 1 : (-1));
		InwardWinding = num22 < 0.0;
		if (num22 < 0.0)
		{
			for (int k = 0; k < array.Length; k++)
			{
				array[k] = 0.0 - array[k];
			}
		}
		Dictionary<(long, long, long), int> dictionary = new Dictionary<(long, long, long), int>();
		int[] array2 = new int[num];
		for (int l = 0; l < num; l++)
		{
			(long, long, long) key = ((long)Math.Round(Pos[l * 3] * 1000.0), (long)Math.Round(Pos[l * 3 + 1] * 1000.0), (long)Math.Round(Pos[l * 3 + 2] * 1000.0));
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = dictionary.Count);
			}
			array2[l] = value;
		}
		double[] array3 = new double[dictionary.Count * 3];
		for (int m = 0; m < num2; m++)
		{
			for (int n = 0; n < 3; n++)
			{
				int num23 = array2[Tri[m * 3 + n]] * 3;
				array3[num23] += array[m * 3];
				array3[num23 + 1] += array[m * 3 + 1];
				array3[num23 + 2] += array[m * 3 + 2];
			}
		}
		VertexNormal = new double[num * 3];
		for (int num24 = 0; num24 < num; num24++)
		{
			int num25 = array2[num24] * 3;
			double num26 = Math.Sqrt(array3[num25] * array3[num25] + array3[num25 + 1] * array3[num25 + 1] + array3[num25 + 2] * array3[num25 + 2]);
			if (num26 < 1E-12)
			{
				VertexNormal[num24 * 3 + 2] = 1.0;
				continue;
			}
			VertexNormal[num24 * 3] = array3[num25] / num26;
			VertexNormal[num24 * 3 + 1] = array3[num25 + 1] / num26;
			VertexNormal[num24 * 3 + 2] = array3[num25 + 2] / num26;
		}
	}

	public void NormalAt(int tri, double bu, double bv, out double nx, out double ny, out double nz)
	{
		double num = 1.0 - bu - bv;
		int num2 = Tri[tri * 3] * 3;
		int num3 = Tri[tri * 3 + 1] * 3;
		int num4 = Tri[tri * 3 + 2] * 3;
		nx = num * VertexNormal[num2] + bu * VertexNormal[num3] + bv * VertexNormal[num4];
		ny = num * VertexNormal[num2 + 1] + bu * VertexNormal[num3 + 1] + bv * VertexNormal[num4 + 1];
		nz = num * VertexNormal[num2 + 2] + bu * VertexNormal[num3 + 2] + bv * VertexNormal[num4 + 2];
		double num5 = Math.Sqrt(nx * nx + ny * ny + nz * nz);
		if (num5 < 1E-12)
		{
			nx = 0.0;
			ny = 0.0;
			nz = 1.0;
		}
		else
		{
			nx /= num5;
			ny /= num5;
			nz /= num5;
		}
	}

	public void ColorAt(int tri, double bu, double bv, out double r, out double g, out double b)
	{
		FgSourceMaterial fgSourceMaterial = Materials[TriMaterial[tri]];
		TexImage texImage = Override ?? (HasUv ? fgSourceMaterial.Picture : null);
		if (texImage == null)
		{
			r = Math.Clamp(fgSourceMaterial.Color.X * 255f, 0f, 255f);
			g = Math.Clamp(fgSourceMaterial.Color.Y * 255f, 0f, 255f);
			b = Math.Clamp(fgSourceMaterial.Color.Z * 255f, 0f, 255f);
			return;
		}
		double num = 1.0 - bu - bv;
		int num2 = Tri[tri * 3];
		int num3 = Tri[tri * 3 + 1];
		int num4 = Tri[tri * 3 + 2];
		double u = num * Uv[num2 * 2] + bu * Uv[num3 * 2] + bv * Uv[num4 * 2];
		double num5 = num * Uv[num2 * 2 + 1] + bu * Uv[num3 * 2 + 1] + bv * Uv[num4 * 2 + 1];
		Sample(texImage, u, 1.0 - num5, out r, out g, out b);
	}

	private static void Sample(TexImage t, double u, double v, out double r, out double g, out double b)
	{
		int width = t.Width;
		int height = t.Height;
		double num = u * (double)width - 0.5;
		double num2 = v * (double)height - 0.5;
		int num3 = (int)Math.Floor(num);
		int num4 = (int)Math.Floor(num2);
		double num5 = num - (double)num3;
		double num6 = num2 - (double)num4;
		r = (g = (b = 0.0));
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 2; j++)
			{
				double num7 = ((j == 0) ? (1.0 - num5) : num5) * ((i == 0) ? (1.0 - num6) : num6);
				int num8 = ((num3 + j) % width + width) % width;
				int num9 = (((num4 + i) % height + height) % height * width + num8) * 4;
				r += (double)(int)t.Rgba[num9] * num7;
				g += (double)(int)t.Rgba[num9 + 1] * num7;
				b += (double)(int)t.Rgba[num9 + 2] * num7;
			}
		}
	}
}
