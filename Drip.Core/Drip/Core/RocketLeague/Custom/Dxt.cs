using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Drip.Core.RocketLeague.Custom;

public static class Dxt
{
	private static int To565(int r, int g, int b)
	{
		return ((r * 31 + 127) / 255 << 11) | ((g * 63 + 127) / 255 << 5) | ((b * 31 + 127) / 255);
	}

	private static (int r, int g, int b) From565(int c)
	{
		return (r: ((c >> 11) & 0x1F) * 255 / 31, g: ((c >> 5) & 0x3F) * 255 / 63, b: (c & 0x1F) * 255 / 31);
	}

	private static void ColorBlock(byte[] px, Span<byte> o)
	{
		int num = 255;
		int num2 = 255;
		int num3 = 255;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		for (int i = 0; i < 16; i++)
		{
			int val = px[i * 4];
			int val2 = px[i * 4 + 1];
			int val3 = px[i * 4 + 2];
			num = Math.Min(num, val);
			num2 = Math.Min(num2, val2);
			num3 = Math.Min(num3, val3);
			num4 = Math.Max(num4, val);
			num5 = Math.Max(num5, val2);
			num6 = Math.Max(num6, val3);
		}
		int num7 = (num4 - num) / 16;
		int num8 = (num5 - num2) / 16;
		int num9 = (num6 - num3) / 16;
		int num10 = To565(num4 - num7, num5 - num8, num6 - num9);
		int num11 = To565(num + num7, num2 + num8, num3 + num9);
		if (num10 < num11)
		{
			int num12 = num11;
			num11 = num10;
			num10 = num12;
		}
		uint num13 = 0u;
		if (num10 == num11)
		{
			WriteColor(o, num10, num11, 0u);
			return;
		}
		(int, int, int) tuple = From565(num10);
		(int, int, int) tuple2 = From565(num11);
		(int, int, int)[] array = new (int, int, int)[4]
		{
			tuple,
			tuple2,
			((2 * tuple.Item1 + tuple2.Item1) / 3, (2 * tuple.Item2 + tuple2.Item2) / 3, (2 * tuple.Item3 + tuple2.Item3) / 3),
			((tuple.Item1 + 2 * tuple2.Item1) / 3, (tuple.Item2 + 2 * tuple2.Item2) / 3, (tuple.Item3 + 2 * tuple2.Item3) / 3)
		};
		for (int j = 0; j < 16; j++)
		{
			int num14 = px[j * 4];
			int num15 = px[j * 4 + 1];
			int num16 = px[j * 4 + 2];
			int num17 = 0;
			int num18 = int.MaxValue;
			for (int k = 0; k < 4; k++)
			{
				int num19 = num14 - array[k].Item1;
				int num20 = num15 - array[k].Item2;
				int num21 = num16 - array[k].Item3;
				int num22 = num19 * num19 * 3 + num20 * num20 * 4 + num21 * num21 * 2;
				if (num22 < num18)
				{
					num18 = num22;
					num17 = k;
				}
			}
			num13 |= (uint)(num17 << j * 2);
		}
		WriteColor(o, num10, num11, num13);
	}

	private static void WriteColor(Span<byte> o, int c0, int c1, uint bits)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(o, (ushort)c0);
		BinaryPrimitives.WriteUInt16LittleEndian(o.Slice(2), (ushort)c1);
		BinaryPrimitives.WriteUInt32LittleEndian(o.Slice(4), bits);
	}

	private static void AlphaBlock(byte[] px, Span<byte> o)
	{
		int num = 255;
		int num2 = 0;
		for (int i = 0; i < 16; i++)
		{
			int val = px[i * 4 + 3];
			num = Math.Min(num, val);
			num2 = Math.Max(num2, val);
		}
		o[0] = (byte)num2;
		o[1] = (byte)num;
		ulong num3 = 0uL;
		if (num2 != num)
		{
			int[] array = new int[8] { num2, num, 0, 0, 0, 0, 0, 0 };
			for (int j = 1; j <= 6; j++)
			{
				array[j + 1] = ((7 - j) * num2 + j * num) / 7;
			}
			for (int k = 0; k < 16; k++)
			{
				int num4 = px[k * 4 + 3];
				int num5 = 0;
				int num6 = int.MaxValue;
				for (int l = 0; l < 8; l++)
				{
					int num7 = Math.Abs(num4 - array[l]);
					if (num7 < num6)
					{
						num6 = num7;
						num5 = l;
					}
				}
				num3 |= (ulong)((long)num5 << k * 3);
			}
		}
		for (int m = 0; m < 6; m++)
		{
			o[2 + m] = (byte)(num3 >> 8 * m);
		}
	}

	public static int BytesFor(TexFormat f, int w, int h)
	{
		return f switch
		{
			TexFormat.Bgra8 => w * h * 4, 
			TexFormat.Dxt1 => Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * 8, 
			_ => Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * 16, 
		};
	}

	public static byte[] Encode(TexImage img, TexFormat f)
	{
		int width = img.Width;
		int height = img.Height;
		if (f == TexFormat.Bgra8)
		{
			byte[] array = new byte[width * height * 4];
			for (int i = 0; i < width * height; i++)
			{
				array[i * 4] = img.Rgba[i * 4 + 2];
				array[i * 4 + 1] = img.Rgba[i * 4 + 1];
				array[i * 4 + 2] = img.Rgba[i * 4];
				array[i * 4 + 3] = img.Rgba[i * 4 + 3];
			}
			return array;
		}
		int num = Math.Max(1, (width + 3) / 4);
		int num2 = Math.Max(1, (height + 3) / 4);
		int num3 = ((f == TexFormat.Dxt1) ? 8 : 16);
		byte[] array2 = new byte[num * num2 * num3];
		byte[] array3 = new byte[64];
		for (int j = 0; j < num2; j++)
		{
			for (int k = 0; k < num; k++)
			{
				for (int l = 0; l < 4; l++)
				{
					for (int m = 0; m < 4; m++)
					{
						int num4 = Math.Min(width - 1, k * 4 + m);
						int num5 = Math.Min(height - 1, j * 4 + l);
						Array.Copy(img.Rgba, (num5 * width + num4) * 4, array3, (l * 4 + m) * 4, 4);
					}
				}
				Span<byte> o = array2.AsSpan((j * num + k) * num3, num3);
				if (f == TexFormat.Dxt1)
				{
					ColorBlock(array3, o);
					continue;
				}
				AlphaBlock(array3, o.Slice(0, 8));
				ColorBlock(array3, o.Slice(8));
			}
		}
		return array2;
	}

	public static List<(int W, int H, byte[] Data)> Chain(TexImage top, TexFormat f)
	{
		List<(int, int, byte[])> list = new List<(int, int, byte[])>();
		TexImage texImage = top;
		while (true)
		{
			list.Add((texImage.Width, texImage.Height, Encode(texImage, f)));
			if (texImage.Width == 1 && texImage.Height == 1)
			{
				break;
			}
			texImage = texImage.Half();
		}
		return list;
	}
}
