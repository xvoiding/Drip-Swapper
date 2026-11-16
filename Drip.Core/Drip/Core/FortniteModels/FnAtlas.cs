using System;
using System.Collections.Generic;
using System.Linq;
using Drip.Core.FallGuys;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public sealed class FnAtlas
{
	private readonly int _cols;

	private readonly int _rows;

	private readonly int _size;

	private readonly Dictionary<int, int> _cell;

	private readonly bool _single;

	private readonly bool _hasUv;

	private readonly int _cw;

	private readonly int _ch;

	public const int DefaultSize = 256;

	public TexImage Image { get; }

	private FnAtlas(TexImage img, int cols, int rows, int size, Dictionary<int, int> cell, bool single, bool hasUv)
	{
		Image = img;
		_cols = cols;
		_rows = rows;
		_size = size;
		_cell = cell;
		_single = single;
		_hasUv = hasUv;
		_cw = size / cols;
		_ch = size / rows;
	}

	public static FnAtlas Create(FnTarget t, FnPart m, int size = 256)
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		for (int i = 0; i + 2 < m.Tri.Length; i += 3)
		{
			dictionary[m.Mat[m.Tri[i]]] = dictionary.GetValueOrDefault(m.Mat[m.Tri[i]]) + 1;
		}
		List<int> list = (from kv in dictionary
			orderby kv.Value descending
			select kv.Key).ToList();
		if (list.Count == 0)
		{
			list.Add(0);
		}
		bool flag = list.Count == 1;
		int num = (flag ? 1 : ((int)Math.Ceiling(Math.Sqrt(list.Count))));
		int num2 = (flag ? 1 : ((int)Math.Ceiling((double)list.Count / (double)num)));
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			dictionary2[list[num3]] = num3;
		}
		TexImage texImage = new TexImage
		{
			Width = size,
			Height = size,
			Rgba = new byte[size * size * 4]
		};
		for (int num4 = 3; num4 < texImage.Rgba.Length; num4 += 4)
		{
			texImage.Rgba[num4] = byte.MaxValue;
		}
		int num5 = size / num;
		int num6 = size / num2;
		for (int num7 = 0; num7 < list.Count; num7++)
		{
			int num8 = num7 % num * num5;
			int num9 = num7 / num * num6;
			int num10 = (flag ? size : num5);
			int num11 = (flag ? size : num6);
			FgSourceMaterial fgSourceMaterial = t.Materials[Math.Clamp(list[num7], 0, t.Materials.Count - 1)];
			TexImage texImage2 = PictureOf(list[num7]);
			TexImage texImage3;
			if (texImage2 != null)
			{
				texImage3 = ((texImage2.Width <= 128 && texImage2.Height <= 128) ? texImage2.Nearest(num10, num11) : texImage2.Resample(num10, num11));
			}
			else
			{
				texImage3 = new TexImage
				{
					Width = num10,
					Height = num11,
					Rgba = new byte[num10 * num11 * 4]
				};
				byte b = (byte)Math.Clamp((int)Math.Round(fgSourceMaterial.Color.X * 255f), 0, 255);
				byte b2 = (byte)Math.Clamp((int)Math.Round(fgSourceMaterial.Color.Y * 255f), 0, 255);
				byte b3 = (byte)Math.Clamp((int)Math.Round(fgSourceMaterial.Color.Z * 255f), 0, 255);
				for (int num12 = 0; num12 < num10 * num11; num12++)
				{
					texImage3.Rgba[num12 * 4] = b;
					texImage3.Rgba[num12 * 4 + 1] = b2;
					texImage3.Rgba[num12 * 4 + 2] = b3;
					texImage3.Rgba[num12 * 4 + 3] = byte.MaxValue;
				}
			}
			for (int num13 = 0; num13 < num11; num13++)
			{
				for (int num14 = 0; num14 < num10; num14++)
				{
					int num15 = (num13 * num10 + num14) * 4;
					int num16 = ((num9 + num13) * size + num8 + num14) * 4;
					texImage.Rgba[num16] = texImage3.Rgba[num15];
					texImage.Rgba[num16 + 1] = texImage3.Rgba[num15 + 1];
					texImage.Rgba[num16 + 2] = texImage3.Rgba[num15 + 2];
					texImage.Rgba[num16 + 3] = byte.MaxValue;
				}
			}
		}
		bool flag2 = t.Override != null || t.HasUv;
		return new FnAtlas(texImage, num, num2, size, dictionary2, flag, flag2 && list.Any((int u) => PictureOf(u) != null));
		TexImage? PictureOf(int mat)
		{
			TexImage? texImage4 = t.Override;
			if (texImage4 == null)
			{
				if (!t.HasUv)
				{
					return null;
				}
				texImage4 = t.Materials[Math.Clamp(mat, 0, t.Materials.Count - 1)].Picture;
			}
			return texImage4;
		}
	}

	public FnPart Apply(FnPart m)
	{
		Dictionary<(int, int, int), int> dictionary = new Dictionary<(int, int, int), int>();
		List<double> list = new List<double>();
		List<double> list2 = new List<double>();
		List<double> list3 = new List<double>();
		List<int> list4 = new List<int>();
		int[] array = new int[m.Tri.Length];
		for (int i = 0; i + 2 < m.Tri.Length; i += 3)
		{
			double num = 0.0;
			double num2 = 0.0;
			for (int j = 0; j < 3; j++)
			{
				num += m.Uv[m.Tri[i + j] * 2];
				num2 += 1.0 - m.Uv[m.Tri[i + j] * 2 + 1];
			}
			int num3 = (int)Math.Floor(num / 3.0);
			int num4 = (int)Math.Floor(num2 / 3.0);
			for (int k = 0; k < 3; k++)
			{
				int num5 = m.Tri[i + k];
				if (!dictionary.TryGetValue((num5, num3, num4), out var value))
				{
					value = dictionary.Count;
					dictionary[(num5, num3, num4)] = value;
					list.Add(m.Pos[num5 * 3]);
					list.Add(m.Pos[num5 * 3 + 1]);
					list.Add(m.Pos[num5 * 3 + 2]);
					list2.Add(m.Nrm[num5 * 3]);
					list2.Add(m.Nrm[num5 * 3 + 1]);
					list2.Add(m.Nrm[num5 * 3 + 2]);
					list4.Add(m.Mat[num5]);
					var (item, item2) = Place(m.Mat[num5], m.Uv[num5 * 2] - (double)num3, 1.0 - m.Uv[num5 * 2 + 1] - (double)num4);
					list3.Add(item);
					list3.Add(item2);
				}
				array[i + k] = value;
			}
		}
		return new FnPart
		{
			Pos = list.ToArray(),
			Nrm = list2.ToArray(),
			Uv = list3.ToArray(),
			Mat = list4.ToArray(),
			Tri = array
		};
	}

	private (double U, double V) Place(int material, double u, double v)
	{
		if (_single && _hasUv)
		{
			return (U: Math.Clamp(u, 0.0, 1.0), V: Math.Clamp(v, 0.0, 1.0));
		}
		int num = (_cell.TryGetValue(material, out var value) ? value : 0);
		double num2 = num % _cols * _cw;
		double num3 = num / _cols * _ch;
		if (!_hasUv || _single)
		{
			return (U: (num2 + (double)_cw / 2.0) / (double)_size, V: (num3 + (double)_ch / 2.0) / (double)_size);
		}
		double num4 = Math.Clamp(u, 0.0, 1.0);
		double num5 = Math.Clamp(v, 0.0, 1.0);
		return (U: (num2 + 0.5 + num4 * (double)(_cw - 1)) / (double)_size, V: (num3 + 0.5 + num5 * (double)(_ch - 1)) / (double)_size);
	}
}
