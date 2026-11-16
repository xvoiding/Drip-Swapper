using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using Drip.Core;

namespace Drip.App;

internal static class Shell
{
	public static void Setup(Window w, double minWidth = 920.0, double minHeight = 620.0)
	{
		Rect workArea = SystemParameters.WorkArea;
		w.MaxWidth = workArea.Width;
		w.MaxHeight = workArea.Height;
		w.MinWidth = Math.Min(minWidth, workArea.Width);
		w.MinHeight = Math.Min(minHeight, workArea.Height);
		w.Width = Math.Min(w.Width, Math.Max(w.MinWidth, workArea.Width - 24.0));
		w.Height = Math.Min(w.Height, Math.Max(w.MinHeight, workArea.Height - 24.0));
		w.ResizeMode = ResizeMode.CanResize;
		WindowChrome.SetWindowChrome(w, new WindowChrome
		{
			CaptionHeight = 0.0,
			ResizeBorderThickness = new Thickness(6.0),
			GlassFrameThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(0.0),
			UseAeroCaptionButtons = false
		});
		RememberPlacement(w);
		Shortcuts(w);
		Localizer.Apply(w);
	}

	private static void Shortcuts(Window w)
	{
		w.PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			try
			{
				if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
				{
					string[] array = new string[2] { "SearchBox", "WardSearchA" };
					foreach (string name in array)
					{
						if (w.FindName(name) is TextBox { IsVisible: not false } textBox)
						{
							textBox.Focus();
							textBox.SelectAll();
							e.Handled = true;
							break;
						}
					}
					return;
				}
				bool flag = e.Key == Key.Escape;
				TextBox textBox2 = default;
				bool flag2;
				if (flag)
				{
					textBox2 = Keyboard.FocusedElement as TextBox;
					if (textBox2 == null)
					{
						goto IL_00dc;
					}
					switch (textBox2.Name)
					{
					case "SearchBox":
					case "WardSearchA":
					case "WardSearchB":
					case "OptSearch":
						break;
					default:
						goto IL_00dc;
					}
					flag2 = true;
					goto IL_00df;
				}
				goto IL_00e3;
				IL_00df:
				flag = flag2;
				goto IL_00e3;
				IL_00dc:
				flag2 = false;
				goto IL_00df;
				IL_00e3:
				if (flag && textBox2.Text.Length > 0)
				{
					textBox2.Clear();
					e.Handled = true;
				}
			}
			catch
			{
			}
		};
	}

	private static void RememberPlacement(Window w)
	{
		string key = w.GetType().Name;
		try
		{
			Placement placement = WindowPlacement.Fit(WindowPlacement.Load(key), w.MinWidth, w.MinHeight, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
			if (placement != null)
			{
				w.WindowStartupLocation = WindowStartupLocation.Manual;
				w.Left = placement.Left;
				w.Top = placement.Top;
				w.Width = placement.Width;
				w.Height = placement.Height;
			}
		}
		catch
		{
		}
		w.Closing += (object? _, CancelEventArgs _) =>
		{
			if (w.WindowState == WindowState.Normal)
			{
				WindowPlacement.Save(key, new Placement(w.Left, w.Top, w.ActualWidth, w.ActualHeight));
			}
		};
	}
}
