using System;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public sealed class FnPreviewPart
{
	public double[] Pos = Array.Empty<double>();

	public int[] Tri = Array.Empty<int>();

	public double[] Uv = Array.Empty<double>();

	public TexImage? Texture;
}
