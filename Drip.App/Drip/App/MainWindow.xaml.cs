using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drip.Core;
using Drip.Core.Cosmetics;
using Drip.Core.FallGuys;
using Drip.Core.FortniteModels;
using Drip.Core.Fun;
using Drip.Core.Install;
using Drip.Core.Keys;
using Drip.Core.Mods;
using Drip.Core.OnDemand;
using Drip.Core.Retrac;
using Drip.Core.RocketLeague.Custom;
using Drip.Core.Swapping;
using Microsoft.Win32;

namespace Drip.App;

public partial class MainWindow : Window, IComponentConnector, IStyleConnector
{
	private sealed record DripCheck(List<StyleRename> Renames, List<string> Problems, List<string> Notes);

	private sealed class EmoteReadyFile
	{
		public string Stamp { get; set; } = "";

		public Dictionary<string, bool> Ready { get; set; } = new Dictionary<string, bool>();
	}

	private sealed class BodyBuildException(FnRigInfo rig, Exception inner) : Exception(inner.Message, inner)
	{
		public FnRigInfo Rig { get; } = rig;
	}

	private enum Ready
	{
		Ready,
		NotReady,
		Unknown
	}

	private sealed record ReadyInfo(Ready State, string Detail, List<string> Missing);

	private enum Page
	{
		Home,
		Outfits,
		Swapped,
		Mods,
		Fun,
		Settings
	}

	private enum Cat
	{
		Outfit,
		Pickaxe,
		Backpack,
		Emote,
		Glider,
		Contrail,
		Music,
		Loading,
		Toy,
		Spray,
		Wrap,
		Emoticon
	}

	private enum StyleMode
	{
		Owned,
		Target,
		ChangeOwned,
		ChangeTarget
	}

	private const string MkOwnPicture = "My own picture…";

	private bool _mkBuilds;

	private byte[]? _mkBPicBytes;

	private TexImage? _mkBPic;

	private string _mkBPicName = "";

	private string _mkPiece = "wall";

	private readonly Dictionary<string, BitmapSource> _mkLookCache = new Dictionary<string, BitmapSource>();

	private bool _mkBInit;

	private Task? _emoteScan;

	private volatile bool _emoteScanStop;

	private FnItemKind? _mkItem;

	private static readonly Regex ReportPath = new Regex("\"(/[A-Za-z0-9_]+(?:/[A-Za-z0-9_.\\-]+)+)\"", RegexOptions.Compiled);

	private static readonly Regex AnimLeaf = new Regex("montage|anim|^eas_|^am_|sequence|_cm[mf]$|_cmm_|dance", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private const int MkPx = 640;

	private FgModelSource? _mkSrc;

	private string _mkPath = "";

	private FgUpAxis _mkAutoUp;

	private float _mkAutoYaw;

	private TexImage? _mkPic;

	private FnTarget? _mkTarget;

	private FnBuild? _mkFitBuild;

	private float _mkView;

	private int _mkGen;

	private int _mkRenderGen;

	private bool _mkMute;

	private bool _mkNameAuto = true;

	private DispatcherTimer? _mkTimer;

	private readonly ConcurrentDictionary<string, byte[]> _mkReads = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

	private static readonly string[] MkModelExt = new string[10] { ".fbx", ".obj", ".glb", ".gltf", ".dae", ".3ds", ".stl", ".ply", ".blend", ".zip" };

	private Point _mkDrag;

	private bool _mkDragging;

	private DateTime _mkLastDragRender = DateTime.MinValue;

	private readonly Dictionary<string, string?> _carrierProblems = new Dictionary<string, string>();

	private readonly ModLibrary _mods = new ModLibrary();

	private readonly PakLibrary _pakLib = new PakLibrary();

	private ModPack? _pendingMod;

	private readonly AppSettings _settings = AppSettings.Load();

	private DispatcherTimer? _watch;

	private bool _gameWasRunning;

	private bool _watchBusy;

	private int _modCount;

	private int _pakCount;

	private readonly Dictionary<string, List<CharacterPart>> _partCache = new Dictionary<string, List<CharacterPart>>(StringComparer.OrdinalIgnoreCase);

	private bool _partCacheLoaded;

	private OnDemandIndex? _onDemand;

	private string _onDemandStamp = "";

	private Task? _readyTask;

	private readonly Dictionary<string, int> _matStyleCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, (List<StyleRename> Renames, string Note)> _styleCache = new Dictionary<string, (List<StyleRename>, string)>(StringComparer.OrdinalIgnoreCase);

	private string _streamNote = "";

	private FortniteLoadout? _loadout;

	private DateTime _loadoutRead = DateTime.MinValue;

	private readonly bool _retrac;

	private RetracSwapper? _rt;

	private int _rtContainers;

	private RetracGuard? _guard;

	private const int MaxShown = 300;

	private readonly StateStore _state = new StateStore();

	private readonly SwappedStore _swapped;

	private readonly Dictionary<Cat, List<CosmeticVm>> _lists = new Dictionary<Cat, List<CosmeticVm>>
	{
		[Cat.Outfit] = new List<CosmeticVm>(),
		[Cat.Pickaxe] = new List<CosmeticVm>(),
		[Cat.Backpack] = new List<CosmeticVm>(),
		[Cat.Emote] = new List<CosmeticVm>(),
		[Cat.Glider] = new List<CosmeticVm>(),
		[Cat.Contrail] = new List<CosmeticVm>(),
		[Cat.Music] = new List<CosmeticVm>(),
		[Cat.Loading] = new List<CosmeticVm>(),
		[Cat.Toy] = new List<CosmeticVm>(),
		[Cat.Spray] = new List<CosmeticVm>(),
		[Cat.Wrap] = new List<CosmeticVm>(),
		[Cat.Emoticon] = new List<CosmeticVm>()
	};

	private Cat _cat;

	private CosmeticVm? _owned;

	private CosmeticVm? _wanted;

	private AesKeys? _keys;

	private SwapEngine? _engine;

	private PartResolver? _resolver;

	private string? _paks;

	private bool _busy;

	private bool _warned;

	private Page _page;

	private StyleVm? _style;

	private StyleVm? _ownedStyle;

	private StyleMode _styleMode;

	private HashSet<string>? _local;

	private const string DefaultSkinId = "DripDefaultSkin";

	private CosmeticVm? _defaultSkin;

	private List<Cosmetic> _defaultVariants = new List<Cosmetic>();

	private Dictionary<SwappedItem, List<SwappedItem>> _groupMembers = new Dictionary<SwappedItem, List<SwappedItem>>(ReferenceEqualityComparer.Instance);

	private const string DefaultOutfitId = "CID_DefaultOutfit";

	private static bool IsDefaultOutfit(Cosmetic c) => string.Equals(c.Id, DefaultOutfitId, StringComparison.OrdinalIgnoreCase);

	private static bool IsRecruit(Cosmetic c)
	{
		return string.Equals(c.Name, "Recruit", StringComparison.OrdinalIgnoreCase);
	}

	// The default skins an account can be given (Chapter 1 and 2 Recruits). Creative mannequins and the henchman NPC are not accounts' defaults.
	private static bool IsAccountDefault(Cosmetic c)
	{
		return IsRecruit(c) && System.Text.RegularExpressions.Regex.IsMatch(c.Id, "^CID_\\d{3}_Athena_Commando_[FM]_(Rebirth)?Default", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	}

	private static readonly object LogFileLock = new object();

	private static readonly string[] RarityOrder = new string[7] { "Mythic", "Exotic", "Legendary", "Epic", "Rare", "Uncommon", "Common" };

	private bool _rarityBusy;

	private readonly SemaphoreSlim _resolverLock = new SemaphoreSlim(1, 1);

	private string _lastStyleNote = "";

	private bool MkBOwn => MkBLook.SelectedItem as string == "My own picture…";

	private bool Connected
	{
		get
		{
			if (_engine == null)
			{
				return _rt != null;
			}
			return true;
		}
	}

	private List<CosmeticVm> _all => _lists[_cat];

	private void MkKind_Click(object sender, RoutedEventArgs e)
	{
		MainWindow mainWindow = this;
		string kind;
		if (MkKindBuilds.IsChecked == true)
		{
			kind = "builds";
		}
		else if (MkKindPickaxe.IsChecked == true)
		{
			kind = "pickaxe";
		}
		else
		{
			kind = ((MkKindBackBling.IsChecked == true) ? "backbling" : "model");
		}
		mainWindow.MkSetKind(kind);
	}

	private void MkApplyKind(bool builds)
	{
		MkSetKind(builds ? "builds" : "model");
	}

	private void MkSetKind(string kind)
	{
		bool flag = kind == "builds";
		FnItemKind? fnItemKind = FnItem.Find(kind);
		bool flag2 = fnItemKind != _mkItem;
		_mkBuilds = flag;
		_mkItem = fnItemKind;
		MkKindBuilds.IsChecked = flag;
		MkKindModel.IsChecked = !flag && !fnItemKind.HasValue;
		MkKindPickaxe.IsChecked = fnItemKind == FnItemKind.Pickaxe;
		MkKindBackBling.IsChecked = fnItemKind == FnItemKind.BackBling;
		Visibility visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		Visibility visibility2 = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		MkModelTop.Visibility = visibility;
		MkModelBottom.Visibility = visibility;
		MkModelRight.Visibility = visibility;
		MkBuildsTop.Visibility = visibility2;
		MkBuildsNote.Visibility = visibility2;
		MkBuildsRight.Visibility = visibility2;
		MkNameLabel.Text = (flag ? "2 · NAME" : "3 · NAME");
		MkItemTexts();
		if (flag)
		{
			if (!_mkBInit)
			{
				_mkBInit = true;
				foreach (BuildLook item in BuildLooks.All)
				{
					MkBLook.Items.Add(item.Name);
				}
				MkBLook.Items.Add("My own picture…");
				MkBLook.SelectedIndex = 0;
			}
			if (_mkNameAuto || MkNameBox.Text.Trim().Length == 0)
			{
				MkBSetAutoName();
			}
			MkStatusSet("Pick a look, name it, press Create drip.", bad: false);
			MkBRender();
		}
		else if (flag2 && _mkSrc != null)
		{
			_mkAutoUp = MkGuessUp(_mkSrc);
			_mkAutoYaw = MkGuessYaw(_mkSrc, _mkAutoUp, mirror: false);
			_mkMute = true;
			MkReset();
			_mkMute = false;
			MkStatusSet("", bad: false);
			MkRebuildAsync();
		}
		else
		{
			MkStatusSet((_mkSrc == null) ? "Choose a model to start." : "", bad: false);
		}
		MkUpdateButtons();
	}

	private void MkBSetAutoName()
	{
		string text;
		if (MkBLook.SelectedItem as string == "My own picture…")
		{
			text = ((_mkBPicName.Length > 0) ? (Path.GetFileNameWithoutExtension(_mkBPicName) + " Builds") : "My Builds");
		}
		else
		{
			text = BuildLooks.All[Math.Clamp(MkBLook.SelectedIndex, 0, BuildLooks.All.Count - 1)].Name;
		}
		_mkMute = true;
		MkNameBox.Text = text;
		_mkMute = false;
		_mkNameAuto = true;
	}

	private void MkBLook_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_mkBInit && MkBLook.SelectedIndex >= 0)
		{
			MkBPickRow.Visibility = ((!MkBOwn) ? Visibility.Collapsed : Visibility.Visible);
			TextBlock mkBLookNote = MkBLookNote;
			string text;
			if (MkBOwn)
			{
				text = ((_mkBPic == null) ? "Choose a picture. It gets repeated over the build blueprint." : $"{_mkBPicName} ({_mkBPic.Width}×{_mkBPic.Height})");
			}
			else
			{
				text = BuildLooks.All[Math.Clamp(MkBLook.SelectedIndex, 0, BuildLooks.All.Count - 1)].Blurb;
			}
			mkBLookNote.Text = text;
			if (_mkNameAuto || MkNameBox.Text.Trim().Length == 0)
			{
				MkBSetAutoName();
			}
			MkBRender();
			MkUpdateButtons();
		}
	}

	private void MkBChoosePicture_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick the picture for your builds",
			Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All files|*.*"
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			byte[] array = File.ReadAllBytes(openFileDialog.FileName);
			TexImage texImage = TexImage.Load(array);
			if (texImage.Width < 4 || texImage.Height < 4)
			{
				throw new InvalidDataException("That picture is too small.");
			}
			_mkBPicBytes = array;
			_mkBPic = texImage;
			_mkBPicName = Path.GetFileName(openFileDialog.FileName);
			MkBPicBox.Text = _mkBPicName;
			MkBLookNote.Text = $"{_mkBPicName} ({texImage.Width}×{texImage.Height})";
			_mkLookCache.Remove("picture");
			if (_mkNameAuto || MkNameBox.Text.Trim().Length == 0)
			{
				MkBSetAutoName();
			}
			MkBRender();
			MkUpdateButtons();
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't read that picture", ex.Message);
		}
	}

	private void MkPiece_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: string tag })
		{
			_mkPiece = tag;
		}
		MkBRender();
	}

	private BitmapSource? MkBLookBitmap()
	{
		TexImage texImage;
		string key;
		if (MkBOwn)
		{
			texImage = _mkBPic;
			key = "picture";
		}
		else
		{
			BuildLook buildLook = BuildLooks.All[Math.Clamp(MkBLook.SelectedIndex, 0, BuildLooks.All.Count - 1)];
			key = buildLook.Id;
			if (_mkLookCache.TryGetValue(key, out BitmapSource value))
			{
				return value;
			}
			texImage = buildLook.Make(256);
		}
		if (texImage == null)
		{
			return null;
		}
		if (_mkLookCache.TryGetValue(key, out BitmapSource value2))
		{
			return value2;
		}
		byte[] array = new byte[texImage.Width * texImage.Height * 4];
		for (int i = 0; i < array.Length; i += 4)
		{
			array[i] = texImage.Rgba[i + 2];
			array[i + 1] = texImage.Rgba[i + 1];
			array[i + 2] = texImage.Rgba[i];
			array[i + 3] = byte.MaxValue;
		}
		BitmapSource bitmapSource = BitmapSource.Create(texImage.Width, texImage.Height, 96.0, 96.0, PixelFormats.Bgra32, null, array, texImage.Width * 4);
		bitmapSource.Freeze();
		return _mkLookCache[key] = bitmapSource;
	}

	private static (Point[] Shape, (Point A, Point B)[] Grid) MkPieceShape(string piece)
	{
		switch (piece)
		{
		case "floor":
		{
			Point point5 = new Point(360.0, 80.0);
			Point point6 = new Point(630.0, 215.0);
			Point point7 = new Point(360.0, 350.0);
			Point point8 = new Point(90.0, 215.0);
			List<(Point, Point)> list4 = new List<(Point, Point)>();
			for (int k = 1; k < 3; k++)
			{
				double t2 = (double)k / 3.0;
				list4.Add((Lerp(point5, point6, t2), Lerp(point8, point7, t2)));
				list4.Add((Lerp(point5, point8, t2), Lerp(point6, point7, t2)));
			}
			return (Shape: new Point[4] { point5, point6, point7, point8 }, Grid: list4.ToArray());
		}
		case "stairs":
		{
			List<Point> list2 = new List<Point>
			{
				new Point(110.0, 380.0)
			};
			List<(Point, Point)> list3 = new List<(Point, Point)>();
			double num = 110.0;
			double num2 = 380.0;
			list2.Add(new Point(110.0, num2 -= 60.0));
			for (int j = 0; j < 5; j++)
			{
				list2.Add(new Point(num += 100.0, num2));
				if (j < 4)
				{
					list2.Add(new Point(num, num2 -= 60.0));
					list3.Add((new Point(num, num2 + 60.0), new Point(num, 380.0)));
				}
			}
			list2.Add(new Point(610.0, 380.0));
			return (Shape: list2.ToArray(), Grid: list3.ToArray());
		}
		case "roof":
		{
			Point point9 = new Point(360.0, 70.0);
			Point point10 = new Point(630.0, 350.0);
			Point point11 = new Point(90.0, 350.0);
			List<(Point, Point)> list5 = new List<(Point, Point)>();
			for (int l = 1; l < 3; l++)
			{
				double t3 = (double)l / 3.0;
				list5.Add((Lerp(point9, point11, t3), Lerp(point9, point10, t3)));
			}
			return (Shape: new Point[3] { point9, point10, point11 }, Grid: list5.ToArray());
		}
		default:
		{
			Point point = new Point(110.0, 70.0);
			Point point2 = new Point(610.0, 70.0);
			Point point3 = new Point(610.0, 380.0);
			Point point4 = new Point(110.0, 380.0);
			List<(Point, Point)> list = new List<(Point, Point)>();
			for (int i = 1; i < 3; i++)
			{
				double t = (double)i / 3.0;
				list.Add((Lerp(point, point2, t), Lerp(point4, point3, t)));
				list.Add((Lerp(point, point4, t), Lerp(point2, point3, t)));
			}
			return (Shape: new Point[4] { point, point2, point3, point4 }, Grid: list.ToArray());
		}
		}
		static Point Lerp(Point a, Point b, double num3)
		{
			return new Point(a.X + (b.X - a.X) * num3, a.Y + (b.Y - a.Y) * num3);
		}
	}

	private void MkBRender()
	{
		if (!_mkBuilds || !_mkBInit)
		{
			return;
		}
		BitmapSource bitmapSource = MkBLookBitmap();
		(Point[] Shape, (Point A, Point B)[] Grid) tuple = MkPieceShape(_mkPiece);
		Point[] item = tuple.Shape;
		(Point, Point)[] item2 = tuple.Grid;
		PathGeometry pathGeometry = new PathGeometry();
		PathFigure pathFigure = new PathFigure
		{
			StartPoint = item[0],
			IsClosed = true,
			IsFilled = true
		};
		foreach (Point item3 in item.Skip(1))
		{
			pathFigure.Segments.Add(new LineSegment(item3, isStroked: true));
		}
		pathGeometry.Figures.Add(pathFigure);
		pathGeometry.Freeze();
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(26, 26, 29)), null, new Rect(0.0, 0.0, 720.0, 440.0));
			Rect bounds = pathGeometry.Bounds;
			if (bitmapSource == null)
			{
				drawingContext.DrawGeometry(new SolidColorBrush(Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue)), null, pathGeometry);
				FormattedText formattedText = new FormattedText("Choose a picture", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 18.0, Brushes.Gray, 1.0);
				drawingContext.DrawText(formattedText, new Point(360.0 - formattedText.Width / 2.0, 220.0 - formattedText.Height / 2.0));
			}
			else
			{
				double num = Math.Max(60.0, bounds.Width / 2.5);
				ImageBrush brush = new ImageBrush(bitmapSource)
				{
					TileMode = TileMode.Tile,
					ViewportUnits = BrushMappingMode.Absolute,
					Viewport = new Rect(bounds.X, bounds.Y, num, num),
					Stretch = Stretch.Fill
				};
				drawingContext.PushClip(pathGeometry);
				drawingContext.DrawRectangle(brush, null, bounds);
				drawingContext.Pop();
			}
			if (MkBOutline.IsChecked == true)
			{
				SolidColorBrush brush2 = new SolidColorBrush(Color.FromRgb(61, 155, byte.MaxValue));
				Pen pen = new Pen(brush2, 1.6);
				pen.DashStyle = new DashStyle(new double[2] { 5.0, 4.0 }, 0.0);
				Pen pen2 = pen;
				(Point, Point)[] array = item2;
				for (int i = 0; i < array.Length; i++)
				{
					var (point, point2) = array[i];
					drawingContext.DrawLine(pen2, point, point2);
				}
				drawingContext.DrawGeometry(null, new Pen(brush2, 3.2)
				{
					LineJoin = PenLineJoin.Round
				}, pathGeometry);
			}
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(720, 440, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		MkBPreview.Source = renderTargetBitmap;
	}

	private void MkBWrite(string dest, string name)
	{
		string style = (MkBOwn ? "picture" : BuildLooks.All[Math.Clamp(MkBLook.SelectedIndex, 0, BuildLooks.All.Count - 1)].Id);
		byte[] picture = (MkBOwn ? _mkBPicBytes : null);
		BubbleBuilds.WriteFile(dest, name, "", picture, "", null, style);
		List<string> list = ModPack.Load(dest).Validate();
		if (list.Count > 0)
		{
			try
			{
				File.Delete(dest);
			}
			catch
			{
			}
			throw new InvalidDataException(string.Join(" ", list));
		}
	}

	private bool MkBReady()
	{
		if (MkNameBox.Text.Trim().Length > 0)
		{
			if (MkBOwn)
			{
				return _mkBPicBytes != null;
			}
			return true;
		}
		return false;
	}

	private async Task MkBCreateAsync(bool intoLibrary, string? chosenPath)
	{
		string name = MkNameBox.Text.Trim();
		string dest;
		if (intoLibrary)
		{
			Directory.CreateDirectory(_mods.Directory);
			string text = MkSafeName(name);
			dest = Path.Combine(_mods.Directory, text + ".dripFN");
			int num = 2;
			while (File.Exists(dest))
			{
				dest = Path.Combine(_mods.Directory, $"{text}-{num}.dripFN");
				num++;
			}
		}
		else
		{
			dest = chosenPath;
		}
		MkBusyShow("Saving…");
		try
		{
			await Task.Run(() =>
			{
				MkBWrite(dest, name);
			});
			if (intoLibrary)
			{
				MakeOverlay.Visibility = Visibility.Collapsed;
				RefreshModsList();
				ShowPage(Page.Mods);
				ModsMsg.Text = "Made '" + name + "'. Press Use… on it and pick wood, brick or metal (Fortnite closed). It shows while you build in a match or Creative, where online play can kick you.";
				ModsMsg.Foreground = (Brush)FindResource("Good");
			}
			else
			{
				MkStatusSet("Saved " + Path.GetFileName(dest) + ".", bad: false);
			}
		}
		catch (Exception ex)
		{
			MkStatusSet("Couldn't save the drip.", bad: true);
			ShowNotice("Couldn't save the drip", Friendly.Explain(ex.Message));
		}
		finally
		{
			MkBusyHide();
		}
	}

	private string? AskBuildMaterial()
	{
		BuildMaterialWindow buildMaterialWindow = new BuildMaterialWindow
		{
			Owner = this
		};
		if (buildMaterialWindow.ShowDialog() != true)
		{
			return null;
		}
		return buildMaterialWindow.Choice;
	}

	private static string DonorLabel(ModPack mod, ModSlotSwap ss)
	{
		int num = mod.Manifest.Name.LastIndexOf('×');
		if (num < 0 || num + 1 >= mod.Manifest.Name.Length)
		{
			return ss.From;
		}
		return mod.Manifest.Name.Substring(num + 1).Trim();
	}

	private DripCheck AnalyseSlotSwaps(PartResolver resolver, OnDemandIndex? idx, ModPack mod, string baseId, string baseName)
	{
		List<StyleRename> list = new List<StyleRename>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		List<CharacterPart> source = ResolveCached(resolver, baseId);
		foreach (ModSlotSwap ss in mod.Manifest.SlotSwaps)
		{
			string text = DonorLabel(mod, ss);
			try
			{
				List<CharacterPart> source2 = ResolveCached(resolver, ss.From);
				List<CharacterPart> list4 = source.Where((CharacterPart p) => p.Slot.Equals(ss.Slot, StringComparison.OrdinalIgnoreCase)).ToList();
				List<CharacterPart> list5 = source2.Where((CharacterPart p) => p.Slot.Equals(ss.Slot, StringComparison.OrdinalIgnoreCase)).ToList();
				if (list4.Count != 1 || list5.Count != 1)
				{
					list2.Add($"{text}: slot '{ss.Slot}' doesn't line up. {baseName} has [{string.Join(", ", source.Select((CharacterPart p) => p.Slot))}], {text} has [{string.Join(", ", source2.Select((CharacterPart p) => p.Slot))}]. I need exactly one part in that slot on each side.");
					continue;
				}
				string value = PartPair.LeafOf(list5[0].Package);
				string text2 = BodyType.Mismatch(list4[0].Package, list5[0].Package);
				if (text2 != null)
				{
					list2.Add($"{text}'s {ss.Slot.ToLowerInvariant()}: {text2}");
					continue;
				}
				if (list5[0].ChunkId == 0L)
				{
					if (idx == null || !idx.Usable)
					{
						list3.Add($"Couldn't read Fortnite's download index, so I can't tell whether {text}'s {ss.Slot.ToLowerInvariant()} is downloaded.");
					}
					else
					{
						switch (idx.CheckPackage(PackageNames.ChunkId(list5[0].Package)))
						{
						case ChunkState.NotInstalled:
							list2.Add($"{text}'s {ss.Slot.ToLowerInvariant()} ({value}) isn't downloaded on this PC, so it would show as nothing. Preview {text} in your Locker once so Fortnite downloads it, then try again.");
							continue;
						default:
							list3.Add($"Couldn't tell whether {text}'s {ss.Slot.ToLowerInvariant()} is downloaded.");
							break;
						case ChunkState.Installed:
							break;
						}
					}
				}
				list.Add(new StyleRename(list4[0].Package, list5[0].Package));
				list3.Add($"Slot '{ss.Slot}': {PartPair.LeafOf(list4[0].Package)} -> {value} (from {text}).");
			}
			catch (Exception ex)
			{
				list2.Add(text + ": couldn't read the skin (" + ex.Message + ").");
			}
		}
		return new DripCheck(list, list2, list3);
	}

	private DripCheck AnalysePartSwaps(PartResolver resolver, OnDemandIndex? idx, ModPack mod, string baseId, string baseName)
	{
		List<StyleRename> list = new List<StyleRename>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		HashSet<string> hashSet = (from p in ResolveCached(resolver, baseId)
			select p.Package).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (ModPartSwap partSwap in mod.Manifest.PartSwaps)
		{
			string text = PartPair.LeafOf(partSwap.From);
			string text2 = PartPair.LeafOf(partSwap.To);
			if (!hashSet.Contains(partSwap.From))
			{
				list2.Add(baseName + " doesn't use " + text + ", so swapping it would change nothing.");
				continue;
			}
			string text3 = BodyType.Mismatch(partSwap.From, partSwap.To);
			if (text3 != null)
			{
				list2.Add(text2 + ": " + text3);
				continue;
			}
			if (idx == null || !idx.Usable)
			{
				list3.Add("Couldn't read Fortnite's download index, so I can't tell whether " + text2 + " is downloaded.");
			}
			else
			{
				switch (idx.CheckPackage(PackageNames.ChunkId(partSwap.To)))
				{
				case ChunkState.NotInstalled:
					list2.Add(text2 + " isn't downloaded on this PC, so it would show as nothing. Preview the skin it belongs to in your Locker once so Fortnite downloads it, then try again.");
					continue;
				case ChunkState.Unknown:
					list3.Add("Couldn't tell whether " + text2 + " is downloaded.");
					break;
				}
			}
			list.Add(new StyleRename(partSwap.From, partSwap.To));
			list3.Add($"Part: {text} -> {text2}.");
		}
		return new DripCheck(list, list2, list3);
	}

	private DripCheck AnalyseDrip(PartResolver resolver, OnDemandIndex? idx, ModPack mod, string baseId, string baseName)
	{
		DripCheck dripCheck = ((mod.Manifest.SlotSwaps.Count > 0) ? AnalyseSlotSwaps(resolver, idx, mod, baseId, baseName) : new DripCheck(new List<StyleRename>(), new List<string>(), new List<string>()));
		DripCheck dripCheck2 = ((mod.Manifest.PartSwaps.Count > 0) ? AnalysePartSwaps(resolver, idx, mod, baseId, baseName) : new DripCheck(new List<StyleRename>(), new List<string>(), new List<string>()));
		return new DripCheck(dripCheck.Renames.Concat(dripCheck2.Renames).ToList(), dripCheck.Problems.Concat(dripCheck2.Problems).ToList(), dripCheck.Notes.Concat(dripCheck2.Notes).ToList());
	}

	private async void CheckDrips_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		if (_engine == null || _paks == null)
		{
			ModsMsg.Text = "Connect on the Home tab first.";
			ModsMsg.Foreground = (Brush)FindResource("Danger");
			return;
		}
		IEnumerable<ModVm> obj = ModsHost.ItemsSource as IEnumerable<ModVm>;
		List<ModVm> rows = ((obj != null) ? obj.Where((ModVm v) =>
		{
			ModPack mod = v.Mod;
			if (mod != null)
			{
				ModManifest manifest = mod.Manifest;
				if (manifest != null)
				{
					string text = manifest.Base;
					if (text != null && text.Length > 0)
					{
						if (manifest.SlotSwaps.Count <= 0)
						{
							return manifest.PartSwaps.Count > 0;
						}
						return true;
					}
				}
			}
			return false;
		}).ToList() : null) ?? new List<ModVm>();
		if (rows.Count == 0)
		{
			ModsMsg.Text = "No drips with part or slot swaps to check.";
			ModsMsg.Foreground = (Brush)FindResource("Text2");
			return;
		}
		SetBusy(busy: true);
		PartResolver resolver = null;
		try
		{
			ModsMsg.Text = $"Checking {rows.Count} drip(s)... the first time this reads each skin and can take a few minutes.";
			ModsMsg.Foreground = (Brush)FindResource("Text2");
			OnDemandIndex idx = await GetOnDemandAsync();
			resolver = await EnsureResolverAsync();
			resolver.Quiet = true;
			int ok = 0;
			int bad = 0;
			await Task.Run(() =>
			{
				int num = 0;
				foreach (ModVm vm in rows)
				{
					ModPack mod = vm.Mod;
					string baseName = _lists[Cat.Outfit].FirstOrDefault((CosmeticVm v) => v.Id.Equals(mod.Manifest.Base, StringComparison.OrdinalIgnoreCase))?.Name ?? (mod.Manifest.Name.Contains('×') ? mod.Manifest.Name.Substring(0, mod.Manifest.Name.LastIndexOf('×')).Trim() : mod.Manifest.Base);
					DripCheck dripCheck;
					try
					{
						dripCheck = AnalyseDrip(resolver, idx, mod, mod.Manifest.Base, baseName);
					}
					catch (Exception ex2)
					{
						dripCheck = new DripCheck(new List<StyleRename>(), new List<string> { ex2.Message }, new List<string>());
					}
					bool good = dripCheck.Problems.Count == 0;
					if (good)
					{
						ok++;
					}
					else
					{
						bad++;
					}
					string text = (good ? "Ready: the part is downloaded and fits the body." : dripCheck.Problems[0]);
					Dispatcher.Invoke(() =>
					{
						vm.SetCheck(good, text);
					});
					if (++num % 50 == 0)
					{
						SavePartCache();
					}
				}
			});
			ModsMsg.Text = $"Checked {rows.Count} drip(s): {ok} should work, {bad} can't right now (the reason is on each row). Skins you preview in your Locker become available after Fortnite downloads them; press Check all again.";
			ModsMsg.Foreground = (Brush)FindResource((ok > 0) ? "Good" : "Danger");
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ModsMsg.Text = ex.Message;
			ModsMsg.Foreground = (Brush)FindResource("Danger");
		}
		finally
		{
			if (resolver != null)
			{
				resolver.Quiet = false;
			}
			SavePartCache();
			SetBusy(busy: false);
		}
	}

	private string EmoteReadyPath()
	{
		return Path.Combine(AppPaths.DataDir, "emote-ready-" + new string((_keys?.Build ?? "x").Where(char.IsLetterOrDigit).ToArray()) + ".json");
	}

	private void StartEmoteScan()
	{
		if (_engine != null && _paks != null && _keys != null && !_busy)
		{
			Task emoteScan = _emoteScan;
			if (emoteScan == null || emoteScan.IsCompleted)
			{
				_emoteScanStop = false;
				_emoteScan = Task.Run((Func<Task?>)ScanEmotesAsync);
			}
		}
	}

	private async void RescanEmotes_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			Log("Connect on the Home tab first.");
		}
		else
		{
			if (_busy)
			{
				return;
			}
			await StopEmoteScanAsync();
			try
			{
				File.Delete(EmoteReadyPath());
			}
			catch
			{
			}
			foreach (CosmeticVm item in _lists[Cat.Emote])
			{
				item.AnimReady = null;
			}
			Log("Checking which emotes are downloaded (runs in the background)...");
			StartEmoteScan();
		}
	}

	private async Task StopEmoteScanAsync()
	{
		_emoteScanStop = true;
		if (_emoteScan != null)
		{
			try
			{
				await _emoteScan;
			}
			catch
			{
			}
		}
	}

	private async Task ScanEmotesAsync()
	{
		_ = 5;
		try
		{
			List<CosmeticVm> list = await Dispatcher.InvokeAsync(() => _lists[Cat.Emote].Where((CosmeticVm v) => v.Available).ToList());
			if (list.Count == 0)
			{
				return;
			}
			string stamp = CasStamp();
			string file = EmoteReadyPath();
			EmoteReadyFile cached = null;
			try
			{
				if (File.Exists(file))
				{
					cached = JsonSerializer.Deserialize<EmoteReadyFile>(File.ReadAllText(file));
				}
			}
			catch
			{
			}
			if (cached != null && cached.Stamp == stamp && list.All((CosmeticVm v) => cached.Ready.ContainsKey(v.Id)))
			{
				await Dispatcher.InvokeAsync(() =>
				{
					foreach (CosmeticVm item in list)
					{
						item.AnimReady = cached.Ready[item.Id];
					}
				});
				return;
			}
			OnDemandIndex idx = await GetOnDemandAsync();
			if (idx == null || !idx.Usable)
			{
				return;
			}
			PartResolver resolver = await EnsureResolverAsync();
			Dictionary<string, bool> ready = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
			resolver.Quiet = true;
			try
			{
				int n = 0;
				foreach (CosmeticVm item2 in list)
				{
					if (_emoteScanStop || _busy || resolver != _resolver || !resolver.IsMounted)
					{
						return;
					}
					bool value;
					try
					{
						value = !MissingDownloads(resolver, idx, item2.Id).Any((string m) => AnimLeaf.IsMatch(m));
					}
					catch
					{
						continue;
					}
					ready[item2.Id] = value;
					int num = n + 1;
					n = num;
					if (num % 60 != 0)
					{
						continue;
					}
					List<KeyValuePair<string, bool>> snap = ready.ToList();
					await Dispatcher.InvokeAsync(() =>
					{
						foreach (KeyValuePair<string, bool> kv in snap)
						{
							list.First((CosmeticVm x) => x.Id == kv.Key).AnimReady = kv.Value;
						}
					});
				}
			}
			finally
			{
				resolver.Quiet = false;
			}
			List<KeyValuePair<string, bool>> final = ready.ToList();
			await Dispatcher.InvokeAsync(() =>
			{
				Dictionary<string, CosmeticVm> dictionary = list.ToDictionary((CosmeticVm v) => v.Id, StringComparer.OrdinalIgnoreCase);
				foreach (KeyValuePair<string, bool> item3 in final)
				{
					if (dictionary.TryGetValue(item3.Key, out var value2))
					{
						value2.AnimReady = item3.Value;
					}
				}
				int value3 = Enumerable.Count(final, (KeyValuePair<string, bool> kv) => !kv.Value);
				Log($"Checked {final.Count} emotes: {value3} aren't downloaded yet and are dimmed (they would do nothing in game).");
			});
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(file));
				File.WriteAllText(file, JsonSerializer.Serialize(new EmoteReadyFile
				{
					Stamp = stamp,
					Ready = ready
				}));
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static Cat CatFromKind(string kind)
	{
		return kind switch
		{
			"Pickaxe" => Cat.Pickaxe, 
			"Back Bling" => Cat.Backpack, 
			"Emote" => Cat.Emote, 
			"Glider" => Cat.Glider, 
			"Contrail" => Cat.Contrail, 
			"Lobby Music" => Cat.Music, 
			"Loading Screen" => Cat.Loading, 
			"Toy" => Cat.Toy, 
			"Spray" => Cat.Spray, 
			"Wrap" => Cat.Wrap, 
			"Emoticon" => Cat.Emoticon, 
			_ => Cat.Outfit, 
		};
	}

	private IReadOnlyList<FitEntry> CurrentFit()
	{
		return (from i in _swapped.Items
			where i.ModKey.Length == 0 && i.Kind != "Mod" && i.SourceId.Length > 0 && !i.SourceId.StartsWith("mod:", StringComparison.OrdinalIgnoreCase) && i.TargetId.Length > 0 && _engine != null && _engine.IsActive(i.PatchedPackages)
			select new FitEntry
			{
				Kind = (string.IsNullOrEmpty(i.Kind) ? "Outfit" : i.Kind),
				Source = i.SourceId,
				Target = i.TargetId,
				Style = i.TargetStyle
			}).ToList();
	}

	private async void RandomLocker_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			ShowNotice("Connect first", "Connect on the Home tab first.");
			return;
		}
		FortniteLoadout fortniteLoadout = CurrentLoadout();
		if (fortniteLoadout == null)
		{
			ShowNotice("Open Fortnite once first", "The app reads what you have equipped from Fortnite's own log. Start Fortnite, equip your items in the Locker, close it, then try again.");
			return;
		}
		(Cat, string)[] array = new (Cat, string)[6]
		{
			(Cat.Outfit, "Character"),
			(Cat.Pickaxe, "Pickaxe"),
			(Cat.Backpack, "Backpack"),
			(Cat.Glider, "Glider"),
			(Cat.Emote, "Dance"),
			(Cat.Contrail, "Contrail")
		};
		List<FitEntry> list = new List<FitEntry>();
		(Cat, string)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(Cat, string) tuple = array2[i];
			Cat cat = tuple.Item1;
			string item = tuple.Item2;
			string eq = fortniteLoadout.Get(item);
			if (eq == null)
			{
				continue;
			}
			CosmeticVm cosmeticVm = _lists[cat].FirstOrDefault((CosmeticVm v) => v.Id.Equals(eq, StringComparison.OrdinalIgnoreCase));
			if (cosmeticVm != null && cosmeticVm.Available)
			{
				CosmeticVm cosmeticVm2 = Surprise.Pick(_lists[cat], (CosmeticVm c) => c.Available && !c.Dead && (cat != Cat.Outfit || c.Downloaded != false), (CosmeticVm c) => c.Id, cosmeticVm.Id);
				if (cosmeticVm2 != null)
				{
					list.Add(new FitEntry
					{
						Kind = KindName(cat),
						Source = cosmeticVm.Id,
						Target = cosmeticVm2.Id
					});
				}
			}
		}
		if (list.Count == 0)
		{
			ShowNotice("Nothing equipped found", "The log doesn't show anything equipped that this list knows. Equip something in your Locker, close Fortnite and try again.");
			return;
		}
		ShowNotice("Random locker", await ApplyFitAsync(new Fit
		{
			Game = "fortnite",
			Name = "Random locker",
			Entries = list
		}) + "\n\nOpen the Locker in your lobby to see it. Press Undo everything on Home to put your own items back.");
	}

	private async Task<string> ApplyFitAsync(Fit fit)
	{
		if (_engine == null)
		{
			return "Connect on the Home tab first.";
		}
		if (_busy)
		{
			return "Wait for the current job to finish.";
		}
		if (await Task.Run((Func<bool>)FortniteLocator.IsFortniteRunning))
		{
			return "Close Fortnite first, then apply the loadout.";
		}
		if (_lists[Cat.Outfit].Count == 0)
		{
			return "The item lists haven't loaded yet. Connect on the Home tab.";
		}
		if (!_warned)
		{
			if (!ConfirmEditFiles())
			{
				return "Cancelled.";
			}
			_warned = true;
		}
		SetBusy(busy: true);
		int ok = 0;
		List<string> problems = new List<string>();
		try
		{
			foreach (FitEntry en in fit.Entries)
			{
				Cat cat = CatFromKind(en.Kind);
				CosmeticVm src = _lists[cat].FirstOrDefault((CosmeticVm v) => v.Id.Equals(en.Source, StringComparison.OrdinalIgnoreCase));
				CosmeticVm tgt = _lists[cat].FirstOrDefault((CosmeticVm v) => v.Id.Equals(en.Target, StringComparison.OrdinalIgnoreCase));
				if (src == null || tgt == null)
				{
					problems.Add(en.Source + " → " + en.Target + ": not in the item list");
					continue;
				}
				if (!src.Available || !tgt.Available)
				{
					problems.Add(src.Name + " → " + tgt.Name + ": not in your local game files");
					continue;
				}
				try
				{
					CosmeticStyle cosmeticStyle = ((en.Style.Length <= 0) ? null : tgt.Item.Styles?.FirstOrDefault((CosmeticStyle s) => s.Name.Equals(en.Style, StringComparison.OrdinalIgnoreCase)));
					await ConvertAsync(src.Item, tgt.Item, cat, cosmeticStyle?.Name ?? "", cosmeticStyle?.Tag ?? "");
					ok++;
					Log($"Loadout \"{fit.Name}\": {src.Name} → {tgt.Name}");
				}
				catch (Exception ex)
				{
					problems.Add($"{src.Name} → {tgt.Name}: {ex.Message}");
					Log("FAIL " + ex.Message);
				}
			}
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
		}
		string text = $"Applied {ok} of {fit.Entries.Count} swap{((fit.Entries.Count == 1) ? "" : "s")} from \"{fit.Name}\".";
		if (problems.Count > 0)
		{
			text = text + " Skipped: " + string.Join("; ", problems.Take(3)) + ((problems.Count > 3) ? $" (+{problems.Count - 3} more)" : "");
		}
		return text;
	}

	private FgUpAxis MkGuessUp(FgModelSource src)
	{
		FnItemKind? mkItem = _mkItem;
		if (mkItem.HasValue)
		{
			FnItemKind valueOrDefault = mkItem.GetValueOrDefault();
			return FnItem.GuessUp(src, valueOrDefault);
		}
		return FgModelFit.GuessUp(src);
	}

	private float MkGuessYaw(FgModelSource src, FgUpAxis up, bool mirror)
	{
		FnItemKind? mkItem = _mkItem;
		if (!mkItem.HasValue)
		{
			return FnAlign.GuessYaw(src, up, mirror);
		}
		return 0f;
	}

	private void MkItemTexts()
	{
		FnItemKind? mkItem = _mkItem;
		MkCharNote.Visibility = (mkItem.HasValue ? Visibility.Collapsed : Visibility.Visible);
		MkItemNote.Visibility = ((!mkItem.HasValue) ? Visibility.Collapsed : Visibility.Visible);
		MkShowFit.Visibility = (mkItem.HasValue ? Visibility.Collapsed : Visibility.Visible);
		if (mkItem.HasValue && MkShowFit.IsChecked == true)
		{
			MkShowModel.IsChecked = true;
		}
		TextBlock mkMakeSub = MkMakeSub;
		mkMakeSub.Text = mkItem switch
		{
			FnItemKind.Pickaxe => "Turn a 3D model into a pickaxe drip. Line it up with the blue pickaxe outline.", 
			FnItemKind.BackBling => "Turn a 3D model into a back bling drip. Line it up with the blue bag on the character's back.", 
			_ => "Turn a 3D model into a drip. The whole model replaces your Rebirth Soldier's body in the lobby, not just a coat of paint.", 
		};
		MkItemNoteText.Text = ((!mkItem.HasValue) ? "" : ("Use… on the drip later, pick a " + FnItem.Label(mkItem.Value) + " you own, and the game's own " + FnItem.Label(mkItem.Value) + " becomes your model (it keeps its name in the Locker). The model is lined up with the real " + FnItem.Label(mkItem.Value) + " automatically, so the blue outline just needs to be close."));
		CheckBox mkOutlineBox = MkOutlineBox;
		mkOutlineBox.Content = mkItem switch
		{
			FnItemKind.Pickaxe => (object)"Show the pickaxe's blue outline", 
			FnItemKind.BackBling => "Show the back bling's blue outline", 
			_ => "Show the character's blue outline", 
		};
		mkOutlineBox = MkOutlineBox;
		mkOutlineBox.ToolTip = mkItem switch
		{
			FnItemKind.Pickaxe => (object)"The blue line is a plain pickaxe: a straight handle on the ground with a curved head. The dim line is the character, so you can see how big it is next to a person.", 
			FnItemKind.BackBling => "The blue line is a plain bag on the character's back. The dim line is the character. Line your back bling up with the bag.", 
			_ => "The blue line is the character in a T-pose. Line your model up with it: same size, feet on the ground, arms along the line, facing the front.", 
		};
		mkMakeSub = MkEmpty;
		mkMakeSub.Text = mkItem switch
		{
			FnItemKind.Pickaxe => "Step 1: choose a 3D model of a pickaxe (.fbx, .glb, .obj or a zip)\nStep 2: line it up with the blue pickaxe\nStep 3: press Create drip", 
			FnItemKind.BackBling => "Step 1: choose a 3D model of a back bling (.fbx, .glb, .obj or a zip)\nStep 2: line it up with the blue bag\nStep 3: press Create drip", 
			_ => "Step 1: choose a 3D model (.fbx, .glb, .obj or a zip)\nStep 2: line it up with the blue character\nStep 3: press Create drip", 
		};
		if (_mkSrc == null)
		{
			MkInfo.Text = MkLineUpHelp();
		}
	}

	private string MkLineUpHelp()
	{
		return _mkItem switch
		{
			FnItemKind.Pickaxe => "The blue line is a plain pickaxe (handle on the ground, curved head on top). The dim line is the character. Line your pickaxe up with the blue one. Drag the picture to turn the view.", 
			FnItemKind.BackBling => "The blue line is a plain bag on the character's back (the dim line). Line your back bling up with it. Drag the picture to turn the view.", 
			_ => "The blue line is the character (T-pose). Line your model up with it. Drag the picture to turn the view.", 
		};
	}

	private static string MkItemTips(FgModelSource src, FnTarget tgt, FnItemKind kind)
	{
		List<string> list = new List<string>();
		if (tgt.Tri.Length / 3 > 60000)
		{
			list.Add($"It has {tgt.Tri.Length / 3:N0} triangles, which is heavy for a {FnItem.Label(kind)}. Under 20,000 is plenty.");
		}
		if (FnItem.Overlap(tgt, kind) < 0.6)
		{
			list.Add("Part of it is far from the blue outline. Try Size, Turn or Which way is up.");
		}
		if (!src.HasUv && src.Materials.All((FgSourceMaterial m) => m.Picture == null))
		{
			list.Add("It has no picture and no picture coordinates, so it will be one flat colour unless you add a picture.");
		}
		if (list.Count != 0)
		{
			return " " + string.Join(" ", list);
		}
		return "";
	}

	private async Task MkWriteItemAsync(string dest, string name, bool intoLibrary)
	{
		FnTarget tgt = _mkTarget;
		FnItemKind kind = _mkItem.Value;
		float yaw = _mkView;
		MkBusyShow("Saving…");
		try
		{
			await Task.Run(() =>
			{
				FnModelFile.WriteItem(dest, name, "", "Custom " + FnItem.Label(kind) + " made in Drip Swapper.", tgt, FnItem.Id(kind), MkIconPng(tgt, yaw));
				List<string> list = ModPack.Load(dest).Validate();
				if (list.Count > 0)
				{
					try
					{
						File.Delete(dest);
					}
					catch
					{
					}
					throw new InvalidDataException(string.Join(" ", list));
				}
			});
			if (intoLibrary)
			{
				MakeOverlay.Visibility = Visibility.Collapsed;
				RefreshModsList();
				ShowPage(Page.Mods);
				ModsMsg.Text = $"Made '{name}' (a {FnItem.Label(kind)}). Hit Use… on it, pick the {FnItem.Label(kind)} you own, and it will look like your model in the lobby.";
				ModsMsg.Foreground = (Brush)FindResource("Good");
			}
			else
			{
				MkStatusSet("Saved " + Path.GetFileName(dest) + ".", bad: false);
			}
		}
		catch (Exception ex)
		{
			MkStatusSet("Couldn't save the drip.", bad: true);
			ShowNotice("Couldn't save the drip", Friendly.Explain(ex.Message));
		}
		finally
		{
			MkBusyHide();
		}
	}

	private void StartItemPick(ModPack mod, FnItemKind kind)
	{
		ShowPage(Page.Outfits);
		_cat = ((kind == FnItemKind.Pickaxe) ? Cat.Pickaxe : Cat.Backpack);
		((kind == FnItemKind.Pickaxe) ? CatPickaxe : CatBackpack).IsChecked = true;
		_owned = (_wanted = null);
		SearchBox.Clear();
		RefreshRarityBox();
		ApplyFilter();
		_pendingMod = mod;
		OpenOwnedPicker();
	}

	private static bool IsItemMod(ModPack? mod, out FnItemKind kind)
	{
		kind = FnItemKind.Pickaxe;
		ModFnItem modFnItem = mod?.Manifest.FnItem;
		if (modFnItem != null)
		{
			FnItemKind? fnItemKind = FnItem.Find(modFnItem.Kind);
			if (fnItemKind.HasValue)
			{
				FnItemKind valueOrDefault = fnItemKind.GetValueOrDefault();
				kind = valueOrDefault;
				return true;
			}
		}
		return false;
	}

	private static Cat CatOf(FnItemKind k)
	{
		if (k != FnItemKind.Pickaxe)
		{
			return Cat.Backpack;
		}
		return Cat.Pickaxe;
	}

	private void ApplyItemModel(ModPack mod, SwappedItem? item, FnItemRig rig)
	{
		SwapEngine engine = _engine;
		Log($"Putting '{mod.Manifest.Name}' on {rig.Name} ({FnItem.Label(rig.Kind)}, this takes a moment)...");
		Func<string, byte, byte[]> read = (string pkg, byte type) => engine.ReadPackageChunk(pkg, type);
		engine.RevertPackages(rig.Packages);
		FnBuild fnBuild = FnItemReplace.Build(read, rig, FnItemFile.Load(mod), Log);
		foreach (string note in fnBuild.Notes)
		{
			Log(note);
		}
		List<string> list = new List<string>();
		try
		{
			foreach (FnPatch item2 in fnBuild.Patches.OrderBy((FnPatch p) => p.Package.Contains("/Textures/", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
			{
				SwapResult swapResult = engine.PatchPackageChunk(item2.Package, item2.Type, item2.Data, mod.Manifest.Name);
				list.Add(swapResult.Source);
				Log($"OK  {item2.What}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
			}
		}
		catch (Exception ex)
		{
			Log("FAIL item model: " + ex.Message);
			try
			{
				engine.RevertPackages(list);
			}
			catch (Exception ex2)
			{
				Log("rollback: " + ex2.Message);
			}
			throw;
		}
		List<string> list2 = engine.MissingPackages(list);
		if (list2.Count > 0)
		{
			try
			{
				engine.RevertPackages(list);
			}
			catch
			{
			}
			throw new InvalidOperationException("The model was written but the game files don't show it afterwards (" + string.Join(", ", list2.Select((string m) => m.Split('/')[^1])) + "). Close the Epic Launcher and try again.");
		}
		Log($"Model check: all {list.Count} files are in your game files.");
		if (item == null)
		{
			return;
		}
		foreach (string item3 in list)
		{
			if (!item.PatchedPackages.Contains(item3, StringComparer.OrdinalIgnoreCase))
			{
				item.PatchedPackages.Add(item3);
			}
		}
		item.Parts.RemoveAll((SwappedPart p) =>
		{
			switch (p.Slot)
			{
			case "ModItem":
			case "ModItemPlain":
			case "ModItemHide":
				return true;
			default:
				return false;
			}
		});
		item.Parts.AddRange(FnItemResolve.Remember(rig, mod.Key));
	}

	private void ReapplyItemModel(SwappedItem item)
	{
		ModPack mod = _mods.List().FirstOrDefault((ModPack m) => string.Equals(m.Key, item.ModKey, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("The drip '" + item.ModKey + "' is no longer in your Mods list.");
		FnItemRig rig = FnItemResolve.Recall(item) ?? throw new InvalidOperationException("That swap didn't remember which files it used. Undo it and swap again.");
		ApplyItemModel(mod, null, rig);
	}

	private async Task<SwappedItem> ConvertItemAsync(Cosmetic src, ModPack mod, Cat cat)
	{
		_lastStyleNote = "";
		FnItemKind kind = ((cat != Cat.Pickaxe) ? FnItemKind.BackBling : FnItemKind.Pickaxe);
		string label = FnItem.Label(kind);
		SwapEngine engine = _engine;
		Func<string, byte, byte[]> read = (string pkg, byte type) => engine.ReadPackageChunk(pkg, type);
		SwappedItem prior = _swapped.Items.FirstOrDefault((SwappedItem i) => string.Equals(i.SourceId, src.Id, StringComparison.OrdinalIgnoreCase));
		if (prior != null)
		{
			ReleaseGameReader();
			await Task.Run(() => engine.RevertPackages(prior.PatchedPackages));
		}
		PartResolver resolver = await EnsureResolverAsync();
		SwapLog.Text = "Reading " + src.Name + "'s files...";
		FnItemRig rig = await Task.Run(() => FnItemResolve.Resolve(resolver, read, src.Id, kind, src.Name, Log));
		string text = await Task.Run(() => FnItemResolve.Problem(rig, read));
		if (text != null)
		{
			throw new InvalidOperationException(text + "\n\nNothing was changed.");
		}
		List<string> containers = await Task.Run(() => (from pk in rig.Packages
			select resolver.LocalContainerName(pk) into n
			where !string.IsNullOrEmpty(n)
			select (n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
		ReleaseGameReader();
		SwappedItem item = new SwappedItem
		{
			ModKey = mod.Key,
			Kind = KindName(cat),
			SourceId = src.Id,
			SourceName = src.Name,
			SourceIcon = src.IconUrl,
			TargetId = src.Id,
			TargetName = mod.Manifest.Name,
			TargetIcon = ModIconUri(mod, src.IconUrl),
			ConvertedAt = DateTime.Now
		};
		item.Notes.Add(src.Name + " keeps its name and picture in the Locker; only the " + label + " itself changes. It's lined up automatically, so if it sits a little off, make the drip again with the model closer to the blue outline.");
		SwapLog.Text = "Working...";
		await Task.Run(() =>
		{
			foreach (string item2 in containers)
			{
				string text2 = engine.EnsureContainer(item2);
				if (text2 != null)
				{
					throw new InvalidOperationException($"The swapper couldn't open the game file {item2} ({text2}). Epic's launcher is probably still updating or verifying Fortnite. Let it finish, close it, press Connect, then try again.");
				}
			}
			try
			{
				ApplyItemModel(mod, item, rig);
			}
			catch
			{
				try
				{
					engine.RevertPackages(item.PatchedPackages);
				}
				catch
				{
				}
				throw;
			}
		});
		_swapped.Add(item);
		return item;
	}

	private static List<string> MissingDownloads(PartResolver resolver, OnDemandIndex? idx, string cosmeticId)
	{
		List<string> list = new List<string>();
		if (idx == null || !idx.Usable)
		{
			return list;
		}
		try
		{
			string text = resolver.FindAssetPath(cosmeticId);
			if (text == null)
			{
				return list;
			}
			string input = resolver.DumpJson(text);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match item in ReportPath.Matches(input))
			{
				string value = item.Groups[1].Value;
				if (value.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("/Engine/", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text2;
				try
				{
					text2 = PackageNames.Normalize(value);
				}
				catch
				{
					continue;
				}
				if (hashSet.Add(text2))
				{
					string text3 = PartPair.LeafOf(text2);
					if (!text2.Contains("/UI/", StringComparison.OrdinalIgnoreCase) && !text3.StartsWith("T_", StringComparison.OrdinalIgnoreCase) && !text3.StartsWith("T-", StringComparison.OrdinalIgnoreCase) && idx.CheckPackage(PackageNames.ChunkId(text2)) == ChunkState.NotInstalled)
					{
						list.Add(text3);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static string MissingNote(string kind, string wantedName, List<string> missing)
	{
		List<string> list = missing.Where((string m) => AnimLeaf.IsMatch(m)).ToList();
		List<string> list2 = missing.Except(list).ToList();
		string value = kind.ToLowerInvariant();
		if (list.Count > 0)
		{
			return $"{wantedName}'s animation isn't downloaded on this PC ({Eg(list)}), so this {value} will do nothing when you use it. Fortnite has to download it first (Pre-download Streamed Assets in the Epic launcher), or pick an emote that isn't dimmed." + ((list2.Count > 0) ? $" {list2.Count} other file(s) are missing too." : "");
		}
		return $"{wantedName} uses {list2.Count} file(s) that aren't downloaded on this PC ({Eg(list2)}), so it can show up empty or do nothing in game. Fortnite has to download it first (Pre-download Streamed Assets in the Epic launcher), or pick one that isn't dimmed.";
		static string Eg(List<string> l)
		{
			return string.Join(", ", l.Take(3)) + ((l.Count > 3) ? ", ..." : "");
		}
	}

	public static bool IsModelFile(string path)
	{
		return MkModelExt.Contains(Path.GetExtension(path).ToLowerInvariant());
	}

	private void OpenMakeModel_Click(object sender, RoutedEventArgs e)
	{
		OpenMakeModel(null);
	}

	private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = _engine?.BackupFolder ?? PakBackup.FolderFor(_state.FilePath);
			Directory.CreateDirectory(text);
			int count = PakBackup.List(text).Count;
			BackupInfo.Text = ((count == 0) ? "Nothing backed up yet. After your next swap, a copy of the original pak shows up in this folder." : $"{count} original pak{((count == 1) ? "" : "s")} backed up. Each folder has the untouched table of contents and the original data size.");
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't open the folder", ex.Message);
		}
	}

	private void OpenMakeModel(string? path)
	{
		CloseOverlays();
		MakeOverlay.Visibility = Visibility.Visible;
		if (path != null && _mkBuilds)
		{
			MkApplyKind(builds: false);
		}
		MainWindow mainWindow = this;
		string text;
		if (_mkBuilds)
		{
			text = "Pick a look, name it, press Create drip.";
		}
		else
		{
			text = ((_mkSrc == null) ? "Choose a model to start." : "");
		}
		mainWindow.MkStatusSet(text, bad: false);
		if (path != null)
		{
			MkLoadAsync(path);
		}
		MkPreflightAsync();
	}

	private async Task MkPreflightAsync()
	{
		SwapEngine engine = _engine;
		if (engine == null || _mkSrc != null)
		{
			return;
		}
		FnItemKind? mkItem = _mkItem;
		if (mkItem.HasValue)
		{
			return;
		}
		try
		{
			FnRigInfo rig = FnRigInfo.MaleRebirth;
			List<string> list = await Task.Run(() => engine.CheckPackagesReadable(rig.Packages.Select((string p) => ((string p, byte))(p: p, 1))));
			if (_mkSrc == null && list.Count > 0)
			{
				MkStatusSet("Heads up: the swapper can't read the character this maker builds on (" + list[0] + "). Press Fix problems on Home, then reopen this. Saving a drip still works.", bad: true);
			}
		}
		catch
		{
		}
	}

	private static string MkHealthTips(FgModelSource src, FnTarget tgt)
	{
		List<string> list = new List<string>();
		int num = tgt.Tri.Length / 3;
		double num2 = tgt.Max.Z - tgt.Min.Z;
		if (num > 120000)
		{
			list.Add($"It has {num:N0} triangles, which is heavy. A lighter model (under 60,000) loads faster and is less likely to fail.");
		}
		if (num2 < 40.0)
		{
			list.Add("It came out very small. Try the Size slider, or check the model's units.");
		}
		else if (num2 > 400.0)
		{
			list.Add("It came out very big. Try the Size slider.");
		}
		if (!src.HasUv && src.Materials.All((FgSourceMaterial m) => m.Picture == null))
		{
			list.Add("It has no picture and no picture coordinates, so it will be one flat colour unless you add a picture.");
		}
		if (list.Count != 0)
		{
			return " " + string.Join(" ", list);
		}
		return "";
	}

	private void CloseMake_Click(object sender, RoutedEventArgs e)
	{
		MakeOverlay.Visibility = Visibility.Collapsed;
	}

	private void MkStatusSet(string text, bool bad)
	{
		MkStatus.Text = text;
		MkStatus.Foreground = (Brush)FindResource(bad ? "Danger" : "Text2");
	}

	private void MkUpdateButtons()
	{
		bool isEnabled = (_mkBuilds ? MkBReady() : (_mkTarget != null && MkNameBox.Text.Trim().Length > 0));
		MkCreateBtn.IsEnabled = isEnabled;
		MkSaveBtn.IsEnabled = isEnabled;
	}

	private void MkName_Changed(object sender, TextChangedEventArgs e)
	{
		if (!_mkMute)
		{
			_mkNameAuto = false;
		}
		MkUpdateButtons();
	}

	private void MkChooseModel_Click(object sender, RoutedEventArgs e)
	{
		string text = string.Join(";", FgModelImport.Extensions.Select((string x) => "*" + x).Append("*.zip").Distinct());
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Choose a 3D model",
			Filter = "3D models and zips|" + text + "|All files|*.*"
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			MkLoadAsync(openFileDialog.FileName);
		}
	}

	private void MkChoosePicture_Click(object sender, RoutedEventArgs e)
	{
		if (_mkSrc == null)
		{
			MkStatusSet("Choose a model first, then the picture it should wear.", bad: true);
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Choose a picture",
			Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All files|*.*"
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			_mkPic = TexImage.Load(File.ReadAllBytes(openFileDialog.FileName));
			MkPicBox.Text = Path.GetFileName(openFileDialog.FileName);
			MkPicNote.Text = $"Wearing {Path.GetFileName(openFileDialog.FileName)} ({_mkPic.Width}×{_mkPic.Height}) on the whole model.";
			MkSchedule(immediate: false);
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't read that picture", ex.Message);
		}
	}

	private void MkClearPicture_Click(object sender, RoutedEventArgs e)
	{
		_mkPic = null;
		MkPicBox.Text = "";
		MkPicNote.Text = "Left empty, the model's own pictures and colours are used.";
		if (_mkSrc != null)
		{
			MkSchedule(immediate: false);
		}
	}

	private async Task MkLoadAsync(string path)
	{
		MkBusyShow("Reading the model…");
		try
		{
			FgModelSource src = await Task.Run(() => FnModelZip.Load(path));
			int mkMerged = FgMaterialReduce.Reduce(src, FnModelFile.MaxMaterials);
			_mkSrc = src;
			_mkPath = path;
			_mkAutoUp = MkGuessUp(src);
			FgUpAxis up0 = _mkAutoUp;
			_mkAutoYaw = await Task.Run(() => MkGuessYaw(src, up0, mirror: false));
			_mkTarget = null;
			_mkFitBuild = null;
			_mkReads.Clear();
			MkFileBox.Text = Path.GetFileName(path);
			MkEmpty.Visibility = Visibility.Collapsed;
			_mkPic = null;
			MkPicBox.Text = "";
			TextBlock mkPicNote = MkPicNote;
			string text;
			if (src.Materials.Any((FgSourceMaterial m) => m.Picture != null))
			{
				text = "Using the model's own pictures.";
			}
			else
			{
				text = (src.HasUv ? "The model has no picture, so it uses its colours." : "This model has no picture coordinates, so it uses its colours. Add a picture to dress it.");
			}
			if (mkMerged > 0)
			{
				text = text + " Fortnite allows " + FnModelFile.MaxMaterials + " materials, so " + mkMerged + " small ones were merged.";
			}
			mkPicNote.Text = text;
			_mkMute = true;
			MkReset();
			if (_mkNameAuto || MkNameBox.Text.Trim().Length == 0)
			{
				string s = (string.IsNullOrWhiteSpace(src.Name) ? Path.GetFileNameWithoutExtension(path) : src.Name);
				MkNameBox.Text = MkPretty(s);
				_mkNameAuto = true;
			}
			_mkMute = false;
			FnItemKind? mkItem = _mkItem;
			if (mkItem.HasValue)
			{
				MkInfo.Text = $"{src.VertexCount:N0} points, {src.TriangleCount:N0} triangles. " + MkLineUpHelp();
			}
			else
			{
				MkInfo.Text = $"{src.VertexCount:N0} points, {src.TriangleCount:N0} triangles. The blue line is the character (T-pose). It was turned to face the front automatically; if it's off, press Auto-align, Turn 90° or Flip front/back, then match the blue line: feet on the ground, arms along it. Drag the picture to turn the view.";
			}
			await MkRebuildAsync();
		}
		catch (Exception ex)
		{
			_mkMute = false;
			MkBusyHide();
			MkStatusSet("Couldn't read that model.", bad: true);
			ShowNotice("Couldn't read that model", ex.Message);
		}
	}

	private static string MkPretty(string s)
	{
		s = s.Replace('_', ' ').Replace('-', ' ').Trim();
		if (s.Length <= 60)
		{
			if (s.Length != 0)
			{
				return s;
			}
			return "My model";
		}
		return s.Substring(0, 60);
	}

	private FnFitOptions MkReadFit()
	{
		FgUpAxis up = MkUpBox.SelectedIndex switch
		{
			1 => FgUpAxis.Y, 
			2 => FgUpAxis.Z, 
			3 => FgUpAxis.NegY, 
			_ => _mkAutoUp, 
		};
		return new FnFitOptions
		{
			Up = up,
			Yaw = (float)MkYaw.Value,
			Mirror = (MkMirrorBox.IsChecked == true),
			Scale = (float)MkSize.Value,
			Width = (float)MkWidth.Value,
			OffsetX = (float)MkMoveX.Value,
			OffsetY = (float)MkMoveY.Value,
			OffsetZ = (float)MkMoveZ.Value,
			PictureOverride = _mkPic
		};
	}

	private void MkReset()
	{
		bool mkMute = _mkMute;
		_mkMute = true;
		MkSize.Value = 1.0;
		MkWidth.Value = 1.0;
		MkMoveX.Value = 0.0;
		MkMoveY.Value = 0.0;
		MkMoveZ.Value = 0.0;
		MkYaw.Value = MkWrap(Math.Round(_mkAutoYaw));
		MkUpBox.SelectedIndex = 0;
		MkMirrorBox.IsChecked = false;
		MkLabels();
		_mkMute = mkMute;
	}

	private static double MkWrap(double deg)
	{
		deg %= 360.0;
		if (deg > 180.0)
		{
			deg -= 360.0;
		}
		if (deg < -180.0)
		{
			deg += 360.0;
		}
		return deg;
	}

	private void MkAutoAlign_Click(object sender, RoutedEventArgs e)
	{
		if (_mkSrc == null)
		{
			MkStatusSet("Choose a model first.", bad: true);
			return;
		}
		_mkAutoYaw = MkGuessYaw(_mkSrc, MkReadFit().Up, MkMirrorBox.IsChecked == true);
		MkYaw.Value = MkWrap(Math.Round(_mkAutoYaw));
		MkLabels();
		MkSchedule(immediate: true);
	}

	private void MkOutline_Click(object sender, RoutedEventArgs e)
	{
		if (_mkSrc != null)
		{
			MkRenderAsync();
		}
	}

	private void MkLabels()
	{
		MkSizeText.Text = $"{MkSize.Value:0.00}x";
		MkWidthText.Text = $"{MkWidth.Value:0.00}x";
		MkMoveXText.Text = $"{MkMoveX.Value:0.#}";
		MkMoveYText.Text = $"{MkMoveY.Value:0.#}";
		MkMoveZText.Text = $"{MkMoveZ.Value:0.#}";
		MkYawText.Text = $"{MkYaw.Value:0}°";
	}

	private void MkReset_Click(object sender, RoutedEventArgs e)
	{
		MkReset();
		if (_mkSrc != null)
		{
			MkSchedule(immediate: false);
		}
	}

	private void MkSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_mkMute && IsLoaded && MkYawText != null)
		{
			MkLabels();
			if (_mkSrc != null)
			{
				MkSchedule(immediate: false);
			}
		}
	}

	private void MkFit_Changed(object sender, RoutedEventArgs e)
	{
		if (!_mkMute && IsLoaded && MkYawText != null && _mkSrc != null)
		{
			try
			{
				_mkAutoYaw = MkGuessYaw(_mkSrc, MkReadFit().Up, MkMirrorBox.IsChecked == true);
			}
			catch
			{
			}
			MkSchedule(immediate: false);
		}
	}

	private void MkTurn_Click(object sender, RoutedEventArgs e)
	{
		double num = MkYaw.Value + 90.0;
		if (num > 180.0)
		{
			num -= 360.0;
		}
		MkYaw.Value = num;
	}

	private void MkFlip_Click(object sender, RoutedEventArgs e)
	{
		double num = MkYaw.Value + 180.0;
		if (num > 180.0)
		{
			num -= 360.0;
		}
		MkYaw.Value = num;
	}

	private void MkView_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && float.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			_mkView = result;
			MkRenderAsync();
		}
	}

	private void MkShow_Click(object sender, RoutedEventArgs e)
	{
		if (MkShowFit.IsChecked == true && _engine == null)
		{
			MkShowModel.IsChecked = true;
			MkStatusSet("Connect on the Home tab first, then you can see it on the character.", bad: true);
		}
		else if (_mkSrc != null)
		{
			if (MkShowFit.IsChecked == true)
			{
				MkSchedule(immediate: true);
			}
			else
			{
				MkRenderAsync();
			}
		}
	}

	private void MkPreview_Down(object sender, MouseButtonEventArgs e)
	{
		_mkDragging = true;
		_mkDrag = e.GetPosition(MkPreview);
		MkPreview.CaptureMouse();
	}

	private void MkPreview_Move(object sender, MouseEventArgs e)
	{
		if (_mkDragging)
		{
			Point position = e.GetPosition(MkPreview);
			_mkView = (float)((((double)_mkView - (position.X - _mkDrag.X) * 0.7) % 360.0 + 360.0) % 360.0);
			_mkDrag = position;
			if ((DateTime.UtcNow - _mkLastDragRender).TotalMilliseconds > 60.0)
			{
				_mkLastDragRender = DateTime.UtcNow;
				MkRenderAsync();
			}
		}
	}

	private void MkPreview_Up(object sender, MouseButtonEventArgs e)
	{
		if (_mkDragging)
		{
			_mkDragging = false;
			MkPreview.ReleaseMouseCapture();
			MkRenderAsync();
		}
	}

	private void MkBusyShow(string text)
	{
		MkBusyText.Text = text;
		MkBusy.Visibility = Visibility.Visible;
	}

	private void MkBusyHide()
	{
		MkBusy.Visibility = Visibility.Collapsed;
	}

	private void MkSchedule(bool immediate)
	{
		if (_mkTimer == null)
		{
			_mkTimer = new DispatcherTimer();
		}
		_mkTimer.Stop();
		_mkTimer.Interval = TimeSpan.FromMilliseconds(immediate ? 20 : ((MkShowFit.IsChecked == true) ? 450 : 120));
		_mkTimer.Tick -= MkTimer_Tick;
		_mkTimer.Tick += MkTimer_Tick;
		_mkTimer.Start();
	}

	private async void MkTimer_Tick(object? sender, EventArgs e)
	{
		_mkTimer?.Stop();
		await MkRebuildAsync();
	}

	private byte[] MkRead(string pkg, byte type)
	{
		return _mkReads.GetOrAdd(pkg + "#" + type, (string _) => _engine.ReadPackageChunk(pkg, type));
	}

	private async Task MkRebuildAsync()
	{
		FgModelSource src = _mkSrc;
		if (src == null)
		{
			return;
		}
		int gen = ++_mkGen;
		FnFitOptions fit = MkReadFit();
		float yaw = _mkView;
		FnItemKind? item = _mkItem;
		bool onChar = !item.HasValue && MkShowFit.IsChecked == true && _engine != null;
		bool outline = MkOutlineBox.IsChecked == true;
		FnRigInfo rig = FnRigInfo.MaleRebirth;
		if (onChar)
		{
			MkBusyShow("Building it as the character…");
		}
		try
		{
			(FnTarget, FnBuild, byte[]) tuple = await Task.Run(() =>
			{
				FnTarget fnTarget;
				if (item.HasValue)
				{
					FnItemKind valueOrDefault2 = item.GetValueOrDefault();
					fnTarget = FnItem.Build(src, valueOrDefault2, fit);
				}
				else
				{
					fnTarget = FnTarget.Build(src, fit);
				}
				FnTarget fnTarget2 = fnTarget;
				FnBuild fnBuild = null;
				byte[] item2;
				if (onChar)
				{
					fnBuild = FnReplace.Build(MkRead, rig, fnTarget2);
					item2 = FnPreview.RenderParts(fnBuild.Preview, yaw, 640);
				}
				else
				{
					byte[] array;
					if (item.HasValue)
					{
						FnItemKind valueOrDefault3 = item.GetValueOrDefault();
						array = FnPreview.RenderItem(fnTarget2, valueOrDefault3, yaw, 640, guides: true, outline);
					}
					else
					{
						array = FnPreview.RenderTarget(fnTarget2, yaw, 640, guides: true, outline);
					}
					item2 = array;
				}
				return (tgt: fnTarget2, fb: fnBuild, px: item2);
			});
			if (gen == _mkGen)
			{
				(_mkTarget, _mkFitBuild, _) = tuple;
				MkShowPixels(tuple.Item3);
				string text = $"{tuple.Item1.Max.Z - tuple.Item1.Min.Z:0} cm tall";
				if (item.HasValue)
				{
					FnItemKind valueOrDefault = item.GetValueOrDefault();
					string text2 = MkItemTips(src, tuple.Item1, valueOrDefault);
					MkStatusSet($"{tuple.Item1.Tri.Length / 3:N0} triangles, {text}. Line it up with the blue {FnItem.Label(valueOrDefault)}.{text2}", text2.Length > 0);
				}
				else if (onChar && tuple.Item2 != null)
				{
					MkStatusSet("As it will look in game: your model's own shape, skinned to the character. " + text + ".", bad: false);
				}
				else
				{
					string text3 = ((Math.Abs(MkWrap(fit.Yaw - _mkAutoYaw)) > 25.0) ? " It looks turned compared with the character: press Auto-align." : "");
					string text4 = MkHealthTips(src, tuple.Item1);
					MkStatusSet($"{tuple.Item1.Tri.Length / 3:N0} triangles, {text}. The character (blue line) is about 166 cm tall.{text3}{text4}", text3.Length > 0 || text4.Length > 0);
				}
				MkUpdateButtons();
			}
		}
		catch (Exception ex)
		{
			if (gen == _mkGen)
			{
				_mkTarget = null;
				_mkFitBuild = null;
				string text5 = Friendly.Explain(ex.Message);
				MkStatusSet(onChar ? ("Couldn't show it on the character: " + text5.Split('\n')[0] + " Switched back to the flat preview; you can still create the drip.") : text5.Split('\n')[0], bad: true);
				if (onChar)
				{
					MkShowModel.IsChecked = true;
					MkSchedule(immediate: true);
				}
				MkUpdateButtons();
			}
		}
		finally
		{
			if (gen == _mkGen)
			{
				MkBusyHide();
			}
		}
	}

	private async Task MkRenderAsync()
	{
		FnTarget tgt = _mkTarget;
		FnBuild fb = _mkFitBuild;
		if (tgt == null)
		{
			return;
		}
		int gen = ++_mkRenderGen;
		float yaw = _mkView;
		FnItemKind? item = _mkItem;
		bool onChar = !item.HasValue && MkShowFit.IsChecked == true && fb != null;
		bool outline = MkOutlineBox.IsChecked == true;
		try
		{
			byte[] bgra = await Task.Run(() =>
			{
				if (!onChar)
				{
					if (!item.HasValue)
					{
						return FnPreview.RenderTarget(tgt, yaw, 640, guides: true, outline);
					}
					FnItemKind valueOrDefault = item.GetValueOrDefault();
					return FnPreview.RenderItem(tgt, valueOrDefault, yaw, 640, guides: true, outline);
				}
				return FnPreview.RenderParts(fb.Preview, yaw, 640);
			});
			if (gen == _mkRenderGen)
			{
				MkShowPixels(bgra);
			}
		}
		catch
		{
		}
	}

	private void MkShowPixels(byte[] bgra)
	{
		BitmapSource bitmapSource = BitmapSource.Create(640, 640, 96.0, 96.0, PixelFormats.Bgra32, null, bgra, 2560);
		bitmapSource.Freeze();
		MkPreview.Source = bitmapSource;
	}

	private static string MkSafeName(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = s.Trim();
		foreach (char c in text)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == '-' || c == '_') ? true : false);
				flag = flag2;
			}
			stringBuilder2.Append(flag ? c : '_');
		}
		if (stringBuilder.Length != 0)
		{
			return stringBuilder.ToString();
		}
		return "model";
	}

	private async void MkCreate_Click(object sender, RoutedEventArgs e)
	{
		if (_mkBuilds)
		{
			if (MkBReady())
			{
				await MkBCreateAsync(intoLibrary: true, null);
			}
		}
		else if (_mkTarget != null && _mkSrc != null)
		{
			string text = MkNameBox.Text.Trim();
			string text2 = MkSafeName(text);
			string text3 = Path.Combine(_mods.Directory, text2 + ".dripFN");
			int num = 2;
			while (File.Exists(text3))
			{
				text3 = Path.Combine(_mods.Directory, $"{text2}-{num}.dripFN");
				num++;
			}
			FnItemKind? mkItem = _mkItem;
			if (mkItem.HasValue)
			{
				Directory.CreateDirectory(_mods.Directory);
				await MkWriteItemAsync(text3, text, intoLibrary: true);
			}
			else
			{
				await MkWriteAsync(text3, text, intoLibrary: true);
			}
		}
	}

	private async void MkSaveCopy_Click(object sender, RoutedEventArgs e)
	{
		if ((_mkBuilds && !MkBReady()) || (!_mkBuilds && (_mkTarget == null || _mkSrc == null)))
		{
			return;
		}
		string text = MkNameBox.Text.Trim();
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Save the drip",
			Filter = "Fortnite drip (*.dripFN)|*.dripFN",
			FileName = MkSafeName(text) + ".dripFN",
			AddExtension = true,
			DefaultExt = ".dripFN"
		};
		if (saveFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		if (_mkBuilds)
		{
			await MkBCreateAsync(intoLibrary: false, saveFileDialog.FileName);
			return;
		}
		FnItemKind? mkItem = _mkItem;
		if (mkItem.HasValue)
		{
			await MkWriteItemAsync(saveFileDialog.FileName, text, intoLibrary: false);
		}
		else
		{
			await MkWriteAsync(saveFileDialog.FileName, text, intoLibrary: false);
		}
	}

	private byte[]? MkIconPng(FnTarget tgt, float yaw)
	{
		try
		{
			int num = 640;
			FnItemKind? mkItem = _mkItem;
			byte[] array;
			if (mkItem.HasValue)
			{
				FnItemKind valueOrDefault = mkItem.GetValueOrDefault();
				array = FnPreview.RenderItem(tgt, valueOrDefault, yaw, num, guides: false, outline: false, character: false);
			}
			else
			{
				array = FnPreview.RenderTarget(tgt, yaw, num, guides: false, outline: false);
			}
			byte[] pixels = array;
			BitmapSource bitmapSource = BitmapSource.Create(num, num, 96.0, 96.0, PixelFormats.Bgra32, null, pixels, num * 4);
			bitmapSource.Freeze();
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
			using MemoryStream memoryStream = new MemoryStream();
			pngBitmapEncoder.Save(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
	}

	private async Task MkWriteAsync(string dest, string name, bool intoLibrary)
	{
		FnTarget tgt = _mkTarget;
		float yaw = _mkView;
		MkBusyShow("Saving…");
		try
		{
			await Task.Run(() =>
			{
				FnModelFile.Write(dest, name, "", "Custom model made in Drip Swapper.", tgt, FnRigInfo.MaleRebirth, "replace", null, MkIconPng(tgt, yaw));
				List<string> list = ModPack.Load(dest).Validate();
				if (list.Count > 0)
				{
					try
					{
						File.Delete(dest);
					}
					catch
					{
					}
					throw new InvalidDataException(string.Join(" ", list));
				}
			});
			if (intoLibrary)
			{
				MakeOverlay.Visibility = Visibility.Collapsed;
				ShowPage(Page.Mods);
				ModsMsg.Text = "Made '" + name + "'. Hit Use… on it, pick the skin you own, and it will look like your model in the lobby.";
				ModsMsg.Foreground = (Brush)FindResource("Good");
			}
			else
			{
				MkStatusSet("Saved " + Path.GetFileName(dest) + ".", bad: false);
			}
		}
		catch (Exception ex)
		{
			MkStatusSet("Couldn't save the drip.", bad: true);
			ShowNotice("Couldn't save the drip", Friendly.Explain(ex.Message));
		}
		finally
		{
			MkBusyHide();
		}
	}

	private void ApplyModModel(ModPack mod, SwappedItem? item, FnRigInfo? carrier = null, IReadOnlyList<string>? ownedHide = null)
	{
		SwapEngine engine = _engine;
		ModFnModel modFnModel = mod.Manifest.FnModel ?? throw new InvalidOperationException("That drip has no model in it.");
		FnRigInfo fnRigInfo = FnRigInfo.Find(modFnModel.Rig) ?? throw new InvalidOperationException("This drip was made for a character this version doesn't have ('" + modFnModel.Rig + "'). Update Drip Swapper.");
		FnRigInfo fnRigInfo2 = (CanUseOtherBody(modFnModel) ? (carrier ?? CarrierOf(item) ?? fnRigInfo) : fnRigInfo);
		Log($"Putting '{mod.Manifest.Name}' on the character ({fnRigInfo2.Name}, this takes a moment)...");
		Func<string, byte, byte[]> func = (string pkg, byte type) => engine.ReadPackageChunk(pkg, type);
		FnBuild fnBuild2;
		try
		{
			FnBuild fnBuild;
			if (!string.Equals(modFnModel.Mode, "fit", StringComparison.OrdinalIgnoreCase))
			{
				FnSkin fnSkin = FnModelFile.LoadSkin(mod);
				fnBuild = ((fnSkin != null) ? FnReplace.BuildNative(func, fnRigInfo2, FnModelFile.Load(mod), fnSkin, Log) : FnReplace.Build(func, fnRigInfo2, FnModelFile.Load(mod), Log));
			}
			else
			{
				fnBuild = FnModelApplier.Build(func, fnRigInfo2, FnModelFile.Load(mod), Log);
			}
			fnBuild2 = fnBuild;
		}
		catch (Exception ex) when (!(ex is BodyBuildException))
		{
			throw new BodyBuildException(fnRigInfo2, ex);
		}
		foreach (string note in fnBuild2.Notes)
		{
			Log(note);
		}
		List<string> list = (ownedHide ?? item?.Parts.Where((SwappedPart p) => p.Slot == "ModHide").Select((SwappedPart p) => p.SourcePackage).ToList() ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		List<string> list2 = new List<string>();
		foreach (string item2 in list)
		{
			if (fnRigInfo2.Packages.Contains(item2, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}
			try
			{
				byte[] array = (byte[])func(item2, 2).Clone();
				List<FnLod> list3 = FnMesh.FindLods(array);
				if (list3.Count == 0)
				{
					Log("Can't hide " + PartPair.LeafOf(item2) + ": its shape isn't in a layout this version understands.");
					continue;
				}
				foreach (FnLod item3 in list3)
				{
					FnFit.Hide(array, item3);
				}
				fnBuild2.Patches.Add(new FnPatch(item2, 2, array, "owned piece hidden"));
				list2.Add(item2);
			}
			catch (Exception ex2)
			{
				Log($"Couldn't hide {PartPair.LeafOf(item2)} ({ex2.Message}); it may still show on top of the model.");
			}
		}
		engine.RevertPackages(fnRigInfo2.Packages.Concat(list));
		List<string> list4 = new List<string>();
		try
		{
			foreach (FnPatch item4 in fnBuild2.Patches.OrderBy((FnPatch p) => p.Package.Contains("/Textures/", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
			{
				SwapResult swapResult = engine.PatchPackageChunk(item4.Package, item4.Type, item4.Data, mod.Manifest.Name);
				list4.Add(swapResult.Source);
				Log($"OK  {item4.What}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
			}
		}
		catch (Exception ex3)
		{
			Log("FAIL model: " + ex3.Message);
			try
			{
				engine.RevertPackages(list4);
			}
			catch (Exception ex4)
			{
				Log("rollback: " + ex4.Message);
			}
			throw;
		}
		List<string> list5 = engine.MissingPackages(list4);
		if (list5.Count > 0)
		{
			try
			{
				engine.RevertPackages(list4);
			}
			catch
			{
			}
			throw new InvalidOperationException("The model was written but the game files don't show it afterwards (" + string.Join(", ", list5.Select((string m) => m.Split('/')[^1])) + "). Close the Epic Launcher and try again.");
		}
		Log($"Model check: all {list4.Count} model files are in your game files.");
		if (item == null)
		{
			return;
		}
		ModelTakesTheSlot(item, mod, fnRigInfo2, list4);
		foreach (string item5 in list4)
		{
			if (!item.PatchedPackages.Contains(item5, StringComparer.OrdinalIgnoreCase))
			{
				item.PatchedPackages.Add(item5);
			}
		}
		item.Parts.RemoveAll((SwappedPart p) => p.Slot == "ModModel" || p.Slot == "ModHide");
		item.Parts.Add(new SwappedPart
		{
			Slot = "ModModel",
			SourcePackage = fnRigInfo2.BodyMesh,
			TargetPackage = mod.Key
		});
		foreach (string item6 in list2)
		{
			item.Parts.Add(new SwappedPart
			{
				Slot = "ModHide",
				SourcePackage = item6,
				TargetPackage = mod.Key
			});
		}
	}

	private static bool CanUseOtherBody(ModFnModel fm)
	{
		return !string.Equals(fm.Mode, "fit", StringComparison.OrdinalIgnoreCase);
	}

	private static FnRigInfo? CarrierOf(SwappedItem? item)
	{
		SwappedPart swappedPart = item?.Parts.FirstOrDefault((SwappedPart p) => p.Slot == "ModModel");
		if (swappedPart != null)
		{
			return FnRigInfo.ForBody(swappedPart.SourcePackage);
		}
		return null;
	}

	private void ModelTakesTheSlot(SwappedItem current, ModPack mod, FnRigInfo rig, IReadOnlyCollection<string> written)
	{
		bool flag = false;
		foreach (SwappedItem item2 in _swapped.Items.ToList())
		{
			if (item2 == current)
			{
				continue;
			}
			SwappedPart swappedPart = item2.Parts.FirstOrDefault((SwappedPart p) => p.Slot == "ModModel");
			if (swappedPart != null && string.Equals(swappedPart.SourcePackage, rig.BodyMesh, StringComparison.OrdinalIgnoreCase))
			{
				item2.Parts.RemoveAll((SwappedPart p) => p.Slot == "ModModel");
				item2.PatchedPackages.RemoveAll((string p) => written.Contains(p, StringComparer.OrdinalIgnoreCase));
				string item = $"All {FnRigInfo.Carriers.Count} custom model bodies are taken, so this one now shows '{mod.Manifest.Name}'. Undo another model first to get it back.";
				item2.Notes.RemoveAll((string n) => n.StartsWith("All ", StringComparison.Ordinal) && n.Contains("custom model bodies are taken", StringComparison.Ordinal));
				item2.Notes.Add(item);
				flag = true;
			}
		}
		if (flag)
		{
			try
			{
				_swapped.Save();
			}
			catch
			{
			}
		}
	}

	private void ReapplyModModel(SwappedItem item)
	{
		ModPack mod = _mods.List().FirstOrDefault((ModPack m) => string.Equals(m.Key, item.ModKey, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("The drip '" + item.ModKey + "' is no longer in your Mods list.");
		ApplyModModel(mod, null, CarrierOf(item), (from p in item.Parts
			where p.Slot == "ModHide"
			select p.SourcePackage).ToList());
	}

	private string CarrierKey(FnRigInfo rig)
	{
		return RuntimeHelpers.GetHashCode(_engine) + "|" + rig.Id;
	}

	private CosmeticVm CarrierVm(FnRigInfo rig)
	{
		CosmeticVm cosmeticVm = _lists[Cat.Outfit].FirstOrDefault((CosmeticVm c) => string.Equals(c.Id, rig.BaseSkin, StringComparison.OrdinalIgnoreCase));
		if (cosmeticVm != null)
		{
			return cosmeticVm;
		}
		string text;
		if (!rig.Name.EndsWith(" (male)", StringComparison.Ordinal))
		{
			text = rig.Name;
		}
		else
		{
			string name = rig.Name;
			text = name.Substring(0, name.Length - 7);
		}
		string name2 = text;
		return new CosmeticVm(new Cosmetic(rig.BaseSkin, name2, "Common", "", ""));
	}

	private async Task<(CosmeticVm? Vm, string Why)> ChooseCarrierAsync(ModPack mod, CosmeticVm owned)
	{
		ModFnModel fnModel = mod.Manifest.FnModel;
		if (fnModel == null || !CanUseOtherBody(fnModel))
		{
			CosmeticVm cosmeticVm = _lists[Cat.Outfit].FirstOrDefault((CosmeticVm c) => string.Equals(c.Id, mod.Manifest.Base, StringComparison.OrdinalIgnoreCase));
			return (cosmeticVm != null && cosmeticVm.Available) ? (Vm: cosmeticVm, Why: "") : (Vm: null, Why: "This drip needs the skin '" + mod.Manifest.Base + "', and it isn't in your local game files, so it can't be used.");
		}
		SwapEngine engine = _engine;
		List<SwappedItem> source = _swapped.Items.Where((SwappedItem i) => i.Parts.Any((SwappedPart p) => p.Slot == "ModModel")).ToList();
		SwappedItem mine = source.FirstOrDefault((SwappedItem i) => string.Equals(i.SourceId, owned.Id, StringComparison.OrdinalIgnoreCase));
		Dictionary<string, SwappedItem> taken = new Dictionary<string, SwappedItem>(StringComparer.OrdinalIgnoreCase);
		foreach (SwappedItem item in source.Where((SwappedItem r) => r != mine))
		{
			FnRigInfo fnRigInfo = CarrierOf(item);
			if (fnRigInfo != null)
			{
				taken[fnRigInfo.Id] = item;
			}
		}
		List<FnRigInfo> order = new List<FnRigInfo>();
		Add(CarrierOf(mine));
		FnRigInfo fnRigInfo2 = FnRigInfo.ForSkin(owned.Id);
		if (fnRigInfo2 != null && !taken.ContainsKey(fnRigInfo2.Id))
		{
			Add(fnRigInfo2);
		}
		foreach (FnRigInfo item2 in FnRigInfo.Carriers.Where((FnRigInfo r) => !taken.ContainsKey(r.Id)))
		{
			Add(item2);
		}
		foreach (FnRigInfo item3 in from r in FnRigInfo.Carriers
			where taken.ContainsKey(r.Id)
			orderby taken[r.Id].ConvertedAt
			select r)
		{
			Add(item3);
		}
		List<string> reasons = new List<string>();
		foreach (FnRigInfo rig in order)
		{
			CosmeticVm vm = CarrierVm(rig);
			if (!vm.Available)
			{
				reasons.Add(vm.Name + " isn't in your local game files.");
				continue;
			}
			string key = CarrierKey(rig);
			if (!_carrierProblems.TryGetValue(key, out string value))
			{
				value = await Task.Run(() => FnCarriers.Problem(rig, (string pkg, byte type) => engine.ReadPackageChunk(pkg, type)));
				_carrierProblems[key] = value;
			}
			if (value != null)
			{
				reasons.Add(value);
				Log("Body skipped: " + value);
				continue;
			}
			Log("Custom model body: " + rig.Name + ".");
			return (Vm: vm, Why: "");
		}
		return (Vm: null, Why: "No body is available for a custom model right now.\n\n" + string.Join("\n", reasons.Take(5)));
		void Add(FnRigInfo? r)
		{
			if (r != null && !order.Contains(r))
			{
				order.Add(r);
			}
		}
	}

	private async void StartModelFlow(CosmeticVm owned, ModPack pm)
	{
		try
		{
			var (cosmeticVm, text) = await ChooseCarrierAsync(pm, owned);
			if (_owned == owned && _pendingMod == pm)
			{
				if (cosmeticVm == null)
				{
					ShowNotice("Can't use this model", text);
					return;
				}
				_wanted = cosmeticVm;
				OpenSwapOverlay();
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Can't use this model", Friendly.Explain(ex.Message));
		}
	}

	private static string ModIconUri(ModPack mod, string fallback)
	{
		string text = mod.EmbeddedIconFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DripSwapper", "previews"));
		if (text.Length <= 0)
		{
			return fallback;
		}
		return new Uri(text).AbsoluteUri;
	}

	private async Task<SwappedItem> ConvertModelOnlyAsync(Cosmetic src, ModPack mod)
	{
		ReleaseGameReader();
		SwappedItem item = new SwappedItem
		{
			ModKey = mod.Key,
			Kind = "Outfit",
			SourceId = src.Id,
			SourceName = src.Name,
			SourceIcon = src.IconUrl,
			TargetId = src.Id,
			TargetName = mod.Manifest.Name,
			TargetIcon = ModIconUri(mod, src.IconUrl),
			ConvertedAt = DateTime.Now
		};
		SwapEngine engine = _engine;
		SwappedItem prior = _swapped.Items.FirstOrDefault((SwappedItem i) => string.Equals(i.SourceId, src.Id, StringComparison.OrdinalIgnoreCase));
		await Task.Run(() =>
		{
			if (prior != null)
			{
				engine.RevertPackages(prior.PatchedPackages);
			}
			try
			{
				ApplyModModel(mod, item, FnRigInfo.ForSkin(src.Id));
			}
			catch
			{
				try
				{
					engine.RevertPackages(item.PatchedPackages);
				}
				catch
				{
				}
				throw;
			}
		});
		_swapped.Add(item);
		return item;
	}

	private void RefreshModsList()
	{
		List<ModVm> list = (from m in _mods.List((string m) =>
			{
				Log("drip skipped: " + m);
			})
			select new ModVm(m)).ToList();
		list.AddRange(from r in _mods.ListRifts()
			select new ModVm(r));
		ModsHost.ItemsSource = list;
		_modCount = list.Count;
		UpdateModsEmpty();
		RefreshPaksAsync();
	}

	private void SeedPreinstalledDrips()
	{
		try
		{
			Preinstalled.Result result = Preinstalled.SeedFortnite(_mods);
			Preinstalled.Result result2 = Preinstalled.SeedBuilds(_mods);
			foreach (string item in result.Problems.Concat(result2.Problems))
			{
				Log("preinstalled drip skipped: " + item);
			}
			if (result.Added + result2.Added > 0)
			{
				ModsMsg.Text = $"Added {result.Added + result2.Added} drips that came with the app" + ((result2.Added > 0) ? $" (including {result2.Added} Builds looks)." : ".");
				ModsMsg.Foreground = (Brush)FindResource("Good");
			}
		}
		catch (Exception ex)
		{
			Log("preinstalled drips: " + ex.Message);
		}
	}

	private void UpdateModsEmpty()
	{
		ModsEmpty.Visibility = ((_modCount + _pakCount != 0) ? Visibility.Collapsed : Visibility.Visible);
	}

	private async Task RefreshPaksAsync()
	{
		List<(string RefFile, string PakPath)> refs = _pakLib.List().ToList();
		List<(string, PakPack, List<(PakSkin, IconPixels)>)> list = await Task.Run(() =>
		{
			List<(string, PakPack, List<(PakSkin, IconPixels)>)> list2 = new List<(string, PakPack, List<(PakSkin, IconPixels)>)>();
			foreach (var item3 in refs)
			{
				string item = item3.RefFile;
				string item2 = item3.PakPath;
				PakPack pakPack = PakPack.Load(item2);
				List<(PakSkin, IconPixels)> list3 = new List<(PakSkin, IconPixels)>();
				if (pakPack.Problem == null)
				{
					try
					{
						LegacyPak pak = LegacyPak.Open(item2);
						foreach (PakSkin skin in pakPack.Skins)
						{
							list3.Add((skin, pakPack.ReadIcon(pak, skin)));
						}
					}
					catch
					{
						foreach (PakSkin skin2 in pakPack.Skins)
						{
							list3.Add((skin2, null));
						}
					}
				}
				list2.Add((item, pakPack, list3));
			}
			return list2;
		});
		PaksHost.ItemsSource = list.Select(((string Ref, PakPack Pack, List<(PakSkin Skin, IconPixels Px)> Skins) l) => new PakVm(l.Ref, l.Pack, l.Skins.Select(((PakSkin Skin, IconPixels Px) s) => new PakSkinVm(s.Skin, PakSkinVm.ToImage(s.Px))))).ToList();
		_pakCount = list.Count;
		UpdateModsEmpty();
	}

	private void PakRemove_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: PakVm dataContext })
		{
			_pakLib.Remove(dataContext.RefFile);
			RefreshModsList();
		}
	}

	private void PakSkin_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: PakSkinVm dataContext })
		{
			PakSkin skin = dataContext.Skin;
			ShowNotice(skin.Name, $"{skin.Files} files, {(double)skin.Bytes / 1048576.0:0.#} MB: {skin.Meshes} mesh, {skin.Textures} texture and {skin.Materials} material assets.\n\n" + "This pak is view only in Drip Swapper. These are loose files in an older game format, and the swapper can't convert them into this Fortnite build, so there is no Apply button.");
		}
	}

	private void RemoveAllMods_Click(object sender, RoutedEventArgs e)
	{
		List<ModPack> list = _mods.List().ToList();
		if (list.Count == 0)
		{
			ModsMsg.Text = "No drips to remove.";
			ModsMsg.Foreground = (Brush)FindResource("Text2");
		}
		else
		{
			if (Dlg.Show(this, $"Remove all {list.Count} drip(s) from this list?\n\nThis only deletes Drip Swapper's copies. Anything already applied to the game stays until you revert it.", "Remove all drips", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			{
				return;
			}
			foreach (ModPack item in list)
			{
				_mods.Remove(item);
			}
			ModsMsg.Text = $"Removed {list.Count} drip(s).";
			ModsMsg.Foreground = (Brush)FindResource("Good");
			RefreshModsList();
		}
	}

	private void ImportMod_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Import a drip",
			Filter = "Drips and skin paks (*.dripFN;*.drip;*.pak;*.rift;*.elenr;*.elenrmod;*.json)|*.dripFN;*.drip;*.pak;*.rift;*.elenr;*.elenrmod;*.json",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			ImportFiles(openFileDialog.FileNames);
		}
	}

	private void ImportFiles(IEnumerable<string> files)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		int num = 0;
		foreach (string file in files)
		{
			string text = Path.GetExtension(file).ToLowerInvariant();
			string fileName = Path.GetFileName(file);
			try
			{
				if (text == ".rift")
				{
					RiftPack riftPack = _mods.ImportRift(file);
					num++;
					if (riftPack.Compat != RiftCompat.Works)
					{
						list2.Add($"'{riftPack.Name}' was added but can't be applied: it was made for an old Fortnite ({((riftPack.Builds.Count > 0) ? string.Join(", ", riftPack.Builds.Select((double b) => b.ToString("0.00"))) : "version unknown")}), not 42.30.");
					}
					else
					{
						list.Add("Added '" + riftPack.Name + "'.");
					}
				}
				else if (text == ".pak")
				{
					PakPack pakPack = _pakLib.Add(file);
					num++;
					list.Add($"Added '{pakPack.Name}' ({pakPack.Skins.Count} skins). View only: it can't be installed.");
				}
				else if (ModPack.IsAddonExtension(text) || text == ".json")
				{
					ModPack modPack = _mods.Import(file);
					num++;
					list.Add("Added '" + modPack.Manifest.Name + "'.");
				}
			}
			catch (Exception ex)
			{
				list2.Add(fileName + ": " + ex.Message);
			}
		}
		List<string> list3 = new List<string>();
		if (list.Count > 3)
		{
			list3.Add($"Added {list.Count} drips.");
		}
		else
		{
			list3.AddRange(list);
		}
		list3.AddRange(list2.Take(3));
		if (list2.Count > 3)
		{
			list3.Add($"...and {list2.Count - 3} more problem(s).");
		}
		bool flag = list2.Count > 0;
		if (list3.Count == 0)
		{
			list3.Add("Drop a .dripFN, .pak or .rift file to add one.");
		}
		ModsMsg.Text = string.Join("  ", list3);
		TextBlock modsMsg = ModsMsg;
		MainWindow mainWindow = this;
		string resourceKey;
		if (flag)
		{
			resourceKey = "Danger";
		}
		else
		{
			resourceKey = ((num > 0) ? "Good" : "Text2");
		}
		modsMsg.Foreground = (Brush)mainWindow.FindResource(resourceKey);
		RefreshModsList();
	}

	private void ModRemove_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ModVm dataContext })
		{
			if (dataContext.Rift != null)
			{
				_mods.Remove(dataContext.Rift);
			}
			else
			{
				_mods.Remove(dataContext.Mod);
			}
			RefreshModsList();
		}
	}

	private async void ModUse_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: ModVm dataContext }) || _busy)
		{
			return;
		}
		if (_engine == null)
		{
			ModsMsg.Text = "Connect on the Home tab first.";
			ModsMsg.Foreground = (Brush)FindResource("Danger");
		}
		else
		{
			if (dataContext.Mod == null)
			{
				return;
			}
			ModPack mod = dataContext.Mod;
			if (mod.Manifest.Base.Length > 0)
			{
				ShowPage(Page.Outfits);
				_cat = Cat.Outfit;
				CatOutfit.IsChecked = true;
				ApplyFilter();
				_owned = (_wanted = null);
				_pendingMod = mod;
				OpenOwnedPicker();
				return;
			}
			if (IsItemMod(mod, out var kind))
			{
				StartItemPick(mod, kind);
				return;
			}
			string[] buildMats = null;
			if (mod.Manifest.FnBuilds != null)
			{
				string text = AskBuildMaterial();
				if (text == null)
				{
					return;
				}
				buildMats = new string[1] { text };
			}
			if (!ConfirmEditFiles())
			{
				return;
			}
			SetBusy(busy: true);
			try
			{
				ReleaseGameReader();
				SwappedItem item = new SwappedItem
				{
					Kind = "Mod",
					SourceId = "mod:" + mod.Key,
					SourceName = mod.Manifest.Name,
					TargetName = ((mod.Manifest.FnBuilds != null) ? "build blueprints" : "custom assets"),
					ModKey = mod.Key,
					ConvertedAt = DateTime.Now,
					TargetStyle = ((buildMats != null && buildMats.Length > 0) ? buildMats[0] : "")
				};
				if (buildMats != null)
				{
					item.TargetName = "Build blueprints";
					item.SourceIcon = BuildLookIconUri(mod);
					item.TargetIcon = BuildMaterialWindow.IconUri(buildMats[0]);
				}
				await Task.Run(() =>
				{
					ApplyModPackages(mod, item, buildMats);
				});
				if (item.Parts.Count == 0)
				{
					throw new InvalidOperationException("Nothing was applied.");
				}
				_swapped.Add(item);
				ModsMsg.Text = ((mod.Manifest.FnBuilds != null) ? $"Applied '{mod.Manifest.Name}' on {item.TargetStyle} ({item.Parts.Count} picture(s)). It only shows while you build in a match or Creative, and Fortnite checks game files there, so online play can kick you. Undo it when you're done." : $"Applied '{mod.Manifest.Name}' ({item.Parts.Count} asset(s)). Lobby/Locker only: revert before playing matches.");
				ModsMsg.Foreground = (Brush)FindResource("Good");
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
				ModsMsg.Text = ex.Message;
				ModsMsg.Foreground = (Brush)FindResource("Danger");
			}
			finally
			{
				SetBusy(busy: false);
				RefreshSwappedList();
			}
		}
	}

	private void ApplyModPackages(ModPack mod, SwappedItem item, IEnumerable<string>? buildMaterials = null)
	{
		SwapEngine engine = _engine;
		if (mod.Manifest.FnBuilds != null)
		{
			try
			{
				foreach (string item2 in BubbleBuilds.Apply(engine, mod, Log, buildMaterials))
				{
					item.Parts.Add(new SwappedPart
					{
						Slot = "ModBuilds",
						SourcePackage = item2,
						TargetPackage = mod.Key
					});
					item.PatchedPackages.Add(item2);
				}
				return;
			}
			catch (Exception ex)
			{
				Log("FAIL " + mod.Manifest.Name + ": " + ex.Message);
				throw;
			}
		}
		foreach (ModPackageFile package in mod.Manifest.Packages)
		{
			try
			{
				SwapResult swapResult = engine.ReplaceRaw(package.Target, mod.ReadPackageFile(package), mod.Manifest.Name);
				item.Parts.Add(new SwappedPart
				{
					Slot = "ModPackage",
					SourcePackage = swapResult.Source,
					TargetPackage = package.File
				});
				item.PatchedPackages.Add(swapResult.Source);
				Log($"OK  {package.Target}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
			}
			catch (Exception ex2)
			{
				Log("FAIL " + package.Target + ": " + ex2.Message);
				throw;
			}
		}
	}

	private static string BuildLookIconUri(ModPack mod)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DripSwapper", "previews");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, mod.Key + ".png");
			File.WriteAllBytes(text2, BubbleBuilds.PreviewPng(mod));
			return new Uri(text2).AbsoluteUri;
		}
		catch
		{
			return "";
		}
	}

	private void ReapplyModBuilds(SwappedItem item)
	{
		ModPack mod = _mods.List().FirstOrDefault((ModPack m) => string.Equals(m.Key, item.ModKey, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("The drip '" + item.ModKey + "' is no longer in your Mods list.");
		BubbleBuilds.Apply(_engine, mod, Log, (item.TargetStyle.Length <= 0) ? null : new string[1] { item.TargetStyle });
	}

	private void ReapplyModPart(SwappedItem item, SwappedPart part)
	{
		ModPack modPack = _mods.List().FirstOrDefault((ModPack m) => string.Equals(m.Key, item.ModKey, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("The drip '" + item.ModKey + "' is no longer in your Mods list.");
		ModPackageFile modPackageFile = modPack.Manifest.Packages.FirstOrDefault((ModPackageFile p) => string.Equals(p.File, part.TargetPackage, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("That asset isn't in the drip any more.");
		_engine.ReplaceRaw(modPackageFile.Target, modPack.ReadPackageFile(modPackageFile), modPack.Manifest.Name);
	}

	private bool ConfirmEditFiles()
	{
		return Dlg.Show(this, "This edits Fortnite's game files, and it only works in the lobby / Locker (your own screen).\n\nOnline matches check your game files and will kick you, and Epic's terms prohibit modifying game files, so accounts can be actioned. Use at your own risk, and don't queue a match while swaps are applied.\n\nMake sure Fortnite and the launcher's Fortnite process are closed.", "Before you apply", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK;
	}

	private async void Start_Click(object sender, RoutedEventArgs e)
	{
		if (Connected)
		{
			ShowPage(Page.Outfits);
			return;
		}
		if (PathBox.Text.Trim().Length == 0)
		{
			Log("ERROR: couldn't find Fortnite. Open 'Advanced' on this page and pick the folder FortniteGame\\Content\\Paks.");
			return;
		}
		Connect_Click(sender, e);
		await Task.CompletedTask;
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		bool dataPresent = e.Data.GetDataPresent(DataFormats.FileDrop);
		e.Effects = (dataPresent ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void Window_Drop(object sender, DragEventArgs e)
	{
		if (_retrac || !(e.Data.GetData(DataFormats.FileDrop) is string[] source))
		{
			return;
		}
		List<string> list = source.Where(IsModelFile).ToList();
		bool flag = source.Any((string f) => ModPack.IsAddonExtension(Path.GetExtension(f)) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".rift", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));
		if (list.Count > 0 && (!flag || MakeOverlay.Visibility == Visibility.Visible))
		{
			ShowPage(Page.Mods);
			OpenMakeModel(list[0]);
			return;
		}
		ShowPage(Page.Mods);
		ImportFiles(source.Where((string f) => ModPack.IsAddonExtension(Path.GetExtension(f)) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".rift", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToList());
	}

	private async void OnFirstShown(object? sender, RoutedEventArgs e)
	{
		if (_retrac)
		{
			if (PathBox.Text.Trim().Length > 0 && _rt == null && !FortniteLocator.IsFortniteRunning())
			{
				Connect_Click(this, new RoutedEventArgs());
			}
			await Task.CompletedTask;
			return;
		}
		if (!_settings.SeenWelcome)
		{
			Dlg.Show(this, "Welcome! Drip Swapper changes how a skin you own LOOKS on your own screen (lobby and Locker).\n\nQuick start: close Fortnite, press Start swapping, pick the skin you have, then the one you want, then Swap. If anything shows an error, press Fix problems on the Home tab.\n\n- It uses skins already on your PC (see the next tip so every skin and emote works).\n- Other players will not see it, and online matches will kick you while a swap is on. Stay in the lobby.\n- When you close Fortnite, your game files are put back automatically.\n- It edits game files, which Epic's terms don't allow, so there is a risk to your account. Use it at your own risk.", "Before you start", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			_settings.SeenWelcome = true;
			try
			{
				_settings.Save();
			}
			catch
			{
			}
		}
		if (!_settings.SeenEpicOptionsTip)
		{
			try
			{
				EpicTipWindow epicTipWindow = new EpicTipWindow(Color.FromRgb(244, 244, 245));
				epicTipWindow.Owner = this;
				epicTipWindow.ShowDialog();
			}
			catch (Exception ex)
			{
				Log("Couldn't show the Epic tip: " + ex.Message);
			}
			_settings.SeenEpicOptionsTip = true;
			try
			{
				_settings.Save();
			}
			catch
			{
			}
		}
		if (PathBox.Text.Trim().Length > 0 && _engine == null && !FortniteLocator.IsFortniteRunning())
		{
			Connect_Click(this, new RoutedEventArgs());
		}
		await Task.CompletedTask;
	}

	private void StartSessionWatcher()
	{
		AutoRevertBox.IsChecked = _settings.AutoRevertOnExit;
		ExperimentalBox.IsChecked = _settings.ShowExperimentalCategories;
		ApplyExperimentalVisibility();
		_watch = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3L)
		};
		_watch.Tick += async (object? _, EventArgs _) =>
		{
			await WatchTickAsync();
		};
		_watch.Start();
		UpdateBannerAsync(running: false);
	}

	private void AutoRevert_Changed(object sender, RoutedEventArgs e)
	{
		if (!IsLoaded)
		{
			return;
		}
		_settings.AutoRevertOnExit = AutoRevertBox.IsChecked == true;
		try
		{
			_settings.Save();
		}
		catch
		{
		}
	}

	private void Experimental_Changed(object sender, RoutedEventArgs e)
	{
		if (IsLoaded)
		{
			_settings.ShowExperimentalCategories = ExperimentalBox.IsChecked == true;
			try
			{
				_settings.Save();
			}
			catch
			{
			}
			ApplyExperimentalVisibility();
		}
	}

	private void ApplyExperimentalVisibility()
	{
		Visibility visibility = ((!_settings.ShowExperimentalCategories) ? Visibility.Collapsed : Visibility.Visible);
		RadioButton[] array = new RadioButton[7] { CatContrail, CatMusic, CatLoading, CatToy, CatSpray, CatWrap, CatEmoticon };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Visibility = visibility;
		}
		bool flag = visibility == Visibility.Collapsed;
		if (flag)
		{
			Cat cat = _cat;
			bool flag2 = (uint)(cat - 5) <= 6u;
			flag = flag2;
		}
		if (flag)
		{
			CatOutfit.IsChecked = true;
			Cat_Click(CatOutfit, new RoutedEventArgs());
		}
	}

	private async Task<bool> AnyPatchActiveAsync()
	{
		List<SwappedItem> items = _swapped.Items.ToList();
		if (items.Count == 0)
		{
			return false;
		}
		SwapEngine engine = _engine;
		if (engine == null)
		{
			return true;
		}
		return await Task.Run(() => items.Any((SwappedItem i) => engine.IsActive(i.PatchedPackages)));
	}

	private async Task WatchTickAsync()
	{
		if (_watchBusy)
		{
			return;
		}
		_watchBusy = true;
		try
		{
			bool running = await Task.Run((Func<bool>)FortniteLocator.IsFortniteRunning);
			await UpdateBannerAsync(running);
			if (_page == Page.Home)
			{
				UpdateDashboard();
			}
			if (running)
			{
				_gameWasRunning = true;
			}
			else if (_gameWasRunning)
			{
				_gameWasRunning = false;
				bool flag = _settings.AutoRevertOnExit && _engine != null && !_busy;
				if (flag)
				{
					flag = await AnyPatchActiveAsync();
				}
				if (flag)
				{
					await AutoRevertAsync();
				}
			}
		}
		catch
		{
		}
		finally
		{
			_watchBusy = false;
		}
	}

	private async Task AutoRevertAsync()
	{
		SetBusy(busy: true);
		try
		{
			ReleaseGameReader();
			var (num, num2) = await Task.Run((Func<(int, int)>)_engine.RevertAll);
			Log($"Fortnite closed: put {num} patched entr{((num == 1) ? "y" : "ies")} back to the original game files{((num2 > 0) ? $" ({num2} skipped, files changed since)" : "")}. " + "Your swaps are saved: hit Re-apply all next time.");
		}
		catch (Exception ex)
		{
			Log("ERROR: auto-revert: " + ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
			await UpdateBannerAsync(running: false);
		}
	}

	private static Brush Hex(string hex)
	{
		return (Brush)new BrushConverter().ConvertFromString(hex);
	}

	private async Task UpdateBannerAsync(bool running)
	{
		bool flag = await AnyPatchActiveAsync();
		if (_rt != null)
		{
			if (running & flag)
			{
				SafetyText.Text = "Retrac is running with swaps applied.";
				SafetyText.Foreground = (Brush)FindResource("Danger");
				SafetyBanner.Background = (Brush)FindResource("DangerBg");
				SafetyBanner.BorderBrush = (Brush)FindResource("DangerLine");
			}
			else if (flag)
			{
				SafetyText.Text = "Swaps are set. They load when Retrac starts the game, so keep Drip Swapper open.";
				SafetyText.Foreground = (Brush)FindResource("Warn");
				SafetyBanner.Background = (Brush)FindResource("WarnBg");
				SafetyBanner.BorderBrush = (Brush)FindResource("WarnLine");
			}
			else
			{
				SafetyText.Text = "Swaps only change what you see on your own screen.";
				SafetyText.Foreground = (Brush)FindResource("Text2");
				SafetyBanner.Background = (Brush)FindResource("External");
				SafetyBanner.BorderBrush = (Brush)FindResource("Line");
			}
			return;
		}
		string text = (_settings.AutoRevertOnExit ? "Auto-revert when Fortnite closes: ON." : "Auto-revert is OFF: revert by hand before playing matches.");
		if (running & flag)
		{
			SafetyText.Text = "Fortnite is running with swaps applied. Stay in the lobby / Locker. Do NOT queue a match: the server kicks players with modified files.";
			SafetyText.Foreground = (Brush)FindResource("Danger");
			SafetyBanner.Background = (Brush)FindResource("DangerBg");
			SafetyBanner.BorderBrush = (Brush)FindResource("DangerLine");
		}
		else if (flag)
		{
			SafetyText.Text = ((_engine == null) ? "Swaps from your last session may still be applied. Connect, then 'Revert all swaps' before playing matches." : ("Swaps are applied and only change your own lobby / Locker. Online matches will kick you. " + text));
			SafetyText.Foreground = (Brush)FindResource("Warn");
			SafetyBanner.Background = (Brush)FindResource("WarnBg");
			SafetyBanner.BorderBrush = (Brush)FindResource("WarnLine");
		}
		else
		{
			SafetyText.Text = "Lobby / Locker preview tool. Swaps change only what you see; online matches check game files and will kick modified ones.";
			SafetyText.Foreground = (Brush)FindResource("Text2");
			SafetyBanner.Background = (Brush)FindResource("External");
			SafetyBanner.BorderBrush = (Brush)FindResource("Line");
		}
	}

	private static bool? ToBool(Ready r)
	{
		return r switch
		{
			Ready.Ready => (bool?)true, 
			Ready.NotReady => false, 
			_ => null, 
		};
	}

	private string PartCachePath()
	{
		return Path.Combine(AppPaths.DataDir, "parts-" + new string((_keys?.Build ?? "x").Where(char.IsLetterOrDigit).ToArray()) + ".json");
	}

	private void EnsurePartCacheLoaded()
	{
		lock (_partCache)
		{
			if (_partCacheLoaded)
			{
				return;
			}
			_partCacheLoaded = true;
			try
			{
				string path = PartCachePath();
				if (!File.Exists(path))
				{
					return;
				}
				Dictionary<string, List<string[]>> dictionary = JsonSerializer.Deserialize<Dictionary<string, List<string[]>>>(File.ReadAllText(path));
				if (dictionary == null)
				{
					return;
				}
				foreach (var (key, source) in dictionary)
				{
					_partCache[key] = source.Select((string[] p) => new CharacterPart(p[0], p[1], Convert.ToUInt64(p[2], 16))).ToList();
				}
			}
			catch
			{
			}
		}
	}

	private void SavePartCache()
	{
		try
		{
			Dictionary<string, List<string[]>> value;
			lock (_partCache)
			{
				value = _partCache.ToDictionary((KeyValuePair<string, List<CharacterPart>> kv) => kv.Key, (KeyValuePair<string, List<CharacterPart>> kv) => kv.Value.Select((CharacterPart p) => new string[3]
				{
					p.Package,
					p.Slot,
					p.ChunkId.ToString("X")
				}).ToList());
			}
			string path = PartCachePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, JsonSerializer.Serialize(value));
		}
		catch
		{
		}
	}

	private List<CharacterPart> ResolveCached(PartResolver resolver, string cosmeticId)
	{
		EnsurePartCacheLoaded();
		lock (_partCache)
		{
			if (_partCache.TryGetValue(cosmeticId, out List<CharacterPart> value))
			{
				return value;
			}
		}
		List<CharacterPart> list = resolver.Resolve(cosmeticId);
		lock (_partCache)
		{
			_partCache[cosmeticId] = list;
			return list;
		}
	}

	private string CasStamp()
	{
		try
		{
			string path = CasIndex.DefaultDirectory();
			FileInfo fileInfo = new FileInfo(Path.Combine(path, "cas.snp"));
			FileInfo fileInfo2 = new FileInfo(Path.Combine(path, "cas.jrn"));
			return $"{(fileInfo.Exists ? (fileInfo.LastWriteTimeUtc.Ticks + fileInfo.Length) : 0)}/{(fileInfo2.Exists ? (fileInfo2.LastWriteTimeUtc.Ticks + fileInfo2.Length) : 0)}";
		}
		catch
		{
			return "";
		}
	}

	private async Task<OnDemandIndex?> GetOnDemandAsync(bool force = false)
	{
		if (_paks == null)
		{
			return null;
		}
		string paks = _paks;
		string stamp = CasStamp();
		try
		{
			if (!force && _onDemand != null)
			{
				if (stamp != _onDemandStamp)
				{
					await Task.Run(() =>
					{
						_onDemand.ReloadCas();
					});
					_onDemandStamp = stamp;
				}
				return _onDemand;
			}
			Log("Reading Fortnite's download index (a few seconds)...");
			OnDemandIndex onDemandIndex = (_onDemand = await Task.Run(() => OnDemandIndex.Load(paks, null, Log)));
			_onDemandStamp = stamp;
			Log(onDemandIndex.Usable ? "Download index ready." : "Couldn't line up Fortnite's download index, so downloaded/not-downloaded can't be told.");
			return onDemandIndex;
		}
		catch (Exception ex)
		{
			Log("Download index failed: " + ex.Message);
			return null;
		}
	}

	private static ReadyInfo Evaluate(OnDemandIndex idx, List<CharacterPart> parts, string name)
	{
		if (parts.Count == 0)
		{
			return new ReadyInfo(Ready.Unknown, "Couldn't find " + name + "'s character parts, so I can't tell whether they're downloaded.", new List<string>());
		}
		List<string> list = new List<string>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (CharacterPart part in parts)
		{
			if (part.ChunkId == 0L)
			{
				num++;
				switch (idx.CheckPackage(PackageNames.ChunkId(part.Package)))
				{
				case ChunkState.Installed:
					num2++;
					break;
				case ChunkState.NotInstalled:
					list.Add(part.Package.Substring(part.Package.LastIndexOf('/') + 1));
					break;
				default:
					num3++;
					break;
				}
			}
		}
		if (num == 0)
		{
			return new ReadyInfo(Ready.Ready, name + "'s character parts are stored in your game files, so a swap to it works in matches.", list);
		}
		if (list.Count > 0)
		{
			return new ReadyInfo(Ready.NotReady, $"{name}'s character parts aren't downloaded on this PC ({list.Count} of {num} missing). You can still swap to it: the game streams the parts itself when it needs them, " + "so it may take a moment to load in the Locker. If it ever fails to load, preview the skin once in your Locker so Fortnite downloads it, then try again.", list);
		}
		if (num3 == 0)
		{
			return new ReadyInfo(Ready.Ready, name + "'s character parts are downloaded on this PC.", list);
		}
		return new ReadyInfo(Ready.Unknown, $"Couldn't tell whether all of {name}'s parts are downloaded ({num3} of {num} unclear).", list);
	}

	private (List<StyleRename> Renames, string Note) ResolveStyleCached(PartResolver resolver, string cosmeticId, string tag)
	{
		string key = cosmeticId + "|" + tag;
		lock (_styleCache)
		{
			if (_styleCache.TryGetValue(key, out (List<StyleRename>, string) value))
			{
				return value;
			}
		}
		(List<StyleRename>, string) tuple = resolver.ResolveStyle(cosmeticId, tag);
		lock (_styleCache)
		{
			_styleCache[key] = tuple;
			return tuple;
		}
	}

	private async Task<ReadyInfo> CheckReadyAsync(Cosmetic target, StyleVm? style = null)
	{
		OnDemandIndex idx = await GetOnDemandAsync();
		if (idx == null || !idx.Usable)
		{
			return new ReadyInfo(Ready.Unknown, "Couldn't read Fortnite's download index on this PC, so I can't tell whether this skin is downloaded.", new List<string>());
		}
		PartResolver resolver = await EnsureResolverAsync();
		string styleNote = "";
		List<CharacterPart> parts;
		try
		{
			parts = await Task.Run(() => ResolveCached(resolver, target.Id));
			if (style != null && !style.IsDefault && style.Tag.Length > 0)
			{
				List<StyleRename> renames = (await Task.Run(() => ResolveStyleCached(resolver, target.Id, style.Tag))).Item1;
				if (renames.Count == 0)
				{
					int mats = 0;
					try
					{
						mats = (await Task.Run(() => resolver.ResolveStyleMaterials(target.Id, style.Tag))).Item1.Count;
					}
					catch
					{
					}
					lock (_styleCache)
					{
						_matStyleCount[target.Id + "|" + style.Tag] = mats;
					}
					if (mats == 0)
					{
						styleNote = StyleNotApplied(style.Name);
					}
				}
				parts = parts.Select((CharacterPart p) =>
				{
					StyleRename styleRename = renames.FirstOrDefault((StyleRename r) => r.OldPackage.Equals(p.Package, StringComparison.OrdinalIgnoreCase));
					return ((object)styleRename == null) ? p : new CharacterPart(styleRename.NewPackage, p.Slot, 0uL);
				}).ToList();
			}
		}
		catch (Exception ex)
		{
			return new ReadyInfo(Ready.Unknown, ex.Message, new List<string>());
		}
		ReadyInfo readyInfo = Evaluate(idx, parts, (style != null && !style.IsDefault) ? (target.Name + " (" + style.Name + ")") : target.Name);
		return (styleNote.Length == 0) ? readyInfo : readyInfo with
		{
			Detail = styleNote + " " + readyInfo.Detail
		};
	}

	private static string StyleNotApplied(string styleName)
	{
		return "Heads up: \"" + styleName + "\" only changes materials, which this app can't apply yet, so you'd get the default look instead.";
	}

	private string LockerWarning()
	{
		if (_cat != Cat.Outfit || _owned == null || _wanted == null || !_owned.Item.HasStyles)
		{
			return "";
		}
		List<string> source = (from s in _owned.Item.Styles
			select s.Channel into c
			where c.Length > 0
			select c).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		HashSet<string> tgt = (_wanted.Item.Styles ?? Array.Empty<CosmeticStyle>()).Select((CosmeticStyle s) => s.Channel).ToHashSet(StringComparer.OrdinalIgnoreCase);
		List<string> list = source.Where((string c) => !tgt.Contains(c)).ToList();
		if (list.Count == 0)
		{
			return "";
		}
		return $"Heads up: {_owned.Name} has style channel(s) ({string.Join(", ", list)}) that {_wanted.Name} doesn't support, so Fortnite may hide {_owned.Name} in your Locker while this swap is on. Revert brings it back; skins without styles don't have this problem.";
	}

	private void ShowReadyNote()
	{
		ReadyNote.Text = "";
		LockerNote.Text = "";
		LockerNote.Visibility = Visibility.Collapsed;
	}

	private Task<bool> ConfirmTargetInstalledAsync(CosmeticVm tgt, StyleVm? style)
	{
		_streamNote = "";
		return Task.FromResult(result: true);
	}

	private FortniteLoadout? CurrentLoadout()
	{
		if (DateTime.UtcNow - _loadoutRead > TimeSpan.FromSeconds(5L))
		{
			_loadout = FortniteLoadout.ReadLatest();
			_loadoutRead = DateTime.UtcNow;
		}
		return _loadout;
	}

	private string LoadoutSlotName()
	{
		return _cat switch
		{
			Cat.Outfit => "Character", 
			Cat.Pickaxe => "Pickaxe", 
			Cat.Glider => "Glider", 
			Cat.Emote => "Dance", 
			Cat.Contrail => "Contrail", 
			Cat.Music => "MusicPack", 
			Cat.Loading => "LoadingScreen", 
			Cat.Toy => "Toy", 
			Cat.Spray => "Spray", 
			Cat.Wrap => "ItemWrap", 
			Cat.Emoticon => "Emoji", 
			_ => "Backpack", 
		};
	}

	private string EquippedWarning()
	{
		if (_owned == null || _owned == _defaultSkin)
		{
			return "";
		}
		FortniteLoadout fortniteLoadout = CurrentLoadout();
		string text = fortniteLoadout?.Get(LoadoutSlotName());
		if (text == null)
		{
			return "";
		}
		if (fortniteLoadout.IsEquipped(LoadoutSlotName(), _owned.Id))
		{
			return "";
		}
		return $"Heads up: Fortnite's log says you last had \"{text}\" equipped, not {_owned.Name}. A swap only shows up when {_owned.Name} is the one equipped in your Locker, so equip it before you look.";
	}

	private List<RetracSwap> RetracDesired()
	{
		return Dispatcher.Invoke(() => _swapped.Items.Select((SwappedItem i) =>
		{
			string text = RetracTypeOfKind(i.Kind);
			return new RetracSwap(i.SourceId, text, i.SourceName, i.TargetId, text, i.TargetName, i.TargetStyleTag ?? "", i.TargetStyle ?? "");
		}).ToList());
	}

	private void StartRetracGuard()
	{
		_guard?.Stop(cleanUp: false);
		if (_rt == null)
		{
			return;
		}
		_guard = new RetracGuard(_rt, _paks, RetracDesired, (string msg) =>
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				Log(msg);
			}));
		}, (string title, string msg) =>
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				ShowNotice(title, msg);
			}));
		});
		_guard.Start();
	}

	private static string RetracType(Cat c)
	{
		return c switch
		{
			Cat.Pickaxe => "pickaxe", 
			Cat.Backpack => "backpack", 
			Cat.Emote => "emote", 
			Cat.Glider => "glider", 
			Cat.Contrail => "contrail", 
			Cat.Music => "music", 
			Cat.Loading => "loadingscreen", 
			Cat.Toy => "toy", 
			Cat.Spray => "spray", 
			Cat.Wrap => "wrap", 
			Cat.Emoticon => "emoji", 
			_ => "outfit", 
		};
	}

	private static string RetracTypeOfKind(string kind)
	{
		return kind switch
		{
			"Pickaxe" => "pickaxe", 
			"Back Bling" => "backpack", 
			"Emote" => "emote", 
			"Glider" => "glider", 
			"Contrail" => "contrail", 
			"Lobby Music" => "music", 
			"Loading Screen" => "loadingscreen", 
			"Toy" => "toy", 
			"Spray" => "spray", 
			"Wrap" => "wrap", 
			"Emoticon" => "emoji", 
			_ => "outfit", 
		};
	}

	private static string RetracStartPath()
	{
		try
		{
			string gamePath = RtSettings.Load().GamePath;
			if (gamePath.Length > 0 && Directory.Exists(gamePath) && !RtLocator.LooksOfficial(gamePath))
			{
				return gamePath;
			}
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "launcher.retrac.site");
			if (Directory.Exists(path))
			{
				foreach (string item in Directory.EnumerateDirectories(path))
				{
					string text = Path.Combine(item, "FortniteGame", "Content", "Paks");
					if (Directory.Exists(text) && !RtLocator.LooksOfficial(text))
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}
		return "";
	}

	private string RetracBuild()
	{
		Match match = Regex.Match(PathBox.Text, "Release-([\\d.]+)");
		if (!match.Success)
		{
			return "-";
		}
		return match.Groups[1].Value;
	}

	private async Task<bool> ConnectRetracAsync(bool showFailure)
	{
		SetBusy(busy: true);
		Dictionary<Cat, List<Cosmetic>> fetched;
		try
		{
			string paks = RtLocator.PaksFrom(PathBox.Text.Trim());
			if (paks == null)
			{
				throw new InvalidOperationException("That isn't a Retrac game folder. Pick the folder that has FortniteGame\\Content\\Paks inside it.");
			}
			if (RtLocator.LooksOfficial(paks))
			{
				throw new InvalidOperationException("That is your real Fortnite from the Epic launcher. Retrac mode never touches it. Pick the Retrac folder.");
			}
			PathBox.Text = paks;
			Log("Fetching cosmetics...");
			fetched = new Dictionary<Cat, List<Cosmetic>>();
			await Get(Cat.Outfit, new string[1] { "outfit" });
			await Get(Cat.Pickaxe, new string[1] { "pickaxe" });
			await Get(Cat.Backpack, new string[1] { "backpack" });
			await Get(Cat.Emote, new string[1] { "emote" });
			await Get(Cat.Glider, new string[1] { "glider" });
			await Get(Cat.Contrail, new string[1] { "contrail" });
			await Get(Cat.Music, new string[2] { "music", "musicpack" });
			await Get(Cat.Loading, new string[1] { "loadingscreen" });
			await Get(Cat.Toy, new string[1] { "toy" });
			await Get(Cat.Spray, new string[1] { "spray" });
			await Get(Cat.Wrap, new string[1] { "wrap" });
			await Get(Cat.Emoticon, new string[1] { "emoji" });
			Log("Reading Retrac's game files (takes a few seconds)...");
			RetracSwapper rt = new RetracSwapper(paks, AppPaths.DataDir);
			List<(string Id, string)> wanted = fetched.SelectMany((KeyValuePair<Cat, List<Cosmetic>> kv) => kv.Value.Select((Cosmetic c) => (Id: c.Id, RetracType(kv.Key)))).ToList();
			int containers = 0;
			int locked = 0;
			HashSet<string> hashSet = await Task.Run(() => rt.Available(wanted, out containers, out locked));
			if (containers == 0)
			{
				throw new InvalidOperationException("No readable game files in that folder. Is Retrac fully downloaded?");
			}
			_paks = paks;
			_rt = rt;
			_rtContainers = containers;
			StartRetracGuard();
			_resolver?.Dispose();
			_resolver = null;
			_owned = (_wanted = null);
			foreach (var (key, src) in fetched)
			{
				_lists[key] = Order(src);
			}
			foreach (List<CosmeticVm> value in _lists.Values)
			{
				foreach (CosmeticVm item in value)
				{
					item.IsFavorite = FunHub.Store.IsFavorite("fortnite", item.Id);
				}
			}
			_local = hashSet;
			ApplyAvailability();
			EmptyText.Visibility = Visibility.Collapsed;
			SearchBox.Clear();
			RefreshRarityBox();
			ApplyFilter();
			try
			{
				RtSettings rtSettings = new RtSettings();
				rtSettings.GamePath = paks;
				rtSettings.Save();
			}
			catch
			{
			}
			Log($"Retrac: {containers} game files open, {hashSet.Count} of {wanted.Count} items can be swapped" + ((locked > 0) ? $" ({locked} locked file(s) hold the newest items; those are greyed out)." : "."));
			Log("Ready. Open Pick skins, click the skin you want, then pick the one you own. Keep Drip Swapper open while you play.");
			if (_page == Page.Home)
			{
				PageSub.Text = HomeSubtitle();
			}
			UpdateDashboard();
			RefreshSwappedList();
			ShowPage(Page.Outfits);
			return true;
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			if (showFailure)
			{
				ShowNotice("Couldn't connect to Retrac", Friendly.Explain(ex.Message));
			}
			return false;
		}
		finally
		{
			SetBusy(busy: false);
			UpdateDashboard();
		}
		async Task Get(Cat cat2, params string[] apiTypes)
		{
			foreach (string t in apiTypes)
			{
				try
				{
					List<Cosmetic> list2 = await CosmeticApi.FetchAsync(t);
					fetched[cat2] = list2;
					if (list2.Count > 0)
					{
						return;
					}
				}
				catch (Exception ex2)
				{
					Log($"{KindName(cat2)} list ({t}) failed: {ex2.Message}");
					if (cat2 == Cat.Outfit)
					{
						throw;
					}
				}
			}
			fetched.TryAdd(cat2, new List<Cosmetic>());
		}
		static List<CosmeticVm> Order(IEnumerable<Cosmetic> source)
		{
			return (from c in source.OrderByDescending((Cosmetic c) => c.Added, StringComparer.Ordinal).ThenBy((Cosmetic c) => c.Name, StringComparer.OrdinalIgnoreCase)
				select new CosmeticVm(c)).ToList();
		}
	}

	private async Task ConvertRetracAsync()
	{
		if (_rt == null || _owned == null || _wanted == null)
		{
			return;
		}
		if (string.Equals(_owned.Id, _wanted.Id, StringComparison.OrdinalIgnoreCase))
		{
			SwapLog.Text = "Pick two different items.";
			return;
		}
		if (!_warned)
		{
			if (Dlg.Show(this, "Retrac's launcher re-downloads any game file that was changed, so Drip doesn't leave your swaps in the files.\n\nIt writes them the moment Retrac starts the game and puts the files back when the game closes. Keep Drip Swapper open when you press Play. It never touches your Epic Fortnite, and it only shows on your own screen.", "How this works on Retrac", MessageBoxButton.OKCancel, MessageBoxImage.Asterisk) != MessageBoxResult.OK)
			{
				return;
			}
			_warned = true;
		}
		SetBusy(busy: true);
		SwapLog.Text = "Working...";
		CosmeticVm owned = _owned;
		CosmeticVm wantedVm = _wanted;
		RetracSwapper rt = _rt;
		Cat cat = _cat;
		string text = RetracType(cat);
		StyleVm style = _style;
		string styleTag = ((style != null && !style.IsDefault) ? style.Tag : "");
		StyleVm style2 = _style;
		string styleName = ((style2 != null && !style2.IsDefault) ? style2.Name : "");
		try
		{
			RetracSwap add = new RetracSwap(owned.Id, text, owned.Name, wantedVm.Id, text, wantedVm.Name, styleTag, styleName);
			List<RetracSwap> all = (from x in RetracDesired()
				where !x.OwnedId.Equals(add.OwnedId, StringComparison.OrdinalIgnoreCase)
				select x).Append(add).ToList();
			RetracResult retracResult = await Task.Run(() => rt.Check(all));
			if (retracResult.Ok)
			{
				foreach (SwappedItem item in _swapped.Items.Where((SwappedItem i) => i.SourceId.Equals(owned.Id, StringComparison.OrdinalIgnoreCase)).ToList())
				{
					_swapped.Remove(item);
				}
				_swapped.Add(new SwappedItem
				{
					Kind = KindName(cat),
					SourceId = owned.Id,
					SourceName = owned.Name,
					SourceIcon = owned.Item.IconUrl,
					TargetId = wantedVm.Id,
					TargetName = wantedVm.Name,
					TargetIcon = wantedVm.Item.IconUrl,
					TargetStyle = styleName,
					TargetStyleTag = styleTag,
					ConvertedAt = DateTime.Now
				});
				Log($"Set up {owned.Name} -> {wantedVm.Name}. It loads when Retrac starts the game.");
				TextBlock swapLog = SwapLog;
				string text2 = $"Ready! {owned.Name} will look like {wantedVm.Name}.\nKeep Drip Swapper open, press Play in Retrac, and it loads the swap as the game starts (you may see the game pause for a few seconds). Only you see it. Press Undo to take it off.";
				string message = retracResult.Message;
				swapLog.Text = text2 + ((message != null && message.Length > 2 && message != "OK") ? ("\n\n" + message) : "");
			}
			else
			{
				Log("ERROR: " + retracResult.Message.Replace('\n', ' '));
				SwapLog.Text = retracResult.Message;
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			SwapLog.Text = "Didn't swap. Nothing was changed.\n" + Friendly.Explain(ex.Message).Split('\n')[0].Trim();
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwapStatus();
			RefreshSwappedList();
		}
	}

	private async Task RevertRetracItemAsync(SwappedItem item)
	{
		if (_rt == null)
		{
			Log("ERROR: connect first.");
			return;
		}
		SetBusy(busy: true);
		try
		{
			_swapped.Remove(item);
			await Task.CompletedTask;
			string text = "Reverted " + item.SourceName + "." + ((_rt.Active().Count > 0) ? " The game is running, so it comes out when you close it." : "");
			Log(text.Replace('\n', ' '));
			if (SwapOverlay.Visibility == Visibility.Visible)
			{
				SwapLog.Text = text;
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			if (SwapOverlay.Visibility == Visibility.Visible)
			{
				SwapLog.Text = ex.Message;
			}
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
		}
	}

	private async Task ReapplyRetracAsync()
	{
		if (_rt == null)
		{
			Log("ERROR: connect first.");
			return;
		}
		SetBusy(busy: true);
		try
		{
			RetracSwapper rt = _rt;
			List<RetracSwap> all = RetracDesired();
			RetracResult retracResult = await Task.Run(() => rt.Check(all));
			string text = (retracResult.Ok ? $"All {all.Count} swap(s) check out against Retrac's current files. They load when the game starts." : retracResult.Message);
			Log(text.Replace('\n', ' '));
			if (!retracResult.Ok)
			{
				ShowNotice("Some swaps don't fit anymore", retracResult.Message);
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
		}
	}

	private async Task RevertAllRetracAsync()
	{
		if (_rt == null)
		{
			Log("ERROR: connect first.");
			return;
		}
		SetBusy(busy: true);
		try
		{
			_swapped.Clear();
			await Task.CompletedTask;
			Log("All swaps removed." + ((_rt.Active().Count > 0) ? " The game is running, so the files go back when you close it." : ""));
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
		}
	}

	private void VerifyRetrac()
	{
		if (_rt == null)
		{
			Log("ERROR: connect first.");
			return;
		}
		int count = _swapped.Items.Count;
		ShowNotice("Swaps checked", (count == 0) ? "You have no swaps set up." : $"{count} swap(s) are set up. Retrac's files stay untouched until you press Play in Retrac; Drip writes them as the game starts and takes them out when it closes, so the launcher never sees a change. Keep Drip Swapper open. If the lobby still shows the old look, equip the item you own (the one that changes) in your Locker.");
	}

	private bool? RetracActive(SwappedItem item)
	{
		if (_rt == null)
		{
			return null;
		}
		return true;
	}

	private void HideForRetrac()
	{
		GameChipBtn.Content = "Retrac";
		Title = "Drip Swapper - Retrac";
		EmptyText.Text = "Retrac isn't connected yet. Go to Start here and wait for the green check.";
		TabMods.Visibility = Visibility.Collapsed;
		TabFun.Visibility = Visibility.Collapsed;
	}

	private static bool StrictKind(string kind)
	{
		switch (kind)
		{
		case "Emote":
		case "Spray":
		case "Contrail":
		case "Emoticon":
		case "Glider":
		case "Lobby Music":
		case "Loading Screen":
		case "Toy":
		case "Wrap":
			return true;
		default:
			return false;
		}
	}

	private static string UnsafeReason(SwapEngine engine, SwappedItem item)
	{
		foreach (SwappedPart item2 in item.Parts.Where((SwappedPart x) => x.Slot == "Definition"))
		{
			LayoutCheck layoutCheck = engine.CheckLayout(item2.SourcePackage, item2.SourceChunk, item2.TargetPackage, item2.TargetChunk);
			if (!layoutCheck.Safe)
			{
				return string.Join(", ", layoutCheck.Differences);
			}
		}
		return "";
	}

	private async Task SafetySweepAsync()
	{
		SwapEngine engine = _engine;
		if (engine == null || FortniteLocator.IsFortniteRunning())
		{
			return;
		}
		List<SwappedItem> items = _swapped.Items.Where((SwappedItem i) => StrictKind(i.Kind) && i.Parts.Any((SwappedPart p) => p.Slot == "Definition")).ToList();
		if (items.Count == 0)
		{
			return;
		}
		ReleaseGameReader();
		List<string> dropped = new List<string>();
		await Task.Run(() =>
		{
			foreach (SwappedItem item in items)
			{
				try
				{
					bool flag = engine.IsActive(item.PatchedPackages);
					if (flag)
					{
						engine.RevertPackages(item.PatchedPackages);
					}
					string text = UnsafeReason(engine, item);
					if (text.Length > 0)
					{
						_swapped.Remove(item);
						dropped.Add($"{item.SourceName} → {item.TargetName} ({item.Kind}): {text}");
						Log($"Removed unsafe {item.Kind.ToLowerInvariant()} swap {item.SourceName} → {item.TargetName}: built differently ({text}).");
					}
					else if (flag)
					{
						foreach (SwappedPart item2 in item.Parts.Where((SwappedPart x) => x.Slot == "Definition"))
						{
							engine.Swap(item2.SourcePackage, item2.TargetPackage, item2.SourceChunk, item2.TargetChunk);
						}
					}
				}
				catch (Exception ex)
				{
					Log("Safety check for " + item.SourceName + " failed: " + ex.Message);
				}
			}
		});
		RefreshSwappedList();
		if (dropped.Count > 0)
		{
			ShowNotice("Took out swaps that crash Fortnite", "These were swapped for items built differently, which makes Fortnite crash right after login. They're out of your game files now:\n\n- " + string.Join("\n- ", dropped) + "\n\nOutfit swaps weren't touched.");
		}
	}

	private static string KindName(Cat c)
	{
		return c switch
		{
			Cat.Pickaxe => "Pickaxe", 
			Cat.Backpack => "Back Bling", 
			Cat.Emote => "Emote", 
			Cat.Glider => "Glider", 
			Cat.Contrail => "Contrail", 
			Cat.Music => "Lobby Music", 
			Cat.Loading => "Loading Screen", 
			Cat.Toy => "Toy", 
			Cat.Spray => "Spray", 
			Cat.Wrap => "Wrap", 
			Cat.Emoticon => "Emoticon", 
			_ => "Outfit", 
		};
	}

	public MainWindow(bool retrac = false)
	{
		_retrac = retrac;
		_swapped = (retrac ? new SwappedStore(Path.Combine(AppPaths.DataDir, "swaps-retrac.json")) : new SwappedStore());
		InitializeComponent();
		Theme.SetGame(_retrac ? Game.Retrac : Game.Fortnite);
		Shell.Setup(this, 1040.0, 640.0);
		PathBox.Text = (_retrac ? RetracStartPath() : (FortniteLocator.FindPaksDirectory() ?? ""));
		Log((PathBox.Text.Length <= 0) ? (_retrac ? "Couldn't find Retrac automatically. Browse to its FortniteGame\\Content\\Paks folder." : "Couldn't find Fortnite automatically. Browse to FortniteGame\\Content\\Paks.") : (_retrac ? "Found Retrac. Hit Connect." : "Found Fortnite. Hit Connect."));
		if (!_retrac)
		{
			SeedPreinstalledDrips();
		}
		FunCtl.Attach("fortnite", FunHub.Store, CurrentFit, ApplyFitAsync, ShowNotice);
		ShowPage(Page.Home);
		RefreshSwappedList();
		StartSessionWatcher();
		if (_retrac)
		{
			HideForRetrac();
		}
		Loaded += OnFirstShown;
		Closed += (object? _, EventArgs _) =>
		{
			_watch?.Stop();
			_resolver?.Dispose();
			_guard?.Stop();
		};
	}

	private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (!SearchWrap.IsMouseOver && e.ButtonState == MouseButtonState.Pressed)
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

	private void Discord_Click(object sender, RoutedEventArgs e)
	{
		App.OpenDiscord();
	}

	private void SwitchGame_Click(object sender, RoutedEventArgs e)
	{
		App.OpenChooser(this);
	}

	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && (NoticeOverlay.Visibility == Visibility.Visible || OptionsOverlay.Visibility == Visibility.Visible || StyleOverlay.Visibility == Visibility.Visible || SwapOverlay.Visibility == Visibility.Visible || MakeOverlay.Visibility == Visibility.Visible))
		{
			CloseOverlays();
			e.Handled = true;
		}
	}

	private void Tab_Click(object sender, RoutedEventArgs e)
	{
		_pendingMod = null;
		if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Page>(tag, out var result))
		{
			ShowPage(result);
		}
	}

	private void ShowPage(Page page)
	{
		_page = page;
		CloseOverlays();
		HomePage.Visibility = ((page != Page.Home) ? Visibility.Collapsed : Visibility.Visible);
		OutfitsPage.Visibility = ((page != Page.Outfits) ? Visibility.Collapsed : Visibility.Visible);
		SwappedPage.Visibility = ((page != Page.Swapped) ? Visibility.Collapsed : Visibility.Visible);
		ModsPage.Visibility = ((page != Page.Mods) ? Visibility.Collapsed : Visibility.Visible);
		FunPage.Visibility = ((page != Page.Fun) ? Visibility.Collapsed : Visibility.Visible);
		SettingsPage.Visibility = ((page != Page.Settings) ? Visibility.Collapsed : Visibility.Visible);
		SearchWrap.Visibility = ((page != Page.Outfits) ? Visibility.Collapsed : Visibility.Visible);
		if (page == Page.Home)
		{
			UpdateDashboard();
		}
		TabHome.IsChecked = page == Page.Home;
		TabOutfits.IsChecked = page == Page.Outfits;
		TabSwapped.IsChecked = page == Page.Swapped;
		TabMods.IsChecked = page == Page.Mods;
		TabFun.IsChecked = page == Page.Fun;
		TabSettings.IsChecked = page == Page.Settings;
		TextBlock pageTitle = PageTitle;
		pageTitle.Text = page switch
		{
			Page.Home => "Start here", 
			Page.Outfits => "Pick skins", 
			Page.Swapped => "What's swapped", 
			Page.Mods => "Add-ons", 
			Page.Fun => "Loadouts", 
			_ => "Settings", 
		};
		pageTitle = PageSub;
		pageTitle.Text = page switch
		{
			Page.Home => HomeSubtitle(), 
			Page.Outfits => "Click the skin you want, then pick the one you own.", 
			Page.Swapped => "Everything that looks different right now.", 
			Page.Mods => "Skin packs and recipes from other people.", 
			Page.Fun => "Save what's swapped and bring it back in one click.", 
			_ => "", 
		};
		if (page == Page.Swapped)
		{
			RefreshSwappedList();
		}
		if (page == Page.Mods)
		{
			RefreshModsList();
		}
		if (page == Page.Fun)
		{
			FunCtl.Refresh();
		}
	}

	private void Diagnostics_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = DiagnosticsHelper.Save("Fortnite");
			ShowNotice("Diagnostics saved", "Saved to your Downloads folder:\n" + text + "\n\nSend that file when something doesn't work.");
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't save the file", ex.Message);
		}
	}

	private void ShowNotice(string title, string text)
	{
		NoticeTitle.Text = title;
		NoticeText.Text = text;
		NoticeOverlay.Visibility = Visibility.Visible;
	}

	private void CloseOverlays()
	{
		NoticeOverlay.Visibility = Visibility.Collapsed;
		OptionsOverlay.Visibility = Visibility.Collapsed;
		StyleOverlay.Visibility = Visibility.Collapsed;
		SwapOverlay.Visibility = Visibility.Collapsed;
		MakeOverlay.Visibility = Visibility.Collapsed;
	}

	private void CloseOverlays_Click(object sender, RoutedEventArgs e)
	{
		CloseOverlays();
	}

	private void Log(string message)
	{
		WriteLogFile(message);
		if (Dispatcher.CheckAccess())
		{
			Append();
		}
		else
		{
			Dispatcher.BeginInvoke(new Action(Append));
		}
		void Append()
		{
			LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
			LogBox.ScrollToEnd();
			ConnectMsg.Text = message.Trim();
			ConnectMsg.Foreground = (Brush)FindResource(message.StartsWith("ERROR", StringComparison.Ordinal) ? "Danger" : "Text2");
			if (SwapOverlay.Visibility == Visibility.Visible)
			{
				SwapLog.Text = message.Trim();
			}
		}
	}

	private static void WriteLogFile(string message)
	{
		try
		{
			lock (LogFileLock)
			{
				string text = Path.Combine(AppPaths.DataDir, "activity.log");
				Directory.CreateDirectory(AppPaths.DataDir);
				if (File.Exists(text) && new FileInfo(text).Length > 1000000)
				{
					string text2 = text + ".old";
					try
					{
						if (File.Exists(text2))
						{
							File.Delete(text2);
						}
						File.Move(text, text2);
					}
					catch
					{
					}
				}
				File.AppendAllText(text, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		BusyBar.Visibility = ((!busy) ? Visibility.Hidden : Visibility.Visible);
		ConnectBtn.IsEnabled = !busy;
		SwapConvertBtn.IsEnabled = !busy;
		SwapRevertBtn.IsEnabled = !busy;
		ReapplyBtn.IsEnabled = !busy;
		RevertAllBtn.IsEnabled = !busy;
	}

	private static bool Matches(Cosmetic c, string q)
	{
		if (q.Length != 0 && !c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
		{
			return c.Id.Contains(q, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private void UpdateDashboard()
	{
		bool connected = Connected;
		StatStatus.Text = (connected ? "Connected" : "Not connected");
		StatStatus.Foreground = (Brush)FindResource(connected ? "Good" : "Danger");
		TextBlock statBuild = StatBuild;
		string text;
		if (_retrac)
		{
			text = (connected ? RetracBuild() : "-");
		}
		else
		{
			text = ((connected && _keys != null) ? _keys.Build.Replace("++Fortnite+Release-", "") : "-");
		}
		statBuild.Text = text;
		TextBlock statContainers = StatContainers;
		string text2;
		if (connected)
		{
			text2 = (_retrac ? _rtContainers.ToString() : _engine.Containers.Count.ToString());
		}
		else
		{
			text2 = "-";
		}
		statContainers.Text = text2;
		StatSwaps.Text = _swapped.Items.Count.ToString();
		if (connected && _keys != null && !_retrac)
		{
			BannerSub.Text = _keys.Build;
		}
		bool flag = PathBox.Text.Trim().Length > 0;
		bool flag2 = false;
		try
		{
			flag2 = FortniteLocator.IsFortniteRunning();
		}
		catch
		{
		}
		StepFound.Text = Dot(flag) + " " + (_retrac ? "Retrac" : "Fortnite") + " found";
		StepConnected.Text = Dot(connected) + " Connected";
		StepClosed.Text = (flag2 ? "○ Fortnite is open: close it before you swap" : "✓ Fortnite is closed");
		StepFound.Foreground = (Brush)FindResource(flag ? "Good" : "Danger");
		StepConnected.Foreground = (Brush)FindResource(connected ? "Good" : "Danger");
		StepClosed.Foreground = (Brush)FindResource(flag2 ? "Danger" : "Good");
		static string Dot(bool ok)
		{
			if (!ok)
			{
				return "○";
			}
			return "✓";
		}
	}

	private void Browse_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = (_retrac ? "Select Retrac's FortniteGame\\Content\\Paks" : "Select FortniteGame\\Content\\Paks")
		};
		if (openFolderDialog.ShowDialog() == true)
		{
			PathBox.Text = openFolderDialog.FolderName;
		}
	}

	private string HomeSubtitle()
	{
		if (!_retrac)
		{
			AesKeys keys = _keys;
			if (keys != null)
			{
				string version = keys.Version;
				if (version != null && version.Length > 0)
				{
					return "Lobby and Locker swaps for Fortnite " + keys.Version + ".";
				}
			}
			return "Swap skins in your lobby and Locker.";
		}
		if (!Connected)
		{
			return "Swap skins in your lobby and Locker.";
		}
		return "Lobby and Locker swaps for Retrac (Fortnite " + RetracBuild() + ").";
	}

	private async void Connect_Click(object sender, RoutedEventArgs e)
	{
		await ConnectAsync(showFailure: true);
	}

	private async Task<bool> ConnectAsync(bool showFailure)
	{
		if (_retrac)
		{
			return await ConnectRetracAsync(showFailure);
		}
		SetBusy(busy: true);
		try
		{
			string paks = PathBox.Text.Trim();
			FortniteLocator.Validate(paks);
			Log("Fetching AES keys...");
			AesKeys keys = await AesKeys.FetchAsync();
			Log($"Build {keys.Build} · {keys.Count} keys.");
			if (keys.Count == 0)
			{
				throw new InvalidOperationException("The AES endpoint returned no keys.");
			}
			SwapEngine engine = new SwapEngine(paks, keys, _state, Log);
			Log("Mounting containers (reads every .utoc, takes a bit)...");
			await Task.Run((Action)engine.Load);
			Log("Fetching cosmetics (outfits, pickaxes, back blings, emotes, gliders, contrails, lobby music, loading screens, toys, sprays, wraps, emoticons)...");
			List<Cosmetic> outfits = await CosmeticApi.FetchAsync("outfit");
			// The empty Locker slot is the special item CID_DefaultOutfit (its look comes from style options), so it goes first.
			Cosmetic defaultOutfit = outfits.FirstOrDefault(IsDefaultOutfit) ?? new Cosmetic(DefaultOutfitId, "Default", "Common", "", "", "0");
			_defaultVariants = new List<Cosmetic> { defaultOutfit };
			_defaultVariants.AddRange(outfits.Where(IsAccountDefault));
			Cosmetic? defaultIcon = _defaultVariants.FirstOrDefault((Cosmetic c) => c.Id.StartsWith("CID_560", StringComparison.OrdinalIgnoreCase)) ?? _defaultVariants.FirstOrDefault((Cosmetic c) => c.IconUrl.Length > 0);
			_defaultSkin = ((defaultIcon == null) ? null : new CosmeticVm(new Cosmetic(DefaultSkinId, "Default Skin", "Common", defaultIcon.IconUrl, "", "0")));
			outfits = outfits.Where((Cosmetic c) => !IsRecruit(c) && !IsDefaultOutfit(c)).ToList();
			List<Cosmetic> pickaxes = new List<Cosmetic>();
			List<Cosmetic> backs = new List<Cosmetic>();
			try
			{
				pickaxes = await CosmeticApi.FetchAsync("pickaxe");
			}
			catch (Exception ex)
			{
				Log("Pickaxe list failed: " + ex.Message);
			}
			try
			{
				backs = await CosmeticApi.FetchAsync("backpack");
			}
			catch (Exception ex2)
			{
				Log("Back bling list failed: " + ex2.Message);
			}
			List<Cosmetic> emotes = new List<Cosmetic>();
			List<Cosmetic> gliders = new List<Cosmetic>();
			List<Cosmetic> contrails = new List<Cosmetic>();
			try
			{
				emotes = await CosmeticApi.FetchAsync("emote");
			}
			catch (Exception ex3)
			{
				Log("Emote list failed: " + ex3.Message);
			}
			try
			{
				gliders = await CosmeticApi.FetchAsync("glider");
			}
			catch (Exception ex4)
			{
				Log("Glider list failed: " + ex4.Message);
			}
			try
			{
				contrails = await CosmeticApi.FetchAsync("contrail");
			}
			catch (Exception ex5)
			{
				Log("Contrail list failed: " + ex5.Message);
			}
			List<Cosmetic> music = new List<Cosmetic>();
			List<Cosmetic> loadings = new List<Cosmetic>();
			List<Cosmetic> toys = new List<Cosmetic>();
			List<Cosmetic> sprays = new List<Cosmetic>();
			string[] array = new string[2] { "music", "musicpack" };
			foreach (string mt in array)
			{
				try
				{
					music = await CosmeticApi.FetchAsync(mt);
					if (music.Count > 0)
					{
						break;
					}
				}
				catch (Exception ex6)
				{
					Log("Lobby music list (" + mt + ") failed: " + ex6.Message);
				}
			}
			try
			{
				loadings = await CosmeticApi.FetchAsync("loadingscreen");
			}
			catch (Exception ex7)
			{
				Log("Loading screen list failed: " + ex7.Message);
			}
			try
			{
				toys = await CosmeticApi.FetchAsync("toy");
			}
			catch (Exception ex8)
			{
				Log("Toy list failed: " + ex8.Message);
			}
			try
			{
				sprays = await CosmeticApi.FetchAsync("spray");
			}
			catch (Exception ex9)
			{
				Log("Spray list failed: " + ex9.Message);
			}
			List<Cosmetic> wraps = new List<Cosmetic>();
			List<Cosmetic> emoticons = new List<Cosmetic>();
			try
			{
				wraps = await CosmeticApi.FetchAsync("wrap");
			}
			catch (Exception ex10)
			{
				Log("Wrap list failed: " + ex10.Message);
			}
			try
			{
				emoticons = await CosmeticApi.FetchAsync("emoji");
			}
			catch (Exception ex11)
			{
				Log("Emoticon list failed: " + ex11.Message);
			}
			_paks = paks;
			_keys = keys;
			_engine = engine;
			double? versionNumber = keys.VersionNumber;
			if (versionNumber.HasValue)
			{
				double valueOrDefault = versionNumber.GetValueOrDefault();
				RiftPack.CurrentBuild = valueOrDefault;
			}
			if (_page == Page.Home)
			{
				PageSub.Text = HomeSubtitle();
			}
			_resolver?.Dispose();
			_resolver = null;
			_partCache.Clear();
			_onDemand = null;
			_partCacheLoaded = false;
			_owned = (_wanted = null);
			_lists[Cat.Outfit] = Order(outfits);
			_lists[Cat.Pickaxe] = Order(pickaxes);
			_lists[Cat.Backpack] = Order(backs);
			_lists[Cat.Emote] = Order(emotes);
			_lists[Cat.Glider] = Order(gliders);
			_lists[Cat.Contrail] = Order(contrails);
			_lists[Cat.Music] = Order(music);
			_lists[Cat.Loading] = Order(loadings);
			_lists[Cat.Toy] = Order(toys);
			_lists[Cat.Spray] = Order(sprays);
			_lists[Cat.Wrap] = Order(wraps);
			_lists[Cat.Emoticon] = Order(emoticons);
			Log($"Lists: {outfits.Count} outfits, {pickaxes.Count} pickaxes, {backs.Count} back blings, {emotes.Count} emotes, {gliders.Count} gliders, {contrails.Count} contrails, {music.Count} lobby music, {loadings.Count} loading screens, {toys.Count} toys, {sprays.Count} sprays, {wraps.Count} wraps, {emoticons.Count} emoticons.");
			foreach (List<CosmeticVm> value in _lists.Values)
			{
				foreach (CosmeticVm item in value)
				{
					item.IsFavorite = FunHub.Store.IsFavorite("fortnite", item.Id);
				}
			}
			_local = await LoadLocalIndexAsync(keys);
			ApplyAvailability();
			EmptyText.Visibility = Visibility.Collapsed;
			SearchBox.Clear();
			RefreshRarityBox();
			ApplyFilter();
			Log("Ready. Open Pick skins, click the skin you want, then pick the one you own.");
			UpdateDashboard();
			RefreshSwappedList();
			ShowPage(Page.Outfits);
			await SafetySweepAsync();
			return true;
		}
		catch (Exception ex12)
		{
			Log("ERROR: " + ex12.Message);
			if (showFailure)
			{
				ShowNotice("Couldn't connect to Fortnite", Friendly.Explain(ex12.Message));
			}
			return false;
		}
		finally
		{
			SetBusy(busy: false);
			UpdateDashboard();
		}
		static List<CosmeticVm> Order(IEnumerable<Cosmetic> src)
		{
			return (from c in src.OrderByDescending((Cosmetic c) => c.Added, StringComparer.Ordinal).ThenBy((Cosmetic c) => c.Name, StringComparer.OrdinalIgnoreCase)
				select new CosmeticVm(c)).ToList();
		}
	}

	private async void FixProblems_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		if (await Task.Run((Func<bool>)FortniteLocator.IsFortniteRunning))
		{
			ShowNotice("Close Fortnite first", Friendly.Explain("Close Fortnite before swapping."));
			return;
		}
		Log("Fix problems: re-reading the game files...");
		ReleaseGameReader();
		if (!(await ConnectAsync(showFailure: true)))
		{
			return;
		}
		ShowPage(Page.Home);
		if (_retrac)
		{
			ShowNotice("All checked", $"Re-read Retrac's files: {_rtContainers} containers open.\n\n" + ((_swapped.Items.Count == 0) ? "You have no swaps set up." : $"{_swapped.Items.Count} swap(s) are set up and load when Retrac starts the game."));
			return;
		}
		SwapEngine engine = _engine;
		List<string> list = new List<string> { $"Re-read your game files: {engine.Containers.Count} containers open." };
		if (engine.Skipped.Count > 0)
		{
			list.Add($"{engine.Skipped.Count} file(s) couldn't be opened (usually the Epic launcher is still updating Fortnite). Wait for it to finish and press Fix problems again.");
		}
		List<SwappedItem> list2 = _swapped.Items.Where((SwappedItem i) => i.PatchedPackages.Count > 0 && !engine.IsActive(i.PatchedPackages)).ToList();
		if (_swapped.Items.Count == 0)
		{
			list.Add("You have no swaps on right now.");
		}
		else if (list2.Count == 0)
		{
			list.Add($"All {_swapped.Items.Count} of your swaps are in place.");
		}
		else
		{
			list.Add($"{list2.Count} of your {_swapped.Items.Count} swaps are no longer in the game files (Epic probably repaired or updated them). Go to the Swapped tab and press Redo all swaps.");
		}
		list.Add("Now try your swap again.");
		ShowNotice("All checked", string.Join("\n\n", list));
	}

	private string IndexPath(AesKeys keys)
	{
		return Path.Combine(AppPaths.DataDir, "local-cosmetics-" + new string(keys.Build.Where(char.IsLetterOrDigit).ToArray()) + ".txt");
	}

	private async Task<HashSet<string>?> LoadLocalIndexAsync(AesKeys keys)
	{
		_ = 3;
		try
		{
			string file = IndexPath(keys);
			if (File.Exists(file))
			{
				return new HashSet<string>(await File.ReadAllLinesAsync(file), StringComparer.OrdinalIgnoreCase);
			}
			PartResolver partResolver = await EnsureResolverAsync();
			Log("Indexing which cosmetics your install actually has (one time per game build)...");
			HashSet<string> names = await Task.Run((Func<HashSet<string>>)partResolver.LocalCosmeticNames);
			Directory.CreateDirectory(Path.GetDirectoryName(file));
			await File.WriteAllLinesAsync(file, names);
			Log($"{names.Count} cosmetic definitions are available locally.");
			return names;
		}
		catch (Exception ex)
		{
			Log("Couldn't index local cosmetics (all skins will be offered): " + ex.Message);
			return null;
		}
	}

	private void ApplyAvailability()
	{
		foreach (List<CosmeticVm> value in _lists.Values)
		{
			foreach (CosmeticVm item in value)
			{
				item.Available = _local == null || _local.Contains(item.Id);
			}
		}
	}

	private static int RarityRank(string r)
	{
		int num = Array.FindIndex(RarityOrder, (string x) => r.Contains(x, StringComparison.OrdinalIgnoreCase));
		if (num >= 0)
		{
			return num;
		}
		return RarityOrder.Length;
	}

	private void RefreshRarityBox()
	{
		if (RarityBox != null)
		{
			string text = (RarityBox.SelectedItem as string) ?? "All rarities";
			_rarityBusy = true;
			List<string> list = (from c in _all
				select c.Rarity into r
				where r.Length > 0
				select r).Distinct().OrderBy(RarityRank).ThenBy((string r) => r)
				.ToList();
			list.Insert(0, "All rarities");
			RarityBox.ItemsSource = list;
			RarityBox.SelectedItem = (list.Contains(text) ? text : list[0]);
			_rarityBusy = false;
		}
	}

	private void Sort_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (IsLoaded && !_rarityBusy && _all.Count != 0)
		{
			ApplyFilter();
		}
	}

	private void ApplyFilter()
	{
		string q = SearchBox.Text.Trim();
		bool favOnly = FavOnlyBox.IsChecked == true;
		string rarity = (RarityBox?.SelectedItem as string) ?? "All rarities";
		bool onlyReady = GridReadyOnly?.IsChecked.GetValueOrDefault() ?? false;
		List<CosmeticVm> list = _all.Where((CosmeticVm c) => Matches(c.Item, q) && (!favOnly || c.IsFavorite) && (rarity == "All rarities" || c.Rarity == rarity) && (!onlyReady || ((_cat != Cat.Emote) ? (_cat != Cat.Outfit || c.Downloaded == true) : (c.AnimReady == true)))).ToList();
		switch (SortBox?.SelectedIndex ?? 0)
		{
		case 0:
		{
			if (q.Length != 0)
			{
				break;
			}
			Dictionary<string, CosmeticVm> byId = new Dictionary<string, CosmeticVm>(StringComparer.OrdinalIgnoreCase);
			foreach (CosmeticVm item in list)
			{
				byId.TryAdd(item.Id, item);
			}
			list = (from c in CosmeticRank.Rank(CatKey(_cat), list.Select((CosmeticVm m) => m.Item).ToList())
				select byId[c.Id]).ToList();
			break;
		}
		case 2:
			list = list.OrderBy((CosmeticVm c) => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
			break;
		}
		GridHost.ItemsSource = list.Take(300).ToList();
		CountText.Text = ((list.Count > 300) ? $"Showing {300} of {list.Count} · search or filter to find others" : $"{list.Count} {KindName(_cat).ToLowerInvariant()}{((list.Count == 1) ? "" : "s")}");
		try
		{
			string eq = CurrentLoadout()?.Get(LoadoutSlotName());
			CosmeticVm cosmeticVm = ((eq == null) ? null : _all.FirstOrDefault((CosmeticVm c) => string.Equals(c.Id, eq, StringComparison.OrdinalIgnoreCase)));
			if (cosmeticVm != null)
			{
				TextBlock countText = CountText;
				countText.Text = countText.Text + " · equipped now: " + cosmeticVm.Name;
			}
		}
		catch
		{
		}
	}

	private static string CatKey(Cat c)
	{
		return c.ToString();
	}

	private void GridReady_Changed(object sender, RoutedEventArgs e)
	{
		if (IsLoaded && _all.Count > 0)
		{
			ApplyFilter();
		}
	}

	private void Search_Changed(object sender, TextChangedEventArgs e)
	{
		SearchHint.Visibility = ((SearchBox.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		if (_all.Count > 0)
		{
			ApplyFilter();
		}
	}

	private void Cat_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Cat>(tag, out var result) && result != _cat)
		{
			_cat = result;
			_pendingMod = null;
			_owned = (_wanted = null);
			SearchBox.Clear();
			RefreshRarityBox();
			ApplyFilter();
			if (result == Cat.Emote)
			{
				StartEmoteScan();
			}
		}
	}

	private void OptSearch_Changed(object sender, TextChangedEventArgs e)
	{
		ApplyOptionFilter();
	}

	private List<CosmeticVm> OptionCandidates()
	{
		string q = OptSearch.Text.Trim();
		List<CosmeticVm> list = _all.Where((CosmeticVm c) => c != _wanted && c.Available && Matches(c.Item, q)).ToList();
		if (!_retrac && _cat == Cat.Outfit && _pendingMod == null && _defaultSkin != null && _wanted != _defaultSkin && (q.Length == 0 || Matches(_defaultSkin.Item, q) || "recruit".Contains(q, StringComparison.OrdinalIgnoreCase)))
		{
			list.Insert(0, _defaultSkin);
		}
		return list;
	}

	private void ApplyOptionFilter()
	{
		string equipped = null;
		try
		{
			equipped = CurrentLoadout()?.Get(LoadoutSlotName());
		}
		catch
		{
		}
		OptHost.ItemsSource = (from c in OptionCandidates()
			orderby (equipped == null || !string.Equals(c.Id, equipped, StringComparison.OrdinalIgnoreCase)) ? 1 : 0, (!c.IsFavorite) ? 1 : 0, (!c.Id.Contains("Default", StringComparison.OrdinalIgnoreCase)) ? 1 : 0
			select c).ThenBy((CosmeticVm c) => c.Item.Added, StringComparer.Ordinal).ThenBy((CosmeticVm c) => c.Name, StringComparer.OrdinalIgnoreCase).Take(300)
			.ToList();
	}

	private void FavOnly_Changed(object sender, RoutedEventArgs e)
	{
		if (IsLoaded && _all.Count > 0)
		{
			ApplyFilter();
		}
	}

	private void Fav_RightClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: CosmeticVm dataContext })
		{
			dataContext.IsFavorite = FunHub.Store.ToggleFavorite("fortnite", dataContext.Id);
			if (FavOnlyBox.IsChecked == true)
			{
				ApplyFilter();
			}
			if (OptionsOverlay.Visibility == Visibility.Visible)
			{
				ApplyOptionFilter();
			}
			e.Handled = true;
		}
	}

	private void GridIcon_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: CosmeticVm dataContext })
		{
			PickWanted(dataContext);
		}
	}

	private void PickWanted(CosmeticVm vm)
	{
		if (!vm.Available)
		{
			ShowNotice(vm.Name + " can't be used", vm.Name + " isn't in your local game files. Fortnite streams it on demand, so it isn't on this PC.\n\nOnly skins that are installed on your PC can be swapped. Greyed-out skins are the ones that aren't.");
			return;
		}
		_pendingMod = null;
		_wanted = vm;
		_owned = null;
		_ownedStyle = null;
		_style = null;
		if (vm.Item.HasStyles && _cat == Cat.Outfit)
		{
			ShowStylePicker(vm, StyleMode.Target, "What style would you like?", $"{vm.Name} has {vm.Item.Styles.Count} style(s). Pick the one you want.");
		}
		else
		{
			OpenOwnedPicker();
		}
	}

	private void OpenOwnedPicker()
	{
		if (_wanted != null || _pendingMod != null)
		{
			ModPack pendingMod = _pendingMod;
			if (pendingMod != null)
			{
				OptionsTitle.Text = (IsItemMod(pendingMod, out var kind) ? ("Which " + FnItem.Label(kind) + " do you own?") : "Which skin do you own?");
				OptionsSub.Text = "'" + pendingMod.Manifest.Name + "' will be written over the one you pick.";
			}
			else
			{
				OptionsTitle.Text = "Which one do you own?";
				OptionsSub.Text = $"Looking like {_wanted.Name}{((_style == null) ? "" : (" (" + _style.Name + ")"))} — pick the {KindName(_cat).ToLowerInvariant()} you own and want to swap it onto.";
			}
			OptSearch.Clear();
			ApplyOptionFilter();
			OptionsOverlay.Visibility = Visibility.Visible;
			OptSearch.Focus();
		}
	}

	private void OptionIcon_Click(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: CosmeticVm dataContext }))
		{
			return;
		}
		_owned = dataContext;
		_ownedStyle = null;
		OptionsOverlay.Visibility = Visibility.Collapsed;
		if (IsItemMod(_pendingMod, out var kind))
		{
			if (_cat != CatOf(kind))
			{
				ShowNotice("Wrong list", $"'{_pendingMod.Manifest.Name}' is a {FnItem.Label(kind)}. Pick one of your {FnItem.Label(kind)}s.");
			}
			else
			{
				_wanted = dataContext;
				OpenSwapOverlay();
			}
		}
		else
		{
			ModPack pendingMod = _pendingMod;
			if (pendingMod != null && _cat == Cat.Outfit)
			{
				StartModelFlow(dataContext, pendingMod);
			}
			else if (dataContext.Item.HasStyles && _cat == Cat.Outfit)
			{
				ShowStylePicker(dataContext, StyleMode.Owned, "Which style do you have equipped?", $"{dataContext.Name} has {dataContext.Item.Styles.Count} style(s). Pick the one you use.");
			}
			else
			{
				OpenSwapOverlay();
			}
		}
	}

	private void ShowStylePicker(CosmeticVm vm, StyleMode mode, string title, string sub)
	{
		_styleMode = mode;
		List<StyleVm> list = new List<StyleVm>
		{
			new StyleVm("Default", "", vm.Item.IconUrl, isDefault: true)
		};
		list.AddRange(vm.Item.Styles.Select((CosmeticStyle st) => new StyleVm(st.Name, st.Tag, st.ImageUrl)));
		StyleTitle.Text = title;
		StyleSub.Text = sub;
		StyleHost.ItemsSource = list;
		SwapOverlay.Visibility = Visibility.Collapsed;
		StyleOverlay.Visibility = Visibility.Visible;
	}

	private void StyleItem_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: StyleVm dataContext })
		{
			StyleVm styleVm = (dataContext.IsDefault ? null : dataContext);
			StyleOverlay.Visibility = Visibility.Collapsed;
			switch (_styleMode)
			{
			case StyleMode.Owned:
				_ownedStyle = styleVm;
				OpenSwapOverlay();
				break;
			case StyleMode.Target:
				_style = styleVm;
				OpenOwnedPicker();
				break;
			case StyleMode.ChangeOwned:
				_ownedStyle = styleVm;
				OpenSwapOverlay();
				break;
			case StyleMode.ChangeTarget:
				_style = styleVm;
				OpenSwapOverlay();
				break;
			}
		}
	}

	private void ChangeStyle_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: string tag }))
		{
			return;
		}
		CosmeticVm owned;
		if (tag == "Src")
		{
			owned = _owned;
			if (owned != null)
			{
				Cosmetic item = owned.Item;
				if ((object)item != null && item.HasStyles)
				{
					ShowStylePicker(_owned, StyleMode.ChangeOwned, "Which style do you have equipped?", _owned.Name ?? "");
					return;
				}
			}
		}
		if (!(tag == "Tgt"))
		{
			return;
		}
		owned = _wanted;
		if (owned != null)
		{
			Cosmetic item = owned.Item;
			if ((object)item != null && item.HasStyles)
			{
				ShowStylePicker(_wanted, StyleMode.ChangeTarget, "What style would you like?", _wanted.Name ?? "");
			}
		}
	}

	private void OptionsBackdrop_Click(object sender, MouseButtonEventArgs e)
	{
		CloseOverlays();
	}

	private void NoticeBackdrop_Click(object sender, MouseButtonEventArgs e)
	{
		CloseOverlays();
	}

	private void Swallow_Click(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void OpenSwapOverlay()
	{
		if (_owned == null || _wanted == null)
		{
			return;
		}
		Image swapSrcImg = SwapSrcImg;
		StyleVm ownedStyle = _ownedStyle;
		ImageSource? icon;
		if (ownedStyle != null)
		{
			string imageUrl = ownedStyle.ImageUrl;
			if (imageUrl != null && imageUrl.Length > 0)
			{
				icon = ownedStyle.Icon;
				goto IL_0047;
			}
		}
		icon = _owned.Icon;
		goto IL_0047;
		IL_0082:
		Image swapTgtImg;
		ImageSource? icon2;
		swapTgtImg.Source = icon2;
		SwapName.Text = $"{_owned.Name}{((_ownedStyle == null) ? "" : (" (" + _ownedStyle.Name + ")"))}  →  {_wanted.Name}{((_style == null) ? "" : (" (" + _style.Name + ")"))}";
		SrcStyleBtn.Content = "Style: " + (_ownedStyle?.Name ?? "Default");
		TgtStyleBtn.Content = "Style: " + (_style?.Name ?? "Default");
		SrcStyleBtn.Visibility = ((_cat != Cat.Outfit || !_owned.Item.HasStyles) ? Visibility.Collapsed : Visibility.Visible);
		TgtStyleBtn.Visibility = ((_cat != Cat.Outfit || !_wanted.Item.HasStyles) ? Visibility.Collapsed : Visibility.Visible);
		ModPack pendingMod = _pendingMod;
		if (pendingMod != null)
		{
			if (IsItemMod(pendingMod, out var kind))
			{
				SwapName.Text = $"{_owned.Name}  →  {pendingMod.Manifest.Name}  (custom {FnItem.Label(kind)}; keeps {_owned.Name}'s name in the Locker)";
			}
			else
			{
				SwapName.Text = ((pendingMod.Manifest.FnModel != null) ? $"{_owned.Name}  →  {pendingMod.Manifest.Name}  (custom model; keeps {_owned.Name}'s name in the Locker)" : $"{_owned.Name}  →  {pendingMod.Manifest.Name}  (drip, based on {_wanted.Name})");
			}
			if (pendingMod.Manifest.FnModel != null || IsItemMod(pendingMod, out var _))
			{
				try
				{
					string text = ModIconUri(pendingMod, "");
					if (text != null && text.Length > 0)
					{
						ImageSource imageSource = IconCache.Load(text);
						if (imageSource != null)
						{
							SwapTgtImg.Source = imageSource;
						}
					}
				}
				catch
				{
				}
			}
			SrcStyleBtn.Visibility = Visibility.Collapsed;
			TgtStyleBtn.Visibility = Visibility.Collapsed;
		}
		SwapLog.Text = "";
		SwapOverlay.Visibility = Visibility.Visible;
		RefreshSwapStatus();
		ShowReadyNote();
		ShowHeadsUp();
		return;
		IL_0047:
		swapSrcImg.Source = icon;
		swapTgtImg = SwapTgtImg;
		StyleVm style = _style;
		if (style != null)
		{
			string imageUrl = style.ImageUrl;
			if (imageUrl != null && imageUrl.Length > 0)
			{
				icon2 = style.Icon;
				goto IL_0082;
			}
		}
		icon2 = _wanted.Icon;
		goto IL_0082;
	}

	private async void ShowHeadsUp()
	{
		if (_cat != Cat.Outfit && _owned != null && _wanted != null && _pendingMod == null && _engine != null && _paks != null)
		{
			CosmeticVm wanted = _wanted;
			Cat cat = _cat;
			try
			{
				OnDemandIndex idxNow = await GetOnDemandAsync();
				PartResolver resolver = await EnsureResolverAsync();
				List<string> list = await Task.Run(() => MissingDownloads(resolver, idxNow, wanted.Id));
				if (list.Count > 0 && wanted == _wanted && SwapOverlay.Visibility == Visibility.Visible)
				{
					LockerNote.Text = "Might not work:\n• " + MissingNote(KindName(cat), wanted.Name, list);
					LockerNote.Visibility = Visibility.Visible;
				}
				return;
			}
			catch
			{
				return;
			}
		}
		if (_cat != Cat.Outfit || _owned == null || _wanted == null || _pendingMod != null)
		{
			return;
		}
		try
		{
			EnsurePartCacheLoaded();
			List<CharacterPart> value;
			List<CharacterPart> value2;
			lock (_partCache)
			{
				_partCache.TryGetValue(_owned.Id, out value);
				_partCache.TryGetValue(_wanted.Id, out value2);
			}
			if (value == null || value2 == null)
			{
				return;
			}
			bool wholeDefinition = _style != null && (value.Any((CharacterPart p) => p.ChunkId == 0) || value2.Any((CharacterPart p) => p.ChunkId == 0));
			List<string> list2 = SwapAdvisor.ForOutfit(_owned.Name, _wanted.Name, value, value2, wholeDefinition);
			StyleVm style = _style;
			if (style != null && !style.IsDefault && style.Tag.Length > 0)
			{
				bool flag;
				(List<StyleRename>, string) value3;
				lock (_styleCache)
				{
					flag = _styleCache.TryGetValue(_wanted.Id + "|" + style.Tag, out value3);
				}
				int value4 = -1;
				lock (_styleCache)
				{
					if (!_matStyleCount.TryGetValue(_wanted.Id + "|" + style.Tag, out value4))
					{
						value4 = -1;
					}
				}
				if (flag && value3.Item1.Count == 0 && value4 == 0)
				{
					list2.Add(StyleNotApplied(style.Name).Replace("Heads up: ", ""));
				}
			}
			if (list2.Count != 0)
			{
				LockerNote.Text = "Might look different:\n• " + string.Join("\n• ", list2);
				LockerNote.Visibility = Visibility.Visible;
			}
		}
		catch
		{
		}
	}

	private SwappedItem? CurrentItem()
	{
		if (_owned != null)
		{
			if (_owned == _defaultSkin && _defaultSkin != null)
			{
				return GroupedSwaps().FirstOrDefault((SwappedItem i) => i.SourceId == DefaultSkinId);
			}
			return _swapped.Items.FirstOrDefault((SwappedItem i) => string.Equals(i.SourceId, _owned.Id, StringComparison.OrdinalIgnoreCase));
		}
		return null;
	}

	private void RefreshSwapStatus()
	{
		SwappedItem swappedItem = CurrentItem();
		bool flag = ((!_retrac) ? (swappedItem != null && _engine != null && swappedItem.TargetId == _wanted?.Id && swappedItem.TargetStyle == (_style?.Name ?? "") && _engine.IsActive(swappedItem.PatchedPackages)) : (swappedItem != null && swappedItem.TargetId == _wanted?.Id && RetracActive(swappedItem) == true));
		SwapStatus.Text = (flag ? "ON" : "OFF");
		SwapStatus.Foreground = (Brush)FindResource(flag ? "Good" : "Danger");
		SwapRevertBtn.Visibility = ((swappedItem == null) ? Visibility.Collapsed : Visibility.Visible);
	}

	private async Task<PartResolver> EnsureResolverAsync()
	{
		await _resolverLock.WaitAsync();
		try
		{
			PartResolver resolver = _resolver;
			if (resolver != null && resolver.IsMounted)
			{
				return _resolver;
			}
			_resolver?.Dispose();
			_resolver = new PartResolver(_paks, _keys, Log);
			Log("Mounting the game for part lookup (first time takes a minute or two)...");
			await _resolver.MountAsync();
			return _resolver;
		}
		finally
		{
			_resolverLock.Release();
		}
	}

	private void ReleaseGameReader()
	{
		if (_resolver != null)
		{
			_resolver.Dispose();
			_resolver = null;
		}
	}

	private async Task<SwappedItem> ConvertAsync(Cosmetic src, Cosmetic tgt, Cat cat, string style = "", string styleTag = "", string srcStyle = "", ModPack? mod = null)
	{
		_lastStyleNote = "";
		if (mod?.Manifest.FnModel != null && cat == Cat.Outfit && string.Equals(src.Id, tgt.Id, StringComparison.OrdinalIgnoreCase) && mod.Manifest.PartSwaps.Count == 0 && mod.Manifest.SlotSwaps.Count == 0 && mod.Manifest.Packages.Count == 0)
		{
			return await ConvertModelOnlyAsync(src, mod);
		}
		PartResolver resolver = await EnsureResolverAsync();
		List<StyleRename> styleRenames0 = new List<StyleRename>();
		List<CharacterPart> srcPartsAll = new List<CharacterPart>();
		List<CharacterPart> tgtPartsAll = new List<CharacterPart>();
		List<string> preNotes = new List<string>();
		List<string> ownedHide = new List<string>();
		bool styleHandled = false;
		List<StyleRename> partStyleRenames = new List<StyleRename>();
		(PartPair Pair, List<StyleRename> Renames, List<StyleRename> StyleParts)? partsFallback = null;
		List<PartPair> pairs;
		if (cat != Cat.Outfit)
		{
			(CharacterPart, CharacterPart) tuple = await Task.Run(() => (resolver.RootPart(src.Id), resolver.RootPart(tgt.Id)));
			CharacterPart item = tuple.Item1;
			CharacterPart item2 = tuple.Item2;
			CharacterPart characterPart = item ?? throw new InvalidOperationException(src.Name + " (" + src.Id + ") isn't in the local game files.");
			if ((object)item2 == null)
			{
				throw new InvalidOperationException(tgt.Name + " (" + tgt.Id + ") isn't in the local game files.");
			}
			CharacterPart characterPart2 = item2;
			if (characterPart.ChunkId == 0L || characterPart2.ChunkId == 0L)
			{
				throw new InvalidOperationException("Couldn't read that item's chunk id from the game index.");
			}
			pairs = new List<PartPair>
			{
				new PartPair("Definition", characterPart.Package, characterPart2.Package, characterPart.ChunkId, characterPart2.ChunkId)
			};
			Log("Swapping the " + KindName(cat).ToLowerInvariant() + " definitions. EXPERIMENTAL.");
			Cat cat2 = cat;
			if ((uint)(cat2 - 1) <= 4u)
			{
				OnDemandIndex idxNow = await GetOnDemandAsync();
				List<string> list = await Task.Run(() => MissingDownloads(resolver, idxNow, tgt.Id));
				if (list.Count > 0)
				{
					preNotes.Add(MissingNote(KindName(cat), tgt.Name, list));
					Log("Heads up: " + preNotes[preNotes.Count - 1]);
					if (cat == Cat.Emote && list.Any((string m) => AnimLeaf.IsMatch(m)))
					{
						throw new InvalidOperationException(tgt.Name + "'s animation isn't downloaded on this PC (" + string.Join(", ", list.Where((string m) => AnimLeaf.IsMatch(m)).Take(2)) + "), so the emote would show up but do nothing. Nothing was changed.\n\nPick an emote that isn't dimmed in the list. To use this one, Fortnite has to download it first (Epic launcher > Fortnite > Options > Pre-download Streamed Assets, a big download), then swap again.");
					}
				}
			}
		}
		else
		{
			var (srcParts, tgtParts) = await Task.Run(() => (ResolveCached(resolver, src.Id), ResolveCached(resolver, tgt.Id)));
			Log($"{src.Name}: {srcParts.Count} parts · {tgt.Name}: {tgtParts.Count} parts");
			srcPartsAll = srcParts.ToList();
			tgtPartsAll = tgtParts.ToList();
			pairs = PartResolver.Pair(srcParts, tgtParts, out List<string> unpaired);
			foreach (string item4 in unpaired)
			{
				Log("  not paired: " + item4);
			}
			if (pairs.Count == 0 || pairs.Any((PartPair p) => p.SourceChunk == 0L || p.TargetChunk == 0))
			{
				CharacterPart rs = resolver.RootPart(src.Id);
				CharacterPart rt = resolver.RootPart(tgt.Id);
				if ((object)rs != null && rs.ChunkId != 0L && (object)rt != null && rt.ChunkId != 0L)
				{
					List<StyleRename> partRenames = (from p in pairs
						where !p.SourcePackage.Equals(p.TargetPackage, StringComparison.OrdinalIgnoreCase)
						select new StyleRename(p.SourcePackage, p.TargetPackage)).ToList();
					if (partRenames.Count > 0 && mod == null)
					{
						List<StyleRename> fbRenames = partRenames;
						List<StyleRename> fbStyle = new List<StyleRename>();
						if (styleTag.Length > 0)
						{
							try
							{
								var (source, message) = await Task.Run(() => ResolveStyleCached(resolver, tgt.Id, styleTag));
								Log(message);
								fbStyle = source.ToList();
								Dictionary<string, string> map = fbStyle.GroupBy((StyleRename r) => r.OldPackage, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, StyleRename> g) => g.Key, (IGrouping<string, StyleRename> g) => g.First().NewPackage, StringComparer.OrdinalIgnoreCase);
								fbRenames = partRenames.Select((StyleRename r) => (!map.TryGetValue(r.NewPackage, out var value)) ? r : new StyleRename(r.OldPackage, value)).ToList();
							}
							catch (Exception ex)
							{
								Log($"Couldn't work out style '{style}' ({ex.Message}); using the default style.");
							}
						}
						partsFallback = (new PartPair("Parts", rs.Package, rt.Package, rs.ChunkId, rt.ChunkId), fbRenames, fbStyle);
					}
					if (mod?.Manifest.FnModel != null && partRenames.Count > 0)
					{
						pairs = new List<PartPair>
						{
							new PartPair("Parts", rs.Package, rt.Package, rs.ChunkId, rt.ChunkId)
						};
						styleRenames0.AddRange(partRenames);
						Log($"Pointing {src.Name}'s character parts at {tgt.Name}'s body; {src.Name} keeps its own name in the Locker.");
						List<CharacterPart> list2 = srcParts.Where((CharacterPart characterPart3) => !tgtParts.Any((CharacterPart tp) => tp.Slot == characterPart3.Slot)).ToList();
						foreach (CharacterPart sp in list2)
						{
							List<string> list3 = await Task.Run(() => FnCarriers.MeshesOf(resolver, sp.Package));
							ownedHide.AddRange(list3);
							Log((list3.Count > 0) ? $"{src.Name}'s {sp.Slot} piece has no slot on the model's body: hiding {string.Join(", ", list3.Select(PartPair.LeafOf))}." : (src.Name + "'s " + sp.Slot + " piece has no slot on the model's body and its mesh couldn't be found, so it may still show on top of the model."));
							if (list3.Count == 0)
							{
								preNotes.Add(src.Name + " has a piece the model's body doesn't (" + sp.Slot + "); it may still show on top of the model.");
							}
						}
					}
					else
					{
						pairs = new List<PartPair>
						{
							new PartPair("Definition", rs.Package, rt.Package, rs.ChunkId, rt.ChunkId)
						};
						Log($"Swapping {src.Name}'s definition for {tgt.Name}'s, so the Locker shows {tgt.Name}'s name.");
					}
				}
			}
		}
		if (pairs.Count == 0)
		{
			throw new InvalidOperationException("No swappable assets found for that pair.");
		}
		List<string> headsUp = new List<string>(preNotes);
		if (cat == Cat.Outfit && mod == null)
		{
			headsUp.AddRange(SwapAdvisor.ForOutfit(src.Name, tgt.Name, srcPartsAll, tgtPartsAll, pairs.All((PartPair p) => p.Slot == "Definition")));
			foreach (string item5 in headsUp)
			{
				Log("Heads up: " + item5);
			}
		}
		List<StyleRename> styleRenames1 = new List<StyleRename>(styleRenames0);
		if (cat == Cat.Outfit && styleTag.Length > 0 && !styleHandled)
		{
			if (pairs.All((PartPair p) => p.Slot == "Definition"))
			{
				try
				{
					var (list4, message2) = await Task.Run(() => ResolveStyleCached(resolver, tgt.Id, styleTag));
					Log(message2);
					styleRenames1.AddRange(list4);
					if (list4.Count == 0)
					{
						_lastStyleNote = StyleNotApplied(style);
					}
				}
				catch (Exception ex2)
				{
					Log($"Couldn't work out style '{style}' ({ex2.Message}); using the default style.");
				}
			}
			else
			{
				Log("Style '" + style + "' can't be applied when swapping individual parts; using the default style.");
			}
		}
		if (mod != null)
		{
			ModManifest manifest = mod.Manifest;
			if (manifest != null)
			{
				List<ModPartSwap> partSwaps = manifest.PartSwaps;
				if (partSwaps != null && partSwaps.Count > 0)
				{
					if (cat == Cat.Outfit && pairs.All((PartPair p) => p.Slot == "Definition"))
					{
						OnDemandIndex pidx = await GetOnDemandAsync();
						DripCheck dripCheck = await Task.Run(() => AnalysePartSwaps(resolver, pidx, mod, tgt.Id, tgt.Name));
						foreach (string note in dripCheck.Notes)
						{
							Log(note);
						}
						if (dripCheck.Problems.Count > 0)
						{
							throw new InvalidOperationException("Not applied, because it would come out wrong:\n" + string.Join("\n", dripCheck.Problems.Select((string x) => "- " + x)));
						}
						styleRenames1.AddRange(dripCheck.Renames);
						Log($"Drip '{mod.Manifest.Name}': {dripCheck.Renames.Count} part swap(s).");
					}
					else
					{
						Log("This drip's part swaps need the skin-definition swap, so they were skipped.");
					}
				}
			}
		}
		if (mod != null)
		{
			ModManifest manifest = mod.Manifest;
			if (manifest != null)
			{
				List<ModSlotSwap> slotSwaps = manifest.SlotSwaps;
				if (slotSwaps != null && slotSwaps.Count > 0)
				{
					if (cat != Cat.Outfit || !pairs.All((PartPair p) => p.Slot == "Definition"))
					{
						throw new InvalidOperationException("This drip's slot swaps need the skin-definition swap, which isn't available for this pair.");
					}
					OnDemandIndex idx = await GetOnDemandAsync();
					DripCheck dripCheck2 = await Task.Run(() => AnalyseSlotSwaps(resolver, idx, mod, tgt.Id, tgt.Name));
					foreach (string note2 in dripCheck2.Notes)
					{
						Log(note2);
					}
					if (dripCheck2.Problems.Count > 0)
					{
						throw new InvalidOperationException("Not applied, because it would come out wrong:\n" + string.Join("\n", dripCheck2.Problems.Select((string x) => "- " + x)));
					}
					styleRenames1.AddRange(dripCheck2.Renames);
				}
			}
		}
		List<(StyleRename R, ulong Src, ulong Tgt)> matSwaps = new List<(StyleRename, ulong, ulong)>();
		if (cat == Cat.Outfit && styleTag.Length > 0 && mod == null)
		{
			try
			{
				var (list5, message3) = await Task.Run(() => resolver.ResolveStyleMaterials(tgt.Id, styleTag));
				Log(message3);
				foreach (StyleRename item6 in list5)
				{
					matSwaps.Add((item6, resolver.LocalChunkId(item6.OldPackage), resolver.LocalChunkId(item6.NewPackage)));
				}
				if (matSwaps.Count > 0 && _lastStyleNote.Length > 0)
				{
					_lastStyleNote = "";
				}
			}
			catch (Exception ex3)
			{
				Log("Couldn't read the materials of style '" + style + "': " + ex3.Message);
			}
		}
		List<string> neededContainers = await Task.Run(() => (from pk in pairs.SelectMany((PartPair p) => new string[2] { p.SourcePackage, p.TargetPackage }).Concat(matSwaps.SelectMany(((StyleRename R, ulong Src, ulong Tgt) m) => new string[2]
			{
				m.R.OldPackage,
				m.R.NewPackage
			})).Distinct(StringComparer.OrdinalIgnoreCase)
			select resolver.LocalContainerName(pk) into n
			where !string.IsNullOrEmpty(n)
			select (n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
		ReleaseGameReader();
		SwapEngine engine = _engine;
		SwappedItem item3 = new SwappedItem
		{
			ModKey = (mod?.Key ?? ""),
			Kind = KindName(cat),
			SourceId = src.Id,
			SourceName = src.Name,
			SourceIcon = src.IconUrl,
			TargetId = tgt.Id,
			TargetName = ((mod?.Manifest.FnModel != null) ? mod.Manifest.Name : tgt.Name),
			TargetIcon = ((mod?.Manifest.FnModel != null) ? ModIconUri(mod, tgt.IconUrl) : tgt.IconUrl),
			TargetStyle = style,
			TargetStyleTag = styleTag,
			SourceStyle = srcStyle,
			StyleRenames = styleRenames1,
			ConvertedAt = DateTime.Now,
			Notes = headsUp
		};
		if (_lastStyleNote.Length > 0)
		{
			item3.Notes.Add(_lastStyleNote.Replace("Heads up: ", ""));
		}
		string firstError = null;
		SwappedItem prior = _swapped.Items.FirstOrDefault((SwappedItem i) => string.Equals(i.SourceId, src.Id, StringComparison.OrdinalIgnoreCase));
		await Task.Run(() =>
		{
			if (prior != null)
			{
				engine.RevertPackages(prior.PatchedPackages);
			}
			foreach (string item7 in neededContainers)
			{
				string text2 = engine.EnsureContainer(item7);
				if (text2 != null)
				{
					throw new InvalidOperationException($"The swapper couldn't open the game file {item7} ({text2}). Epic's launcher is probably still updating or verifying Fortnite. Let it finish, close it, press Connect, then try again.");
				}
			}
			foreach (PartPair item8 in pairs.Where((PartPair x) => x.Slot == "Definition"))
			{
				LayoutCheck layoutCheck = engine.CheckLayout(item8.SourcePackage, item8.SourceChunk, item8.TargetPackage, item8.TargetChunk);
				Log($"Layout check: yours = {layoutCheck.Original}; the one you want = {layoutCheck.Replacement}.");
				if (!layoutCheck.Safe)
				{
					string text3 = string.Join(", ", layoutCheck.Differences);
					if (cat == Cat.Outfit && partsFallback.HasValue)
					{
						(PartPair, List<StyleRename>, List<StyleRename>) valueOrDefault = partsFallback.GetValueOrDefault();
						if (pairs.Count == 1)
						{
							pairs = new List<PartPair> { valueOrDefault.Item1 };
							styleRenames1.Clear();
							styleRenames1.AddRange(valueOrDefault.Item2);
							partStyleRenames = valueOrDefault.Item3;
							item3.Notes.Add($"{src.Name} and {tgt.Name} are built too differently to swap the whole thing safely, so {src.Name} keeps its own name in the Locker and only the look changes.");
							Log("Fell back to part swapping (keeps the owned name) to avoid a crash.");
							break;
						}
					}
					Cat cat3 = cat;
					if ((uint)cat3 > 2u)
					{
						throw new InvalidOperationException($"Not applied: {src.Name} and {tgt.Name} are built differently ({text3}), and Fortnite crashes on launch when a {KindName(cat).ToLowerInvariant()} is swapped for one shaped differently. Nothing was changed. Pick one that is built the same way.");
					}
					Log("Heads up: these two are built differently (" + text3 + "). If Fortnite crashes on launch, revert this swap.");
					item3.Notes.AddRange(SwapAdvisor.ForOther(KindName(cat), layoutDiffers: true));
				}
			}
			foreach (PartPair item9 in pairs)
			{
				try
				{
					SwapResult swapResult;
					if (item9.Slot == "Parts")
					{
						try
						{
							swapResult = engine.SwapParts(item9.SourcePackage, item9.SourceChunk, styleRenames1);
						}
						catch (InvalidOperationException ex4) when (ex4.Message.Contains("nothing to point"))
						{
							Log("Couldn't retarget the parts by name, swapping the whole definition instead.");
							swapResult = engine.Swap(item9.SourcePackage, item9.TargetPackage, item9.SourceChunk, item9.TargetChunk, (partStyleRenames.Count > 0) ? partStyleRenames : null);
						}
					}
					else
					{
						swapResult = engine.Swap(item9.SourcePackage, item9.TargetPackage, item9.SourceChunk, item9.TargetChunk, (item9.Slot == "Definition") ? styleRenames1 : null);
					}
					item3.Parts.Add(new SwappedPart
					{
						Slot = item9.Slot,
						SourcePackage = item9.SourcePackage,
						TargetPackage = item9.TargetPackage,
						SourceChunk = item9.SourceChunk,
						TargetChunk = item9.TargetChunk
					});
					item3.PatchedPackages.Add(swapResult.Source);
					Log($"OK  {item9.Slot}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
				}
				catch (Exception ex5)
				{
					if (firstError == null)
					{
						firstError = ex5.Message;
					}
					Log($"FAIL {item9.Slot} {item9.SourcePackage}: {ex5.Message}");
				}
			}
		});
		if (item3.Parts.Count == 0)
		{
			throw new InvalidOperationException(firstError ?? "Nothing could be swapped.");
		}
		if (matSwaps.Count > 0)
		{
			string text = await Task.Run(() => ApplyStyleMaterials(engine, matSwaps, item3));
			if (text != null)
			{
				Log("Heads up: " + text);
				item3.Notes.Add(text);
				_lastStyleNote = text;
			}
		}
		if (mod != null)
		{
			ModManifest manifest = mod.Manifest;
			if (manifest != null)
			{
				List<ModPackageFile> packages = manifest.Packages;
				if (packages != null && packages.Count > 0)
				{
					await Task.Run(() =>
					{
						ApplyModPackages(mod, item3);
					});
				}
			}
		}
		if (mod?.Manifest.FnModel != null)
		{
			try
			{
				await Task.Run(() =>
				{
					ApplyModModel(mod, item3, FnRigInfo.ForSkin(tgt.Id), ownedHide);
				});
			}
			catch
			{
				try
				{
					await Task.Run(() => engine.RevertPackages(item3.PatchedPackages));
				}
				catch
				{
				}
				throw;
			}
		}
		_swapped.Add(item3);
		return item3;
	}

	private string? ApplyStyleMaterials(SwapEngine engine, List<(StyleRename R, ulong Src, ulong Tgt)> mats, SwappedItem item)
	{
		List<string> list = new List<string>();
		List<SwappedPart> list2 = new List<SwappedPart>();
		string text = (string.IsNullOrEmpty(item.TargetStyle) ? "That style" : ("\"" + item.TargetStyle + "\""));
		try
		{
			foreach (var mat in mats)
			{
				LayoutCheck layoutCheck = engine.CheckLayout(mat.R.OldPackage, mat.Src, mat.R.NewPackage, mat.Tgt);
				if (!layoutCheck.Safe)
				{
					return text + " uses materials that are built differently from the normal ones (" + string.Join(", ", layoutCheck.Differences) + "), and Fortnite can crash on that, so the normal colours were kept.";
				}
			}
			foreach (var mat2 in mats)
			{
				SwapResult swapResult = engine.Swap(mat2.R.OldPackage, mat2.R.NewPackage, mat2.Src, mat2.Tgt);
				list.Add(swapResult.Source);
				list2.Add(new SwappedPart
				{
					Slot = "Material",
					SourcePackage = mat2.R.OldPackage,
					TargetPackage = mat2.R.NewPackage,
					SourceChunk = mat2.Src,
					TargetChunk = mat2.Tgt
				});
				Log($"OK  Material {PartPair.LeafOf(mat2.R.OldPackage)} -> {PartPair.LeafOf(mat2.R.NewPackage)}: {swapResult.NewSize} bytes into {swapResult.ContainersPatched} container(s)");
			}
		}
		catch (Exception ex)
		{
			Log("FAIL style materials: " + ex.Message);
			try
			{
				engine.RevertPackages(list);
			}
			catch (Exception ex2)
			{
				Log("rollback: " + ex2.Message);
			}
			return (ex.Message.Contains("not in any container", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("isn't in your game files", StringComparison.OrdinalIgnoreCase)) ? (text + "'s colours aren't downloaded on this PC yet, so the normal colours were kept. Preview " + item.TargetName + " with that style in your Locker once, then swap again.") : (text + "'s colours couldn't be applied (" + Friendly.Explain(ex.Message).Split('\n')[0] + "), so the normal colours were kept.");
		}
		foreach (string item2 in list)
		{
			item.PatchedPackages.Add(item2);
		}
		item.Parts.AddRange(list2);
		return null;
	}

	private async void Convert_Click(object sender, RoutedEventArgs e)
	{
		if (!Connected)
		{
			SwapLog.Text = "Connect on the Home tab first.";
		}
		else
		{
			if (_owned == null || _wanted == null || _busy)
			{
				return;
			}
			if (_retrac)
			{
				await ConvertRetracAsync();
				return;
			}
			if (!_warned)
			{
				if (!ConfirmEditFiles())
				{
					return;
				}
				_warned = true;
			}
			if (_readyTask != null)
			{
				try
				{
					await _readyTask;
				}
				catch
				{
				}
			}
			await StopEmoteScanAsync();
			bool flag = _cat == Cat.Outfit;
			if (flag)
			{
				flag = !(await ConfirmTargetInstalledAsync(_wanted, _style));
			}
			if (flag)
			{
				return;
			}
			if (_cat == Cat.Emote && _pendingMod == null)
			{
				try
				{
					CosmeticVm wantedEmote = _wanted;
					OnDemandIndex idxNow = await GetOnDemandAsync();
					PartResolver resolverNow = await EnsureResolverAsync();
					if ((await Task.Run(() => MissingDownloads(resolverNow, idxNow, wantedEmote.Id))).Any((string m) => AnimLeaf.IsMatch(m)))
					{
						wantedEmote.AnimReady = false;
						Dlg.Show(this, wantedEmote.Name + "'s animation isn't downloaded on this PC, so the emote would show up but do nothing when you use it. Nothing was changed.\n\nTo make it work, Fortnite has to download it first (Epic launcher > Fortnite > Options > Pre-download Streamed Assets, a big download), or pick an emote that isn't dimmed.", "This emote won't animate yet", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						return;
					}
				}
				catch
				{
				}
			}
			SetBusy(busy: true);
			SwapLog.Text = "Working...";
			try
			{
				if (_owned == _defaultSkin && _defaultSkin != null && _cat == Cat.Outfit)
				{
					await ConvertDefaultSkinAsync();
					return;
				}
				SwappedItem swappedItem;
				if (IsItemMod(_pendingMod, out var _))
				{
					swappedItem = await ConvertItemAsync(_owned.Item, _pendingMod, _cat);
				}
				else
				{
					int attempt = 0;
					ModPack pendingMod = default;
					Unsafe.SkipInit(out int num);
					while (true)
					{
						try
						{
							swappedItem = await ConvertAsync(_owned.Item, _wanted.Item, _cat, _style?.Name ?? "", _style?.Tag ?? "", _ownedStyle?.Name ?? "", _pendingMod);
						}
						catch (BodyBuildException ex) when (((Func<bool>)delegate
						{
							// Could not convert BlockContainer to single expression
							if (attempt < 4)
							{
								pendingMod = _pendingMod;
								num = ((pendingMod != null) ? 1 : 0);
							}
							else
							{
								num = 0;
							}
							return num != 0;
						}).Invoke())
						{
							_carrierProblems[CarrierKey(ex.Rig)] = ex.Rig.Name + ": couldn't build the model on it (" + Friendly.Explain(ex.Message).Split('\n')[0] + ").";
							Log(ex.Rig.Name + " didn't work for this model (" + ex.Message + "); trying another body.");
							var (cosmeticVm, message) = await ChooseCarrierAsync(pendingMod, _owned);
							if (cosmeticVm == null)
							{
								throw new InvalidOperationException(message);
							}
							_wanted = cosmeticVm;
							SwapLog.Text = ex.Rig.Name + " didn't work, trying " + cosmeticVm.Name + "'s body...";
							goto IL_0778;
						}
						break;
						IL_0778:
						attempt++;
					}
				}
				Log($"Converted {swappedItem.Parts.Count} asset(s)." + ((_lastStyleNote.Length > 0) ? (" " + _lastStyleNote) : ""));
				foreach (string note in swappedItem.Notes)
				{
					Log("Might look different: " + note);
				}
				string value = (string.IsNullOrEmpty(swappedItem.TargetName) ? "the new look" : swappedItem.TargetName);
				SwapLog.Text = $"Swapped! {swappedItem.SourceName} now looks like {value}. Open Fortnite and check your Locker.\nYou only see it in the lobby and Locker, not in a match. Press Undo before you join a match, or Fortnite can crash or kick you.";
			}
			catch (Exception ex2)
			{
				Log("ERROR: " + ex2.Message);
				SwapLog.Text = "Didn't swap. Nothing was changed.\n" + Friendly.Explain(ex2.Message).Split('\n')[0].Trim() + "\nClose Fortnite and the Epic launcher, then try again.";
			}
			finally
			{
				SetBusy(busy: false);
				RefreshSwapStatus();
				RefreshSwappedList();
			}
		}
	}

	// "Default Skin": every default Recruit an account can be given gets the same swap, so whichever one the account wears shows the wanted skin.
	private async Task ConvertDefaultSkinAsync()
	{
		CosmeticVm wanted = _wanted;
		List<Cosmetic> variants = _defaultVariants.Where((Cosmetic v) => IsDefaultOutfit(v) || _local == null || _local.Contains(v.Id)).ToList();
		if (variants.Count == 0)
		{
			throw new InvalidOperationException("No default skins were found in your game files.");
		}
		int done = 0;
		int failed = 0;
		bool mainOk = false;
		SwappedItem? last = null;
		string lastError = "";
		Log($"Default Skin: swapping {variants.Count} default skin(s) to {wanted.Name}.");
		foreach (Cosmetic v in variants)
		{
			SwapLog.Text = $"Working... default skin {done + failed + 1} of {variants.Count}";
			try
			{
				last = await ConvertAsync(v, wanted.Item, _cat, _style?.Name ?? "", _style?.Tag ?? "", "", null);
				done++;
				if (IsDefaultOutfit(v))
				{
					mainOk = true;
				}
			}
			catch (Exception ex)
			{
				failed++;
				lastError = ex.Message;
				Log($"Default Skin: {v.Id} didn't swap: {ex.Message}");
				if (IsDefaultOutfit(v))
				{
					// The lobby uses this one item for the empty slot, so nothing else is worth doing without it.
					throw new InvalidOperationException("The main Default item (" + DefaultOutfitId + ") couldn't be swapped: " + ex.Message + "\nNothing else was changed for Default Skin.");
				}
			}
		}
		if (done == 0)
		{
			throw new InvalidOperationException(lastError.Length > 0 ? lastError : "None of the default skins could be swapped.");
		}
		Log($"Default Skin: {done} of {variants.Count} default skin(s) now look like {wanted.Name}." + ((failed > 0) ? $" {failed} couldn't be swapped (see the lines above)." : ""));
		string value = (string.IsNullOrEmpty(last?.TargetName) ? wanted.Name : last.TargetName);
		SwapLog.Text = $"Swapped! Your default skin now looks like {value} ({done} of {variants.Count} default skins).\nOpen Fortnite and check your Locker.\nYou only see it in the lobby and Locker, not in a match. Press Undo before you join a match." + ((failed > 0) ? $"\n{failed} default skin(s) couldn't be swapped; details are in the log." : "");
	}

	// All the swapped default skins that share a target are shown (and undone) as one "Default Skin" row.
	private List<SwappedItem> GroupedSwaps()
	{
		_groupMembers = new Dictionary<SwappedItem, List<SwappedItem>>(ReferenceEqualityComparer.Instance);
		List<SwappedItem> list = new List<SwappedItem>();
		List<SwappedItem> members = _swapped.Items.Where((SwappedItem i) => !_retrac && (i.Kind.Length == 0 || i.Kind == "Outfit") && i.ModKey.Length == 0 && _defaultVariants.Any((Cosmetic v) => string.Equals(v.Id, i.SourceId, StringComparison.OrdinalIgnoreCase))).ToList();
		HashSet<SwappedItem> used = new HashSet<SwappedItem>(ReferenceEqualityComparer.Instance as IEqualityComparer<SwappedItem>);
		foreach (SwappedItem item in _swapped.Items)
		{
			if (!members.Contains(item))
			{
				list.Add(item);
				continue;
			}
			if (used.Contains(item))
			{
				continue;
			}
			List<SwappedItem> same = members.Where((SwappedItem m) => m.TargetId == item.TargetId && m.TargetStyle == item.TargetStyle).ToList();
			foreach (SwappedItem m2 in same)
			{
				used.Add(m2);
			}
			SwappedItem agg = new SwappedItem
			{
				Kind = "Outfit",
				SourceId = DefaultSkinId,
				SourceName = "Default Skin",
				SourceIcon = (_defaultSkin?.Item.IconUrl ?? item.SourceIcon),
				TargetId = item.TargetId,
				TargetName = item.TargetName,
				TargetIcon = item.TargetIcon,
				TargetStyle = item.TargetStyle,
				TargetStyleTag = item.TargetStyleTag,
				ConvertedAt = same.Max((SwappedItem m) => m.ConvertedAt),
				Parts = same.SelectMany((SwappedItem m) => m.Parts).ToList(),
				PatchedPackages = same.SelectMany((SwappedItem m) => m.PatchedPackages).Distinct().ToList(),
				Notes = same.SelectMany((SwappedItem m) => m.Notes).Distinct().Take(3).ToList()
			};
			_groupMembers[agg] = same;
			list.Add(agg);
		}
		return list;
	}

	private async Task RevertAnyAsync(SwappedItem item)
	{
		if (_groupMembers.TryGetValue(item, out List<SwappedItem> value))
		{
			foreach (SwappedItem member in value.ToList())
			{
				await RevertItemAsync(member);
			}
		}
		else
		{
			await RevertItemAsync(item);
		}
	}

	private async void OverlayRevert_Click(object sender, RoutedEventArgs e)
	{
		SwappedItem swappedItem = CurrentItem();
		if (swappedItem != null && !_busy)
		{
			await RevertAnyAsync(swappedItem);
			RefreshSwapStatus();
		}
	}

	private async Task RevertItemAsync(SwappedItem item)
	{
		if (_retrac)
		{
			await RevertRetracItemAsync(item);
			return;
		}
		if (_engine == null)
		{
			Log("ERROR: connect first.");
			return;
		}
		SetBusy(busy: true);
		ReleaseGameReader();
		try
		{
			SwapEngine engine = _engine;
			var (num, num2) = await Task.Run(() => engine.RevertPackages(item.PatchedPackages));
			_swapped.Remove(item);
			string text = "Reverted " + item.SourceName + ((num2 > 0 && num == 0) ? " (game files were already original)." : ".");
			Log(text);
			if (SwapOverlay.Visibility == Visibility.Visible)
			{
				SwapLog.Text = text;
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			if (SwapOverlay.Visibility == Visibility.Visible)
			{
				SwapLog.Text = ex.Message;
			}
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwappedList();
		}
	}

	private void BackfillNotes()
	{
		bool flag = false;
		try
		{
			EnsurePartCacheLoaded();
			foreach (SwappedItem item in _swapped.Items.Where((SwappedItem i) => i.Notes.Count == 0 && (i.Kind.Length == 0 || i.Kind == "Outfit") && i.ModKey.Length == 0))
			{
				List<CharacterPart> value;
				List<CharacterPart> value2;
				lock (_partCache)
				{
					_partCache.TryGetValue(item.SourceId, out value);
					_partCache.TryGetValue(item.TargetId, out value2);
				}
				if (value != null && value2 != null)
				{
					List<string> list = SwapAdvisor.ForOutfit(item.SourceName, item.TargetName, value, value2, item.Parts.Count > 0 && item.Parts.All((SwappedPart p) => p.Slot == "Definition"));
					if (list.Count != 0)
					{
						item.Notes = list;
						flag = true;
					}
				}
			}
			if (flag)
			{
				_swapped.Save();
			}
		}
		catch
		{
		}
	}

	private void RefreshSwappedList()
	{
		BackfillNotes();
		List<SwappedVm> list = GroupedSwaps().Select((SwappedItem i) => new SwappedVm(i)).ToList();
		foreach (SwappedVm item in list)
		{
			SwappedVm swappedVm = item;
			bool? active;
			if (_retrac)
			{
				active = RetracActive(item.Item);
			}
			else
			{
				active = ((_engine == null) ? ((bool?)null) : new bool?(_engine.IsActive(item.Item.PatchedPackages)));
			}
			swappedVm.Active = active;
		}
		SwappedHost.ItemsSource = list;
		SwappedEmpty.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		int num = Enumerable.Count(list, (SwappedVm v) => v.Active == false);
		ReapplyBtn.Visibility = ((list.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		TextBlock swappedHint = SwappedHint;
		string text;
		if (list.Count != 0)
		{
			if (Connected)
			{
				text = ((num > 0) ? $"{num} swap(s) are not applied right now (reverted when Fortnite closed, a game update, or Epic verify). Hit Re-apply all to use them again." : "All swaps active.");
			}
			else
			{
				text = "Connect to check which swaps are still active.";
			}
		}
		else
		{
			text = "";
		}
		swappedHint.Text = text;
		UpdateDashboard();
	}

	private async void SwappedRevert_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: SwappedVm dataContext } && !_busy)
		{
			await RevertAnyAsync(dataContext.Item);
		}
	}

	private async void Reapply_Click(object sender, RoutedEventArgs e)
	{
		if (_retrac)
		{
			if (!_busy)
			{
				await ReapplyRetracAsync();
			}
		}
		else if (_engine == null)
		{
			Log("ERROR: connect first.");
		}
		else
		{
			if (_busy)
			{
				return;
			}
			SetBusy(busy: true);
			ReleaseGameReader();
			SwapEngine engine = _engine;
			List<SwappedItem> items = _swapped.Items.ToList();
			try
			{
				int redone = 0;
				List<string> failures = new List<string>();
				await Task.Run(() =>
				{
					foreach (SwappedItem item in items)
					{
						if (!engine.IsActive(item.PatchedPackages))
						{
							try
							{
								if (!StrictKind(item.Kind))
								{
									goto IL_013e;
								}
								string text = UnsafeReason(engine, item);
								if (text.Length <= 0)
								{
									goto IL_013e;
								}
								_swapped.Remove(item);
								Log($"Not re-applied: {item.SourceName} → {item.TargetName} is built differently ({text}); Fortnite crashes on launch with that. Dropped it.");
								failures.Add($"{item.SourceName} → {item.TargetName}: built differently ({text}), so it was dropped instead of crashing Fortnite.");
								goto end_IL_0030;
								IL_013e:
								foreach (SwappedPart part in item.Parts)
								{
									if (part.Slot == "ModPackage")
									{
										ReapplyModPart(item, part);
									}
									else
									{
										if (part.Slot == "ModBuilds")
										{
											Log("Re-applying '" + item.SourceName + "'...");
											ReapplyModBuilds(item);
											break;
										}
										if (part.Slot == "ModModel")
										{
											Log($"Re-applying the model '{item.TargetName}' (missing: {string.Join(", ", from m in engine.MissingPackages(item.PatchedPackages)
												select m.Split('/')[^1])})...");
											ReapplyModModel(item);
										}
										else if (part.Slot == "ModItem")
										{
											Log($"Re-applying the model '{item.TargetName}' on {item.SourceName}...");
											ReapplyItemModel(item);
										}
										else
										{
											string slot = part.Slot;
											bool flag = ((slot == "ModItemPlain" || slot == "ModItemHide") ? true : false);
											if (!flag && !(part.Slot == "ModHide"))
											{
												if (part.Slot == "Parts")
												{
													engine.SwapParts(part.SourcePackage, part.SourceChunk, item.StyleRenames);
												}
												else
												{
													engine.Swap(part.SourcePackage, part.TargetPackage, part.SourceChunk, part.TargetChunk, (part.Slot == "Definition") ? item.StyleRenames : null);
												}
											}
										}
									}
								}
								redone++;
								Log("Re-applied " + item.SourceName + " → " + item.TargetName);
								end_IL_0030:;
							}
							catch (Exception ex2)
							{
								Log("FAIL " + item.SourceName + ": " + ex2.Message);
								failures.Add($"{item.SourceName} → {item.TargetName}: {ex2.Message}");
							}
						}
					}
				});
				Log((redone == 0 && failures.Count == 0) ? "Nothing needed re-applying." : ($"Re-applied {redone} swap(s)." + ((failures.Count > 0) ? $" {failures.Count} failed." : "")));
				if (failures.Count > 0)
				{
					ShowNotice("Couldn't re-apply everything", string.Join("\n\n", failures) + "\n\nClose Fortnite and the Epic Launcher (don't press Verify), then try again.");
				}
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
			finally
			{
				SetBusy(busy: false);
				RefreshSwappedList();
			}
		}
	}

	private async void Verify_Click(object sender, RoutedEventArgs e)
	{
		if (_retrac)
		{
			VerifyRetrac();
		}
		else if (_engine == null)
		{
			Log("ERROR: connect first.");
		}
		else
		{
			if (_busy)
			{
				return;
			}
			SetBusy(busy: true);
			SwapEngine engine = _engine;
			List<SwappedItem> items = _swapped.Items.ToList();
			try
			{
				Log("Verifying swaps against the game files...");
				ReleaseGameReader();
				VerifyReport report = await Task.Run(() => engine.Verify());
				if (report.Records > 0 && report.Ok)
				{
					try
					{
						PartResolver resolver = await EnsureResolverAsync();
						List<SwappedItem> list = items.Where((SwappedItem i) => i.ModKey.Length == 0 && i.SourceId.Length > 0 && i.TargetId.Length > 0 && engine.IsActive(i.PatchedPackages)).ToList();
						foreach (SwappedItem item in list)
						{
							VerifyLine item2 = await Task.Run(() => SwapVerifier.Semantic(resolver, item.SourceId, item.TargetId, item.SourceName, item.TargetName, item.StyleRenames.Count > 0));
							report.Lines.Add(item2);
						}
					}
					catch (Exception ex)
					{
						report.Add(VerifyLevel.Warn, "", "Couldn't check what the game loads: " + ex.Message);
					}
				}
				foreach (VerifyLine line in report.Lines)
				{
					Log(line.ToString());
				}
				Log(report.Summary());
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine(report.Summary());
				foreach (VerifyLine item3 in report.Lines.Where((VerifyLine l) => l.Level != VerifyLevel.Ok || l.Container.Length == 0).Take(14))
				{
					StringBuilder stringBuilder2 = stringBuilder.AppendLine();
					string value;
					if (item3.Level != VerifyLevel.Ok)
					{
						value = ((item3.Level == VerifyLevel.Warn) ? "! " : "✘ ");
					}
					else
					{
						value = "✔ ";
					}
					stringBuilder2.Append(value).Append(item3.Text);
				}
				List<string> list2 = FortniteLogCheck.Latest();
				if (list2.Count > 0)
				{
					stringBuilder.AppendLine().Append("✘ Fortnite's last log has a signature error in: " + string.Join(", ", list2) + ". Fortnite's log reports a signature error in the container(s) listed above: the game checks every block of its signed files and a swapped container fails that check, which can leave the game stuck on \"Logging in\" or show \"Verification Required\". Close Fortnite and the Epic launcher, then press Undo all swaps / Revert all.");
				}
				if (report.Records > 0)
				{
					FortniteLoadout fortniteLoadout = FortniteLoadout.ReadLatest();
					foreach (SwappedItem item4 in items.Where((SwappedItem i) => engine.IsActive(i.PatchedPackages)))
					{
						string kind = item4.Kind;
						string text = fortniteLoadout?.Get(kind switch
						{
							"Pickaxe" => "Pickaxe", 
							"Back Bling" => "Backpack", 
							"Glider" => "Glider", 
							"Emote" => "Dance", 
							"Contrail" => "Contrail", 
							"Lobby Music" => "MusicPack", 
							"Loading Screen" => "LoadingScreen", 
							"Toy" => "Toy", 
							"Spray" => "Spray", 
							"Wrap" => "ItemWrap", 
							"Emoticon" => "Emoji", 
							_ => "Character", 
						});
						if (text != null && !string.Equals(text, item4.SourceId, StringComparison.OrdinalIgnoreCase))
						{
							StringBuilder stringBuilder3 = stringBuilder.AppendLine();
							StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(109, 3, stringBuilder3);
							handler.AppendLiteral("! ");
							handler.AppendFormatted(item4.SourceName);
							handler.AppendLiteral(" is swapped, but Fortnite's log says \"");
							handler.AppendFormatted(text);
							handler.AppendLiteral("\" is what you have equipped. Equip ");
							handler.AppendFormatted(item4.SourceName);
							handler.AppendLiteral(" in your Locker to see the change.");
							stringBuilder3.Append(ref handler);
						}
					}
				}
				if (report.Ok && report.Records > 0)
				{
					stringBuilder.AppendLine().AppendLine().Append("The files are right. If Fortnite still shows the old skin, equip the skin you swapped (the one you own) in your Locker; that is the one that changes.");
				}
				else if (!report.Ok)
				{
					stringBuilder.AppendLine().AppendLine().Append("Use \"Redo all swaps\" after closing Fortnite. If that fails the same way, Revert all and try one swap again.");
				}
				ShowNotice(report.Ok ? "Swaps verified" : "Swaps are not in the files", stringBuilder.ToString());
			}
			catch (Exception ex2)
			{
				Log("ERROR: " + ex2.Message);
			}
			finally
			{
				SetBusy(busy: false);
			}
		}
	}

	private async void RevertAll_Click(object sender, RoutedEventArgs e)
	{
		if (_retrac)
		{
			if (!_busy)
			{
				await RevertAllRetracAsync();
			}
		}
		else if (_engine == null)
		{
			Log("ERROR: connect first.");
		}
		else
		{
			if (_busy)
			{
				return;
			}
			SetBusy(busy: true);
			ReleaseGameReader();
			try
			{
				var (value, num) = await Task.Run((Func<(int, int)>)_engine.RevertAll);
				_swapped.Clear();
				Log($"Reverted {value} patch(es){((num > 0) ? $", {num} skipped (game files changed since)" : "")}.");
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
			}
			finally
			{
				SetBusy(busy: false);
				RefreshSwappedList();
			}
		}
	}
}
