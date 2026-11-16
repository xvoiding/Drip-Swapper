using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drip.Core.RocketLeague;

public sealed class UpkPropList
{
	public byte[] Prefix = Array.Empty<byte>();

	public List<UpkProp> Props = new List<UpkProp>();

	public int NoneIdx;

	public int NoneNum;

	public byte[] Tail = Array.Empty<byte>();

	public UpkProp? Find(string name)
	{
		return Props.FirstOrDefault((UpkProp p) => p.Name == name);
	}

	public static UpkPropList Read(byte[] d, List<NameEntry> names)
	{
		int[] array = new int[3] { 4, 8, 0 };
		foreach (int start in array)
		{
			UpkPropList upkPropList = TryRead(d, names, start);
			if (upkPropList != null)
			{
				return upkPropList;
			}
		}
		throw new UpkException("Couldn't read an object's properties.");
	}

	private static UpkPropList? TryRead(byte[] d, List<NameEntry> names, int start)
	{
		UpkPropList upkPropList = new UpkPropList
		{
			Prefix = d.AsSpan(0, Math.Min(start, d.Length)).ToArray()
		};
		int num = start;
		for (int i = 0; i < 256; i++)
		{
			if (num + 8 > d.Length)
			{
				return null;
			}
			int num2 = I(num);
			int num3 = I(num + 4);
			if (num2 < 0 || num2 >= names.Count)
			{
				return null;
			}
			num += 8;
			if (N(num2) == "None")
			{
				upkPropList.NoneIdx = num2;
				upkPropList.NoneNum = num3;
				upkPropList.Tail = d.AsSpan(num).ToArray();
				return upkPropList;
			}
			if (num + 16 > d.Length)
			{
				return null;
			}
			UpkProp upkProp = new UpkProp
			{
				NameIdx = num2,
				NameNum = num3,
				Name = N(num2)
			};
			upkProp.TypeIdx = I(num);
			upkProp.TypeNum = I(num + 4);
			if (upkProp.TypeIdx < 0 || upkProp.TypeIdx >= names.Count)
			{
				return null;
			}
			upkProp.Type = N(upkProp.TypeIdx);
			if (!upkProp.Type.EndsWith("Property", StringComparison.Ordinal))
			{
				return null;
			}
			int num4 = I(num + 8);
			upkProp.ArrayIdx = I(num + 12);
			num += 16;
			string type = upkProp.Type;
			bool flag = ((type == "StructProperty" || type == "ByteProperty") ? true : false);
			int num5 = (flag ? 8 : ((upkProp.Type == "BoolProperty") ? 1 : 0));
			if (num4 < 0 || num + num5 + num4 > d.Length)
			{
				return null;
			}
			upkProp.Extra = d.AsSpan(num, num5).ToArray();
			num += num5;
			upkProp.Payload = d.AsSpan(num, num4).ToArray();
			num += num4;
			upkPropList.Props.Add(upkProp);
		}
		return null;
		int I(int p)
		{
			return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
		}
		string N(int num6)
		{
			if (num6 < 0 || num6 >= names.Count)
			{
				return "";
			}
			return names[num6].Text;
		}
	}

	public byte[] Write()
	{
		MemoryStream ms = new MemoryStream();
		try
		{
			ms.Write(Prefix);
			foreach (UpkProp prop in Props)
			{
				W(prop.NameIdx);
				W(prop.NameNum);
				W(prop.TypeIdx);
				W(prop.TypeNum);
				W(prop.Payload.Length);
				W(prop.ArrayIdx);
				ms.Write(prop.Extra);
				ms.Write(prop.Payload);
			}
			W(NoneIdx);
			W(NoneNum);
			ms.Write(Tail);
			return ms.ToArray();
		}
		finally
		{
			if (ms != null)
			{
				((IDisposable)ms).Dispose();
			}
		}
		void W(int v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(span, v);
			ms.Write(span);
		}
	}

	public static int NameIndex(List<NameEntry> names, string text)
	{
		for (int i = 0; i < names.Count; i++)
		{
			if (names[i].Text == text)
			{
				return i;
			}
		}
		return -1;
	}
}
