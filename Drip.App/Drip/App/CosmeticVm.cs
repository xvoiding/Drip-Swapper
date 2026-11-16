using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Drip.Core.Cosmetics;

namespace Drip.App;

public sealed class CosmeticVm : INotifyPropertyChanged
{
	private ImageSource? _icon;

	private bool _isOwned;

	private bool _isWanted;

	private bool _available = true;

	private bool? _downloaded;

	private bool _fav;

	private bool? _animReady;

	private static readonly Brush DangerBrush = Freeze(new SolidColorBrush(Color.FromRgb(229, 57, 53)));

	private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 145, 230)));

	private static readonly Brush GoodBrush = Freeze(new SolidColorBrush(Color.FromRgb(46, 204, 113)));

	public Cosmetic Item { get; }

	public string Id => Item.Id;

	public string Name => Item.Name;

	public string Rarity => Item.Rarity;

	public string Introduced => Item.Introduced;

	public Brush RarityBrush { get; }

	public bool Available
	{
		get
		{
			return _available;
		}
		set
		{
			_available = value;
			Raise("Available");
			Raise("Dim");
			Raise("Tip");
		}
	}

	public bool? Downloaded
	{
		get
		{
			return _downloaded;
		}
		set
		{
			_downloaded = value;
			Raise("Downloaded");
			Raise("NotReady");
			Raise("Dim");
			Raise("Tip");
			Raise("ReadyBrush");
		}
	}

	public bool IsFavorite
	{
		get
		{
			return _fav;
		}
		set
		{
			if (_fav != value)
			{
				_fav = value;
				Raise("IsFavorite");
				Raise("FavVisible");
			}
		}
	}

	public Visibility FavVisible
	{
		get
		{
			if (!_fav)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public bool NotReady => _downloaded == false;

	public bool? AnimReady
	{
		get
		{
			return _animReady;
		}
		set
		{
			if (_animReady != value)
			{
				_animReady = value;
				Raise("AnimReady");
				Raise("Dim");
				Raise("Tip");
			}
		}
	}

	public bool Dead => _animReady == false;

	public double Dim
	{
		get
		{
			if (_available)
			{
				if (!Dead)
				{
					if (!NotReady)
					{
						return 1.0;
					}
					return 0.55;
				}
				return 0.3;
			}
			return 0.3;
		}
	}

	public string Tip
	{
		get
		{
			if (_available)
			{
				if (!Dead)
				{
					if (!NotReady)
					{
						if (_downloaded != true)
						{
							return Item.Name;
						}
						return Item.Name + " (downloaded: safe to swap to)";
					}
					return Item.Name + " (its character parts aren't downloaded on this PC; a swap to it would send you back to the lobby in a match)";
				}
				return Item.Name + " (animation not downloaded yet, a swap to it would do nothing)";
			}
			return Item.Name + " (not in your local game files)";
		}
	}

	public bool HasStyles => Item.HasStyles;

	public Visibility StyleBadge
	{
		get
		{
			if (!Item.HasStyles)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Brush ReadyBrush
	{
		get
		{
			bool? downloaded = _downloaded;
			if (downloaded.HasValue)
			{
				if (downloaded == true)
				{
					return GoodBrush;
				}
				return DangerBrush;
			}
			return Brushes.Transparent;
		}
	}

	public ImageSource? Icon => _icon ?? (_icon = IconCache.Load(Item.IconUrl));

	public bool IsOwned
	{
		get
		{
			return _isOwned;
		}
		set
		{
			if (_isOwned != value)
			{
				_isOwned = value;
				Raise("IsOwned");
				Raise("Badge");
				Raise("BorderBrush");
			}
		}
	}

	public bool IsWanted
	{
		get
		{
			return _isWanted;
		}
		set
		{
			if (_isWanted != value)
			{
				_isWanted = value;
				Raise("IsWanted");
				Raise("Badge");
				Raise("BorderBrush");
			}
		}
	}

	public string Badge
	{
		get
		{
			if (!IsOwned)
			{
				if (!IsWanted)
				{
					return "";
				}
				return "WANTED";
			}
			return "OWNED";
		}
	}

	public Brush BorderBrush
	{
		get
		{
			if (!IsOwned)
			{
				if (!IsWanted)
				{
					return Brushes.Transparent;
				}
				return GoodBrush;
			}
			return AccentBrush;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public CosmeticVm(Cosmetic item)
	{
		Item = item;
		RarityBrush = RarityBrushes.For(item.Rarity);
	}

	private static Brush Freeze(Brush b)
	{
		b.Freeze();
		return b;
	}

	private void Raise(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
