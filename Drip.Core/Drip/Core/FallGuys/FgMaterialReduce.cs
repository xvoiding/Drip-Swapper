using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FallGuys;

/// <summary>Brings a model with lots of materials down to the number Fortnite allows.</summary>
public static class FgMaterialReduce
{
	/// <summary>Returns how many materials were folded away (0 when nothing had to change).</summary>
	public static int Reduce(FgModelSource src, int max)
	{
		int before = src.Materials.Count;
		if (before == 0 || src.Idx.Length < 3)
		{
			return 0;
		}
		int[] usage = new int[before];
		for (int i = 0; i + 2 < src.Idx.Length; i += 3)
		{
			int m = Math.Clamp(src.Mat[src.Idx[i]], 0, before - 1);
			usage[m]++;
		}
		// 1. drop unused, merge copies (same picture or same plain colour)
		Dictionary<string, int> byKey = new Dictionary<string, int>();
		int[] group = new int[before];
		List<int> groupUse = new List<int>();
		List<int> groupFirst = new List<int>();
		for (int m = 0; m < before; m++)
		{
			group[m] = -1;
			if (usage[m] == 0)
			{
				continue;
			}
			string key = Key(src.Materials[m]);
			if (!byKey.TryGetValue(key, out int g))
			{
				g = groupUse.Count;
				byKey[key] = g;
				groupUse.Add(0);
				groupFirst.Add(m);
			}
			group[m] = g;
			groupUse[g] += usage[m];
		}
		if (groupUse.Count == 0)
		{
			return 0;
		}
		List<FgSourceMaterial> mats = groupFirst.Select(i => src.Materials[i]).ToList();
		List<int> use = groupUse;
		// 2. still too many: keep the biggest, fold the rest into a few flat colours
		if (mats.Count > max)
		{
			int keep = Math.Max(1, max - 4);
			List<int> order = Enumerable.Range(0, mats.Count).OrderByDescending(i => use[i]).ToList();
			Dictionary<int, int> newIndex = new Dictionary<int, int>();
			List<FgSourceMaterial> outMats = new List<FgSourceMaterial>();
			List<int> outUse = new List<int>();
			foreach (int i in order.Take(keep))
			{
				newIndex[i] = outMats.Count;
				outMats.Add(mats[i]);
				outUse.Add(use[i]);
			}
			int slots = Math.Max(1, max - keep);
			List<(Vector4 c, int w, List<int> members)> flats = new List<(Vector4, int, List<int>)>();
			foreach (int i in order.Skip(keep))
			{
				flats.Add((AverageColour(mats[i]), use[i], new List<int> { i }));
			}
			while (flats.Count > slots)
			{
				int bi = 0;
				int bj = 1;
				float best = float.MaxValue;
				for (int a = 0; a < flats.Count; a++)
				{
					for (int b = a + 1; b < flats.Count; b++)
					{
						float d = Vector4.DistanceSquared(flats[a].c, flats[b].c);
						if (d < best)
						{
							best = d;
							bi = a;
							bj = b;
						}
					}
				}
				var x = flats[bi];
				var y = flats[bj];
				int w = x.w + y.w;
				Vector4 c = (x.c * x.w + y.c * y.w) / Math.Max(1, w);
				List<int> mem = new List<int>(x.members);
				mem.AddRange(y.members);
				flats.RemoveAt(bj);
				flats[bi] = (c, w, mem);
			}
			foreach (var f in flats)
			{
				int idx = outMats.Count;
				outMats.Add(new FgSourceMaterial
				{
					Name = "merged",
					Color = new Vector4(f.c.X, f.c.Y, f.c.Z, 1f),
					Picture = null
				});
				outUse.Add(f.w);
				foreach (int member in f.members)
				{
					newIndex[member] = idx;
				}
			}
			for (int m = 0; m < before; m++)
			{
				if (group[m] >= 0)
				{
					group[m] = newIndex[group[m]];
				}
			}
			mats = outMats;
		}
		int[] vertexMat = new int[src.Mat.Length];
		for (int v = 0; v < vertexMat.Length; v++)
		{
			int old = Math.Clamp(src.Mat[v], 0, before - 1);
			vertexMat[v] = group[old] >= 0 ? group[old] : 0;
		}
		if (mats.Count == before)
		{
			return 0;
		}
		src.Materials = mats;
		src.Mat = vertexMat;
		return before - mats.Count;
	}

	private static string Key(FgSourceMaterial m)
	{
		if (m.Picture != null && m.Picture.Rgba != null && m.Picture.Rgba.Length > 0)
		{
			ulong h = 1469598103934665603UL;
			byte[] px = m.Picture.Rgba;
			int step = Math.Max(1, px.Length / 4096);
			for (int i = 0; i < px.Length; i += step)
			{
				h = (h ^ px[i]) * 1099511628211UL;
			}
			return "p" + m.Picture.Width + "x" + m.Picture.Height + ":" + h;
		}
		return "c" + m.Color.X.ToString("F3") + "," + m.Color.Y.ToString("F3") + "," + m.Color.Z.ToString("F3");
	}

	private static Vector4 AverageColour(FgSourceMaterial m)
	{
		if (m.Picture != null && m.Picture.Rgba != null && m.Picture.Rgba.Length >= 4)
		{
			byte[] px = m.Picture.Rgba;
			int pixels = px.Length / 4;
			int step = Math.Max(1, pixels / 2048);
			double r = 0, g = 0, b = 0;
			int n = 0;
			for (int i = 0; i < pixels; i += step)
			{
				if (px[i * 4 + 3] < 16)
				{
					continue;
				}
				r += px[i * 4];
				g += px[i * 4 + 1];
				b += px[i * 4 + 2];
				n++;
			}
			if (n > 0)
			{
				return new Vector4((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(b / n / 255.0), 1f);
			}
		}
		return new Vector4(m.Color.X, m.Color.Y, m.Color.Z, 1f);
	}
}
