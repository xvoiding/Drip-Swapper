using System;
using System.Collections.Generic;

namespace Drip.Core.Fun;

public sealed class Fit
{
	public string Game { get; set; } = "";

	public string Name { get; set; } = "";

	public DateTime Saved { get; set; } = DateTime.UtcNow;

	public List<FitEntry> Entries { get; set; } = new List<FitEntry>();
}
