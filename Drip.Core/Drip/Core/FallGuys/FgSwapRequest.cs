using System.Collections.Generic;

namespace Drip.Core.FallGuys;

public sealed class FgSwapRequest
{
	public List<(string Target, string Source, bool Top, bool Bottom)> CostumeSwaps { get; } = new List<(string, string, bool, bool)>();

	public List<(string Target, string Source)> EmoteSwaps { get; } = new List<(string, string)>();

	public FgFittedModel? BeanModel { get; set; }

	public string? BeanModelName { get; set; }
}
