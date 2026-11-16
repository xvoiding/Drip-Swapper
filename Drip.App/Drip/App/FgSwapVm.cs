using System.Windows.Media;
using Drip.Core.FallGuys;

namespace Drip.App;

public sealed class FgSwapVm
{
	public FgSwapRecord Record { get; }

	public string Title { get; }

	public string Subtitle { get; }

	public string TargetInitial { get; }

	public string SourceInitial { get; }

	public Brush TargetTint { get; }

	public Brush SourceTint { get; }

	public FgSwapVm(FgSwapRecord r, FgLibrary? lib)
	{
		Record = r;
		if (r.Kind == "emote")
		{
			FgEmote? fgEmote = lib?.FindEmote(r.Target);
			FgEmote fgEmote2 = lib?.FindEmote(r.Source);
			string text = fgEmote?.Name ?? r.Target;
			string text2 = fgEmote2?.Name ?? r.Source;
			Title = "The " + text + " emote now plays " + text2;
			Subtitle = "Emote swap";
			TargetInitial = ((text.Length > 0) ? text.Substring(0, 1).ToUpperInvariant() : "?");
			SourceInitial = ((text2.Length > 0) ? text2.Substring(0, 1).ToUpperInvariant() : "?");
			TargetTint = FgCostumeVm.TintFor(r.Target);
			SourceTint = FgCostumeVm.TintFor(r.Source);
		}
		else
		{
			FgCostume fgCostume = lib?.Find(r.Target);
			FgCostume fgCostume2 = lib?.Find(r.Source);
			string text3 = fgCostume?.Name ?? r.Target;
			string text4 = fgCostume2?.Name ?? r.Source;
			Title = text3 + " now looks like " + text4;
			string value = ((r.Top && r.Bottom) ? "Top and bottom" : (r.Top ? "Top only" : "Bottom only"));
			Subtitle = $"{value} · season {(fgCostume?.SeasonLabel ?? "").TrimStart('S')} costume shows season {(fgCostume2?.SeasonLabel ?? "").TrimStart('S')} look";
			TargetInitial = ((text3.Length > 0) ? text3.Substring(0, 1).ToUpperInvariant() : "?");
			SourceInitial = ((text4.Length > 0) ? text4.Substring(0, 1).ToUpperInvariant() : "?");
			TargetTint = FgCostumeVm.TintFor(fgCostume?.Id ?? r.Target);
			SourceTint = FgCostumeVm.TintFor(fgCostume2?.Id ?? r.Source);
		}
	}
}
