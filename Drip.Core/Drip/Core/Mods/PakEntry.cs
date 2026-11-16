namespace Drip.Core.Mods;

public sealed record PakEntry(string Name, long Offset, long CompressedSize, long Size, uint Method, (long Start, long End)[] Blocks);
