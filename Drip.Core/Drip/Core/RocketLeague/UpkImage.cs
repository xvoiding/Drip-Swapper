using System.Collections.Generic;

namespace Drip.Core.RocketLeague;

public sealed class UpkImage
{
	public required byte[] Data { get; init; }

	public required UpkSummary Summary { get; init; }

	public required List<UpkChunk> Chunks { get; init; }

	public required byte[] RegionPlain { get; init; }

	public required byte[] Gap { get; init; }

	public int ChunkEntrySize { get; init; } = 24;
}
