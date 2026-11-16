using System;
using System.Collections.Generic;

namespace Drip.Core.Swapping;

public sealed class SwappedItem
{
	public string Kind { get; set; } = "";

	public string SourceId { get; set; } = "";

	public string SourceName { get; set; } = "";

	public string SourceIcon { get; set; } = "";

	public string TargetId { get; set; } = "";

	public string TargetName { get; set; } = "";

	public string TargetIcon { get; set; } = "";

	public string TargetStyle { get; set; } = "";

	public string TargetStyleTag { get; set; } = "";

	public string SourceStyle { get; set; } = "";

	public List<StyleRename> StyleRenames { get; set; } = new List<StyleRename>();

	public string ModKey { get; set; } = "";

	public DateTime ConvertedAt { get; set; } = DateTime.Now;

	public List<SwappedPart> Parts { get; set; } = new List<SwappedPart>();

	public List<string> PatchedPackages { get; set; } = new List<string>();

	public List<string> Notes { get; set; } = new List<string>();
}
