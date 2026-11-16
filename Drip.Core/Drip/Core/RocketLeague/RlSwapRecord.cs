using System;
using System.Collections.Generic;

namespace Drip.Core.RocketLeague;

public sealed class RlSwapRecord
{
	public string Slot { get; set; } = "";

	public string TargetName { get; set; } = "";

	public string TargetPackage { get; set; } = "";

	public string DonorName { get; set; } = "";

	public string DonorPackage { get; set; } = "";

	public bool WithThumbnail { get; set; }

	public bool Custom { get; set; }

	public string CustomFile { get; set; } = "";

	public bool Live { get; set; }

	public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;

	public List<RlFileRecord> Files { get; set; } = new List<RlFileRecord>();
}
