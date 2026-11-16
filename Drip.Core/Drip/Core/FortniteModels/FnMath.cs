using System;

namespace Drip.Core.FortniteModels;

public static class FnMath
{
	public static double[] Identity()
	{
		return new double[16]
		{
			1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0,
			1.0, 0.0, 0.0, 0.0, 0.0, 1.0
		};
	}

	public static double[] FromQuatTrs(double[] d)
	{
		double num = d[0];
		double num2 = d[1];
		double num3 = d[2];
		double num4 = d[3];
		double[,] array = new double[3, 3]
		{
			{
				1.0 - 2.0 * (num2 * num2 + num3 * num3),
				2.0 * (num * num2 - num3 * num4),
				2.0 * (num * num3 + num2 * num4)
			},
			{
				2.0 * (num * num2 + num3 * num4),
				1.0 - 2.0 * (num * num + num3 * num3),
				2.0 * (num2 * num3 - num * num4)
			},
			{
				2.0 * (num * num3 - num2 * num4),
				2.0 * (num2 * num3 + num * num4),
				1.0 - 2.0 * (num * num + num2 * num2)
			}
		};
		double[] array2 = Identity();
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				array2[i * 4 + j] = array[i, j] * d[7 + j];
			}
		}
		array2[3] = d[4];
		array2[7] = d[5];
		array2[11] = d[6];
		return array2;
	}

	public static double[] Mul(double[] a, double[] b)
	{
		double[] array = new double[16];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				double num = 0.0;
				for (int k = 0; k < 4; k++)
				{
					num += a[i * 4 + k] * b[k * 4 + j];
				}
				array[i * 4 + j] = num;
			}
		}
		return array;
	}

	public static double[] Inverse(double[] m)
	{
		double[,] array = new double[4, 8];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				array[i, j] = m[i * 4 + j];
			}
			array[i, 4 + i] = 1.0;
		}
		for (int k = 0; k < 4; k++)
		{
			int num = k;
			for (int l = k + 1; l < 4; l++)
			{
				if (Math.Abs(array[l, k]) > Math.Abs(array[num, k]))
				{
					num = l;
				}
			}
			if (Math.Abs(array[num, k]) < 1E-14)
			{
				return Identity();
			}
			if (num != k)
			{
				for (int n = 0; n < 8; n++)
				{
					ref double reference = ref array[k, n];
					ref double reference2 = ref array[num, n];
					double num2 = array[num, n];
					double num3 = array[k, n];
					reference = num2;
					reference2 = num3;
				}
			}
			double num4 = array[k, k];
			for (int num5 = 0; num5 < 8; num5++)
			{
				array[k, num5] /= num4;
			}
			for (int num6 = 0; num6 < 4; num6++)
			{
				if (num6 == k)
				{
					continue;
				}
				double num7 = array[num6, k];
				if (num7 != 0.0)
				{
					for (int num8 = 0; num8 < 8; num8++)
					{
						array[num6, num8] -= num7 * array[k, num8];
					}
				}
			}
		}
		double[] array2 = new double[16];
		for (int num9 = 0; num9 < 4; num9++)
		{
			for (int num10 = 0; num10 < 4; num10++)
			{
				array2[num9 * 4 + num10] = array[num9, 4 + num10];
			}
		}
		return array2;
	}

	public static void Point(double[] m, double x, double y, double z, out double ox, out double oy, out double oz)
	{
		ox = m[0] * x + m[1] * y + m[2] * z + m[3];
		oy = m[4] * x + m[5] * y + m[6] * z + m[7];
		oz = m[8] * x + m[9] * y + m[10] * z + m[11];
	}

	public static void Dir(double[] m, double x, double y, double z, out double ox, out double oy, out double oz)
	{
		ox = m[0] * x + m[1] * y + m[2] * z;
		oy = m[4] * x + m[5] * y + m[6] * z;
		oz = m[8] * x + m[9] * y + m[10] * z;
	}

	public static void DirT(double[] m, double x, double y, double z, out double ox, out double oy, out double oz)
	{
		ox = m[0] * x + m[4] * y + m[8] * z;
		oy = m[1] * x + m[5] * y + m[9] * z;
		oz = m[2] * x + m[6] * y + m[10] * z;
	}

	public static double[] RotAlign(double ax, double ay, double az, double bx, double by, double bz)
	{
		double num = Math.Sqrt(ax * ax + ay * ay + az * az);
		double num2 = Math.Sqrt(bx * bx + by * by + bz * bz);
		if (num < 1E-12 || num2 < 1E-12)
		{
			return new double[9] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 };
		}
		ax /= num;
		ay /= num;
		az /= num;
		bx /= num2;
		by /= num2;
		bz /= num2;
		double num3 = ay * bz - az * by;
		double num4 = az * bx - ax * bz;
		double num5 = ax * by - ay * bx;
		double num6 = ax * bx + ay * by + az * bz;
		if (num6 < -0.99999)
		{
			double num7 = 0.0;
			double num8 = 0.0;
			double num9 = 0.0;
			if (Math.Abs(ax) < 0.9)
			{
				num7 = 0.0;
				num8 = az;
				num9 = 0.0 - ay;
			}
			else
			{
				num7 = 0.0 - az;
				num8 = 0.0;
				num9 = ax;
			}
			double num10 = Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
			num7 /= num10;
			num8 /= num10;
			num9 /= num10;
			return new double[9]
			{
				2.0 * num7 * num7 - 1.0,
				2.0 * num7 * num8,
				2.0 * num7 * num9,
				2.0 * num8 * num7,
				2.0 * num8 * num8 - 1.0,
				2.0 * num8 * num9,
				2.0 * num9 * num7,
				2.0 * num9 * num8,
				2.0 * num9 * num9 - 1.0
			};
		}
		double num11 = 1.0 / (1.0 + num6);
		double[,] array = new double[3, 3]
		{
			{
				0.0,
				0.0 - num5,
				num4
			},
			{
				num5,
				0.0,
				0.0 - num3
			},
			{
				0.0 - num4,
				num3,
				0.0
			}
		};
		double[] array2 = new double[9];
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				double num12 = (double)((i == j) ? 1 : 0) + array[i, j];
				double num13 = 0.0;
				for (int k = 0; k < 3; k++)
				{
					num13 += array[i, k] * array[k, j];
				}
				array2[i * 3 + j] = num12 + num13 * num11;
			}
		}
		return array2;
	}

	public static void Rot3(double[] r, double x, double y, double z, out double ox, out double oy, out double oz)
	{
		ox = r[0] * x + r[1] * y + r[2] * z;
		oy = r[3] * x + r[4] * y + r[5] * z;
		oz = r[6] * x + r[7] * y + r[8] * z;
	}
}
