using System;

namespace Drip.Core.FortniteModels;

public sealed class FnLodFit
{
	public double[] PosT = Array.Empty<double>();

	public int[] Tri = Array.Empty<int>();

	public double[] Uv = Array.Empty<double>();

	public double MedianMove;

	public double P90Move;
}
