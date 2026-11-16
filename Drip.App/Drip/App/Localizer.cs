using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Drip.Core.Localization;

namespace Drip.App;

internal static class Localizer
{
	private static readonly ConditionalWeakTable<object, object> Hooked = new ConditionalWeakTable<object, object>();

	private static bool _busy;

	public static void Install()
	{
		Loc.Init();
		if (!Loc.IsEnglish)
		{
			EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, (RoutedEventHandler)((object s, RoutedEventArgs _) =>
			{
				Translate(s as FrameworkElement);
			}));
		}
	}

	public static void Apply(Window w)
	{
		if (Loc.IsEnglish)
		{
			return;
		}
		try
		{
			w.Title = Loc.T(w.Title);
			Walk(w);
		}
		catch
		{
		}
	}

	private static void Walk(DependencyObject d)
	{
		if (d is FrameworkElement fe)
		{
			Translate(fe);
		}
		foreach (object child in LogicalTreeHelper.GetChildren(d))
		{
			if (child is DependencyObject d2)
			{
				Walk(d2);
			}
		}
	}

	private static void Translate(FrameworkElement? fe)
	{
		if (fe == null || Loc.IsEnglish)
		{
			return;
		}
		try
		{
			if (fe.ToolTip is string { Length: >0 } text)
			{
				fe.ToolTip = Loc.T(text);
			}
			if (!(fe is TextBlock tb))
			{
				if (!(fe is HeaderedContentControl headeredContentControl))
				{
					if (fe is ContentControl contentControl)
					{
						if (contentControl.Content is string s)
						{
							contentControl.Content = Loc.T(s);
						}
						if (!(contentControl is Window) && contentControl.Name.Length > 0)
						{
							Watch(contentControl, ContentControl.ContentProperty, typeof(ContentControl));
						}
					}
				}
				else
				{
					if (headeredContentControl.Header is string s2)
					{
						headeredContentControl.Header = Loc.T(s2);
					}
					if (headeredContentControl.Content is string s3)
					{
						headeredContentControl.Content = Loc.T(s3);
					}
				}
			}
			else
			{
				TranslateTextBlock(tb);
			}
		}
		catch
		{
		}
	}

	private static void TranslateTextBlock(TextBlock tb)
	{
		if (tb.Inlines.Count == 1 && tb.Inlines.FirstInline is Run run)
		{
			string text = Loc.T(run.Text);
			if (text != run.Text)
			{
				run.Text = text;
			}
		}
		else if (tb.Inlines.Count > 1)
		{
			foreach (Inline inline in tb.Inlines)
			{
				if (inline is Run run2 && run2.Text.Length > 3)
				{
					run2.Text = Loc.T(run2.Text);
				}
			}
		}
		if (tb.Name.Length > 0)
		{
			Watch(tb, TextBlock.TextProperty, typeof(TextBlock));
		}
	}

	private static void Watch(DependencyObject d, DependencyProperty prop, Type owner)
	{
		if (Hooked.TryGetValue(d, out object _))
		{
			return;
		}
		Hooked.Add(d, true);
		DependencyPropertyDescriptor.FromProperty(prop, owner)?.AddValueChanged(d, (object? _, EventArgs _) =>
		{
			if (_busy)
			{
				return;
			}
			_busy = true;
			try
			{
				DependencyObject dependencyObject = d;
				if (!(dependencyObject is TextBlock textBlock))
				{
					if (dependencyObject is ContentControl { Content: string content } contentControl)
					{
						string text = Loc.T(content);
						if (text != content)
						{
							contentControl.Content = text;
						}
					}
				}
				else if (textBlock.Inlines.Count <= 1 && textBlock.Text.Length > 0)
				{
					string text2 = Loc.T(textBlock.Text);
					if (text2 != textBlock.Text)
					{
						textBlock.Text = text2;
					}
				}
			}
			catch
			{
			}
			finally
			{
				_busy = false;
			}
		});
	}
}
