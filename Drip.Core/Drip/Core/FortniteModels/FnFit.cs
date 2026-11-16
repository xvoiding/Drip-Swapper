using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class FnFit
{
	public static FnLodFit FitLod(FnTarget tgt, byte[] bulk, FnLod lod, double[][]? skinMatrices, ushort[]? boneMap)
	{
		int vertexCount = lod.VertexCount;
		double[] pos = FnMesh.ReadPositions(bulk, lod);
		var (xs, zs, _) = FnMesh.ReadFrames(bulk, lod);
		double[][] mats;
		if (skinMatrices != null && boneMap != null)
		{
			(byte[] Bone, byte[] Weight) tuple2 = FnMesh.ReadSkin(bulk, lod);
			byte[] item = tuple2.Bone;
			byte[] item2 = tuple2.Weight;
			mats = FnPose.Blend(item, item2, vertexCount, lod.Influences, boneMap, skinMatrices);
		}
		else
		{
			mats = new double[vertexCount][];
			double[] array = FnMath.Identity();
			for (int i = 0; i < vertexCount; i++)
			{
				mats[i] = array;
			}
		}
		float[] posA = new float[vertexCount * 3];
		byte[] tan = new byte[vertexCount * 8];
		double[] posT = new double[vertexCount * 3];
		double[] dist = new double[vertexCount];
		Array.Copy(bulk, lod.TanOffset, tan, 0, tan.Length);
		SurfaceIndex index = tgt.Index;
		Parallel.For(0, vertexCount, (int num) =>
		{
			double[] m = mats[num];
			FnMath.Point(m, pos[num * 3], pos[num * 3 + 1], pos[num * 3 + 2], out var ox, out var oy, out var oz);
			dist[num] = index.Nearest(ox, oy, oz, out var qx, out var qy, out var qz, out var tri, out var bu, out var bv);
			tgt.NormalAt(tri, bu, bv, out var nx, out var ny, out var nz);
			posT[num * 3] = qx;
			posT[num * 3 + 1] = qy;
			posT[num * 3 + 2] = qz;
			FnMath.Dir(m, zs[num * 3], zs[num * 3 + 1], zs[num * 3 + 2], out var ox2, out var oy2, out var oz2);
			FnMath.Dir(m, xs[num * 3], xs[num * 3 + 1], xs[num * 3 + 2], out var ox3, out var oy3, out var oz3);
			Norm(ref ox2, ref oy2, ref oz2);
			Norm(ref ox3, ref oy3, ref oz3);
			FnMath.Rot3(FnMath.RotAlign(ox2, oy2, oz2, nx, ny, nz), ox3, oy3, oz3, out var ox4, out var oy4, out var oz4);
			double num2 = ox4 * nx + oy4 * ny + oz4 * nz;
			ox4 -= nx * num2;
			oy4 -= ny * num2;
			oz4 -= nz * num2;
			Norm(ref ox4, ref oy4, ref oz4);
			double[] m2 = FnMath.Inverse(m);
			FnMath.Point(m2, qx, qy, qz, out var ox5, out var oy5, out var oz5);
			FnMath.DirT(m, nx, ny, nz, out var ox6, out var oy6, out var oz6);
			Norm(ref ox6, ref oy6, ref oz6);
			FnMath.Dir(m2, ox4, oy4, oz4, out var ox7, out var oy7, out var oz7);
			double num3 = ox7 * ox6 + oy7 * oy6 + oz7 * oz6;
			ox7 -= ox6 * num3;
			oy7 -= oy6 * num3;
			oz7 -= oz6 * num3;
			Norm(ref ox7, ref oy7, ref oz7);
			posA[num * 3] = (float)ox5;
			posA[num * 3 + 1] = (float)oy5;
			posA[num * 3 + 2] = (float)oz5;
			int num4 = num * 8;
			tan[num4] = FnMesh.SnormByte(ox7);
			tan[num4 + 1] = FnMesh.SnormByte(oy7);
			tan[num4 + 2] = FnMesh.SnormByte(oz7);
			tan[num4 + 4] = FnMesh.SnormByte(ox6);
			tan[num4 + 5] = FnMesh.SnormByte(oy6);
			tan[num4 + 6] = FnMesh.SnormByte(oz6);
		});
		FnMesh.WritePositions(bulk, lod, posA);
		FnMesh.WriteTangents(bulk, lod, tan);
		double[] array2 = (double[])dist.Clone();
		Array.Sort(array2);
		return new FnLodFit
		{
			PosT = posT,
			Tri = FnMesh.ReadTriangles(bulk, lod),
			Uv = FnMesh.ReadUv0(bulk, lod),
			MedianMove = array2[vertexCount / 2],
			P90Move = array2[Math.Min(vertexCount - 1, (int)((double)vertexCount * 0.9))]
		};
	}

	private static void Norm(ref double x, ref double y, ref double z)
	{
		double num = Math.Sqrt(x * x + y * y + z * z);
		if (num < 1E-12)
		{
			x = 0.0;
			y = 0.0;
			z = 1.0;
		}
		else
		{
			x /= num;
			y /= num;
			z /= num;
		}
	}

	public static void Hide(byte[] bulk, FnLod lod)
	{
		FnMesh.WritePositions(bulk, lod, new float[lod.VertexCount * 3]);
		FnMesh.WriteSingleBone(bulk, lod);
	}

	public static TexImage Bake(FnTarget tgt, FnLodFit fit, int width, int height)
	{
		float[] array = new float[width * height * 3];
		bool[] array2 = new bool[width * height];
		double[] array3 = new double[fit.Uv.Length / 2];
		double[] array4 = new double[fit.Uv.Length / 2];
		for (int i = 0; i < array3.Length; i++)
		{
			array3[i] = fit.Uv[i * 2] * (double)width;
			array4[i] = fit.Uv[i * 2 + 1] * (double)height;
		}
		int num = fit.Tri.Length / 3;
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		List<(double, double, double)> pp = new List<(double, double, double)>();
		for (int j = 0; j < num; j++)
		{
			int num2 = fit.Tri[j * 3];
			int num3 = fit.Tri[j * 3 + 1];
			int num4 = fit.Tri[j * 3 + 2];
			double num5 = Math.Min(array3[num2], Math.Min(array3[num3], array3[num4]));
			double num6 = Math.Max(array3[num2], Math.Max(array3[num3], array3[num4]));
			double num7 = Math.Min(array4[num2], Math.Min(array4[num3], array4[num4]));
			double num8 = Math.Max(array4[num2], Math.Max(array4[num3], array4[num4]));
			int num9 = (int)Math.Floor(num5 - 0.5);
			int num10 = (int)Math.Ceiling(num6 + 0.5);
			int num11 = (int)Math.Floor(num7 - 0.5);
			int num12 = (int)Math.Ceiling(num8 + 0.5);
			if (num10 < 0 || num12 < 0 || num9 >= width || num11 >= height)
			{
				continue;
			}
			double num13 = (array4[num3] - array4[num4]) * (array3[num2] - array3[num4]) + (array3[num4] - array3[num3]) * (array4[num2] - array4[num4]);
			if (Math.Abs(num13) < 1E-12)
			{
				continue;
			}
			for (int k = Math.Max(0, num11); k <= Math.Min(height - 1, num12); k++)
			{
				for (int l = Math.Max(0, num9); l <= Math.Min(width - 1, num10); l++)
				{
					double num14 = (double)l + 0.5;
					double num15 = (double)k + 0.5;
					double num16 = ((array4[num3] - array4[num4]) * (num14 - array3[num4]) + (array3[num4] - array3[num3]) * (num15 - array4[num4])) / num13;
					double num17 = ((array4[num4] - array4[num2]) * (num14 - array3[num4]) + (array3[num2] - array3[num4]) * (num15 - array4[num4])) / num13;
					double num18 = 1.0 - num16 - num17;
					if (!(num16 < -1E-06) && !(num17 < -1E-06) && !(num18 < -1E-06))
					{
						list.Add(l);
						list2.Add(k);
						pp.Add((num16 * fit.PosT[num2 * 3] + num17 * fit.PosT[num3 * 3] + num18 * fit.PosT[num4 * 3], num16 * fit.PosT[num2 * 3 + 1] + num17 * fit.PosT[num3 * 3 + 1] + num18 * fit.PosT[num4 * 3 + 1], num16 * fit.PosT[num2 * 3 + 2] + num17 * fit.PosT[num3 * 3 + 2] + num18 * fit.PosT[num4 * 3 + 2]));
					}
				}
			}
		}
		float[] col = new float[list.Count * 3];
		SurfaceIndex index = tgt.Index;
		Parallel.For(0, list.Count, (int num22) =>
		{
			var (x, y, z) = pp[num22];
			index.Nearest(x, y, z, out var _, out var _, out var _, out var tri, out var bu, out var bv);
			tgt.ColorAt(tri, bu, bv, out var r, out var g, out var b);
			col[num22 * 3] = (float)r;
			col[num22 * 3 + 1] = (float)g;
			col[num22 * 3 + 2] = (float)b;
		});
		for (int num19 = 0; num19 < list.Count; num19++)
		{
			int num20 = list2[num19] * width + list[num19];
			array[num20 * 3] = col[num19 * 3];
			array[num20 * 3 + 1] = col[num19 * 3 + 1];
			array[num20 * 3 + 2] = col[num19 * 3 + 2];
			array2[num20] = true;
		}
		Dilate(array, array2, width, height);
		TexImage texImage = new TexImage
		{
			Width = width,
			Height = height,
			Rgba = new byte[width * height * 4]
		};
		for (int num21 = 0; num21 < width * height; num21++)
		{
			texImage.Rgba[num21 * 4] = (byte)Math.Clamp((int)Math.Round(array[num21 * 3]), 0, 255);
			texImage.Rgba[num21 * 4 + 1] = (byte)Math.Clamp((int)Math.Round(array[num21 * 3 + 1]), 0, 255);
			texImage.Rgba[num21 * 4 + 2] = (byte)Math.Clamp((int)Math.Round(array[num21 * 3 + 2]), 0, 255);
			texImage.Rgba[num21 * 4 + 3] = byte.MaxValue;
		}
		return texImage;
	}

	private static void Dilate(float[] rgb, bool[] covered, int w, int h)
	{
		Queue<int> queue = new Queue<int>();
		for (int i = 0; i < covered.Length; i++)
		{
			if (covered[i])
			{
				queue.Enqueue(i);
			}
		}
		if (queue.Count == 0)
		{
			for (int j = 0; j < w * h; j++)
			{
				rgb[j * 3] = (rgb[j * 3 + 1] = (rgb[j * 3 + 2] = 128f));
			}
			return;
		}
		int[] array = new int[8] { 1, -1, 0, 0, 1, 1, -1, -1 };
		int[] array2 = new int[8] { 0, 0, 1, -1, 1, -1, 1, -1 };
		while (queue.Count > 0)
		{
			int num = queue.Dequeue();
			int num2 = num % w;
			int num3 = num / w;
			for (int k = 0; k < 8; k++)
			{
				int num4 = num2 + array[k];
				int num5 = num3 + array2[k];
				if (num4 >= 0 && num5 >= 0 && num4 < w && num5 < h)
				{
					int num6 = num5 * w + num4;
					if (!covered[num6])
					{
						covered[num6] = true;
						rgb[num6 * 3] = rgb[num * 3];
						rgb[num6 * 3 + 1] = rgb[num * 3 + 1];
						rgb[num6 * 3 + 2] = rgb[num * 3 + 2];
						queue.Enqueue(num6);
					}
				}
			}
		}
	}
}
