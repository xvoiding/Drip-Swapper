namespace Drip.Core.Swapping;

public sealed class SwapResult
{
	public string Source { get; init; } = "";

	public string Target { get; init; } = "";

	public int ContainersPatched { get; init; }

	public int OldSize { get; init; }

	public int NewSize { get; init; }

	public int HashMismatchesInTarget { get; init; }
}
