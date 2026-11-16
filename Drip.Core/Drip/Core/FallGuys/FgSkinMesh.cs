using System;
using System.Numerics;

namespace Drip.Core.FallGuys;

public sealed class FgSkinMesh
{
	public string Name = "";

	public Vector3[] Pos = Array.Empty<Vector3>();

	public Vector3[] Nrm = Array.Empty<Vector3>();

	public Vector4[] Tan = Array.Empty<Vector4>();

	public Vector2[] Uv = Array.Empty<Vector2>();

	public float[] W = Array.Empty<float>();

	public int[] B = Array.Empty<int>();

	public int[] Idx = Array.Empty<int>();

	public float[][] BindPoses = Array.Empty<float[]>();

	public int VertexCount => Pos.Length;

	public int TriangleCount => Idx.Length / 3;

	public (Vector3 Min, Vector3 Max) Bounds()
	{
		if (Pos.Length == 0)
		{
			return (Min: Vector3.Zero, Max: Vector3.Zero);
		}
		Vector3 vector = new Vector3(float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue);
		Vector3[] pos = Pos;
		foreach (Vector3 value in pos)
		{
			vector = Vector3.Min(vector, value);
			vector2 = Vector3.Max(vector2, value);
		}
		return (Min: vector, Max: vector2);
	}

	public static Vector3 Apply(float[] m, Vector3 p)
	{
		return new Vector3(m[0] * p.X + m[1] * p.Y + m[2] * p.Z + m[3], m[4] * p.X + m[5] * p.Y + m[6] * p.Z + m[7], m[8] * p.X + m[9] * p.Y + m[10] * p.Z + m[11]);
	}

	public (Vector3 Min, Vector3 Max)[] BoneBounds()
	{
		int num = BindPoses.Length;
		Vector3[] array = new Vector3[num];
		Vector3[] array2 = new Vector3[num];
		bool[] array3 = new bool[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new Vector3(float.MaxValue);
			array2[i] = new Vector3(float.MinValue);
		}
		for (int j = 0; j < Pos.Length; j++)
		{
			for (int k = 0; k < 4; k++)
			{
				if (!(W[j * 4 + k] <= 0f))
				{
					int num2 = B[j * 4 + k];
					if (num2 >= 0 && num2 < num)
					{
						Vector3 value = Apply(BindPoses[num2], Pos[j]);
						array[num2] = Vector3.Min(array[num2], value);
						array2[num2] = Vector3.Max(array2[num2], value);
						array3[num2] = true;
					}
				}
			}
		}
		(Vector3, Vector3)[] array4 = new (Vector3, Vector3)[num];
		for (int l = 0; l < num; l++)
		{
			array4[l] = (array3[l] ? (array[l], array2[l]) : (Vector3.Zero, Vector3.Zero));
		}
		return array4;
	}

	public void ComputeNormals()
	{
		Vector3[] array = new Vector3[Pos.Length];
		for (int i = 0; i + 2 < Idx.Length; i += 3)
		{
			Vector3 vector = Pos[Idx[i]];
			Vector3 vector2 = Pos[Idx[i + 1]];
			Vector3 vector3 = Vector3.Cross(vector2: Pos[Idx[i + 2]] - vector, vector1: vector2 - vector);
			array[Idx[i]] += vector3;
			array[Idx[i + 1]] += vector3;
			array[Idx[i + 2]] += vector3;
		}
		Nrm = new Vector3[Pos.Length];
		for (int j = 0; j < array.Length; j++)
		{
			Nrm[j] = ((array[j].LengthSquared() > 1E-20f) ? Vector3.Normalize(array[j]) : Vector3.UnitY);
		}
	}

	public void ComputeTangents()
	{
		Vector3[] array = new Vector3[Pos.Length];
		Vector3[] array2 = new Vector3[Pos.Length];
		for (int i = 0; i + 2 < Idx.Length; i += 3)
		{
			int num = Idx[i];
			int num2 = Idx[i + 1];
			int num3 = Idx[i + 2];
			Vector3 vector = Pos[num2] - Pos[num];
			Vector3 vector2 = Pos[num3] - Pos[num];
			Vector2 vector3 = Uv[num2] - Uv[num];
			Vector2 vector4 = Uv[num3] - Uv[num];
			float num4 = vector3.X * vector4.Y - vector4.X * vector3.Y;
			if (!(MathF.Abs(num4) < 1E-12f))
			{
				float num5 = 1f / num4;
				Vector3 vector5 = (vector * vector4.Y - vector2 * vector3.Y) * num5;
				Vector3 vector6 = (vector2 * vector3.X - vector * vector4.X) * num5;
				array[num] += vector5;
				array[num2] += vector5;
				array[num3] += vector5;
				array2[num] += vector6;
				array2[num2] += vector6;
				array2[num3] += vector6;
			}
		}
		Tan = new Vector4[Pos.Length];
		for (int j = 0; j < Pos.Length; j++)
		{
			Vector3 vector7 = ((Nrm.Length > j) ? Nrm[j] : Vector3.UnitY);
			Vector3 value = array[j] - vector7 * Vector3.Dot(vector7, array[j]);
			if (value.LengthSquared() < 1E-20f)
			{
				value = ((MathF.Abs(vector7.X) < 0.9f) ? Vector3.Cross(vector7, Vector3.UnitX) : Vector3.Cross(vector7, Vector3.UnitY));
			}
			value = Vector3.Normalize(value);
			float w = ((Vector3.Dot(Vector3.Cross(vector7, value), array2[j]) < 0f) ? (-1f) : 1f);
			Tan[j] = new Vector4(value, w);
		}
	}
}
