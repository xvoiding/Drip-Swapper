using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Drip.Core.IoStore;
using Drip.Core.Swapping;

namespace Drip.Core.Retrac;

public sealed class RetracSwapper
{
	private sealed class State
	{
		public string Paks { get; set; } = "";

		public List<RetracSwap> Swaps { get; set; } = new List<RetracSwap>();

		public List<TouchedContainer> Containers { get; set; } = new List<TouchedContainer>();
	}

	private sealed class TouchedContainer
	{
		public string Name { get; set; } = "";

		public string UcasFile { get; set; } = "";

		public long UcasLengthBefore { get; set; }

		public long UcasLengthAfter { get; set; }

		public string UtocSha1After { get; set; } = "";

		public DateTime UcasMtimeBeforeUtc { get; set; }

		public DateTime UtocMtimeBeforeUtc { get; set; }
	}

	private sealed record Loc(IoContainer Container, int Chunk);

	private sealed record PlannedWrite(IoContainer Container, int Chunk, byte[] Data);

	private sealed class Held : IDisposable
	{
		private bool _done;

		public void Dispose()
		{
			if (!_done)
			{
				_done = true;
				try
				{
					ProcessLock.ReleaseMutex();
				}
				catch
				{
				}
				Monitor.Exit(Gate);
			}
		}
	}

	private sealed class RetracIndex
	{
		private readonly List<IoContainer> _containers = new List<IoContainer>();

		private readonly Dictionary<ulong, List<Loc>> _packages = new Dictionary<ulong, List<Loc>>();

		private readonly Dictionary<IoContainer, int> _headers = new Dictionary<IoContainer, int>();

		public int Locked { get; private set; }

		public int Count => _containers.Count;

		public bool Has(string id, string type)
		{
			return RetracPaths.Candidates(id, type).Any((string path) => _packages.ContainsKey(PackageNames.ChunkId(path)));
		}

		public static RetracIndex Build(string paks)
		{
			RetracIndex retracIndex = new RetracIndex();
			foreach (string item in Directory.EnumerateFiles(paks, "*.utoc").OrderBy((string p) => p, StringComparer.OrdinalIgnoreCase))
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
				try
				{
					IoContainer ioContainer = new IoContainer(paks, fileNameWithoutExtension, (string _) => (byte[]?)null);
					if (ioContainer.Toc.Encrypted)
					{
						retracIndex.Locked++;
						continue;
					}
					retracIndex._containers.Add(ioContainer);
					TocFile toc = ioContainer.Toc;
					for (int num = 0; num < toc.EntryCount; num++)
					{
						(ulong Id, byte Type) chunkId = toc.GetChunkId(num);
						var (key, _) = chunkId;
						switch (chunkId.Type)
						{
						case 2:
						{
							if (!retracIndex._packages.TryGetValue(key, out List<Loc> value))
							{
								value = (retracIndex._packages[key] = new List<Loc>());
							}
							value.Add(new Loc(ioContainer, num));
							break;
						}
						case 10:
							if (!retracIndex._headers.ContainsKey(ioContainer))
							{
								retracIndex._headers[ioContainer] = num;
							}
							break;
						}
					}
				}
				catch (InvalidDataException)
				{
					retracIndex.Locked++;
				}
				catch (EndOfStreamException)
				{
					retracIndex.Locked++;
				}
			}
			return retracIndex;
		}

		public bool TryFind(string path, out List<Loc> locs)
		{
			return _packages.TryGetValue(PackageNames.ChunkId(path), out locs);
		}

		private Ue426Package? Load(string path)
		{
			if (!TryFind(path, out List<Loc> locs))
			{
				return null;
			}
			return Ue426Package.Parse(locs[0].Container.ReadChunk(locs[0].Chunk));
		}

		private static string? Soft(Ue426Package p, string prefix)
		{
			return p.Names.FirstOrDefault((string n) => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !n.Contains('.'));
		}

		private static IEnumerable<string> PathsUnder(Ue426Package p, string prefix)
		{
			return p.Names.Where((string n) => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !n.Contains('.'));
		}

		private static Dictionary<string, string>? StyleParts(Ue426Package wCid, Ue426Package wHs, string wHsPath, string tag, string name, List<string> notes)
		{
			HashSet<string> mine = new HashSet<string>(wHs.Names.Where((string n) => !n.Contains('.')), StringComparer.OrdinalIgnoreCase);
			string[] kinds = new string[4] { "/Bodies/", "/Heads/", "/Hats/", "/FaceAccessories/" };
			List<string> list = wCid.Names.Where((string n) => n.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) && !n.Contains('.') && !mine.Contains(n) && kinds.Any((string value) => n.Contains(value, StringComparison.OrdinalIgnoreCase))).ToList();
			List<string> list2 = list.Where((string n) => n.Contains("/Bodies/", StringComparison.OrdinalIgnoreCase)).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
			if (list.Count == 0)
			{
				notes.Add("Style \"" + name + "\" isn't a body/head style on this skin (this one has no colour or part change Drip can read), so you get the default look.");
				return null;
			}
			int num = 2;
			string text = new string(tag.Where(char.IsDigit).ToArray());
			if (text.Length > 0 && int.TryParse(text, out var result))
			{
				num = result;
			}
			string text2 = ((list2.Count > 0) ? list2[Math.Clamp(num - 2, 0, list2.Count - 1)] : null);
			string family = text2?.Substring(text2.LastIndexOf('/') + 1).Split('_').Last();
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			if (text2 != null)
			{
				dictionary["/Bodies/"] = text2;
			}
			foreach (string k in kinds.Skip(1))
			{
				List<string> list3 = list.Where((string v) => v.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
				if (list3.Count != 0)
				{
					dictionary[k] = ((family != null) ? list3.FirstOrDefault((string v) => v.EndsWith(family, StringComparison.OrdinalIgnoreCase)) : null) ?? list3[0];
				}
			}
			return dictionary;
		}

		public IEnumerable<(string OwnedName, string OwnedPath, string WantedName, string WantedPath)> PartPairs(Loc ownedCid, Loc wantedCid, bool hatIntoHead, string styleTag, string styleName, List<string> notes)
		{
			Ue426Package p = Ue426Package.Parse(ownedCid.Container.ReadChunk(ownedCid.Chunk));
			Ue426Package ue426Package = Ue426Package.Parse(wantedCid.Container.ReadChunk(wantedCid.Chunk));
			string text = Soft(p, "/Game/Athena/Heroes/HID_");
			string text2 = Soft(ue426Package, "/Game/Athena/Heroes/HID_");
			if (text == null || text2 == null)
			{
				yield break;
			}
			Ue426Package ue426Package2 = Load(text);
			Ue426Package ue426Package3 = Load(text2);
			if (ue426Package2 == null || ue426Package3 == null)
			{
				yield break;
			}
			string oHsPath = Soft(ue426Package2, "/Game/Athena/Heroes/Specializations/HS_");
			string wHsPath = Soft(ue426Package3, "/Game/Athena/Heroes/Specializations/HS_");
			if (oHsPath == null || wHsPath == null)
			{
				yield break;
			}
			Ue426Package oHs = Load(oHsPath);
			Ue426Package wHs = Load(wHsPath);
			if (oHs == null || wHs == null)
			{
				yield break;
			}
			List<(string Swap, string Use)> mats = null;
			if (!IsDefaultStyle(styleTag, styleName))
			{
				Ue426Variants.Option option = Ue426Variants.Pick(Ue426Variants.Parse(ue426Package), styleTag, styleName);
				if ((object)option != null && option.Materials.Count > 0)
				{
					mats = option.Materials;
				}
			}
			Dictionary<string, string> style = ((IsDefaultStyle(styleTag, styleName) || mats != null) ? null : StyleParts(ue426Package, wHs, wHsPath, styleTag, styleName, notes));
			if (style == null)
			{
				yield return (OwnedName: "the hero's parts list", OwnedPath: oHsPath, WantedName: "the wanted hero's parts list", WantedPath: wHsPath);
			}
			string oHat = Parts(oHs, oHsPath, "/Hats/");
			string[] array = new string[3] { "/Bodies/", "/Heads/", "/Hats/" };
			foreach (string text3 in array)
			{
				string text4 = Parts(oHs, oHsPath, text3);
				string text5 = ((style != null && style.TryGetValue(text3, out string value)) ? value : Parts(wHs, wHsPath, text3));
				string text6 = text3.Trim('/').ToLowerInvariant();
				string text7 = ((style != null && style.TryGetValue("/Hats/", out string value2)) ? value2 : Parts(wHs, wHsPath, "/Hats/"));
				if ((text3 == "/Heads/" && text4 != null && text7 != null && oHat == null) & hatIntoHead)
				{
					yield return (OwnedName: "head part", OwnedPath: text4, WantedName: "the wanted hat", WantedPath: text7);
				}
				else if (text4 != null && text5 != null)
				{
					yield return (OwnedName: text6 + " part", OwnedPath: text4, WantedName: "the wanted " + text6 + " part", WantedPath: text5);
				}
			}
			if (mats == null)
			{
				yield break;
			}
			foreach (var (item, item2) in mats)
			{
				yield return (OwnedName: "a base material", OwnedPath: item, WantedName: "the style's material", WantedPath: item2);
			}
		}

		public IEnumerable<(string OwnedName, string OwnedPath, string WantedName, string WantedPath)> SoftPairs(Loc owned, Loc wanted, string prefix, string label)
		{
			Ue426Package p = Ue426Package.Parse(owned.Container.ReadChunk(owned.Chunk));
			Ue426Package p2 = Ue426Package.Parse(wanted.Container.ReadChunk(wanted.Chunk));
			string text = Soft(p, prefix);
			string text2 = Soft(p2, prefix);
			if (text != null && text2 != null)
			{
				yield return (OwnedName: "the " + label, OwnedPath: text, WantedName: "the wanted " + label, WantedPath: text2);
			}
		}

		private static string? Parts(Ue426Package hs, string hsPath, string kind)
		{
			return hs.Names.FirstOrDefault((string n) => n.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) && !n.Contains('.') && !n.Equals(hsPath, StringComparison.OrdinalIgnoreCase) && n.Contains(kind, StringComparison.OrdinalIgnoreCase));
		}

		public int? HeaderChunk(IoContainer c)
		{
			if (!_headers.TryGetValue(c, out var value))
			{
				return null;
			}
			return value;
		}

		public (string Path, ulong Id, List<Loc> Locs) Resolve(string id, string type, string display, string role)
		{
			foreach (string item in RetracPaths.Candidates(id, type))
			{
				ulong num = PackageNames.ChunkId(item);
				if (_packages.TryGetValue(num, out List<Loc> value))
				{
					return (Path: item, Id: num, Locs: value);
				}
			}
			throw new RetracPlanException(display + " (" + role + ") isn't in the game files Drip Swapper can open." + ((Locked > 0) ? $"\n\nRetrac keeps its newest items in {Locked} locked file{((Locked == 1) ? "" : "s")} that can't be read yet. Try an older item for now." : ""));
		}
	}

	private sealed class RetracPlanException : Exception
	{
		public RetracPlanException(string message)
			: base(message)
		{
		}
	}

	private static readonly object Gate = new object();

	private static readonly Mutex ProcessLock = new Mutex(initiallyOwned: false, "Local\\DripSwapper.RetracFiles");

	private readonly string _paks;

	private readonly string _stateFile;

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public RetracSignatures? SignaturesOverride { get; set; }

	public bool HatIntoHead => true;

	public RetracSignatures Signatures => SignaturesOverride.GetValueOrDefault();

	public static bool IsDefaultStyle(string tag, string name)
	{
		if ((!string.IsNullOrWhiteSpace(tag) || !string.IsNullOrWhiteSpace(name)) && !tag.Equals("Stage1", StringComparison.OrdinalIgnoreCase))
		{
			return name.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static IDisposable Enter()
	{
		Monitor.Enter(Gate);
		bool flag;
		try
		{
			flag = ProcessLock.WaitOne(TimeSpan.FromSeconds(60L));
		}
		catch (AbandonedMutexException)
		{
			flag = true;
		}
		if (!flag)
		{
			Monitor.Exit(Gate);
			throw new IOException("Another Drip Swapper window is writing the Retrac files. Close the other window and try again.");
		}
		return new Held();
	}

	public RetracSwapper(string paksDirectory, string stateDirectory)
	{
		_paks = paksDirectory;
		Directory.CreateDirectory(stateDirectory);
		_stateFile = Path.Combine(stateDirectory, "retrac-swaps.json");
	}

	public IReadOnlyList<RetracSwap> Active()
	{
		return LoadState().Swaps;
	}

	public void Tidy()
	{
		using (Enter())
		{
			DropStaleBackups(LoadState());
		}
	}

	public bool FilesIntact()
	{
		State state = LoadState();
		if (state.Containers.Count == 0)
		{
			return false;
		}
		foreach (TouchedContainer container in state.Containers)
		{
			string path = Path.Combine(_paks, container.Name + ".utoc");
			if (!File.Exists(path) || Sha1Of(path) != container.UtocSha1After)
			{
				return false;
			}
		}
		return true;
	}

	public RetracResult Apply(RetracSwap add)
	{
		List<RetracSwap> list = LoadState().Swaps.Where((RetracSwap s) => !s.OwnedId.Equals(add.OwnedId, StringComparison.OrdinalIgnoreCase)).ToList();
		list.Add(add);
		return ApplySet(list, $"Swapped {add.WantedName} onto {add.OwnedName}.\n\nStart Retrac and look at {add.OwnedName} in your locker/lobby.");
	}

	public RetracResult Replace(IReadOnlyList<RetracSwap> all, string successText = "Swaps re-applied.")
	{
		if (all.Count != 0)
		{
			return ApplySet(all.ToList(), successText);
		}
		return Undo();
	}

	public RetracResult Remove(string ownedId)
	{
		List<RetracSwap> all = LoadState().Swaps.Where((RetracSwap s) => !s.OwnedId.Equals(ownedId, StringComparison.OrdinalIgnoreCase)).ToList();
		return Replace(all, "Reverted. Your other swaps are still on.");
	}

	public RetracResult Check(IReadOnlyList<RetracSwap> all)
	{
		using (Enter())
		{
			try
			{
				if (!Directory.Exists(_paks))
				{
					return new RetracResult(Ok: false, "The Retrac folder isn't there.");
				}
				DropStaleBackups(LoadState());
				List<string> item = Plan(RetracIndex.Build(_paks), all.ToList(), HatIntoHead).Notes;
				return new RetracResult(Ok: true, (item.Count > 0) ? string.Join("\n", item) : "OK");
			}
			catch (RetracPlanException ex)
			{
				return new RetracResult(Ok: false, "Can't swap that pair.\n\n" + ex.Message);
			}
			catch (Exception ex2)
			{
				return new RetracResult(Ok: false, "Couldn't check that pair. " + ex2.Message);
			}
		}
	}

	private RetracResult ApplySet(List<RetracSwap> all, string successText)
	{
		using (Enter())
		{
			try
			{
				if (!Directory.Exists(_paks))
				{
					return new RetracResult(Ok: false, "The Retrac folder isn't there.");
				}
				State state = LoadState();
				DropStaleBackups(state);
				RetracIndex index = RetracIndex.Build(_paks);
				(List<PlannedWrite> Writes, List<string> Notes) tuple = Plan(index, all, HatIntoHead);
				List<PlannedWrite> item = tuple.Writes;
				List<string> item2 = tuple.Notes;
				RetracResult retracResult = UndoCore(state);
				if (!retracResult.Ok)
				{
					return new RetracResult(Ok: false, retracResult.Message + "\n\nNothing new was applied.");
				}
				Execute(item, all, index);
				item2.Insert(0, $"{item.Count} chunk{((item.Count == 1) ? "" : "s")} written (skin, hero parts, container headers).");
				string message = successText + ((all.Count > 1) ? $"\n{all.Count} swaps are active." : "") + ((item2.Count > 0) ? ("\n\n" + string.Join("\n", item2)) : "");
				return new RetracResult(Ok: true, message);
			}
			catch (RetracPlanException ex)
			{
				return new RetracResult(Ok: false, "Didn't swap. Nothing was changed.\n\n" + ex.Message);
			}
			catch (IOException ex2) when (IsLocked(ex2))
			{
				return new RetracResult(Ok: false, "Didn't swap: a file is in use. Close Retrac and Fortnite completely, then try again.");
			}
			catch (UnauthorizedAccessException)
			{
				return new RetracResult(Ok: false, "Didn't swap: Windows won't let me write to that folder. Close Retrac and Fortnite, or run Drip Swapper as admin.");
			}
			catch (Exception ex4)
			{
				return new RetracResult(Ok: false, "Didn't swap. " + ex4.Message + "\n\nIf this keeps happening, press Revert all, then send the Retrac scan report to xvoid.");
			}
		}
	}

	public HashSet<string> Available(IEnumerable<(string Id, string Type)> items, out int containers, out int locked)
	{
		RetracIndex retracIndex = RetracIndex.Build(_paks);
		containers = retracIndex.Count;
		locked = retracIndex.Locked;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (text, type) in items)
		{
			if (retracIndex.Has(text, type))
			{
				hashSet.Add(text);
			}
		}
		return hashSet;
	}

	private static (List<PlannedWrite> Writes, List<string> Notes) Plan(RetracIndex index, List<RetracSwap> swaps, bool hatIntoHead = true)
	{
		List<string> list = new List<string>();
		List<PlannedWrite> writes = new List<PlannedWrite>();
		Dictionary<IoContainer, Dictionary<ulong, StoreEntry>> headerEdits = new Dictionary<IoContainer, Dictionary<ulong, StoreEntry>>();
		Dictionary<IoContainer, Ue426ContainerHeader> headerCache = new Dictionary<IoContainer, Ue426ContainerHeader>();
		HashSet<ulong> planned = new HashSet<ulong>();
		foreach (RetracSwap swap in swaps)
		{
			var (ownedPath, num, list2) = index.Resolve(swap.OwnedId, swap.OwnedType, swap.OwnedName, "the one you own");
			var (wantedPath, num2, list3) = index.Resolve(swap.WantedId, swap.WantedType, swap.WantedName, "the one you want");
			if (num == num2)
			{
				list.Add("Rewrite-only test on " + swap.OwnedName + ": the file is written back with identical content.");
			}
			AddPair(swap.OwnedName, swap.WantedName, ownedPath, num, list2, wantedPath, num2, list3);
			if (num == num2)
			{
				continue;
			}
			try
			{
				IEnumerable<(string, string, string, string)> enumerable;
				if (swap.OwnedType.Equals("outfit", StringComparison.OrdinalIgnoreCase) && swap.WantedType.Equals("outfit", StringComparison.OrdinalIgnoreCase))
				{
					enumerable = index.PartPairs(list2[0], list3[0], hatIntoHead, swap.StyleTag, swap.StyleName, list);
					goto IL_0207;
				}
				if (swap.OwnedType.Equals("pickaxe", StringComparison.OrdinalIgnoreCase) && swap.WantedType.Equals("pickaxe", StringComparison.OrdinalIgnoreCase))
				{
					enumerable = index.SoftPairs(list2[0], list3[0], "/Game/Athena/Items/Weapons/WID_", "pickaxe weapon");
					goto IL_0207;
				}
				if (swap.OwnedType.Equals("backpack", StringComparison.OrdinalIgnoreCase) && swap.WantedType.Equals("backpack", StringComparison.OrdinalIgnoreCase))
				{
					enumerable = index.SoftPairs(list2[0], list3[0], "/Game/Characters/CharacterParts/Backpacks/", "back bling part");
					goto IL_0207;
				}
				continue;
				IL_0207:
				if (!IsDefaultStyle(swap.StyleTag, swap.StyleName) && !swap.OwnedType.Equals("outfit", StringComparison.OrdinalIgnoreCase))
				{
					list.Add("Styles only work on outfits right now, so this uses the default style.");
				}
				foreach (var item5 in enumerable)
				{
					string item = item5.Item1;
					string item2 = item5.Item2;
					string item3 = item5.Item3;
					string item4 = item5.Item4;
					ulong num3 = PackageNames.ChunkId(item2);
					ulong num4 = PackageNames.ChunkId(item4);
					if (num3 == num4 || planned.Contains(num3))
					{
						continue;
					}
					if (!index.TryFind(item2, out List<Loc> locs) || !index.TryFind(item4, out List<Loc> locs2))
					{
						list.Add("Skipped " + item3 + ": not in the files Drip can open.");
						continue;
					}
					try
					{
						AddPair(item, item3, item2, num3, locs, item4, num4, locs2);
					}
					catch (Exception ex) when ((ex is RetracPlanException || ex is InvalidDataException) ? true : false)
					{
						list.Add($"Skipped {item3} -> {item}: {ex.Message}");
					}
				}
			}
			catch (Exception ex2) when ((ex2 is InvalidDataException || ex2 is InvalidOperationException) ? true : false)
			{
				list.Add("Couldn't work out the extra parts: " + ex2.Message);
			}
		}
		foreach (var (ioContainer2, replacements) in headerEdits)
		{
			byte[] array;
			try
			{
				array = headerCache[ioContainer2].WithEntries(replacements);
			}
			catch (InvalidOperationException ex3)
			{
				throw new RetracPlanException(ex3.Message);
			}
			int value = index.HeaderChunk(ioContainer2).Value;
			if (IoContainer.Align16((uint)array.Length) > ioContainer2.Capacity(value))
			{
				throw new RetracPlanException(ioContainer2.Name + "'s container header has no room left.");
			}
			writes.Add(new PlannedWrite(ioContainer2, value, array));
		}
		return (Writes: writes, Notes: list);
		void AddPair(string ownedName, string wantedName, string text, ulong ownedId, List<Loc> ownedLocs, string wantedPackage, ulong wantedId, List<Loc> wantedLocs)
		{
			Ue426Package owned = Ue426Package.Parse(ownedLocs[0].Container.ReadChunk(ownedLocs[0].Chunk));
			Ue426Package ue426Package = Ue426Package.Parse(wantedLocs[0].Container.ReadChunk(wantedLocs[0].Chunk));
			byte[] array2;
			try
			{
				array2 = Ue426Package.CloneAs(ue426Package, wantedPackage, owned, text);
			}
			catch (InvalidDataException ex4)
			{
				throw new RetracPlanException($"{wantedName} can't be swapped onto {ownedName}: {ex4.Message}");
			}
			Ue426Package ue426Package2 = Ue426Package.Parse(array2);
			if (ue426Package2.PackageName != text || !ue426Package2.HashesMatchNames() || ue426Package2.ExportCount != ue426Package.ExportCount)
			{
				throw new RetracPlanException("Internal check failed while building " + wantedName + ". Nothing was written.");
			}
			StoreEntry storeEntry = HeaderOf(wantedLocs[0].Container).Get(wantedId) ?? throw new RetracPlanException(wantedName + " isn't listed in its container header, so it can't be loaded safely.");
			List<(Loc, StoreEntry)> list4 = new List<(Loc, StoreEntry)>();
			foreach (Loc ownedLoc in ownedLocs)
			{
				long num5 = ownedLoc.Container.Capacity(ownedLoc.Chunk);
				if (IoContainer.Align16((uint)array2.Length) > num5)
				{
					throw new RetracPlanException($"{wantedName} is too big for {ownedName}'s slot ({array2.Length:N0} bytes, room for {num5:N0}). Pick a different pair.");
				}
				StoreEntry storeEntry2 = HeaderOf(ownedLoc.Container).Get(ownedId) ?? throw new RetracPlanException(ownedName + " isn't listed in " + ownedLoc.Container.Name + "'s container header.");
				list4.Add((ownedLoc, new StoreEntry((ulong)array2.Length, storeEntry.ExportCount, storeEntry.ExportBundleCount, storeEntry2.LoadOrder, storeEntry2.Pad, storeEntry.ImportedPackages)));
			}
			foreach (var (loc, value2) in list4)
			{
				if (!headerEdits.TryGetValue(loc.Container, out Dictionary<ulong, StoreEntry> value3))
				{
					value3 = (headerEdits[loc.Container] = new Dictionary<ulong, StoreEntry>());
				}
				value3[ownedId] = value2;
				writes.Add(new PlannedWrite(loc.Container, loc.Chunk, array2));
			}
			planned.Add(ownedId);
		}
		Ue426ContainerHeader HeaderOf(IoContainer c)
		{
			if (headerCache.TryGetValue(c, out Ue426ContainerHeader value2))
			{
				return value2;
			}
			int index2 = index.HeaderChunk(c) ?? throw new RetracPlanException(c.Name + " has no container header, so I can't swap inside it.");
			return headerCache[c] = Ue426ContainerHeader.Parse(c.ReadChunk(index2));
		}
	}

	private void Execute(List<PlannedWrite> writes, List<RetracSwap> swaps, RetracIndex index)
	{
		Dictionary<string, TouchedContainer> dictionary = new Dictionary<string, TouchedContainer>();
		RetracSignatures signatures = Signatures;
		try
		{
			foreach (PlannedWrite write in writes)
			{
				IoContainer container = write.Container;
				if (!dictionary.TryGetValue(container.Name, out var value))
				{
					string lastUcasPath = container.LastUcasPath;
					long length = new FileInfo(lastUcasPath).Length;
					string name = container.Name;
					TouchedContainer touchedContainer = new TouchedContainer
					{
						Name = container.Name,
						UcasFile = Path.GetFileName(lastUcasPath),
						UcasLengthBefore = length,
						UcasLengthAfter = length,
						UcasMtimeBeforeUtc = File.GetLastWriteTimeUtc(lastUcasPath),
						UtocMtimeBeforeUtc = File.GetLastWriteTimeUtc(container.LiveUtocPath)
					};
					value = touchedContainer;
					dictionary[name] = touchedContainer;
				}
				container.EnsureBackup();
				PatchRecord patchRecord = container.PatchChunk(write.Chunk, write.Data, exactLength: true);
				if (signatures == RetracSignatures.Update)
				{
					UpdateBlockHashes(container, patchRecord.BlockIndex, write.Data);
				}
				value.UcasLengthAfter = patchRecord.UcasLengthAfter;
			}
			if (signatures == RetracSignatures.Strip)
			{
				foreach (IoContainer item in writes.Select((PlannedWrite w) => w.Container).Distinct())
				{
					StripSignatures(item);
				}
			}
			foreach (TouchedContainer value2 in dictionary.Values)
			{
				value2.UtocSha1After = Sha1Of(Path.Combine(_paks, value2.Name + ".utoc"));
			}
			State s = new State
			{
				Paks = _paks,
				Swaps = swaps,
				Containers = dictionary.Values.ToList()
			};
			SaveState(s);
			foreach (PlannedWrite write2 in writes)
			{
				if (!((ReadOnlySpan<byte>)new IoContainer(_paks, write2.Container.Name, (string _) => (byte[]?)null, preferBackup: false).ReadChunk(write2.Chunk).AsSpan()).SequenceEqual((ReadOnlySpan<byte>)write2.Data))
				{
					throw new InvalidOperationException($"Read-back of {write2.Container.Name} chunk {write2.Chunk} didn't match.");
				}
			}
		}
		catch
		{
			try
			{
				UndoCore(new State
				{
					Paks = _paks,
					Containers = dictionary.Values.ToList()
				}, force: true);
			}
			catch
			{
			}
			try
			{
				if (File.Exists(_stateFile))
				{
					File.Delete(_stateFile);
				}
			}
			catch
			{
			}
			throw;
		}
	}

	private static void StripSignatures(IoContainer c)
	{
		TocFile toc = c.Toc;
		if (!toc.Signed)
		{
			return;
		}
		byte[] array = File.ReadAllBytes(c.LiveUtocPath);
		long signatureSectionPos = toc.SignatureSectionPos;
		long num = BinaryPrimitives.ReadUInt32LittleEndian(array.AsSpan((int)signatureSectionPos));
		if ((num <= 0 || num > 4096) ? true : false)
		{
			throw new InvalidOperationException($"{c.Name}: unexpected signature size {num}.");
		}
		long num2 = 4 + 2 * num + 20L * (long)toc.BlockCount;
		if (signatureSectionPos + num2 > array.Length)
		{
			throw new InvalidOperationException(c.Name + ": signature section runs past the end of the toc.");
		}
		byte[] array2 = new byte[array.Length - num2];
		Array.Copy(array, 0L, array2, 0L, signatureSectionPos);
		Array.Copy(array, signatureSectionPos + num2, array2, signatureSectionPos, array.Length - signatureSectionPos - num2);
		array2[80] &= 251;
		using FileStream fileStream = new FileStream(c.LiveUtocPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		fileStream.SetLength(0L);
		fileStream.Write(array2);
		fileStream.Flush(flushToDisk: true);
	}

	private static void UpdateBlockHashes(IoContainer c, int firstBlock, byte[] data)
	{
		TocFile toc = c.Toc;
		if (!toc.Signed)
		{
			return;
		}
		using FileStream fileStream = new FileStream(c.LiveUtocPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
		byte[] array = new byte[4];
		fileStream.Position = toc.SignatureSectionPos;
		fileStream.ReadExactly(array);
		long num = BinaryPrimitives.ReadUInt32LittleEndian(array);
		if ((num > 0 && num <= 4096) || 1 == 0)
		{
			long num2 = toc.SignatureSectionPos + 4 + 2 * num;
			int blockSize = (int)toc.BlockSize;
			byte[] array2 = new byte[IoContainer.Align16((uint)data.Length)];
			data.CopyTo(array2, 0);
			int num3 = (array2.Length + blockSize - 1) / blockSize;
			for (int i = 0; i < num3; i++)
			{
				int length = Math.Min(blockSize, array2.Length - i * blockSize);
				fileStream.Position = num2 + 20L * (long)(firstBlock + i);
				fileStream.Write(SHA1.HashData(array2.AsSpan(i * blockSize, length)));
			}
			fileStream.Flush(flushToDisk: true);
		}
	}

	public RetracResult Undo()
	{
		using (Enter())
		{
			try
			{
				State state = LoadState();
				if (state.Swaps.Count == 0 && state.Containers.Count == 0)
				{
					return new RetracResult(Ok: true, "Nothing to undo. No swap has been applied.");
				}
				return UndoCore(state);
			}
			catch (IOException ex) when (IsLocked(ex))
			{
				return new RetracResult(Ok: false, "Couldn't undo: a file is in use. Close Retrac and Fortnite completely, then press Undo again.");
			}
			catch (Exception ex2)
			{
				return new RetracResult(Ok: false, "Couldn't undo: " + ex2.Message);
			}
		}
	}

	private RetracResult UndoCore(State state, bool force = false)
	{
		int num = 0;
		int num2 = 0;
		foreach (TouchedContainer container in state.Containers)
		{
			string text = Path.Combine(_paks, container.Name + ".utoc");
			string text2 = text + ".elenr-bak";
			if (!File.Exists(text2))
			{
				continue;
			}
			if (!File.Exists(text))
			{
				File.Delete(text2);
				continue;
			}
			if (!force && container.UtocSha1After.Length > 0 && Sha1Of(text) != container.UtocSha1After)
			{
				File.Delete(text2);
				num2++;
				continue;
			}
			File.Copy(text2, text, overwrite: true);
			File.Delete(text2);
			if (container.UtocMtimeBeforeUtc.Year > 2000)
			{
				try
				{
					File.SetLastWriteTimeUtc(text, container.UtocMtimeBeforeUtc);
				}
				catch
				{
				}
			}
			string text3 = Path.Combine(_paks, container.UcasFile);
			try
			{
				if (container.UcasLengthBefore > 0 && File.Exists(text3) && new FileInfo(text3).Length == container.UcasLengthAfter)
				{
					using FileStream fileStream = new FileStream(text3, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
					fileStream.SetLength(container.UcasLengthBefore);
				}
				if (container.UcasMtimeBeforeUtc.Year > 2000 && File.Exists(text3))
				{
					File.SetLastWriteTimeUtc(text3, container.UcasMtimeBeforeUtc);
				}
			}
			catch
			{
			}
			num++;
		}
		if (!force)
		{
			try
			{
				if (File.Exists(_stateFile))
				{
					File.Delete(_stateFile);
				}
			}
			catch
			{
			}
		}
		return new RetracResult(Ok: true, (num == 0 && num2 == 0) ? "Nothing was changed on disk, so there was nothing to undo." : ($"Undone. {num} file{((num == 1) ? "" : "s")} put back to how Retrac shipped them." + ((num2 > 0) ? $"\n{num2} file{((num2 == 1) ? " was" : "s were")} already replaced by a Retrac update, so those were left alone." : "")));
	}

	private void DropStaleBackups(State state)
	{
		if (state.Containers.Count == 0)
		{
			try
			{
				foreach (string item in Directory.EnumerateFiles(_paks, "*.elenr-bak"))
				{
					File.Delete(item);
				}
				return;
			}
			catch
			{
				return;
			}
		}
		foreach (TouchedContainer container in state.Containers)
		{
			string text = Path.Combine(_paks, container.Name + ".utoc");
			string path = text + ".elenr-bak";
			if (File.Exists(path) && File.Exists(text) && container.UtocSha1After.Length > 0 && Sha1Of(text) != container.UtocSha1After)
			{
				File.Delete(path);
				state.Containers.Remove(container);
				state.Swaps.Clear();
				SaveState(state);
				break;
			}
		}
	}

	private State LoadState()
	{
		try
		{
			if (File.Exists(_stateFile))
			{
				State state = JsonSerializer.Deserialize<State>(File.ReadAllText(_stateFile));
				if (state != null && string.Equals(Path.GetFullPath(state.Paks), Path.GetFullPath(_paks), StringComparison.OrdinalIgnoreCase))
				{
					return state;
				}
			}
		}
		catch
		{
		}
		return new State
		{
			Paks = _paks
		};
	}

	private void SaveState(State s)
	{
		File.WriteAllText(_stateFile, JsonSerializer.Serialize(s, Json));
	}

	private static string Sha1Of(string path)
	{
		using FileStream source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		return Convert.ToHexString(SHA1.HashData(source));
	}

	private static bool IsLocked(IOException ex)
	{
		int num = ex.HResult & 0xFFFF;
		if ((uint)(num - 32) <= 1u)
		{
			return true;
		}
		return false;
	}
}
