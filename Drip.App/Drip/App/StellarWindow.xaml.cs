using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drip.Core.Cosmetics;
using Drip.Core.Stellar;

namespace Drip.App;

/// <summary>Swapper for Project Stellar (Fortnite 12.41, Chapter 2 Season 2). Only shows items from Chapter 2 Season 2 and older.</summary>
public partial class StellarWindow : Window
{
	/// <summary>Newest season Stellar can play: Chapter 2 Season 2 (season index 12).</summary>
	private const int NewestSeasonIndex = 12;

	private sealed class StellarSettings
	{
		public string GamePath { get; set; } = "";

		private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drip Swapper", "stellar.json");

		public static StellarSettings Load()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					return JsonSerializer.Deserialize<StellarSettings>(File.ReadAllText(FilePath)) ?? new StellarSettings();
				}
			}
			catch
			{
			}
			return new StellarSettings();
		}

		public void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
				File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
			}
			catch
			{
			}
		}
	}

	public sealed class ItemVm
	{
		private static readonly Dictionary<string, ImageSource> Icons = new Dictionary<string, ImageSource>();
		private ImageSource? _icon;
		private readonly string _iconUrl;

		public string Id { get; }
		public string Category { get; }
		public string Name { get; }
		public string Season { get; }
		public string Rarity { get; }
		public string Sub { get; }
		public int Order { get; }

		public ImageSource? Icon
		{
			get
			{
				if (_icon == null && _iconUrl.Length > 0)
				{
					try
					{
						if (!Icons.TryGetValue(_iconUrl, out ImageSource? cached))
						{
							BitmapImage bmp = new BitmapImage();
							bmp.BeginInit();
							bmp.UriSource = new Uri(_iconUrl, UriKind.Absolute);
							bmp.DecodePixelWidth = 160;
							bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
							bmp.EndInit();
							Icons[_iconUrl] = bmp;
							cached = bmp;
						}
						_icon = cached;
					}
					catch
					{
					}
				}
				return _icon;
			}
		}

		public ItemVm(string category, string id, string name, string season, string rarity, string iconUrl, int order)
		{
			Category = category;
			Id = id;
			Name = name;
			Season = season;
			Rarity = rarity;
			_iconUrl = iconUrl;
			Order = order;
			Sub = (rarity.Length > 0 ? rarity + "  " : "") + name + (season.Length > 0 ? "  " + season : "") + "\n" + id;
		}
	}

	public sealed class SwapVm
	{
		public StellarSwap Swap { get; }
		public string Title { get; }
		public string Subtitle { get; }
		public ImageSource? TargetIcon { get; }
		public ImageSource? SourceIcon { get; }

		public SwapVm(StellarSwap swap, string title, string subtitle, ImageSource? targetIcon, ImageSource? sourceIcon)
		{
			Swap = swap;
			Title = title;
			Subtitle = subtitle;
			TargetIcon = targetIcon;
			SourceIcon = sourceIcon;
		}
	}

	private readonly StellarSettings _settings = StellarSettings.Load();
	private StellarSwapper? _swapper;
	private List<StellarItem> _catalog = new List<StellarItem>();
	private readonly Dictionary<string, List<ItemVm>> _items = new Dictionary<string, List<ItemVm>>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, ItemVm> _lookup = new Dictionary<string, ItemVm>(StringComparer.OrdinalIgnoreCase);
	private List<StellarSwap> _active = new List<StellarSwap>();
	private string _cat = "skin";
	private bool _swapsPage;
	private ItemVm? _want;
	private bool _ready;
	private bool _busy;
	private const int MaxShown = 300;

	private static readonly string[] RarityOrder = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic", "Exotic", "Transcendent", "Marvel Series", "DC Series", "Icon Series", "Star Wars Series", "Gaming Legends Series", "Shadow Series", "Slurp Series", "Lava Series", "Frozen Series", "Dark Series", "Platform Series" };

	public StellarWindow()
	{
		InitializeComponent();
		Theme.SetGame(Game.Stellar);
		Shell.Setup(this, 980.0, 640.0);
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			try
			{
				await LoadAsync();
			}
			catch (Exception ex)
			{
				SetBusy(false);
				ShowNotice("Something went wrong opening Stellar: " + ex.Message);
				SetStatus("Couldn't load.");
			}
		};
	}

	private string? FindGameFolder()
	{
		List<string> candidates = new List<string>();
		if (_settings.GamePath.Length > 0)
		{
			candidates.Add(_settings.GamePath);
		}
		candidates.Add(@"C:\Project Stellar\Fortnite Build\Fortnite 12.41");
		try
		{
			foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
			{
				string root = Path.Combine(drive.RootDirectory.FullName, "Project Stellar", "Fortnite Build");
				if (Directory.Exists(root))
				{
					candidates.AddRange(Directory.GetDirectories(root).OrderByDescending(d => d.Contains("12.41", StringComparison.Ordinal)));
				}
			}
		}
		catch
		{
		}
		foreach (string c in candidates)
		{
			string? paks = StellarSwapper.FindPaks(c);
			if (paks != null)
			{
				return paks;
			}
		}
		return null;
	}

	private async Task LoadAsync()
	{
		_ready = false;
		string? paks = null;
		try
		{
			paks = FindGameFolder();
		}
		catch
		{
		}
		if (paks == null)
		{
			ShowNotice("Drip Swapper couldn't find Project Stellar's Fortnite 12.41. Press Game folder and pick the folder that contains FortniteGame (for example C:\\Project Stellar\\Fortnite Build\\Fortnite 12.41).");
			SetStatus("Waiting for your Stellar folder.");
			EmptyText.Text = "Pick your Stellar folder with Game folder at the top.";
			return;
		}
		Notice.Visibility = Visibility.Collapsed;
		EmptyText.Text = "";
		_swapper = new StellarSwapper(paks);
		SetBusy(true);
		SetStatus("Reading your Stellar files...");
		try
		{
			StellarSwapper swapper = _swapper;
			Task<List<StellarItem>> scan = Task.Run(() => swapper.Scan(msg => Dispatcher.Invoke(() => SetStatus(msg))));
			Task<Dictionary<string, Cosmetic>> meta = FetchMetaAsync();
			_catalog = await scan;
			Dictionary<string, Cosmetic> info = await meta;
			BuildLists(info);
			_active = swapper.Current();
		}
		catch (Exception ex)
		{
			SetBusy(false);
			ShowNotice("Something went wrong reading your Stellar files: " + ex.Message);
			SetStatus("Couldn't load.");
			return;
		}
		SetBusy(false);
		FillRarities();
		_ready = true;
		ShowPage();
	}

	private void FillRarities()
	{
		List<string> found = _items.Values.SelectMany(l => l).Select(v => v.Rarity).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r =>
		{
			int i = Array.FindIndex(RarityOrder, x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));
			return i < 0 ? 999 : i;
		}).ThenBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
		List<string> all = new List<string> { "All rarities" };
		all.AddRange(found);
		RarityBox.ItemsSource = all;
		RarityBox.SelectedIndex = 0;
	}

	private static async Task<Dictionary<string, Cosmetic>> FetchMetaAsync()
	{
		Dictionary<string, Cosmetic> map = new Dictionary<string, Cosmetic>(StringComparer.OrdinalIgnoreCase);
		string[] types = { "outfit", "pickaxe", "emote", "glider" };
		try
		{
			List<Cosmetic>[] lists = await Task.WhenAll(types.Select(t => CosmeticApi.FetchAsync(t)));
			foreach (List<Cosmetic> list in lists)
			{
				foreach (Cosmetic c in list)
				{
					map[c.Id] = c;
				}
			}
		}
		catch
		{
			map.Clear();
		}
		return map;
	}

	private static bool IsJunk(Cosmetic c)
	{
		return c.Id.Contains("Default", StringComparison.OrdinalIgnoreCase) || c.Id.Contains("Random", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("TBD", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("HIDDEN", StringComparison.OrdinalIgnoreCase);
	}

	private void BuildLists(Dictionary<string, Cosmetic> info)
	{
		_items.Clear();
		_lookup.Clear();
		foreach (string cat in StellarSwapper.Categories)
		{
			_items[cat] = new List<ItemVm>();
		}
		bool haveMeta = info.Count > 0;
		foreach (StellarItem item in _catalog)
		{
			int order;
			ItemVm vm;
			if (haveMeta)
			{
				if (!info.TryGetValue(item.Id, out Cosmetic? c) || IsJunk(c))
				{
					continue;
				}
				order = CosmeticRank.SeasonIndex(c.Introduced);
				if (order > NewestSeasonIndex)
				{
					continue;
				}
				vm = new ItemVm(item.Category, item.Id, c.Name, c.Introduced, c.Rarity, c.IconUrl, order);
			}
			else
			{
				vm = new ItemVm(item.Category, item.Id, item.Id, "", "", "", 0);
			}
			_items[item.Category].Add(vm);
			_lookup[item.Category + "/" + item.Id] = vm;
		}
		foreach (string cat in StellarSwapper.Categories)
		{
			_items[cat] = _items[cat].OrderByDescending(v => v.Order).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
		}
		if (!haveMeta)
		{
			ShowNotice("Couldn't reach the cosmetics list (are you offline?), so every item in your Stellar files is shown without names or pictures. Open Drip Swapper again online to see names and pictures.");
		}
	}

	private static string One(string cat) => cat switch
	{
		"skin" => "skin",
		"pickaxe" => "pickaxe",
		"emote" => "emote",
		_ => "glider"
	};

	private static string Many(string cat) => cat switch
	{
		"skin" => "skins",
		"pickaxe" => "pickaxes",
		"emote" => "emotes",
		_ => "gliders"
	};

	private static string Label(string cat) => cat switch
	{
		"skin" => "Skin",
		"pickaxe" => "Pickaxe",
		"emote" => "Emote",
		_ => "Glider"
	};

	private void ShowPage()
	{
		PickPage.Visibility = _swapsPage ? Visibility.Collapsed : Visibility.Visible;
		SwappedPage.Visibility = _swapsPage ? Visibility.Visible : Visibility.Collapsed;
		SearchWrap.Visibility = _swapsPage ? Visibility.Collapsed : Visibility.Visible;
		if (_swapsPage)
		{
			PageTitle.Text = "Swapped";
			RefreshSwaps();
			return;
		}
		UpdateHeader();
		RebuildGrid();
	}

	private void UpdateHeader()
	{
		PageTitle.Text = "Pick " + Many(_cat);
		if (_want == null)
		{
			PageSub.Text = "Click the " + One(_cat) + " you want, then pick the one you equip. Chapter 2 Season 2 and older only.";
			WantBar.Visibility = Visibility.Collapsed;
		}
		else
		{
			PageSub.Text = "Now click the " + One(_cat) + " you equip in Stellar. It will show as " + _want.Name + ".";
			WantBar.Visibility = Visibility.Visible;
			WantIcon.Source = _want.Icon;
			WantName.Text = _want.Name;
		}
	}

	private void RebuildGrid()
	{
		if (!_ready || !_items.TryGetValue(_cat, out List<ItemVm>? list))
		{
			return;
		}
		string q = SearchBox.Text.Trim();
		string rarity = RarityBox.SelectedIndex > 0 ? RarityBox.SelectedItem as string ?? "" : "";
		IEnumerable<ItemVm> q1 = list;
		if (_want != null)
		{
			q1 = q1.Where(v => !string.Equals(v.Id, _want.Id, StringComparison.OrdinalIgnoreCase));
		}
		if (q.Length > 0)
		{
			q1 = q1.Where(v => v.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || v.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
		}
		if (rarity.Length > 0)
		{
			q1 = q1.Where(v => string.Equals(v.Rarity, rarity, StringComparison.OrdinalIgnoreCase));
		}
		q1 = SortBox.SelectedIndex switch
		{
			1 => q1.OrderBy(v => v.Order).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase),
			2 => q1.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase),
			_ => q1.OrderByDescending(v => v.Order).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
		};
		List<ItemVm> all = q1.ToList();
		GridHost.ItemsSource = all.Take(MaxShown).ToList();
		EmptyText.Text = all.Count == 0 ? (list.Count == 0 ? "Nothing found for this category in your Stellar files." : "Nothing matches. Clear the search or filter.") : "";
		CountText.Text = all.Count > MaxShown ? "Showing " + MaxShown + " of " + all.Count + " · search or filter to find others" : all.Count + " " + Many(_cat) + " from Chapter 2 Season 2 and older";
	}

	private void RefreshSwaps()
	{
		List<SwapVm> rows = new List<SwapVm>();
		foreach (StellarSwap s in _active)
		{
			_lookup.TryGetValue(s.Category + "/" + s.TargetId, out ItemVm? a);
			_lookup.TryGetValue(s.Category + "/" + s.SourceId, out ItemVm? b);
			string t = a?.Name ?? s.TargetId;
			string src = b?.Name ?? s.SourceId;
			rows.Add(new SwapVm(s, t + " shows as " + src, Label(s.Category) + ". Equip " + t + " in Stellar.", a?.Icon, b?.Icon));
		}
		SwapsHost.ItemsSource = rows;
		UndoAllBtn.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
		SwappedEmpty.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
		SwappedHint.Text = rows.Count == 0 ? "Your Stellar files are exactly as Stellar installed them." : rows.Count + (rows.Count == 1 ? " swap is" : " swaps are") + " in your Stellar files right now.";
		PageSub.Text = "The swaps that are in your Stellar files.";
	}

	private void ShowNotice(string text)
	{
		NoticeText.Text = text;
		Notice.Visibility = Visibility.Visible;
	}

	private void SetStatus(string text)
	{
		CountText.Text = text;
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
	}

	private void Search_Changed(object sender, TextChangedEventArgs e)
	{
		SearchHint.Visibility = SearchBox.Text.Length != 0 ? Visibility.Collapsed : Visibility.Visible;
		if (IsLoaded)
		{
			RebuildGrid();
		}
	}

	private void Sort_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (IsLoaded)
		{
			RebuildGrid();
		}
	}

	private void Tab_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: string tab })
		{
			_swapsPage = tab == "swaps";
			ShowPage();
		}
	}

	private void Cat_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: string cat })
		{
			_cat = cat;
			_want = null;
			SearchBox.Text = "";
			UpdateHeader();
			RebuildGrid();
		}
	}

	private void WantBack_Click(object sender, RoutedEventArgs e)
	{
		_want = null;
		UpdateHeader();
		RebuildGrid();
	}

	private async void Tile_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is not FrameworkElement { DataContext: ItemVm vm } || _busy)
		{
			return;
		}
		if (_want == null)
		{
			_want = vm;
			UpdateHeader();
			RebuildGrid();
			return;
		}
		ItemVm source = _want;
		List<StellarSwap> next = _active.Where(s => !(s.Category == vm.Category && string.Equals(s.TargetId, vm.Id, StringComparison.OrdinalIgnoreCase))).ToList();
		next.Add(new StellarSwap(vm.Category, vm.Id, source.Id));
		if (await ApplyAsync(next, "Done. Start Stellar, equip " + vm.Name + " and you'll get " + source.Name + "."))
		{
			_want = null;
			UpdateHeader();
			RebuildGrid();
		}
	}

	private static bool GameRunning()
	{
		try
		{
			return Process.GetProcessesByName("FortniteClient-Win64-Shipping").Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> ApplyAsync(List<StellarSwap> list, string okText)
	{
		if (_swapper == null || _busy)
		{
			return false;
		}
		if (GameRunning())
		{
			Dlg.Show(this, "Fortnite is open. Close it first, then try again.", "Stellar");
			return false;
		}
		SetBusy(true);
		SetStatus("Writing your swap...");
		StellarSwapper swapper = _swapper;
		List<StellarItem> catalog = _catalog;
		StellarResult result = await Task.Run(() => swapper.Apply(list, catalog));
		SetBusy(false);
		if (result.Ok)
		{
			_active = list.Where(s => !string.Equals(s.TargetId, s.SourceId, StringComparison.OrdinalIgnoreCase)).ToList();
			SetStatus(result.Message);
			if (_swapsPage)
			{
				RefreshSwaps();
			}
			if (okText.Length > 0)
			{
				Dlg.Show(this, okText, "Stellar");
			}
			return true;
		}
		SetStatus("Didn't work.");
		Dlg.Show(this, result.Message, "Stellar");
		return false;
	}

	private async void SwapUndo_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: SwapVm vm })
		{
			List<StellarSwap> next = _active.Where(s => s != vm.Swap).ToList();
			await ApplyAsync(next, "");
			RefreshSwaps();
		}
	}

	private async void UndoAll_Click(object sender, RoutedEventArgs e)
	{
		if (_swapper == null || _busy)
		{
			return;
		}
		await ApplyAsync(new List<StellarSwap>(), "All swaps are undone.");
		RefreshSwaps();
	}

	private async void Folder_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
		{
			Title = "Pick your Project Stellar Fortnite 12.41 folder"
		};
		if (dialog.ShowDialog(this) != true)
		{
			return;
		}
		string? paks = StellarSwapper.FindPaks(dialog.FolderName);
		if (paks == null)
		{
			Dlg.Show(this, "That folder doesn't have Fortnite's pak files. Pick the folder that contains FortniteGame, for example C:\\Project Stellar\\Fortnite Build\\Fortnite 12.41.", "Stellar");
			return;
		}
		_settings.GamePath = dialog.FolderName;
		_settings.Save();
		await LoadAsync();
	}

	private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void Minimize_Click(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void Theme_Click(object sender, RoutedEventArgs e)
	{
		Theme.Toggle();
	}

	private void SwitchGame_Click(object sender, RoutedEventArgs e)
	{
		App.OpenChooser(this);
	}
}
