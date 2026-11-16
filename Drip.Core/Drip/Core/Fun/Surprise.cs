using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.Fun;

public static class Surprise
{
	public static T? Pick<T>(IReadOnlyList<T> items, Func<T, bool> ok, Func<T, string> id, string? avoidId, Random? rng = null) where T : class
	{
		if (rng == null)
		{
			rng = Random.Shared;
		}
		List<T> list = items.Where((T i) => ok(i) && !string.Equals(id(i), avoidId, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count != 0)
		{
			return list[rng.Next(list.Count)];
		}
		return null;
	}
}
