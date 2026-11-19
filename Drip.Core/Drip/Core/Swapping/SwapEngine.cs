using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core.Install;
using Drip.Core.IoStore;
using Drip.Core.Keys;
using Drip.Core.Zen;

namespace Drip.Core.Swapping;

public sealed class SwapEngine
{
	private static readonly byte[] PackageChunkTypes = new byte[3] { 1, 2, 3 };

	private readonly string _paks;

	private readonly AesKeys _keys;

	private readonly StateStore _state;

	private readonly Action<string> _log;

	private string? _backupFolder;

	private const int LockRetries = 60;

	private const int LockRetryDelayMs = 250;

	public List<IoContainer> Containers { get; private set; } = new List<IoContainer>();

	public List<string> Skipped { get; private set; } = new List<string>();

	public bool SaveBackups { get; set; } = true;

	public string BackupFolder
	{
		get
		{
			return _backupFolder ?? PakBackup.FolderFor(_state.FilePath);
		}
		set
		{
			_backupFolder = value;
		}
	}

	public SwapEngine(string paksDirectory, AesKeys keys, StateStore state, Action<string>? log = null)
	{
		_paks = paksDirectory;
		_keys = keys;
		_state = state;
		_log = log ?? ((Action<string>)((string _) =>
		{
		}));
	}

	public void Load()
	{
		HandleBuildChange();
		Mount();
	}

	private void Mount()
	{
		List<string> source = (from n in Directory.EnumerateFiles(_paks, "*.utoc").Select(Path.GetFileNameWithoutExtension)
			where n != null && !n.Equals("global", StringComparison.OrdinalIgnoreCase)
			select n).Cast<string>().OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
		ConcurrentBag<IoContainer> bag = new ConcurrentBag<IoContainer>();
		ConcurrentBag<string> skipped = new ConcurrentBag<string>();
		Parallel.ForEach(source, (string name) =>
		{
			try
			{
				bag.Add(new IoContainer(_paks, name, _keys.Find));
			}
			catch (Exception ex)
			{
				skipped.Add(name + ": " + ex.Message);
				_log("skip " + name + ": " + ex.Message);
			}
		});
		Containers = bag.OrderBy((IoContainer c) => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
		Skipped = skipped.OrderBy((string x) => x, StringComparer.OrdinalIgnoreCase).ToList();
		_log($"Mounted {Containers.Count} containers ({Skipped.Count} skipped).");
		int num = Enumerable.Count(Containers, (IoContainer c) => c.Toc.Encrypted && c.Key == null);
		if (num > 0)
		{
			_log($"{num} encrypted containers have no AES key and can't be read.");
		}
	}

	private void RefreshForMissing(ulong id)
	{
		_log($"Chunk {id:X16} isn't in the {Containers.Count} opened containers; re-reading the game's container list.");
		try
		{
			foreach (string item in Directory.EnumerateFiles(_paks, "*.elenr-bak").ToList())
			{
				string text = item;
				int length = ".elenr-bak".Length;
				string path = text.Substring(0, text.Length - length);
				string name = Path.GetFileNameWithoutExtension(path);
				if (_state.State.Patches.Any((PatchRecord p) => p.Container == name))
				{
					continue;
				}
				try
				{
					bool flag = TocFile.Load(path).FindChunks(id).Any();
					bool flag2 = TocFile.Load(item).FindChunks(id).Any();
					if (flag && !flag2)
					{
						_log(name + ": its saved backup is older than the game's files (the game was updated), so it was dropped.");
						File.Delete(item);
					}
				}
				catch (Exception ex)
				{
					_log(name + ": couldn't compare the backup (" + ex.Message + ").");
				}
			}
		}
		catch (Exception ex2)
		{
			_log("Couldn't check the backups (" + ex2.Message + ").");
		}
		Mount();
	}

	public string? EnsureContainer(string name)
	{
		if (Containers.Any((IoContainer c) => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
		{
			return null;
		}
		string result = null;
		for (int num = 0; num < 3; num++)
		{
			try
			{
				IoContainer element = new IoContainer(_paks, name, _keys.Find);
				Containers = Containers.Append(element).OrderBy((IoContainer x) => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
				Skipped = Skipped.Where((string x) => !x.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)).ToList();
				_log("Opened " + name + ", which the first read had missed.");
				return null;
			}
			catch (Exception ex)
			{
				result = ex.Message;
				Thread.Sleep(400);
			}
		}
		return result;
	}

	public string MountSummary()
	{
		int value = Containers.Sum((IoContainer c) => (int)c.Toc.EntryCount);
		string value2 = string.Join("/", from v in Containers.Select((IoContainer c) => c.Toc.Version).Distinct()
			orderby v
			select v);
		string value3 = ((Skipped.Count == 0) ? "" : ("; couldn't open: " + string.Join(" | ", Skipped.Take(3)) + ((Skipped.Count > 3) ? $" (+{Skipped.Count - 3} more)" : "")));
		int num = Enumerable.Count(Containers, (IoContainer c) => c.Toc.Encrypted && c.Key == null);
		return $"The swapper has {Containers.Count} containers open ({value} entries, toc version {value2}{((num > 0) ? $", {num} locked without a key" : "")}){value3}. Game build {_keys.Build}.";
	}

	private void HandleBuildChange()
	{
		bool flag = false;
		string build = _keys.Build;
		bool flag2 = !string.IsNullOrEmpty(build) && !string.IsNullOrEmpty(_state.State.Build) && _state.State.Build != build;
		if (flag2)
		{
			_log($"Game updated ({_state.State.Build} -> {build}).");
		}
		foreach (string item in Directory.EnumerateFiles(_paks, "*.elenr-bak").ToList())
		{
			string text = item;
			int length = ".elenr-bak".Length;
			string text2 = text.Substring(0, text.Length - length);
			string name = Path.GetFileNameWithoutExtension(text2);
			bool flag3;
			try
			{
				flag3 = IoContainer.BackupIsCurrent(_paks, name, _state.State.Patches);
			}
			catch
			{
				flag3 = false;
			}
			if (flag3)
			{
				continue;
			}
			if (!flag2 && IsSameLayoutAsBackup(text2, item))
			{
				_log(name + ": the toc has changes this app has no record of. The original is kept as a backup; use Revert all / Repair to restore it.");
				flag |= _state.State.Patches.RemoveAll((PatchRecord p) => p.Container == name) > 0;
				continue;
			}
			_log(name + ": the game files changed since the last swap (update or repair), so the old backup was dropped.");
			try
			{
				File.Delete(item);
			}
			catch (Exception ex)
			{
				_log("cleanup " + Path.GetFileName(item) + ": " + ex.Message);
			}
			flag |= _state.State.Patches.RemoveAll((PatchRecord p) => p.Container == name) > 0;
		}
		int num = _state.State.Patches.RemoveAll((PatchRecord p) => !IoContainer.RecordIsApplied(_paks, p));
		if (num > 0)
		{
			_log($"{num} remembered swap(s) are no longer in the game files, so they were forgotten.");
			flag = true;
		}
		if (!string.IsNullOrEmpty(build) && _state.State.Build != build)
		{
			_state.State.Build = build;
			flag = true;
		}
		if (flag)
		{
			_state.Save();
		}
	}

	private static bool IsSameLayoutAsBackup(string live, string bak)
	{
		try
		{
			if (!File.Exists(live) || new FileInfo(live).Length != new FileInfo(bak).Length)
			{
				return false;
			}
			return TocFile.Load(live).DescribeHeaderDifference(TocFile.Load(bak)) == null;
		}
		catch
		{
			return false;
		}
	}

	public (int Restored, int Skipped) RepairFromBackups()
	{
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite before restoring the game files.");
		}
		int num = 0;
		int num2 = 0;
		foreach (string item in Directory.EnumerateFiles(_paks, "*.elenr-bak").ToList())
		{
			string text = item;
			int length = ".elenr-bak".Length;
			string text2 = text.Substring(0, text.Length - length);
			string name = Path.GetFileNameWithoutExtension(text2);
			try
			{
				if (!File.Exists(text2) || !IsSameLayoutAsBackup(text2, item))
				{
					_log(name + ": the backup belongs to a different version of the game, so it was left alone.");
					num2++;
					continue;
				}
				long[] array = TocFile.Load(item).PartitionEnds();
				foreach (string item2 in new string[1] { text2 }.Concat(from i in Enumerable.Range(0, array.Length)
					select UcasFile(name, i)))
				{
					if (File.Exists(item2) && (File.GetAttributes(item2) & FileAttributes.ReadOnly) != 0)
					{
						File.SetAttributes(item2, File.GetAttributes(item2) & ~FileAttributes.ReadOnly);
					}
				}
				bool flag = !((ReadOnlySpan<byte>)File.ReadAllBytes(text2).AsSpan()).SequenceEqual((ReadOnlySpan<byte>)File.ReadAllBytes(item));
				if (flag)
				{
					File.Copy(item, text2, overwrite: true);
				}
				for (int num3 = 0; num3 < array.Length; num3++)
				{
					string text3 = UcasFile(name, num3);
					if (File.Exists(text3) && array[num3] > 0 && new FileInfo(text3).Length > array[num3])
					{
						using FileStream fileStream = new FileStream(text3, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
						fileStream.SetLength(array[num3]);
						flag = true;
					}
				}
				File.Delete(item);
				_state.State.Patches.RemoveAll((PatchRecord p) => p.Container == name);
				if (flag)
				{
					num++;
					_log(name + ": restored from the backup.");
				}
			}
			catch (Exception ex)
			{
				num2++;
				_log(name + ": couldn't restore (" + ex.Message + ").");
			}
		}
		_state.Save();
		return (Restored: num, Skipped: num2);
	}

	private string UcasFile(string name, int partition)
	{
		return Path.Combine(_paks, (partition == 0) ? (name + ".ucas") : $"{name}_s{partition}.ucas");
	}

	public List<ChunkHit> FindChunks(ulong id)
	{
		List<ChunkHit> list = new List<ChunkHit>();
		foreach (IoContainer container in Containers)
		{
			foreach (int item in container.Toc.FindChunks(id))
			{
				list.Add(new ChunkHit(container, item, container.Toc.GetChunkId(item).Type));
			}
		}
		return list;
	}

	public List<ChunkHit> FindChunksFresh(ulong id)
	{
		List<ChunkHit> list = FindChunks(id);
		if (list.Count > 0)
		{
			return list;
		}
		RefreshForMissing(id);
		return FindChunks(id);
	}

	public List<string> CheckPackagesReadable(IEnumerable<(string Package, byte Type)> wanted)
	{
		List<string> list = new List<string>();
		foreach (var item3 in wanted)
		{
			string item = item3.Package;
			byte item2 = item3.Type;
			string text = item.Substring(item.LastIndexOf('/') + 1);
			try
			{
				ReadPackageChunk(item, item2);
			}
			catch (InvalidOperationException ex) when (ex.Message.Contains("isn't in your game files"))
			{
				list.Add(text + " isn't in your game files");
			}
			catch (Exception ex2)
			{
				list.Add(text + " can't be read (" + ex2.Message + ")");
			}
		}
		return list;
	}

	public (List<ChunkHit> Hits, ZenPackage Package) Resolve(string packageName, ulong chunkIdOverride = 0uL)
	{
		bool flag = chunkIdOverride != 0;
		ulong num = (flag ? chunkIdOverride : PackageNames.ChunkId(packageName));
		List<ChunkHit> list = FindChunks(num);
		if ((list.Count == 0) & flag)
		{
			RefreshForMissing(num);
			list = FindChunks(num);
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException(flag ? $"'{packageName}' (chunk {num:X16}) is in the game's index but not in any container the swapper can read. {MountSummary()} Press Connect again; if it still fails, send me this message." : $"'{packageName}' not found in any container (chunk id {num:X16}). Check the path, or the package-id hashing changed in this build. {MountSummary()}");
		}
		List<string> list2 = new List<string>();
		byte[] packageChunkTypes = PackageChunkTypes;
		foreach (byte type in packageChunkTypes)
		{
			List<ChunkHit> list3 = list.Where((ChunkHit h) => h.Type == type).ToList();
			if (list3.Count == 0)
			{
				continue;
			}
			try
			{
				ZenPackage zenPackage = ZenPackage.Parse(list3[0].Container.ReadChunk(list3[0].Index));
				if (flag || string.Equals(zenPackage.PackageName, packageName, StringComparison.OrdinalIgnoreCase))
				{
					return (Hits: list3, Package: zenPackage);
				}
				list2.Add($"type {type}: header says '{zenPackage.PackageName}'");
			}
			catch (Exception ex)
			{
				list2.Add($"type {type}: {ex.Message}");
			}
		}
		throw new InvalidOperationException($"Found chunk id {num:X16} for '{packageName}' but it did not verify as that package ({string.Join("; ", list2)}). Types present: {string.Join(",", list.Select((ChunkHit h) => h.Type).Distinct())}.");
	}

	public SwapResult Swap(string sourcePackage, string targetPackage, ulong sourceChunk = 0uL, ulong targetChunk = 0uL, IReadOnlyList<StyleRename>? styleRenames = null)
	{
		string text = PackageNames.Normalize(sourcePackage);
		string text2 = PackageNames.Normalize(targetPackage);
		if (string.Equals(text, text2, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Source and target are the same package.");
		}
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite (and the launcher's Fortnite process) before swapping.");
		}
		_log("Resolving " + text);
		var (list, zenPackage) = Resolve(text, sourceChunk);
		_log("Resolving " + text2);
		ZenPackage item = Resolve(text2, targetChunk).Package;
		if (sourceChunk != 0L && !string.IsNullOrEmpty(zenPackage.PackageName))
		{
			text = zenPackage.PackageName;
		}
		if (targetChunk != 0L && !string.IsNullOrEmpty(item.PackageName))
		{
			text2 = item.PackageName;
		}
		int oldSize = list[0].Container.ReadChunk(list[0].Index).Length;
		ZenCloner.CloneAs(item, text2, zenPackage, text);
		if (styleRenames != null && styleRenames.Count > 0)
		{
			int num = StyleTweaks.Apply(item, styleRenames);
			_log((num > 0) ? $"Style applied ({num} name entries retargeted)." : "Style parts weren't referenced by name in this definition; default style kept.");
		}
		byte[] array = item.Serialize();
		foreach (ChunkHit item2 in list)
		{
			if (ZenSize(array) > item2.Container.Capacity(item2.Index))
			{
				throw new InvalidOperationException($"Patched package is {array.Length} bytes, but the slot it replaces in {item2.Container.Name} only holds {item2.Container.Capacity(item2.Index)}.");
			}
			AssertUnlocked(item2.Container);
		}
		int num2 = 0;
		List<PatchRecord> list2 = new List<PatchRecord>();
		foreach (ChunkHit item3 in list)
		{
			item3.Container.EnsureBackup();
			PatchRecord patchRecord = item3.Container.PatchChunk(item3.Index, array);
			patchRecord.SourcePackage = text;
			patchRecord.TargetPackage = text2;
			_state.Add(patchRecord);
			SaveOriginalPak(item3.Container);
			list2.Add(patchRecord);
			num2++;
			_log($"Patched {item3.Container.Name} chunk {item3.Index}.");
		}
		return new SwapResult
		{
			Source = text,
			Target = text2,
			ContainersPatched = num2,
			OldSize = oldSize,
			NewSize = array.Length,
			HashMismatchesInTarget = item.HashMismatches
		};
	}

	public LayoutCheck CheckLayout(string sourcePackage, ulong sourceChunk, string targetPackage, ulong targetChunk)
	{
		ZenPackage item = Resolve(PackageNames.Normalize(sourcePackage), sourceChunk).Package;
		ZenPackage item2 = Resolve(PackageNames.Normalize(targetPackage), targetChunk).Package;
		ZenLayout layout = item.Layout;
		ZenLayout layout2 = item2.Layout;
		return new LayoutCheck(layout, layout2, ZenLayout.Differences(layout, layout2));
	}

	public SwapResult SwapParts(string sourcePackage, ulong sourceChunk, IReadOnlyList<StyleRename> renames)
	{
		string text = PackageNames.Normalize(sourcePackage);
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite (and the launcher's Fortnite process) before swapping.");
		}
		if (renames.Count == 0)
		{
			throw new InvalidOperationException("No character parts to point at.");
		}
		_log("Resolving " + text);
		var (list, zenPackage) = Resolve(text, sourceChunk);
		if (sourceChunk != 0L && !string.IsNullOrEmpty(zenPackage.PackageName))
		{
			text = zenPackage.PackageName;
		}
		int oldSize = list[0].Container.ReadChunk(list[0].Index).Length;
		int num = StyleTweaks.Apply(zenPackage, renames);
		if (num == 0)
		{
			throw new InvalidOperationException(text + " doesn't refer to those character parts by name, so there is nothing to point at the other skin.");
		}
		_log($"Pointed {renames.Count} character part(s) at the other skin ({num} name entries).");
		byte[] array = zenPackage.Serialize();
		foreach (ChunkHit item in list)
		{
			if (ZenSize(array) > item.Container.Capacity(item.Index))
			{
				throw new InvalidOperationException($"Patched package is {array.Length} bytes, but the slot it replaces in {item.Container.Name} only holds {item.Container.Capacity(item.Index)}.");
			}
			AssertUnlocked(item.Container);
		}
		int num2 = 0;
		foreach (ChunkHit item2 in list)
		{
			item2.Container.EnsureBackup();
			PatchRecord patchRecord = item2.Container.PatchChunk(item2.Index, array);
			patchRecord.SourcePackage = text;
			patchRecord.TargetPackage = "parts:" + renames[0].NewPackage;
			_state.Add(patchRecord);
			SaveOriginalPak(item2.Container);
			num2++;
			_log($"Patched {item2.Container.Name} chunk {item2.Index}.");
		}
		return new SwapResult
		{
			Source = text,
			Target = renames[0].NewPackage,
			ContainersPatched = num2,
			OldSize = oldSize,
			NewSize = array.Length
		};
	}

	public SwapResult ReplaceRaw(string packageName, byte[] data, string modName = "mod")
	{
		string text = PackageNames.Normalize(packageName);
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite (and the launcher's Fortnite process) before applying a mod.");
		}
		ZenPackage zenPackage;
		try
		{
			zenPackage = ZenPackage.Parse(data);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(text + ": the mod's file is not a readable package (" + ex.Message + ").");
		}
		if (!string.Equals(zenPackage.PackageName, text, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(text + ": the mod's file is for '" + zenPackage.PackageName + "', not this package.");
		}
		List<ChunkHit> item = Resolve(text, 0uL).Hits;
		int oldSize = item[0].Container.ReadChunk(item[0].Index).Length;
		foreach (ChunkHit item2 in item)
		{
			if (ZenSize(data) > item2.Container.Capacity(item2.Index))
			{
				throw new InvalidOperationException($"{text}: mod data is {data.Length} bytes, but the slot it replaces in {item2.Container.Name} only holds {item2.Container.Capacity(item2.Index)}. A package can't grow past the space the original used.");
			}
			AssertUnlocked(item2.Container);
		}
		int num = 0;
		List<PatchRecord> list = new List<PatchRecord>();
		foreach (ChunkHit item3 in item)
		{
			item3.Container.EnsureBackup();
			PatchRecord patchRecord = item3.Container.PatchChunk(item3.Index, data);
			patchRecord.SourcePackage = text;
			patchRecord.TargetPackage = "mod:" + modName;
			_state.Add(patchRecord);
			SaveOriginalPak(item3.Container);
			list.Add(patchRecord);
			num++;
			_log($"Patched {item3.Container.Name} chunk {item3.Index} ({text}).");
		}
		return new SwapResult
		{
			Source = text,
			Target = "mod:" + modName,
			ContainersPatched = num,
			OldSize = oldSize,
			NewSize = data.Length
		};
	}

	public byte[] ReadPackageChunk(string packageName, byte type)
	{
		string text = PackageNames.Normalize(packageName);
		ChunkHit chunkHit = FindChunksFresh(PackageNames.ChunkId(text)).FirstOrDefault((ChunkHit h) => h.Type == type) ?? throw new InvalidOperationException($"'{text}' (chunk type {type}) isn't in your game files.");
		return chunkHit.Container.ReadChunk(chunkHit.Index);
	}

	public long ChunkCapacity(string packageName, byte type)
	{
		string text = PackageNames.Normalize(packageName);
		List<ChunkHit> list = (from h in FindChunksFresh(PackageNames.ChunkId(text))
			where h.Type == type
			select h).ToList();
		if (list.Count == 0)
		{
			throw new InvalidOperationException($"'{text}' (chunk type {type}) isn't in your game files.");
		}
		return list.Min((ChunkHit h) => h.Container.Capacity(h.Index));
	}

	public SwapResult PatchPackageChunk(string packageName, byte type, byte[] data, string label)
	{
		string text = PackageNames.Normalize(packageName);
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite (and the launcher's Fortnite process) before applying a model.");
		}
		List<ChunkHit> list = (from h in FindChunksFresh(PackageNames.ChunkId(text))
			where h.Type == type
			select h).ToList();
		if (list.Count == 0)
		{
			throw new InvalidOperationException($"'{text}' (chunk type {type}) isn't in your game files.");
		}
		int oldSize = list[0].Container.ReadChunk(list[0].Index).Length;
		foreach (ChunkHit item in list)
		{
			if (ZenSize(data) > item.Container.Capacity(item.Index))
			{
				throw new InvalidOperationException($"{text}: the new data is {data.Length} bytes, but the slot it replaces in {item.Container.Name} only holds {item.Container.Capacity(item.Index)}.");
			}
			AssertUnlocked(item.Container);
		}
		int num = 0;
		foreach (ChunkHit item2 in list)
		{
			item2.Container.EnsureBackup();
			PatchRecord patchRecord = item2.Container.PatchChunk(item2.Index, data);
			patchRecord.SourcePackage = text;
			patchRecord.TargetPackage = "model:" + label;
			_state.Add(patchRecord);
			SaveOriginalPak(item2.Container);
			num++;
			_log($"Patched {item2.Container.Name} chunk {item2.Index} ({text}, type {type}).");
			try
			{
				TocFile.Load(item2.Container.LiveUtocPath);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(item2.Container.Name + ": the game files were damaged by this write (" + ex.Message + "). Nothing is kept; try again with Fortnite and the Epic Launcher closed.");
			}
		}
		return new SwapResult
		{
			Source = text,
			Target = "model:" + label,
			ContainersPatched = num,
			OldSize = oldSize,
			NewSize = data.Length
		};
	}

	private void SaveOriginalPak(IoContainer c)
	{
		if (SaveBackups)
		{
			PakBackup.Save(BackupFolder, _paks, c.Name, _state.State.Build, _state.State.Patches, _log);
		}
	}

	private static int ZenSize(byte[] b)
	{
		return (int)IoContainer.Align16((uint)b.Length);
	}

	private void VerifyOrRollBack(PatchRecord rec, List<PatchRecord> done, string package)
	{
		VerifyReport verifyReport = SwapVerifier.VerifyRecord(_paks, rec, _keys.Find);
		foreach (VerifyLine line in verifyReport.Lines)
		{
			_log("verify " + line);
		}
		if (verifyReport.Ok)
		{
			return;
		}
		foreach (PatchRecord item in Enumerable.Reverse(done))
		{
			try
			{
				IoContainer.Revert(_paks, item);
			}
			catch (Exception ex)
			{
				_log("rollback " + item.Container + ": " + ex.Message);
			}
			_state.State.Patches.Remove(item);
		}
		_state.Save();
		string text = string.Join(" ", from l in verifyReport.Lines
			where l.Level == VerifyLevel.Fail
			select l.Text);
		throw new InvalidOperationException(package + ": the game files did not take the change, so it was undone. " + text);
	}

	public VerifyReport Verify(IEnumerable<string>? sourcePackages = null, bool deep = true)
	{
		HashSet<string> set = ((sourcePackages == null) ? null : new HashSet<string>(sourcePackages, StringComparer.OrdinalIgnoreCase));
		List<PatchRecord> records = _state.State.Patches.Where((PatchRecord r) => set == null || set.Contains(r.SourcePackage)).ToList();
		VerifyReport verifyReport = SwapVerifier.Run(_paks, records, _keys.Find, deep);
		foreach (string item in SwapVerifier.Preflight(_paks))
		{
			verifyReport.Add(VerifyLevel.Warn, "", item);
		}
		return verifyReport;
	}

	private static void AssertUnlocked(IoContainer c)
	{
		foreach (string item in c.FilesToLock())
		{
			try
			{
				FileAttributes attributes = File.GetAttributes(item);
				if ((attributes & FileAttributes.ReadOnly) != 0)
				{
					File.SetAttributes(item, attributes & ~FileAttributes.ReadOnly);
				}
			}
			catch
			{
			}
			int num = 0;
			while (true)
			{
				try
				{
					using (new FileStream(item, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
					{
					}
				}
				catch (IOException) when (num < 60)
				{
					Thread.Sleep(250);
					goto IL_0087;
				}
				catch (IOException)
				{
					throw new IOException(Path.GetFileName(item) + " is in use by another program. Fortnite and the Epic launcher must be closed, but it can also be an antivirus scan, Windows Search, or a second copy of Drip Swapper / Galaxy Drip. Wait about 30 seconds and try again.");
				}
				break;
				IL_0087:
				num++;
			}
		}
	}

	public bool IsActive(IEnumerable<string> sourcePackages)
	{
		HashSet<string> set = new HashSet<string>(sourcePackages, StringComparer.OrdinalIgnoreCase);
		if (set.Count == 0)
		{
			return false;
		}
		List<PatchRecord> list = _state.State.Patches.Where((PatchRecord r) => set.Contains(r.SourcePackage)).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(list.Select((PatchRecord r) => r.SourcePackage), StringComparer.OrdinalIgnoreCase);
		if (set.All(hashSet.Contains))
		{
			return list.All((PatchRecord r) => IoContainer.IsPatched(_paks, r));
		}
		return false;
	}

	public List<string> MissingPackages(IEnumerable<string> sourcePackages)
	{
		List<string> list = new List<string>();
		foreach (string pkg in sourcePackages.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			List<PatchRecord> list2 = _state.State.Patches.Where((PatchRecord r) => string.Equals(r.SourcePackage, pkg, StringComparison.OrdinalIgnoreCase)).ToList();
			if (list2.Count == 0 || !list2.All((PatchRecord r) => IoContainer.IsPatched(_paks, r)))
			{
				list.Add(pkg);
			}
		}
		return list;
	}

	public (int Reverted, int Skipped) RevertPackages(IEnumerable<string> sourcePackages)
	{
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite before reverting.");
		}
		HashSet<string> set = new HashSet<string>(sourcePackages, StringComparer.OrdinalIgnoreCase);
		int num = 0;
		int num2 = 0;
		foreach (PatchRecord item in _state.State.Patches.Where((PatchRecord r) => set.Contains(r.SourcePackage)).Reverse().ToList())
		{
			if (IoContainer.Revert(_paks, item))
			{
				num++;
			}
			else
			{
				num2++;
			}
			_state.State.Patches.Remove(item);
		}
		_state.Save();
		return (Reverted: num, Skipped: num2);
	}

	public (int Reverted, int Skipped) RevertAll()
	{
		if (FortniteLocator.IsFortniteRunning())
		{
			throw new InvalidOperationException("Close Fortnite before reverting.");
		}
		int num = 0;
		int num2 = 0;
		foreach (PatchRecord item2 in _state.State.Patches.AsEnumerable().Reverse().ToList())
		{
			if (IoContainer.Revert(_paks, item2))
			{
				num++;
				continue;
			}
			num2++;
			_log($"Skipped {item2.Container} chunk {item2.ChunkIndex}: toc changed since the swap.");
		}
		_state.Clear();
		_log($"Reverted {num}, skipped {num2}.");
		try
		{
			int item = RepairFromBackups().Restored;
			if (item > 0)
			{
				num += item;
				num2 = Math.Max(0, num2 - item);
			}
		}
		catch (Exception ex)
		{
			_log("restore from backups: " + ex.Message);
		}
		return (Reverted: num, Skipped: num2);
	}
}
