namespace Drip.Core.FallGuys;

public sealed record FgPiece(FgAsset Asset, FgRole Role, bool Team, int Variant, string Tag = "")
{
	public string Slot => $"{(Team ? "T" : "")}{((Variant > 0) ? ("V" + Variant) : "")}:{Role}{((Tag.Length > 0) ? (":" + Tag) : "")}";
}
