using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace Drip.App;

public sealed class BuildMaterialWindow : Window
{
	public string Choice { get; private set; } = "";

	public static string IconUri(string material)
	{
		return "pack://application:,,,/Assets/Builds/" + material + ".png";
	}

	private static ControlTemplate CardTemplate()
	{
		return (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='box' CornerRadius='12' BorderThickness='1' Padding='6' Background='{DynamicResource Panel}' BorderBrush='{DynamicResource Line}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
	}

	public BuildMaterialWindow()
	{
		Title = "Which builds?";
		Width = 400.0;
		SizeToContent = SizeToContent.Height;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		SetResourceReference(Control.BackgroundProperty, "WindowBg");
		SetResourceReference(Control.ForegroundProperty, "Text");
		TextBlock textBlock = new TextBlock
		{
			Text = "Which builds?",
			FontSize = 20.0,
			FontWeight = FontWeights.SemiBold
		};
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBright");
		TextBlock textBlock2 = new TextBlock
		{
			Text = "Pick the build material this look goes on.",
			Margin = new Thickness(0.0, 6.0, 0.0, 14.0),
			TextWrapping = TextWrapping.Wrap
		};
		textBlock2.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
		UniformGrid uniformGrid = new UniformGrid
		{
			Rows = 1,
			Columns = 3
		};
		(string, string)[] array = new (string, string)[3]
		{
			("Wood", "wood"),
			("Brick", "brick"),
			("Metal", "metal")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			Image element = new Image
			{
				Width = 72.0,
				Height = 72.0,
				Source = new BitmapImage(new Uri(IconUri(item2)))
			};
			TextBlock textBlock3 = new TextBlock
			{
				Text = item,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				FontWeight = FontWeights.SemiBold
			};
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(0.0, 6.0, 0.0, 6.0)
			};
			stackPanel.Children.Add(element);
			stackPanel.Children.Add(textBlock3);
			Button button = new Button
			{
				Content = stackPanel,
				Height = 120.0,
				Margin = new Thickness(0.0, 0.0, (!(item2 == "metal")) ? 8 : 0, 0.0),
				Template = CardTemplate(),
				Cursor = Cursors.Hand
			};
			textBlock3.SetResourceReference(TextBlock.ForegroundProperty, "TextBright");
			string k = item2;
			button.Click += (object _, RoutedEventArgs _) =>
			{
				Choice = k;
				DialogResult = true;
			};
			uniformGrid.Children.Add(button);
		}
		Button element2 = new Button
		{
			Content = "Cancel",
			IsCancel = true,
			MinWidth = 90.0,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(24.0, 18.0, 24.0, 20.0)
		};
		stackPanel2.Children.Add(textBlock);
		stackPanel2.Children.Add(textBlock2);
		stackPanel2.Children.Add(uniformGrid);
		stackPanel2.Children.Add(element2);
		Content = stackPanel2;
	}
}
