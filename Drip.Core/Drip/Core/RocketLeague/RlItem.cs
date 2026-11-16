namespace Drip.Core.RocketLeague;

public sealed record RlItem(int Id, string Name, string Slot, string Quality, string AssetPackage, string ObjectName, bool Multi)
{
	public string FileName
	{
		get
		{
			if (!(Slot == "Map"))
			{
				return AssetPackage + "_SF.upk";
			}
			return AssetPackage + ".upk";
		}
	}

	public string ThumbFileName => AssetPackage + "_T_SF.upk";

	public string? CustomFile { get; init; }

	public bool IsCustom => CustomFile != null;

	public string IconUrl => "https://api.velocityrl.tech/thumbnails/" + AssetPackage.ToLowerInvariant() + "_t.png";

	public override string ToString()
	{
		return Name + " (" + Slot + ")";
	}
}
