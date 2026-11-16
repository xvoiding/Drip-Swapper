using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Drip.Core.FallGuys;
using Drip.Core.Install;
using Drip.Core.Localization;
using Drip.Core.RocketLeague;

namespace Drip.App;

public partial class GameChooserWindow : Window, IComponentConnector
{
	public GameChooserWindow()
	{
		InitializeComponent();
		Theme.SetGame(null);
		Shell.Setup(this, 720.0, 440.0);
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			BitmapSource rl = await Task.Run((Func<BitmapSource>)GameIcons.RocketLeague);
			BitmapSource fn = await Task.Run((Func<BitmapSource>)GameIcons.Fortnite);
			BitmapSource bitmapSource = await Task.Run((Func<BitmapSource>)GameIcons.FallGuys);
			if (rl != null)
			{
				RlIcon.Source = rl;
				RlLetter.Visibility = Visibility.Collapsed;
			}
			if (fn != null)
			{
				FnIcon.Source = fn;
				FnLetter.Visibility = Visibility.Collapsed;
			}
			if (bitmapSource != null)
			{
				FgIcon.Source = bitmapSource;
				FgLetter.Visibility = Visibility.Collapsed;
			}
			if (fn != null)
			{
				PkFnIcon.Source = fn;
				PkFnLetter.Visibility = Visibility.Collapsed;
			}
			BitmapSource bitmapSource2 = await Task.Run((Func<BitmapSource>)GameIcons.Retrac);
			if (bitmapSource2 != null)
			{
				PkRtIcon.Source = bitmapSource2;
				PkRtLetter.Visibility = Visibility.Collapsed;
			}
			BitmapSource st = await Task.Run((Func<BitmapSource>)GameIcons.Stellar);
			if (st != null)
			{
				PkStIcon.Source = st;
				PkStLetter.Visibility = Visibility.Collapsed;
			}
		};
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			TextBlock rlState = RlState;
			Show(rlState, await Has(() => RlLocator.FindCookedDirectory()));
			rlState = FnState;
			Show(rlState, await Has(() => FortniteLocator.FindPaksDirectory()));
			rlState = FgState;
			Show(rlState, await Has(() => FgLocator.Find()));
			static async Task<bool> Has(Func<object?> find)
			{
				try
				{
					return await Task.Run(find) != null;
				}
				catch
				{
					return false;
				}
			}
			static void Show(TextBlock t, bool found)
			{
				t.Text = (found ? "● Found on this PC" : "○ Not found yet: you can point to it inside");
				t.Opacity = (found ? 1.0 : 0.7);
			}
		};
	}

	private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void Theme_Click(object sender, RoutedEventArgs e)
	{
		Theme.Toggle();
	}

	private void Language_Click(object sender, RoutedEventArgs e)
	{
		ContextMenu menu = new ContextMenu
		{
			PlacementTarget = LangBtn,
			Placement = PlacementMode.Bottom
		};
		string cur = Loc.Choice;
		Add("auto", Loc.T("Automatic (same as Windows)"));
		(string, string)[] known = Loc.Known;
		for (int i = 0; i < known.Length; i++)
		{
			var (code, header) = known[i];
			Add(code, header);
		}
		menu.IsOpen = true;
		void Add(string text, string header2)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = header2,
				IsChecked = (cur == text)
			};
			menuItem.Click += (object _, RoutedEventArgs _) =>
			{
				if (cur == text)
				{
					return;
				}
				Loc.SaveChoice(text);
				try
				{
					Process.Start(new ProcessStartInfo(Environment.ProcessPath)
					{
						UseShellExecute = true
					});
					Application.Current.Shutdown();
				}
				catch
				{
					Dlg.Show(this, "Close and open Drip Swapper again to use the new language.", "Language");
				}
			};
			menu.Items.Add(menuItem);
		}
	}

	private void Discord_Click(object sender, RoutedEventArgs e)
	{
		App.OpenDiscord();
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

	private void RocketLeague_Click(object sender, MouseButtonEventArgs e)
	{
		App.Open(Game.RocketLeague, this);
	}

	private void Fortnite_Click(object sender, MouseButtonEventArgs e)
	{
		ShowFortnitePicker(show: true);
	}

	private void FnNormal_Click(object sender, MouseButtonEventArgs e)
	{
		App.Open(Game.Fortnite, this);
	}

	private void FnRetrac_Click(object sender, MouseButtonEventArgs e)
	{
		App.Open(Game.Retrac, this);
	}

	private void FnStellar_Click(object sender, MouseButtonEventArgs e)
	{
		App.Open(Game.Stellar, this);
	}

	private void FnPickerClose_Click(object sender, RoutedEventArgs e)
	{
		ShowFortnitePicker(show: false);
	}

	private void FnPickerBackdrop_Down(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource == FnPicker)
		{
			ShowFortnitePicker(show: false);
		}
	}

	private void ShowFortnitePicker(bool show)
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		if (show)
		{
			FnPicker.Visibility = Visibility.Visible;
			FnPicker.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160L)));
			FnPickerCard.RenderTransform = new TranslateTransform(0.0, 14.0);
			((TranslateTransform)FnPickerCard.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, TimeSpan.FromMilliseconds(240L))
			{
				EasingFunction = easingFunction
			});
		}
		else
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120L));
			doubleAnimation.Completed += (object? _, EventArgs _) =>
			{
				FnPicker.Visibility = Visibility.Collapsed;
			};
			FnPicker.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	private void FallGuys_Click(object sender, MouseButtonEventArgs e)
	{
		App.Open(Game.FallGuys, this);
	}
}
