using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Drip.Core.Mods;

namespace Drip.App;

public sealed class PakVm
{
	public string RefFile { get; }

	public PakPack Pack { get; }

	public ObservableCollection<PakSkinVm> Skins { get; }

	public string Name => Pack.Name;

	public string Sub
	{
		get
		{
			if (Pack.Problem == null)
			{
				return $"community pak  ·  {Skins.Count} skin(s)  ·  {Pack.FileCount:N0} files  ·  {(double)Pack.TotalBytes / 1048576.0:N0} MB";
			}
			return "Can't read this pak";
		}
	}

	public string Status
	{
		get
		{
			if (Pack.Problem == null)
			{
				return "View only. Drip Swapper shows what's inside, but it can't install these: they're loose files in an older game format, and the swapper can't convert them for this Fortnite build.";
			}
			return Pack.Problem + " If you moved or deleted the file, remove this entry and add it again.";
		}
	}

	public Brush StatusBrush => (Brush)Application.Current.FindResource((Pack.Problem != null) ? "Danger" : "Text2");

	public PakVm(string refFile, PakPack pack, IEnumerable<PakSkinVm> skins)
	{
		RefFile = refFile;
		Pack = pack;
		Skins = new ObservableCollection<PakSkinVm>(skins);
	}
}
