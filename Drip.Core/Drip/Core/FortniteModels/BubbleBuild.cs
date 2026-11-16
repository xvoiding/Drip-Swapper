using System.Collections.Generic;

namespace Drip.Core.FortniteModels;

public sealed class BubbleBuild
{
	public List<BubblePatch> Patches { get; } = new List<BubblePatch>();

	public List<string> Notes { get; } = new List<string>();
}
