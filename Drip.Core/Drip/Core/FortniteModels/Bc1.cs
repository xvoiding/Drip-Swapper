using System;
using System.Collections.Generic;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class Bc1
{
	public sealed record Mip(int Width, int Height, byte[] Data);

	public static List<Mip> EncodeChain(TexImage img)
	{
		List<Mip> list = new List<Mip>();
		int num = img.Width;
		int num2 = img.Height;
		float[] array = new float[num * num2 * 3];
		for (int i = 0; i < num * num2; i++)
		{
			array[i * 3] = (int)img.Rgba[i * 4];
			array[i * 3 + 1] = (int)img.Rgba[i * 4 + 1];
			array[i * 3 + 2] = (int)img.Rgba[i * 4 + 2];
		}
		while (true)
		{
			list.Add(new Mip(num, num2, EncodeLevel(array, num, num2)));
			if (num == 1 && num2 == 1)
			{
				break;
			}
			int num3 = Math.Max(1, num / 2);
			int num4 = Math.Max(1, num2 / 2);
			float[] array2 = new float[num3 * num4 * 3];
			for (int j = 0; j < num4; j++)
			{
				for (int k = 0; k < num3; k++)
				{
					for (int l = 0; l < 3; l++)
					{
						float num5 = 0f;
						int num6 = 0;
						for (int m = 0; m < 2; m++)
						{
							for (int n = 0; n < 2; n++)
							{
								int num7 = Math.Min(num - 1, k * 2 + n);
								int num8 = Math.Min(num2 - 1, j * 2 + m);
								if ((num != 1 || n != 1) && (num2 != 1 || m != 1))
								{
									num5 += array[(num8 * num + num7) * 3 + l];
									num6++;
								}
							}
						}
						array2[(j * num3 + k) * 3 + l] = num5 / (float)Math.Max(1, num6);
					}
				}
			}
			array = array2;
			num = num3;
			num2 = num4;
		}
		return list;
	}

	private static byte[] EncodeLevel(float[] px, int w, int h)
	{
		int num = Math.Max(1, (w + 3) / 4);
		int num2 = Math.Max(1, (h + 3) / 4);
		byte[] array = new byte[num * num2 * 8];
		float[] array2 = new float[48];
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				for (int k = 0; k < 4; k++)
				{
					for (int l = 0; l < 4; l++)
					{
						int num3 = Math.Min(w - 1, j * 4 + l);
						int num4 = (Math.Min(h - 1, i * 4 + k) * w + num3) * 3;
						int num5 = (k * 4 + l) * 3;
						array2[num5] = px[num4];
						array2[num5 + 1] = px[num4 + 1];
						array2[num5 + 2] = px[num4 + 2];
					}
				}
				EncodeBlock(array2, array, (i * num + j) * 8);
			}
		}
		return array;
	}

	private static int Pack565(float r, float g, float b)
	{
		int num = Math.Clamp((int)Math.Round(r * 31f / 255f), 0, 31);
		int num2 = Math.Clamp((int)Math.Round(g * 63f / 255f), 0, 63);
		int num3 = Math.Clamp((int)Math.Round(b * 31f / 255f), 0, 31);
		return (num << 11) | (num2 << 5) | num3;
	}

	private static void Unpack565(int c, out float r, out float g, out float b)
	{
		int num = (c >> 11) & 0x1F;
		int num2 = (c >> 5) & 0x3F;
		int num3 = c & 0x1F;
		r = (num << 3) | (num >> 2);
		g = (num2 << 2) | (num2 >> 4);
		b = (num3 << 3) | (num3 >> 2);
	}

	private static void EncodeBlock(float[] p, byte[] dst, int at)
	{
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		for (int i = 0; i < 16; i++)
		{
			num += p[i * 3];
			num2 += p[i * 3 + 1];
			num3 += p[i * 3 + 2];
		}
		num /= 16f;
		num2 /= 16f;
		num3 /= 16f;
		float num4 = 0f;
		float num5 = 0f;
		float num6 = 0f;
		float num7 = 0f;
		float num8 = 0f;
		float num9 = 0f;
		for (int j = 0; j < 16; j++)
		{
			float num10 = p[j * 3] - num;
			float num11 = p[j * 3 + 1] - num2;
			float num12 = p[j * 3 + 2] - num3;
			num4 += num10 * num10;
			num5 += num10 * num11;
			num6 += num10 * num12;
			num7 += num11 * num11;
			num8 += num11 * num12;
			num9 += num12 * num12;
		}
		float num13 = 1f;
		float num14 = 1f;
		float num15 = 1f;
		for (int k = 0; k < 6; k++)
		{
			float num16 = num4 * num13 + num5 * num14 + num6 * num15;
			float num17 = num5 * num13 + num7 * num14 + num8 * num15;
			float num18 = num6 * num13 + num8 * num14 + num9 * num15;
			float num19 = MathF.Sqrt(num16 * num16 + num17 * num17 + num18 * num18);
			if (num19 < 1E-06f)
			{
				break;
			}
			num13 = num16 / num19;
			num14 = num17 / num19;
			num15 = num18 / num19;
		}
		float num20 = float.MaxValue;
		float num21 = float.MinValue;
		int num22 = 0;
		int num23 = 0;
		for (int l = 0; l < 16; l++)
		{
			float num24 = (p[l * 3] - num) * num13 + (p[l * 3 + 1] - num2) * num14 + (p[l * 3 + 2] - num3) * num15;
			if (num24 < num20)
			{
				num20 = num24;
				num22 = l;
			}
			if (num24 > num21)
			{
				num21 = num24;
				num23 = l;
			}
		}
		int num25 = Pack565(p[num23 * 3], p[num23 * 3 + 1], p[num23 * 3 + 2]);
		int num26 = Pack565(p[num22 * 3], p[num22 * 3 + 1], p[num22 * 3 + 2]);
		Assign(p, num25, num26, out float err, out int[] idx);
		if (err > 0f && Refine(p, idx, out var c, out var c2))
		{
			Assign(p, c, c2, out float err2, out int[] idx2);
			if (err2 < err)
			{
				err = err2;
				idx = idx2;
				num25 = c;
				num26 = c2;
			}
		}
		if (num25 == num26)
		{
			dst[at] = (byte)num25;
			dst[at + 1] = (byte)(num25 >> 8);
			dst[at + 2] = (byte)num25;
			dst[at + 3] = (byte)(num25 >> 8);
			dst[at + 4] = (dst[at + 5] = (dst[at + 6] = (dst[at + 7] = 0)));
			return;
		}
		int[] array = idx;
		if (num25 < num26)
		{
			int num27 = num26;
			num26 = num25;
			num25 = num27;
			for (int m = 0; m < 16; m++)
			{
				int[] array2 = array;
				int num28 = m;
				array2[num28] = array[m] switch
				{
					0 => 1, 
					1 => 0, 
					2 => 3, 
					_ => 2, 
				};
			}
		}
		dst[at] = (byte)num25;
		dst[at + 1] = (byte)(num25 >> 8);
		dst[at + 2] = (byte)num26;
		dst[at + 3] = (byte)(num26 >> 8);
		uint num29 = 0u;
		for (int n = 0; n < 16; n++)
		{
			num29 |= (uint)(array[n] << 2 * n);
		}
		dst[at + 4] = (byte)num29;
		dst[at + 5] = (byte)(num29 >> 8);
		dst[at + 6] = (byte)(num29 >> 16);
		dst[at + 7] = (byte)(num29 >> 24);
	}

	private static int[] Assign(float[] p, int c0, int c1, out float err, out int[] idx)
	{
		Unpack565(c0, out var r, out var g, out var b);
		Unpack565(c1, out var r2, out var g2, out var b2);
		float[] array = new float[4]
		{
			r,
			r2,
			(2f * r + r2) / 3f,
			(r + 2f * r2) / 3f
		};
		float[] array2 = new float[4]
		{
			g,
			g2,
			(2f * g + g2) / 3f,
			(g + 2f * g2) / 3f
		};
		float[] array3 = new float[4]
		{
			b,
			b2,
			(2f * b + b2) / 3f,
			(b + 2f * b2) / 3f
		};
		idx = new int[16];
		err = 0f;
		for (int i = 0; i < 16; i++)
		{
			float num = float.MaxValue;
			int num2 = 0;
			for (int j = 0; j < 4; j++)
			{
				float num3 = p[i * 3] - array[j];
				float num4 = p[i * 3 + 1] - array2[j];
				float num5 = p[i * 3 + 2] - array3[j];
				float num6 = num3 * num3 + num4 * num4 + num5 * num5;
				if (num6 < num)
				{
					num = num6;
					num2 = j;
				}
			}
			idx[i] = num2;
			err += num;
		}
		return idx;
	}

	private static bool Refine(float[] p, int[] idx, out int c0, out int c1)
	{
		c0 = (c1 = 0);
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double[] array = new double[3];
		double[] array2 = new double[3];
		for (int i = 0; i < 16; i++)
		{
			double num4 = idx[i] switch
			{
				0 => 1.0, 
				1 => 0.0, 
				2 => 2.0 / 3.0, 
				_ => 1.0 / 3.0, 
			};
			double num5 = 1.0 - num4;
			num += num4 * num4;
			num2 += num4 * num5;
			num3 += num5 * num5;
			for (int j = 0; j < 3; j++)
			{
				array[j] += num4 * (double)p[i * 3 + j];
				array2[j] += num5 * (double)p[i * 3 + j];
			}
		}
		double num6 = num * num3 - num2 * num2;
		if (Math.Abs(num6) < 1E-06)
		{
			return false;
		}
		float[] array3 = new float[3];
		float[] array4 = new float[3];
		for (int k = 0; k < 3; k++)
		{
			array3[k] = (float)((array[k] * num3 - array2[k] * num2) / num6);
			array4[k] = (float)((array2[k] * num - array[k] * num2) / num6);
		}
		c0 = Pack565(array3[0], array3[1], array3[2]);
		c1 = Pack565(array4[0], array4[1], array4[2]);
		return true;
	}

	public static byte[] Decode(byte[] data, int w, int h)
	{
		int num = Math.Max(1, (w + 3) / 4);
		byte[] array = new byte[w * h * 4];
		for (int i = 0; i < h; i++)
		{
			for (int j = 0; j < w; j++)
			{
				int num2 = (i / 4 * num + j / 4) * 8;
				int num3 = data[num2] | (data[num2 + 1] << 8);
				int num4 = data[num2 + 2] | (data[num2 + 3] << 8);
				Unpack565(num3, out var r, out var g, out var b);
				Unpack565(num4, out var r2, out var g2, out var b2);
				int num5 = ((data[num2 + 4] | (data[num2 + 5] << 8) | (data[num2 + 6] << 16) | (data[num2 + 7] << 24)) >>> 2 * (i % 4 * 4 + j % 4)) & 3;
				float num6;
				float num7;
				float num8;
				if (num3 > num4)
				{
					(num6, num7, num8) = num5 switch
					{
						0 => (r, g, b), 
						1 => (r2, g2, b2), 
						2 => ((2f * r + r2) / 3f, (2f * g + g2) / 3f, (2f * b + b2) / 3f), 
						_ => ((r + 2f * r2) / 3f, (g + 2f * g2) / 3f, (b + 2f * b2) / 3f), 
					};
				}
				else
				{
					(num6, num7, num8) = num5 switch
					{
						0 => (r, g, b), 
						1 => (r2, g2, b2), 
						2 => ((r + r2) / 2f, (g + g2) / 2f, (b + b2) / 2f), 
						_ => (0f, 0f, 0f), 
					};
				}
				int num9 = (i * w + j) * 4;
				array[num9] = (byte)Math.Round(num6);
				array[num9 + 1] = (byte)Math.Round(num7);
				array[num9 + 2] = (byte)Math.Round(num8);
				array[num9 + 3] = byte.MaxValue;
			}
		}
		return array;
	}
}
