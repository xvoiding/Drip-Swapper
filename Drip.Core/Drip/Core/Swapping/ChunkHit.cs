using Drip.Core.IoStore;

namespace Drip.Core.Swapping;

public sealed record ChunkHit(IoContainer Container, int Index, byte Type);
