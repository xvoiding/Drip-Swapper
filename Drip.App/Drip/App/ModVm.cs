using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drip.Core.FortniteModels;
using Drip.Core.Mods;

namespace Drip.App;

public sealed class ModVm : INotifyPropertyChanged
{
	private string _check = "";

	private bool _checkOk;

	private static readonly Dictionary<string, ImageSource?> IconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	private ImageSource? _icon;

	private bool _iconTried;

	public ModPack? Mod { get; }

	public RiftPack? Rift { get; }

	public ImageSource? Icon
	{
		get
		{
			if (_iconTried)
			{
				return _icon;
			}
			_iconTried = true;
			string text = Mod?.Manifest.Icon ?? "";
			if (text.Length == 0)
			{
				return _icon = EmbeddedIcon();
			}
			if (IconCache.TryGetValue(text, out ImageSource value))
			{
				return _icon = value;
			}
			try
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(text);
				bitmapImage.DecodePixelWidth = 112;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				_icon = bitmapImage;
			}
			catch
			{
				_icon = null;
			}
			IconCache[text] = _icon;
			return _icon;
		}
	}

	public string Letter
	{
		get
		{
			if (Name.Length <= 0)
			{
				return "?";
			}
			return Name.Substring(0, 1).ToUpperInvariant();
		}
	}

	public Visibility IconVisible
	{
		get
		{
			if (Icon == null)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Visibility LetterVisible
	{
		get
		{
			if (Icon == null)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public string Name
	{
		get
		{
			if (Rift == null)
			{
				return Mod.Manifest.Name;
			}
			return Rift.Name;
		}
	}

	public string Sub
	{
		get
		{
			if (Rift == null)
			{
				return string.Join("  ·  ", new string[2]
				{
					string.IsNullOrWhiteSpace(Mod.Manifest.Author) ? null : ("by " + Mod.Manifest.Author),
					(Mod.Manifest.Base.Length > 0) ? ("look: " + Mod.Manifest.Base) : null
				}.Where((string x) => x != null));
			}
			return string.Join("  ·  ", new string[3]
			{
				"legacy .rift",
				string.IsNullOrWhiteSpace(Rift.Author) ? null : ("by " + Rift.Author),
				(Rift.VersionText.Length > 0) ? ("for Fortnite " + Rift.VersionText) : null
			}.Where((string x) => x != null));
		}
	}

	public string Description
	{
		get
		{
			if (Rift == null)
			{
				return Mod.Manifest.Description;
			}
			return Rift.Description;
		}
	}

	public string Detail
	{
		get
		{
			List<string> list = new List<string>();
			if (Rift != null)
			{
				list.Add($"{Rift.AssetCount} assets");
				if (Rift.MeshCount > 0)
				{
					list.Add($"{Rift.MeshCount} mesh files");
				}
				if (Rift.TextureCount > 0)
				{
					list.Add($"{Rift.TextureCount} texture files");
				}
				list.Add($"{(double)Rift.TotalBytes / 1048576.0:0.#} MB");
				return string.Join("  ·  ", list);
			}
			if (Mod.Manifest.SlotSwaps.Count > 0)
			{
				list.Add($"{Mod.Manifest.SlotSwaps.Count} slot swap(s)");
			}
			if (Mod.Manifest.PartSwaps.Count > 0)
			{
				list.Add($"{Mod.Manifest.PartSwaps.Count} part swap(s)");
			}
			if (Mod.Manifest.Packages.Count > 0)
			{
				list.Add($"{Mod.Manifest.Packages.Count} replacement asset(s)");
			}
			if (Mod.Manifest.FnModel != null)
			{
				list.Add("custom model");
			}
			ModFnItem fnItem = Mod.Manifest.FnItem;
			if (fnItem != null)
			{
				FnItemKind? fnItemKind = FnItem.Find(fnItem.Kind);
				string text;
				if (fnItemKind.HasValue)
				{
					FnItemKind valueOrDefault = fnItemKind.GetValueOrDefault();
					text = FnItem.Label(valueOrDefault);
				}
				else
				{
					text = "item";
				}
				list.Add("custom " + text);
			}
			ModFnBuilds fnBuilds = Mod.Manifest.FnBuilds;
			if (fnBuilds != null)
			{
				list.Add("build blueprints (you pick wood, brick or metal)" + (fnBuilds.Style.Equals("picture", StringComparison.OrdinalIgnoreCase) ? " with your picture" : (" look: " + (BuildLooks.Find(fnBuilds.Style)?.Name ?? fnBuilds.Style))));
			}
			return string.Join("  ·  ", list);
		}
	}

	public string Status => Rift?.Status ?? _check;

	public Visibility StatusVisible
	{
		get
		{
			if (Rift != null || _check.Length != 0)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public Brush StatusBrush
	{
		get
		{
			Application current = Application.Current;
			string resourceKey;
			if (Rift != null)
			{
				resourceKey = ((Rift.Compat == RiftCompat.Works) ? "Good" : "Danger");
			}
			else
			{
				resourceKey = (_checkOk ? "Good" : "Danger");
			}
			return (Brush)current.FindResource(resourceKey);
		}
	}

	public bool CanUse => Rift == null;

	public string UseText
	{
		get
		{
			if (Rift == null)
			{
				if (Mod.Manifest.Base.Length <= 0)
				{
					return "Apply";
				}
				return "Use…";
			}
			return "Can't apply";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public void SetCheck(bool ok, string text)
	{
		_checkOk = ok;
		_check = text;
		string[] array = new string[3] { "Status", "StatusVisible", "StatusBrush" };
		foreach (string propertyName in array)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public ModVm(ModPack mod)
	{
		Mod = mod;
	}

	public ModVm(RiftPack rift)
	{
		Rift = rift;
	}

	private ImageSource? EmbeddedIcon()
	{
		if (Mod == null)
		{
			return null;
		}
		string key = "embedded:" + Mod.FilePath;
		if (IconCache.TryGetValue(key, out ImageSource value))
		{
			return value;
		}
		ImageSource imageSource = null;
		try
		{
			byte[] array = Mod.ReadEmbeddedIcon();
			if (array != null)
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = new MemoryStream(array);
				bitmapImage.DecodePixelWidth = 112;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				imageSource = bitmapImage;
			}
		}
		catch
		{
			imageSource = null;
		}
		IconCache[key] = imageSource;
		return imageSource;
	}
}
