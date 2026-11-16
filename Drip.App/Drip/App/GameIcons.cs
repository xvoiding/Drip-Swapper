using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Drip.Core.FallGuys;
using Drip.Core.Install;
using Drip.Core.RocketLeague;

namespace Drip.App;

internal static class GameIcons
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, nint[] icons, int[]? ids, uint count, uint flags);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(nint icon);

	private static BitmapSource? FromLogosFolder(string name)
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "logos", name + ".png");
			if (!File.Exists(text))
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(text);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	public static BitmapSource? Fortnite()
	{
		BitmapSource bitmapSource = FromLogosFolder("fortnite");
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		string text = FortniteLocator.FindPaksDirectory();
		if (text == null)
		{
			return null;
		}
		string text2 = Up(text, 2);
		if (text2 != null)
		{
			return FromExe(Path.Combine(text2, "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));
		}
		return null;
	}

	public static BitmapSource? RocketLeague()
	{
		BitmapSource bitmapSource = FromLogosFolder("rocketleague");
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		string text = RlLocator.FindCookedDirectory();
		if (text == null)
		{
			return null;
		}
		string text2 = Up(text, 2);
		if (text2 != null)
		{
			return FromExe(Path.Combine(text2, "Binaries", "Win64", "RocketLeague.exe"));
		}
		return null;
	}

	public static BitmapSource? Retrac()
	{
		BitmapSource bitmapSource = FromLogosFolder("retrac");
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		List<string> list = new List<string>
		{
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Retrac"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Retrac"),
			Path.Combine(folderPath, "Programs", "Retrac"),
			Path.Combine(folderPath, "Retrac")
		};
		try
		{
			list.AddRange(Directory.EnumerateDirectories(folderPath, "*retrac*"));
		}
		catch
		{
		}
		foreach (string item in list)
		{
			try
			{
				if (Directory.Exists(item))
				{
					string text = Path.Combine(item, "retrac.exe");
					string text2 = (File.Exists(text) ? text : Directory.EnumerateFiles(item, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault((string f) => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).StartsWith("Update", StringComparison.OrdinalIgnoreCase)));
					BitmapSource bitmapSource2 = ((text2 == null) ? null : FromExe(text2));
					if (bitmapSource2 != null)
					{
						return bitmapSource2;
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public static BitmapSource? Stellar()
	{
		BitmapSource bitmapSource = FromLogosFolder("stellar");
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		List<string> list = new List<string>
		{
			Path.Combine(folderPath, "dev.stellar.launcher"),
			Path.Combine(folderPath, "Programs", "Stellar"),
			Path.Combine(folderPath, "Programs", "Stellar Launcher"),
			Path.Combine(folderPath, "Stellar"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Stellar"),
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Stellar"),
			@"C:\Project Stellar"
		};
		try
		{
			list.AddRange(Directory.EnumerateDirectories(Path.Combine(folderPath, "Programs"), "*stellar*"));
		}
		catch
		{
		}
		foreach (string item in list)
		{
			try
			{
				if (!Directory.Exists(item))
				{
					continue;
				}
				foreach (string file in Directory.EnumerateFiles(item, "*.exe", SearchOption.TopDirectoryOnly))
				{
					string name = Path.GetFileName(file);
					if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Update", StringComparison.OrdinalIgnoreCase) || name.Contains("Fortnite", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					BitmapSource bitmapSource2 = FromExe(file);
					if (bitmapSource2 != null)
					{
						return bitmapSource2;
					}
				}
			}
			catch
			{
			}
		}
		return Embedded("stellar");
	}

	/// <summary>Icon bundled inside Drip Swapper, used when no real one is found on this PC.</summary>
	public static BitmapSource? Embedded(string name)
	{
		try
		{
			BitmapImage bitmapImage = new BitmapImage(new Uri("pack://application:,,,/assets/" + name + ".png", UriKind.Absolute));
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	public static BitmapSource? FallGuys()
	{
		BitmapSource bitmapSource = FromLogosFolder("fallguys");
		if (bitmapSource != null)
		{
			return bitmapSource;
		}
		FgInstall fgInstall = FgLocator.Find();
		if (!(fgInstall == null))
		{
			return FromExe(Path.Combine(fgInstall.Root, "FallGuys_client_game.exe"));
		}
		return null;
	}

	private static string? Up(string path, int levels)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(path);
		for (int i = 0; i < levels; i++)
		{
			if (directoryInfo.Parent == null)
			{
				break;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return directoryInfo.FullName;
	}

	private static BitmapSource? FromExe(string exe)
	{
		try
		{
			if (!File.Exists(exe))
			{
				return null;
			}
			BitmapSource bitmapSource = LargestIcon(exe);
			if (bitmapSource != null)
			{
				return bitmapSource;
			}
			using Icon icon = Icon.ExtractAssociatedIcon(exe);
			if (icon == null)
			{
				return null;
			}
			BitmapSource bitmapSource2 = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource2.Freeze();
			return bitmapSource2;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? LargestIcon(string exe)
	{
		int[] array = new int[4] { 256, 128, 64, 48 };
		foreach (int num in array)
		{
			nint[] array2 = new nint[1];
			uint num2 = PrivateExtractIcons(exe, 0, num, num, array2, null, 1u, 0u);
			if (num2 == 0 || num2 == uint.MaxValue || array2[0] == IntPtr.Zero)
			{
				continue;
			}
			try
			{
				BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(array2[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				bitmapSource.Freeze();
				if (bitmapSource.PixelWidth >= 48)
				{
					return bitmapSource;
				}
			}
			finally
			{
				DestroyIcon(array2[0]);
			}
		}
		return null;
	}
}
