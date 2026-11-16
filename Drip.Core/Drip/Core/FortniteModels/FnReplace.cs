using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Drip.Core.FortniteModels;

public static class FnReplace
{
	private sealed class SlotGrid
	{
		private const double Cell = 5.0;

		private readonly double[] _p;

		private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

		private int _remaining;

		private readonly int _minX = int.MaxValue;

		private readonly int _maxX = int.MinValue;

		private readonly int _minY = int.MaxValue;

		private readonly int _maxY = int.MinValue;

		private readonly int _minZ = int.MaxValue;

		private readonly int _maxZ = int.MinValue;

		public SlotGrid(double[] p, List<int> items)
		{
			_p = p;
			_remaining = items.Count;
			foreach (int item in items)
			{
				int num = C(p[item * 3]);
				int num2 = C(p[item * 3 + 1]);
				int num3 = C(p[item * 3 + 2]);
				if (!_cells.TryGetValue(Key(num, num2, num3), out List<int> value))
				{
					value = (_cells[Key(num, num2, num3)] = new List<int>());
				}
				value.Add(item);
				_minX = Math.Min(_minX, num);
				_maxX = Math.Max(_maxX, num);
				_minY = Math.Min(_minY, num2);
				_maxY = Math.Max(_maxY, num2);
				_minZ = Math.Min(_minZ, num3);
				_maxZ = Math.Max(_maxZ, num3);
			}
		}

		private static int C(double v)
		{
			return (int)Math.Floor(v / 5.0);
		}

		private static long Key(int x, int y, int z)
		{
			return ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (uint)(z + 4096);
		}

		public int Take(double x, double y, double z)
		{
			if (_remaining == 0)
			{
				return -1;
			}
			int num = C(x);
			int num2 = C(y);
			int num3 = C(z);
			int num4 = Math.Max(Math.Max(Math.Abs(num - _minX), Math.Abs(num - _maxX)), Math.Max(Math.Max(Math.Abs(num2 - _minY), Math.Abs(num2 - _maxY)), Math.Max(Math.Abs(num3 - _minZ), Math.Abs(num3 - _maxZ)))) + 1;
			double num5 = double.MaxValue;
			int num6 = -1;
			List<int> list = null;
			int num7 = -1;
			for (int i = 0; i <= num4; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						int num8 = ((Math.Abs(j) == i || Math.Abs(k) == i) ? 1 : Math.Max(1, 2 * i));
						for (int l = -i; l <= i; l += num8)
						{
							if (!_cells.TryGetValue(Key(num + l, num2 + k, num3 + j), out List<int> value))
							{
								continue;
							}
							for (int m = 0; m < value.Count; m++)
							{
								int num9 = value[m];
								double num10 = _p[num9 * 3] - x;
								double num11 = _p[num9 * 3 + 1] - y;
								double num12 = _p[num9 * 3 + 2] - z;
								double num13 = num10 * num10 + num11 * num11 + num12 * num12;
								if (num13 < num5)
								{
									num5 = num13;
									num6 = num9;
									list = value;
									num7 = m;
								}
							}
						}
					}
				}
				if (num6 >= 0 && Math.Sqrt(num5) <= (double)i * 5.0)
				{
					break;
				}
			}
			if (num6 < 0)
			{
				return -1;
			}
			List<int> list2 = list;
			int index = num7;
			List<int> list3 = list;
			list2[index] = list3[list3.Count - 1];
			list.RemoveAt(list.Count - 1);
			_remaining--;
			return num6;
		}
	}

	private const double FarCm = 6.0;

	public static FnBuild Build(Func<string, byte, byte[]> read, FnRigInfo rig, FnTarget target, Action<string>? log = null)
	{
		return BuildCore(read, rig, target, null, log);
	}

	public static FnBuild BuildNative(Func<string, byte, byte[]> read, FnRigInfo rig, FnTarget target, FnSkin skin, Action<string>? log = null)
	{
		return BuildCore(read, rig, target, skin, log);
	}

	private static FnBuild BuildCore(Func<string, byte, byte[]> read, FnRigInfo rig, FnTarget target, FnSkin? skin, Action<string>? log)
	{
		if (skin != null && skin.VertexCount != target.Pos.Length / 3)
		{
			throw new InvalidDataException("The model's rig doesn't match its shape.");
		}
		FnBuild fnBuild = new FnBuild();
		byte[] type = read(rig.BodyMesh, 1);
		byte[] array = (byte[])read(rig.BodyMesh, 2).Clone();
		List<FnLod> list = FnMesh.FindLods(array);
		FnMeshInfo fnMeshInfo = FnMeshInfo.Read(type, list);
		double[][] sm = ((skin != null) ? (from _ in Enumerable.Range(0, fnMeshInfo.BoneNames.Length)
			select FnMath.Identity()).ToArray() : FnPose.TPoseSkin(fnMeshInfo));
		double[][] joints = TPoseJoints(fnMeshInfo, sm);
		int num = Math.Min(rig.MaxLods, list.Count);
		NativeRig native = ((skin != null) ? NativeRig.Create(skin, fnMeshInfo, target.Pos, L) : null);
		FnPart m = FnPart.From(target);
		FnAtlas fnAtlas = FnAtlas.Create(target, m);
		FnPart fnPart = fnAtlas.Apply(m);
		L($"Model: {fnPart.VertexCount:N0} points, {fnPart.TriCount:N0} triangles.");
		FnPart src = fnPart;
		for (int num2 = 0; num2 < num; num2++)
		{
			if (!fnMeshInfo.BoneMaps.TryGetValue(num2, out ushort[] value))
			{
				throw new InvalidDataException($"Body detail level {num2}: no bone list found, so the model can't be attached.");
			}
			FnLod fnLod = list[num2];
			FnPart fnPart2 = FnDecimate.Reduce(src, fnLod.VertexCount, fnLod.TriangleCount, L);
			src = fnPart2;
			double[] pos = ReplaceLod(array, fnLod, value, sm, joints, fnMeshInfo, rig, fnPart2, (num2 == 0) ? 0.3 : 0.6, native, out var medianFar);
			if (num2 == 0)
			{
				fnBuild.Preview.Add(new FnPreviewPart
				{
					Pos = pos,
					Tri = fnPart2.Tri,
					Uv = fnPart2.Uv,
					Texture = fnAtlas.Image
				});
				L($"Body replaced ({num} detail levels); {fnPart2.VertexCount:N0} of {fnLod.VertexCount:N0} point slots used at the closest one.");
				fnBuild.BodyMove = medianFar;
			}
		}
		CheckFinite(array, list, num);
		fnBuild.Patches.Add(new FnPatch(rig.BodyMesh, 2, array, "body shape"));
		foreach (string item in from h in new string[1] { rig.HeadMesh }.Concat(rig.HiddenMeshes)
			where h.Length > 0
			select h)
		{
			byte[] array2 = (byte[])read(item, 2).Clone();
			foreach (FnLod item2 in FnMesh.FindLods(array2))
			{
				FnFit.Hide(array2, item2);
			}
			fnBuild.Patches.Add(new FnPatch(item, 2, array2, "hidden part"));
		}
		fnBuild.Patches.Add(new FnPatch(rig.BodyTexture, 1, FnTexturePackage.Build(read(rig.BodyTexture, 1), fnAtlas.Image), "body picture"));
		(string, byte, byte, byte, string)[] array3 = new (string, byte, byte, byte, string)[5]
		{
			(rig.BodyNormal, 128, 128, byte.MaxValue, "flat normal map"),
			(rig.BodySpecular, 120, 8, 185, "plain shine map"),
			(rig.BodyMask, 0, 0, 0, "empty mask"),
			(rig.BodyEffects, 0, 0, 0, "empty effects mask"),
			(rig.BodyEmissive, 0, 0, 0, "empty glow map")
		};
		for (int num3 = 0; num3 < array3.Length; num3++)
		{
			var (text, r, g, b, what) = array3[num3];
			if (text.Length != 0)
			{
				fnBuild.Patches.Add(new FnPatch(text, 1, FnTexturePackage.BuildSolid(read(text, 1), r, g, b), what));
			}
		}
		if (rig.BodyMaterial.Length > 0 && rig.Tints.Length != 0)
		{
			try
			{
				var (data, num4) = FnMaterialTint.Apply(read(rig.BodyMaterial, 1), rig.Tints);
				if (num4 > 0)
				{
					fnBuild.Patches.Add(new FnPatch(rig.BodyMaterial, 1, data, "material tint"));
					L($"Body material: {num4} loud colour setting(s) turned down.");
				}
				else
				{
					L("Body material: none of the expected colour settings were found, so it was left as it is.");
				}
			}
			catch (Exception ex)
			{
				L("Body material left as it is (" + ex.Message + ").");
			}
		}
		foreach (FnPatch item3 in fnBuild.Patches.Where((FnPatch p) => p.Type == 1 && p.What != "material tint"))
		{
			if (FnTexturePackage.Read(item3.Data).MipCount < 1)
			{
				throw new InvalidDataException("The rebuilt " + item3.What + " didn't read back correctly.");
			}
		}
		return fnBuild;
		void L(string s)
		{
			log?.Invoke(s);
		}
	}

	private static double[] ReplaceLod(byte[] body, FnLod lod, ushort[] bmap, double[][] sm, double[][] joints, FnMeshInfo info, FnRigInfo rig, FnPart part, double spacing, NativeRig? native, out double medianFar)
	{
		int vertexCount = lod.VertexCount;
		int k = lod.Influences;
		int vertexCount2 = part.VertexCount;
		double[] array = FnMesh.ReadPositions(body, lod);
		(byte[], byte[]) tuple = FnMesh.ReadSkin(body, lod);
		byte[] sBone = tuple.Item1;
		byte[] sW = tuple.Item2;
		double[][] array2 = FnPose.Blend(sBone, sW, vertexCount, k, bmap, sm);
		double[] array3 = new double[vertexCount * 3];
		for (int i = 0; i < vertexCount; i++)
		{
			FnMath.Point(array2[i], array[i * 3], array[i * 3 + 1], array[i * 3 + 2], out array3[i * 3], out array3[i * 3 + 1], out array3[i * 3 + 2]);
		}
		int[] array4 = FnMesh.ReadTriangles(body, lod);
		byte[] array5 = FnMesh.ReadColorRed(body, lod);
		HashSet<byte> hashSet = new HashSet<byte>(rig.HiddenColorCodes);
		bool[] array6 = new bool[vertexCount];
		for (int j = 0; j < vertexCount; j++)
		{
			array6[j] = array5 == null || !hashSet.Contains(array5[j]);
		}
		List<int> list = new List<int>(array4.Length);
		for (int l = 0; l + 2 < array4.Length; l += 3)
		{
			if (array6[array4[l]] && array6[array4[l + 1]] && array6[array4[l + 2]])
			{
				list.Add(array4[l]);
				list.Add(array4[l + 1]);
				list.Add(array4[l + 2]);
			}
		}
		if (list.Count == 0)
		{
			list.AddRange(array4);
		}
		int[] visArr = list.ToArray();
		SurfaceIndex surf = new SurfaceIndex(array3, visArr, spacing, 3000000L);
		Dictionary<int, int> local = new Dictionary<int, int>();
		for (int m = 0; m < bmap.Length; m++)
		{
			local.TryAdd(bmap[m], m);
		}
		double[][] segA = new double[bmap.Length][];
		double[][] segB = new double[bmap.Length][];
		for (int n = 0; n < bmap.Length; n++)
		{
			int b = bmap[n];
			segA[n] = joints[b];
			int num = Array.FindIndex(info.Parent, (int p) => p == b);
			segB[n] = ((num >= 0) ? joints[num] : joints[b]);
		}
		byte[] boneOut = new byte[vertexCount2 * k];
		byte[] wOut = new byte[vertexCount2 * k];
		double[] dist = new double[vertexCount2];
		if (native != null)
		{
			native.Weights(part, local, info.Parent, k, boneOut, wOut);
		}
		else
		{
			Parallel.For(0, vertexCount2, (int num43) =>
			{
				double x2 = part.Pos[num43 * 3];
				double y2 = part.Pos[num43 * 3 + 1];
				double z2 = part.Pos[num43 * 3 + 2];
				Dictionary<int, double> acc = new Dictionary<int, double>(16);
				double num44 = surf.Nearest(x2, y2, z2, out var _, out var _, out var _, out var tri, out var bu, out var bv);
				dist[num43] = num44;
				if (num44 <= 6.0 && tri >= 0)
				{
					Add(visArr[tri * 3], 1.0 - bu - bv);
					Add(visArr[tri * 3 + 1], bu);
					Add(visArr[tri * 3 + 2], bv);
				}
				else
				{
					int num45 = 0;
					double num46 = double.MaxValue;
					for (int num47 = 0; num47 < bmap.Length; num47++)
					{
						double num48 = SegDist(x2, y2, z2, segA[num47], segB[num47]);
						if (num48 < num46)
						{
							num46 = num48;
							num45 = num47;
						}
					}
					acc[bmap[num45]] = 1.0;
				}
				Pack(acc, local, k, boneOut.AsSpan(num43 * k, k), wOut.AsSpan(num43 * k, k));
				void Add(int v, double wt)
				{
					for (int num49 = 0; num49 < k; num49++)
					{
						int num50 = sW[v * k + num49];
						if (num50 != 0)
						{
							int key = bmap[Math.Min(sBone[v * k + num49], bmap.Length - 1)];
							acc[key] = acc.GetValueOrDefault(key) + wt * (double)num50 / 255.0;
						}
					}
				}
			});
		}
		double[] array7 = (double[])dist.Clone();
		Array.Sort(array7);
		medianFar = ((array7.Length != 0) ? array7[array7.Length / 2] : 0.0);
		double[][] array8 = FnPose.Blend(boneOut, wOut, vertexCount2, k, bmap, sm);
		double[] array9 = new double[vertexCount2 * 3];
		double[] array10 = new double[vertexCount2 * 3];
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
				int[] array11 = new int[3] { num3, num4, num5 };
				foreach (int num27 in array11)
				{
					array9[num27 * 3] += num18 / num24;
					array9[num27 * 3 + 1] += num19 / num24;
					array9[num27 * 3 + 2] += num20 / num24;
					array10[num27 * 3] += num21 / num25;
					array10[num27 * 3 + 1] += num22 / num25;
					array10[num27 * 3 + 2] += num23 / num25;
				}
			}
		}
		double[] array12 = new double[vertexCount2 * 3];
		float[] array13 = new float[vertexCount2 * 3];
		byte[] array14 = new byte[vertexCount2 * 8];
		for (int num28 = 0; num28 < vertexCount2; num28++)
		{
			double[] m2 = array8[num28];
			double[] m3 = FnMath.Inverse(m2);
			FnMath.Point(m3, part.Pos[num28 * 3], part.Pos[num28 * 3 + 1], part.Pos[num28 * 3 + 2], out var ox, out var oy, out var oz);
			array12[num28 * 3] = ox;
			array12[num28 * 3 + 1] = oy;
			array12[num28 * 3 + 2] = oz;
			double x = part.Nrm[num28 * 3];
			double y = part.Nrm[num28 * 3 + 1];
			double z = part.Nrm[num28 * 3 + 2];
			Unit(ref x, ref y, ref z);
			double num29 = array9[num28 * 3];
			double num30 = array9[num28 * 3 + 1];
			double num31 = array9[num28 * 3 + 2];
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
			Unit(ref num29, ref num30, ref num31);
			double num33 = y * num31 - z * num30;
			double num34 = z * num29 - x * num31;
			double num35 = x * num30 - y * num29;
			bool flag = num33 * array10[num28 * 3] + num34 * array10[num28 * 3 + 1] + num35 * array10[num28 * 3 + 2] >= 0.0;
			FnMath.DirT(m2, x, y, z, out var ox2, out var oy2, out var oz2);
			Unit(ref ox2, ref oy2, ref oz2);
			FnMath.Dir(m3, num29, num30, num31, out var ox3, out var oy3, out var oz3);
			double num36 = ox3 * ox2 + oy3 * oy2 + oz3 * oz2;
			ox3 -= ox2 * num36;
			oy3 -= oy2 * num36;
			oz3 -= oz2 * num36;
			Unit(ref ox3, ref oy3, ref oz3);
			array13[num28 * 3] = (float)ox;
			array13[num28 * 3 + 1] = (float)oy;
			array13[num28 * 3 + 2] = (float)oz;
			int num37 = num28 * 8;
			array14[num37] = FnMesh.SnormByte(ox3);
			array14[num37 + 1] = FnMesh.SnormByte(oy3);
			array14[num37 + 2] = FnMesh.SnormByte(oz3);
			array14[num37 + 3] = 127;
			array14[num37 + 4] = FnMesh.SnormByte(ox2);
			array14[num37 + 5] = FnMesh.SnormByte(oy2);
			array14[num37 + 6] = FnMesh.SnormByte(oz2);
			array14[num37 + 7] = (byte)(flag ? 127 : FnMesh.SnormByte(-1.0));
		}
		int[] array15 = AssignSlots(array3, array6, part.Pos, vertexCount);
		for (int num38 = 0; num38 < vertexCount2; num38++)
		{
			int num39 = array15[num38];
			for (int num40 = 0; num40 < 3; num40++)
			{
				BitConverter.TryWriteBytes(body.AsSpan(lod.PosOffset + 12 * num39 + 4 * num40), array13[num38 * 3 + num40]);
			}
			Array.Copy(array14, num38 * 8, body, lod.TanOffset + 8 * num39, 8);
			FnMesh.WriteUv(body, lod, num39, part.Uv[num38 * 2], part.Uv[num38 * 2 + 1]);
			FnMesh.WriteSkin(body, lod, num39, boneOut.AsSpan(num38 * k, k), wOut.AsSpan(num38 * k, k));
		}
		int[] array16 = new int[lod.IndexCount];
		for (int num41 = 0; num41 < part.Tri.Length; num41++)
		{
			array16[num41] = array15[part.Tri[num41]];
		}
		for (int num42 = part.Tri.Length; num42 < array16.Length; num42++)
		{
			array16[num42] = array15[0];
		}
		FnMesh.WriteTriangles(body, lod, array16);
		FnMesh.FillColors(body, lod, 0, 0, 0, byte.MaxValue);
		return array12;
	}

	internal static void Pack(Dictionary<int, double> acc, Dictionary<int, int> local, int k, Span<byte> bones, Span<byte> weights)
	{
		List<KeyValuePair<int, double>> list = (from kv in acc
			where kv.Value > 0.0 && local.ContainsKey(kv.Key)
			orderby kv.Value descending
			select kv).Take(k).ToList();
		if (list.Count == 0)
		{
			weights[0] = byte.MaxValue;
			return;
		}
		double num = list.Sum((KeyValuePair<int, double> kv) => kv.Value);
		int num2 = 0;
		int index = 0;
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			int num4 = (int)Math.Floor(list[num3].Value / num * 255.0);
			bones[num3] = (byte)local[list[num3].Key];
			weights[num3] = (byte)num4;
			num2 += num4;
			if (num4 > weights[index])
			{
				index = num3;
			}
		}
		weights[index] = (byte)(weights[index] + (255 - num2));
	}

	internal static double SegDist(double x, double y, double z, double[] a, double[] b)
	{
		double num = a[3];
		double num2 = a[7];
		double num3 = a[11];
		double num4 = b[3];
		double num5 = b[7];
		double num6 = b[11];
		double num7 = num4 - num;
		double num8 = num5 - num2;
		double num9 = num6 - num3;
		double num10 = num7 * num7 + num8 * num8 + num9 * num9;
		double num11 = ((num10 < 1E-12) ? 0.0 : Math.Clamp(((x - num) * num7 + (y - num2) * num8 + (z - num3) * num9) / num10, 0.0, 1.0));
		double num12 = num + num7 * num11 - x;
		double num13 = num2 + num8 * num11 - y;
		double num14 = num3 + num9 * num11 - z;
		return Math.Sqrt(num12 * num12 + num13 * num13 + num14 * num14);
	}

	private static double[][] TPoseJoints(FnMeshInfo info, double[][] sm)
	{
		double[][] array = info.Globals();
		double[][] array2 = new double[array.Length][];
		for (int i = 0; i < array.Length; i++)
		{
			FnMath.Point(sm[i], array[i][3], array[i][7], array[i][11], out var ox, out var oy, out var oz);
			array2[i] = new double[16]
			{
				1.0, 0.0, 0.0, ox, 0.0, 1.0, 0.0, oy, 0.0, 0.0,
				1.0, oz, 0.0, 0.0, 0.0, 1.0
			};
		}
		return array2;
	}

	internal static void Unit(ref double x, ref double y, ref double z)
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

	internal static int[] AssignSlots(double[] slotPos, bool[] visible, double[] pts, int slotCount)
	{
		int num = pts.Length / 3;
		SlotGrid slotGrid = new SlotGrid(slotPos, (from i in Enumerable.Range(0, slotCount)
			where visible[i]
			select i).ToList());
		SlotGrid slotGrid2 = new SlotGrid(slotPos, (from i in Enumerable.Range(0, slotCount)
			where !visible[i]
			select i).ToList());
		int[] array = new int[num];
		for (int num2 = 0; num2 < num; num2++)
		{
			double x = pts[num2 * 3];
			double y = pts[num2 * 3 + 1];
			double z = pts[num2 * 3 + 2];
			int num3 = slotGrid.Take(x, y, z);
			if (num3 < 0)
			{
				num3 = slotGrid2.Take(x, y, z);
			}
			if (num3 < 0)
			{
				throw new InvalidDataException("The model has more points than the character's mesh has room for.");
			}
			array[num2] = num3;
		}
		return array;
	}

	private static void CheckFinite(byte[] bulk, IReadOnlyList<FnLod> lods, int count)
	{
		for (int i = 0; i < count; i++)
		{
			double[] array = FnMesh.ReadPositions(bulk, lods[i]);
			foreach (double num in array)
			{
				if (double.IsNaN(num) || double.IsInfinity(num) || Math.Abs(num) > 5000.0)
				{
					throw new InvalidDataException("The model came out broken (a point ran away), so nothing was written.");
				}
			}
		}
	}
}
