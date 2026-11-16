using System;
using System.Collections.Generic;
using System.Windows.Media;
using Drip.Core.FallGuys;

namespace Drip.App;

public sealed class FgCostumeVm
{
	private static readonly Dictionary<string, Brush> Cache = new Dictionary<string, Brush>();

	public FgCostume Costume { get; }

	public string Key => Costume.Key;

	public bool IsFavorite { get; set; }

	public string Mark
	{
		get
		{
			if (!IsFavorite)
			{
				return "";
			}
			return "★ ";
		}
	}

	public string Name => Costume.Name;

	public string Sub { get; }

	public string Initial { get; }

	public Brush Tint { get; }

	public FgCostumeVm(FgCostume c)
	{
		Costume = c;
		string value = ((c.VariantCount > 0) ? $" · {c.VariantCount} variant{((c.VariantCount == 1) ? "" : "s")}" : "");
		Sub = $"Season {c.SeasonLabel.TrimStart('S')} · {c.Kind}{value}";
		Initial = ((c.Name.Length > 0) ? c.Name.Substring(0, 1).ToUpperInvariant() : "?");
		Tint = TintFor(c.Id);
	}

	public static Brush TintFor(string id)
	{
		lock (Cache)
		{
			if (Cache.TryGetValue(id, out Brush value))
			{
				return value;
			}
			int num = 17;
			foreach (char c in id)
			{
				num = num * 31 + c;
			}
			SolidColorBrush solidColorBrush = new SolidColorBrush(FromHsl((uint)num % 360u, 0.55, 0.72));
			solidColorBrush.Freeze();
			Cache[id] = solidColorBrush;
			return solidColorBrush;
		}
	}

	private static Color FromHsl(double h, double s, double l)
	{
		double num = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
		double num2 = num * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
		double num3 = l - num / 2.0;
		double num5;
		double num6;
		double num7;
		if (!(h < 60.0))
		{
			if (!(h < 120.0))
			{
				if (!(h < 180.0))
				{
					if (!(h < 240.0))
					{
						if (!(h < 300.0))
						{
							double num4 = num2;
							num5 = num4;
							num6 = 0.0;
							num7 = num;
						}
						else
						{
							double num4 = num;
							num5 = num4;
							num6 = 0.0;
							num7 = num2;
						}
					}
					else
					{
						double num4 = num2;
						double num8 = num;
						num5 = num8;
						num6 = num4;
						num7 = 0.0;
					}
				}
				else
				{
					double num8 = num;
					double num4 = num2;
					num5 = num4;
					num6 = num8;
					num7 = 0.0;
				}
			}
			else
			{
				double num4 = num;
				num5 = 0.0;
				num6 = num4;
				num7 = num2;
			}
		}
		else
		{
			double num4 = num2;
			num5 = 0.0;
			num6 = num4;
			num7 = num;
		}
		return Color.FromRgb((byte)((num7 + num3) * 255.0), (byte)((num6 + num3) * 255.0), (byte)((num5 + num3) * 255.0));
	}
}
