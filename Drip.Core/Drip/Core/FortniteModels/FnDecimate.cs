using System;
using System.Collections.Generic;
using System.IO;

namespace Drip.Core.FortniteModels;

public static class FnDecimate
{
	public static FnPart Reduce(FnPart src, int maxVerts, int maxTris, Action<string>? log = null)
	{
		FnPart fnPart = src.Compact();
		if (fnPart.VertexCount <= maxVerts && fnPart.TriCount <= maxTris)
		{
			return fnPart;
		}
		int vertexCount = fnPart.VertexCount;
		int triCount = fnPart.TriCount;
		FnPart fnPart2 = Collapse(fnPart, maxVerts, maxTris).Compact();
		if (fnPart2.VertexCount > maxVerts || fnPart2.TriCount > maxTris)
		{
			log?.Invoke($"Edge collapse stopped at {fnPart2.VertexCount:N0} points; merging the rest on a grid.");
			fnPart2 = Cluster(fnPart2, maxVerts, maxTris);
		}
		if (fnPart2.VertexCount > maxVerts || fnPart2.TriCount > maxTris)
		{
			throw new InvalidDataException($"That model is too detailed to fit onto the character ({vertexCount:N0} points, {triCount:N0} triangles; the limit is {maxVerts:N0} points and {maxTris:N0} triangles). Reduce it in a 3D program first.");
		}
		log?.Invoke($"Reduced {vertexCount:N0} -> {fnPart2.VertexCount:N0} points and {triCount:N0} -> {fnPart2.TriCount:N0} triangles.");
		return fnPart2;
	}

	private static FnPart Collapse(FnPart m, int maxVerts, int maxTris)
	{
		int vertexCount = m.VertexCount;
		int triCount = m.TriCount;
		double[] P = m.Pos;
		int[] array = (int[])m.Tri.Clone();
		bool[] array2 = new bool[triCount];
		Array.Fill(array2, value: true);
		bool[] vertAlive = new bool[vertexCount];
		Array.Fill(vertAlive, value: true);
		List<int>[] array3 = new List<int>[vertexCount];
		for (int i = 0; i < vertexCount; i++)
		{
			array3[i] = new List<int>(6);
		}
		for (int j = 0; j < triCount; j++)
		{
			for (int k = 0; k < 3; k++)
			{
				array3[array[j * 3 + k]].Add(j);
			}
		}
		int[] array4 = new int[vertexCount];
		Dictionary<(long, long, long), int> dictionary = new Dictionary<(long, long, long), int>();
		for (int l = 0; l < vertexCount; l++)
		{
			(long, long, long) key = ((long)Math.Round(P[l * 3] * 1000.0), (long)Math.Round(P[l * 3 + 1] * 1000.0), (long)Math.Round(P[l * 3 + 2] * 1000.0));
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = dictionary.Count);
			}
			array4[l] = value;
		}
		int[] array5 = new int[dictionary.Count];
		for (int n = 0; n < vertexCount; n++)
		{
			array5[array4[n]]++;
		}
		bool[] locked = new bool[vertexCount];
		for (int num = 0; num < vertexCount; num++)
		{
			locked[num] = array5[array4[num]] > 1;
		}
		Dictionary<long, int> dictionary2 = new Dictionary<long, int>();
		for (int num2 = 0; num2 < triCount; num2++)
		{
			for (int num3 = 0; num3 < 3; num3++)
			{
				int num4 = array4[array[num2 * 3 + num3]];
				int num5 = array4[array[num2 * 3 + (num3 + 1) % 3]];
				long key2 = ((num4 < num5) ? (((long)num4 << 32) | (uint)num5) : (((long)num5 << 32) | (uint)num4));
				dictionary2[key2] = dictionary2.GetValueOrDefault(key2) + 1;
			}
		}
		for (int num6 = 0; num6 < triCount; num6++)
		{
			for (int num7 = 0; num7 < 3; num7++)
			{
				int num8 = array[num6 * 3 + num7];
				int num9 = array[num6 * 3 + (num7 + 1) % 3];
				int num10 = array4[num8];
				int num11 = array4[num9];
				long key3 = ((num10 < num11) ? (((long)num10 << 32) | (uint)num11) : (((long)num11 << 32) | (uint)num10));
				if (dictionary2[key3] != 2)
				{
					locked[num8] = true;
					locked[num9] = true;
				}
			}
		}
		double[][] Q = new double[vertexCount][];
		for (int num12 = 0; num12 < vertexCount; num12++)
		{
			Q[num12] = new double[10];
		}
		for (int num13 = 0; num13 < triCount; num13++)
		{
			int num14 = array[num13 * 3];
			int num15 = array[num13 * 3 + 1];
			int num16 = array[num13 * 3 + 2];
			double num17 = P[num16 * 3] - P[num14 * 3];
			double num18 = P[num16 * 3 + 1] - P[num14 * 3 + 1];
			double num19 = P[num16 * 3 + 2] - P[num14 * 3 + 2];
			double num20 = P[num15 * 3] - P[num14 * 3];
			double num21 = P[num15 * 3 + 1] - P[num14 * 3 + 1];
			double num22 = P[num15 * 3 + 2] - P[num14 * 3 + 2];
			double num23 = num18 * num22 - num19 * num21;
			double num24 = num19 * num20 - num17 * num22;
			double num25 = num17 * num21 - num18 * num20;
			double num26 = Math.Sqrt(num23 * num23 + num24 * num24 + num25 * num25);
			if (!(num26 < 1E-12))
			{
				double w = num26 / 2.0;
				num23 /= num26;
				num24 /= num26;
				num25 /= num26;
				double d = 0.0 - (num23 * P[num14 * 3] + num24 * P[num14 * 3 + 1] + num25 * P[num14 * 3 + 2]);
				AddPlane(Q[num14], num23, num24, num25, d, w);
				AddPlane(Q[num15], num23, num24, num25, d, w);
				AddPlane(Q[num16], num23, num24, num25, d, w);
			}
		}
		int[] version = new int[vertexCount];
		PriorityQueue<(int U, int V, int VerU, int VerV), double> heap = new PriorityQueue<(int, int, int, int), double>();
		HashSet<long> hashSet = new HashSet<long>();
		for (int num27 = 0; num27 < triCount; num27++)
		{
			for (int num28 = 0; num28 < 3; num28++)
			{
				int num29 = array[num27 * 3 + num28];
				int num30 = array[num27 * 3 + (num28 + 1) % 3];
				long item = ((num29 < num30) ? (((long)num29 << 32) | (uint)num30) : (((long)num30 << 32) | (uint)num29));
				if (hashSet.Add(item))
				{
					Push(num29, num30);
					Push(num30, num29);
				}
			}
		}
		int num31 = triCount;
		int num32 = vertexCount;
		HashSet<int> hashSet2 = new HashSet<int>();
		HashSet<int> hashSet3 = new HashSet<int>();
		(int, int, int, int) element;
		double priority;
		while ((num32 > maxVerts || num31 > maxTris) && heap.TryDequeue(out element, out priority))
		{
			var (num33, num34, _, _) = element;
			if (!vertAlive[num33] || !vertAlive[num34] || version[num33] != element.Item3 || version[num34] != element.Item4)
			{
				continue;
			}
			hashSet2.Clear();
			hashSet3.Clear();
			int num35 = 0;
			foreach (int item2 in array3[num33])
			{
				if (!array2[item2])
				{
					continue;
				}
				bool flag = false;
				for (int num36 = 0; num36 < 3; num36++)
				{
					int num37 = array[item2 * 3 + num36];
					if (num37 != num33)
					{
						hashSet2.Add(num37);
					}
					if (num37 == num34)
					{
						flag = true;
					}
				}
				if (flag)
				{
					num35++;
				}
			}
			foreach (int item3 in array3[num34])
			{
				if (!array2[item3])
				{
					continue;
				}
				for (int num38 = 0; num38 < 3; num38++)
				{
					int num39 = array[item3 * 3 + num38];
					if (num39 != num34)
					{
						hashSet3.Add(num39);
					}
				}
			}
			if (num35 == 0 || num35 > 2)
			{
				continue;
			}
			int num40 = 0;
			foreach (int item4 in hashSet2)
			{
				if (hashSet3.Contains(item4))
				{
					num40++;
				}
			}
			if (num40 != num35)
			{
				continue;
			}
			bool flag2 = true;
			foreach (int item5 in array3[num33])
			{
				if (array2[item5])
				{
					int num41 = array[item5 * 3];
					int num42 = array[item5 * 3 + 1];
					int num43 = array[item5 * 3 + 2];
					if (num41 != num34 && num42 != num34 && num43 != num34 && !KeepsShape(P, num41, num42, num43, num33, num34))
					{
						flag2 = false;
						break;
					}
				}
			}
			if (!flag2)
			{
				continue;
			}
			foreach (int item6 in array3[num33])
			{
				if (!array2[item6])
				{
					continue;
				}
				if (array[item6 * 3] == num34 || array[item6 * 3 + 1] == num34 || array[item6 * 3 + 2] == num34)
				{
					array2[item6] = false;
					num31--;
					continue;
				}
				for (int num44 = 0; num44 < 3; num44++)
				{
					if (array[item6 * 3 + num44] == num33)
					{
						array[item6 * 3 + num44] = num34;
					}
				}
				array3[num34].Add(item6);
			}
			array3[num33].Clear();
			vertAlive[num33] = false;
			num32--;
			for (int num45 = 0; num45 < 10; num45++)
			{
				Q[num34][num45] += Q[num33][num45];
			}
			version[num34]++;
			HashSet<int> hashSet4 = new HashSet<int>();
			foreach (int item7 in array3[num34])
			{
				if (!array2[item7])
				{
					continue;
				}
				for (int num46 = 0; num46 < 3; num46++)
				{
					int num47 = array[item7 * 3 + num46];
					if (num47 != num34)
					{
						hashSet4.Add(num47);
					}
				}
			}
			foreach (int item8 in hashSet4)
			{
				Push(num34, item8);
				Push(item8, num34);
			}
		}
		List<int> list = new List<int>();
		for (int num48 = 0; num48 < triCount; num48++)
		{
			if (array2[num48])
			{
				list.Add(array[num48 * 3]);
				list.Add(array[num48 * 3 + 1]);
				list.Add(array[num48 * 3 + 2]);
			}
		}
		return new FnPart
		{
			Pos = m.Pos,
			Nrm = m.Nrm,
			Uv = m.Uv,
			Mat = m.Mat,
			Tri = list.ToArray()
		};
		void Push(int u, int v)
		{
			if (!locked[u] && vertAlive[u] && vertAlive[v])
			{
				double num49 = P[v * 3];
				double num50 = P[v * 3 + 1];
				double num51 = P[v * 3 + 2];
				double num52 = P[u * 3] - num49;
				double num53 = P[u * 3 + 1] - num50;
				double num54 = P[u * 3 + 2] - num51;
				double priority2 = Eval(Q[u], num49, num50, num51) + Eval(Q[v], num49, num50, num51) + 1E-09 * (num52 * num52 + num53 * num53 + num54 * num54);
				heap.Enqueue((u, v, version[u], version[v]), priority2);
			}
		}
	}

	private static bool KeepsShape(double[] P, int a, int b, int c, int u, int v)
	{
		double x = 0.0;
		double y = 0.0;
		double z = 0.0;
		double x2 = 0.0;
		double y2 = 0.0;
		double z2 = 0.0;
		Normal(P, a, b, c, a, a, out x, out y, out z);
		int num = ((a == u) ? v : a);
		int b2 = ((b == u) ? v : b);
		int c2 = ((c == u) ? v : c);
		Normal(P, num, b2, c2, num, num, out x2, out y2, out z2);
		double num2 = Math.Sqrt(x * x + y * y + z * z);
		double num3 = Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
		if (num2 < 1E-12)
		{
			return true;
		}
		if (num3 < 1E-09 * Math.Max(1.0, num2))
		{
			return false;
		}
		return (x * x2 + y * y2 + z * z2) / (num2 * num3) > 0.25;
	}

	private static void Normal(double[] P, int a, int b, int c, int _, int __, out double x, out double y, out double z)
	{
		double num = P[c * 3] - P[a * 3];
		double num2 = P[c * 3 + 1] - P[a * 3 + 1];
		double num3 = P[c * 3 + 2] - P[a * 3 + 2];
		double num4 = P[b * 3] - P[a * 3];
		double num5 = P[b * 3 + 1] - P[a * 3 + 1];
		double num6 = P[b * 3 + 2] - P[a * 3 + 2];
		x = num2 * num6 - num3 * num5;
		y = num3 * num4 - num * num6;
		z = num * num5 - num2 * num4;
	}

	private static void AddPlane(double[] q, double a, double b, double c, double d, double w)
	{
		q[0] += w * a * a;
		q[1] += w * a * b;
		q[2] += w * a * c;
		q[3] += w * a * d;
		q[4] += w * b * b;
		q[5] += w * b * c;
		q[6] += w * b * d;
		q[7] += w * c * c;
		q[8] += w * c * d;
		q[9] += w * d * d;
	}

	private static double Eval(double[] q, double x, double y, double z)
	{
		return q[0] * x * x + 2.0 * q[1] * x * y + 2.0 * q[2] * x * z + 2.0 * q[3] * x + q[4] * y * y + 2.0 * q[5] * y * z + 2.0 * q[6] * y + q[7] * z * z + 2.0 * q[8] * z + q[9];
	}

	private static FnPart Cluster(FnPart m, int maxVerts, int maxTris)
	{
		double num = 0.05;
		double num2 = 80.0;
		FnPart fnPart = null;
		for (int i = 0; i < 26; i++)
		{
			double num3 = (num + num2) / 2.0;
			FnPart fnPart2 = ClusterWith(m, num3);
			if (fnPart2.VertexCount <= maxVerts && fnPart2.TriCount <= maxTris)
			{
				fnPart = fnPart2;
				num2 = num3;
			}
			else
			{
				num = num3;
			}
		}
		return fnPart ?? ClusterWith(m, num2);
	}

	private static FnPart ClusterWith(FnPart m, double s)
	{
		int vertexCount = m.VertexCount;
		Dictionary<(int, int, int, int, int, int), int> dictionary = new Dictionary<(int, int, int, int, int, int), int>();
		int[] array = new int[vertexCount];
		List<double[]> list = new List<double[]>();
		for (int i = 0; i < vertexCount; i++)
		{
			(int, int, int, int, int, int) key = ((int)Math.Floor(m.Pos[i * 3] / s), (int)Math.Floor(m.Pos[i * 3 + 1] / s), (int)Math.Floor(m.Pos[i * 3 + 2] / s), m.Mat[i], (int)Math.Floor(m.Uv[i * 2] * 16.0), (int)Math.Floor(m.Uv[i * 2 + 1] * 16.0));
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = dictionary.Count);
				list.Add(new double[9]);
			}
			array[i] = value;
			double[] array2 = list[value];
			array2[0] += m.Pos[i * 3];
			array2[1] += m.Pos[i * 3 + 1];
			array2[2] += m.Pos[i * 3 + 2];
			array2[3] += m.Nrm[i * 3];
			array2[4] += m.Nrm[i * 3 + 1];
			array2[5] += m.Nrm[i * 3 + 2];
			array2[6] += m.Uv[i * 2];
			array2[7] += m.Uv[i * 2 + 1];
			array2[8]++;
		}
		int count2 = dictionary.Count;
		FnPart fnPart = new FnPart
		{
			Pos = new double[count2 * 3],
			Nrm = new double[count2 * 3],
			Uv = new double[count2 * 2],
			Mat = new int[count2]
		};
		foreach (var (tuple2, num2) in dictionary)
		{
			double[] array3 = list[num2];
			fnPart.Pos[num2 * 3] = array3[0] / array3[8];
			fnPart.Pos[num2 * 3 + 1] = array3[1] / array3[8];
			fnPart.Pos[num2 * 3 + 2] = array3[2] / array3[8];
			double num3 = Math.Sqrt(array3[3] * array3[3] + array3[4] * array3[4] + array3[5] * array3[5]);
			if (num3 < 1E-09)
			{
				fnPart.Nrm[num2 * 3 + 2] = 1.0;
			}
			else
			{
				fnPart.Nrm[num2 * 3] = array3[3] / num3;
				fnPart.Nrm[num2 * 3 + 1] = array3[4] / num3;
				fnPart.Nrm[num2 * 3 + 2] = array3[5] / num3;
			}
			fnPart.Uv[num2 * 2] = array3[6] / array3[8];
			fnPart.Uv[num2 * 2 + 1] = array3[7] / array3[8];
			fnPart.Mat[num2] = tuple2.Item4;
		}
		List<int> list2 = new List<int>(m.Tri.Length);
		for (int j = 0; j + 2 < m.Tri.Length; j += 3)
		{
			int num4 = array[m.Tri[j]];
			int num5 = array[m.Tri[j + 1]];
			int num6 = array[m.Tri[j + 2]];
			if (num4 != num5 && num5 != num6 && num4 != num6)
			{
				list2.Add(num4);
				list2.Add(num5);
				list2.Add(num6);
			}
		}
		fnPart.Tri = list2.ToArray();
		return fnPart.Compact();
	}
}
