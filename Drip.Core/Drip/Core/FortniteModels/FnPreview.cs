using System;
using System.Collections.Generic;
using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class FnPreview
{
	private const float FrameHeight = 196f;

	public static byte[] RenderTarget(FnTarget t, float yaw, int size, bool guides = true, bool outline = true)
	{
		var (array, depth) = Start(size);
		if (guides)
		{
			DrawGuides(array, size, yaw);
		}
		Paint(array, depth, t, yaw, size);
		if (outline)
		{
			DrawOutline(array, size, yaw);
		}
		return array;
	}

	public static byte[] RenderItem(FnTarget t, FnItemKind kind, float yaw, int size, bool guides = true, bool outline = true, bool character = true)
	{
		(byte[] Px, float[] Depth) tuple = Start(size);
		byte[] item = tuple.Px;
		float[] item2 = tuple.Depth;
		double dx = ((kind == FnItemKind.Pickaxe) ? (-80.0) : 0.0);
		if (guides)
		{
			DrawGround(item, size);
		}
		if (character)
		{
			DrawOutline(item, size, yaw, FnOutline.Pos, FnOutline.Tri, 52, 60, 78, dx);
		}
		Paint(item, item2, t, yaw, size);
		if (outline)
		{
			var (pos, tri) = FnItem.Outline(kind);
			DrawOutline(item, size, yaw, pos, tri, 70, 175, 255, 0.0);
		}
		return item;
	}

	private static void Paint(byte[] px, float[] depth, FnTarget t, float yaw, int size)
	{
		int num = t.Pos.Length / 3;
		Vector3[] array = new Vector3[num];
		float x = yaw * (float)Math.PI / 180f;
		float ca = MathF.Cos(x);
		float sa = MathF.Sin(x);
		for (int i = 0; i < num; i++)
		{
			array[i] = View((float)t.Pos[i * 3], (float)t.Pos[i * 3 + 1], (float)t.Pos[i * 3 + 2], ca, sa);
		}
		Vector3 light = Vector3.Normalize(new Vector3(0.35f, 0.75f, 0.55f));
		Raster(px, depth, size, array, t.Tri, (int tri, float l0, float l1, float l2) =>
		{
			int num2 = t.Tri[tri * 3];
			int num3 = t.Tri[tri * 3 + 1];
			int num4 = t.Tri[tri * 3 + 2];
			float x2 = (float)((double)l0 * t.VertexNormal[num2 * 3] + (double)l1 * t.VertexNormal[num3 * 3] + (double)l2 * t.VertexNormal[num4 * 3]);
			float y = (float)((double)l0 * t.VertexNormal[num2 * 3 + 1] + (double)l1 * t.VertexNormal[num3 * 3 + 1] + (double)l2 * t.VertexNormal[num4 * 3 + 1]);
			float z = (float)((double)l0 * t.VertexNormal[num2 * 3 + 2] + (double)l1 * t.VertexNormal[num3 * 3 + 2] + (double)l2 * t.VertexNormal[num4 * 3 + 2]);
			Vector3 value = View(x2, y, z, ca, sa);
			value = ((value.LengthSquared() > 1E-09f) ? Vector3.Normalize(value) : Vector3.UnitZ);
			float num5 = MathF.Max(0f, Vector3.Dot(value, light)) * 0.55f + 0.45f;
			t.ColorAt(tri, l1, l2, out var r, out var g, out var b);
			return (R: r / 255.0 * (double)num5, G: g / 255.0 * (double)num5, B: b / 255.0 * (double)num5);
		});
	}

	public static byte[] RenderParts(IReadOnlyList<FnPreviewPart> parts, float yaw, int size)
	{
		var (array, depth) = Start(size);
		DrawGuides(array, size, yaw);
		float x = yaw * (float)Math.PI / 180f;
		float ca = MathF.Cos(x);
		float sa = MathF.Sin(x);
		Vector3 light = Vector3.Normalize(new Vector3(0.35f, 0.75f, 0.55f));
		foreach (FnPreviewPart part in parts)
		{
			int n = part.Pos.Length / 3;
			Vector3[] pts = new Vector3[n];
			for (int i = 0; i < n; i++)
			{
				pts[i] = View((float)part.Pos[i * 3], (float)part.Pos[i * 3 + 1], (float)part.Pos[i * 3 + 2], ca, sa);
			}
			TexImage tex = part.Texture;
			Raster(array, depth, size, pts, part.Tri, (int tri, float l0, float l1, float l2) =>
			{
				int num = part.Tri[tri * 3];
				int num2 = part.Tri[tri * 3 + 1];
				int num3 = part.Tri[tri * 3 + 2];
				Vector3 vector = pts[num2] - pts[num];
				Vector3 vector2 = pts[num3] - pts[num];
				Vector3 value = Vector3.Cross(vector, vector2);
				value = ((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : Vector3.UnitZ);
				float num4 = MathF.Abs(Vector3.Dot(value, light)) * 0.35f + 0.65f;
				double num5 = 200.0;
				double num6 = 200.0;
				double num7 = 205.0;
				if (tex != null && part.Uv.Length == n * 2)
				{
					double num8 = (double)l0 * part.Uv[num * 2] + (double)l1 * part.Uv[num2 * 2] + (double)l2 * part.Uv[num3 * 2];
					double num9 = (double)l0 * part.Uv[num * 2 + 1] + (double)l1 * part.Uv[num2 * 2 + 1] + (double)l2 * part.Uv[num3 * 2 + 1];
					num8 -= Math.Floor(num8);
					num9 -= Math.Floor(num9);
					int num10 = Math.Min(tex.Width - 1, (int)(num8 * (double)tex.Width));
					int num11 = (Math.Min(tex.Height - 1, (int)(num9 * (double)tex.Height)) * tex.Width + num10) * 4;
					num5 = (int)tex.Rgba[num11];
					num6 = (int)tex.Rgba[num11 + 1];
					num7 = (int)tex.Rgba[num11 + 2];
				}
				return (R: num5 / 255.0 * (double)num4, G: num6 / 255.0 * (double)num4, B: num7 / 255.0 * (double)num4);
			});
		}
		return array;
	}

	private static void DrawOutline(byte[] px, int size, float yaw)
	{
		DrawOutline(px, size, yaw, FnOutline.Pos, FnOutline.Tri, 70, 175, 255, 0.0);
	}

	private static void DrawGround(byte[] px, int size)
	{
		int y = (int)Scr(new Vector3(0f, 0f, 0f), size).Y;
		for (int i = (int)((float)size * 0.08f); (float)i < (float)size * 0.92f; i++)
		{
			Dot(px, size, i, y, 90, 94, 102);
		}
	}

	private static void DrawOutline(byte[] px, int size, float yaw, double[] pos, int[] tri, int cr, int cg, int cb, double dx)
	{
		int num = pos.Length / 3;
		float x = yaw * (float)Math.PI / 180f;
		float ca = MathF.Cos(x);
		float sa = MathF.Sin(x);
		Vector2[] array = new Vector2[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = Scr(View((float)(pos[i * 3] + dx), (float)pos[i * 3 + 1], (float)pos[i * 3 + 2], ca, sa), size);
		}
		bool[] array2 = new bool[size * size];
		for (int j = 0; j + 2 < tri.Length; j += 3)
		{
			Vector2 vector = array[tri[j]];
			Vector2 vector2 = array[tri[j + 1]];
			Vector2 vector3 = array[tri[j + 2]];
			float num2 = (vector2.Y - vector3.Y) * (vector.X - vector3.X) + (vector3.X - vector2.X) * (vector.Y - vector3.Y);
			if (MathF.Abs(num2) < 1E-06f)
			{
				continue;
			}
			int num3 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector.X, MathF.Min(vector2.X, vector3.X))));
			int num4 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector.X, MathF.Max(vector2.X, vector3.X))));
			int num5 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector.Y, MathF.Min(vector2.Y, vector3.Y))));
			int num6 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector.Y, MathF.Max(vector2.Y, vector3.Y))));
			for (int k = num5; k <= num6; k++)
			{
				for (int l = num3; l <= num4; l++)
				{
					float num7 = ((vector2.Y - vector3.Y) * ((float)l + 0.5f - vector3.X) + (vector3.X - vector2.X) * ((float)k + 0.5f - vector3.Y)) / num2;
					float num8 = ((vector3.Y - vector.Y) * ((float)l + 0.5f - vector3.X) + (vector.X - vector3.X) * ((float)k + 0.5f - vector3.Y)) / num2;
					if (!(num7 < -0.02f) && !(num8 < -0.02f) && !(1f - num7 - num8 < -0.02f))
					{
						array2[k * size + l] = true;
					}
				}
			}
		}
		for (int m = 1; m < size - 1; m++)
		{
			for (int n = 1; n < size - 1; n++)
			{
				int num9 = m * size + n;
				if (array2[num9] && (!array2[num9 - 1] || !array2[num9 + 1] || !array2[num9 - size] || !array2[num9 + size]))
				{
					Dot(px, size, n, m, cr, cg, cb);
					if (!array2[num9 + 1])
					{
						Dot(px, size, n + 1, m, cr, cg, cb);
					}
				}
			}
		}
	}

	private static Vector3 View(float x, float y, float z, float ca, float sa)
	{
		return new Vector3(x * ca + y * sa, (0f - x) * sa + y * ca, z);
	}

	private static (byte[] Px, float[] Depth) Start(int size)
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
		float[] array2 = new float[size * size];
		Array.Fill(array2, float.MinValue);
		return (Px: array, Depth: array2);
	}

	private static Vector2 Scr(Vector3 p, int size)
	{
		float num = (float)size * 0.9f / 196f;
		return new Vector2(p.X * num + (float)size / 2f, (float)size * 0.95f - p.Z * num);
	}

	private static void DrawGuides(byte[] px, int size, float yaw)
	{
		(float, byte)[] array = new (float, byte)[3]
		{
			(0f, 90),
			(165.8f, 58),
			(135f, 46)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(float, byte) tuple = array[i];
			float item = tuple.Item1;
			byte item2 = tuple.Item2;
			int y = (int)Scr(new Vector3(0f, 0f, item), size).Y;
			for (int j = (int)((float)size * 0.08f); (float)j < (float)size * 0.92f; j++)
			{
				if (item == 0f || j / 6 % 2 == 0)
				{
					Dot(px, size, j, y, item2, item2 + 4, item2 + 12);
				}
			}
		}
		int x = size / 2;
		for (int k = (int)Scr(new Vector3(0f, 0f, 175f), size).Y; (float)k < (float)size * 0.95f; k++)
		{
			if (k / 6 % 2 == 0)
			{
				Dot(px, size, x, k, 54, 58, 70);
			}
		}
	}

	private static void Dot(byte[] px, int size, int x, int y, int r, int g, int b)
	{
		if (x >= 0 && y >= 0 && x < size && y < size)
		{
			int num = (y * size + x) * 4;
			px[num] = (byte)Math.Min(255, b);
			px[num + 1] = (byte)Math.Min(255, g);
			px[num + 2] = (byte)Math.Min(255, r);
		}
	}

	private static void Raster(byte[] px, float[] depth, int size, Vector3[] pts, int[] tri, Func<int, float, float, float, (double R, double G, double B)> shade)
	{
		int num = tri.Length / 3;
		for (int i = 0; i < num; i++)
		{
			int num2 = tri[i * 3];
			int num3 = tri[i * 3 + 1];
			int num4 = tri[i * 3 + 2];
			Vector2 vector = Scr(pts[num2], size);
			Vector2 vector2 = Scr(pts[num3], size);
			Vector2 vector3 = Scr(pts[num4], size);
			float num5 = (vector2.Y - vector3.Y) * (vector.X - vector3.X) + (vector3.X - vector2.X) * (vector.Y - vector3.Y);
			if (MathF.Abs(num5) < 1E-06f)
			{
				continue;
			}
			int num6 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector.X, MathF.Min(vector2.X, vector3.X))));
			int num7 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector.X, MathF.Max(vector2.X, vector3.X))));
			int num8 = Math.Max(0, (int)MathF.Floor(MathF.Min(vector.Y, MathF.Min(vector2.Y, vector3.Y))));
			int num9 = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(vector.Y, MathF.Max(vector2.Y, vector3.Y))));
			for (int j = num8; j <= num9; j++)
			{
				for (int k = num6; k <= num7; k++)
				{
					float num10 = ((vector2.Y - vector3.Y) * ((float)k + 0.5f - vector3.X) + (vector3.X - vector2.X) * ((float)j + 0.5f - vector3.Y)) / num5;
					float num11 = ((vector3.Y - vector.Y) * ((float)k + 0.5f - vector3.X) + (vector.X - vector3.X) * ((float)j + 0.5f - vector3.Y)) / num5;
					float num12 = 1f - num10 - num11;
					if (!(num10 < -0.0001f) && !(num11 < -0.0001f) && !(num12 < -0.0001f))
					{
						float num13 = num10 * pts[num2].Y + num11 * pts[num3].Y + num12 * pts[num4].Y;
						int num14 = j * size + k;
						if (!(num13 <= depth[num14]))
						{
							depth[num14] = num13;
							var (num15, num16, num17) = shade(i, num10, num11, num12);
							px[num14 * 4] = (byte)Math.Clamp(num17 * 255.0, 0.0, 255.0);
							px[num14 * 4 + 1] = (byte)Math.Clamp(num16 * 255.0, 0.0, 255.0);
							px[num14 * 4 + 2] = (byte)Math.Clamp(num15 * 255.0, 0.0, 255.0);
							px[num14 * 4 + 3] = byte.MaxValue;
						}
					}
				}
			}
		}
	}
}
