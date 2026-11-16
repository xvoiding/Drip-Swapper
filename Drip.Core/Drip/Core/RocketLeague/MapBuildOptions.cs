namespace Drip.Core.RocketLeague;

public sealed class MapBuildOptions
{
	public MapMaterial Material { get; set; }

	public bool KeepScenery { get; set; }

	public string TargetName { get; set; } = "This map";
}
