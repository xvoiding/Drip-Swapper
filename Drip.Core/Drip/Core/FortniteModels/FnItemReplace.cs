using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Drip.Core.FortniteModels;

public static class FnItemReplace
{
	public const int MaxPicture = 1024;

	public static FnBuild Build(Func<string, byte, byte[]> read, FnItemRig rig, FnTarget target, Action<string>? log = null)
	{
		FnBuild fnBuild = new FnBuild();
		byte[] type = read(rig.Mesh, 1);
		byte[] array = (byte[])read(rig.Mesh, 2).Clone();
		List<FnLod> list = FnMesh.FindLods(array);
		FnMeshInfo fnMeshInfo = FnMeshInfo.Read(type, list);
		double[][] joints = Joints(fnMeshInfo);
		FnTexturePackage.Info info = FnTexturePackage.Read(read(rig.Diffuse, 1));
		int size = Math.Clamp(1 << Math.Max(0, info.BulkEntries - 1), 64, 1024);
		FnLod l = list[0];
		double[] array2 = FnMesh.ReadPositions(array, l);
		int[] refTri = FnMesh.ReadTriangles(array, l);
		(double[] Pos, int[] Tri) tuple = FnItem.Outline(rig.Kind);
		double[] item = tuple.Pos;
		int[] item2 = tuple.Tri;
		FnItemAlign fnItemAlign = FnItemAlign.Fit(array2, refTri, item, item2);
		L($"Lined the model up with {rig.Name}'s own mesh (overlap {fnItemAlign.Score:P0}).");
		FnPart m = FnPart.From(target);
		FnAtlas fnAtlas = FnAtlas.Create(target, m, size);
		FnPart fnPart = fnAtlas.Apply(m);
		Carry(fnPart, fnItemAlign);
		L($"Model: {fnPart.VertexCount:N0} points, {fnPart.TriCount:N0} triangles.");
		double extent = Extent(array2);
		FnPart src = fnPart;
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			FnLod fnLod = list[i];
			if (!fnMeshInfo.BoneMaps.TryGetValue(i, out ushort[] value))
			{
				FnFit.Hide(array, fnLod);
				L($"Detail level {i} has no bone list, so it was switched off.");
				continue;
			}
			FnPart fnPart2 = FnDecimate.Reduce(src, fnLod.VertexCount, fnLod.TriangleCount, L);
			src = fnPart2;
			double[] pos = ReplaceLod(array, fnLod, value, fnMeshInfo, joints, fnPart2, extent);
			if (i == 0)
			{
				fnBuild.Preview.Add(new FnPreviewPart
				{
					Pos = pos,
					Tri = fnPart2.Tri,
					Uv = fnPart2.Uv,
					Texture = fnAtlas.Image
				});
			}
			num++;
		}
		if (num == 0)
		{
			throw new InvalidDataException(rig.Name + "'s mesh has no detail level the model can be attached to.");
		}
		CheckFinite(array, list);
		L($"Mesh replaced ({num} detail level(s)).");
		fnBuild.Patches.Add(new FnPatch(rig.Mesh, 2, array, "item shape"));
		foreach (string item3 in rig.Hide.Where((string h) => h.Length > 0 && !h.Equals(rig.Mesh, StringComparison.OrdinalIgnoreCase)))
		{
			byte[] array3 = (byte[])read(item3, 2).Clone();
			foreach (FnLod item4 in FnMesh.FindLods(array3))
			{
				FnFit.Hide(array3, item4);
			}
			fnBuild.Patches.Add(new FnPatch(item3, 2, array3, "hidden part"));
		}
		fnBuild.Patches.Add(new FnPatch(rig.Diffuse, 1, FnTexturePackage.Build(read(rig.Diffuse, 1), fnAtlas.Image, trimToSlots: true), "item picture"));
		foreach (FnPlain item5 in rig.Plain)
		{
			fnBuild.Patches.Add(new FnPatch(item5.Package, 1, FnTexturePackage.BuildSolid(read(item5.Package, 1), item5.R, item5.G, item5.B), item5.What));
		}
		foreach (FnPatch item6 in fnBuild.Patches.Where((FnPatch p) => p.Type == 1))
		{
			if (FnTexturePackage.Read(item6.Data).MipCount < 1)
			{
				throw new InvalidDataException("The rebuilt " + item6.What + " didn't read back correctly.");
			}
		}
		return fnBuild;
		void L(string s)
		{
			log?.Invoke(s);
		}
	}

	internal static void Carry(FnPart part, FnItemAlign a)
	{
		for (int i = 0; i + 2 < part.Pos.Length; i += 3)
		{
			a.Point(part.Pos[i], part.Pos[i + 1], part.Pos[i + 2], out part.Pos[i], out part.Pos[i + 1], out part.Pos[i + 2]);
			a.Dir(part.Nrm[i], part.Nrm[i + 1], part.Nrm[i + 2], out part.Nrm[i], out part.Nrm[i + 1], out part.Nrm[i + 2]);
		}
	}

	private static double Extent(double[] p)
	{
		double[] array = new double[3]
		{
			double.MaxValue,
			double.MaxValue,
			double.MaxValue
		};
		double[] array2 = new double[3]
		{
			double.MinValue,
			double.MinValue,
			double.MinValue
		};
		for (int i = 0; i + 2 < p.Length; i += 3)
		{
			for (int j = 0; j < 3; j++)
			{
				array[j] = Math.Min(array[j], p[i + j]);
				array2[j] = Math.Max(array2[j], p[i + j]);
			}
		}
		return Math.Max(array2[0] - array[0], Math.Max(array2[1] - array[1], array2[2] - array[2]));
	}

	internal static double[][] Joints(FnMeshInfo info)
	{
		double[][] array = info.Globals();
		double[][] array2 = new double[array.Length][];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = new double[16]
			{
				1.0,
				0.0,
				0.0,
				array[i][3],
				0.0,
				1.0,
				0.0,
				array[i][7],
				0.0,
				0.0,
				1.0,
				array[i][11],
				0.0,
				0.0,
				0.0,
				1.0
			};
		}
		return array2;
	}

	private static void CheckFinite(byte[] body, IReadOnlyList<FnLod> lods)
	{
		foreach (FnLod lod in lods)
		{
			for (int i = 0; i < lod.VertexCount * 3; i++)
			{
				float num = BitConverter.ToSingle(body, lod.PosOffset + 4 * i);
				if (float.IsNaN(num) || float.IsInfinity(num) || Math.Abs(num) > 100000f)
				{
					throw new InvalidDataException("The rebuilt mesh has a point outside any sensible place.");
				}
			}
		}
	}

	internal static double[] ReplaceLod(byte[] body, FnLod lod, ushort[] bmap, FnMeshInfo info, double[][] joints, FnPart part, double extent)
	{
		int vertexCount = lod.VertexCount;
		int k = lod.Influences;
		int vertexCount2 = part.VertexCount;
		if (vertexCount2 > vertexCount)
		{
			throw new InvalidDataException("The model has more points than the item's mesh has room for.");
		}
		double far = Math.Max(2.0, extent * 0.1);
		double[] array = FnMesh.ReadPositions(body, lod);
		(byte[], byte[]) tuple = FnMesh.ReadSkin(body, lod);
		byte[] oBone = tuple.Item1;
		byte[] oW = tuple.Item2;
		int[] oTri = FnMesh.ReadTriangles(body, lod);
		(byte, byte, byte, byte)? tuple2 = FnMesh.CommonColor(body, lod);
		SurfaceIndex surf = new SurfaceIndex(array, oTri, Math.Clamp(extent / 150.0, 0.2, 2.0), 3000000L);
		Dictionary<int, int> local = new Dictionary<int, int>();
		for (int i = 0; i < bmap.Length; i++)
		{
			local.TryAdd(bmap[i], i);
		}
		double[][] segA = new double[bmap.Length][];
		double[][] segB = new double[bmap.Length][];
		for (int j = 0; j < bmap.Length; j++)
		{
			int b = bmap[j];
			segA[j] = joints[b];
			int num = Array.FindIndex(info.Parent, (int p) => p == b);
			segB[j] = ((num >= 0) ? joints[num] : joints[b]);
		}
		byte[] boneOut = new byte[vertexCount2 * k];
		byte[] wOut = new byte[vertexCount2 * k];
		Parallel.For(0, vertexCount2, (int num42) =>
		{
			double x2 = part.Pos[num42 * 3];
			double y2 = part.Pos[num42 * 3 + 1];
			double z2 = part.Pos[num42 * 3 + 2];
			Dictionary<int, double> acc = new Dictionary<int, double>(8);
			if (surf.Nearest(x2, y2, z2, out var _, out var _, out var _, out var tri, out var bu, out var bv) <= far && tri >= 0)
			{
				Add(oTri[tri * 3], 1.0 - bu - bv);
				Add(oTri[tri * 3 + 1], bu);
				Add(oTri[tri * 3 + 2], bv);
			}
			else
			{
				int num43 = 0;
				double num44 = double.MaxValue;
				for (int l = 0; l < bmap.Length; l++)
				{
					double num45 = FnReplace.SegDist(x2, y2, z2, segA[l], segB[l]);
					if (num45 < num44)
					{
						num44 = num45;
						num43 = l;
					}
				}
				acc[bmap[num43]] = 1.0;
			}
			FnReplace.Pack(acc, local, k, boneOut.AsSpan(num42 * k, k), wOut.AsSpan(num42 * k, k));
			void Add(int v, double wt)
			{
				for (int m = 0; m < k; m++)
				{
					int num46 = oW[v * k + m];
					if (num46 != 0)
					{
						int key = bmap[Math.Min(oBone[v * k + m], bmap.Length - 1)];
						acc[key] = acc.GetValueOrDefault(key) + wt * (double)num46 / 255.0;
					}
				}
			}
		});
		double[] array2 = new double[vertexCount2 * 3];
		double[] array3 = new double[vertexCount2 * 3];
		for (int num2 = 0; num2 + 2 < part.Tri.Length; num2 += 3)
		{
			int num3 = part.Tri[num2];
			int num4 = part.Tri[num2 + 1];
			int num5 = part.Tri[num2 + 2];
			double num6 = part.Pos[num4 * 3] - part.Pos[num3 * 3];
			double num7 = part.Pos[num4 * 3 + 1] - part.Pos[num3 * 3 + 1];
			double num8 = part.Pos[num4 * 3 + 2] - part.Pos[num3 * 3 + 2];
			double num9 = part.Pos[num5 * 3] - part.Pos[num3 * 3];
			double num10 = part.Pos[num5 * 3 + 1] - part.Pos[num3 * 3 + 1];
			double num11 = part.Pos[num5 * 3 + 2] - part.Pos[num3 * 3 + 2];
			double num12 = part.Uv[num4 * 2] - part.Uv[num3 * 2];
			double num13 = part.Uv[num4 * 2 + 1] - part.Uv[num3 * 2 + 1];
			double num14 = part.Uv[num5 * 2] - part.Uv[num3 * 2];
			double num15 = part.Uv[num5 * 2 + 1] - part.Uv[num3 * 2 + 1];
			double num16 = num12 * num15 - num13 * num14;
			if (Math.Abs(num16) < 1E-14)
			{
				continue;
			}
			double num17 = 1.0 / num16;
			double num18 = (num6 * num15 - num9 * num13) * num17;
			double num19 = (num7 * num15 - num10 * num13) * num17;
			double num20 = (num8 * num15 - num11 * num13) * num17;
			double num21 = (num9 * num12 - num6 * num14) * num17;
			double num22 = (num10 * num12 - num7 * num14) * num17;
			double num23 = (num11 * num12 - num8 * num14) * num17;
			double num24 = Math.Sqrt(num18 * num18 + num19 * num19 + num20 * num20);
			double num25 = Math.Sqrt(num21 * num21 + num22 * num22 + num23 * num23);
			if (!(num24 < 1E-12) && !(num25 < 1E-12))
			{
				int[] array4 = new int[3] { num3, num4, num5 };
				foreach (int num27 in array4)
				{
					array2[num27 * 3] += num18 / num24;
					array2[num27 * 3 + 1] += num19 / num24;
					array2[num27 * 3 + 2] += num20 / num24;
					array3[num27 * 3] += num21 / num25;
					array3[num27 * 3 + 1] += num22 / num25;
					array3[num27 * 3 + 2] += num23 / num25;
				}
			}
		}
		byte[] array5 = new byte[vertexCount2 * 8];
		for (int num28 = 0; num28 < vertexCount2; num28++)
		{
			double x = part.Nrm[num28 * 3];
			double y = part.Nrm[num28 * 3 + 1];
			double z = part.Nrm[num28 * 3 + 2];
			FnReplace.Unit(ref x, ref y, ref z);
			double num29 = array2[num28 * 3];
			double num30 = array2[num28 * 3 + 1];
			double num31 = array2[num28 * 3 + 2];
			double num32 = num29 * x + num30 * y + num31 * z;
			num29 -= x * num32;
			num30 -= y * num32;
			num31 -= z * num32;
			if (num29 * num29 + num30 * num30 + num31 * num31 < 1E-10)
			{
				if (Math.Abs(z) < 0.9)
				{
					num29 = 0.0 - y;
					num30 = x;
					num31 = 0.0;
				}
				else
				{
					num29 = 1.0;
					num30 = 0.0;
					num31 = 0.0;
				}
			}
			FnReplace.Unit(ref num29, ref num30, ref num31);
			double num33 = y * num31 - z * num30;
			double num34 = z * num29 - x * num31;
			double num35 = x * num30 - y * num29;
			bool flag = num33 * array3[num28 * 3] + num34 * array3[num28 * 3 + 1] + num35 * array3[num28 * 3 + 2] >= 0.0;
			int num36 = num28 * 8;
			array5[num36] = FnMesh.SnormByte(num29);
			array5[num36 + 1] = FnMesh.SnormByte(num30);
			array5[num36 + 2] = FnMesh.SnormByte(num31);
			array5[num36 + 3] = 127;
			array5[num36 + 4] = FnMesh.SnormByte(x);
			array5[num36 + 5] = FnMesh.SnormByte(y);
			array5[num36 + 6] = FnMesh.SnormByte(z);
			array5[num36 + 7] = (byte)(flag ? 127 : FnMesh.SnormByte(-1.0));
		}
		bool[] array6 = new bool[vertexCount];
		Array.Fill(array6, value: true);
		int[] array7 = FnReplace.AssignSlots(array, array6, part.Pos, vertexCount);
		for (int num37 = 0; num37 < vertexCount2; num37++)
		{
			int num38 = array7[num37];
			for (int num39 = 0; num39 < 3; num39++)
			{
				BitConverter.TryWriteBytes(body.AsSpan(lod.PosOffset + 12 * num38 + 4 * num39), (float)part.Pos[num37 * 3 + num39]);
			}
			Array.Copy(array5, num37 * 8, body, lod.TanOffset + 8 * num38, 8);
			FnMesh.WriteUv(body, lod, num38, part.Uv[num37 * 2], part.Uv[num37 * 2 + 1]);
			FnMesh.WriteSkin(body, lod, num38, boneOut.AsSpan(num37 * k, k), wOut.AsSpan(num37 * k, k));
		}
		int[] array8 = new int[lod.IndexCount];
		for (int num40 = 0; num40 < part.Tri.Length; num40++)
		{
			array8[num40] = array7[part.Tri[num40]];
		}
		for (int num41 = part.Tri.Length; num41 < array8.Length; num41++)
		{
			array8[num41] = array7[0];
		}
		FnMesh.WriteTriangles(body, lod, array8);
		if (tuple2.HasValue)
		{
			(byte, byte, byte, byte) valueOrDefault = tuple2.GetValueOrDefault();
			FnMesh.FillColors(body, lod, valueOrDefault.Item3, valueOrDefault.Item2, valueOrDefault.Item1, valueOrDefault.Item4);
		}
		return (double[])part.Pos.Clone();
	}
}
