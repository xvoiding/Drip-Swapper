using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drip.Core;
using Drip.Core.Fun;
using Drip.Core.RocketLeague;
using Drip.Core.RocketLeague.Custom;
using Drip.Core.RocketLeague.Live;
using Microsoft.Win32;

namespace Drip.App;

public partial class RocketLeagueWindow : Window, IComponentConnector, IStyleConnector
{
	private enum Page
	{
		Home,
		Items,
		Mods,
		Swaps,
		Live,
		Fun,
		Settings
	}

	private const string LookPack = "Cinematic";

	private List<RlCustom.Entry> _library = new List<RlCustom.Entry>();

	private FbxScene? _scene;

	private string? _fbxPath;

	private readonly List<string> _picPaths = new List<string>();

	private List<NamedPicture> _previewPics = new List<NamedPicture>();

	private int _turns;

	private bool _flip;

	private bool _modsReady;

	private int _previewSeq;

	private RlItem? _customPick;

	private bool _mapRanges;

	private bool _nameFromFile = true;

	private CarMesh? _reference;

	private CarMesh? _refBall;

	private CarMesh? _refWheel;

	private bool _referenceTried;

	private float _scaleA = 1f;

	private float _scaleB = 1f;

	private float _scaleC = 1f;

	private Point _dragFrom;

	private double _dragX;

	private double _dragY;

	private double _dragZ;

	private bool _dragging;

	private const int MaxShown = 150;

	private readonly RlSwapState _state = RlSwapState.Load();

	private RlKeys _keys = RlKeys.LoadSaved() ?? new RlKeys(Array.Empty<byte[]>());

	private RlSwapEngine? _engine;

	private List<RlItem> _items = new List<RlItem>();

	private HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private string _slot = "Body";

	private RlItem? _target;

	private bool _busy;

	private bool _wasRunning;

	private Page _page;

	private readonly DispatcherTimer _watch = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(3L)
	};

	private bool _connected;

	private bool _tweakSilent;

	private string _tweakReportShown = "";

	private readonly DispatcherTimer _tweakTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(700L)
	};

	private bool _liveUnlocked;

	private string LookPreset => ((LookPresetBox?.SelectedItem as ComboBoxItem)?.Tag as string) ?? "DripCinematic.ini";

	private string CurrentSlot
	{
		get
		{
			if (KindMap.IsChecked != true)
			{
				if (KindBall.IsChecked != true)
				{
					if (KindWheel.IsChecked != true)
					{
						return "Body";
					}
					return "Wheels";
				}
				return "Ball";
			}
			return "Map";
		}
	}

	private MapCollisionMode CurrentMapCollision => MapCollisionBox.SelectedIndex switch
	{
		1 => MapCollisionMode.ModelTriangles, 
		2 => MapCollisionMode.SimpleBox, 
		_ => MapCollisionMode.Auto, 
	};

	private IReadOnlyList<FitEntry> CurrentFitEntries()
	{
		return (from r in _state.Swaps
			where !r.Custom && r.TargetPackage.Length > 0 && r.DonorPackage.Length > 0 && r.Slot != "Map"
			select new FitEntry
			{
				Kind = r.Slot,
				Source = r.TargetPackage,
				Target = r.DonorPackage
			}).ToList();
	}

	private async Task<string> ApplyFitAsync(Fit fit)
	{
		if (_busy)
		{
			return "Wait for the current job to finish.";
		}
		if (_engine == null)
		{
			return "Connect to Rocket League on the Home tab first.";
		}
		if (_keys.All.Count == 0)
		{
			return "Add your keys file in Settings first.";
		}
		if (RlLocator.IsRunning())
		{
			return "Close Rocket League first, then apply the loadout.";
		}
		if (_items.Count == 0)
		{
			return "The item list hasn't loaded yet. Connect on the Home tab.";
		}
		SetBusy(busy: true);
		int ok = 0;
		List<string> problems = new List<string>();
		try
		{
			RlSwapEngine engine = _engine;
			bool thumbs = ThumbBox.IsChecked == true;
			foreach (FitEntry en in fit.Entries)
			{
				RlItem target = _items.FirstOrDefault((RlItem i) => i.AssetPackage.Equals(en.Source, StringComparison.OrdinalIgnoreCase) && IsSwappable(i));
				RlItem donor = _items.FirstOrDefault((RlItem i) => i.AssetPackage.Equals(en.Target, StringComparison.OrdinalIgnoreCase) && IsSwappable(i));
				if ((object)target == null || (object)donor == null)
				{
					problems.Add(en.Source + " → " + en.Target + ": not in your game files");
					continue;
				}
				try
				{
					await Task.Run(() => engine.Swap(target, donor, thumbs));
					ok++;
					Log($"Loadout \"{fit.Name}\": {target.Name} now looks like {donor.Name}.");
				}
				catch (Exception ex)
				{
					problems.Add($"{target.Name} → {donor.Name}: {ex.Message}");
					Log("ERROR: " + ex.Message);
				}
			}
		}
		finally
		{
			SetBusy(busy: false);
			RefreshSwaps();
		}
		string text = $"Applied {ok} of {fit.Entries.Count} swap{((fit.Entries.Count == 1) ? "" : "s")} from \"{fit.Name}\". Stay offline: modified files can get your account banned in online play.";
		if (problems.Count > 0)
		{
			text = text + " Skipped: " + string.Join("; ", problems.Take(3)) + ((problems.Count > 3) ? $" (+{problems.Count - 3} more)" : "");
		}
		return text;
	}

	private string? LookWin64()
	{
		return RlLook.Win64From(_state.Settings.CookedDirectory) ?? RlLook.Win64From(RlLocator.FindCookedDirectory());
	}

	private static string LookPackDir()
	{
		return System.IO.Path.Combine(AppContext.BaseDirectory, "Looks", "Cinematic");
	}

	private void RefreshLook()
	{
		string text = LookWin64();
		bool flag = Directory.Exists(LookPackDir());
		bool flag2 = text != null && RlLook.HasReShade(text);
		bool flag3 = text != null && RlLook.Installed(text) != null;
		TextBlock lookStatus = LookStatus;
		string text2;
		if (text == null)
		{
			text2 = "Rocket League's program folder wasn't found. Pick your game folder on the Home page first.";
		}
		else if (flag)
		{
			if (flag3)
			{
				text2 = "The look is on. Press Home in game to open ReShade's menu and tune or switch off each effect.";
			}
			else
			{
				text2 = (flag2 ? "ReShade is installed. Press Add the look." : "ReShade isn't installed for Rocket League yet. Press Get ReShade, install it for RocketLeague.exe, then come back.");
			}
		}
		else
		{
			text2 = "The look files are missing from this copy of the app.";
		}
		lookStatus.Text = text2;
		LookInstallBtn.IsEnabled = (text != null) & flag & flag2;
		LookInstallBtn.Content = (flag3 ? "Switch style" : "Add the look");
		if (flag3)
		{
			string text3 = RlLook.Installed(text)?.PresetFile;
			if (text3 != null && text3.Length > 0)
			{
				foreach (ComboBoxItem item in (IEnumerable)LookPresetBox.Items)
				{
					if (string.Equals(item.Tag as string, System.IO.Path.GetFileName(text3), StringComparison.OrdinalIgnoreCase))
					{
						LookPresetBox.SelectedItem = item;
						break;
					}
				}
			}
		}
		LookRemoveBtn.IsEnabled = flag3;
	}

	private void LookGet_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo("https://reshade.me")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't open the site", ex.Message);
		}
		ShowNotice("Installing ReShade", "On reshade.me download the installer and run it.\n1. Pick RocketLeague.exe from your game's Binaries\\Win64 folder.\n2. Choose DirectX 10/11/12.\n3. Skip the shader packs (Drip brings its own).\nThen come back here and press Add the look.");
	}

	private void LookInstall_Click(object sender, RoutedEventArgs e)
	{
		string text = LookWin64();
		if (text != null)
		{
			try
			{
				int value = RlLook.Install(text, LookPackDir(), LookPreset);
				Log($"Look added: {LookPreset} ({value} files).");
				ShowNotice("The look is on", "Start Rocket League with the anti-cheat off (offline play) and open freeplay.\n\nPress Home in game to open ReShade's menu. If a tick box is red, that effect failed to compile on your setup; leave the rest on.\n\nPress Remove before you play online.");
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
				ShowNotice("Couldn't add the look", ex.Message);
			}
			RefreshLook();
		}
	}

	private void LookRemove_Click(object sender, RoutedEventArgs e)
	{
		string text = LookWin64();
		if (text != null)
		{
			try
			{
				int value = RlLook.Remove(text);
				Log($"Cinematic look removed ({value} files).");
				ShowNotice("The look is removed", "Everything Drip added is gone and ReShade's settings are back as they were. ReShade itself is still installed; remove it with its own installer if you don't want it.");
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
				ShowNotice("Couldn't remove the look", ex.Message);
			}
			RefreshLook();
		}
	}

	private ModelFit CurrentFit()
	{
		return new ModelFit
		{
			Turns = _turns,
			FlipFrontBack = _flip,
			Yaw = YawSlider.Value,
			Pitch = PitchSlider.Value,
			Roll = RollSlider.Value,
			Size = SizeSlider.Value,
			Lift = LiftSlider.Value,
			OffsetX = MoveXSlider.Value,
			OffsetY = MoveYSlider.Value,
			UseMaterialColors = (ColorsBox.IsChecked == true),
			LeaveOut = ((LeaveOutBox.IsChecked == true && KindWheel.IsChecked != true) ? new ModelFit().LeaveOut : new List<string>())
		};
	}

	private static string KindWord(string slot)
	{
		return slot switch
		{
			"Ball" => "ball", 
			"Wheels" => "wheels", 
			"Map" => "map", 
			_ => "car", 
		};
	}

	private void MapScenery_Changed(object sender, RoutedEventArgs e)
	{
		if (!_modsReady)
		{
			return;
		}
		_state.Settings.MapKeepScenery = MapSceneryBox.IsChecked == true;
		try
		{
			_state.Save();
		}
		catch
		{
		}
	}

	private void MapMaterial_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (!_modsReady || MapMaterialBox.SelectedIndex < 0)
		{
			return;
		}
		_state.Settings.MapMaterial = MapMaterialBox.SelectedIndex;
		try
		{
			_state.Save();
		}
		catch
		{
		}
	}

	private void SetMapRanges(bool map)
	{
		double num = (map ? 4000 : 0);
		MoveXSlider.Minimum = (map ? (0.0 - num) : (-60.0));
		MoveXSlider.Maximum = (map ? num : 60.0);
		MoveYSlider.Minimum = (map ? (0.0 - num) : (-40.0));
		MoveYSlider.Maximum = (map ? num : 40.0);
		LiftSlider.Minimum = (map ? (-500) : (-40));
		LiftSlider.Maximum = (map ? 500 : 40);
		SizeSlider.Minimum = (map ? 0.1 : 0.3);
		SizeSlider.Maximum = (map ? 1.5 : 2.5);
		Slider moveXSlider = MoveXSlider;
		Slider moveYSlider = MoveYSlider;
		double num2 = (LiftSlider.TickFrequency = (map ? 5.0 : 0.5));
		double tickFrequency = (moveYSlider.TickFrequency = num2);
		moveXSlider.TickFrequency = tickFrequency;
		Slider moveXSlider2 = MoveXSlider;
		Slider moveYSlider2 = MoveYSlider;
		num2 = (LiftSlider.SmallChange = (map ? 5.0 : 0.5));
		tickFrequency = (moveYSlider2.SmallChange = num2);
		moveXSlider2.SmallChange = tickFrequency;
		Slider moveXSlider3 = MoveXSlider;
		Slider moveYSlider3 = MoveYSlider;
		num2 = (LiftSlider.LargeChange = (map ? 50 : 2));
		tickFrequency = (moveYSlider3.LargeChange = num2);
		moveXSlider3.LargeChange = tickFrequency;
		if (map)
		{
			MoveXSlider.Value = 0.0;
			MoveYSlider.Value = 0.0;
			LiftSlider.Value = 0.0;
			SizeSlider.Value = 1.0;
		}
	}

	private void InitMods()
	{
		_modsReady = true;
		try
		{
			MapMaterialBox.SelectedIndex = Math.Clamp(_state.Settings.MapMaterial, 0, 3);
			MapSceneryBox.IsChecked = _state.Settings.MapKeepScenery;
		}
		catch
		{
		}
		try
		{
			Preinstalled.Result result = Preinstalled.SeedRocketLeague();
			foreach (string problem in result.Problems)
			{
				Log("preinstalled model skipped: " + problem);
			}
			if (result.Added > 0)
			{
				Log($"Added {result.Added} models that came with the app (see My models).");
			}
		}
		catch (Exception ex)
		{
			Log("preinstalled models: " + ex.Message);
		}
		RefreshLibrary();
	}

	private void RefreshLibrary()
	{
		_library = RlCustom.List();
		foreach (string item in RlCustom.Skipped)
		{
			Log("Couldn't open " + item);
		}
		ModelsHost.ItemsSource = _library.Select((RlCustom.Entry e) => new RlModelVm(e)).ToList();
		ModelsEmpty.Visibility = ((_library.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		ModelsSkipped.Visibility = ((RlCustom.Skipped.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		ModelsSkipped.Text = ((RlCustom.Skipped.Count == 0) ? "" : ("These model files are in your models folder but couldn't be opened:\n" + string.Join("\n", RlCustom.Skipped) + "\n(Folder: " + RlCustom.Dir() + ")"));
	}

	private void PickFbx_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick a model",
			Filter = ModelFileReader.DialogFilter()
		};
		if (openFileDialog.ShowDialog() == true)
		{
			LoadFbxAsync(openFileDialog.FileName);
		}
	}

	private void PickPicture_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick the texture pictures (select as many as you need)",
			Filter = "Pictures (*.png;*.jpg;*.jpeg;*.bmp;*.tga)|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All files (*.*)|*.*",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			SetPictures(openFileDialog.FileNames);
		}
	}

	private void ClearPictures_Click(object sender, RoutedEventArgs e)
	{
		SetPictures(Array.Empty<string>());
	}

	private void OpenMake_Click(object sender, RoutedEventArgs e)
	{
		MakeOverlay.Visibility = Visibility.Visible;
	}

	private void CloseMake_Click(object sender, RoutedEventArgs e)
	{
		MakeOverlay.Visibility = Visibility.Collapsed;
	}

	private async Task LoadFbxAsync(string path)
	{
		ModStatus.Text = "Reading the model…";
		SetBusy(busy: true);
		try
		{
			_scene = await Task.Run(() => ModelFileReader.Load(path));
			_fbxPath = path;
			FbxBox.Text = path;
			if (ModelNameBox.Text.Trim().Length == 0 || _nameFromFile)
			{
				ModelNameBox.Text = System.IO.Path.GetFileNameWithoutExtension(path);
				_nameFromFile = true;
			}
			_turns = 0;
			_flip = false;
			TurnText.Text = "";
			YawSlider.Value = 0.0;
			PitchSlider.Value = 0.0;
			RollSlider.Value = 0.0;
			MoveXSlider.Value = 0.0;
			MoveYSlider.Value = 0.0;
			CreateBtn.IsEnabled = true;
			ModStatus.Text = $"{_scene.Meshes.Count} mesh{((_scene.Meshes.Count == 1) ? "" : "es")}, {_scene.Meshes.Sum((FbxMesh m) => m.TriangleCount):N0} triangles.";
			RebuildPreview();
		}
		catch (Exception ex)
		{
			_scene = null;
			CreateBtn.IsEnabled = false;
			ModStatus.Text = ex.Message;
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void SetPictures(IEnumerable<string> paths)
	{
		List<string> list = paths.Where((string f) => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		List<NamedPicture> list2 = new List<NamedPicture>();
		List<string> list3 = new List<string>();
		List<string> list4 = new List<string>();
		foreach (string item in list)
		{
			try
			{
				list2.Add(new NamedPicture(System.IO.Path.GetFileName(item), TexImage.Load(File.ReadAllBytes(item)).ForGame(256, 64)));
				list3.Add(item);
			}
			catch
			{
				list4.Add(System.IO.Path.GetFileName(item));
			}
		}
		_picPaths.Clear();
		_picPaths.AddRange(list3);
		_previewPics = list2;
		TextBox picBox = PicBox;
		picBox.Text = list3.Count switch
		{
			0 => "", 
			1 => list3[0], 
			_ => $"{list3.Count} pictures: " + string.Join(", ", list3.Select(System.IO.Path.GetFileName)), 
		};
		if (list4.Count > 0)
		{
			ModStatus.Text = "Couldn't read: " + string.Join(", ", list4);
		}
		RebuildPreview();
	}

	private void Fit_Changed(object sender, RoutedEventArgs e)
	{
		if (KeepWheelsBox != null)
		{
			KeepWheelsBox.Visibility = ((KindCar.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (MapOptions != null)
		{
			MapOptions.Visibility = ((KindMap.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (_modsReady && MoveXSlider != null && KindMap.IsChecked == true != _mapRanges)
		{
			_mapRanges = KindMap.IsChecked == true;
			SetMapRanges(_mapRanges);
		}
		if (_modsReady)
		{
			RebuildPreview();
		}
	}

	private void Fit_Slider(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_modsReady && SizeText != null && LiftText != null && MoveXText != null && MoveYText != null && YawText != null && PitchText != null && RollText != null)
		{
			YawText.Text = YawSlider.Value.ToString("0") + "°";
			PitchText.Text = PitchSlider.Value.ToString("0") + "°";
			RollText.Text = RollSlider.Value.ToString("0") + "°";
			SizeText.Text = SizeSlider.Value.ToString("0.0") + "x";
			LiftText.Text = LiftSlider.Value.ToString("0.#");
			MoveXText.Text = MoveXSlider.Value.ToString("0.#");
			MoveYText.Text = MoveYSlider.Value.ToString("0.#");
			RebuildPreview();
		}
	}

	private void Turn_Click(object sender, RoutedEventArgs e)
	{
		_turns = (_turns + 1) % 4;
		TurnText.Text = $"Turned {_turns * 90}°";
		RebuildPreview();
	}

	private void Flip_Click(object sender, RoutedEventArgs e)
	{
		_flip = !_flip;
		RebuildPreview();
	}

	private void EnsureReference()
	{
		if (_referenceTried || _engine == null)
		{
			return;
		}
		_referenceTried = true;
		RlSwapEngine engine = _engine;
		Task.Run(() => (engine.ReadReferenceCar(), engine.ReadReferenceBall(), engine.ReadReferenceWheel())).ContinueWith((Task<(CarMesh, CarMesh, CarMesh)> t) => Dispatcher.BeginInvoke((Action)(() =>
		{
			if (t.IsCompletedSuccessfully)
			{
				(CarMesh, CarMesh, CarMesh) result = t.Result;
				_reference = result.Item1;
				_refBall = result.Item2;
				_refWheel = result.Item3;
			}
			RebuildPreview();
		})));
	}

	private CarMesh? ReferenceFor(string slot)
	{
		if (!(slot == "Ball"))
		{
			if (!(slot == "Wheels"))
			{
				return _reference;
			}
			return _refWheel;
		}
		return _refBall;
	}

	private (float X, float Y, float Z) OctaneCentre(string slot = "Body")
	{
		CarMesh carMesh = ReferenceFor(slot);
		if (carMesh == null)
		{
			if (!(slot == "Body"))
			{
				return (X: 0f, Y: 0f, Z: 0f);
			}
			return (X: 10f, Y: 0f, Z: 16f);
		}
		(Vector3 Min, Vector3 Max) tuple = carMesh.Bounds();
		Vector3 item = tuple.Min;
		Vector3 item2 = tuple.Max;
		Vector3 vector = (item + item2) / 2f;
		return (X: vector.X, Y: vector.Y, Z: vector.Z);
	}

	private void RebuildPreview()
	{
		if (_scene == null)
		{
			return;
		}
		EnsureReference();
		int seq = ++_previewSeq;
		FbxScene scene = _scene;
		ModelFit fit = CurrentFit();
		string slot = CurrentSlot;
		List<NamedPicture> pics = _previewPics.ToList();
		CarMesh reference = ReferenceFor(slot);
		Task.Run(() =>
		{
			try
			{
				TexImage texImage = ((pics.Count > 0) ? pics[0].Image : null);
				CarMesh mesh = ((slot == "Map") ? MapGeometry.Build(scene, fit, MapCollisionMode.ModelTriangles, fit.UseMaterialColors, texImage, pics).Visual : CarMeshBuilder.Build(scene, fit, null, slot == "Ball", fit.UseMaterialColors, texImage, pics, slot == "Wheels"));
				TexImage picture = mesh.Atlas ?? texImage;
				bool car = slot != "Map" && (slot == "Body" || reference != null);
				float sa = 1f;
				float sb = 1f;
				float sc = 1f;
				byte[] a = MeshPreview.Render(mesh, PreviewView.Side, 300, picture, car, reference, (float v) =>
				{
					sa = v;
				}, slot == "Map");
				byte[] b = MeshPreview.Render(mesh, PreviewView.Top, 300, picture, car, reference, (float v) =>
				{
					sb = v;
				}, slot == "Map");
				byte[] c = MeshPreview.Render(mesh, PreviewView.Front, 300, picture, car, reference, (float v) =>
				{
					sc = v;
				}, slot == "Map");
				byte[] d = MeshPreview.Render(mesh, PreviewView.ThreeQuarter, 300, picture, car: false, null, null, slot == "Map");
				(Vector3 Min, Vector3 Max) tuple = mesh.Bounds();
				Vector3 item = tuple.Min;
				Vector3 item2 = tuple.Max;
				Vector3 mid = (item + item2) / 2f;
				Dispatcher.BeginInvoke((Action)(() =>
				{
					if (seq == _previewSeq)
					{
						_scaleA = sa;
						_scaleB = sb;
						_scaleC = sc;
						PreviewA.Source = RlCustomIcons.ToSource(a, 300);
						PreviewB.Source = RlCustomIcons.ToSource(b, 300);
						PreviewC.Source = RlCustomIcons.ToSource(c, 300);
						PreviewD.Source = RlCustomIcons.ToSource(d, 300);
						(float, float, float) tuple2 = OctaneCentre(slot);
						TextBlock previewNote = PreviewNote;
						string text = $"{mesh.TriangleCount:N0} triangles, {mesh.VertexCount:N0} vertices. ";
						string text2 = ((!(slot == "Map")) ? "" : ((mesh.VertexCount > 196605) ? "Too detailed: the map has room for about 196,000 vertices. " : "The map is stood on the ground, centred and fitted to the field (8,512 by 12,164 units). Goals are at the two short ends. Blue lines = the standard arena (floor, ceiling and both goals), so you can see how your map lines up. "));
						previewNote.Text = text + text2 + ((slot == "Map") ? "" : (car ? ($"Middle of your model vs the {((slot == "Ball") ? "standard ball" : ((slot == "Wheels") ? "stock wheel" : "Octane"))}'s middle: forward {mid.X - tuple2.Item1:+0.0;-0.0;0.0}, right {mid.Y - tuple2.Item2:+0.0;-0.0;0.0}. " + ((reference == null) ? "(Connect on the Home tab to see the real outline; a box is shown for now.) " : "") + "Blue outline = the real " + ((slot == "Ball") ? "standard ball" : ((slot == "Wheels") ? "wheel (the axle points sideways)" : "Octane")) + ", yellow dashes = its middle, pink cross = your model's middle.") : ((slot == "Wheels") ? "The wheel is centered and turned so its axle points sideways. Wheels are about 33 units across and 15 wide, a ball is about 190 across." : "The ball is centered in the picture.")));
					}
				}));
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				Dispatcher.BeginInvoke((Action)(() =>
				{
					if (seq == _previewSeq)
					{
						PreviewNote.Text = ex3.Message;
					}
				}));
			}
		});
	}

	private void ResetMove_Click(object sender, RoutedEventArgs e)
	{
		MoveXSlider.Value = 0.0;
		MoveYSlider.Value = 0.0;
		LiftSlider.Value = 0.0;
	}

	private void ResetRotation_Click(object sender, RoutedEventArgs e)
	{
		_turns = 0;
		_flip = false;
		TurnText.Text = "";
		YawSlider.Value = 0.0;
		PitchSlider.Value = 0.0;
		RollSlider.Value = 0.0;
		RebuildPreview();
	}

	private async void CenterOnOctane_Click(object sender, RoutedEventArgs e)
	{
		if (_scene == null)
		{
			return;
		}
		FbxScene scene = _scene;
		ModelFit fit = CurrentFit();
		string slot = CurrentSlot;
		List<NamedPicture> pics = _previewPics.ToList();
		if (slot == "Map")
		{
			ResetMove_Click(sender, e);
			return;
		}
		(float X, float Y, float Z) oc = OctaneCentre(slot);
		try
		{
			var (num, num2) = await Task.Run(() =>
			{
				TexImage picture = ((pics.Count > 0) ? pics[0].Image : null);
				(Vector3 Min, Vector3 Max) tuple2 = CarMeshBuilder.Build(scene, fit, null, slot == "Ball", fit.UseMaterialColors, picture, pics, slot == "Wheels").Bounds();
				Vector3 item = tuple2.Min;
				Vector3 item2 = tuple2.Max;
				Vector3 vector = (item + item2) / 2f;
				return (oc.X - vector.X, oc.Y - vector.Y);
			});
			MoveXSlider.Value = Math.Clamp(Math.Round((MoveXSlider.Value + (double)num) * 2.0) / 2.0, MoveXSlider.Minimum, MoveXSlider.Maximum);
			MoveYSlider.Value = Math.Clamp(Math.Round((MoveYSlider.Value + (double)num2) * 2.0) / 2.0, MoveYSlider.Minimum, MoveYSlider.Maximum);
			RebuildPreview();
		}
		catch (Exception ex)
		{
			ModStatus.Text = ex.Message;
		}
	}

	private void Preview_Down(object sender, MouseButtonEventArgs e)
	{
		if (sender is Image image)
		{
			_dragging = true;
			_dragFrom = e.GetPosition(image);
			_dragX = MoveXSlider.Value;
			_dragY = MoveYSlider.Value;
			_dragZ = LiftSlider.Value;
			image.CaptureMouse();
		}
	}

	private void Preview_Up(object sender, MouseButtonEventArgs e)
	{
		_dragging = false;
		(sender as Image)?.ReleaseMouseCapture();
	}

	private void Preview_Move(object sender, MouseEventArgs e)
	{
		if (!_dragging || !(sender is Image image) || e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		double num = Math.Min(image.ActualWidth, image.ActualHeight);
		if (!(num < 10.0))
		{
			Point position = e.GetPosition(image);
			double num2 = 300.0 / num;
			double px = (position.X - _dragFrom.X) * num2;
			double px2 = (position.Y - _dragFrom.Y) * num2;
			if (image == PreviewA)
			{
				MoveXSlider.Value = Math.Clamp(_dragX + Units(px, _scaleA), MoveXSlider.Minimum, MoveXSlider.Maximum);
				LiftSlider.Value = Math.Clamp(_dragZ - Units(px2, _scaleA), LiftSlider.Minimum, LiftSlider.Maximum);
			}
			else if (image == PreviewB)
			{
				MoveXSlider.Value = Math.Clamp(_dragX + Units(px, _scaleB), MoveXSlider.Minimum, MoveXSlider.Maximum);
				MoveYSlider.Value = Math.Clamp(_dragY + Units(px2, _scaleB), MoveYSlider.Minimum, MoveYSlider.Maximum);
			}
			else
			{
				MoveYSlider.Value = Math.Clamp(_dragY + Units(px, _scaleC), MoveYSlider.Minimum, MoveYSlider.Maximum);
				LiftSlider.Value = Math.Clamp(_dragZ - Units(px2, _scaleC), LiftSlider.Minimum, LiftSlider.Maximum);
			}
		}
		static double Units(double num3, float scale)
		{
			if (!((double)scale > 1E-06))
			{
				return 0.0;
			}
			return num3 / (double)scale;
		}
	}

	private async void CreateModel_Click(object sender, RoutedEventArgs e)
	{
		if (_scene == null || _busy)
		{
			return;
		}
		string name = ModelNameBox.Text.Trim();
		if (name.Length == 0)
		{
			ShowNotice("Name it", "Give the model a name first.");
			return;
		}
		FbxScene scene = _scene;
		ModelFit fit = CurrentFit();
		string slot = CurrentSlot;
		List<string> pics = _picPaths.ToList();
		bool ownWheels = slot == "Body" && KeepWheelsBox.IsChecked != true;
		MapCollisionMode mapMode = CurrentMapCollision;
		SetBusy(busy: true);
		try
		{
			string path = await Task.Run(() =>
			{
				DripRlFile dripRlFile = DripRlFile.FromScene(scene, pics, fit, name, slot, "", mapMode);
				dripRlFile.OwnWheels = ownWheels;
				return RlCustom.Add(dripRlFile);
			});
			RefreshLibrary();
			Log("Made " + System.IO.Path.GetFileName(path) + ".");
			ModStatus.Text = "Saved " + name + ". Hit Swap… on it in the list.";
			MakeOverlay.Visibility = Visibility.Collapsed;
			ShowPage(Page.Mods);
			ShowNotice("Model ready", (slot == "Map") ? (name + " is in your list. Hit Swap… to put it over Underpass (Labs). Offline play only, with anti-cheat off. Hit Undo on the Swaps tab to get the original map back.") : (name + " is in your list. Hit Swap… and pick the " + KindWord(slot) + " it should replace. Export gives you a .dripRL file to share." + (ownWheels ? " It's marked as having its own wheels, so after you swap it in the app offers to hide the game's wheels." : "")));
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Couldn't make the model", ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void ModelUse_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: RlModelVm dataContext })
		{
			if (_engine == null)
			{
				ShowNotice("Connect first", "Hit Connect on the Home tab so the app knows where Rocket League is.");
				return;
			}
			_customPick = dataContext.Entry.ToItem();
			_target = null;
			string value = KindWord(dataContext.Entry.Model.Slot);
			bool flag = dataContext.Entry.Model.Slot == "Map";
			PickTitle.Text = (flag ? ("Which map should " + dataContext.Name + " replace?") : $"Which {value} should become {dataContext.Name}?");
			PickSub.Text = (flag ? "Pick the map to put it over. The whole stock map is switched off and your model takes its place. Offline play only; the original is backed up." : $"Pick the {value} you have equipped. In game you keep using it and it shows {dataContext.Name} instead.");
			PickSearch.Text = "";
			RefreshPick();
			PickOverlay.Visibility = Visibility.Visible;
		}
	}

	private async Task ApplyMapAsync(RlItem model, RlItem target)
	{
		if (_busy || _engine == null || model.CustomFile == null)
		{
			return;
		}
		if (_keys.All.Count == 0)
		{
			ShowNotice("Keys needed", "Import your keys file in Settings first. Rocket League's packages are encrypted and the app doesn't ship the keys.");
		}
		else if (RlLocator.IsRunning() || RlLocator.IsAntiCheatRunning())
		{
			ShowNotice("Close the game first", "Custom maps only work offline with anti-cheat off, and the map's file can't change while Rocket League is open.");
		}
		else
		{
			if (Dlg.Show(this, $"Put {model.Name} over {target.Name}?\n\n" + "• OFFLINE PLAY ONLY (Freeplay, Exhibition, Training), and launch the game with Easy Anti-Cheat OFF.\n• Never go online with this on. Changed map files can get your account banned.\n• Experimental and not tested in game: it can crash, the map can look dark, and the boost pads can change.\n• The original map is backed up. Undo on the Swaps tab puts it back, or run Verify Game Files in your launcher.\n\nSurface look: " + RlMapBuilder.Describe((MapMaterial)Math.Clamp(_state.Settings.MapMaterial, 0, 3)) + ". " + (_state.Settings.MapKeepScenery ? "The stock scenery outside the arena stays." : "The whole stock map is replaced.") + " (Change these in the maker, under Map.)", "Custom map (offline only)", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) != MessageBoxResult.OK)
			{
				return;
			}
			SetBusy(busy: true);
			try
			{
				RlSwapEngine engine = _engine;
				string file = model.CustomFile;
				string targetFile = target.FileName;
				await Task.Run(() => engine.SwapMap(DripRlFile.Load(file), file, targetFile));
				RefreshSwaps();
				RefreshStrip();
				Log(model.Name + " is now over " + target.Name + ".");
				string text = ((engine.LastMapNotes.Count > 0) ? ("\n\n" + string.Join("\n", engine.LastMapNotes)) : "");
				ShowNotice("Map swapped", $"{model.Name} is over {target.Name}. Start Rocket League with anti-cheat off, go offline and load {target.Name} in Freeplay or Exhibition. Stay offline: modified files can get your account banned.\n\nIf it crashes or looks wrong, tell me what you saw (dark map? no collision? falls through the floor?) and hit Undo on the Swaps tab." + text);
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message);
				ShowNotice("Couldn't swap the map", ex.Message);
			}
			finally
			{
				SetBusy(busy: false);
			}
		}
	}

	private void ModelWheels_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: RlModelVm dataContext }))
		{
			return;
		}
		try
		{
			DripRlFile model = dataContext.Entry.Model;
			model.OwnWheels = !model.OwnWheels;
			model.Save(dataContext.Entry.Path);
			Log(model.Name + ": Rocket League wheels " + (model.OwnWheels ? "off (the model has its own)" : "on") + ".");
			RefreshLibrary();
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't change that", ex.Message);
		}
	}

	private void ModelExport_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: RlModelVm dataContext }))
		{
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Export model",
			Filter = "Drip model (*.dripRL)|*.dripRL",
			FileName = RlCustom.SafeName(dataContext.Name) + ".dripRL"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			File.Copy(dataContext.Entry.Path, saveFileDialog.FileName, overwrite: true);
			Log("Exported " + System.IO.Path.GetFileName(saveFileDialog.FileName) + ".");
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't export", ex.Message);
		}
	}

	private void ModelDelete_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: var dataContext }))
		{
			return;
		}
		RlModelVm vm = dataContext as RlModelVm;
		if (vm != null)
		{
			if (_state.Swaps.Any((RlSwapRecord s) => s.Custom && string.Equals(s.CustomFile, vm.Entry.Path, StringComparison.OrdinalIgnoreCase)))
			{
				ShowNotice("It's in use", vm.Name + " is swapped in right now. Revert that swap on the Swaps tab first.");
				return;
			}
			RlCustom.Delete(vm.Entry.Path);
			RefreshLibrary();
		}
	}

	private void AddDripRl_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick a .dripRL file",
			Filter = "Drip model (*.dripRL)|*.dripRL",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			string[] fileNames = openFileDialog.FileNames;
			foreach (string file in fileNames)
			{
				ImportDripRl(file);
			}
		}
	}

	private void ImportDripRl(string file)
	{
		try
		{
			RlCustom.Entry entry = RlCustom.Import(file);
			RefreshLibrary();
			Log("Added " + entry.Model.Name + "." + (entry.Model.OwnWheels ? " It has its own wheels, so you will be offered to hide the game wheels when you swap it in." : ""));
			ShowPage(Page.Mods);
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't add that model", ex.Message);
		}
	}

	private bool HandleModDrop(string[] files)
	{
		bool result = false;
		List<string> list = new List<string>();
		string text = null;
		foreach (string text2 in files)
		{
			string text3 = System.IO.Path.GetExtension(text2).ToLowerInvariant();
			if (text3 == ".driprl")
			{
				ImportDripRl(text2);
				result = true;
				continue;
			}
			if (ModelFileReader.IsModelFile(text2))
			{
				text = text2;
				continue;
			}
			bool flag;
			switch (text3)
			{
			case ".png":
			case ".jpg":
			case ".jpeg":
			case ".bmp":
			case ".tga":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag && (_page == Page.Mods || MakeOverlay.Visibility == Visibility.Visible))
			{
				list.Add(text2);
			}
		}
		if (text != null)
		{
			ShowPage(Page.Mods);
			MakeOverlay.Visibility = Visibility.Visible;
			LoadFbxAsync(text);
			result = true;
		}
		if (list.Count > 0)
		{
			MakeOverlay.Visibility = Visibility.Visible;
			SetPictures(list);
			result = true;
		}
		return result;
	}

	public RocketLeagueWindow()
	{
		InitializeComponent();
		Theme.SetGame(Game.RocketLeague);
		Shell.Setup(this, 1040.0, 640.0);
		AutoRevertBox.IsChecked = _state.Settings.RevertWhenGameCloses;
		ResizeBox.IsChecked = _state.Settings.AllowResizing;
		TexModeBox.SelectedIndex = Math.Clamp(_state.Settings.CustomTextures, 0, 4);
		RlCustomBuilder.Mode = (CustomTextureMode)TexModeBox.SelectedIndex;
		WheelModeBox.SelectedIndex = Math.Clamp(_state.Settings.CustomWheels, 0, 3);
		RlCustomBuilder.WheelMode = (WheelTextureMode)WheelModeBox.SelectedIndex;
		string path = _state.Settings.CookedDirectory;
		if (!Directory.Exists(path))
		{
			path = RlLocator.FindCookedDirectory() ?? "";
		}
		PathBox.Text = path;
		Log((path.Length > 0) ? "Found Rocket League. Hit Connect." : "Couldn't find Rocket League automatically. Browse to TAGame\\CookedPCConsole.");
		AutoLoadKeys();
		InitMods();
		FunCtl.Attach("rocketleague", FunHub.Store, CurrentFitEntries, ApplyFitAsync, ShowNotice);
		ShowPage(Page.Home);
		RefreshKeysUi();
		RefreshSwaps();
		_watch.Tick += (object? _, EventArgs _) =>
		{
			WatchGame();
		};
		_watch.Start();
		StartTweakTimer();
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			if (path.Length > 0)
			{
				await ConnectAsync();
			}
		};
		Closed += (object? _, EventArgs _) =>
		{
			_watch.Stop();
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
		if (e.Key == Key.Escape && (NoticeOverlay.Visibility == Visibility.Visible || PickOverlay.Visibility == Visibility.Visible))
		{
			CloseOverlays();
			e.Handled = true;
		}
	}

	private void Tab_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Page>(tag, out var result))
		{
			ShowPage(result);
		}
	}

	private void HomeTile_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Page>(tag, out var result))
		{
			if (result == Page.Items)
			{
				Start_Click(sender, e);
			}
			else
			{
				ShowPage(result);
			}
		}
	}

	private void Start_Click(object sender, RoutedEventArgs e)
	{
		if (!_connected)
		{
			ShowNotice("Let's get set up", "The app needs to find Rocket League first. Use the Find game button on the Home page.");
		}
		else if (_keys.All.Count == 0)
		{
			ShowNotice("One more thing", "Add your keys file once (the Add keys file button on the Home page). After that, you're ready to swap.");
		}
		else
		{
			ShowPage(Page.Items);
		}
	}

	private void RefreshSetup()
	{
		bool flag = PathBox.Text.Trim().Length > 0;
		Ellipse setupGameDot = SetupGameDot;
		TextBlock setupGameText = SetupGameText;
		bool connected = _connected;
		string message;
		if (_connected)
		{
			message = "Found your game.";
		}
		else
		{
			message = (flag ? "Couldn't open that folder. Press Find game and pick your Rocket League folder (Epic or Steam)." : "Couldn't find Rocket League by itself. Press Find game and pick your Rocket League folder (Epic or Steam).");
		}
		Row(setupGameDot, setupGameText, connected, message);
		SetupGameBtn.Visibility = (_connected ? Visibility.Collapsed : Visibility.Visible);
		int count = _keys.All.Count;
		Row(SetupKeysDot, SetupKeysText, count > 0, (count > 0) ? "Keys added." : "Rocket League locks its files, so the app needs a keys file one time. Press Add keys file and pick it. (The app doesn't come with one.)");
		SetupKeysBtn.Visibility = ((count > 0) ? Visibility.Collapsed : Visibility.Visible);
		bool flag2 = _connected && count > 0;
		Row(SetupReadyDot, SetupReadyText, flag2, flag2 ? "All set! Press Start swapping." : "Do steps 1 and 2 first.");
		StartBtn.Content = (flag2 ? "Start swapping" : "Finish setup");
		SetupCard.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
		void Row(Ellipse dot, TextBlock text, bool ok, string text2)
		{
			dot.Fill = (Brush)FindResource(ok ? "Good" : "Danger");
			text.Text = text2;
		}
	}

	private async void SetupGame_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Pick the Rocket League folder (Epic or Steam; the game folder or its CookedPCConsole folder both work)"
		};
		if (openFolderDialog.ShowDialog() == true)
		{
			PathBox.Text = RlLocator.Resolve(openFolderDialog.FolderName);
			await ConnectAsync();
		}
	}

	private void SetupKeys_Click(object sender, RoutedEventArgs e)
	{
		ImportKeys_Click(sender, e);
	}

	private void ShowPage(Page page)
	{
		_page = page;
		CloseOverlays();
		HomePage.Visibility = ((page != Page.Home) ? Visibility.Collapsed : Visibility.Visible);
		ItemsPage.Visibility = ((page != Page.Items) ? Visibility.Collapsed : Visibility.Visible);
		ModsPage.Visibility = ((page != Page.Mods) ? Visibility.Collapsed : Visibility.Visible);
		SwapsPage.Visibility = ((page != Page.Swaps) ? Visibility.Collapsed : Visibility.Visible);
		LivePage.Visibility = ((page != Page.Live) ? Visibility.Collapsed : Visibility.Visible);
		FunPage.Visibility = ((page != Page.Fun) ? Visibility.Collapsed : Visibility.Visible);
		SettingsPage.Visibility = ((page != Page.Settings) ? Visibility.Collapsed : Visibility.Visible);
		SearchWrap.Visibility = ((page != Page.Items) ? Visibility.Collapsed : Visibility.Visible);
		TabHome.IsChecked = page == Page.Home;
		TabItems.IsChecked = page == Page.Items;
		TabMods.IsChecked = page == Page.Mods;
		TabSwaps.IsChecked = page == Page.Swaps;
		TabLive.IsChecked = page == Page.Live;
		TabFun.IsChecked = page == Page.Fun;
		TabSettings.IsChecked = page == Page.Settings;
		(PageTitle.Text, PageSub.Text) = page switch
		{
			Page.Items => ("Swap an item", "Click the item you use, then the one you want to look like."), 
			Page.Mods => ("My models", "Your own 3D cars, wheels, balls and maps."), 
			Page.Swaps => ("What's swapped", "Everything that looks different right now."), 
			Page.Live => ("Extras", "Cinematic look, car and ball size, physics, slow-mo, boost and more. Freeplay only."), 
			Page.Fun => ("Loadouts", "Save what's swapped and bring it back in one click."), 
			Page.Settings => ("Settings", "Keys, backups, game folder and extras."), 
			_ => ("Start here", "Get set up, then start swapping."), 
		};
		if (page == Page.Items)
		{
			RefreshGrid();
		}
		if (page == Page.Swaps)
		{
			RefreshSwaps();
		}
		if (page == Page.Live)
		{
			RefreshLive();
			RefreshLook();
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
			string text = DiagnosticsHelper.Save("Rocket League");
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
		PickOverlay.Visibility = Visibility.Collapsed;
	}

	private void CloseOverlays_Click(object sender, RoutedEventArgs e)
	{
		CloseOverlays();
	}

	private void Backdrop_Click(object sender, MouseButtonEventArgs e)
	{
		CloseOverlays();
	}

	private void Swallow_Click(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void Log(string message)
	{
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
		}
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		BusyBar.Visibility = ((!busy) ? Visibility.Hidden : Visibility.Visible);
		ConnectBtn.IsEnabled = !busy;
	}

	private void Browse_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Pick the Rocket League folder (Epic or Steam)"
		};
		if (openFolderDialog.ShowDialog() == true)
		{
			PathBox.Text = RlLocator.Resolve(openFolderDialog.FolderName);
		}
	}

	private async void Connect_Click(object sender, RoutedEventArgs e)
	{
		await ConnectAsync();
	}

	private async Task ConnectAsync()
	{
		if (_busy)
		{
			return;
		}
		SetBusy(busy: true);
		try
		{
			string dir = RlLocator.Resolve(PathBox.Text.Trim());
			PathBox.Text = dir;
			RlLocator.Validate(dir);
			_state.Settings.CookedDirectory = dir;
			_state.Save();
			_keys = RlKeys.LoadSaved() ?? new RlKeys(Array.Empty<byte[]>());
			_engine = new RlSwapEngine(dir, _keys, _state)
			{
				Log = Log
			};
			Log("Reading your game files...");
			_files = await Task.Run(() => new HashSet<string>(Directory.EnumerateFiles(dir, "*_SF.upk").Concat(Directory.EnumerateFiles(dir, "*_P.upk")).Select(System.IO.Path.GetFileName), StringComparer.OrdinalIgnoreCase));
			await Task.Run(() =>
			{
				_engine.HandleChanges();
			});
			Log("Getting the item list...");
			_items = await RlCatalog.LoadAsync();
			_items.AddRange(RlCustom.DiscoverBalls(_files));
			_items.AddRange(from f in RlMapCatalog.Discover(_files)
				select new RlItem(-3, RlMapCatalog.Pretty(f), "Map", "Common", System.IO.Path.GetFileNameWithoutExtension(f), System.IO.Path.GetFileNameWithoutExtension(f), Multi: false));
			int value = Enumerable.Count(_items, (RlItem i) => IsSwappable(i) && i.Slot != "Map");
			Log($"Connected. {value} items you can swap.");
			_connected = true;
			StatStatus.Text = "Connected";
			StatusDot.Fill = (Brush)FindResource("Good");
			StatItems.Text = value.ToString();
			RefreshKeysUi();
			RefreshSwaps();
			RefreshGrid();
		}
		catch (Exception ex)
		{
			_connected = false;
			StatStatus.Text = "Not connected";
			StatusDot.Fill = (Brush)FindResource("Danger");
			Log("ERROR: " + ex.Message);
			RefreshSetup();
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private bool IsSwappable(RlItem i)
	{
		if (!i.Multi && (RlCatalog.SwappableSlots.Contains(i.Slot) || i.Slot == "Map"))
		{
			return _files.Contains(i.FileName);
		}
		return false;
	}

	private void RefreshKeysUi()
	{
		int count = _keys.All.Count;
		StatKeys.Text = ((count == 0) ? "None yet" : (count + " loaded"));
		KeysText.Text = ((count == 0) ? "Rocket League's files are locked, and this app doesn't come with the keys. Add a keys file once (a keys.txt with one key per line, or a table with a key column) and you're set. Then use Test my keys to make sure they open your game files." : $"{count} keys added. Use Test my keys to make sure they open your game files.");
		if (IsLoaded || SetupKeysDot != null)
		{
			RefreshSetup();
		}
	}

	private void Slot_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag })
		{
			_slot = tag;
			RefreshGrid();
		}
	}

	private void Search_Changed(object sender, TextChangedEventArgs e)
	{
		SearchHint.Visibility = ((SearchBox.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		RefreshGrid();
	}

	private bool IsFav(RlItem i)
	{
		return FunHub.Store.IsFavorite("rocketleague", i.AssetPackage);
	}

	private IEnumerable<RlItem> Matching(string slot, string query)
	{
		return _items.Where((RlItem i) => i.Slot == slot && IsSwappable(i) && (query.Length == 0 || i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) && (FavOnlyBox.IsChecked != true || IsFav(i))).OrderByDescending(IsFav).ThenBy((RlItem i) => i.Name, StringComparer.OrdinalIgnoreCase);
	}

	private void FavOnly_Changed(object sender, RoutedEventArgs e)
	{
		if (IsLoaded)
		{
			RefreshGrid();
			if (PickOverlay.Visibility == Visibility.Visible)
			{
				RefreshPick();
			}
		}
	}

	private void Fav_RightClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: RlItemVm dataContext } && !dataContext.Item.IsCustom)
		{
			FunHub.Store.ToggleFavorite("rocketleague", dataContext.Item.AssetPackage);
			RefreshGrid();
			if (PickOverlay.Visibility == Visibility.Visible)
			{
				RefreshPick();
			}
			e.Handled = true;
		}
	}

	private async void Surprise_Click(object sender, RoutedEventArgs e)
	{
		if (!_busy && !(_target == null) && !(_customPick != null))
		{
			IEnumerable<RlItemVm> obj = PickHost.ItemsSource as IEnumerable<RlItemVm>;
			RlItem rlItem = Surprise.Pick(((obj != null) ? (from v in obj
				where !v.Item.IsCustom
				select v.Item).ToList() : null) ?? new List<RlItem>(), (RlItem _) => true, (RlItem i) => i.AssetPackage, _target.AssetPackage);
			if (rlItem == null)
			{
				ShowNotice("Nothing to pick", "There's nothing in the list to choose from. Clear the search first.");
			}
			else
			{
				await PerformSwapAsync(_target, rlItem);
			}
		}
	}

	private void RefreshGrid()
	{
		List<RlItem> list = Matching(_slot, SearchBox.Text.Trim()).ToList();
		List<RlItemVm> itemsSource = (from i in list.Take(150)
			select new RlItemVm(i)).ToList();
		GridHost.ItemsSource = itemsSource;
		EmptyText.Visibility = ((_items.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		EmptyText.Text = ((_items.Count == 0) ? "Connect to Rocket League on the Home tab first." : "");
		TextBlock countText = CountText;
		string text;
		if (_items.Count != 0)
		{
			text = ((list.Count > 150) ? $"Showing {150} of {list.Count}. Search to find the rest." : $"{list.Count} items");
		}
		else
		{
			text = "";
		}
		countText.Text = text;
	}

	private void ItemTile_Click(object sender, MouseButtonEventArgs e)
	{
		if (!_busy && sender is FrameworkElement { DataContext: RlItemVm dataContext })
		{
			_target = dataContext.Item;
			_customPick = null;
			PickTitle.Text = "What should " + dataContext.Item.Name + " look like?";
			PickSub.Text = "Pick the one you want to see. In game you keep using " + dataContext.Item.Name + " and it shows this one instead." + (RlCatalog.ExperimentalSlots.Contains(dataContext.Item.Slot) ? " Car swaps are still new: if the game won't start, hit Undo all swaps." : "");
			PickSearch.Text = "";
			RefreshPick();
			PickOverlay.Visibility = Visibility.Visible;
		}
	}

	private void PickSearch_Changed(object sender, TextChangedEventArgs e)
	{
		RefreshPick();
	}

	private void RefreshPick()
	{
		string q = PickSearch.Text.Trim();
		if (_customPick != null)
		{
			PickHost.ItemsSource = (from i in Matching(_customPick.Slot, q).Take(150)
				select new RlItemVm(i)).ToList();
		}
		else if (!(_target == null))
		{
			List<RlItem> source = (from i in Matching(_target.Slot, q)
				where i.AssetPackage != _target.AssetPackage
				select i).ToList();
			IEnumerable<RlItemVm> first = from c in _library
				where c.Model.Slot == _target.Slot && (q.Length == 0 || c.Model.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
				select new RlItemVm(c.ToItem());
			PickHost.ItemsSource = first.Concat(from i in source.Take(150)
				select new RlItemVm(i)).ToList();
		}
	}

	private async void DonorTile_Click(object sender, MouseButtonEventArgs e)
	{
		if (!_busy && sender is FrameworkElement { DataContext: RlItemVm dataContext })
		{
			RlItem rlItem = ((_customPick != null) ? dataContext.Item : _target);
			RlItem look = _customPick ?? dataContext.Item;
			if (!(rlItem == null))
			{
				await PerformSwapAsync(rlItem, look);
			}
		}
	}

	private async Task PerformSwapAsync(RlItem picked, RlItem look)
	{
		if (_engine == null)
		{
			ShowNotice("Connect first", "Hit Connect on the Home tab so the app knows where Rocket League is.");
			return;
		}
		if (_keys.All.Count == 0)
		{
			ShowNotice("Keys needed", "Import your keys file in Settings first. Rocket League's packages are encrypted and the app doesn't ship the keys.");
			return;
		}
		if (picked.Slot == "Map" && look.IsCustom)
		{
			CloseOverlays();
			_customPick = null;
			await ApplyMapAsync(look, picked);
			return;
		}
		CloseOverlays();
		SetBusy(busy: true);
		try
		{
			bool thumbs = ThumbBox.IsChecked == true;
			RlSwapEngine engine = _engine;
			await Task.Run(() => engine.Swap(picked, look, thumbs));
			_customPick = null;
			RefreshSwaps();
			RefreshStrip();
			Log(picked.Name + " now looks like " + look.Name + ".");
			bool flag = picked.Slot == "Wheels";
			if (flag)
			{
				flag = await Task.Run(() => engine.WheelFileIsShell(picked));
			}
			bool flag2 = flag;
			ShowNotice("Swapped", picked.Name + " will look like " + look.Name + ". Open Rocket League and check your garage or freeplay. Stay offline: modified files can get your account banned in online play." + (flag2 ? ("\n\nHeads up: " + picked.Name + " looks like one of the stock wheels. Its file has no wheel model inside (the real one is stored somewhere else), so the game may keep showing the old wheels. If nothing changes, equip a different wheel in the garage (any non-stock one) and swap that instead.") : ""));
			if (look.IsCustom && look.Slot == "Body" && look.CustomFile != null && DripRlFile.Load(look.CustomFile).OwnWheels)
			{
				OfferHideWheels(look.Name);
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Couldn't swap", ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void OfferHideWheels(string modelName)
	{
		if (Dlg.Show(this, modelName + " has its own wheels built in.\n\nHide Rocket League's wheels so they don't show up on top of them? You pick the wheels you have equipped and they turn invisible. Choose No to keep the game's wheels.\n\nWheels are separate from cars in the game, so they stay hidden on every car until you press Undo on that swap.", "Hide the game's wheels?", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			RlCustom.Entry entry = RlCustom.EnsureInvisibleWheels();
			RefreshLibrary();
			_customPick = entry.ToItem();
			_target = null;
			PickTitle.Text = "Which wheels do you have equipped?";
			PickSub.Text = "Pick the wheels on your car. In game you keep using them and they turn invisible.";
			PickSearch.Text = "";
			RefreshPick();
			PickOverlay.Visibility = Visibility.Visible;
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Couldn't hide the wheels", ex.Message);
		}
	}

	private void RefreshSwaps()
	{
		List<RlSwapVm> list = _state.Swaps.Select((RlSwapRecord r) => new RlSwapVm(r)).ToList();
		SwapsHost.ItemsSource = list;
		SwapsEmptyBox.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		SwapsHint.Text = ((list.Count == 0) ? "" : "Your swaps stay in place until you revert them.");
		StatSwaps.Text = list.Count.ToString();
		RefreshStrip();
	}

	private async void SwapRevert_Click(object sender, RoutedEventArgs e)
	{
		if (_busy || _engine == null || !(sender is FrameworkElement { DataContext: RlSwapVm dataContext }))
		{
			return;
		}
		RlSwapEngine engine = _engine;
		RlSwapRecord rec = dataContext.Record;
		bool live = rec.Live && _liveUnlocked;
		await RunRevertAsync(() =>
		{
			engine.AllowWhileRunning = live;
			try
			{
				return engine.Revert(rec) ? 1 : (-1);
			}
			finally
			{
				engine.AllowWhileRunning = false;
			}
		});
	}

	private async void RevertAll_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		if (_engine == null)
		{
			ShowNotice("Nothing to revert", "Connect to Rocket League first.");
			return;
		}
		await RunRevertAsync(() => _engine.RevertAll());
	}

	private async Task RunRevertAsync(Func<int> work)
	{
		SetBusy(busy: true);
		try
		{
			Log((await Task.Run(work) < 0) ? "A backup was missing, so run Verify Game Files in your launcher to repair that item." : "Put the original files back.");
			RefreshSwaps();
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Couldn't revert", ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void ImportKeys_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick your keys file",
			Filter = "Key files (*.txt;*.csv)|*.txt;*.csv|All files (*.*)|*.*"
		};
		string found = RlKeys.FindCandidate();
		IReadOnlyList<string> readOnlyList = RlKeys.AppKeyFolders();
		if (found != null && readOnlyList.Any((string r) => string.Equals(r, System.IO.Path.GetDirectoryName(found), StringComparison.OrdinalIgnoreCase)))
		{
			openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(found);
		}
		else if (Directory.Exists(readOnlyList[1]))
		{
			openFileDialog.InitialDirectory = readOnlyList[1];
			found = null;
		}
		else if (Directory.Exists(_state.Settings.LastKeysFolder))
		{
			openFileDialog.InitialDirectory = _state.Settings.LastKeysFolder;
			found = null;
		}
		else if (found != null)
		{
			openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(found);
		}
		else
		{
			openFileDialog.InitialDirectory = AppContext.BaseDirectory;
		}
		if (found != null)
		{
			openFileDialog.FileName = System.IO.Path.GetFileName(found);
		}
		if (openFileDialog.ShowDialog() == true)
		{
			ImportKeysFrom(openFileDialog.FileName);
		}
	}

	private void AutoLoadKeys()
	{
		try
		{
			string text = RlKeys.FindAutoLoad(_keys.All.Count > 0);
			if (text != null)
			{
				_keys = RlKeys.Import(text);
				_state.Settings.LastKeysFolder = System.IO.Path.GetDirectoryName(text) ?? "";
				_state.Save();
				Log($"Found {System.IO.Path.GetFileName(text)} in {System.IO.Path.GetDirectoryName(text)} and loaded {_keys.All.Count} keys.");
			}
		}
		catch (Exception ex)
		{
			Log("Couldn't load a keys file automatically: " + ex.Message);
		}
	}

	private void ImportKeysFrom(string file)
	{
		try
		{
			_keys = RlKeys.Import(file);
			_state.Settings.LastKeysFolder = System.IO.Path.GetDirectoryName(file) ?? "";
			_state.Save();
			if (_engine != null)
			{
				_engine = new RlSwapEngine(_engine.CookedDirectory, _keys, _state)
				{
					Log = Log
				};
			}
			RefreshKeysUi();
			Log($"Imported {_keys.All.Count} keys.");
			CheckKeysAsync();
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't import", ex.Message);
		}
	}

	private void Window_Drop(object sender, DragEventArgs e)
	{
		if ((!(e.Data.GetData(DataFormats.FileDrop) is string[] files) || !HandleModDrop(files)) && e.Data.GetData(DataFormats.FileDrop) is string[] source)
		{
			string text = source.FirstOrDefault((string f) => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
			if (text != null)
			{
				ImportKeysFrom(text);
			}
		}
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private async void CheckKeys_Click(object sender, RoutedEventArgs e)
	{
		await CheckKeysAsync();
	}

	private async Task CheckKeysAsync()
	{
		if (_busy)
		{
			return;
		}
		if (_engine == null)
		{
			ShowNotice("Connect first", "Hit Connect on the Home tab, then check your keys.");
			return;
		}
		if (_keys.All.Count == 0)
		{
			ShowNotice("No keys yet", "Import a keys file first.");
			return;
		}
		SetBusy(busy: true);
		try
		{
			List<string> samples = (from i in _items.Where(IsSwappable).Take(8)
				select i.FileName).ToList();
			RlKeys keys = _keys;
			string dir = _engine.CookedDirectory;
			List<(string, bool, string)> list = await Task.Run(() => samples.Select((string f) =>
			{
				try
				{
					return (f: f, ok: true, msg: RlSwapEngine.SelfTest(File.ReadAllBytes(System.IO.Path.Combine(dir, f)), keys));
				}
				catch (Exception ex2)
				{
					return (f: f, ok: false, msg: ex2.Message);
				}
			}).ToList());
			int num = Enumerable.Count(list, ((string f, bool ok, string msg) r) => r.ok);
			foreach (var item in list)
			{
				Log(item.Item1 + ": " + (item.Item2 ? "OK, " : "") + item.Item3);
			}
			if (num == list.Count && num > 0)
			{
				ShowNotice("Keys work", $"Your keys opened and rebuilt all {num} test packages. You're good to swap.");
			}
			else if (num > 0)
			{
				ShowNotice("Some keys work", $"{num} of {list.Count} test packages opened. Swaps will work for those items; check the log for the rest.");
			}
			else
			{
				ShowNotice("Keys don't match", "None of your keys opened the test packages, or rebuilding them failed. The log on the Settings tab has the reason. Try a newer keys file.");
			}
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void Setting_Changed(object sender, RoutedEventArgs e)
	{
		_state.Settings.RevertWhenGameCloses = AutoRevertBox.IsChecked == true;
		_state.Settings.AllowResizing = ResizeBox.IsChecked == true;
		if (TexModeBox.SelectedIndex >= 0)
		{
			_state.Settings.CustomTextures = TexModeBox.SelectedIndex;
			RlCustomBuilder.Mode = (CustomTextureMode)TexModeBox.SelectedIndex;
		}
		if (WheelModeBox.SelectedIndex >= 0)
		{
			_state.Settings.CustomWheels = WheelModeBox.SelectedIndex;
			RlCustomBuilder.WheelMode = (WheelTextureMode)WheelModeBox.SelectedIndex;
		}
		_state.Save();
	}

	private void WatchGame()
	{
		bool flag = RlLocator.IsRunning();
		if (_wasRunning && !flag && _state.Settings.RevertWhenGameCloses && _state.Swaps.Count > 0 && _engine != null && !_busy)
		{
			RunRevertAsync(() => _engine.RevertAll());
			Log("Rocket League closed, so the original files are being put back.");
		}
		_wasRunning = flag;
		RefreshStrip();
		RefreshLive();
		AutoOffLive();
	}

	private void StartTweakTimer()
	{
		_tweakTimer.Tick += (object? _, EventArgs _) =>
		{
			TweakStatus.Text = ((LiveTweaks.Status == "Off.") ? "" : LiveTweaks.Status);
			string report = LiveTweaks.Report;
			if (report.Length > 0 && report != _tweakReportShown)
			{
				_tweakReportShown = report;
				TweakReport.Text = report;
				TweakReport.Visibility = Visibility.Visible;
			}
		};
		_tweakTimer.Start();
		Closed += (object? _, EventArgs _) =>
		{
			_tweakTimer.Stop();
			LiveTweaks.Shutdown();
		};
	}

	private bool TweakReady()
	{
		if (_liveUnlocked)
		{
			return true;
		}
		ShowNotice("Locked", "Open Rocket League without anti-cheat first. These only work in freeplay, training and bot matches.");
		return false;
	}

	private static string Mult(double v)
	{
		if (!(Math.Abs(v - 1.0) < 0.001))
		{
			return $"{v:0.0}x";
		}
		return "Normal";
	}

	private void TweakSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!(sender is Slider slider) || TweakSpeedText == null)
		{
			return;
		}
		TweakSpeedText.Text = Mult(slider.Value);
		if (!_tweakSilent && IsLoaded)
		{
			if (!_liveUnlocked)
			{
				_tweakSilent = true;
				TweakSpeed.Value = 1.0;
				TweakSpeedText.Text = "Normal";
				_tweakSilent = false;
				TweakReady();
			}
			else
			{
				LiveTweaks.SetCarSpeed((float)slider.Value);
			}
		}
	}

	private void TweakJump_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!(sender is Slider slider) || TweakJumpText == null)
		{
			return;
		}
		TweakJumpText.Text = Mult(slider.Value);
		if (!_tweakSilent && IsLoaded)
		{
			if (!_liveUnlocked)
			{
				_tweakSilent = true;
				TweakJump.Value = 1.0;
				TweakJumpText.Text = "Normal";
				_tweakSilent = false;
				TweakReady();
			}
			else
			{
				LiveTweaks.SetJumpScale((float)slider.Value);
			}
		}
	}

	private (Slider Slider, TextBlock Text)[] FunSliders()
	{
		return new (Slider, TextBlock)[5]
		{
			(FunCarSize, FunCarSizeText),
			(FunBallSize, FunBallSizeText),
			(FunBallWeight, FunBallWeightText),
			(FunBallBounce, FunBallBounceText),
			(FunGameSpeed, FunGameSpeedText)
		};
	}

	private void FunPreset_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag } && TweakReady())
		{
			double[] array = tag switch
			{
				"tiny" => new double[5] { 0.5, 1.0, 1.0, 1.0, 1.0 }, 
				"giant" => new double[5] { 3.0, 1.0, 1.0, 1.0, 1.0 }, 
				"moon" => new double[5] { 1.0, 1.0, 0.3, 1.5, 1.0 }, 
				"beach" => new double[5] { 1.0, 2.5, 0.4, 1.0, 1.0 }, 
				"slowmo" => new double[5] { 1.0, 1.0, 1.0, 1.0, 0.5 }, 
				_ => new double[5] { 1.0, 1.0, 1.0, 1.0, 1.0 }, 
			};
			(Slider, TextBlock)[] array2 = FunSliders();
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].Item1.Value = Math.Clamp(array[i], array2[i].Item1.Minimum, array2[i].Item1.Maximum);
				array2[i].Item2.Text = Mult(array2[i].Item1.Value);
			}
			LiveTweaks.SetCarSize((float)array[0]);
			LiveTweaks.SetBallSize((float)array[1]);
			LiveTweaks.SetBallWeight((float)array[2]);
			LiveTweaks.SetBallBounce((float)array[3]);
			LiveTweaks.SetGameSpeed((float)array[4]);
		}
	}

	private void FunSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		Slider sl = sender as Slider;
		if (sl == null || FunSliders().Any(((Slider Slider, TextBlock Text) p) => p.Slider == null || p.Text == null))
		{
			return;
		}
		(Slider, TextBlock) tuple = FunSliders().First(((Slider Slider, TextBlock Text) p) => p.Slider == sl);
		tuple.Item2.Text = Mult(sl.Value);
		if (_tweakSilent || !IsLoaded)
		{
			return;
		}
		if (!_liveUnlocked)
		{
			_tweakSilent = true;
			sl.Value = 1.0;
			tuple.Item2.Text = "Normal";
			_tweakSilent = false;
			TweakReady();
			return;
		}
		float num = (float)sl.Value;
		switch (sl.Tag as string)
		{
		case "carsize":
			LiveTweaks.SetCarSize(num);
			break;
		case "ballsize":
			LiveTweaks.SetBallSize(num);
			break;
		case "ballweight":
			LiveTweaks.SetBallWeight(num);
			break;
		case "ballbounce":
			LiveTweaks.SetBallBounce(num);
			break;
		case "gamespeed":
			LiveTweaks.SetGameSpeed(num);
			break;
		}
	}

	private void TweakCantLose_Click(object sender, RoutedEventArgs e)
	{
		if (!TweakReady())
		{
			TweakCantLose.IsChecked = false;
			return;
		}
		LiveTweaks.SetMyTeam((TeamOrange.IsChecked == true) ? 1 : 0);
		LiveTweaks.SetCantLose(TweakCantLose.IsChecked == true);
	}

	private void MyTeam_Click(object sender, RoutedEventArgs e)
	{
		LiveTweaks.SetMyTeam((TeamOrange.IsChecked == true) ? 1 : 0);
	}

	private void TweakJumps_Click(object sender, RoutedEventArgs e)
	{
		if (!TweakReady())
		{
			TweakJumps.IsChecked = false;
		}
		else
		{
			LiveTweaks.SetUnlimitedJumps(TweakJumps.IsChecked == true);
		}
	}

	private void TweakBoost_Click(object sender, RoutedEventArgs e)
	{
		if (!TweakReady())
		{
			TweakBoost.IsChecked = false;
		}
		else
		{
			LiveTweaks.SetUnlimitedBoost(TweakBoost.IsChecked == true);
		}
	}

	private void TweakSetTime_Click(object sender, RoutedEventArgs e)
	{
		if (TweakReady())
		{
			if (!int.TryParse(TweakMinutes.Text.Trim(), out var result) || result < 1 || result > 999)
			{
				ShowNotice("Time limit", "Type a number of minutes from 1 to 999.");
			}
			else
			{
				LiveTweaks.SetMinutes(result);
			}
		}
	}

	private void TweakReport_Click(object sender, RoutedEventArgs e)
	{
		if (TweakReady())
		{
			_tweakReportShown = "";
			LiveTweaks.RequestReport();
		}
	}

	private void TweakReset_Click(object sender, RoutedEventArgs e)
	{
		ResetTweakUi();
		LiveTweaks.ResetAll();
	}

	private void ResetTweakUi()
	{
		_tweakSilent = true;
		TweakSpeed.Value = 1.0;
		TweakJump.Value = 1.0;
		(Slider, TextBlock)[] array = FunSliders();
		for (int i = 0; i < array.Length; i++)
		{
			(Slider, TextBlock) tuple = array[i];
			Slider item = tuple.Item1;
			TextBlock item2 = tuple.Item2;
			item.Value = 1.0;
			item2.Text = "Normal";
		}
		TweakSpeedText.Text = "Normal";
		TweakJumpText.Text = "Normal";
		TweakCantLose.IsChecked = false;
		TeamBlue.IsChecked = true;
		TweakJumps.IsChecked = false;
		TweakBoost.IsChecked = false;
		TweakMinutes.Text = "5";
		_tweakSilent = false;
	}

	private void RefreshLive()
	{
		bool flag = RlLocator.IsRunning();
		bool flag2 = flag && RlLocator.IsAntiCheatRunning();
		_liveUnlocked = flag && !flag2 && _connected && _engine != null;
		if (!_liveUnlocked && LiveTweaks.Running)
		{
			LiveTweaks.Shutdown();
			ResetTweakUi();
		}
		LiveBody.IsEnabled = _liveUnlocked;
		LiveBody.Opacity = (_liveUnlocked ? 1.0 : 0.45);
		if (flag2)
		{
			LiveDot.Fill = (Brush)FindResource("Danger");
			LiveTitle.Text = "Locked: anti-cheat is on";
			LiveText.Text = "These extras are switched off while anti-cheat is on, and the app puts back anything it changed. To use them, close Rocket League and start it without anti-cheat (offline play only).";
		}
		else if (!flag)
		{
			LiveDot.Fill = (Brush)FindResource("Warn");
			LiveTitle.Text = "Open Rocket League first";
			LiveText.Text = "Start Rocket League without anti-cheat (your launcher has an offline option). This page unlocks by itself when the game is open. Stay out of online matches.";
		}
		else if (!_connected || _engine == null)
		{
			LiveDot.Fill = (Brush)FindResource("Warn");
			LiveTitle.Text = "Connect first";
			LiveText.Text = "The app needs to find your game folder first. Use the Find game button on the Home page.";
		}
		else
		{
			LiveDot.Fill = (Brush)FindResource("Good");
			LiveTitle.Text = "Ready! Rocket League is open";
			LiveText.Text = "Use these in freeplay, training and bot matches only. Using them online can get your account banned.";
		}
	}

	private void AutoOffLive()
	{
		if (_busy || _engine == null || !RlLocator.IsRunning() || !RlLocator.IsAntiCheatRunning())
		{
			return;
		}
		List<RlSwapRecord> mine = _state.Swaps.Where((RlSwapRecord r) => r.Live).ToList();
		if (mine.Count == 0)
		{
			return;
		}
		RlSwapEngine engine = _engine;
		_busy = true;
		Task.Run(() =>
		{
			engine.AllowWhileRunning = true;
			try
			{
				foreach (RlSwapRecord item in mine)
				{
					engine.Revert(item);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				Dispatcher.BeginInvoke((Action)(() =>
				{
					Log("ERROR: " + ex3.Message);
				}));
			}
			finally
			{
				engine.AllowWhileRunning = false;
			}
		}).ContinueWith((Task _) => Dispatcher.BeginInvoke((Action)(() =>
		{
			_busy = false;
			Log("Anti-cheat is running, so the changes made on the Live tab were undone.");
			RefreshSwaps();
		})));
	}

	private void RefreshStrip()
	{
		bool flag = RlLocator.IsRunning();
		bool flag2 = _state.Swaps.Count > 0;
		if ((flag & flag2) && RlLocator.IsAntiCheatRunning())
		{
			SafetyText.Text = "Anti-cheat is running with swapped files. Close Rocket League now and relaunch it with 'Launch without Anti-Cheat'.";
			SafetyText.Foreground = (Brush)FindResource("Danger");
		}
		else if (flag2)
		{
			SafetyText.Text = "Swaps are on. Stay out of online matches: modified files can get your account banned even if the game loads.";
			SafetyText.Foreground = (Brush)FindResource("Warn");
		}
		else
		{
			SafetyText.Text = "Use this offline. Playing online with modified files can get your account banned, even if the game lets you in.";
			SafetyText.Foreground = (Brush)FindResource("Text2");
		}
	}
}
