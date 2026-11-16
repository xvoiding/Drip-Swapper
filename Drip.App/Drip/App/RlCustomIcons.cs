using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drip.Core.RocketLeague.Custom;

namespace Drip.App;

internal static class RlCustomIcons
{
	private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

	public static ImageSource? For(string path)
	{
		try
		{
			string key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
			if (Cache.TryGetValue(key, out ImageSource value))
			{
				return value;
			}
			ImageSource imageSource = Render(DripRlFile.Load(path));
			Cache[key] = imageSource;
			return imageSource;
		}
		catch
		{
			return null;
		}
	}

	public static ImageSource Render(DripRlFile model)
	{
		TexImage picture = null;
		try
		{
			if (model.Picture.Length != 0)
			{
				picture = TexImage.Load(model.Picture).ForGame(256, 64);
			}
		}
		catch
		{
		}
		return ToSource(MeshPreview.Render(model.Mesh, PreviewView.ThreeQuarter, 160, picture, car: false), 160);
	}

	public static BitmapSource ToSource(byte[] bgra, int size)
	{
		BitmapSource bitmapSource = BitmapSource.Create(size, size, 96.0, 96.0, PixelFormats.Bgra32, null, bgra, size * 4);
		bitmapSource.Freeze();
		return bitmapSource;
	}
}
