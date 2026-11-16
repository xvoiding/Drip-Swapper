namespace Drip.Core.FortniteModels;

public readonly record struct FnTint(float R, float G, float B, float A, float Keep)
{
	public static FnTint Off(float r, float g, float b, float a = 1f)
	{
		return new FnTint(r, g, b, a, 0f);
	}

	public static FnTint Soft(float r, float g, float b, float a, float keep = 0.4f)
	{
		return new FnTint(r, g, b, a, keep);
	}
}
