namespace Drip.Core.Swapping;

public sealed class SwappedPart
{
	public string Slot { get; set; } = "";

	public string SourcePackage { get; set; } = "";

	public string TargetPackage { get; set; } = "";

	public ulong SourceChunk { get; set; }

	public ulong TargetChunk { get; set; }
}
