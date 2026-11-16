namespace Drip.Core.FallGuys;

public sealed record FgRenderer(long SmrPathId, string Name, long MeshPathId, long[] MaterialPathIds, int BoneCount);
