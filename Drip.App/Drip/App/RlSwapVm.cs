using System.Windows.Media;
using Drip.Core.RocketLeague;

namespace Drip.App;

public sealed class RlSwapVm
{
	public RlSwapRecord Record { get; }

	public string TargetName => Record.TargetName;

	public string DonorName => Record.DonorName;

	public string Title => Record.TargetName + " now looks like " + Record.DonorName;

	public string Subtitle => Record.Slot + (Record.Custom ? " · custom model" : "") + (Record.WithThumbnail ? " · preview picture swapped too" : "");

	public ImageSource? TargetIcon
	{
		get
		{
			if (!(Record.Slot == "Map"))
			{
				return RlIcons.Load(RlIcons.UrlFor(Record.TargetPackage));
			}
			return RlIcons.MapPicture(Record.TargetPackage);
		}
	}

	public ImageSource? DonorIcon
	{
		get
		{
			if (!Record.Custom || Record.CustomFile.Length <= 0)
			{
				return RlIcons.Load(RlIcons.UrlFor(Record.DonorPackage));
			}
			return RlCustomIcons.For(Record.CustomFile);
		}
	}

	public RlSwapVm(RlSwapRecord rec)
	{
		Record = rec;
	}
}
