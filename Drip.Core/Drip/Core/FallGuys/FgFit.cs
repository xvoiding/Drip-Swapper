using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FallGuys;

public sealed class FgFit
{
	public FgUpAxis Up { get; set; }

	public float Yaw { get; set; }

	public bool MirrorX { get; set; } = true;

	public float Height { get; set; } = 1.9f;

	public float Scale { get; set; } = 1f;

	public float OffsetX { get; set; }

	public float OffsetY { get; set; }

	public float OffsetZ { get; set; }

	public TexImage? PictureOverride { get; set; }

	public Vector3? TintColor { get; set; }

	public int MaxTexture { get; set; } = 2048;
}
