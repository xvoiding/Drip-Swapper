using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Drip.App;

internal static class RlIcons
{
	private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

	private static readonly Dictionary<string, ImageSource?> MapPics = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	public static ImageSource MapGlyph { get; } = MakeGlyph();

	public static ImageSource? Load(string url)
	{
		if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri result))
		{
			return null;
		}
		if (Cache.TryGetValue(url, out ImageSource value))
		{
			return value;
		}
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = result;
		bitmapImage.DecodePixelWidth = 160;
		bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
		bitmapImage.EndInit();
		Cache[url] = bitmapImage;
		return bitmapImage;
	}

	private static ImageSource MakeGlyph()
	{
		Geometry geometry = Geometry.Parse("M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z");
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(91, 214, byte.MaxValue));
		solidColorBrush.Freeze();
		DrawingImage drawingImage = new DrawingImage(new GeometryDrawing(solidColorBrush, null, geometry));
		drawingImage.Freeze();
		return drawingImage;
	}

	public static ImageSource MapPicture(string package)
	{
		if (MapPics.TryGetValue(package, out ImageSource value))
		{
			return value ?? MapGlyph;
		}
		ImageSource imageSource = null;
		foreach (string item in MapPictureNames(package.ToLowerInvariant()))
		{
			try
			{
				BitmapImage bitmapImage = new BitmapImage(new Uri("pack://application:,,,/Assets/Maps/" + item + ".png", UriKind.Absolute));
				if (bitmapImage.CanFreeze)
				{
					bitmapImage.Freeze();
				}
				imageSource = bitmapImage;
			}
			catch
			{
				continue;
			}
			break;
		}
		MapPics[package] = imageSource;
		return imageSource ?? MapGlyph;
	}

	private static IEnumerable<string> MapPictureNames(string stem)
	{
		yield return stem;
		string[] array = new string[14]
		{
			"stadium_", "eurostadium_", "park_", "trainstation_", "utopiastadium_", "wasteland", "neotokyo", "cs_", "arc_", "beach",
			"chn_stadium", "farm", "outlaw", "street"
		};
		foreach (string text in array)
		{
			if (stem.StartsWith(text))
			{
				yield return text.TrimEnd('_') + (text.EndsWith('_') ? "_p" : "_p");
				break;
			}
		}
		yield return "default";
	}

	public static string UrlFor(string package)
	{
		return "https://api.velocityrl.tech/thumbnails/" + package.ToLowerInvariant() + "_t.png";
	}
}
