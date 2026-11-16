using System.Collections.Generic;
using Drip.Core.IoStore;

namespace Drip.Core.Swapping;

public sealed class SwapState
{
	public string Build { get; set; } = "";

	public List<PatchRecord> Patches { get; set; } = new List<PatchRecord>();
}
