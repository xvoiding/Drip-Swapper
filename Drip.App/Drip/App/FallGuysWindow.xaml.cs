using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drip.Core;
using Drip.Core.FallGuys;
using Drip.Core.Fun;
using Drip.Core.RocketLeague.Custom;
using Drip.Core.Swapping;
using Microsoft.Win32;

namespace Drip.App;

public partial class FallGuysWindow : Window, IComponentConnector, IStyleConnector
{
	private enum Page
	{
		Home,
		Wardrobe,
		Models,
		Maker,
		Swaps,
		Fun,
		Settings
	}

	private sealed class Settings
	{
		public string GameFolder { get; set; } = "";

		public bool AcceptedRisk { get; set; }
	}

	private FgModelSource? _src;

	private FgFittedModel? _fitted;

	private FgFittedModel? _lookCache;

	private TexImage? _picture;

	private string _pictureName = "";

	private string _makerPath = "";

	private bool _fixedModel;

	private float _viewYaw = 35f;

	private int _renderVersion;

	private readonly DispatcherTimer _renderTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(80L)
	};

	private bool _makerReady;

	private FgUpAxis _autoUp;

	private bool _resetting;

	private bool _dragging;

	private Point _dragFrom;

	private float _dragYaw;

	private FgLibrary? _lib;

	private const int CloneCap = 40;

	private Settings _settings = new Settings();

	private FgInstall? _install;

	private FgSwapEngine? _engine;

	private readonly FgModelLibrary _models = new FgModelLibrary();

	private Page _page;

	private bool _busy;

	private readonly DispatcherTimer _watch = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2L)
	};

	private Action? _noticeYes;

	private bool _gameOpen;

	private bool WardEmoteMode => WardEmotes.IsChecked == true;

	private string WardGame => "fallguys";

	private static string SettingsPath => Path.Combine(AppPaths.DataDir, "FallGuys", "settings.json");

	private void InitMaker()
	{
		if (!_makerReady)
		{
			_makerReady = true;
			_renderTimer.Tick += (object? _, EventArgs _) =>
			{
				_renderTimer.Stop();
				RenderAsync();
			};
		}
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (!(e.Data.GetData(DataFormats.FileDrop) is string[] array) || array.Length == 0)
		{
			return;
		}
		string text = array[0];
		string text2 = Path.GetExtension(text).ToLowerInvariant();
		try
		{
			if (text2 == ".dripFG".ToLowerInvariant())
			{
				FgDripFile fgDripFile = _models.Import(text);
				ShowPage(Page.Models);
				Log("Added " + fgDripFile.Name);
				return;
			}
			bool flag;
			switch (text2)
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
			if (flag)
			{
				if (_src == null)
				{
					ShowNotice("Load a model first", "Drop a 3D file first, then drop the picture you want it to wear.");
					return;
				}
				SetPicture(text);
				ShowPage(Page.Maker);
			}
			else if (FgModelImport.Extensions.Contains(text2))
			{
				ShowPage(Page.Maker);
				await LoadModelFile(text);
			}
			else
			{
				ShowNotice("Can't use that file", "Drop a 3D model (FBX, OBJ, GLB, glTF, DAE, 3DS, STL, PLY...), a picture, or a .dripFG file.");
			}
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't open that file", ex.Message);
		}
	}

	private async void RefreshModels()
	{
		string active = _engine?.LoadState()?.ModelName;
		List<FgModelLibrary.Entry> list = await Task.Run(() => _models.List());
		List<FgModelVm> list2 = new List<FgModelVm>();
		foreach (FgModelLibrary.Entry item2 in list)
		{
			FgModelVm item = new FgModelVm(item2)
			{
				Active = (active != null && string.Equals(active, item2.File.Name, StringComparison.OrdinalIgnoreCase))
			};
			list2.Add(item);
		}
		ModelsHost.ItemsSource = list2;
		ModelsEmpty.Visibility = ((list2.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		foreach (FgModelVm vm in list2)
		{
			FgFittedModel m = vm.Entry.File.Model;
			vm.Icon = ToBitmap(await Task.Run(() => FgPreview.Render(m, 28f, 168, showBean: false)), 168);
		}
	}

	private static BitmapSource ToBitmap(byte[] bgra, int size)
	{
		BitmapSource bitmapSource = BitmapSource.Create(size, size, 96.0, 96.0, PixelFormats.Bgra32, null, bgra, size * 4);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	private static FgModelVm? VmOf(object sender)
	{
		return (sender as FrameworkElement)?.DataContext as FgModelVm;
	}

	private void ModelWear_Click(object sender, RoutedEventArgs e)
	{
		FgModelVm fgModelVm = VmOf(sender);
		if (fgModelVm != null)
		{
			WearModel(fgModelVm.Entry.File.Model, fgModelVm.Name);
		}
	}

	private void ModelView_Click(object sender, RoutedEventArgs e)
	{
		FgModelVm fgModelVm = VmOf(sender);
		if (fgModelVm != null)
		{
			InitMaker();
			_src = null;
			_fixedModel = true;
			_lookCache = null;
			_picture = null;
			_fitted = fgModelVm.Entry.File.Model;
			MakerFile.Text = "Viewing a saved model (drop a 3D file to make a new one).";
			NameBox.Text = fgModelVm.Name;
			MakerEmpty.Visibility = Visibility.Collapsed;
			SaveBtn.IsEnabled = false;
			WearBtn.IsEnabled = true;
			UpdateInfo();
			ShowPage(Page.Maker);
			RequestRender();
		}
	}

	private void ModelExport_Click(object sender, RoutedEventArgs e)
	{
		FgModelVm fgModelVm = VmOf(sender);
		if (fgModelVm == null)
		{
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = "Export model",
			Filter = "Fall Guys model (*.dripFG)|*.dripFG",
			FileName = fgModelVm.Name + ".dripFG"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			fgModelVm.Entry.File.Save(saveFileDialog.FileName);
			Log("Exported " + fgModelVm.Name);
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't save that", ex.Message);
		}
	}

	private void ModelDelete_Click(object sender, RoutedEventArgs e)
	{
		FgModelVm vm = VmOf(sender);
		if (vm == null)
		{
			return;
		}
		ShowNotice("Remove this model?", vm.Name + " moves to a \"removed\" folder inside your models folder, so you can still get it back.", () =>
		{
			try
			{
				_models.Remove(vm.Entry.Path);
				RefreshModels();
			}
			catch (Exception ex)
			{
				ShowNotice("Couldn't remove it", ex.Message);
			}
		}, "Remove", "Keep it");
	}

	private void NewModel_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(Page.Maker);
	}

	private void AddDrip_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Add a Fall Guys model",
			Filter = "Fall Guys model (*.dripFG)|*.dripFG",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		string[] fileNames = openFileDialog.FileNames;
		foreach (string path in fileNames)
		{
			try
			{
				_models.Import(path);
			}
			catch (Exception ex)
			{
				ShowNotice("Couldn't add " + Path.GetFileName(path), ex.Message);
			}
		}
		RefreshModels();
	}

	private void OpenModels_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = _models.Dir,
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void WearModel(FgFittedModel model, string name)
	{
		if (_engine == null)
		{
			ShowNotice("Find the game first", "The app needs to find Fall Guys before it can put a model on your bean. Use the Find game button on the Start page.");
			return;
		}
		WithRiskAccepted(async () =>
		{
			FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
			fgSwapRequest.BeanModel = model;
			fgSwapRequest.BeanModelName = name;
			if (await ApplyAsync(fgSwapRequest))
			{
				ShowNotice("Your bean is " + name, "Start Fall Guys. The bean's body is now this model. A costume you equip draws on top of it, so go to your Locker and take your costume off (None for top and bottom) to see the model.\n\nIf you don't see it, make sure the game was fully closed (check the system tray) when you pressed Wear.", () =>
				{
					ShowPage(Page.Swaps);
				}, "See my bean");
			}
			RefreshModels();
		});
	}

	private async void ChooseModel_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick a 3D model",
			Filter = FgModelImport.DialogFilter()
		};
		if (openFileDialog.ShowDialog() == true)
		{
			await LoadModelFile(openFileDialog.FileName);
		}
	}

	private async Task LoadModelFile(string path)
	{
		InitMaker();
		MakerBusy.Visibility = Visibility.Visible;
		MakerStatus.Text = "";
		try
		{
			FgModelSource fgModelSource = await Task.Run(() => FgModelImport.Load(path));
			_autoUp = FgModelFit.GuessUp(fgModelSource);
			_src = fgModelSource;
			_fixedModel = false;
			_lookCache = null;
			_picture = null;
			_pictureName = "";
			_makerPath = path;
			TextBlock pictureText = PictureText;
			string text;
			if (fgModelSource.Materials.Any((FgSourceMaterial m) => m.Picture != null))
			{
				text = "Using the model's own pictures.";
			}
			else
			{
				text = (fgModelSource.HasUv ? "The model has no picture, so it uses its colours." : "The model has no texture coordinates; add your own picture to dress it.");
			}
			pictureText.Text = text;
			ResetFit();
			MakerFile.Text = ((fgModelSource.Rig != null) ? $"{Path.GetFileName(path)} · {fgModelSource.Rig.Pos.Length:N0} points, {fgModelSource.Rig.Idx.Length / 3:N0} triangles · rigged" : $"{Path.GetFileName(path)} · {fgModelSource.VertexCount:N0} points, {fgModelSource.TriangleCount:N0} triangles");
			if (string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Tag as string == "auto")
			{
				NameBox.Text = Pretty(fgModelSource.Name);
				NameBox.Tag = "auto";
			}
			MakerEmpty.Visibility = Visibility.Collapsed;
			Button saveBtn = SaveBtn;
			bool isEnabled = (WearBtn.IsEnabled = true);
			saveBtn.IsEnabled = isEnabled;
			if (fgModelSource.Rig != null)
			{
				MakerStatus.Text = "A humanoid skeleton was found, so this model is rebuilt on the bean's own skeleton (chunky proportions, arms out like the bean's) and walks, runs and jumps with the game's animations. Size, turn and flip controls don't apply to it; Lift and Forward do.";
			}
			else if (fgModelSource.TriangleCount > 60000)
			{
				MakerStatus.Text = "That's a very detailed model. It works, but a lighter one (under 40k triangles) loads faster in the game.";
			}
			await RenderAsync(now: true);
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't read that model", ex.Message);
		}
		finally
		{
			MakerBusy.Visibility = Visibility.Collapsed;
		}
	}

	private static string Pretty(string s)
	{
		s = s.Replace('_', ' ').Replace('-', ' ').Trim();
		if (s.Length != 0)
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return "My model";
	}

	private void ResetFit()
	{
		_resetting = true;
		YawSlider.Value = 0.0;
		HeightSlider.Value = 1.9;
		LiftSlider.Value = 0.0;
		FwdSlider.Value = 0.0;
		MirrorBox.IsChecked = true;
		_resetting = false;
		UpdateFitTexts();
	}

	private void UpdateFitTexts()
	{
		YawText.Text = $"{YawSlider.Value:0}°";
		HeightText.Text = $"{HeightSlider.Value:0.00} m";
		LiftText.Text = $"{LiftSlider.Value:0.00}";
		FwdText.Text = $"{FwdSlider.Value:0.00}";
	}

	private FgFit CurrentFit()
	{
		return new FgFit
		{
			Up = _autoUp,
			Yaw = (float)YawSlider.Value,
			Height = (float)HeightSlider.Value,
			MirrorX = (MirrorBox.IsChecked == true),
			OffsetY = (float)LiftSlider.Value,
			OffsetZ = (float)FwdSlider.Value,
			PictureOverride = _picture
		};
	}

	private void Fit_Slider(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_resetting && IsLoaded)
		{
			UpdateFitTexts();
			RequestRender();
		}
	}

	private void Fit_Changed(object sender, RoutedEventArgs e)
	{
		RequestRender();
	}

	private void Turn_Click(object sender, RoutedEventArgs e)
	{
		double num = YawSlider.Value + 90.0;
		if (num > 180.0)
		{
			num -= 360.0;
		}
		YawSlider.Value = num;
	}

	private void TurnBack_Click(object sender, RoutedEventArgs e)
	{
		double num = YawSlider.Value - 90.0;
		if (num < -180.0)
		{
			num += 360.0;
		}
		YawSlider.Value = num;
	}

	private void NameBox_Changed(object sender, TextChangedEventArgs e)
	{
		NameHint.Visibility = ((NameBox.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		if (NameBox.IsKeyboardFocused)
		{
			NameBox.Tag = null;
		}
	}

	private void ChoosePicture_Click(object sender, RoutedEventArgs e)
	{
		if (_src == null)
		{
			ShowNotice("Load a model first", "Choose a 3D file first, then pick the picture it should wear.");
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Pick a picture",
			Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All files|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			SetPicture(openFileDialog.FileName);
		}
	}

	private void SetPicture(string path)
	{
		try
		{
			_picture = TexImage.Load(File.ReadAllBytes(path));
			_pictureName = Path.GetFileName(path);
			PictureText.Text = $"Wearing {_pictureName} ({_picture.Width}×{_picture.Height}) on the whole model.";
			_lookCache = null;
			RequestRender();
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't read that picture", ex.Message);
		}
	}

	private void ClearPicture_Click(object sender, RoutedEventArgs e)
	{
		if (_src != null)
		{
			_picture = null;
			_pictureName = "";
			_lookCache = null;
			PictureText.Text = "Using the model's own colours and pictures.";
			RequestRender();
		}
	}

	private void RequestRender()
	{
		InitMaker();
		if (_fixedModel || _src != null)
		{
			_renderTimer.Stop();
			_renderTimer.Start();
		}
	}

	private async Task RenderAsync(bool now = false)
	{
		int ver = ++_renderVersion;
		try
		{
			FgFittedModel model = _fitted;
			if (!_fixedModel && _src != null)
			{
				FgModelSource src = _src;
				FgFit fit = CurrentFit();
				FgFittedModel look = _lookCache;
				model = await Task.Run(() => FgModelFit.Fit(src, fit, look));
				if (ver != _renderVersion)
				{
					return;
				}
				_lookCache = model;
				_fitted = model;
			}
			if (model != null)
			{
				float yaw = _viewYaw;
				byte[] bgra = await Task.Run(() => FgPreview.Render(model, yaw, 560));
				if (ver == _renderVersion)
				{
					MakerPreview.Source = ToBitmap(bgra, 560);
					UpdateInfo();
				}
			}
		}
		catch (Exception ex)
		{
			MakerStatus.Text = "Preview problem: " + ex.Message;
		}
	}

	private void UpdateInfo()
	{
		if (_fitted == null)
		{
			MakerInfo.Text = "";
			return;
		}
		FgFittedModel fitted = _fitted;
		float value = 0f;
		if (fitted.Pos.Length != 0)
		{
			value = fitted.Pos.Max((Vector3 p) => p.Y) - fitted.Pos.Min((Vector3 p) => p.Y);
		}
		MakerInfo.Text = $"{fitted.Pos.Length:N0} points · {fitted.Idx.Length / 3:N0} triangles · {value:0.00} m tall · picture {fitted.Width}×{fitted.Height}";
	}

	private void Preview_Down(object sender, MouseButtonEventArgs e)
	{
		_dragging = true;
		_dragFrom = e.GetPosition(MakerPreview);
		_dragYaw = _viewYaw;
		MakerPreview.CaptureMouse();
	}

	private void Preview_Up(object sender, MouseButtonEventArgs e)
	{
		_dragging = false;
		MakerPreview.ReleaseMouseCapture();
	}

	private void Preview_Move(object sender, MouseEventArgs e)
	{
		if (_dragging)
		{
			Point position = e.GetPosition(MakerPreview);
			_viewYaw = _dragYaw + (float)(position.X - _dragFrom.X) * 0.7f;
			if (!_renderTimer.IsEnabled)
			{
				_renderTimer.Start();
			}
		}
	}

	private void SetView(float yaw)
	{
		_viewYaw = yaw;
		RequestRender();
	}

	private void ViewFront_Click(object sender, RoutedEventArgs e)
	{
		SetView(0f);
	}

	private void ViewSide_Click(object sender, RoutedEventArgs e)
	{
		SetView(90f);
	}

	private void ViewBack_Click(object sender, RoutedEventArgs e)
	{
		SetView(180f);
	}

	private void ViewAngle_Click(object sender, RoutedEventArgs e)
	{
		SetView(35f);
	}

	private FgFittedModel? FinalModel()
	{
		if (_fixedModel)
		{
			return _fitted;
		}
		if (_src == null)
		{
			return null;
		}
		return FgModelFit.Fit(_src, CurrentFit(), _lookCache);
	}

	private void SaveModel_Click(object sender, RoutedEventArgs e)
	{
		FgFittedModel fgFittedModel = FinalModel();
		if (fgFittedModel == null)
		{
			return;
		}
		string text = NameBox.Text.Trim();
		if (text.Length == 0)
		{
			ShowNotice("Give it a name", "Type a name for your model first.");
			return;
		}
		try
		{
			_models.Add(new FgDripFile
			{
				Name = text,
				Author = Environment.UserName,
				Notes = ((_src != null) ? ("Made from " + Path.GetFileName(_makerPath)) : ""),
				Model = fgFittedModel
			});
			MakerStatus.Text = "Saved to My models.";
			Log("Saved " + text);
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't save that", ex.Message);
		}
	}

	private void WearNow_Click(object sender, RoutedEventArgs e)
	{
		FgFittedModel fgFittedModel = FinalModel();
		if (fgFittedModel != null)
		{
			string text = NameBox.Text.Trim();
			WearModel(fgFittedModel, (text.Length > 0) ? text : "Custom model");
		}
	}

	private async Task<FgLibrary?> EnsureLibraryAsync()
	{
		if (_lib != null)
		{
			return _lib;
		}
		if (_engine == null)
		{
			return null;
		}
		FgSwapEngine eng = _engine;
		try
		{
			_lib = await Task.Run(() => new FgLibrary(FgCatalog.Load(eng.CatalogForReading())));
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("Couldn't read the costume list", ex.Message);
		}
		return _lib;
	}

	private async Task RefreshWardrobeAsync()
	{
		if (_engine == null)
		{
			WardStatus.Text = "Find the game on the Start page first.";
			return;
		}
		WardStatus.Text = ((_lib == null) ? "Reading the game's costume list..." : "");
		if (await EnsureLibraryAsync() != null)
		{
			ApplyWardFilter();
			RefreshSwaps();
			if (WardStatus.Text.StartsWith("Reading", StringComparison.Ordinal))
			{
				WardStatus.Text = "";
			}
		}
	}

	private void ApplyWardFilter()
	{
		if (_lib == null)
		{
			return;
		}
		bool favOnly = WardFavOnly.IsChecked == true;
		bool wardEmoteMode = WardEmoteMode;
		WardLabelA.Text = (wardEmoteMode ? "The emote you equip" : "The costume you equip");
		WardLabelB.Text = (wardEmoteMode ? "Make it play like" : "Make it look like");
		string q = WardSearchA.Text.Trim();
		string q2 = WardSearchB.Text.Trim();
		string keepA = (WardListA.SelectedItem as FgCostumeVm)?.Key ?? (WardListA.SelectedItem as FgEmoteVm)?.Key;
		string keepB = (WardListB.SelectedItem as FgCostumeVm)?.Key ?? (WardListB.SelectedItem as FgEmoteVm)?.Key;
		if (wardEmoteMode)
		{
			List<FgEmoteVm> list = Build(q);
			List<FgEmoteVm> list2 = Build(q2);
			WardListA.ItemsSource = list;
			WardListB.ItemsSource = list2;
			WardListA.SelectedItem = list.FirstOrDefault((FgEmoteVm v) => v.Key == keepA);
			WardListB.SelectedItem = list2.FirstOrDefault((FgEmoteVm v) => v.Key == keepB);
		}
		else
		{
			List<FgCostumeVm> list3 = Build2(q);
			List<FgCostumeVm> list4 = Build2(q2);
			WardListA.ItemsSource = list3;
			WardListB.ItemsSource = list4;
			WardListA.SelectedItem = list3.FirstOrDefault((FgCostumeVm v) => v.Key == keepA);
			WardListB.SelectedItem = list4.FirstOrDefault((FgCostumeVm v) => v.Key == keepB);
		}
		List<FgEmoteVm> Build(string q3)
		{
			return (from e in _lib.Emotes
				where Hit(e.Name, e.Season, q3) && (!favOnly || Fav(e.Key))
				select new FgEmoteVm(e)
				{
					IsFavorite = Fav(e.Key)
				} into v
				orderby v.IsFavorite descending
				select v).ThenBy((FgEmoteVm v) => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
		}
		List<FgCostumeVm> Build2(string q3)
		{
			return (from c in _lib.Costumes
				where Hit(c.Name, c.Season + " " + c.Id, q3) && (!favOnly || Fav(c.Key))
				select new FgCostumeVm(c)
				{
					IsFavorite = Fav(c.Key)
				} into v
				orderby v.IsFavorite descending
				select v).ThenBy((FgCostumeVm v) => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
		}
		bool Fav(string key)
		{
			return FunHub.Store.IsFavorite(WardGame, key);
		}
		static bool Hit(string name, string extra, string text)
		{
			if (text.Length != 0 && !name.Contains(text, StringComparison.OrdinalIgnoreCase))
			{
				return extra.Contains(text, StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	private void WardKind_Click(object sender, RoutedEventArgs e)
	{
		WardSearchA.Clear();
		WardSearchB.Clear();
		ApplyWardFilter();
	}

	private void WardFilter_Changed(object sender, RoutedEventArgs e)
	{
		if (IsLoaded)
		{
			ApplyWardFilter();
		}
	}

	private void WardRow_Fav(object sender, MouseButtonEventArgs e)
	{
		object obj = (sender as FrameworkElement)?.DataContext;
		string text;
		if (obj is FgCostumeVm fgCostumeVm)
		{
			text = fgCostumeVm.Key;
		}
		else
		{
			text = ((!(obj is FgEmoteVm fgEmoteVm)) ? null : fgEmoteVm.Key);
		}
		string text2 = text;
		if (text2 != null)
		{
			FunHub.Store.ToggleFavorite(WardGame, text2);
			ApplyWardFilter();
			e.Handled = true;
		}
	}

	private static (string Key, string Name)? KeyOf(object? o)
	{
		if (!(o is FgCostumeVm fgCostumeVm))
		{
			if (o is FgEmoteVm fgEmoteVm)
			{
				return (fgEmoteVm.Key, fgEmoteVm.Name);
			}
			return null;
		}
		return (fgCostumeVm.Key, fgCostumeVm.Name);
	}

	private void WardSwap_Click(object sender, RoutedEventArgs e)
	{
		StartWardSwap();
	}

	private void WardSurprise_Click(object sender, RoutedEventArgs e)
	{
		(string, string)? tuple = KeyOf(WardListA.SelectedItem);
		if (tuple.HasValue)
		{
			(string Key, string Name) mine = tuple.GetValueOrDefault();
			object obj = Surprise.Pick(WardListB.Items.Cast<object>().Where((object o) =>
			{
				(string, string)? tuple2 = KeyOf(o);
				return tuple2.HasValue && tuple2.GetValueOrDefault().Item1 != mine.Key;
			}).ToList(), (object _) => true, (object o) => KeyOf(o).Value.Key, mine.Key);
			if (obj == null)
			{
				WardStatus.Text = "Nothing to pick from. Clear the search on the right.";
				return;
			}
			WardListB.SelectedItem = obj;
			WardListB.ScrollIntoView(obj);
			StartWardSwap();
		}
		else
		{
			WardStatus.Text = "Pick yours on the left first.";
		}
	}

	private void StartWardSwap()
	{
		if (_engine == null)
		{
			ShowNotice("Find the game first", "The app needs to find Fall Guys first. Use the Find game button on the Start page.");
			return;
		}
		(string, string)? tuple = KeyOf(WardListA.SelectedItem);
		if (tuple.HasValue)
		{
			(string Key, string Name) mine = tuple.GetValueOrDefault();
			tuple = KeyOf(WardListB.SelectedItem);
			if (tuple.HasValue)
			{
				(string Key, string Name) look = tuple.GetValueOrDefault();
				if (mine.Key == look.Key)
				{
					WardStatus.Text = "Pick two different ones.";
					return;
				}
				bool emote = WardEmoteMode;
				WithRiskAccepted(async () =>
				{
					FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
					if (emote)
					{
						fgSwapRequest.EmoteSwaps.RemoveAll(((string Target, string Source) x) => x.Target == mine.Key);
						fgSwapRequest.EmoteSwaps.Add((mine.Key, look.Key));
					}
					else
					{
						fgSwapRequest.CostumeSwaps.RemoveAll(((string Target, string Source, bool Top, bool Bottom) x) => x.Target == mine.Key);
						fgSwapRequest.CostumeSwaps.Add((mine.Key, look.Key, true, true));
					}
					if (await ApplyAsync(fgSwapRequest))
					{
						WardStatus.Text = (emote ? $"The {mine.Name} emote now plays {look.Name}. Start Fall Guys and use {mine.Name}." : $"{mine.Name} now looks like {look.Name}. Equip {mine.Name} in your Locker, then start the game.");
					}
				});
				return;
			}
		}
		WardStatus.Text = "Pick one on the left and one on the right.";
	}

	private List<string> LibraryKeys()
	{
		if (_lib != null)
		{
			return (WardEmoteMode ? _lib.Emotes.Select((FgEmote e) => e.Key) : _lib.Costumes.Select((FgCostume c) => c.Key)).ToList();
		}
		return new List<string>();
	}

	private void ApplyBulk(IReadOnlyList<(string Mine, string Look)> pairs, string done)
	{
		if (_engine == null)
		{
			ShowNotice("Find the game first", "The app needs to find Fall Guys first. Use the Find game button on the Start page.");
			return;
		}
		bool emote = WardEmoteMode;
		WithRiskAccepted(async () =>
		{
			FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
			foreach (var pair in pairs)
			{
				var (mine, item) = pair;
				if (emote)
				{
					fgSwapRequest.EmoteSwaps.RemoveAll(((string Target, string Source) x) => x.Target == mine);
					fgSwapRequest.EmoteSwaps.Add((mine, item));
				}
				else
				{
					fgSwapRequest.CostumeSwaps.RemoveAll(((string Target, string Source, bool Top, bool Bottom) x) => x.Target == mine);
					fgSwapRequest.CostumeSwaps.Add((mine, item, true, true));
				}
			}
			if (await ApplyAsync(fgSwapRequest))
			{
				WardStatus.Text = done;
			}
		});
	}

	private void WardClone_Click(object sender, RoutedEventArgs e)
	{
		(string, string)? tuple = KeyOf(WardListB.SelectedItem);
		if (tuple.HasValue)
		{
			(string Key, string Name) look = tuple.GetValueOrDefault();
			List<string> list = (from k in LibraryKeys()
				where k != look.Key
				orderby FunHub.Store.IsFavorite(WardGame, k) descending
				select k).Take(40).ToList();
			if (list.Count == 0)
			{
				WardStatus.Text = "Nothing to change. Wait for the list to load.";
				return;
			}
			ApplyBulk(list.Select((string k) => (k: k, Key: look.Key)).ToList(), $"{list.Count} {(WardEmoteMode ? "emotes" : "costumes")} now look like {look.Name}. Equip any of them in the game.");
		}
		else
		{
			WardStatus.Text = "Pick the look on the right first.";
		}
	}

	private void WardShuffle_Click(object sender, RoutedEventArgs e)
	{
		List<string> keys = LibraryKeys();
		if (keys.Count < 3)
		{
			WardStatus.Text = "Nothing to shuffle yet. Wait for the list to load.";
			return;
		}
		Random rng = Random.Shared;
		List<(string, string)> list = (from m in keys.OrderBy((string _) => rng.Next()).Take(12).ToList()
			select (m: m, keys.Where((string k) => k != m).ElementAt(rng.Next(keys.Count - 1)))).ToList();
		ApplyBulk(list, $"Shuffled {list.Count} {(WardEmoteMode ? "emotes" : "costumes")}. See which ones in On you.");
	}

	private void WardClear_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			ShowNotice("Find the game first", "The app needs to find Fall Guys first. Use the Find game button on the Start page.");
			return;
		}
		bool emote = WardEmoteMode;
		WithRiskAccepted(async () =>
		{
			FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
			if (emote)
			{
				fgSwapRequest.EmoteSwaps.Clear();
			}
			else
			{
				fgSwapRequest.CostumeSwaps.Clear();
			}
			if (await ApplyAsync(fgSwapRequest))
			{
				WardStatus.Text = (emote ? "Emotes are back to normal." : "Costumes are back to normal.");
			}
		});
	}

	private void SwapUndo_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null || !(sender is FrameworkElement { Tag: var tag }))
		{
			return;
		}
		FgSwapVm vm = tag as FgSwapVm;
		if (vm == null)
		{
			return;
		}
		WithRiskAccepted(async () =>
		{
			FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
			FgSwapRecord rec = vm.Record;
			if (rec.Kind == "emote")
			{
				fgSwapRequest.EmoteSwaps.RemoveAll(((string Target, string Source) x) => x.Target == rec.Target);
			}
			else
			{
				fgSwapRequest.CostumeSwaps.RemoveAll(((string Target, string Source, bool Top, bool Bottom) x) => x.Target == rec.Target);
			}
			await ApplyAsync(fgSwapRequest);
		});
	}

	private IReadOnlyList<FitEntry> CurrentFitEntries()
	{
		return _engine?.LoadState()?.Swaps.Select((FgSwapRecord r) => new FitEntry
		{
			Kind = ((r.Kind == "emote") ? "Emote" : "Costume"),
			Source = r.Target,
			Target = r.Source
		}).ToList() ?? new List<FitEntry>();
	}

	private async Task<string> ApplyFitAsync(Fit fit)
	{
		if (_engine == null)
		{
			return "Find the game on the Start page first.";
		}
		if (_busy)
		{
			return "Wait for the current job to finish.";
		}
		if (_gameOpen)
		{
			return "Close Fall Guys first, then apply the loadout.";
		}
		FgLibrary lib = await EnsureLibraryAsync();
		if (lib == null)
		{
			return "Couldn't read the game's costume list.";
		}
		TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
		WithRiskAccepted(async () =>
		{
			try
			{
				FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
				int ok = 0;
				List<string> problems = new List<string>();
				foreach (FitEntry en in fit.Entries)
				{
					if (en.Kind == "Emote")
					{
						if ((object)lib.FindEmote(en.Source) == null || (object)lib.FindEmote(en.Target) == null)
						{
							problems.Add(en.Source + ": not in this game version");
							continue;
						}
						fgSwapRequest.EmoteSwaps.RemoveAll(((string Target, string Source) x) => x.Target == en.Source);
						fgSwapRequest.EmoteSwaps.Add((en.Source, en.Target));
					}
					else
					{
						if (lib.Find(en.Source) == null || lib.Find(en.Target) == null)
						{
							problems.Add(en.Source + ": not in this game version");
							continue;
						}
						fgSwapRequest.CostumeSwaps.RemoveAll(((string Target, string Source, bool Top, bool Bottom) x) => x.Target == en.Source);
						fgSwapRequest.CostumeSwaps.Add((en.Source, en.Target, true, true));
					}
					ok++;
				}
				if (ok == 0)
				{
					tcs.TrySetResult("None of that loadout's items are in this version of the game.");
				}
				else if (await ApplyAsync(fgSwapRequest))
				{
					string text = $"Applied {ok} of {fit.Entries.Count} swap{((fit.Entries.Count == 1) ? "" : "s")} from \"{fit.Name}\".";
					if (problems.Count > 0)
					{
						text = text + " Skipped: " + string.Join("; ", problems.Take(3));
					}
					tcs.TrySetResult(text);
				}
				else
				{
					tcs.TrySetResult("That didn't work. See the message.");
				}
			}
			catch (Exception ex)
			{
				tcs.TrySetResult(ex.Message);
			}
		});
		if (_engine != null && !_settings.AcceptedRisk)
		{
			return "Accept the notice first, then press Apply again.";
		}
		return await tcs.Task;
	}

	public FallGuysWindow()
	{
		InitializeComponent();
		Theme.SetGame(Game.FallGuys);
		Shell.Setup(this, 1040.0, 660.0);
		LoadSettings();
		FormatsText.Text = "FBX, OBJ, GLB, glTF, DAE, 3DS, STL, PLY, BLEND and about " + Math.Max(0, FgModelImport.Extensions.Count - 9) + " more formats. Or drop a picture on the model to wear it.";
		try
		{
			Preinstalled.SeedFallGuys(null, _models);
		}
		catch
		{
		}
		_install = ((!string.IsNullOrEmpty(_settings.GameFolder)) ? FgLocator.FromFolder(_settings.GameFolder) : null);
		if ((object)_install == null)
		{
			_install = FgLocator.Find();
		}
		PathBox.Text = _install?.Root ?? "";
		Log((_install != null) ? "Found Fall Guys." : "Couldn't find Fall Guys automatically. Press Find game.");
		FunCtl.Attach("fallguys", FunHub.Store, CurrentFitEntries, ApplyFitAsync, (string t, string m) =>
		{
			ShowNotice(t, m);
		});
		ShowPage(Page.Home);
		RefreshSetup();
		_watch.Tick += (object? _, EventArgs _) =>
		{
			WatchGame();
		};
		_watch.Start();
		WatchGame();
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			await ConnectAsync();
		};
		Closed += (object? _, EventArgs _) =>
		{
			_watch.Stop();
		};
	}

	private void LoadSettings()
	{
		try
		{
			if (File.Exists(SettingsPath))
			{
				_settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
			}
		}
		catch
		{
			_settings = new Settings();
		}
	}

	private void SaveSettings()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
			File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings));
		}
		catch
		{
		}
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
		if (e.Key == Key.Escape && NoticeOverlay.Visibility == Visibility.Visible)
		{
			CloseNotice();
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
			ShowPage(result);
		}
	}

	private void Start_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			ShowNotice("Let's get set up", "The app needs to find Fall Guys first. Use the Find game button on the Start page.");
		}
		else
		{
			ShowPage(Page.Models);
		}
	}

	private void ShowPage(Page page)
	{
		_page = page;
		CloseNotice();
		HomePage.Visibility = ((page != Page.Home) ? Visibility.Collapsed : Visibility.Visible);
		WardrobePage.Visibility = ((page != Page.Wardrobe) ? Visibility.Collapsed : Visibility.Visible);
		FunPage.Visibility = ((page != Page.Fun) ? Visibility.Collapsed : Visibility.Visible);
		ModelsPage.Visibility = ((page != Page.Models) ? Visibility.Collapsed : Visibility.Visible);
		MakerPage.Visibility = ((page != Page.Maker) ? Visibility.Collapsed : Visibility.Visible);
		SwapsPage.Visibility = ((page != Page.Swaps) ? Visibility.Collapsed : Visibility.Visible);
		SettingsPage.Visibility = ((page != Page.Settings) ? Visibility.Collapsed : Visibility.Visible);
		TabHome.IsChecked = page == Page.Home;
		TabWardrobe.IsChecked = page == Page.Wardrobe;
		TabFun.IsChecked = page == Page.Fun;
		TabModels.IsChecked = page == Page.Models;
		TabMaker.IsChecked = page == Page.Maker;
		TabSwaps.IsChecked = page == Page.Swaps;
		TabSettings.IsChecked = page == Page.Settings;
		(PageTitle.Text, PageSub.Text) = page switch
		{
			Page.Wardrobe => ("Wardrobe", "Swap a costume or emote for another, or go wild: Clone army, Shuffle 12."), 
			Page.Fun => ("Loadouts", "Save what's swapped and bring it back in one click."), 
			Page.Models => ("My models", "Your 3D models. Press Wear it to turn your bean into one."), 
			Page.Maker => ("Model maker", "Turn a 3D file (FBX, OBJ, GLB and more) into a model your bean can wear."), 
			Page.Swaps => ("On your bean", "What your bean is wearing right now. Press Undo to put the normal bean back."), 
			Page.Settings => ("Settings", "Game folder, backups and the log."), 
			_ => ("Start here", "Wear your own 3D model as your bean."), 
		};
		if (page == Page.Models)
		{
			RefreshModels();
		}
		if (page == Page.Wardrobe)
		{
			RefreshWardrobeAsync();
		}
		if (page == Page.Fun)
		{
			FunCtl.Refresh();
		}
		if (page == Page.Swaps)
		{
			RefreshSwaps();
		}
		if (page == Page.Maker)
		{
			RequestRender();
		}
	}

	private void Diagnostics_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = DiagnosticsHelper.Save("Fall Guys");
			ShowNotice("Diagnostics saved", "Saved to your Downloads folder:\n" + text + "\n\nSend that file when something doesn't work.");
		}
		catch (Exception ex)
		{
			ShowNotice("Couldn't save the file", ex.Message);
		}
	}

	private void ShowNotice(string title, string text, Action? yes = null, string yesText = "OK", string? noText = null)
	{
		NoticeTitle.Text = title;
		NoticeText.Text = text;
		_noticeYes = yes;
		NoticeYes.Content = yesText;
		NoticeNo.Content = noText ?? "Cancel";
		NoticeNo.Visibility = ((noText == null) ? Visibility.Collapsed : Visibility.Visible);
		NoticeOverlay.Visibility = Visibility.Visible;
	}

	private void CloseNotice()
	{
		NoticeOverlay.Visibility = Visibility.Collapsed;
		_noticeYes = null;
	}

	private void NoticeNo_Click(object sender, RoutedEventArgs e)
	{
		CloseNotice();
	}

	private void NoticeYes_Click(object sender, RoutedEventArgs e)
	{
		Action? noticeYes = _noticeYes;
		CloseNotice();
		noticeYes?.Invoke();
	}

	private void Backdrop_Click(object sender, MouseButtonEventArgs e)
	{
		CloseNotice();
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
	}

	private void WatchGame()
	{
		bool flag = (_gameOpen = FgLocator.IsRunning());
		OpenBanner.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (flag)
		{
			OpenBannerText.Text = "Fall Guys is open. Close it to change swaps (the game locks its files while it runs).";
		}
	}

	private void RefreshSetup()
	{
		bool flag = _install != null;
		SetupGameDot.Fill = (Brush)FindResource(flag ? "Good" : "Danger");
		SetupGameText.Text = (flag ? ("Found your game: " + _install.Root) : "Couldn't find Fall Guys by itself. Press Find game and pick the folder that has FallGuys_client_game.exe in it.");
		SetupGameBtn.Content = (flag ? "Change…" : "Find game…");
		StartBtn.Content = (flag ? "Pick a model" : "Find the game first");
	}

	private void SetupGame_Click(object sender, RoutedEventArgs e)
	{
		Browse_Click(sender, e);
	}

	private async void Browse_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Pick the Fall Guys folder (the one with FallGuys_client_game.exe)"
		};
		if (openFolderDialog.ShowDialog() == true)
		{
			FgInstall fgInstall = FgLocator.FromFolder(openFolderDialog.FolderName);
			if (fgInstall == null)
			{
				ShowNotice("That's not Fall Guys", "I couldn't find Fall Guys' files in that folder. Pick the folder that has FallGuys_client_game.exe in it.");
				return;
			}
			_install = fgInstall;
			_settings.GameFolder = fgInstall.Root;
			SaveSettings();
			PathBox.Text = fgInstall.Root;
			RefreshSetup();
			await ConnectAsync();
		}
	}

	private async Task ConnectAsync()
	{
		if (_busy || _install == null)
		{
			return;
		}
		SetBusy(busy: true);
		try
		{
			_engine = new FgSwapEngine(_install);
			_lib = null;
			await Task.Run(() => _engine.CatalogForReading());
			Log("Ready. Pick a model to wear.");
			FgCheck chk = await Task.Run(() => _engine.Check());
			if (chk.HadChanges && !chk.Ok)
			{
				Log(chk.Summary);
				try
				{
					if (_engine.Blocker() == null)
					{
						await Task.Run(() =>
						{
							_engine.Restore();
						});
					}
				}
				catch (Exception ex)
				{
					Log("Cleanup: " + ex.Message);
				}
				ShowNotice("Your model isn't on any more", chk.Summary + "\n\nPick it under My models and press Wear again.");
			}
			RefreshSwaps();
		}
		catch (Exception ex2)
		{
			_engine = null;
			Log("ERROR: " + ex2.Message);
			ShowNotice("Couldn't read the game's files", ex2.Message + "\n\nIf the game just updated, give the Epic launcher a moment to finish, then try again.");
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void WithRiskAccepted(Action go)
	{
		if (_engine == null)
		{
			return;
		}
		if (_gameOpen)
		{
			ShowNotice("Close Fall Guys first", "Swaps can only be changed while the game is closed. Close it (check the system tray too) and try again.");
			return;
		}
		if (_settings.AcceptedRisk)
		{
			go();
			return;
		}
		ShowNotice("Before you wear a model", "Fall Guys uses Easy Anti-Cheat, and a custom model changes the game's files on your PC. Drip Swapper does nothing to hide that, so there is a risk the game flags your account. Use it at your own risk.\n\nThe app keeps a backup of everything it touches, and Undo everything puts the original files back.", () =>
		{
			_settings.AcceptedRisk = true;
			SaveSettings();
			go();
		}, "I understand", "Cancel");
	}

	private async Task<bool> ApplyAsync(FgSwapRequest req)
	{
		if (_engine == null || _busy)
		{
			return false;
		}
		SetBusy(busy: true);
		try
		{
			Progress<string> log = new Progress<string>(Log);
			FgSwapEngine eng = _engine;
			await Task.Run(() =>
			{
				if (req.BeanModel == null && req.CostumeSwaps.Count == 0 && req.EmoteSwaps.Count == 0)
				{
					eng.Restore(log);
				}
				else
				{
					eng.Apply(req, null, log);
				}
			});
			if (req.BeanModel != null || req.CostumeSwaps.Count > 0 || req.EmoteSwaps.Count > 0)
			{
				FgCheck chk = await Task.Run(() => eng.Check());
				Log(chk.Summary);
				if (!chk.Ok)
				{
					try
					{
						await Task.Run(() =>
						{
							eng.Restore(log);
						});
					}
					catch
					{
					}
					RefreshSwaps();
					ShowNotice("That didn't stick", chk.Summary + "\n\nIt was undone so your game is untouched. Close the Epic launcher and Fall Guys completely, then try again.");
					return false;
				}
			}
			RefreshSwaps();
			return true;
		}
		catch (Exception ex)
		{
			Log("ERROR: " + ex.Message);
			ShowNotice("That didn't work", Friendly.Explain(ex.Message));
			return false;
		}
		finally
		{
			SetBusy(busy: false);
		}
	}

	private void RefreshSwaps()
	{
		FgState fgState = _engine?.LoadState();
		int num;
		if (fgState != null)
		{
			List<FgBundleRecord> bundles = fgState.Bundles;
			if (bundles != null)
			{
				num = ((bundles.Count > 0) ? 1 : 0);
				goto IL_002c;
			}
		}
		num = 0;
		goto IL_002c;
		IL_002c:
		bool flag = (byte)num != 0;
		BeanRow.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		BeanTitle.Text = (flag ? (fgState.ModelName ?? "Custom model") : "");
		ClearBeanBtn.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		List<FgSwapVm> list = fgState?.Swaps.Select((FgSwapRecord r) => new FgSwapVm(r, _lib)).ToList() ?? new List<FgSwapVm>();
		SwapRowsHost.ItemsSource = list;
		SwapsEmpty.Visibility = ((flag || list.Count > 0) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void ClearBean_Click(object sender, RoutedEventArgs e)
	{
		if (_engine != null)
		{
			if (_gameOpen)
			{
				ShowNotice("Close Fall Guys first", "Swaps can only be changed while the game is closed.");
				return;
			}
			FgSwapRequest fgSwapRequest = _engine.CurrentRequest();
			fgSwapRequest.BeanModel = null;
			fgSwapRequest.BeanModelName = null;
			ApplyAsync(fgSwapRequest);
		}
	}

	private void RevertAll_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			ShowNotice("Nothing to undo", "Find the game first.");
		}
		else if (_gameOpen)
		{
			ShowNotice("Close Fall Guys first", "Swaps can only be changed while the game is closed.");
		}
		else
		{
			ApplyAsync(new FgSwapRequest());
		}
	}

	private void OpenBackups_Click(object sender, RoutedEventArgs e)
	{
		if (_engine == null)
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = _engine.WorkDir,
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}
}
