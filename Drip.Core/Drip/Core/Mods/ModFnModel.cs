using System.Collections.Generic;

namespace Drip.Core.Mods;

public sealed class ModFnModel
{
	public string Rig { get; set; } = "";

	public string Mesh { get; set; } = "model/mesh.bin";

	public bool HasUv { get; set; }

	public List<ModFnMaterial> Materials { get; set; } = new List<ModFnMaterial>();

	public string Mode { get; set; } = "";

	public string Skin { get; set; } = "";
}
