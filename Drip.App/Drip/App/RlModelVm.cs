using System.Windows;
using System.Windows.Media;
using Drip.Core.RocketLeague;

namespace Drip.App;

public sealed class RlModelVm
{
	private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(98, 227, 161)));

	private static readonly Brush Grey = Freeze(new SolidColorBrush(Color.FromRgb(107, 107, 120)));

	public RlCustom.Entry Entry { get; }

	public string Name => Entry.Model.Name;

	public string Kind
	{
		get
		{
			if (!(Entry.Model.Slot == "Body"))
			{
				return Entry.Model.Slot;
			}
			return "Car";
		}
	}

	public string Triangles => $"{Entry.Model.Mesh.TriangleCount:N0} triangles";

	public Visibility TexturedVisibility
	{
		get
		{
			if (Entry.Model.Picture.Length == 0)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Brush WheelsBrush
	{
		get
		{
			if (!Entry.Model.OwnWheels)
			{
				return Green;
			}
			return Grey;
		}
	}

	public ImageSource? Icon => RlCustomIcons.For(Entry.Path);

	public Visibility WheelsVisibility
	{
		get
		{
			if (!(Entry.Model.Slot == "Body"))
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public string WheelsText
	{
		get
		{
			if (!Entry.Model.OwnWheels)
			{
				return "RL wheels on";
			}
			return "RL wheels off · own wheels";
		}
	}

	public RlModelVm(RlCustom.Entry entry)
	{
		Entry = entry;
	}

	private static Brush Freeze(SolidColorBrush b)
	{
		b.Freeze();
		return b;
	}
}
