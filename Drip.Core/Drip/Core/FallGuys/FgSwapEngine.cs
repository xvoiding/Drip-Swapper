using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Drip.Core.FallGuys;

public sealed class FgSwapEngine
{
	public FgInstall Install { get; }

	public string WorkDir { get; }

	private string StatePath => Path.Combine(WorkDir, "state.json");

	private string BackupDir => Path.Combine(WorkDir, "backup");

	private string CatalogBackup => Path.Combine(BackupDir, "catalog.bundle");

	private string ActiveModelPath => Path.Combine(WorkDir, "active-model.dripFG");

	public bool IsModified
	{
		get
		{
			FgState fgState = LoadState();
			if (fgState != null)
			{
				if (fgState.Swaps.Count <= 0)
				{
					return fgState.Bundles.Count > 0;
				}
				return true;
			}
			return false;
		}
	}

	public FgSwapEngine(FgInstall install, string? workDir = null)
	{
		Install = install;
		WorkDir = workDir ?? Path.Combine(AppPaths.DataDir, "FallGuys");
		Directory.CreateDirectory(BackupDir);
	}

	public FgState? LoadState()
	{
		try
		{
			return File.Exists(StatePath) ? JsonSerializer.Deserialize<FgState>(File.ReadAllText(StatePath)) : null;
		}
		catch
		{
			return null;
		}
	}

	private void SaveState(FgState? st)
	{
		if (st == null)
		{
			if (File.Exists(StatePath))
			{
				File.Move(StatePath, StatePath + ".old", overwrite: true);
			}
		}
		else
		{
			string text = StatePath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(st, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			File.Move(text, StatePath, overwrite: true);
		}
	}

	public FgSwapRequest CurrentRequest()
	{
		FgSwapRequest fgSwapRequest = new FgSwapRequest();
		FgState fgState = LoadState();
		if (fgState == null)
		{
			return fgSwapRequest;
		}
		foreach (FgSwapRecord swap in fgState.Swaps)
		{
			if (swap.Kind == "emote")
			{
				fgSwapRequest.EmoteSwaps.Add((swap.Target, swap.Source));
			}
			else
			{
				fgSwapRequest.CostumeSwaps.Add((swap.Target, swap.Source, swap.Top, swap.Bottom));
			}
		}
		if (fgState.Bundles.Count > 0 && File.Exists(ActiveModelPath))
		{
			try
			{
				FgDripFile fgDripFile = FgDripFile.Load(ActiveModelPath);
				fgSwapRequest.BeanModel = fgDripFile.Model;
				fgSwapRequest.BeanModelName = fgDripFile.Name;
			}
			catch
			{
			}
		}
		return fgSwapRequest;
	}

	public string CatalogForReading()
	{
		FgState fgState = LoadState();
		if (fgState != null && File.Exists(CatalogBackup) && File.Exists(Install.CatalogPath))
		{
			try
			{
				if (Sha(Install.CatalogPath) == fgState.CatalogModifiedSha)
				{
					return CatalogBackup;
				}
			}
			catch
			{
			}
		}
		return Install.CatalogPath;
	}

	public string? Blocker()
	{
		if (FgLocator.IsRunning())
		{
			return "Close Fall Guys first - swaps can only be changed while the game is closed.";
		}
		return null;
	}

	public FgCheck Check()
	{
		FgState fgState = LoadState();
		if (fgState == null || (fgState.Swaps.Count == 0 && fgState.Bundles.Count == 0))
		{
			return new FgCheck(Ok: true, HadChanges: false, "Nothing is applied right now.");
		}
		try
		{
			string text = Sha(Install.CatalogPath);
			if (text != fgState.CatalogModifiedSha)
			{
				return new FgCheck(Ok: false, HadChanges: true, (text == fgState.CatalogOriginalSha) ? "The game's files were put back to original (the Epic launcher repaired them), so your model is no longer on." : "Fall Guys was updated since you put your model on, so it is no longer on.");
			}
			foreach (FgBundleRecord bundle in fgState.Bundles)
			{
				string path = BundlePath(bundle.File);
				if (!File.Exists(path))
				{
					return new FgCheck(Ok: false, HadChanges: true, "A game file the model needed is missing (" + bundle.File + ").");
				}
				if (Sha(path) != bundle.ModifiedSha)
				{
					return new FgCheck(Ok: false, HadChanges: true, "A game file changed after the model was put on (" + bundle.File + "), so it is no longer on.");
				}
			}
		}
		catch (Exception ex)
		{
			return new FgCheck(Ok: false, HadChanges: true, "Couldn't read the game files: " + ex.Message);
		}
		string modelName = fgState.ModelName;
		return new FgCheck(Ok: true, HadChanges: true, (modelName != null && modelName.Length > 0) ? (modelName + " is in the game files.") : "Your changes are in the game files.");
	}

	private string BundlePath(string file)
	{
		return Path.Combine(Install.BundleDir, file);
	}

	private string BundleBackup(string file)
	{
		return Path.Combine(BackupDir, "bundles", file);
	}

	public static string Sha(string path)
	{
		using FileStream source = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(source));
	}

	private FgState EnsureBackup(FgState? st)
	{
		string text = Sha(Install.CatalogPath);
		bool flag = File.Exists(CatalogBackup);
		if (((st != null) & flag) && (text == st.CatalogOriginalSha || text == st.CatalogModifiedSha))
		{
			return st;
		}
		if (((st != null) & flag) && st.Swaps.Count == 0 && st.Bundles.Count == 0 && Sha(CatalogBackup) == text)
		{
			return st;
		}
		Directory.CreateDirectory(Path.GetDirectoryName(CatalogBackup));
		File.Copy(Install.CatalogPath, CatalogBackup, overwrite: true);
		if (Directory.Exists(Path.Combine(BackupDir, "bundles")))
		{
			string[] files = Directory.GetFiles(Path.Combine(BackupDir, "bundles"));
			foreach (string obj in files)
			{
				File.Move(obj, obj + ".stale", overwrite: true);
			}
		}
		FgState fgState = new FgState
		{
			CatalogOriginalSha = text
		};
		SaveState(fgState);
		return fgState;
	}

	public void Restore(IProgress<string>? log = null)
	{
		string text = Blocker();
		if (text != null)
		{
			throw new FgException(text);
		}
		FgState fgState = LoadState();
		if (fgState == null || !File.Exists(CatalogBackup))
		{
			log?.Report("Nothing to restore.");
			return;
		}
		string text2 = Sha(Install.CatalogPath);
		if (text2 != fgState.CatalogModifiedSha && text2 != fgState.CatalogOriginalSha)
		{
			log?.Report("The game was updated since the last swap, so its files are already clean.");
			SaveState(null);
			return;
		}
		foreach (FgBundleRecord bundle in fgState.Bundles)
		{
			string to = BundlePath(bundle.File);
			string text3 = BundleBackup(bundle.File);
			if (File.Exists(text3))
			{
				if (Sha(text3) != bundle.OriginalSha)
				{
					throw new FgException("A backup file is damaged (" + bundle.File + "). Use 'Verify files' in the Epic launcher to repair the game.");
				}
				CopyAtomic(text3, to);
				log?.Report("Restored " + bundle.File);
			}
		}
		CopyAtomic(CatalogBackup, Install.CatalogPath);
		log?.Report("Restored the catalog.");
		SaveState(new FgState
		{
			CatalogOriginalSha = fgState.CatalogOriginalSha
		});
	}

	private static void CopyAtomic(string from, string to)
	{
		string text = to + ".drip.tmp";
		File.Copy(from, text, overwrite: true);
		File.Move(text, to, overwrite: true);
	}

	public FgState Apply(FgSwapRequest req, FgCatalog? originalCatalog = null, IProgress<string>? log = null)
	{
		string text = Blocker();
		if (text != null)
		{
			throw new FgException(text);
		}
		FgState fgState = EnsureBackup(LoadState());
		foreach (FgBundleRecord bundle in fgState.Bundles)
		{
			string text2 = BundleBackup(bundle.File);
			if (File.Exists(text2) && Sha(text2) == bundle.OriginalSha)
			{
				CopyAtomic(text2, BundlePath(bundle.File));
			}
		}
		FgCatalog fgCatalog = FgCatalog.Load(CatalogBackup);
		FgLibrary fgLibrary = new FgLibrary(fgCatalog);
		FgState fgState2 = new FgState
		{
			CatalogOriginalSha = fgState.CatalogOriginalSha,
			AppliedUtc = DateTime.UtcNow
		};
		List<string> list = new List<string>();
		foreach (var costumeSwap in req.CostumeSwaps)
		{
			string item = costumeSwap.Target;
			string item2 = costumeSwap.Source;
			bool item3 = costumeSwap.Top;
			bool item4 = costumeSwap.Bottom;
			FgCostume fgCostume = fgLibrary.Find(item) ?? throw new FgException("Unknown costume " + item);
			FgCostume fgCostume2 = fgLibrary.Find(item2) ?? throw new FgException("Unknown costume " + item2);
			List<FgSwapPair> list2 = FgLibrary.Plan(fgCostume, fgCostume2, item3, item4);
			if (list2.Count == 0)
			{
				throw new FgException(fgCostume.Name + " and " + fgCostume2.Name + " don't have matching parts.");
			}
			foreach (FgSwapPair item7 in list2)
			{
				fgCatalog.Retarget(item7.Target.Entry, item7.Source.Entry);
			}
			fgState2.Swaps.Add(new FgSwapRecord
			{
				Target = fgCostume.Key,
				Source = fgCostume2.Key,
				Label = fgCostume.Name + " → " + fgCostume2.Name,
				Top = item3,
				Bottom = item4
			});
			log?.Report($"{fgCostume.Name} now shows as {fgCostume2.Name} ({list2.Count} parts)");
		}
		foreach (var emoteSwap in req.EmoteSwaps)
		{
			string item5 = emoteSwap.Target;
			string item6 = emoteSwap.Source;
			FgEmote fgEmote = fgLibrary.FindEmote(item5) ?? throw new FgException("Unknown emote " + item5);
			FgEmote fgEmote2 = fgLibrary.FindEmote(item6) ?? throw new FgException("Unknown emote " + item6);
			fgCatalog.Retarget(fgEmote.Asset.Entry, fgEmote2.Asset.Entry);
			fgState2.Swaps.Add(new FgSwapRecord
			{
				Kind = "emote",
				Target = fgEmote.Key,
				Source = fgEmote2.Key,
				Label = fgEmote.Name + " → " + fgEmote2.Name
			});
			log?.Report("The " + fgEmote.Name + " emote now plays " + fgEmote2.Name);
		}
		if (req.BeanModel != null)
		{
			string text3 = FindBeanBundle(fgCatalog);
			string text4 = BundlePath(text3);
			string text5 = BundleBackup(text3);
			if (!File.Exists(text5))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text5));
				File.Copy(text4, text5, overwrite: true);
			}
			string originalSha = Sha(text5);
			string text6 = text4 + ".drip.tmp";
			log?.Report("Building the custom bean...");
			FgBeanPatch.Apply(text5, text6, req.BeanModel);
			File.Move(text6, text4, overwrite: true);
			fgState2.Bundles.Add(new FgBundleRecord
			{
				File = text3,
				OriginalSha = originalSha,
				ModifiedSha = Sha(text4)
			});
			fgState2.ModelName = req.BeanModelName ?? "Custom model";
			FgDripFile fgDripFile = new FgDripFile();
			fgDripFile.Name = fgState2.ModelName;
			fgDripFile.Model = req.BeanModel;
			fgDripFile.Save(ActiveModelPath);
			list.Add(text3);
		}
		if (list.Count > 0)
		{
			fgCatalog.ZeroCrc(list);
		}
		string text7 = Path.Combine(WorkDir, "catalog.new");
		fgCatalog.Save(CatalogBackup, text7);
		File.Move(text7, Install.CatalogPath, overwrite: true);
		fgState2.CatalogModifiedSha = Sha(Install.CatalogPath);
		SaveState(fgState2);
		return fgState2;
	}

	public static string FindBeanBundle(FgCatalog cat)
	{
		string text = cat.BundleOfKey("CH_FallGuy_MO.fbx");
		if (text != null)
		{
			return text;
		}
		return "b2ab3afca2ce62c975c193d51b18ab5f.bundle";
	}
}
