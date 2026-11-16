using System.Windows;
using Drip.Core.Localization;

namespace Drip.App;

internal static class Dlg
{
	public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
	{
		text = Loc.T(text);
		caption = Loc.T(caption);
		if (owner == null)
		{
			return MessageBox.Show(text, caption, buttons, icon);
		}
		return MessageBox.Show(owner, text, caption, buttons, icon);
	}

	public static MessageBoxResult Show(string text, string caption = "Drip Swapper", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
	{
		return Show(null, text, caption, buttons, icon);
	}
}
