using System;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;

namespace Drip.Core.FallGuys;

public static class FgTexIo
{
	public static void WriteRgba(AssetsManager am, AssetsFileInstance afi, AssetFileInfo info, int w, int h, byte[] rgba, bool mips)
	{
		AssetTypeValueField baseField = am.GetBaseField(afi, info);
		TextureFile textureFile = TextureFile.ReadTextureFile(baseField);
		List<byte[]> list = new List<byte[]>();
		int num = w;
		int num2 = h;
		byte[] array = rgba;
		list.Add(array);
		while (mips && (num > 1 || num2 > 1))
		{
			int num3 = Math.Max(1, num / 2);
			int num4 = Math.Max(1, num2 / 2);
			array = Downsample(array, num, num2, num3, num4);
			list.Add(array);
			num = num3;
			num2 = num4;
		}
		List<byte> list2 = new List<byte>();
		num = w;
		num2 = h;
		foreach (byte[] item in list)
		{
			list2.AddRange(FlipRows(item, num, num2));
			num = Math.Max(1, num / 2);
			num2 = Math.Max(1, num2 / 2);
		}
		textureFile.m_TextureFormat = 4;
		textureFile.m_Width = w;
		textureFile.m_Height = h;
		textureFile.m_MipCount = list.Count;
		textureFile.m_CompleteImageSize = list2.Count;
		textureFile.pictureData = list2.ToArray();
		textureFile.m_StreamData.path = "";
		textureFile.m_StreamData.offset = 0uL;
		textureFile.m_StreamData.size = 0u;
		textureFile.WriteTo(baseField);
		info.SetNewData(baseField);
	}

	private static byte[] FlipRows(byte[] px, int w, int h)
	{
		byte[] array = new byte[px.Length];
		int num = w * 4;
		for (int i = 0; i < h; i++)
		{
			Buffer.BlockCopy(px, i * num, array, (h - 1 - i) * num, num);
		}
		return array;
	}

	private static byte[] Downsample(byte[] src, int w, int h, int nw, int nh)
	{
		byte[] array = new byte[nw * nh * 4];
		for (int i = 0; i < nh; i++)
		{
			for (int j = 0; j < nw; j++)
			{
				int num = Math.Min(w - 1, j * 2);
				int num2 = Math.Min(w - 1, j * 2 + 1);
				int num3 = Math.Min(h - 1, i * 2);
				int num4 = Math.Min(h - 1, i * 2 + 1);
				for (int k = 0; k < 4; k++)
				{
					int num5 = src[(num3 * w + num) * 4 + k] + src[(num3 * w + num2) * 4 + k] + src[(num4 * w + num) * 4 + k] + src[(num4 * w + num2) * 4 + k];
					array[(i * nw + j) * 4 + k] = (byte)((num5 + 2) / 4);
				}
			}
		}
		return array;
	}

	public static void WriteFlat(AssetsManager am, AssetsFileInstance afi, AssetFileInfo info, byte r, byte g, byte b, byte a)
	{
		byte[] array = new byte[64];
		for (int i = 0; i < 16; i++)
		{
			array[i * 4] = r;
			array[i * 4 + 1] = g;
			array[i * 4 + 2] = b;
			array[i * 4 + 3] = a;
		}
		WriteRgba(am, afi, info, 4, 4, array, mips: false);
	}
}
