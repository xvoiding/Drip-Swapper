using System;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public sealed class CarMesh
{
	public Vector3[] Pos = Array.Empty<Vector3>();

	public Vector3[] Nrm = Array.Empty<Vector3>();

	public Vector2[] Uv = Array.Empty<Vector2>();

	public int[] Idx = Array.Empty<int>();

	public TexImage? Atlas;

	public int VertexCount => Pos.Length;

	public int TriangleCount => Idx.Length / 3;

	public (Vector3 Min, Vector3 Max) Bounds()
	{
		Vector3 vector = new Vector3(float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue);
		Vector3[] pos = Pos;
		foreach (Vector3 value in pos)
		{
			vector = Vector3.Min(vector, value);
			vector2 = Vector3.Max(vector2, value);
		}
		return (Min: vector, Max: vector2);
	}
}
