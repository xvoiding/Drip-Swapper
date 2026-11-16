using System.Windows.Media;

namespace Drip.App;

public sealed class StyleVm
{
	public string Name { get; }

	public string Tag { get; }

	public string ImageUrl { get; }

	public bool IsDefault { get; }

	public ImageSource? Icon => IconCache.Load(ImageUrl);

	public StyleVm(string name, string tag, string imageUrl, bool isDefault = false)
	{
		Name = name;
		Tag = tag;
		ImageUrl = imageUrl;
		IsDefault = isDefault;
	}
}
