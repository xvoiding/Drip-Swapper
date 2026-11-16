namespace Drip.Core.RocketLeague;

public readonly record struct UpkChunk(long UncompressedOffset, int UncompressedSize, long CompressedOffset, int CompressedSize);
