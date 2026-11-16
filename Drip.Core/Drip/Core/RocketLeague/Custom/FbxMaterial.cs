using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public sealed class FbxMaterial
{
	public string Name = "";

	public Vector3 Color = new Vector3(0.8f);

	public bool HasTexture;

	public string TextureFile = "";
}
