using System;
using System.Collections.Generic;
using System.IO;

namespace Drip.Core.FortniteModels;

public sealed class SurfaceIndex
{
	private readonly float[] _sx;

	private readonly float[] _sy;

	private readonly float[] _sz;

	private readonly int[] _tri;

	private readonly float[] _bu;

	private readonly float[] _bv;

	private readonly int[] _cellStart;

	private readonly int[] _cellItems;

	private readonly int _nx;

	private readonly int _ny;

	private readonly int _nz;

	private readonly double _ox;

	private readonly double _oy;

	private readonly double _oz;

	private readonly double _cell;

	public int SampleCount => _sx.Length;

	public SurfaceIndex(double[] pos, int[] tri, double spacing = 0.25, long maxSamples = 3000000L)
	{
		int num = tri.Length / 3;
		double num2 = 0.0;
		for (int i = 0; i < num; i++)
		{
			num2 += TriArea(pos, tri, i);
		}
		double num3 = Math.Max(spacing, Math.Sqrt(num2 / Math.Max(1.0, (double)maxSamples / 2.0)));
		List<float> list = new List<float>();
		List<float> list2 = new List<float>();
		List<float> list3 = new List<float>();
		List<int> list4 = new List<int>();
		List<float> list5 = new List<float>();
		List<float> list6 = new List<float>();
		for (int j = 0; j < num; j++)
		{
			int num4 = tri[j * 3] * 3;
			int num5 = tri[j * 3 + 1] * 3;
			int num6 = tri[j * 3 + 2] * 3;
			double num7 = pos[num4];
			double num8 = pos[num4 + 1];
			double num9 = pos[num4 + 2];
			double num10 = pos[num5];
			double num11 = pos[num5 + 1];
			double num12 = pos[num5 + 2];
			double num13 = pos[num6];
			double num14 = pos[num6 + 1];
			double num15 = pos[num6 + 2];
			double num16 = Math.Max(Dist(num7, num8, num9, num10, num11, num12), Math.Max(Dist(num7, num8, num9, num13, num14, num15), Dist(num10, num11, num12, num13, num14, num15)));
			if (double.IsNaN(num16))
			{
				continue;
			}
			int num17 = Math.Max(1, (int)Math.Ceiling(num16 / num3));
			for (int k = 0; k <= num17; k++)
			{
				for (int l = 0; k + l <= num17; l++)
				{
					double num18 = (double)k / (double)num17;
					double num19 = (double)l / (double)num17;
					double num20 = 1.0 - num18 - num19;
					list.Add((float)(num20 * num7 + num18 * num10 + num19 * num13));
					list2.Add((float)(num20 * num8 + num18 * num11 + num19 * num14));
					list3.Add((float)(num20 * num9 + num18 * num12 + num19 * num15));
					list4.Add(j);
					list5.Add((float)num18);
					list6.Add((float)num19);
				}
			}
		}
		if (list.Count == 0)
		{
			throw new InvalidDataException("The model has no usable surface.");
		}
		_sx = list.ToArray();
		_sy = list2.ToArray();
		_sz = list3.ToArray();
		_tri = list4.ToArray();
		_bu = list5.ToArray();
		_bv = list6.ToArray();
		double num21 = double.MaxValue;
		double num22 = double.MaxValue;
		double num23 = double.MaxValue;
		double num24 = double.MinValue;
		double num25 = double.MinValue;
		double num26 = double.MinValue;
		for (int m = 0; m < _sx.Length; m++)
		{
			num21 = Math.Min(num21, _sx[m]);
			num24 = Math.Max(num24, _sx[m]);
			num22 = Math.Min(num22, _sy[m]);
			num25 = Math.Max(num25, _sy[m]);
			num23 = Math.Min(num23, _sz[m]);
			num26 = Math.Max(num26, _sz[m]);
		}
		_cell = 2.0;
		_ox = num21;
		_oy = num22;
		_oz = num23;
		_nx = (int)((num24 - num21) / _cell) + 1;
		_ny = (int)((num25 - num22) / _cell) + 1;
		_nz = (int)((num26 - num23) / _cell) + 1;
		long num27 = (long)_nx * (long)_ny * _nz;
		if (num27 > 40000000)
		{
			throw new InvalidDataException("The model is far too big or spread out to fit.");
		}
		_cellStart = new int[num27 + 1];
		for (int n = 0; n < _sx.Length; n++)
		{
			_cellStart[CellOf(_sx[n], _sy[n], _sz[n]) + 1]++;
		}
		for (int num28 = 0; num28 < num27; num28++)
		{
			_cellStart[num28 + 1] += _cellStart[num28];
		}
		int[] array = new int[num27];
		_cellItems = new int[_sx.Length];
		for (int num29 = 0; num29 < _sx.Length; num29++)
		{
			int num30 = CellOf(_sx[num29], _sy[num29], _sz[num29]);
			_cellItems[_cellStart[num30] + array[num30]++] = num29;
		}
	}

	private static double Dist(double ax, double ay, double az, double bx, double by, double bz)
	{
		return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz));
	}

	private static double TriArea(double[] p, int[] t, int i)
	{
		int num = t[i * 3] * 3;
		int num2 = t[i * 3 + 1] * 3;
		int num3 = t[i * 3 + 2] * 3;
		double num4 = p[num2] - p[num];
		double num5 = p[num2 + 1] - p[num + 1];
		double num6 = p[num2 + 2] - p[num + 2];
		double num7 = p[num3] - p[num];
		double num8 = p[num3 + 1] - p[num + 1];
		double num9 = p[num3 + 2] - p[num + 2];
		double num10 = num5 * num9 - num6 * num8;
		double num11 = num6 * num7 - num4 * num9;
		double num12 = num4 * num8 - num5 * num7;
		return 0.5 * Math.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
	}

	private int CellOf(double x, double y, double z)
	{
		int num = Math.Clamp((int)((x - _ox) / _cell), 0, _nx - 1);
		int num2 = Math.Clamp((int)((y - _oy) / _cell), 0, _ny - 1);
		return (Math.Clamp((int)((z - _oz) / _cell), 0, _nz - 1) * _ny + num2) * _nx + num;
	}

	public double Nearest(double x, double y, double z, out double qx, out double qy, out double qz, out int tri, out double bu, out double bv)
	{
		int num = (int)Math.Floor((x - _ox) / _cell);
		int num2 = (int)Math.Floor((y - _oy) / _cell);
		int num3 = (int)Math.Floor((z - _oz) / _cell);
		double num4 = double.MaxValue;
		int num5 = -1;
		int num6 = Math.Max(_nx, Math.Max(_ny, _nz)) + Math.Max(Math.Abs(num), Math.Max(Math.Abs(num2), Math.Abs(num3))) + 2;
		for (int i = 0; i <= num6; i++)
		{
			double num7 = (double)(i - 1) * _cell;
			if (num5 >= 0 && num7 >= 0.0 && num4 <= num7 * num7)
			{
				break;
			}
			for (int j = -i; j <= i; j++)
			{
				int num8 = num3 + j;
				if (num8 < 0 || num8 >= _nz)
				{
					continue;
				}
				for (int k = -i; k <= i; k++)
				{
					int num9 = num2 + k;
					if (num9 < 0 || num9 >= _ny)
					{
						continue;
					}
					int num10 = ((Math.Abs(j) == i || Math.Abs(k) == i) ? 1 : Math.Max(1, 2 * i));
					for (int l = -i; l <= i; l += num10)
					{
						int num11 = num + l;
						if (num11 < 0 || num11 >= _nx)
						{
							continue;
						}
						int num12 = (num8 * _ny + num9) * _nx + num11;
						for (int m = _cellStart[num12]; m < _cellStart[num12 + 1]; m++)
						{
							int num13 = _cellItems[m];
							double num14 = (double)_sx[num13] - x;
							double num15 = (double)_sy[num13] - y;
							double num16 = (double)_sz[num13] - z;
							double num17 = num14 * num14 + num15 * num15 + num16 * num16;
							if (num17 < num4)
							{
								num4 = num17;
								num5 = num13;
							}
						}
					}
				}
			}
		}
		if (num5 < 0)
		{
			qx = (qy = (qz = 0.0));
			tri = 0;
			bu = (bv = 0.0);
			return double.MaxValue;
		}
		qx = _sx[num5];
		qy = _sy[num5];
		qz = _sz[num5];
		tri = _tri[num5];
		bu = _bu[num5];
		bv = _bv[num5];
		return Math.Sqrt(num4);
	}
}
