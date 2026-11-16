using System;
using System.Collections.Generic;

namespace Drip.Core.FallGuys;

public sealed class FgState
{
	public string CatalogOriginalSha { get; set; } = "";

	public string CatalogModifiedSha { get; set; } = "";

	public List<FgSwapRecord> Swaps { get; set; } = new List<FgSwapRecord>();

	public List<FgBundleRecord> Bundles { get; set; } = new List<FgBundleRecord>();

	public string? ModelName { get; set; }

	public DateTime AppliedUtc { get; set; }
}
