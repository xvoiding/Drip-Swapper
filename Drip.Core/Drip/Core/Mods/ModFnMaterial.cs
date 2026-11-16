using System.Collections.Generic;

namespace Drip.Core.Mods;

public sealed class ModFnMaterial
{
	public string Name { get; set; } = "";

	public List<float> Color { get; set; } = new List<float>();

	public string Picture { get; set; } = "";
}
