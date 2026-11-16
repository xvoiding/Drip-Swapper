namespace Drip.Core.FallGuys;

public sealed record FgEmote(FgAsset Asset, string Name, string Season)
{
	public string Key => Asset.Address;

	public string SeasonLabel => Season.Replace('_', '.');
}
