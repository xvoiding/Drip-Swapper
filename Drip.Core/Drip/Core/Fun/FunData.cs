using System.Collections.Generic;

namespace Drip.Core.Fun;

public sealed class FunData
{
	public Dictionary<string, List<string>> Favorites { get; set; } = new Dictionary<string, List<string>>();

	public List<Fit> Fits { get; set; } = new List<Fit>();
}
