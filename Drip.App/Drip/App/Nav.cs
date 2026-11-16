using System.Windows;
using System.Windows.Media;

namespace Drip.App;

public static class Nav
{
	public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached("Icon", typeof(Geometry), typeof(Nav), new PropertyMetadata(null));

	public static Geometry? GetIcon(DependencyObject o)
	{
		return (Geometry)o.GetValue(IconProperty);
	}

	public static void SetIcon(DependencyObject o, Geometry? value)
	{
		o.SetValue(IconProperty, value);
	}
}
