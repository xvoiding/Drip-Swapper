namespace Drip.Core.Swapping;

public sealed record VerifyLine(VerifyLevel Level, string Container, string Text)
{
	public override string ToString()
	{
		string text;
		if (Level != VerifyLevel.Ok)
		{
			text = ((Level == VerifyLevel.Warn) ? "WARN" : "FAIL");
		}
		else
		{
			text = "OK  ";
		}
		return text + " " + ((Container.Length > 0) ? (Container + ": ") : "") + Text;
	}
}
