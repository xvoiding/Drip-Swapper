using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.RocketLeague.Custom;

public static class Texture2DWriter
{
	private sealed class Prop
	{
		public int NameIdx;

		public int NameNum;

		public int TypeIdx;

		public int TypeNum;

		public int ArrayIdx;

		public byte[] Extra = Array.Empty<byte>();

		public byte[] Payload = Array.Empty<byte>();

		public string Name = "";

		public string Type = "";
	}

	public static int Variant { get; set; } = 1;

	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	private static List<Prop> ReadProps(byte[] d, List<NameEntry> names, out int end, out int noneIdx, out int noneNum)
	{
		List<Prop> list = new List<Prop>();
		int num = 4;
		noneIdx = 0;
		noneNum = 0;
		for (int i = 0; i < 256; i++)
		{
			if (num + 8 > d.Length)
			{
				throw new UpkException("Couldn't read the texture's properties.");
			}
			int num2 = I(d, num);
			int num3 = I(d, num + 4);
			num += 8;
			if (N(num2) == "None")
			{
				noneIdx = num2;
				noneNum = num3;
				end = num;
				return list;
			}
			Prop prop = new Prop
			{
				NameIdx = num2,
				NameNum = num3,
				Name = N(num2)
			};
			prop.TypeIdx = I(d, num);
			prop.TypeNum = I(d, num + 4);
			prop.Type = N(prop.TypeIdx);
			int num4 = I(d, num + 8);
			prop.ArrayIdx = I(d, num + 12);
			num += 16;
			string type = prop.Type;
			bool flag = ((type == "StructProperty" || type == "ByteProperty") ? true : false);
			int num5 = (flag ? 8 : ((prop.Type == "BoolProperty") ? 1 : 0));
			if (num4 < 0 || num + num5 + num4 > d.Length)
			{
				throw new UpkException("Couldn't read the texture's properties.");
			}
			prop.Extra = d.AsSpan(num, num5).ToArray();
			num += num5;
			prop.Payload = d.AsSpan(num, num4).ToArray();
			num += num4;
			list.Add(prop);
		}
		throw new UpkException("Couldn't read the texture's properties.");
		string N(int num6)
		{
			if (num6 < 0 || num6 >= names.Count)
			{
				return "?";
			}
			return names[num6].Text;
		}
	}

	public static int PropsEnd(byte[] data, List<NameEntry> names)
	{
		ReadProps(data, names, out var end, out var _, out var _);
		return end;
	}

	public static string FormatOf(byte[] data, List<NameEntry> names)
	{
		Prop prop = ReadProps(data, names, out var _, out var _, out var _).FirstOrDefault((Prop p) => p.Name == "Format");
		if (prop != null && prop.Payload.Length >= 4)
		{
			return names[I(prop.Payload, 0)].Text;
		}
		return "";
	}

	public static (int W, int H) SizeOf(byte[] data, List<NameEntry> names)
	{
		List<Prop> props = ReadProps(data, names, out var _, out var _, out var _);
		return (W: Get("SizeX"), H: Get("SizeY"));
		int Get(string n)
		{
			Prop prop = props.FirstOrDefault((Prop p) => p.Name == n);
			if (prop != null)
			{
				byte[] payload = prop.Payload;
				if (payload != null && payload.Length == 4)
				{
					return I(prop.Payload, 0);
				}
			}
			return 0;
		}
	}

	public static byte[] Replace(byte[] old, List<NameEntry> names, TexImage img)
	{
		List<Prop> props = ReadProps(old, names, out var end, out var noneIdx, out var noneNum);
		Prop prop = props.FirstOrDefault((Prop p) => p.Name == "Format") ?? throw new UpkException("That texture has no format property.");
		string text = names[I(prop.Payload, 0)].Text;
		TexFormat texFormat = text switch
		{
			"PF_DXT1" => TexFormat.Dxt1, 
			"PF_DXT5" => TexFormat.Dxt5, 
			"PF_A8R8G8B8" => TexFormat.Bgra8, 
			_ => throw new UpkException("Texture format " + text + " isn't supported yet."), 
		};
		int num = end + 12;
		int num2 = I(old, num);
		num += 4;
		if (num2 < 1 || num2 > 16)
		{
			throw new UpkException("Unexpected texture layout.");
		}
		for (int num3 = 0; num3 < num2; num3++)
		{
			uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(old.AsSpan(num, 4));
			int num5 = I(old, num + 8);
			num += 12;
			num = (((num4 & 1) == 0) ? (num + num5) : (num + 8));
			num += 8;
		}
		if (num > old.Length)
		{
			throw new UpkException("Unexpected texture layout.");
		}
		byte[] array = old.AsSpan(num).ToArray();
		byte[] array2 = old.AsSpan(end, 12).ToArray();
		List<(int, int, byte[])> list = Dxt.Chain(img, texFormat);
		SetInt("SizeX", img.Width);
		SetInt("SizeY", img.Height);
		SetInt("OriginalSizeX", img.Width);
		SetInt("OriginalSizeY", img.Height);
		SetInt("MipTailBaseIdx", list.Count - 1);
		int v = 0;
		if (Variant == 2)
		{
			for (int num6 = 0; num6 < list.Count; num6++)
			{
				if (Math.Max(list[num6].Item1, list[num6].Item2) <= 64)
				{
					v = num6;
					break;
				}
			}
		}
		SetInt("FirstResourceMemMip", v);
		if (Variant != 3)
		{
			Prop prop2 = props.FirstOrDefault((Prop x) => x.Name == "TextureFileCacheName");
			if (prop2 != null)
			{
				byte[] payload = prop2.Payload;
				if (payload != null && payload.Length == 8)
				{
					BinaryPrimitives.WriteInt32LittleEndian(prop2.Payload, noneIdx);
					BinaryPrimitives.WriteInt32LittleEndian(prop2.Payload.AsSpan(4), 0);
				}
			}
		}
		MemoryStream ms = new MemoryStream();
		try
		{
			ms.Write(old.AsSpan(0, 4));
			foreach (Prop item in props)
			{
				W(item.NameIdx);
				W(item.NameNum);
				W(item.TypeIdx);
				W(item.TypeNum);
				W(item.Payload.Length);
				W(item.ArrayIdx);
				ms.Write(item.Extra);
				ms.Write(item.Payload);
			}
			W(noneIdx);
			W(noneNum);
			ms.Write(array2);
			W(list.Count);
			bool flag = texFormat != TexFormat.Bgra8;
			foreach (var (num7, num8, array3) in list)
			{
				W((Variant != 5) ? 65536 : 0);
				W(array3.Length);
				W(array3.Length);
				ms.Write(array3);
				W(flag ? Math.Max(4, num7) : num7);
				W(flag ? Math.Max(4, num8) : num8);
			}
			ms.Write(array);
			return ms.ToArray();
		}
		finally
		{
			if (ms != null)
			{
				((IDisposable)ms).Dispose();
			}
		}
		void SetInt(string name, int value)
		{
			Prop prop3 = props.FirstOrDefault((Prop x) => x.Name == name);
			if (prop3 != null)
			{
				byte[] payload2 = prop3.Payload;
				if (payload2 != null && payload2.Length == 4)
				{
					BinaryPrimitives.WriteInt32LittleEndian(prop3.Payload, value);
				}
			}
		}
		void W(int value)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(span, value);
			ms.Write(span);
		}
	}

	public static byte[] Apply(byte[] img, string name, TexImage picture, out (long Start, int OldLen, int NewLen) edit)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		UpkExport upkExport = UpkTables.Exports(img, s).FirstOrDefault((UpkExport e) => e.ClassName == "Texture2D" && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new UpkException("This package has no texture called " + name + ".");
		byte[] array = Replace(img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names, picture);
		edit = (Start: upkExport.SerialOffset, OldLen: upkExport.SerialSize, NewLen: array.Length);
		return UpkTables.ReplaceExport(img, s, upkExport, array);
	}

	public static (byte R, byte G, byte B, byte A) AverageColor(byte[] data, List<NameEntry> names)
	{
		Prop prop = ReadProps(data, names, out var end, out var _, out var _).FirstOrDefault((Prop p) => p.Name == "Format") ?? throw new UpkException("That texture has no format property.");
		string text = names[I(prop.Payload, 0)].Text;
		int num = end + 12;
		int num2 = I(data, num);
		num += 4;
		if (num2 < 1 || num2 > 16)
		{
			throw new UpkException("Unexpected texture layout.");
		}
		(byte, byte, byte, byte)? tuple = null;
		for (int num3 = 0; num3 < num2; num3++)
		{
			uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(num, 4));
			int num5 = I(data, num + 8);
			num += 12;
			int num6 = num;
			num = (((num4 & 1) == 0) ? (num + num5) : (num + 8));
			I(data, num);
			I(data, num + 4);
			num += 8;
			if ((num4 & 1) != 0 || num5 < 8)
			{
				continue;
			}
			switch (text)
			{
			case "PF_DXT1":
			case "PF_DXT5":
			{
				int start = num6 + ((text == "PF_DXT5") ? 8 : 0);
				int num7 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start, 2));
				tuple = ((byte)(((num7 >> 11) & 0x1F) * 255 / 31), (byte)(((num7 >> 5) & 0x3F) * 255 / 63), (byte)((num7 & 0x1F) * 255 / 31), (text == "PF_DXT5") ? data[num6] : byte.MaxValue);
				break;
			}
			case "PF_A8R8G8B8":
				if (num5 >= 4)
				{
					tuple = (data[num6 + 2], data[num6 + 1], data[num6], data[num6 + 3]);
				}
				break;
			}
		}
		return tuple ?? (128, 128, 128, byte.MaxValue);
	}
}
