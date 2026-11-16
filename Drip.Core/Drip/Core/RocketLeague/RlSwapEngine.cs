using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.RocketLeague;

public sealed class RlSwapEngine
{
	private readonly string _backups;

	private readonly string? _statePath;

	public string CookedDirectory { get; }

	public RlKeys Keys { get; }

	public RlSwapState State { get; }

	public Action<string>? Log { get; set; }

	public bool AllowWhileRunning { get; set; }

	public List<string> LastMapNotes { get; private set; } = new List<string>();

	public RlSwapEngine(string cookedDirectory, RlKeys keys, RlSwapState state, string? backupDir = null, string? statePath = null)
	{
		CookedDirectory = cookedDirectory;
		Keys = keys;
		State = state;
		_backups = backupDir ?? RlSwapState.BackupDir();
		_statePath = statePath;
	}

	private string Live(string file)
	{
		return Path.Combine(CookedDirectory, file);
	}

	private string Backup(string file)
	{
		return Path.Combine(_backups, file);
	}

	private static string Sha(byte[] b)
	{
		return Convert.ToHexString(SHA256.HashData(b));
	}

	private static string ShaFile(string p)
	{
		return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
	}

	public static byte[] BuildSwapped(byte[] donorFile, byte[] donorKey, byte[] targetKey, IReadOnlyList<(string From, string To)> tokens, bool allowResize, out int renamed, bool allowNoRename = false)
	{
		UpkImage upkImage = UpkCodec.Decrypt(donorFile, donorKey);
		List<(string From, string To)> ordered = (from t in tokens
			where t.From.Length > 0 && !t.From.Equals(t.To, StringComparison.Ordinal)
			orderby t.From.Length descending
			select t).ToList();
		byte[] data;
		try
		{
			data = UpkNames.RenameKeepingSize(upkImage.Data, Rename, out renamed, out var _);
		}
		catch (UpkException) when (allowResize)
		{
			data = UpkNames.Rename(upkImage.Data, Rename, padShorter: true, out renamed);
		}
		if (renamed == 0 && !allowNoRename)
		{
			throw new UpkException("Nothing in that package refers to the item, so there is nothing to swap.");
		}
		return UpkCodec.Encrypt(upkImage, data, targetKey);
		string? Rename(string name)
		{
			string text = name;
			foreach (var item3 in ordered)
			{
				string item = item3.From;
				string item2 = item3.To;
				text = ReplaceIgnoreCase(text, item, item2);
			}
			if (!(text == name))
			{
				return text;
			}
			return null;
		}
	}

	private static string ReplaceIgnoreCase(string s, string from, string to)
	{
		int num = s.IndexOf(from, StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return s;
		}
		StringBuilder stringBuilder = new StringBuilder();
		int num2 = 0;
		while (num >= 0)
		{
			stringBuilder.Append(s, num2, num - num2).Append(to);
			num2 = num + from.Length;
			num = s.IndexOf(from, num2, StringComparison.OrdinalIgnoreCase);
		}
		return stringBuilder.Append(s, num2, s.Length - num2).ToString();
	}

	public static List<(string From, string To)> TokensFor(RlItem donor, RlItem target)
	{
		List<(string, string)> list = new List<(string, string)> { (donor.AssetPackage, target.AssetPackage) };
		if (donor.ObjectName.Length > 0 && target.ObjectName.Length > 0 && !donor.ObjectName.Equals(donor.AssetPackage, StringComparison.OrdinalIgnoreCase))
		{
			list.Add((donor.ObjectName, target.ObjectName));
		}
		return list;
	}

	public static string SelfTest(byte[] file, RlKeys keys)
	{
		UpkSummary summary = UpkSummary.Parse(file);
		byte[] array = keys.Find(file, summary) ?? throw new UpkException("None of the keys in keys.txt open this package.");
		UpkImage upkImage = UpkCodec.Decrypt(file, array);
		if (!((ReadOnlySpan<byte>)UpkCodec.Decrypt(UpkCodec.Encrypt(upkImage, upkImage.Data, array), array).Data.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)upkImage.Data))
		{
			throw new UpkException("Rebuilding this package changed it, so swapping isn't safe here.");
		}
		List<NameEntry> list = UpkNames.Read(upkImage.Data, upkImage.Summary, out var endOffset);
		if (endOffset != upkImage.Summary.ImportOffset)
		{
			throw new UpkException("Name table has an unexpected size.");
		}
		return $"{list.Count} names, {upkImage.Chunks.Count} chunks";
	}

	public CarMesh? ReadReferenceCar()
	{
		try
		{
			byte[] array = ReadOriginal("Body_Octane_SF.upk");
			byte[] array2 = Keys.Find(array, UpkSummary.Parse(array));
			if (array2 == null)
			{
				return null;
			}
			return ReferenceCar.FromImage(UpkCodec.Decrypt(array, array2).Data);
		}
		catch
		{
			return null;
		}
	}

	public CarMesh? ReadReferenceBall()
	{
		return ReadStaticReference("GameInfo_Soccar_SF.upk", RlCustomBuilder.GameBalls[0].Mesh);
	}

	public CarMesh? ReadReferenceWheel()
	{
		return ReadStaticReference("WHEEL_Hydra_SF.upk", "WHEEL_Hydra_SM");
	}

	private CarMesh? ReadStaticReference(string file, string mesh)
	{
		try
		{
			byte[] array = ReadOriginal(file);
			byte[] array2 = Keys.Find(array, UpkSummary.Parse(array));
			if (array2 == null)
			{
				return null;
			}
			return ReferenceCar.StaticFromImage(UpkCodec.Decrypt(array, array2).Data, mesh);
		}
		catch
		{
			return null;
		}
	}

	public bool WheelFileIsShell(RlItem wheel)
	{
		try
		{
			byte[] array = ReadOriginal(wheel.FileName);
			byte[] array2 = Keys.Find(array, UpkSummary.Parse(array));
			if (array2 == null)
			{
				return false;
			}
			byte[] data = UpkCodec.Decrypt(array, array2).Data;
			return !UpkTables.Exports(data, UpkSummary.Parse(data)).Any((UpkExport e) => e.ClassName == "StaticMesh" && e.SerialSize > 5000);
		}
		catch
		{
			return false;
		}
	}

	private byte[] ReadOriginal(string file)
	{
		string path = Backup(file);
		if (State.Swaps.Any((RlSwapRecord r) => r.Files.Any((RlFileRecord f) => f.File == file)) && File.Exists(path))
		{
			return File.ReadAllBytes(path);
		}
		return File.ReadAllBytes(Live(file));
	}

	public IReadOnlyList<string> Problems(RlItem target, RlItem donor, bool thumbs)
	{
		List<string> list = new List<string>();
		if (!RlCatalog.SwappableSlots.Contains(target.Slot))
		{
			list.Add(target.Slot + " items can't be swapped.");
		}
		if (!target.Slot.Equals(donor.Slot, StringComparison.OrdinalIgnoreCase))
		{
			list.Add("Both items need to be the same kind (wheels for wheels, and so on).");
		}
		if (target.Multi || donor.Multi)
		{
			list.Add("That item shares a package with others, which can't be swapped yet.");
		}
		if (!donor.IsCustom && target.AssetPackage.Equals(donor.AssetPackage, StringComparison.OrdinalIgnoreCase))
		{
			list.Add("That's the same item.");
		}
		if (!File.Exists(Live(target.FileName)))
		{
			list.Add(target.Name + " isn't in your game files.");
		}
		if (!File.Exists(Live(donor.FileName)))
		{
			list.Add(donor.Name + " isn't in your game files.");
		}
		if (thumbs && !File.Exists(Live(target.ThumbFileName)))
		{
			list.Add("The preview picture file for the item you're replacing is missing.");
		}
		return list;
	}

	public RlSwapRecord Swap(RlItem target, RlItem donor, bool thumbs, bool? allowResize = null)
	{
		if (RlLocator.IsRunning() && !AllowWhileRunning)
		{
			throw new InvalidOperationException("Close Rocket League first. Its files can't change while it's open.");
		}
		IReadOnlyList<string> readOnlyList = Problems(target, donor, thumbs);
		if (readOnlyList.Count > 0)
		{
			throw new InvalidOperationException(readOnlyList[0]);
		}
		if (donor.IsCustom && donor.Slot.Equals("Ball", StringComparison.OrdinalIgnoreCase))
		{
			return SwapBalls(DripRlFile.Load(donor.CustomFile), donor.CustomFile);
		}
		HandleChanges();
		if (donor.IsCustom)
		{
			thumbs = false;
		}
		bool allowResize2 = allowResize ?? State.Settings.AllowResizing;
		List<(string, string)> list = TokensFor(donor, target);
		List<(string, string)> list2 = new List<(string, string)> { (target.FileName, donor.FileName) };
		if (thumbs)
		{
			list2.Add((target.ThumbFileName, donor.ThumbFileName));
		}
		List<(string, byte[])> list3 = new List<(string, byte[])>();
		foreach (var item4 in list2)
		{
			string item = item4.Item1;
			string item2 = item4.Item2;
			byte[] array = ReadOriginal(item);
			byte[] array2 = ReadOriginal(item2);
			byte[] array3 = Keys.Find(array, UpkSummary.Parse(array)) ?? throw new UpkException("None of your keys open " + item + ".");
			byte[] array4 = Keys.Find(array2, UpkSummary.Parse(array2)) ?? throw new UpkException("None of your keys open " + item2 + ".");
			List<(string, string)> tokens = list;
			if (item.EndsWith("_T_SF.upk", StringComparison.OrdinalIgnoreCase))
			{
				tokens = list;
			}
			if (donor.IsCustom && item2 == donor.FileName)
			{
				array2 = RlCustomBuilder.Build(DripRlFile.Load(donor.CustomFile), array2, array4);
			}
			byte[] array5 = BuildSwapped(array2, array4, array3, tokens, allowResize2, out var renamed, donor.IsCustom);
			if (UpkCodec.Decrypt(array5, array3).Data.Length < 64)
			{
				throw new UpkException("Built package looks wrong.");
			}
			list3.Add((item, array5));
			Log?.Invoke($"{item}: {renamed} names renamed");
		}
		RlSwapRecord rlSwapRecord = State.Swaps.FirstOrDefault((RlSwapRecord r) => r.Files.Any((RlFileRecord f) => f.File == target.FileName));
		Directory.CreateDirectory(_backups);
		RlSwapRecord rec = new RlSwapRecord
		{
			Slot = target.Slot,
			TargetName = target.Name,
			TargetPackage = target.AssetPackage,
			DonorName = donor.Name,
			DonorPackage = donor.AssetPackage,
			WithThumbnail = thumbs,
			Custom = donor.IsCustom,
			CustomFile = (donor.CustomFile ?? "")
		};
		foreach (var item5 in list3)
		{
			string file = item5.Item1;
			byte[] item3 = item5.Item2;
			string text = Backup(file);
			if (!File.Exists(text) || !State.Swaps.Any((RlSwapRecord r) => r.Files.Any((RlFileRecord f) => f.File == file)))
			{
				File.Copy(Live(file), text, overwrite: true);
			}
			string text2 = Live(file) + ".drip-tmp";
			File.WriteAllBytes(text2, item3);
			File.Move(text2, Live(file), overwrite: true);
			rec.Files.Add(new RlFileRecord
			{
				File = file,
				WrittenSha = Sha(item3),
				BackupSha = ShaFile(text)
			});
		}
		if (rlSwapRecord != null)
		{
			foreach (RlFileRecord item6 in rlSwapRecord.Files.Where((RlFileRecord f) => rec.Files.All((RlFileRecord n) => n.File != f.File)))
			{
				RestoreFile(item6);
			}
			State.Swaps.Remove(rlSwapRecord);
		}
		State.Swaps.Add(rec);
		State.Save(_statePath);
		return rec;
	}

	public RlSwapRecord SwapBalls(DripRlFile model, string customFile)
	{
		if (RlLocator.IsRunning() && !AllowWhileRunning)
		{
			throw new InvalidOperationException("Close Rocket League first. Its files can't change while it's open.");
		}
		HandleChanges();
		List<(string, byte[])> list = new List<(string, byte[])>();
		string text = null;
		foreach (string item2 in Directory.GetFiles(CookedDirectory, "GameInfo_*_SF.upk").OrderBy((string p) => p, StringComparer.OrdinalIgnoreCase))
		{
			string fileName = Path.GetFileName(item2);
			if (fileName.EndsWith("_T_SF.upk", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			try
			{
				byte[] array = ReadOriginal(fileName);
				byte[] array2 = Keys.Find(array, UpkSummary.Parse(array));
				if (array2 == null)
				{
					Log?.Invoke(fileName + ": none of your keys open it, skipped");
					continue;
				}
				List<string> list2 = new List<string>();
				byte[] array3 = RlCustomBuilder.BuildGameBalls(array, array2, model, list2);
				if (array3 != null)
				{
					list.Add((fileName, array3));
					Log?.Invoke(fileName + ": " + string.Join(", ", list2));
				}
			}
			catch (Exception ex) when ((ex is UpkException || ex is IOException || ex is ArgumentException || ex is InvalidDataException) ? true : false)
			{
				Log?.Invoke(fileName + ": skipped (" + ex.Message + ")");
				if (ex is UpkException && ex.Message.StartsWith("That model", StringComparison.Ordinal) && text == null)
				{
					text = ex.Message;
				}
			}
		}
		if (list.Count == 0 && text != null)
		{
			throw new UpkException(text);
		}
		if (list.Count == 0)
		{
			throw new UpkException("None of the game-mode files that hold the standard ball could be opened with your keys.");
		}
		Directory.CreateDirectory(_backups);
		RlSwapRecord rec = new RlSwapRecord
		{
			Slot = "Ball",
			TargetName = "Standard ball",
			TargetPackage = "Mutators_Balls",
			DonorName = model.Name,
			DonorPackage = "Mutators_Balls",
			WithThumbnail = false,
			Custom = true,
			CustomFile = customFile
		};
		foreach (var item3 in list)
		{
			string file = item3.Item1;
			byte[] item = item3.Item2;
			string text2 = Backup(file);
			if (!File.Exists(text2) || !State.Swaps.Any((RlSwapRecord r) => r.Files.Any((RlFileRecord x) => x.File == file)))
			{
				File.Copy(Live(file), text2, overwrite: true);
			}
			string text3 = Live(file) + ".drip-tmp";
			File.WriteAllBytes(text3, item);
			File.Move(text3, Live(file), overwrite: true);
			rec.Files.Add(new RlFileRecord
			{
				File = file,
				WrittenSha = Sha(item),
				BackupSha = ShaFile(text2)
			});
		}
		foreach (RlSwapRecord item4 in State.Swaps.Where((RlSwapRecord r) => r.Slot.Equals("Ball", StringComparison.OrdinalIgnoreCase)).ToList())
		{
			foreach (RlFileRecord item5 in item4.Files.Where((RlFileRecord f) => rec.Files.All((RlFileRecord n) => n.File != f.File)))
			{
				RestoreFile(item5);
			}
			State.Swaps.Remove(item4);
		}
		State.Swaps.Add(rec);
		State.Save(_statePath);
		return rec;
	}

	public List<string> MapFiles()
	{
		return RlMapCatalog.Discover(Directory.Exists(CookedDirectory) ? Directory.GetFiles(CookedDirectory, "*_P.upk") : Array.Empty<string>()).ToList();
	}

	public RlSwapRecord SwapMap(DripRlFile model, string customFile, string file = "Labs_Underpass_P.upk")
	{
		if (RlLocator.IsRunning() || RlLocator.IsAntiCheatRunning())
		{
			throw new InvalidOperationException("Close Rocket League first. Custom maps are for offline play with anti-cheat off, and the files can't change while the game is open.");
		}
		HandleChanges();
		file = Path.GetFileName(file);
		if (!File.Exists(Live(file)))
		{
			throw new InvalidOperationException(file + " isn't in your game files.");
		}
		byte[] array = ReadOriginal(file);
		byte[] key = Keys.Find(array, UpkSummary.Parse(array)) ?? throw new UpkException("None of your keys open " + file + ".");
		List<string> list = new List<string>();
		MapBuildOptions options = new MapBuildOptions
		{
			Material = (Enum.IsDefined(typeof(MapMaterial), State.Settings.MapMaterial) ? ((MapMaterial)State.Settings.MapMaterial) : MapMaterial.BoostPad),
			KeepScenery = State.Settings.MapKeepScenery,
			TargetName = RlMapCatalog.Pretty(file)
		};
		byte[] array2 = RlMapBuilder.Build(model, array, key, options, list);
		foreach (string item in list)
		{
			Log?.Invoke(file + ": " + item);
		}
		LastMapNotes = list.Where((string l) => l.StartsWith("The collision", StringComparison.Ordinal) || l.StartsWith("Picture:", StringComparison.Ordinal)).ToList();
		Directory.CreateDirectory(_backups);
		string targetName = RlMapCatalog.Pretty(file);
		RlSwapRecord rlSwapRecord = new RlSwapRecord
		{
			Slot = "Map",
			TargetName = targetName,
			TargetPackage = Path.GetFileNameWithoutExtension(file),
			DonorName = model.Name,
			DonorPackage = Path.GetFileNameWithoutExtension(file),
			WithThumbnail = false,
			Custom = true,
			CustomFile = customFile
		};
		string text = Backup(file);
		if (!File.Exists(text) || !State.Swaps.Any((RlSwapRecord r) => r.Files.Any((RlFileRecord x) => x.File == file)))
		{
			File.Copy(Live(file), text, overwrite: true);
		}
		string text2 = Live(file) + ".drip-tmp";
		File.WriteAllBytes(text2, array2);
		File.Move(text2, Live(file), overwrite: true);
		rlSwapRecord.Files.Add(new RlFileRecord
		{
			File = file,
			WrittenSha = Sha(array2),
			BackupSha = ShaFile(text)
		});
		foreach (RlSwapRecord item2 in State.Swaps.Where((RlSwapRecord r) => r.Files.Any((RlFileRecord x) => x.File == file)).ToList())
		{
			State.Swaps.Remove(item2);
		}
		State.Swaps.Add(rlSwapRecord);
		State.Save(_statePath);
		return rlSwapRecord;
	}

	private bool RestoreFile(RlFileRecord f)
	{
		string text = Backup(f.File);
		if (!File.Exists(text))
		{
			return false;
		}
		string text2 = Live(f.File) + ".drip-tmp";
		File.Copy(text, text2, overwrite: true);
		File.Move(text2, Live(f.File), overwrite: true);
		File.Delete(text);
		return true;
	}

	public bool Revert(RlSwapRecord rec)
	{
		if (RlLocator.IsRunning() && !AllowWhileRunning)
		{
			throw new InvalidOperationException("Close Rocket League first. Its files can't change while it's open.");
		}
		bool result = true;
		foreach (RlFileRecord file in rec.Files)
		{
			string text = Live(file.File);
			if (File.Exists(text) && ShaFile(text) != file.WrittenSha)
			{
				try
				{
					File.Delete(Backup(file.File));
				}
				catch
				{
				}
			}
			else if (!RestoreFile(file))
			{
				result = false;
			}
		}
		State.Swaps.Remove(rec);
		State.Save(_statePath);
		return result;
	}

	public int RevertAll()
	{
		int num = 0;
		foreach (RlSwapRecord item in State.Swaps.ToList())
		{
			Revert(item);
			num++;
		}
		return num;
	}

	public void HandleChanges()
	{
		bool flag = false;
		foreach (RlSwapRecord item in State.Swaps.ToList())
		{
			foreach (RlFileRecord item2 in item.Files.ToList())
			{
				string text = Live(item2.File);
				if (File.Exists(text) && ShaFile(text) == item2.WrittenSha)
				{
					continue;
				}
				try
				{
					if (File.Exists(Backup(item2.File)))
					{
						File.Delete(Backup(item2.File));
					}
				}
				catch
				{
				}
				item.Files.Remove(item2);
				flag = true;
			}
			if (item.Files.Count == 0)
			{
				State.Swaps.Remove(item);
				flag = true;
			}
			else if (!item.Files.Any((RlFileRecord f) => (f.File.EndsWith("_SF.upk") && !f.File.EndsWith("_T_SF.upk")) || f.File.EndsWith("_P.upk", StringComparison.OrdinalIgnoreCase)))
			{
				RevertSilently(item);
				flag = true;
			}
		}
		if (flag)
		{
			State.Save(_statePath);
		}
	}

	private void RevertSilently(RlSwapRecord r)
	{
		foreach (RlFileRecord item in r.Files.ToList())
		{
			try
			{
				RestoreFile(item);
			}
			catch
			{
			}
		}
		State.Swaps.Remove(r);
	}
}
