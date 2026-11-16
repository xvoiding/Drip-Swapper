using System.Collections.Generic;

namespace Drip.Core.FortniteModels;

public sealed class FnBuild
{
	public double BodyMove;

	public double HeadMove;

	public List<FnPatch> Patches { get; } = new List<FnPatch>();

	public List<FnPreviewPart> Preview { get; } = new List<FnPreviewPart>();

	public List<string> Notes { get; } = new List<string>();
}
