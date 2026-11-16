using System;
using System.Collections.Generic;
using System.Linq;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class BuildLooks
{
	public static readonly IReadOnlyList<BuildLook> All = new _003C_003Ez__ReadOnlyArray<BuildLook>(new BuildLook[7]
	{
		new BuildLook("bubble", "Bubblewraps Builds", "Pink bubble wrap.", BubbleBuilds.Generate),
		new BuildLook("og-mobile", "OG Mobile Builds", "Classic bright-blue blueprint with a white grid.", OgMobile),
		new BuildLook("neon", "Neon Grid Builds", "Dark with glowing cyan and magenta lines.", Neon),
		new BuildLook("candy", "Candy Stripe Builds", "Pink and white diagonal stripes.", Candy),
		new BuildLook("checker", "Checker Builds", "Black and white checkerboard.", Checker),
		new BuildLook("lava", "Lava Builds", "Dark crust with glowing orange cracks.", Lava),
		new BuildLook("ice", "Ice Builds", "Pale blue crystal facets.", Ice)
	});

	public static BuildLook? Find(string id)
	{
		return All.FirstOrDefault((BuildLook l) => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}

	private static TexImage Image(int n, Func<int, int, (double r, double g, double b)> px)
	{
		byte[] array = new byte[n * n * 4];
		for (int i = 0; i < n; i++)
		{
			for (int j = 0; j < n; j++)
			{
				(double, double, double) tuple = px(j, i);
				double item = tuple.Item1;
				double item2 = tuple.Item2;
				double item3 = tuple.Item3;
				int num = (i * n + j) * 4;
				array[num] = (byte)Math.Clamp(item, 0.0, 255.0);
				array[num + 1] = (byte)Math.Clamp(item2, 0.0, 255.0);
				array[num + 2] = (byte)Math.Clamp(item3, 0.0, 255.0);
				array[num + 3] = byte.MaxValue;
			}
		}
		return new TexImage
		{
			Width = n,
			Height = n,
			Rgba = array
		};
	}

	private static double Mix(double a, double b, double t)
	{
		return a + (b - a) * Math.Clamp(t, 0.0, 1.0);
	}

	private static double GridDist(int v, double step)
	{
		double num = (double)v % step;
		return Math.Min(num, step - num);
	}

	private static TexImage OgMobile(int n)
	{
		double step = (double)n / 16.0;
		double line = Math.Max(1.2, (double)n / 256.0);
		return Image(n, (int x, int y) =>
		{
			double t = (double)y / (double)n;
			double a = Mix(52.0, 22.0, t);
			double a2 = Mix(150.0, 96.0, t);
			double a3 = Mix(240.0, 200.0, t);
			double num = Math.Min(GridDist(x, step), GridDist(y, step));
			double step2 = step * 4.0;
			double num2 = Math.Min(GridDist(x, step2), GridDist(y, step2));
			double t2 = Math.Clamp(1.0 - num / line, 0.0, 1.0) * 0.55 + Math.Clamp(1.0 - num2 / (line * 1.8), 0.0, 1.0) * 0.45;
			return (r: Mix(a, 255.0, t2), g: Mix(a2, 255.0, t2), b: Mix(a3, 255.0, t2));
		});
	}

	private static TexImage Neon(int n)
	{
		double step = (double)n / 8.0;
		double line = Math.Max(1.5, (double)n / 220.0);
		double glow = step * 0.18;
		return Image(n, (int x, int y) =>
		{
			double num = Math.Min(GridDist(x, step), GridDist(y, step));
			double val = Math.Clamp(1.0 - num / line, 0.0, 1.0);
			double val2 = Math.Exp((0.0 - num) * num / (glow * glow)) * 0.55;
			double t = (double)(x + y) / (2.0 * (double)n);
			double b = Mix(0.0, 255.0, t);
			double b2 = Mix(240.0, 30.0, t);
			double b3 = Mix(255.0, 220.0, t);
			double t2 = Math.Max(val, val2);
			return (r: Mix(10.0, b, t2), g: Mix(8.0, b2, t2), b: Mix(26.0, b3, t2));
		});
	}

	private static TexImage Candy(int n)
	{
		double period = (double)n / 8.0;
		return Image(n, (int x, int y) =>
		{
			double num = (double)(x + y) % period / period;
			bool flag = num >= 0.5;
			double num2 = Math.Min(Math.Min(num, 1.0 - num), Math.Abs(num - 0.5)) * period;
			double t = 0.5 * (1.0 - Math.Clamp(num2 / 2.0, 0.0, 1.0));
			double num3 = 0.94 + 0.06 * Math.Sin(num * Math.PI * 2.0);
			(int, int, int) tuple = (flag ? (255, 96, 170) : (255, 246, 250));
			(double, double, double) tuple2 = (tuple.Item1, tuple.Item2, tuple.Item3);
			tuple = (flag ? (255, 246, 250) : (255, 96, 170));
			(double, double, double) tuple3 = (tuple.Item1, tuple.Item2, tuple.Item3);
			return (r: Mix(tuple2.Item1, tuple3.Item1, t) * num3, g: Mix(tuple2.Item2, tuple3.Item2, t) * num3, b: Mix(tuple2.Item3, tuple3.Item3, t) * num3);
		});
	}

	private static TexImage Checker(int n)
	{
		double cell = (double)n / 8.0;
		return Image(n, (int x, int y) =>
		{
			double num = ((((int)((double)x / cell) + (int)((double)y / cell)) % 2 == 0) ? 24 : 236);
			return (r: num, g: num, b: num);
		});
	}

	private static uint Hash(int x, int y, int salt)
	{
		uint num = (uint)(x * 374761393 + y * 668265263 + salt * 2246822519u);
		uint num2 = (num ^ (num >> 13)) * 1274126177;
		return num2 ^ (num2 >> 16);
	}

	private static double Rnd(int x, int y, int salt)
	{
		return (double)(Hash(x, y, salt) & 0xFFFFFF) / 16777216.0;
	}

	private static (double d1, double d2, int cx, int cy) Voronoi(double px, double py, int cells)
	{
		int num = (int)Math.Floor(px);
		int num2 = (int)Math.Floor(py);
		double num3 = 9.0;
		double num4 = 9.0;
		int item = 0;
		int item2 = 0;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				int num5 = num + j;
				int num6 = num2 + i;
				int num7 = (num5 % cells + cells) % cells;
				int num8 = (num6 % cells + cells) % cells;
				double num9 = (double)num5 + 0.15 + 0.7 * Rnd(num7, num8, 1);
				double num10 = (double)num6 + 0.15 + 0.7 * Rnd(num7, num8, 2);
				double num11 = num9 - px;
				double num12 = num10 - py;
				double num13 = Math.Sqrt(num11 * num11 + num12 * num12);
				if (num13 < num3)
				{
					num4 = num3;
					num3 = num13;
					item = num7;
					item2 = num8;
				}
				else if (num13 < num4)
				{
					num4 = num13;
				}
			}
		}
		return (d1: num3, d2: num4, cx: item, cy: item2);
	}

	private static TexImage Lava(int n)
	{
		double scale = 7.0 / (double)n;
		return Image(n, (int x, int y) =>
		{
			(double d1, double d2, int cx, int cy) tuple = Voronoi(((double)x + 0.5) * scale, ((double)y + 0.5) * scale, 7);
			double item = tuple.d1;
			double item2 = tuple.d2;
			int item3 = tuple.cx;
			int item4 = tuple.cy;
			double num = Math.Clamp(1.0 - (item2 - item) / 0.3, 0.0, 1.0);
			double t = Math.Pow(num, 1.4);
			double num2 = 0.6 + 0.4 * Rnd(item3, item4, 3);
			double a = 38.0 * num2;
			double a2 = 14.0 * num2;
			double a3 = 10.0 * num2;
			return (r: Mix(a, 255.0, t), g: Mix(a2, 70.0 + 140.0 * num, t), b: Mix(a3, 10.0, t));
		});
	}

	private static TexImage Ice(int n)
	{
		double scale = 6.0 / (double)n;
		return Image(n, (int x, int y) =>
		{
			(double d1, double d2, int cx, int cy) tuple = Voronoi(((double)x + 0.5) * scale, ((double)y + 0.5) * scale, 6);
			double item = tuple.d1;
			double item2 = tuple.d2;
			int item3 = tuple.cx;
			int item4 = tuple.cy;
			double t = Rnd(item3, item4, 4);
			double num = Mix(150.0, 205.0, t);
			double num2 = Mix(205.0, 235.0, t);
			double num3 = Mix(240.0, 255.0, t);
			double num4 = Math.Clamp(1.0 - (item2 - item) / 0.1, 0.0, 1.0);
			double num5 = 1.0 - Math.Clamp(item / 0.7, 0.0, 1.0) * 0.18;
			return (r: Mix(num * num5, 255.0, num4 * 0.9), g: Mix(num2 * num5, 255.0, num4 * 0.9), b: Mix(num3 * num5, 255.0, num4 * 0.9));
		});
	}
}
