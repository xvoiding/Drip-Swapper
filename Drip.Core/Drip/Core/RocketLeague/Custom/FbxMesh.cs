using System.Collections.Generic;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public sealed class FbxMesh
{
	public string Name = "";

	public List<string> Path = new List<string>();

	public List<string> MaterialNames = new List<string>();

	public List<FbxMaterial> Materials = new List<FbxMaterial>();

	public List<Vector3> Pos = new List<Vector3>();

	public List<Vector3> Nrm = new List<Vector3>();

	public List<Vector2> Uv = new List<Vector2>();

	public List<int> Material = new List<int>();

	public int TriangleCount => Material.Count;

	public string MaterialFor(int tri)
	{
		if (Material[tri] < 0 || Material[tri] >= MaterialNames.Count)
		{
			return "";
		}
		return MaterialNames[Material[tri]];
	}
}
