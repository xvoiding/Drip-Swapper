using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Drip.Core.FallGuys;

namespace Drip.Core.FortniteModels;

public static class FnItem
{
	private sealed class Mesh
	{
		public readonly List<double> Pos = new List<double>();

		public readonly List<int> Tri = new List<int>();

		public int Vertex(double x, double y, double z)
		{
			Pos.Add(x);
			Pos.Add(y);
			Pos.Add(z);
			return Pos.Count / 3 - 1;
		}

		public void Quad(int a, int b, int c, int d)
		{
			Tri.AddRange(new _003C_003Ez__ReadOnlyArray<int>(new int[6] { a, b, c, a, c, d }));
		}

		public void Box(double x0, double x1, double y0, double y1, double z0, double z1)
		{
			int[] array = new int[8]
			{
				Vertex(x0, y0, z0),
				Vertex(x1, y0, z0),
				Vertex(x1, y1, z0),
				Vertex(x0, y1, z0),
				Vertex(x0, y0, z1),
				Vertex(x1, y0, z1),
				Vertex(x1, y1, z1),
				Vertex(x0, y1, z1)
			};
			Quad(array[0], array[1], array[2], array[3]);
			Quad(array[4], array[7], array[6], array[5]);
			Quad(array[0], array[4], array[5], array[1]);
			Quad(array[2], array[6], array[7], array[3]);
			Quad(array[1], array[5], array[6], array[2]);
			Quad(array[3], array[7], array[4], array[0]);
		}

		public (double[], int[]) Done()
		{
			return (Pos.ToArray(), Tri.ToArray());
		}
	}

	public const double PickaxeLength = 100.0;

	public const double BackBlingSize = 55.0;

	public const double PickaxeBottom = 0.0;

	public static readonly Vector3 BackBlingCentre = new Vector3(0f, -22f, 116f);

	public const double PickaxeCharacterShift = -80.0;

	private static readonly Dictionary<FnItemKind, (double[] Pos, int[] Tri)> Cache = new Dictionary<FnItemKind, (double[], int[])>();

	public static string Id(FnItemKind k)
	{
		if (k != FnItemKind.Pickaxe)
		{
			return "backbling";
		}
		return "pickaxe";
	}

	public static string Label(FnItemKind k)
	{
		if (k != FnItemKind.Pickaxe)
		{
			return "back bling";
		}
		return "pickaxe";
	}

	public static FnItemKind? Find(string? id)
	{
		if (!string.Equals(id, "pickaxe", StringComparison.OrdinalIgnoreCase))
		{
			if (!string.Equals(id, "backbling", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			return FnItemKind.BackBling;
		}
		return FnItemKind.Pickaxe;
	}

	public static (double[] Pos, int[] Tri) Outline(FnItemKind kind)
	{
		lock (Cache)
		{
			if (!Cache.TryGetValue(kind, out (double[], int[]) value))
			{
				value = (Cache[kind] = ((kind == FnItemKind.Pickaxe) ? MakePickaxe() : MakeBackBling()));
			}
			return value;
		}
	}

	private static (double[], int[]) MakePickaxe()
	{
		Mesh mesh = new Mesh();
		mesh.Box(-2.2, 2.2, -2.2, 2.2, 0.0, 93.0);
		double num = 56.0;
		double num2 = 38.0;
		double num3 = 3.4;
		double num4 = 2.8;
		int num5 = -1;
		int num6 = -1;
		int num7 = -1;
		int num8 = -1;
		for (int i = 0; i <= 24; i++)
		{
			double num9 = (22.0 + 136.0 * (double)i / 24.0) * Math.PI / 180.0;
			double x = Math.Cos(num9) * (num2 + num3);
			double z = num + Math.Sin(num9) * (num2 + num3);
			double x2 = Math.Cos(num9) * (num2 - num3);
			double z2 = num + Math.Sin(num9) * (num2 - num3);
			int num10 = mesh.Vertex(x, num4, z);
			int num11 = mesh.Vertex(x2, num4, z2);
			int num12 = mesh.Vertex(x, 0.0 - num4, z);
			int num13 = mesh.Vertex(x2, 0.0 - num4, z2);
			if (num5 >= 0)
			{
				mesh.Quad(num5, num10, num11, num6);
				mesh.Quad(num7, num8, num13, num12);
				mesh.Quad(num5, num7, num12, num10);
				mesh.Quad(num6, num11, num13, num8);
			}
			num5 = num10;
			num6 = num11;
			num7 = num12;
			num8 = num13;
		}
		return mesh.Done();
	}

	private static (double[], int[]) MakeBackBling()
	{
		Mesh mesh = new Mesh();
		Vector3 backBlingCentre = BackBlingCentre;
		double num = 17.0;
		double num2 = 24.0;
		mesh.Box(0.0 - num, num, backBlingCentre.Y - 9f, backBlingCentre.Y + 9f, (double)backBlingCentre.Z - num2, (double)backBlingCentre.Z + num2 - 4.0);
		mesh.Box(0.0 - num + 3.0, num - 3.0, backBlingCentre.Y - 8f, backBlingCentre.Y + 8f, (double)backBlingCentre.Z + num2 - 4.0, (double)backBlingCentre.Z + num2 + 3.0);
		mesh.Box(0.0 - num + 5.0, num - 5.0, backBlingCentre.Y - 14f, backBlingCentre.Y - 9f, (double)backBlingCentre.Z - num2 + 4.0, backBlingCentre.Z - 4f);
		return mesh.Done();
	}

	public static FgUpAxis GuessUp(FgModelSource src, FnItemKind kind)
	{
		if (kind == FnItemKind.BackBling || src.VertexCount == 0)
		{
			return FgModelFit.GuessUp(src);
		}
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		float num5 = float.MinValue;
		float num6 = float.MinValue;
		Vector3[] pos = src.Pos;
		for (int i = 0; i < pos.Length; i++)
		{
			Vector3 vector = pos[i];
			num = Math.Min(num, vector.X);
			num4 = Math.Max(num4, vector.X);
			num2 = Math.Min(num2, vector.Y);
			num5 = Math.Max(num5, vector.Y);
			num3 = Math.Min(num3, vector.Z);
			num6 = Math.Max(num6, vector.Z);
		}
		float num7 = num4 - num;
		float num8 = num5 - num2;
		float num9 = num6 - num3;
		if (!(num8 >= num7) || !(num8 >= num9))
		{
			if (!(num9 >= num7))
			{
				return FgUpAxis.X;
			}
			return FgUpAxis.Z;
		}
		return FgUpAxis.Y;
	}

	private static bool IsHelper(FgSourceMaterial m)
	{
		if (m.Picture == null)
		{
			return m.Name.StartsWith("wire", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static FnTarget Build(FgModelSource src, FnItemKind kind, FnFitOptions fit)
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
			Vector3 value = FnTarget.ToUp(src.Pos[j], fit.Up);
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
		double num8 = Math.Max(0.0001, Math.Max(num5 - num2, Math.Max(num6 - num3, num7 - num4)));
		double num9 = ((kind == FnItemKind.Pickaxe) ? 100.0 : 55.0) / num8 * (double)fit.Scale;
		double num10 = (num2 + num5) / 2.0;
		double num11 = (num3 + num6) / 2.0;
		double num12 = (num4 + num7) / 2.0;
		double[] array3 = new double[vertexCount * 3];
		for (int k = 0; k < vertexCount; k++)
		{
			double num13 = ((double)array[k].X - num10) * num9 * (double)fit.Width + (double)fit.OffsetX;
			double num14 = ((double)array[k].Z - num12) * num9 + (double)fit.OffsetY;
			double num15;
			if (kind == FnItemKind.Pickaxe)
			{
				num15 = ((double)array[k].Y - num3) * num9 + 0.0 + (double)fit.OffsetZ;
			}
			else
			{
				num13 += (double)BackBlingCentre.X;
				num14 += (double)BackBlingCentre.Y;
				num15 = ((double)array[k].Y - num11) * num9 + (double)BackBlingCentre.Z + (double)fit.OffsetZ;
			}
			array3[k * 3] = num13;
			array3[k * 3 + 1] = num14;
			array3[k * 3 + 2] = num15;
		}
		int[] array4 = new int[list.Count * 3];
		int[] array5 = new int[list.Count];
		for (int l = 0; l < list.Count; l++)
		{
			int num16 = list[l];
			array4[l * 3] = src.Idx[num16];
			array4[l * 3 + 1] = src.Idx[num16 + 1];
			array4[l * 3 + 2] = src.Idx[num16 + 2];
			array5[l] = Math.Clamp(src.Mat[src.Idx[num16]], 0, src.Materials.Count - 1);
		}
		double[] array6 = new double[vertexCount * 2];
		for (int m = 0; m < vertexCount; m++)
		{
			array6[m * 2] = src.Uv[m].X;
			array6[m * 2 + 1] = src.Uv[m].Y;
		}
		return FnTarget.FromPlaced(array3, array4, array6, array5, src.Materials, src.HasUv, fit.PictureOverride);
	}

	public static double Overlap(FnTarget t, FnItemKind kind)
	{
		double[] item = Outline(kind).Pos;
		double num = double.MaxValue;
		double num2 = double.MaxValue;
		double num3 = double.MaxValue;
		double num4 = double.MinValue;
		double num5 = double.MinValue;
		double num6 = double.MinValue;
		for (int i = 0; i < item.Length; i += 3)
		{
			num = Math.Min(num, item[i]);
			num4 = Math.Max(num4, item[i]);
			num2 = Math.Min(num2, item[i + 1]);
			num5 = Math.Max(num5, item[i + 1]);
			num3 = Math.Min(num3, item[i + 2]);
			num6 = Math.Max(num6, item[i + 2]);
		}
		double num7 = (num4 - num) * 0.2;
		double num8 = Math.Max(10.0, (num5 - num2) * 0.2);
		double num9 = (num6 - num3) * 0.2;
		int num10 = t.Pos.Length / 3;
		int num11 = Math.Max(1, num10 / 4000);
		int num12 = 0;
		int num13 = 0;
		for (int j = 0; j < num10; j += num11)
		{
			num13++;
			double num14 = t.Pos[j * 3];
			double num15 = t.Pos[j * 3 + 1];
			double num16 = t.Pos[j * 3 + 2];
			if (num14 >= num - num7 && num14 <= num4 + num7 && num15 >= num2 - num8 && num15 <= num5 + num8 && num16 >= num3 - num9 && num16 <= num6 + num9)
			{
				num12++;
			}
		}
		if (num13 != 0)
		{
			return (double)num12 / (double)num13;
		}
		return 0.0;
	}
}
