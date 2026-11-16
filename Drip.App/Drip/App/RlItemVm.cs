using System.Windows;
using System.Windows.Media;
using Drip.Core.RocketLeague;

namespace Drip.App;

public sealed class RlItemVm
{
	public bool IsFavorite
	{
		get
		{
			if (!Item.IsCustom)
			{
				return FunHub.Store.IsFavorite("rocketleague", Item.AssetPackage);
			}
			return false;
		}
	}

	public Visibility FavVisible
	{
		get
		{
			if (!IsFavorite)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public RlItem Item { get; }

	public string Name => Item.Name;

	public string Tip => Item.Name + " (" + Item.Quality + ")";

	public ImageSource? Icon
	{
		get
		{
			if (!Item.IsCustom)
			{
				if (!(Item.Slot == "Map"))
				{
					return RlIcons.Load(Item.IconUrl);
				}
				return RlIcons.MapPicture(Item.AssetPackage);
			}
			return RlCustomIcons.For(Item.CustomFile);
		}
	}

	public Brush QualityBrush => RarityBrushes.For(Item.Quality);

	public RlItemVm(RlItem item)
	{
		Item = item;
	}
}
