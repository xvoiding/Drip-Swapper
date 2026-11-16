using System;
using System.Text;

namespace Drip.Core.Mods;

public static class PakTexture
{
	private const uint InlinePayload = 64u;

	public static IconPixels? TryDecode(byte[] uexp, int maxSize = 128)
	{
		try
		{
			int num = Find(uexp, "PF_");
			if (num < 16)
			{
				return null;
			}
			int num2 = BitConverter.ToInt32(uexp, num - 16);
			int num3 = BitConverter.ToInt32(uexp, num - 12);
			int num4 = BitConverter.ToInt32(uexp, num - 4);
			if (num2 < 4 || num3 < 4 || num2 > 8192 || num3 > 8192 || num4 < 4 || num4 > 40)
			{
				return null;
			}
			string text = Encoding.ASCII.GetString(uexp, num, num4 - 1);
			int num5 = num + num4;
			if (num5 + 32 > uexp.Length)
			{
				return null;
			}
			uint num6 = BitConverter.ToUInt32(uexp, num5 + 12);
			int num7 = BitConverter.ToInt32(uexp, num5 + 16);
			if ((num6 & 0x40) == 0 || num7 <= 0 || num5 + 32 + num7 > uexp.Length)
			{
				return null;
			}
			int num8 = num5 + 32;
			byte[] array;
			switch (text)
			{
			case "PF_DXT5":
				if (num7 >= BlockBytes(num2, num3, 16))
				{
					array = DecodeBc(uexp, num8, num2, num3, alphaBlock: true);
					break;
				}
				goto default;
			case "PF_DXT1":
				if (num7 >= BlockBytes(num2, num3, 8))
				{
					array = DecodeBc(uexp, num8, num2, num3, alphaBlock: false);
					break;
				}
				goto default;
			case "PF_B8G8R8A8":
				if (num7 >= num2 * num3 * 4)
				{
					array = Slice(uexp, num8, num2 * num3 * 4);
					break;
				}
				goto default;
			default:
				array = null;
				break;
			}
			byte[] array2 = array;
			return (array2 == null) ? null : Downscale(new IconPixels(num2, num3, array2), maxSize);
		}
		catch
		{
			return null;
		}
	}

	private static int BlockBytes(int w, int h, int per)
	{
		return (w + 3) / 4 * ((h + 3) / 4) * per;
	}

	private static byte[] Slice(byte[] b, int s, int n)
	{
		byte[] array = new byte[n];
		Buffer.BlockCopy(b, s, array, 0, n);
		return array;
	}

	private static int Find(byte[] hay, string needle)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(needle);
		for (int i = 0; i <= hay.Length - bytes.Length; i++)
		{
			int j;
			for (j = 0; j < bytes.Length && hay[i + j] == bytes[j]; j++)
			{
			}
			if (j == bytes.Length)
			{
				return i;
			}
		}
		return -1;
	}

	private static void Rgb565(int c, out int r, out int g, out int b)
	{
		r = ((c >> 11) & 0x1F) * 255 / 31;
		g = ((c >> 5) & 0x3F) * 255 / 63;
		b = (c & 0x1F) * 255 / 31;
	}

	public static byte[] DecodeBc(byte[] src, int start, int w, int h, bool alphaBlock)
	{
		byte[] array = new byte[w * h * 4];
		int num = (w + 3) / 4;
		int num2 = (h + 3) / 4;
		int num3 = start;
		Span<int> span = stackalloc int[16];
		Span<int> span2 = stackalloc int[8];
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				if (alphaBlock)
				{
					int num4 = src[num3];
					int num5 = src[num3 + 1];
					span2[0] = num4;
					span2[1] = num5;
					if (num4 > num5)
					{
						for (int k = 1; k <= 6; k++)
						{
							span2[k + 1] = ((7 - k) * num4 + k * num5) / 7;
						}
					}
					else
					{
						for (int l = 1; l <= 4; l++)
						{
							span2[l + 1] = ((5 - l) * num4 + l * num5) / 5;
						}
						span2[6] = 0;
						span2[7] = 255;
					}
				}
				int num6 = (alphaBlock ? (num3 + 8) : num3);
				int num7 = src[num6] | (src[num6 + 1] << 8);
				int num8 = src[num6 + 2] | (src[num6 + 3] << 8);
				Rgb565(num7, out var r, out var g, out var b);
				Rgb565(num8, out var r2, out var g2, out var b2);
				span[0] = (r << 16) | (g << 8) | b;
				span[1] = (r2 << 16) | (g2 << 8) | b2;
				bool flag = alphaBlock || num7 > num8;
				if (flag)
				{
					span[2] = ((2 * r + r2) / 3 << 16) | ((2 * g + g2) / 3 << 8) | ((2 * b + b2) / 3);
					span[3] = ((r + 2 * r2) / 3 << 16) | ((g + 2 * g2) / 3 << 8) | ((b + 2 * b2) / 3);
				}
				else
				{
					span[2] = ((r + r2) / 2 << 16) | ((g + g2) / 2 << 8) | ((b + b2) / 2);
					span[3] = 0;
				}
				uint num9 = (uint)(src[num6 + 4] | (src[num6 + 5] << 8) | (src[num6 + 6] << 16) | (src[num6 + 7] << 24));
				ulong num10 = 0uL;
				if (alphaBlock)
				{
					for (int m = 0; m < 6; m++)
					{
						num10 |= (ulong)src[num3 + 2 + m] << 8 * m;
					}
				}
				for (int n = 0; n < 4; n++)
				{
					for (int num11 = 0; num11 < 4; num11++)
					{
						int num12 = j * 4 + num11;
						int num13 = i * 4 + n;
						if (num12 < w && num13 < h)
						{
							int num14 = n * 4 + num11;
							int num15 = (int)((num9 >> 2 * num14) & 3);
							int num16 = span[num15];
							int num17 = (alphaBlock ? span2[(int)((num10 >> 3 * num14) & 7)] : ((flag || num15 != 3) ? 255 : 0));
							int num18 = (num13 * w + num12) * 4;
							array[num18] = (byte)(num16 & 0xFF);
							array[num18 + 1] = (byte)((num16 >> 8) & 0xFF);
							array[num18 + 2] = (byte)((num16 >> 16) & 0xFF);
							array[num18 + 3] = (byte)num17;
						}
					}
				}
				num3 += (alphaBlock ? 16 : 8);
			}
		}
		return array;
	}

	public static IconPixels Downscale(IconPixels s, int max)
	{
		int num = Math.Max(1, (Math.Max(s.Width, s.Height) + max - 1) / max);
		if (num == 1)
		{
			return s;
		}
		int num2 = s.Width / num;
		int num3 = s.Height / num;
		byte[] array = new byte[num2 * num3 * 4];
		for (int i = 0; i < num3; i++)
		{
			for (int j = 0; j < num2; j++)
			{
				long num4 = 0L;
				long num5 = 0L;
				long num6 = 0L;
				long num7 = 0L;
				for (int k = 0; k < num; k++)
				{
					for (int l = 0; l < num; l++)
					{
						int num8 = ((i * num + k) * s.Width + j * num + l) * 4;
						int num9 = s.Bgra[num8 + 3];
						num4 += s.Bgra[num8] * num9;
						num5 += s.Bgra[num8 + 1] * num9;
						num6 += s.Bgra[num8 + 2] * num9;
						num7 += num9;
					}
				}
				int num10 = (i * num2 + j) * 4;
				int num11 = num * num;
				if (num7 > 0)
				{
					array[num10] = (byte)(num4 / num7);
					array[num10 + 1] = (byte)(num5 / num7);
					array[num10 + 2] = (byte)(num6 / num7);
				}
				array[num10 + 3] = (byte)(num7 / num11);
			}
		}
		return new IconPixels(num2, num3, array);
	}
}
