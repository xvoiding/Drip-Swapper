using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Drip.Core.FortniteModels;

public static class FnMaterialTint
{
	public static (byte[] Data, int Changed) Apply(byte[] chunk, IReadOnlyList<FnTint> tints)
	{
		byte[] array = (byte[])chunk.Clone();
		int num = 0;
		foreach (FnTint tint in tints)
		{
			for (int i = 0; i + 16 <= array.Length; i++)
			{
				if (Near(array, i, tint.R) && Near(array, i + 4, tint.G) && Near(array, i + 8, tint.B) && Near(array, i + 12, tint.A))
				{
					Write(array, i, tint.R * tint.Keep);
					Write(array, i + 4, tint.G * tint.Keep);
					Write(array, i + 8, tint.B * tint.Keep);
					num++;
					i += 15;
				}
			}
		}
		return (Data: array, Changed: num);
	}

	private static bool Near(byte[] b, int at, float want)
	{
		return MathF.Abs(BitConverter.ToSingle(b, at) - want) <= 0.0001f * MathF.Max(1f, MathF.Abs(want));
	}

	private static void Write(byte[] b, int at, float v)
	{
		BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at), v);
	}
}
