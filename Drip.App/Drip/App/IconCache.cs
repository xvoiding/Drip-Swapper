using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Drip.App;

public static class IconCache
{
	private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

	public static ImageSource? Load(string url, int decodeWidth = 180)
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
		bitmapImage.DecodePixelWidth = decodeWidth;
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
		bitmapImage.EndInit();
		Cache[url] = bitmapImage;
		return bitmapImage;
	}
}
