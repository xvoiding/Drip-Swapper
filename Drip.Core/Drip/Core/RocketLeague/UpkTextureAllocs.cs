using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Drip.Core.RocketLeague;

public static class UpkTextureAllocs
{
	public static List<UpkTextureGroup> Read(byte[] img, UpkSummary s)
	{
		List<UpkTextureGroup> list = new List<UpkTextureGroup>();
		int texAllocPos = s.TexAllocPos;
		int num = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos, 4));
		texAllocPos += 4;
		if (num < 0 || num > 4096)
		{
			return list;
		}
		for (int i = 0; i < num; i++)
		{
			if (texAllocPos + 24 > img.Length)
			{
				break;
			}
			UpkTextureGroup upkTextureGroup = new UpkTextureGroup
			{
				At = texAllocPos,
				SizeX = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos, 4)),
				SizeY = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos + 4, 4)),
				NumMips = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos + 8, 4))
			};
			texAllocPos += 20;
			int num2 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos, 4));
			texAllocPos += 4;
			if (num2 < 0 || texAllocPos + 4 * num2 > img.Length)
			{
				break;
			}
			upkTextureGroup.Exports = new int[num2];
			for (int j = 0; j < num2; j++)
			{
				upkTextureGroup.Exports[j] = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(texAllocPos + 4 * j, 4));
			}
			texAllocPos += 4 * num2;
			list.Add(upkTextureGroup);
		}
		return list;
	}

	public static void Set(byte[] img, UpkTextureGroup g, int w, int h, int mips)
	{
		BinaryPrimitives.WriteInt32LittleEndian(img.AsSpan(g.At, 4), w);
		BinaryPrimitives.WriteInt32LittleEndian(img.AsSpan(g.At + 4, 4), h);
		BinaryPrimitives.WriteInt32LittleEndian(img.AsSpan(g.At + 8, 4), mips);
	}
}
