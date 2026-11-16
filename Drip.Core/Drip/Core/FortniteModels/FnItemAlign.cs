using System;
using System.Collections.Generic;
using System.IO;

namespace Drip.Core.FortniteModels;

public sealed class FnItemAlign
{
	public double[] R = new double[9] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 };

	public double Scale = 1.0;

	public double[] From = new double[3];

	public double[] To = new double[3];

	public double Score;

	private const int N = 28;

	public static IReadOnlyList<double[]> Rotations { get; } = MakeRotations();

	public void Point(double x, double y, double z, out double ox, out double oy, out double oz)
	{
		x -= From[0];
		y -= From[1];
		z -= From[2];
		ox = (R[0] * x + R[1] * y + R[2] * z) * Scale + To[0];
		oy = (R[3] * x + R[4] * y + R[5] * z) * Scale + To[1];
		oz = (R[6] * x + R[7] * y + R[8] * z) * Scale + To[2];
	}

	public void Dir(double x, double y, double z, out double ox, out double oy, out double oz)
	{
		ox = R[0] * x + R[1] * y + R[2] * z;
		oy = R[3] * x + R[4] * y + R[5] * z;
		oz = R[6] * x + R[7] * y + R[8] * z;
	}

	private static List<double[]> MakeRotations()
	{
		List<double[]> list = new List<double[]>();
		int[][] array = new int[6][]
		{
			new int[3] { 0, 1, 2 },
			new int[3] { 0, 2, 1 },
			new int[3] { 1, 0, 2 },
			new int[3] { 1, 2, 0 },
			new int[3] { 2, 0, 1 },
			new int[3] { 2, 1, 0 }
		};
		foreach (int[] array2 in array)
		{
			for (int j = 0; j < 8; j++)
			{
				double[] array3 = new double[9];
				for (int k = 0; k < 3; k++)
				{
					array3[k * 3 + array2[k]] = ((((j >> k) & 1) == 0) ? 1 : (-1));
				}
				if (array3[0] * (array3[4] * array3[8] - array3[5] * array3[7]) - array3[1] * (array3[3] * array3[8] - array3[5] * array3[6]) + array3[2] * (array3[3] * array3[7] - array3[4] * array3[6]) > 0.0)
				{
					list.Add(array3);
				}
			}
		}
		return list;
	}

	public static FnItemAlign Fit(double[] refPos, int[] refTri, double[] outPos, int[] outTri)
	{
		double[] array = Sample(refPos, refTri, 6000);
		double[] array2 = Sample(outPos, outTri, 6000);
		if (array.Length == 0 || array2.Length == 0)
		{
			throw new InvalidDataException("There is no shape to line the model up with.");
		}
		Bounds(array, out double[] mn, out double[] mx);
		double[] array3 = new double[3]
		{
			(mn[0] + mx[0]) / 2.0,
			(mn[1] + mx[1]) / 2.0,
			(mn[2] + mx[2]) / 2.0
		};
		double num = Math.Max(1E-06, Math.Max(mx[0] - mn[0], Math.Max(mx[1] - mn[1], mx[2] - mn[2])));
		Bounds(array2, out double[] mn2, out double[] mx2);
		double[] array4 = new double[3]
		{
			(mn2[0] + mx2[0]) / 2.0,
			(mn2[1] + mx2[1]) / 2.0,
			(mn2[2] + mx2[2]) / 2.0
		};
		double num2 = num * 1.4;
		double cell = num2 / 28.0;
		double[] origin = new double[3]
		{
			array3[0] - num2 / 2.0,
			array3[1] - num2 / 2.0,
			array3[2] - num2 / 2.0
		};
		bool[] array5 = Dilate(Occupy(array, origin, cell, null, 1.0, array4, array3));
		FnItemAlign fnItemAlign = null;
		foreach (double[] rotation in Rotations)
		{
			double val = 0.0;
			for (int i = 0; i < 3; i++)
			{
				double num3 = double.MaxValue;
				double num4 = double.MinValue;
				for (int j = 0; j < array2.Length; j += 3)
				{
					double val2 = rotation[i * 3] * (array2[j] - array4[0]) + rotation[i * 3 + 1] * (array2[j + 1] - array4[1]) + rotation[i * 3 + 2] * (array2[j + 2] - array4[2]);
					num3 = Math.Min(num3, val2);
					num4 = Math.Max(num4, val2);
				}
				val = Math.Max(val, num4 - num3);
			}
			double scale = num / Math.Max(val, 1E-06);
			bool[] array6 = Dilate(Occupy(array2, origin, cell, rotation, scale, array4, array3));
			int num5 = 0;
			int num6 = 0;
			for (int k = 0; k < array5.Length; k++)
			{
				if (array5[k] && array6[k])
				{
					num5++;
				}
				if (array5[k] || array6[k])
				{
					num6++;
				}
			}
			double num7 = ((num6 == 0) ? 0.0 : ((double)num5 / (double)num6));
			if (fnItemAlign == null || num7 > fnItemAlign.Score + 1E-09)
			{
				fnItemAlign = new FnItemAlign
				{
					R = rotation,
					Scale = scale,
					From = array4,
					To = array3,
					Score = num7
				};
			}
		}
		return fnItemAlign;
	}

	private static bool[] Occupy(double[] pts, double[] origin, double cell, double[]? rot, double scale, double[] from, double[] to)
	{
		bool[] array = new bool[21952];
		for (int i = 0; i < pts.Length; i += 3)
		{
			double num = pts[i] - from[0];
			double num2 = pts[i + 1] - from[1];
			double num3 = pts[i + 2] - from[2];
			double num4;
			double num5;
			double num6;
			if (rot == null)
			{
				num4 = pts[i];
				num5 = pts[i + 1];
				num6 = pts[i + 2];
			}
			else
			{
				num4 = (rot[0] * num + rot[1] * num2 + rot[2] * num3) * scale + to[0];
				num5 = (rot[3] * num + rot[4] * num2 + rot[5] * num3) * scale + to[1];
				num6 = (rot[6] * num + rot[7] * num2 + rot[8] * num3) * scale + to[2];
			}
			int num7 = (int)Math.Floor((num4 - origin[0]) / cell);
			int num8 = (int)Math.Floor((num5 - origin[1]) / cell);
			int num9 = (int)Math.Floor((num6 - origin[2]) / cell);
			if (num7 >= 0 && num8 >= 0 && num9 >= 0 && num7 < 28 && num8 < 28 && num9 < 28)
			{
				array[(num9 * 28 + num8) * 28 + num7] = true;
			}
		}
		return array;
	}

	private static bool[] Dilate(bool[] g)
	{
		bool[] array = new bool[g.Length];
		for (int i = 0; i < 28; i++)
		{
			for (int j = 0; j < 28; j++)
			{
				for (int k = 0; k < 28; k++)
				{
					if (!g[(i * 28 + j) * 28 + k])
					{
						continue;
					}
					for (int l = -1; l <= 1; l++)
					{
						for (int m = -1; m <= 1; m++)
						{
							for (int n = -1; n <= 1; n++)
							{
								int num = k + n;
								int num2 = j + m;
								int num3 = i + l;
								if (num >= 0 && num2 >= 0 && num3 >= 0 && num < 28 && num2 < 28 && num3 < 28)
								{
									array[(num3 * 28 + num2) * 28 + num] = true;
								}
							}
						}
					}
				}
			}
		}
		return array;
	}

	private static void Bounds(double[] p, out double[] mn, out double[] mx)
	{
		mn = new double[3]
		{
			double.MaxValue,
			double.MaxValue,
			double.MaxValue
		};
		mx = new double[3]
		{
			double.MinValue,
			double.MinValue,
			double.MinValue
		};
		for (int i = 0; i < p.Length; i += 3)
		{
			for (int j = 0; j < 3; j++)
			{
				mn[j] = Math.Min(mn[j], p[i + j]);
				mx[j] = Math.Max(mx[j], p[i + j]);
			}
		}
	}

	internal static double[] Sample(double[] pos, int[] tri, int target)
	{
		double num = 0.0;
		for (int i = 0; i + 2 < tri.Length; i += 3)
		{
			num += TriArea(pos, tri[i], tri[i + 1], tri[i + 2]);
		}
		if (num <= 0.0)
		{
			return Array.Empty<double>();
		}
		double num2 = Math.Sqrt(num / (double)Math.Max(100, target));
		List<double> list = new List<double>(target * 3);
		for (int j = 0; j + 2 < tri.Length; j += 3)
		{
			int num3 = tri[j] * 3;
			int num4 = tri[j + 1] * 3;
			int num5 = tri[j + 2] * 3;
			int num6 = Math.Clamp((int)Math.Ceiling(Math.Max(Dist(pos, num3, num4), Math.Max(Dist(pos, num4, num5), Dist(pos, num3, num5))) / num2), 1, 40);
			for (int k = 0; k <= num6; k++)
			{
				for (int l = 0; l + k <= num6; l++)
				{
					double num7 = (double)k / (double)num6;
					double num8 = (double)l / (double)num6;
					double num9 = 1.0 - num7 - num8;
					for (int m = 0; m < 3; m++)
					{
						list.Add(pos[num3 + m] * num9 + pos[num4 + m] * num7 + pos[num5 + m] * num8);
					}
				}
			}
		}
		return list.ToArray();
	}

	private static double Dist(double[] p, int a, int b)
	{
		return Math.Sqrt((p[a] - p[b]) * (p[a] - p[b]) + (p[a + 1] - p[b + 1]) * (p[a + 1] - p[b + 1]) + (p[a + 2] - p[b + 2]) * (p[a + 2] - p[b + 2]));
	}

	private static double TriArea(double[] p, int a, int b, int c)
	{
		double num = p[b * 3] - p[a * 3];
		double num2 = p[b * 3 + 1] - p[a * 3 + 1];
		double num3 = p[b * 3 + 2] - p[a * 3 + 2];
		double num4 = p[c * 3] - p[a * 3];
		double num5 = p[c * 3 + 1] - p[a * 3 + 1];
		double num6 = p[c * 3 + 2] - p[a * 3 + 2];
		double num7 = num2 * num6 - num3 * num5;
		double num8 = num3 * num4 - num * num6;
		double num9 = num * num5 - num2 * num4;
		return 0.5 * Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
	}
}
