using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Drip.Core.Fun;

namespace Drip.App;

public partial class FunPanel : UserControl, IComponentConnector, IStyleConnector
{
	private sealed record FitRow(Fit Fit)
	{
		public string Name => Fit.Name;

		public string Sub => $"{Fit.Entries.Count} swap{((Fit.Entries.Count == 1) ? "" : "s")}:  " + string.Join(",  ", from e in Fit.Entries.Take(3)
			select e.Source + " → " + e.Target);
	}

	private string _game = "fortnite";

	private FunStore _store;

	private Func<IReadOnlyList<FitEntry>> _current = () => Array.Empty<FitEntry>();

	private Func<Fit, Task<string>> _apply = (Fit _) => Task.FromResult("This game can't apply loadouts yet.");

	private Action<string, string> _notice = (string _, string _) =>
	{
	};

	public FunPanel()
	{
		InitializeComponent();
	}

	public void Attach(string game, FunStore store, Func<IReadOnlyList<FitEntry>> current, Func<Fit, Task<string>> apply, Action<string, string> notice)
	{
		_game = game;
		_store = store;
		_current = current;
		_apply = apply;
		_notice = notice;
		Refresh();
	}

	public void Refresh()
	{
		if (_store != null)
		{
			List<FitRow> list = (from f in _store.FitsFor(_game)
				select new FitRow(f)).ToList();
			FitsHost.ItemsSource = list;
			FitsEmpty.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void FitSave_Click(object sender, RoutedEventArgs e)
	{
		IReadOnlyList<FitEntry> readOnlyList = _current();
		if (readOnlyList.Count == 0)
		{
			FitStatus.Text = "Nothing is swapped right now, so there's nothing to save. Do a swap first.";
			return;
		}
		try
		{
			Fit fit = _store.SaveFit(_game, FitName.Text, readOnlyList);
			FitStatus.Text = $"Saved \"{fit.Name}\" with {fit.Entries.Count} swap{((fit.Entries.Count == 1) ? "" : "s")}.";
		}
		catch (Exception ex)
		{
			FitStatus.Text = ex.Message;
		}
		Refresh();
	}

	private async void FitApply_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: FitRow tag })
		{
			FitStatus.Text = "Applying \"" + tag.Name + "\"...";
			try
			{
				TextBlock fitStatus = FitStatus;
				fitStatus.Text = await _apply(tag.Fit);
			}
			catch (Exception ex)
			{
				FitStatus.Text = ex.Message;
			}
			Refresh();
		}
	}

	private void FitShare_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: FitRow tag }))
		{
			return;
		}
		string text = _store.ShareCode(tag.Fit);
		try
		{
			Clipboard.SetText(text);
			FitStatus.Text = "Code copied. Send it to a friend; they paste it under Import.";
		}
		catch
		{
			_notice("Your loadout code", text);
			FitStatus.Text = "Couldn't reach the clipboard, so the code is in the popup.";
		}
	}

	private void FitDelete_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: FitRow tag })
		{
			_store.DeleteFit(tag.Fit);
			FitStatus.Text = "Deleted \"" + tag.Name + "\".";
			Refresh();
		}
	}

	private void FitImport_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Fit fit = _store.ImportCode(ImportBox.Text);
			FitStatus.Text = ((fit.Game != _game) ? $"Imported \"{fit.Name}\". It's a {FunGames.Pretty(fit.Game)} loadout, so it shows up in that game's Loadouts tab." : $"Imported \"{fit.Name}\" with {fit.Entries.Count} swap{((fit.Entries.Count == 1) ? "" : "s")}. Press Apply to use it.");
			ImportBox.Clear();
		}
		catch (FormatException ex)
		{
			FitStatus.Text = ex.Message;
		}
		Refresh();
	}
}
