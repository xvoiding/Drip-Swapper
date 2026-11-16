using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drip.Core.Mods;

namespace Drip.App;

public sealed class PakSkinVm
{
	public PakSkin Skin { get; }

	public ImageSource? Icon { get; }

	public string Name => Skin.Name;

	public string Letter
	{
		get
		{
			if (Skin.Name.Length <= 0)
			{
				return "?";
			}
			return Skin.Name.Substring(0, 1).ToUpperInvariant();
		}
	}

	public Visibility IconVisible
	{
		get
		{
			if (Icon != null)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public Visibility LetterVisible
	{
		get
		{
			if (Icon != null)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public string Tip => $"{Skin.Name}: {Skin.Files} files, {(double)Skin.Bytes / 1048576.0:0.#} MB";

	public PakSkinVm(PakSkin skin, ImageSource? icon)
	{
		Skin = skin;
		Icon = icon;
	}

	public static ImageSource? ToImage(IconPixels? px)
	{
		if ((object)px == null)
		{
			return null;
		}
		BitmapSource bitmapSource = BitmapSource.Create(px.Width, px.Height, 96.0, 96.0, PixelFormats.Bgra32, null, px.Bgra, px.Width * 4);
		bitmapSource.Freeze();
		return bitmapSource;
	}
}
