namespace Drip.Core.IoStore;

public readonly record struct TocBlock(ulong Offset, uint CompressedSize, uint UncompressedSize, byte Method);
