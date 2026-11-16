using System.Collections.Generic;

namespace Drip.Core.RocketLeague.Custom;

public sealed class ModelFit
{
	public int Turns { get; set; }

	public bool FlipFrontBack { get; set; }

	public double Yaw { get; set; }

	public double Pitch { get; set; }

	public double Roll { get; set; }

	public double Size { get; set; } = 1.0;

	public double Lift { get; set; }

	public double OffsetX { get; set; }

	public double OffsetY { get; set; }

	public bool UseMaterialColors { get; set; }

	public List<string> LeaveOut { get; set; } = new List<string> { "tire", "tyre", "wheel", "rim" };
}
