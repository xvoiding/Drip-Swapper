using System;
using System.Collections.Generic;
using System.Numerics;

namespace Drip.Core.FallGuys;

public sealed class FgRigSource
{
	public Vector3[] Pos = Array.Empty<Vector3>();

	public Vector3[] Nrm = Array.Empty<Vector3>();

	public Vector2[] Uv = Array.Empty<Vector2>();

	public int[] Idx = Array.Empty<int>();

	public int[] Mat = Array.Empty<int>();

	public List<FgSourceMaterial> Materials = new List<FgSourceMaterial>();

	public bool HasUv;

	public float[] Role = Array.Empty<float>();

	public string Name = "";
}
