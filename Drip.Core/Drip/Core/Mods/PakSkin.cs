namespace Drip.Core.Mods;

public sealed class PakSkin
{
	public string Folder { get; init; } = "";

	public string Name { get; init; } = "";

	public int Files { get; set; }

	public long Bytes { get; set; }

	public int Meshes { get; set; }

	public int Textures { get; set; }

	public int Materials { get; set; }

	public string? IconEntry { get; set; }
}
