using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Drip.App;

public sealed class EpicTipWindow : Window
{
	private sealed record Slide(string Image, string Title, string Body);

	private static readonly Slide[] Slides = new Slide[4]
	{
		new Slide("tip1.png", "Open Library, then press the … next to Fortnite", "In the Epic Games Launcher, go to Library and click the three dots under the Fortnite card."),
		new Slide("tip2.png", "Click Options", "It's in the menu that pops up, right under “Explore Addons”."),
		new Slide("tip3.png", "Tick Epic Quality Assets, then Apply", "It's about 27 GB. Let it finish downloading before you swap. This is where most of the skins and emotes come from."),
		new Slide("tip4.png", "Not every skin and emote will work", "Even with this on, some items can still show up empty or do nothing. Faded items in the lists are the ones missing on your PC: pick a bright one.")
	};

	private readonly Color _accent;

	private readonly Image _image = new Image
	{
		Stretch = Stretch.Uniform,
		SnapsToDevicePixels = true
	};

	private readonly TextBlock _title = new TextBlock
	{
		FontSize = 22.0,
		FontWeight = FontWeights.SemiBold,
		Foreground = Brush("#F4F4F5"),
		TextWrapping = TextWrapping.Wrap
	};

	private readonly TextBlock _body = new TextBlock
	{
		FontSize = 14.0,
		LineHeight = 21.0,
		Foreground = Brush("#A3A3AB"),
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
	};

	private readonly StackPanel _dots = new StackPanel
	{
		Orientation = Orientation.Horizontal,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly Border _back = new Border();

	private readonly Border _next = new Border();

	private readonly TextBlock _nextText = new TextBlock();

	private readonly TextBlock _backText = new TextBlock();

	private int _i;

	public EpicTipWindow(Color accent)
	{
		_accent = accent;
		Width = 920.0;
		Height = 700.0;
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
		SnapsToDevicePixels = true;
		UseLayoutRounding = true;
		Border border = new Border
		{
			Background = Brush("#0E0E10"),
			BorderBrush = Brush("#2A2A2F"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(20.0),
			Margin = new Thickness(14.0)
		};
		border.Effect = new DropShadowEffect
		{
			BlurRadius = 36.0,
			ShadowDepth = 8.0,
			Opacity = 0.45,
			Color = Colors.Black
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(26.0, 20.0, 26.0, 22.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0),
			LastChildFill = true
		};
		TextBlock skip = new TextBlock
		{
			Text = "Skip",
			FontSize = 13.0,
			Foreground = Brush("#85858E"),
			Cursor = Cursors.Hand,
			VerticalAlignment = VerticalAlignment.Center
		};
		skip.MouseEnter += (object _, MouseEventArgs _) =>
		{
			skip.Foreground = Brush("#F4F4F5");
		};
		skip.MouseLeave += (object _, MouseEventArgs _) =>
		{
			skip.Foreground = Brush("#85858E");
		};
		skip.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			Close();
		};
		DockPanel.SetDock(skip, Dock.Right);
		dockPanel.Children.Add(skip);
		dockPanel.Children.Add(new TextBlock
		{
			Text = "Get every skin and emote working",
			FontSize = 13.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(accent),
			VerticalAlignment = VerticalAlignment.Center
		});
		dockPanel.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource != skip)
			{
				try
				{
					DragMove();
				}
				catch
				{
				}
			}
		};
		Grid.SetRow(dockPanel, 0);
		grid.Children.Add(dockPanel);
		Border frame = new Border
		{
			Background = Brush("#09090B"),
			CornerRadius = new CornerRadius(14.0),
			ClipToBounds = true,
			Child = _image
		};
		frame.SizeChanged += (object _, SizeChangedEventArgs e) =>
		{
			frame.Clip = new RectangleGeometry(new Rect(0.0, 0.0, e.NewSize.Width, e.NewSize.Height), 14.0, 14.0);
		};
		Grid.SetRow(frame, 1);
		grid.Children.Add(frame);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(2.0, 16.0, 2.0, 0.0)
		};
		stackPanel.Children.Add(_title);
		stackPanel.Children.Add(_body);
		Grid.SetRow(stackPanel, 2);
		grid.Children.Add(stackPanel);
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		Style(_back, _backText, "Back", filled: false);
		Style(_next, _nextText, "Next", filled: true);
		_back.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		stackPanel2.Children.Add(_back);
		stackPanel2.Children.Add(_next);
		DockPanel.SetDock(stackPanel2, Dock.Right);
		dockPanel2.Children.Add(stackPanel2);
		dockPanel2.Children.Add(_dots);
		Grid.SetRow(dockPanel2, 3);
		grid.Children.Add(dockPanel2);
		_back.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			Go(_i - 1);
		};
		_next.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			if (_i >= Slides.Length - 1)
			{
				Close();
			}
			else
			{
				Go(_i + 1);
			}
		};
		border.Child = grid;
		Content = border;
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Right || e.Key == Key.Return)
			{
				if (_i >= Slides.Length - 1)
				{
					Close();
				}
				else
				{
					Go(_i + 1);
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Left)
			{
				Go(_i - 1);
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}
		};
		Show(0, animate: false);
	}

	private static SolidColorBrush Brush(string hex)
	{
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
	}

	private new void Style(Border b, TextBlock t, string text, bool filled)
	{
		t.Text = text;
		t.FontSize = 14.0;
		t.FontWeight = FontWeights.SemiBold;
		t.HorizontalAlignment = HorizontalAlignment.Center;
		t.VerticalAlignment = VerticalAlignment.Center;
		t.Foreground = (filled ? Brush("#0E0E10") : Brush("#D6D6DA"));
		b.Child = t;
		b.Cursor = Cursors.Hand;
		b.CornerRadius = new CornerRadius(12.0);
		b.Padding = new Thickness(26.0, 10.0, 26.0, 10.0);
		b.MinWidth = 96.0;
		b.Background = (filled ? new SolidColorBrush(_accent) : Brush("#202024"));
		b.MouseEnter += (object _, MouseEventArgs _) =>
		{
			b.Opacity = 0.88;
		};
		b.MouseLeave += (object _, MouseEventArgs _) =>
		{
			b.Opacity = 1.0;
		};
	}

	private void Go(int index)
	{
		if (index >= 0 && index < Slides.Length)
		{
			Show(index, animate: true);
		}
	}

	private void Show(int index, bool animate)
	{
		_i = index;
		Slide slide = Slides[index];
		try
		{
			_image.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/EpicTip/" + slide.Image, UriKind.Absolute));
		}
		catch
		{
			_image.Source = null;
		}
		_title.Text = slide.Title;
		_body.Text = slide.Body;
		_nextText.Text = ((index == Slides.Length - 1) ? "Got it" : "Next");
		_back.Visibility = ((index == 0) ? Visibility.Hidden : Visibility.Visible);
		_dots.Children.Clear();
		for (int i = 0; i < Slides.Length; i++)
		{
			_dots.Children.Add(new Border
			{
				Width = ((i == index) ? 26 : 9),
				Height = 9.0,
				CornerRadius = new CornerRadius(5.0),
				Margin = new Thickness(0.0, 0.0, 7.0, 0.0),
				Background = ((i == index) ? new SolidColorBrush(_accent) : Brush("#3A3A40"))
			});
		}
		if (animate)
		{
			DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180L));
			_image.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}
}
