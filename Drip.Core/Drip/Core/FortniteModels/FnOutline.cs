using System;
using System.IO;
using System.IO.Compression;

namespace Drip.Core.FortniteModels;

public static class FnOutline
{
	private static readonly Lazy<(double[] Pos, int[] Tri)> Mesh = new Lazy<(double[], int[])>(Load);

	private static readonly Lazy<SurfaceIndex> IndexLazy = new Lazy<SurfaceIndex>(() => new SurfaceIndex(Mesh.Value.Pos, Mesh.Value.Tri, 0.8, 600000L));

	public static double[] Pos => Mesh.Value.Pos;

	public static int[] Tri => Mesh.Value.Tri;

	public static SurfaceIndex Index => IndexLazy.Value;

	private static (double[], int[]) Load()
	{
		using Stream stream = typeof(FnOutline).Assembly.GetManifestResourceStream("Drip.Core.FortniteModels.soldier_outline.bin") ?? throw new InvalidDataException("The character outline is missing from this build.");
		using DeflateStream input = new DeflateStream(stream, CompressionMode.Decompress);
		using BinaryReader binaryReader = new BinaryReader(input);
		int num = binaryReader.ReadInt32();
		int num2 = binaryReader.ReadInt32();
		double[] array = new double[num * 3];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = binaryReader.ReadSingle();
		}
		int[] array2 = new int[num2 * 3];
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j] = binaryReader.ReadUInt16();
		}
		return (array, array2);
	}

	public static (double Within, double Mean) Fit(FnTarget t)
	{
		int num = t.Pos.Length / 3;
		int num2 = Math.Max(1, num / 2500);
		int num3 = 0;
		int num4 = 0;
		double num5 = 0.0;
		SurfaceIndex index = Index;
		for (int i = 0; i < num; i += num2)
		{
			double num6 = index.Nearest(t.Pos[i * 3], t.Pos[i * 3 + 1], t.Pos[i * 3 + 2], out var _, out var _, out var _, out var _, out var _, out var _);
			num3++;
			num5 += Math.Min(num6, 60.0);
			if (num6 <= 12.0)
			{
				num4++;
			}
		}
		if (num3 != 0)
		{
			return (Within: (double)num4 / (double)num3, Mean: num5 / (double)num3);
		}
		return (Within: 0.0, Mean: 0.0);
	}
}
