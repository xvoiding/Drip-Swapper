using System.Windows.Media;
using Drip.Core.FallGuys;

namespace Drip.App;

public sealed class FgEmoteVm
{
	public FgEmote Emote { get; }

	public string Key => Emote.Key;

	public bool IsFavorite { get; set; }

	public string Mark
	{
		get
		{
			if (!IsFavorite)
			{
				return "";
			}
			return "★ ";
		}
	}

	public string Name => Emote.Name;

	public string Sub { get; }

	public string Initial { get; }

	public Brush Tint { get; }

	public FgEmoteVm(FgEmote e)
	{
		Emote = e;
		Sub = "Season " + e.SeasonLabel.TrimStart('S') + " · " + e.Asset.FileName.Replace(".fbx", "");
		Initial = ((e.Name.Length > 0) ? e.Name.Substring(0, 1).ToUpperInvariant() : "?");
		Tint = FgCostumeVm.TintFor(e.Key);
	}
}
