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

	// Signature re-seal, so the container still passes the game's block check and online play
	// doesn't drop the session. All three regions are restored by Revert.

	public long HashRegionPos { get; set; } = -1;

	public string OriginalHashes { get; set; } = "";

	public string PatchedHashes { get; set; } = "";

	public long SignaturePos { get; set; } = -1;

	public string OriginalSignature { get; set; } = "";

	public string PatchedSignature { get; set; } = "";

	public long MetaPos { get; set; } = -1;

	public string OriginalMeta { get; set; } = "";

	public string PatchedMeta { get; set; } = "";

	public bool Sealed { get; set; }

	public string SealError { get; set; } = "";
}
