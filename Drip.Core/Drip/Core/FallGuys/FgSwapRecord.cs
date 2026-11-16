namespace Drip.Core.FallGuys;

public sealed class FgSwapRecord
{
	public string Target { get; set; } = "";

	public string Source { get; set; } = "";

	public string Label { get; set; } = "";

	public bool Top { get; set; } = true;

	public bool Bottom { get; set; } = true;

	public string Kind { get; set; } = "costume";
}
