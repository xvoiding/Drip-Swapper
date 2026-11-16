using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Drip.Core.RocketLeague.Custom;

public static class MaterialTools
{
	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	public static Dictionary<string, int> TextureParams(byte[] d, List<NameEntry> names)
	{
		return TextureParams(d, names, null);
	}

	public static Dictionary<string, int> TextureParams(byte[] d, List<NameEntry> names, Dictionary<string, int>? offsets)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		int num = 4;
		for (int i = 0; i < 256; i++)
		{
			if (num + 8 > d.Length)
			{
				return dictionary;
			}
			string text = N(I(d, num));
			num += 8;
			if (text == "None")
			{
				return dictionary;
			}
			if (num + 16 > d.Length)
			{
				return dictionary;
			}
			string text2 = N(I(d, num));
			num += 8;
			int num2 = I(d, num);
			num += 8;
			if ((text2 == "StructProperty" || text2 == "ByteProperty") ? true : false)
			{
				num += 8;
			}
			else if (text2 == "BoolProperty")
			{
				num++;
			}
			if (num2 < 0 || num + num2 > d.Length)
			{
				return dictionary;
			}
			if (text == "TextureParameterValues" && text2 == "ArrayProperty")
			{
				ReadTextureArray(d, names, num, num + num2, dictionary, offsets);
			}
			num += num2;
		}
		return dictionary;
		string N(int num3)
		{
			if (num3 < 0 || num3 >= names.Count)
			{
				return "?";
			}
			return names[num3].Text;
		}
	}

	private static void ReadTextureArray(byte[] d, List<NameEntry> names, int p, int end, Dictionary<string, int> into, Dictionary<string, int>? offsets)
	{
		int num = I(d, p);
		p += 4;
		for (int i = 0; i < num; i++)
		{
			if (p >= end)
			{
				break;
			}
			string text = null;
			int value = 0;
			int value2 = -1;
			for (int j = 0; j < 16; j++)
			{
				if (p + 8 > end)
				{
					break;
				}
				string text2 = N(I(d, p));
				p += 8;
				if (text2 == "None")
				{
					break;
				}
				string text3 = N(I(d, p));
				p += 8;
				int num2 = I(d, p);
				p += 8;
				if ((text3 == "StructProperty" || text3 == "ByteProperty") ? true : false)
				{
					p += 8;
				}
				else if (text3 == "BoolProperty")
				{
					p++;
				}
				if (num2 < 0 || p + num2 > end)
				{
					return;
				}
				if (text2 == "ParameterName" && num2 >= 4)
				{
					text = N(I(d, p));
				}
				else if (text2 == "ParameterValue" && num2 >= 4 && text3 == "ObjectProperty")
				{
					value = I(d, p);
					value2 = p;
				}
				p += num2;
			}
			if (text != null)
			{
				into[text] = value;
				if (offsets != null)
				{
					offsets[text] = value2;
				}
			}
		}
		string N(int num3)
		{
			if (num3 < 0 || num3 >= names.Count)
			{
				return "?";
			}
			return names[num3].Text;
		}
	}
}
