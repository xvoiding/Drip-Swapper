using System.Collections.Generic;

namespace Drip.Core.RocketLeague;

public sealed class RlSwapSettings
{
	public bool RevertWhenGameCloses { get; set; }

	public bool AllowResizing { get; set; }

	public string CookedDirectory { get; set; } = "";

	public int CustomTextures { get; set; }

	public int CustomWheels { get; set; }

	public Dictionary<string, string> LiveTargets { get; set; } = new Dictionary<string, string>();

	public int MapMaterial { get; set; }

	public bool MapKeepScenery { get; set; }

	public string LastKeysFolder { get; set; } = "";
}
