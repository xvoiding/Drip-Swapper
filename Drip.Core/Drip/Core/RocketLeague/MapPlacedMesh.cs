using System.Numerics;

namespace Drip.Core.RocketLeague;

public sealed class MapPlacedMesh
{
	public int CompIndex;

	public int MeshIndex;

	public string MeshName = "";

	public string OwnerClass = "";

	public string OwnerName = "";

	public Matrix4x4 World = Matrix4x4.Identity;

	public bool Identity;

	public bool Hidden;

	public bool Movable;

	public bool Keep;

	public Vector3 WorldMin;

	public Vector3 WorldMax;

	public bool InsideArena
	{
		get
		{
			if (WorldMin.X >= -5300f && WorldMax.X <= 5300f && WorldMin.Y >= -6800f && WorldMax.Y <= 6800f && WorldMin.Z >= -300f)
			{
				return WorldMax.Z <= 2800f;
			}
			return false;
		}
	}
}
