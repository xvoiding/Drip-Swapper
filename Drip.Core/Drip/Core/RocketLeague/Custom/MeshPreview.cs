using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public static class MeshPreview
{
	public static byte[] Render(CarMesh mesh, PreviewView view, int size, TexImage? picture = null, bool car = true, CarMesh? reference = null, Action<float>? scaleOut = null, bool arena = false)
	{
		byte[] array = new byte[size * size * 4];
		for (int i = 0; i < size * size; i++)
		{
			array[i * 4] = 28;
			array[i * 4 + 1] = 26;
			array[i * 4 + 2] = 26;
			array[i * 4 + 3] = byte.MaxValue;
		}
		float[] array2 = new float[size * size];
		Array.Fill(array2, float.MinValue);
		float a = 0.6f;
		Vector3 vector = Vector3.Normalize((view == PreviewView.Top) ? new Vector3(0.3f, 0.3f, 0.9f) : new Vector3(0.35f, 0.5f, 0.8f));
		Vector3[] array3 = mesh.Pos.Select(Proj).ToArray();
		Vector2 mn = new Vector2(float.MaxValue);
		Vector2 mx = new Vector2(float.MinValue);
		Vector3[] array4 = array3;
		foreach (Vector3 p in array4)
		{
			Grow(p);
		}
		Vector3[] array5 = new Vector3[2]
		{
			new Vector3(-60f, -36f, -23f),
			new Vector3(80f, 36f, 43f)
		};
		Vector3 p2 = new Vector3(10f, 0f, 16f);
		if (reference != null)
		{
			(Vector3 Min, Vector3 Max) tuple = reference.Bounds();
			Vector3 item = tuple.Min;
			Vector3 item2 = tuple.Max;
			array5 = new Vector3[2] { item, item2 };
			p2 = (item + item2) / 2f;
		}
		List<Vector3> list = new List<Vector3>();
		float[] array6 = new float[2]
		{
			array5[0].X,
			array5[1].X
		};
		foreach (float x in array6)
		{
			float[] array7 = new float[2]
			{
				array5[0].Y,
				array5[1].Y
			};
			foreach (float y in array7)
			{
				float[] array8 = new float[2]
				{
					array5[0].Z,
					array5[1].Z
				};
				foreach (float z in array8)
				{
					list.Add(Proj(new Vector3(x, y, z)));
				}
			}
		}
		if (car)
		{
			foreach (Vector3 item5 in list)
			{
				Grow(item5);
			}
		}
		List<(Vector3, Vector3, bool)> list2 = (arena ? ArenaLines() : null);
		if (list2 != null)
		{
			foreach (var (p3, p4, _) in list2)
			{
				Grow(Proj(p3));
				Grow(Proj(p4));
			}
		}
		float num = MathF.Max(mx.X - mn.X, mx.Y - mn.Y);
		if (num < 0.001f)
		{
			num = 1f;
		}
		float scale = (float)size * 0.86f / num;
		float ox = (float)size / 2f - (mn.X + mx.X) / 2f * scale;
		float oy = (float)size / 2f + (mn.Y + mx.Y) / 2f * scale;
		scaleOut?.Invoke(scale);
		if (car)
		{
			Vector2 value = new Vector2(float.MaxValue);
			Vector2 value2 = new Vector2(float.MinValue);
			foreach (Vector3 item6 in list)
			{
				Vector2 value3 = Scr(item6);
				value = Vector2.Min(value, value3);
				value2 = Vector2.Max(value2, value3);
			}
			for (int m = (int)value.X; m <= (int)value2.X; m++)
			{
				Dot(array, size, m, (int)value.Y);
				Dot(array, size, m, (int)value2.Y);
			}
			for (int n = (int)value.Y; n <= (int)value2.Y; n++)
			{
				Dot(array, size, (int)value.X, n);
				Dot(array, size, (int)value2.X, n);
			}
		}
		for (int num2 = 0; num2 + 2 < mesh.Idx.Length; num2 += 3)
		{
			int num3 = mesh.Idx[num2];
			int num4 = mesh.Idx[num2 + 1];
			int num5 = mesh.Idx[num2 + 2];
			Vector2 vector2 = Scr(array3[num3]);
			Vector2 vector3 = Scr(array3[num4]);
			Vector2 vector4 = Scr(array3[num5]);
			float num6 = (vector3.Y - vector4.Y) * (vector2.X - vector4.X) + (vector4.X - vector3.X) * (vector2.Y - vector4.Y);
			if (MathF.Abs(num6) < 1E-06f)
			{
				continue;
			}
			int num7 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector2.X, MathF.Min(vector3.X, vector4.X))));
			int num8 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector2.X, MathF.Max(vector3.X, vector4.X))));
			int num9 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector2.Y, MathF.Min(vector3.Y, vector4.Y))));
			int num10 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector2.Y, MathF.Max(vector3.Y, vector4.Y))));
			for (int num11 = num9; num11 <= num10; num11++)
			{
				for (int num12 = num7; num12 <= num8; num12++)
				{
					float num13 = ((vector3.Y - vector4.Y) * ((float)num12 - vector4.X) + (vector4.X - vector3.X) * ((float)num11 - vector4.Y)) / num6;
					float num14 = ((vector4.Y - vector2.Y) * ((float)num12 - vector4.X) + (vector2.X - vector4.X) * ((float)num11 - vector4.Y)) / num6;
					float num15 = 1f - num13 - num14;
					if (num13 < -0.0001f || num14 < -0.0001f || num15 < -0.0001f)
					{
						continue;
					}
					float num16 = num13 * array3[num3].Z + num14 * array3[num4].Z + num15 * array3[num5].Z;
					int num17 = num11 * size + num12;
					if (!(num16 <= array2[num17]))
					{
						array2[num17] = num16;
						Vector3 d = num13 * mesh.Nrm[num3] + num14 * mesh.Nrm[num4] + num15 * mesh.Nrm[num5];
						d = ((d.LengthSquared() > 1E-09f) ? Vector3.Normalize(ProjDir(d)) : Vector3.UnitZ);
						float num18 = MathF.Max(0f, Vector3.Dot(d, vector)) * 0.65f + 0.4f;
						float num19 = 0.78f;
						float num20 = 0.8f;
						float num21 = 0.86f;
						if (picture != null)
						{
							Vector2 vector5 = num13 * mesh.Uv[num3] + num14 * mesh.Uv[num4] + num15 * mesh.Uv[num5];
							int num22 = Math.Clamp((int)((vector5.X - MathF.Floor(vector5.X)) * (float)picture.Width), 0, picture.Width - 1);
							int num23 = (Math.Clamp((int)((vector5.Y - MathF.Floor(vector5.Y)) * (float)picture.Height), 0, picture.Height - 1) * picture.Width + num22) * 4;
							num19 = (float)(int)picture.Rgba[num23] / 255f;
							num20 = (float)(int)picture.Rgba[num23 + 1] / 255f;
							num21 = (float)(int)picture.Rgba[num23 + 2] / 255f;
						}
						array[num17 * 4] = (byte)Math.Clamp(num21 * num18 * 255f, 0f, 255f);
						array[num17 * 4 + 1] = (byte)Math.Clamp(num20 * num18 * 255f, 0f, 255f);
						array[num17 * 4 + 2] = (byte)Math.Clamp(num19 * num18 * 255f, 0f, 255f);
					}
				}
			}
		}
		if (list2 != null)
		{
			foreach (var (p5, p6, dash) in list2)
			{
				Line(array, size, Scr(Proj(p5)), Scr(Proj(p6)), 120, 200, byte.MaxValue, dash);
			}
		}
		if (car)
		{
			if (reference != null)
			{
				Vector2[] array9 = reference.Pos.Select((Vector3 v) => Scr(Proj(v))).ToArray();
				bool[] array10 = new bool[size * size];
				for (int num24 = 0; num24 + 2 < reference.Idx.Length; num24 += 3)
				{
					Vector2 vector6 = array9[reference.Idx[num24]];
					Vector2 vector7 = array9[reference.Idx[num24 + 1]];
					Vector2 vector8 = array9[reference.Idx[num24 + 2]];
					float num25 = (vector7.Y - vector8.Y) * (vector6.X - vector8.X) + (vector8.X - vector7.X) * (vector6.Y - vector8.Y);
					if (MathF.Abs(num25) < 1E-06f)
					{
						continue;
					}
					int num26 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector6.X, MathF.Min(vector7.X, vector8.X))));
					int num27 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector6.X, MathF.Max(vector7.X, vector8.X))));
					int num28 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector6.Y, MathF.Min(vector7.Y, vector8.Y))));
					int num29 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector6.Y, MathF.Max(vector7.Y, vector8.Y))));
					for (int num30 = num28; num30 <= num29; num30++)
					{
						for (int num31 = num26; num31 <= num27; num31++)
						{
							float num32 = ((vector7.Y - vector8.Y) * ((float)num31 - vector8.X) + (vector8.X - vector7.X) * ((float)num30 - vector8.Y)) / num25;
							float num33 = ((vector8.Y - vector6.Y) * ((float)num31 - vector8.X) + (vector6.X - vector8.X) * ((float)num30 - vector8.Y)) / num25;
							if (num32 >= -0.02f && num33 >= -0.02f && 1f - num32 - num33 >= -0.02f)
							{
								array10[num30 * size + num31] = true;
							}
						}
					}
				}
				for (int num34 = 1; num34 < size - 1; num34++)
				{
					for (int num35 = 1; num35 < size - 1; num35++)
					{
						if (array10[num34 * size + num35] && (!array10[num34 * size + num35 - 1] || !array10[num34 * size + num35 + 1] || !array10[(num34 - 1) * size + num35] || !array10[(num34 + 1) * size + num35]))
						{
							int num36 = (num34 * size + num35) * 4;
							array[num36] = 235;
							array[num36 + 1] = 190;
							array[num36 + 2] = 70;
						}
					}
				}
			}
			Vector2 vector9 = Scr(Proj(p2));
			Line(array, size, new Vector2(vector9.X, 0f), new Vector2(vector9.X, size - 1), 240, 210, 60, dash: true);
			Line(array, size, new Vector2(0f, vector9.Y), new Vector2(size - 1, vector9.Y), 240, 210, 60, dash: true);
			(Vector3 Min, Vector3 Max) tuple4 = mesh.Bounds();
			Vector3 item3 = tuple4.Min;
			Vector3 item4 = tuple4.Max;
			Vector2 vector10 = Scr(Proj((item3 + item4) / 2f));
			Line(array, size, new Vector2(vector10.X - 7f, vector10.Y), new Vector2(vector10.X + 7f, vector10.Y), byte.MaxValue, 90, 200);
			Line(array, size, new Vector2(vector10.X, vector10.Y - 7f), new Vector2(vector10.X, vector10.Y + 7f), byte.MaxValue, 90, 200);
		}
		return array;
		void Grow(Vector3 vector11)
		{
			mn = Vector2.Min(mn, new Vector2(vector11.X, vector11.Y));
			mx = Vector2.Max(mx, new Vector2(vector11.X, vector11.Y));
		}
		Vector3 Proj(Vector3 vector11)
		{
			return view switch
			{
				PreviewView.Side => new Vector3(vector11.X, vector11.Z, 0f - vector11.Y), 
				PreviewView.Top => new Vector3(vector11.X, 0f - vector11.Y, vector11.Z), 
				PreviewView.Front => new Vector3(vector11.Y, vector11.Z, vector11.X), 
				_ => new Vector3(vector11.X * MathF.Cos(a) + vector11.Y * MathF.Sin(a), vector11.Z, 0f - ((0f - vector11.X) * MathF.Sin(a) + vector11.Y * MathF.Cos(a))), 
			};
		}
		Vector3 ProjDir(Vector3 p7)
		{
			return Proj(p7);
		}
		Vector2 Scr(Vector3 vector11)
		{
			return new Vector2(vector11.X * scale + ox, oy - vector11.Y * scale);
		}
	}

	private static List<(Vector3, Vector3, bool)> ArenaLines()
	{
		List<(Vector3, Vector3, bool)> list = new List<(Vector3, Vector3, bool)>();
		Loop(0f, dashed: false);
		Loop(2044f, dashed: true);
		float[] array = new float[2] { -4096f, 4096f };
		foreach (float x in array)
		{
			float[] array2 = new float[2] { -5120f, 5120f };
			foreach (float y in array2)
			{
				Seg(new Vector3(x, y, 0f), new Vector3(x, y, 2044f), dashed: true);
			}
		}
		array = new float[2] { -1f, 1f };
		foreach (float num in array)
		{
			float y2 = num * 5120f;
			float y3 = num * 6000f;
			float[] array2 = new float[2] { 0f, 642f };
			foreach (float z in array2)
			{
				Seg(new Vector3(-892f, y2, z), new Vector3(892f, y2, z));
				Seg(new Vector3(-892f, y3, z), new Vector3(892f, y3, z));
				Seg(new Vector3(-892f, y2, z), new Vector3(-892f, y3, z));
				Seg(new Vector3(892f, y2, z), new Vector3(892f, y3, z));
			}
			array2 = new float[2] { -892f, 892f };
			foreach (float x2 in array2)
			{
				Seg(new Vector3(x2, y2, 0f), new Vector3(x2, y2, 642f));
				Seg(new Vector3(x2, y3, 0f), new Vector3(x2, y3, 642f));
			}
		}
		Seg(new Vector3(-4096f, 0f, 0f), new Vector3(4096f, 0f, 0f), dashed: true);
		for (int k = 0; k < 40; k++)
		{
			float x3 = (float)k * ((float)Math.PI * 2f) / 40f;
			float x4 = (float)(k + 1) * ((float)Math.PI * 2f) / 40f;
			Seg(new Vector3(1000f * MathF.Cos(x3), 1000f * MathF.Sin(x3), 0f), new Vector3(1000f * MathF.Cos(x4), 1000f * MathF.Sin(x4), 0f));
		}
		return list;
		void Loop(float z2, bool dashed)
		{
			Seg(new Vector3(-4096f, -5120f, z2), new Vector3(4096f, -5120f, z2), dashed);
			Seg(new Vector3(4096f, -5120f, z2), new Vector3(4096f, 5120f, z2), dashed);
			Seg(new Vector3(4096f, 5120f, z2), new Vector3(-4096f, 5120f, z2), dashed);
			Seg(new Vector3(-4096f, 5120f, z2), new Vector3(-4096f, -5120f, z2), dashed);
		}
		void Seg(Vector3 a, Vector3 b, bool dashed = false)
		{
			list.Add((a, b, dashed));
		}
	}

	private static void Line(byte[] px, int size, Vector2 a, Vector2 b, byte r, byte g, byte bl, bool dash = false)
	{
		int num = (int)MathF.Round(a.X);
		int num2 = (int)MathF.Round(a.Y);
		int num3 = (int)MathF.Round(b.X);
		int num4 = (int)MathF.Round(b.Y);
		if (Math.Abs(num) > 4000 || Math.Abs(num2) > 4000 || Math.Abs(num3) > 4000 || Math.Abs(num4) > 4000)
		{
			return;
		}
		int num5 = Math.Abs(num3 - num);
		int num6 = -Math.Abs(num4 - num2);
		int num7 = ((num < num3) ? 1 : (-1));
		int num8 = ((num2 < num4) ? 1 : (-1));
		int num9 = num5 + num6;
		int num10 = 0;
		while (true)
		{
			if ((!dash || num10 / 4 % 2 == 0) && num >= 0 && num2 >= 0 && num < size && num2 < size)
			{
				int num11 = (num2 * size + num) * 4;
				px[num11] = bl;
				px[num11 + 1] = g;
				px[num11 + 2] = r;
			}
			num10++;
			if (num != num3 || num2 != num4)
			{
				int num12 = 2 * num9;
				if (num12 >= num6)
				{
					num9 += num6;
					num += num7;
				}
				if (num12 <= num5)
				{
					num9 += num5;
					num2 += num8;
				}
				continue;
			}
			break;
		}
	}

	private static void Dot(byte[] px, int size, int x, int y)
	{
		if (x >= 0 && y >= 0 && x < size && y < size)
		{
			int num = (y * size + x) * 4;
			px[num] = 110;
			px[num + 1] = 90;
			px[num + 2] = 70;
		}
	}
}
