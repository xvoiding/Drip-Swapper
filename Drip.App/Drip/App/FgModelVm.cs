using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using Drip.Core.FallGuys;

namespace Drip.App;

public sealed class FgModelVm : INotifyPropertyChanged
{
	private BitmapSource? _icon;

	public FgModelLibrary.Entry Entry { get; }

	public string Name => Entry.File.Name;

	public string Notes
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Entry.File.Notes))
			{
				return Entry.File.Notes;
			}
			return "Custom bean model";
		}
	}

	public string Author
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Entry.File.Author))
			{
				return "by " + Entry.File.Author;
			}
			return "Unknown maker";
		}
	}

	public string Triangles => $"{Entry.File.Triangles:N0} triangles";

	public BitmapSource? Icon
	{
		get
		{
			return _icon;
		}
		set
		{
			_icon = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Icon"));
		}
	}

	public bool Active { get; set; }

	public Visibility ActiveVisibility
	{
		get
		{
			if (!Active)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public FgModelVm(FgModelLibrary.Entry e)
	{
		Entry = e;
	}
}
