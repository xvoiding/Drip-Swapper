using System;
using System.Collections.Generic;
using System.Numerics;

namespace Drip.Core.FallGuys;

public static class FgPreview
{
	public static byte[] Render(FgFittedModel m, float yawDegrees, int size, bool showBean = true)
	{
		byte[] array = new byte[size * size * 4];
		for (int i = 0; i < size; i++)
		{
			float num = (float)i / (float)size;
			byte b = (byte)(26f + 8f * (1f - num));
			byte b2 = (byte)(26f + 9f * (1f - num));
			byte b3 = (byte)(32f + 12f * (1f - num));
			for (int j = 0; j < size; j++)
			{
				int num2 = (i * size + j) * 4;
				array[num2] = b3;
				array[num2 + 1] = b2;
				array[num2 + 2] = b;
				array[num2 + 3] = byte.MaxValue;
			}
		}
		if (m.Pos.Length == 0)
		{
			return array;
		}
		float[] array2 = new float[size * size];
		Array.Fill(array2, float.MinValue);
		float x = yawDegrees * (float)Math.PI / 180f;
		float ca = MathF.Cos(x);
		float sa = MathF.Sin(x);
		float scale = (float)size * 0.86f / 2.3f;
		float ox = (float)size / 2f;
		float oy = (float)size * 0.93f;
		if (showBean)
		{
			for (int k = (int)((float)size * 0.1f); (float)k < (float)size * 0.9f; k++)
			{
				Dot(array, size, k, (int)oy, 70, 74, 84);
			}
		}
		Vector3[] array3 = new Vector3[m.Pos.Length];
		for (int l = 0; l < array3.Length; l++)
		{
			array3[l] = Proj(m.Pos[l]);
		}
		Vector3 vector = Vector3.Normalize(new Vector3(0.35f, 0.55f, 0.75f));
		Vector3[] array4 = new Vector3[m.Pos.Length];
		for (int n = 0; n < array4.Length; n++)
		{
			Vector3 vector2 = Rot((n < m.Nrm.Length) ? m.Nrm[n] : Vector3.UnitY);
			array4[n] = new Vector3(0f - vector2.X, vector2.Y, vector2.Z);
		}
		for (int num3 = 0; num3 + 2 < m.Idx.Length; num3 += 3)
		{
			int num4 = m.Idx[num3];
			int num5 = m.Idx[num3 + 1];
			int num6 = m.Idx[num3 + 2];
			Vector2 vector3 = Scr(array3[num4]);
			Vector2 vector4 = Scr(array3[num5]);
			Vector2 vector5 = Scr(array3[num6]);
			float num7 = (vector4.Y - vector5.Y) * (vector3.X - vector5.X) + (vector5.X - vector4.X) * (vector3.Y - vector5.Y);
			if (MathF.Abs(num7) < 1E-06f)
			{
				continue;
			}
			int num8 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector3.X, MathF.Min(vector4.X, vector5.X))));
			int num9 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector3.X, MathF.Max(vector4.X, vector5.X))));
			int num10 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector3.Y, MathF.Min(vector4.Y, vector5.Y))));
			int num11 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector3.Y, MathF.Max(vector4.Y, vector5.Y))));
			for (int num12 = num10; num12 <= num11; num12++)
			{
				for (int num13 = num8; num13 <= num9; num13++)
				{
					float num14 = ((vector4.Y - vector5.Y) * ((float)num13 - vector5.X) + (vector5.X - vector4.X) * ((float)num12 - vector5.Y)) / num7;
					float num15 = ((vector5.Y - vector3.Y) * ((float)num13 - vector5.X) + (vector3.X - vector5.X) * ((float)num12 - vector5.Y)) / num7;
					float num16 = 1f - num14 - num15;
					if (num14 < -0.0001f || num15 < -0.0001f || num16 < -0.0001f)
					{
						continue;
					}
					float num17 = num14 * array3[num4].Z + num15 * array3[num5].Z + num16 * array3[num6].Z;
					int num18 = num12 * size + num13;
					if (!(num17 <= array2[num18]))
					{
						array2[num18] = num17;
						Vector3 value = num14 * array4[num4] + num15 * array4[num5] + num16 * array4[num6];
						value = ((value.LengthSquared() > 1E-09f) ? Vector3.Normalize(value) : Vector3.UnitZ);
						float num19 = MathF.Max(0f, Vector3.Dot(value, vector)) * 0.62f + 0.38f;
						float num20 = 0.8f;
						float num21 = 0.82f;
						float num22 = 0.88f;
						if (m.Rgba.Length == m.Width * m.Height * 4 && m.Width > 0 && m.Uv.Length == array3.Length)
						{
							float num23 = num14 * m.Uv[num4].X + num15 * m.Uv[num5].X + num16 * m.Uv[num6].X;
							float num24 = num14 * m.Uv[num4].Y + num15 * m.Uv[num5].Y + num16 * m.Uv[num6].Y;
							num23 -= MathF.Floor(num23);
							num24 -= MathF.Floor(num24);
							int num25 = Math.Min(m.Width - 1, (int)(num23 * (float)m.Width));
							int num26 = (Math.Min(m.Height - 1, (int)((1f - num24) * (float)m.Height)) * m.Width + num25) * 4;
							num20 = (float)(int)m.Rgba[num26] / 255f;
							num21 = (float)(int)m.Rgba[num26 + 1] / 255f;
							num22 = (float)(int)m.Rgba[num26 + 2] / 255f;
						}
						array[num18 * 4] = (byte)Math.Clamp(num22 * num19 * 255f, 0f, 255f);
						array[num18 * 4 + 1] = (byte)Math.Clamp(num21 * num19 * 255f, 0f, 255f);
						array[num18 * 4 + 2] = (byte)Math.Clamp(num20 * num19 * 255f, 0f, 255f);
						array[num18 * 4 + 3] = byte.MaxValue;
					}
				}
			}
		}
		if (showBean)
		{
			BeanOutline(array, size, ca, sa, scale, ox, oy);
		}
		return array;
		Vector3 Proj(Vector3 p)
		{
			Vector3 vector6 = Rot(p);
			return new Vector3(0f - vector6.X, vector6.Y, vector6.Z);
		}
		Vector3 Rot(Vector3 p)
		{
			return new Vector3(p.X * ca + p.Z * sa, p.Y, (0f - p.X) * sa + p.Z * ca);
		}
		Vector2 Scr(Vector3 p)
		{
			return new Vector2(p.X * scale + ox, oy - p.Y * scale);
		}
	}

	private static void BeanOutline(byte[] px, int size, float ca, float sa, float scale, float ox, float oy)
	{
		List<(Vector2 A, Vector2 B, float R, float Y0, float Y1)> caps = new List<(Vector2, Vector2, float, float, float)>();
		int[] array = new int[2] { -1, 1 };
		foreach (int num in array)
		{
			Cap(new Vector3((float)num * 0.49f, 1.12f, 0.08f), new Vector3((float)num * 1.42f, 1.09f, 0.2f), 0.105f);
			Cap(new Vector3((float)num * 0.19f, 0.13f, 0.06f), new Vector3((float)num * 0.19f, 0.13f, 0.16f), 0.115f);
		}
		float bw = MathF.Sqrt(0.25f * ca * ca + 0.28089997f * sa * sa);
		float bc = Sx(0f, 0.03f);
		bool[] array2 = new bool[size * size];
		for (int j = 0; j < size; j++)
		{
			for (int k = 0; k < size; k++)
			{
				array2[j * size + k] = Inside(((float)k - ox) / scale, (oy - (float)j) / scale);
			}
		}
		for (int l = 1; l < size - 1; l++)
		{
			for (int m = 1; m < size - 1; m++)
			{
				int num2 = l * size + m;
				if (array2[num2] && (!array2[num2 - 1] || !array2[num2 + 1] || !array2[num2 - size] || !array2[num2 + size]))
				{
					Dot(px, size, m, l, 120, 215, byte.MaxValue);
					Dot(px, size, m + 1, l, 120, 215, byte.MaxValue);
					Dot(px, size, m, l + 1, 120, 215, byte.MaxValue);
				}
			}
		}
		void Cap(Vector3 a, Vector3 b, float r)
		{
			caps.Add((new Vector2(Sx(a.X, a.Z), a.Y), new Vector2(Sx(b.X, b.Z), b.Y), r, 0f, 0f));
		}
		bool Inside(float sx, float y)
		{
			float num3 = (sx - bc) / bw;
			float num4 = (y - 1.09f) / 0.84f;
			if (num3 * num3 + num4 * num4 <= 1f)
			{
				return true;
			}
			Vector2 vector = new Vector2(sx, y);
			foreach (var item in caps)
			{
				Vector2 vector2 = item.B - item.A;
				float num5 = Math.Clamp(Vector2.Dot(vector - item.A, vector2) / Math.Max(vector2.LengthSquared(), 1E-06f), 0f, 1f);
				if ((vector - (item.A + vector2 * num5)).LengthSquared() <= item.R * item.R)
				{
					return true;
				}
			}
			return false;
		}
		float Sx(float x, float z)
		{
			return 0f - (x * ca + z * sa);
		}
	}

	private static void Dot(byte[] px, int size, int x, int y, byte r, byte g, byte b)
	{
		if (x >= 0 && y >= 0 && x < size && y < size)
		{
			int num = (y * size + x) * 4;
			px[num] = b;
			px[num + 1] = g;
			px[num + 2] = r;
			px[num + 3] = byte.MaxValue;
		}
	}
}
