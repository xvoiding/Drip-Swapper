using System.Collections.Generic;
using Drip.Core.Zen;

namespace Drip.Core.Swapping;

public sealed record LayoutCheck(ZenLayout Original, ZenLayout Replacement, List<string> Differences)
{
	public bool Safe => Differences.Count == 0;
}
