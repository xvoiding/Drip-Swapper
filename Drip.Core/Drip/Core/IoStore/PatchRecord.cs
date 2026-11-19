namespace Drip.Core.IoStore;

public sealed class PatchRecord
{
	public string Container { get; set; } = "";

	public int ChunkIndex { get; set; }

	public int BlockIndex { get; set; }

	public long BlockPos { get; set; }

	public long OffsetLengthPos { get; set; }

	public string OriginalBlock { get; set; } = "";

	public string OriginalOffsetLength { get; set; } = "";

	public string PatchedBlock { get; set; } = "";

	public string PatchedOffsetLength { get; set; } = "";

	public string SourcePackage { get; set; } = "";

	public string TargetPackage { get; set; } = "";

	public int BlockCount { get; set; }

	public string UcasFile { get; set; } = "";

	public long UcasLengthBefore { get; set; }

	public long UcasLengthAfter { get; set; }

	public long UtocWriteUtcTicks { get; set; }

	public long UcasWriteUtcTicks { get; set; }
}
