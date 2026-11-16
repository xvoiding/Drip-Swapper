using System;
using System.Numerics;

namespace Drip.Core.FallGuys;

public sealed class FgFittedModel
{
	public Vector3[] Pos = Array.Empty<Vector3>();

	public Vector3[] Nrm = Array.Empty<Vector3>();

	public Vector2[] Uv = Array.Empty<Vector2>();

	public int[] Idx = Array.Empty<int>();

	public byte[] Rgba = Array.Empty<byte>();

	public int Width;

	public int Height;

	public float[]? SkinW;

	public int[]? SkinB;
}
