using Drip.Core.FallGuys;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public sealed class FnFitOptions
{
	public FgUpAxis Up { get; set; }

	public float Yaw { get; set; }

	public bool Mirror { get; set; }

	public float Scale { get; set; } = 1f;

	public float Width { get; set; } = 1f;

	public float OffsetX { get; set; }

	public float OffsetY { get; set; }

	public float OffsetZ { get; set; }

	public TexImage? PictureOverride { get; set; }

	public FnFitOptions Clone()
	{
		return (FnFitOptions)MemberwiseClone();
	}
}
