using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Drip.Core.Swapping;

namespace Drip.App;

public sealed class SwappedVm : INotifyPropertyChanged
{
	private static readonly Brush Good = Freeze("#2ECC71");

	private static readonly Brush Bad = Freeze("#D30000");

	private static readonly Brush Dim = Freeze("#6B7280");

	private bool? _active;

	public SwappedItem Item { get; }

	public ImageSource? SourceIcon => IconCache.Load(Item.SourceIcon);

	public ImageSource? TargetIcon => IconCache.Load(Item.TargetIcon);

	public string SourceName
	{
		get
		{
			if (!string.IsNullOrEmpty(Item.SourceStyle))
			{
				return Item.SourceName + " (" + Item.SourceStyle + ")";
			}
			return Item.SourceName;
		}
	}

	public string TargetName
	{
		get
		{
			if (!string.IsNullOrEmpty(Item.TargetStyle))
			{
				return Item.TargetName + " (" + Item.TargetStyle + ")";
			}
			return Item.TargetName;
		}
	}

	public string Title => SourceName + "  →  " + TargetName;

	public string Subtitle => $"{(string.IsNullOrEmpty(Item.Kind) ? "Outfit" : Item.Kind)}  ·  Converted {Item.ConvertedAt:g}  ·  {Item.Parts.Count} asset{((Item.Parts.Count == 1) ? "" : "s")}";

	public string NotesText
	{
		get
		{
			if (Item.Notes.Count != 0)
			{
				return "Might look different: " + string.Join("  ", Item.Notes);
			}
			return "";
		}
	}

	public Visibility NotesVisible
	{
		get
		{
			if (Item.Notes.Count != 0)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public bool? Active
	{
		get
		{
			return _active;
		}
		set
		{
			_active = value;
			Raise("StatusText");
			Raise("StatusBrush");
		}
	}

	public string StatusText
	{
		get
		{
			bool? active = _active;
			if (active.HasValue)
			{
				if (active == true)
				{
					return "ACTIVE";
				}
				return "UNDONE";
			}
			return "";
		}
	}

	public Brush StatusBrush
	{
		get
		{
			bool? active = _active;
			if (active.HasValue)
			{
				if (active == true)
				{
					return Good;
				}
				return Bad;
			}
			return Dim;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public SwappedVm(SwappedItem item)
	{
		Item = item;
	}

	private static Brush Freeze(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private void Raise(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
