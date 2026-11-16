using System;
using System.IO;
using StbImageSharp;
using StbImageWriteSharp;

namespace Drip.Core.RocketLeague.Custom;

public sealed class TexImage
{
	public int Width;

	public int Height;

	public byte[] Rgba = Array.Empty<byte>();

	public bool HasAlpha
	{
		get
		{
			for (int i = 3; i < Rgba.Length; i += 4)
			{
				if (Rgba[i] < 250)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static TexImage Load(byte[] file)
	{
		try
		{
			ImageResult imageResult = ImageResult.FromMemory(file, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
			return new TexImage
			{
				Width = imageResult.Width,
				Height = imageResult.Height,
				Rgba = imageResult.Data
			};
		}
		catch (Exception ex)
		{
			throw new UpkException("Couldn't read that picture (PNG, JPG, BMP and TGA work): " + ex.Message);
		}
	}

	public byte[] ToPng()
	{
		using MemoryStream memoryStream = new MemoryStream();
		new ImageWriter().WritePng(Rgba, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, memoryStream);
		return memoryStream.ToArray();
	}

	private static int Pow2Floor(int n)
	{
		int num = 1;
		while (num * 2 <= n)
		{
			num *= 2;
		}
		return num;
	}

	public TexImage ForGame(int max = 1024, int min = 256)
	{
		int num = Pow2Floor(Math.Max(1, Width));
		int num2 = Pow2Floor(Math.Max(1, Height));
		if (Width - num > num / 2)
		{
			num = Math.Min(num * 2, max);
		}
		if (Height - num2 > num2 / 2)
		{
			num2 = Math.Min(num2 * 2, max);
		}
		bool flag = Width <= 128 && Height <= 128;
		while (flag && num < min && num2 < min)
		{
			num *= 2;
			num2 *= 2;
		}
		num = Math.Clamp(num, 4, max);
		num2 = Math.Clamp(num2, 4, max);
		if (!flag)
		{
			return Resample(num, num2);
		}
		return Nearest(num, num2);
	}

	public TexImage Nearest(int w, int h)
	{
		byte[] array = new byte[w * h * 4];
		for (int i = 0; i < h; i++)
		{
			for (int j = 0; j < w; j++)
			{
				int num = Math.Min(Width - 1, (int)((long)j * (long)Width / w));
				int num2 = Math.Min(Height - 1, (int)((long)i * (long)Height / h));
				Array.Copy(Rgba, (num2 * Width + num) * 4, array, (i * w + j) * 4, 4);
			}
		}
		return new TexImage
		{
			Width = w,
			Height = h,
			Rgba = array
		};
	}

	public TexImage Resample(int w, int h)
	{
		if (w == Width && h == Height)
		{
			return this;
		}
		byte[] array = new byte[w * h * 4];
		for (int i = 0; i < h; i++)
		{
			double num = (double)i * (double)Height / (double)h;
			double num2 = (double)(i + 1) * (double)Height / (double)h;
			for (int j = 0; j < w; j++)
			{
				double num3 = (double)j * (double)Width / (double)w;
				double num4 = (double)(j + 1) * (double)Width / (double)w;
				double num5 = 0.0;
				double num6 = 0.0;
				double num7 = 0.0;
				double num8 = 0.0;
				double num9 = 0.0;
				for (int k = (int)num; k < Math.Min(Height, (int)Math.Ceiling(num2)); k++)
				{
					double num10 = Math.Min(k + 1, num2) - Math.Max(k, num);
					for (int l = (int)num3; l < Math.Min(Width, (int)Math.Ceiling(num4)); l++)
					{
						double num11 = (Math.Min(l + 1, num4) - Math.Max(l, num3)) * num10;
						int num12 = (k * Width + l) * 4;
						num5 += (double)(int)Rgba[num12] * num11;
						num6 += (double)(int)Rgba[num12 + 1] * num11;
						num7 += (double)(int)Rgba[num12 + 2] * num11;
						num8 += (double)(int)Rgba[num12 + 3] * num11;
						num9 += num11;
					}
				}
				int num13 = (i * w + j) * 4;
				if (num9 <= 0.0)
				{
					num9 = 1.0;
				}
				array[num13] = (byte)(num5 / num9 + 0.5);
				array[num13 + 1] = (byte)(num6 / num9 + 0.5);
				array[num13 + 2] = (byte)(num7 / num9 + 0.5);
				array[num13 + 3] = (byte)(num8 / num9 + 0.5);
			}
		}
		return new TexImage
		{
			Width = w,
			Height = h,
			Rgba = array
		};
	}

	public TexImage Half()
	{
		return Resample(Math.Max(1, Width / 2), Math.Max(1, Height / 2));
	}
}
