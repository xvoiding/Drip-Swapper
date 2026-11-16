using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Drip.Core.Keys;
using Drip.Core.Swapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Drip.Core.Cosmetics;

public sealed class PartResolver : IDisposable
{
	private static readonly Regex PathString = new Regex("\"((?:/[A-Za-z0-9_.\\-]+)+)\"", RegexOptions.Compiled);

	private static readonly Regex SlotType = new Regex("\"CharacterPartType\"\\s*:\\s*\"[^\"]*?([A-Za-z]+)\"", RegexOptions.Compiled);

	private readonly string _paks;

	private readonly AesKeys _keys;

	private readonly Action<string> _log;

	private DefaultFileProvider? _provider;

	private static readonly Regex AnyPackagePath = new Regex("\"(/[A-Za-z0-9_]+(?:/[A-Za-z0-9_.\\-]+)+)\"", RegexOptions.Compiled);

	private Lazy<Dictionary<string, List<string>>>? _byLeaf;

	public bool Quiet { get; set; }

	public bool IsMounted => _provider != null;

	private Lazy<Dictionary<string, List<string>>> ByLeaf => _byLeaf ?? (_byLeaf = new Lazy<Dictionary<string, List<string>>>(BuildLeafIndex));

	private void L(string s)
	{
		if (!Quiet)
		{
			_log(s);
		}
	}

	public PartResolver(string paksDirectory, AesKeys keys, Action<string>? log = null)
	{
		_paks = paksDirectory;
		_keys = keys;
		_log = log ?? ((Action<string>)((string _) =>
		{
		}));
	}

	public async Task MountAsync()
	{
		string usmap = null;
		try
		{
			(string, string) tuple = await MappingsFetcher.EnsureAsync(_log).ConfigureAwait(continueOnCapturedContext: false);
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			usmap = item;
			if (!string.IsNullOrEmpty(_keys.Build) && !string.Equals(item2, _keys.Build, StringComparison.OrdinalIgnoreCase))
			{
				L($"  warning: mappings are for {item2} but the game is {_keys.Build}; property reads may fail.");
			}
			else
			{
				L("Mappings ready (" + item2 + ").");
			}
		}
		catch (Exception ex)
		{
			L("  could not get mappings (" + ex.Message + "). Property reads will fail without them.");
		}
		await Task.Run(() =>
		{
			DefaultFileProvider defaultFileProvider = new DefaultFileProvider(_paks, SearchOption.TopDirectoryOnly, new VersionContainer(EGame.GAME_UE5_9), StringComparer.OrdinalIgnoreCase);
			defaultFileProvider.Initialize();
			if (usmap != null)
			{
				defaultFileProvider.MappingsContainer = new FileUsmapTypeMappingsProvider(usmap);
			}
			foreach (var (hexString, keyString) in _keys.AllKeys())
			{
				defaultFileProvider.SubmitKey(new FGuid(hexString), new FAesKey(keyString));
			}
			_provider = defaultFileProvider;
			_byLeaf = null;
			L($"CUE4Parse mounted {defaultFileProvider.Files.Count} files.");
			Diagnose(defaultFileProvider);
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void Diagnose(DefaultFileProvider provider)
	{
		try
		{
			int num = 0;
			int num2 = 0;
			List<string> list = new List<string>();
			foreach (string key in provider.Files.Keys)
			{
				if (key.Contains("/CP_", StringComparison.OrdinalIgnoreCase) && key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
				{
					num++;
				}
				if (key.Contains("gamefeatures/brcosmetics", StringComparison.OrdinalIgnoreCase))
				{
					num2++;
				}
				if (list.Count < 6 && key.Contains("cyclone", StringComparison.OrdinalIgnoreCase) && key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(key);
				}
			}
			L($"  readable: {num} character-part (CP_) files, {num2} files under GameFeatures/BRCosmetics.");
			foreach (string item in list)
			{
				L("  sample: " + item);
			}
			int num3 = 0;
			foreach (string key2 in provider.Files.Keys)
			{
				if (num3 >= 8)
				{
					break;
				}
				if (key2.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
				{
					int num4 = key2.LastIndexOf('/');
					string text = key2.Substring(num4 + 1);
					if (text.StartsWith("CP_", StringComparison.OrdinalIgnoreCase) && (text.Contains("Cyclone", StringComparison.OrdinalIgnoreCase) || text.Contains("KelpLinen", StringComparison.OrdinalIgnoreCase)))
					{
						L("  CP file present: " + key2);
						num3++;
					}
				}
			}
			if (num3 == 0)
			{
				L("  no CP_ file with 'Cyclone' or 'KelpLinen' in its name is readable.");
			}
		}
		catch (Exception ex)
		{
			L("  diagnose failed: " + ex.Message);
		}
	}

	public CharacterPart? RootPart(string cosmeticId)
	{
		EnsureMounted();
		string text = FindAssetPath(cosmeticId);
		if (text == null)
		{
			return null;
		}
		string package = PackageNames.Normalize(text);
		return new CharacterPart(package, "Definition", RealChunkId(package));
	}

	public ulong RootChunkId(string cosmeticId)
	{
		EnsureMounted();
		string text = FindAssetPath(cosmeticId);
		if (text != null)
		{
			return RealChunkId(PackageNames.Normalize(text));
		}
		return 0uL;
	}

	public HashSet<string> LocalCosmeticNames()
	{
		EnsureMounted();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string key in _provider.Files.Keys)
		{
			if (key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && key.IndexOf("/Items/Cosmetics/", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				int num = key.LastIndexOf('/');
				hashSet.Add(key.Substring(num + 1, key.Length - num - 1 - ".uasset".Length));
			}
		}
		return hashSet;
	}

	public string? FindAssetPath(string cosmeticId)
	{
		EnsureMounted();
		string tail = "/" + cosmeticId + ".uasset";
		return _provider.Files.Keys.FirstOrDefault((string k) => k.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
	}

	public string DumpJson(string assetPath)
	{
		EnsureMounted();
		return JsonConvert.SerializeObject(_provider.LoadPackageObjects(assetPath), Formatting.Indented);
	}

	public List<CharacterPart> Resolve(string cosmeticId)
	{
		EnsureMounted();
		string text = FindAssetPath(cosmeticId) ?? throw new FileNotFoundException(cosmeticId + " isn't in your local game files (it is streamed or in a locked container), so it can't be swapped yet.");
		string text2 = PackageNames.Normalize(text);
		ulong num = RealChunkId(text2);
		ulong num2 = PackageNames.ChunkId(text2);
		L($"  {cosmeticId}: {text} · chunk {num:X16} " + ((num == num2) ? "(name hash OK)" : $"(name-hash guess was {num2:X16} - hashing differs!)"));
		Dictionary<string, CharacterPart> dictionary = new Dictionary<string, CharacterPart>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { text };
		Queue<(string, int)> queue = new Queue<(string, int)>();
		queue.Enqueue((text, 0));
		while (queue.Count > 0)
		{
			var (text3, num3) = queue.Dequeue();
			string input;
			try
			{
				input = DumpJson(text3);
			}
			catch (Exception ex)
			{
				L($"  could not read {text3}: {ex.GetType().Name}: {ex.Message}");
				continue;
			}
			foreach (Match item in PathString.Matches(input))
			{
				string value = item.Groups[1].Value;
				string text4 = LeafOf(value);
				if (text4.StartsWith("CP_", StringComparison.OrdinalIgnoreCase))
				{
					string text5 = PackageNames.Normalize(value);
					if (!dictionary.ContainsKey(text5))
					{
						string text6 = FindByPackage(text5);
						ulong num4 = RealChunkId(text5);
						ulong num5 = PackageNames.ChunkId(text5);
						if (text6 == null)
						{
							L($"  {LeafOf(text5)}: not in any readable container (it may sit in an encrypted container with no key). hash guess {num5:X16}");
						}
						else
						{
							L($"  {LeafOf(text5)}: {text6} · chunk {num4:X16}{((num4 == num5) ? " (matches name hash)" : $" (name-hash guess was {num5:X16})")}");
						}
						dictionary[text5] = new CharacterPart(text5, SlotOf(text5), num4);
					}
				}
				else if (num3 < 3 && IsFollowable(text4))
				{
					string text7 = FindByPackage(PackageNames.Normalize(value));
					if (text7 != null && hashSet.Add(text7))
					{
						queue.Enqueue((text7, num3 + 1));
					}
				}
			}
		}
		return dictionary.Values.ToList();
	}

	public (List<StyleRename> Renames, string Note) ResolveStyle(string cosmeticId, string styleTag)
	{
		EnsureMounted();
		string text = FindAssetPath(cosmeticId) ?? throw new FileNotFoundException(cosmeticId + " isn't in your local game files.");
		List<string> list = new List<string>();
		List<string> list2 = null;
		int num = int.MaxValue;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { text };
		Queue<(string, int)> queue = new Queue<(string, int)>();
		queue.Enqueue((text, 0));
		while (queue.Count > 0)
		{
			var (assetPath, num2) = queue.Dequeue();
			string text2;
			try
			{
				text2 = DumpJson(assetPath);
			}
			catch
			{
				continue;
			}
			JToken jToken;
			try
			{
				jToken = JToken.Parse(text2);
			}
			catch
			{
				continue;
			}
			IEnumerable<JObject> enumerable2;
			if (!(jToken is JArray source))
			{
				IEnumerable<JObject> enumerable = new JObject[1] { (JObject)jToken };
				enumerable2 = enumerable;
			}
			else
			{
				enumerable2 = source.OfType<JObject>();
			}
			foreach (JObject item in enumerable2)
			{
				if (!(item["Type"]?.ToString() ?? "").Contains("Variant", StringComparison.OrdinalIgnoreCase))
				{
					CollectParts(item, list, skipVariantProps: true);
				}
			}
			foreach (JObject item2 in Walk(jToken).OfType<JObject>())
			{
				List<string> list3 = (from v in item2.DescendantsAndSelf().OfType<JValue>()
					where v.Type == JTokenType.String
					select (string)v.Value).ToList();
				if (list3.Count < num && list3.Any((string v) => TagMatches(v, styleTag)))
				{
					List<string> list4 = new List<string>();
					CollectParts(item2, list4, skipVariantProps: false);
					if (list4.Count != 0)
					{
						list2 = list4;
						num = list3.Count;
					}
				}
			}
			foreach (Match item3 in PathString.Matches(text2))
			{
				string value = item3.Groups[1].Value;
				if (num2 < 3 && IsFollowable(LeafOf(value)))
				{
					string text3 = FindByPackage(PackageNames.Normalize(value));
					if (text3 != null && hashSet.Add(text3))
					{
						queue.Enqueue((text3, num2 + 1));
					}
				}
			}
		}
		list = list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (list2 == null)
		{
			return (Renames: new List<StyleRename>(), Note: "style '" + styleTag + "': no character-part override found in the definition (it may change materials, not parts)");
		}
		List<StyleRename> list5 = StyleTweaks.Pair(list, list2.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
		return (Renames: list5, Note: (list5.Count == 0) ? ("style '" + styleTag + "': its parts match the default ones, nothing to retarget") : $"style '{styleTag}': retargeting {list5.Count} part(s) ({string.Join(", ", list5.Select((StyleRename r) => LeafOf(r.OldPackage) + " -> " + LeafOf(r.NewPackage)))})");
	}

	public (List<StyleRename> Pairs, string Note) ResolveStyleMaterials(string cosmeticId, string styleTag)
	{
		EnsureMounted();
		string assetPath = FindAssetPath(cosmeticId) ?? throw new FileNotFoundException(cosmeticId + " isn't in your local game files.");
		return StyleMaterialsFromJson(DumpJson(assetPath), styleTag);
	}

	public static (List<StyleRename> Pairs, string Note) StyleMaterialsFromJson(string json, string styleTag)
	{
		JToken t = JToken.Parse(json);
		List<StyleRename> list = new List<StyleRename>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (JObject item in Walk(t).OfType<JObject>())
		{
			string text = item["CustomizationVariantTag"]?["TagName"]?.ToString();
			if (text == null || !TagMatches(text, styleTag) || !(item["VariantMaterials"] is JArray source))
			{
				continue;
			}
			foreach (JObject item2 in source.OfType<JObject>())
			{
				string text2 = item2["MaterialToSwap"]?["AssetPathName"]?.ToString();
				string text3 = item2["OverrideMaterial"]?["AssetPathName"]?.ToString();
				if (!string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3))
				{
					string text4 = PackageNames.Normalize(text2);
					string text5 = PackageNames.Normalize(text3);
					if (!string.Equals(text4, text5, StringComparison.OrdinalIgnoreCase) && hashSet.Add(text4))
					{
						list.Add(new StyleRename(text4, text5));
					}
				}
			}
		}
		return (Pairs: list, Note: (list.Count == 0) ? ("style '" + styleTag + "': no material overrides in the definition") : $"style '{styleTag}': {list.Count} material(s) to swap ({string.Join(", ", list.Select((StyleRename r) => LeafOf(r.OldPackage) + " -> " + LeafOf(r.NewPackage)))})");
	}

	private static IEnumerable<JToken> Walk(JToken t)
	{
		if (!(t is JContainer jContainer))
		{
			return new JToken[1] { t };
		}
		return jContainer.DescendantsAndSelf();
	}

	private static bool TagMatches(string value, string tag)
	{
		if (!value.Equals(tag, StringComparison.OrdinalIgnoreCase))
		{
			return value.EndsWith("." + tag, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static void CollectParts(JToken t, List<string> into, bool skipVariantProps)
	{
		if (t is JValue jValue)
		{
			if (jValue.Type == JTokenType.String)
			{
				string text = (string)jValue.Value;
				if (text.StartsWith('/') && text.IndexOf(' ') < 0 && LeafOf(text).StartsWith("CP_", StringComparison.OrdinalIgnoreCase))
				{
					into.Add(PackageNames.Normalize(text));
				}
			}
		}
		else
		{
			if (!(t is JProperty jProperty))
			{
				if (!(t is JContainer jContainer))
				{
					return;
				}
				{
					foreach (JToken item in jContainer.Children())
					{
						CollectParts(item, into, skipVariantProps);
					}
					return;
				}
			}
			if (!skipVariantProps || !jProperty.Name.Contains("Variant", StringComparison.OrdinalIgnoreCase))
			{
				CollectParts(jProperty.Value, into, skipVariantProps);
			}
		}
	}

	public static List<PartPair> Pair(IEnumerable<CharacterPart> source, IEnumerable<CharacterPart> target, out List<string> unpaired)
	{
		unpaired = new List<string>();
		List<PartPair> list = new List<PartPair>();
		Dictionary<string, CharacterPart> dictionary = (from p in target
			group p by p.Slot).ToDictionary((IGrouping<string, CharacterPart> g) => g.Key, (IGrouping<string, CharacterPart> g) => g.First());
		foreach (CharacterPart item in source)
		{
			if (dictionary.TryGetValue(item.Slot, out var value))
			{
				list.Add(new PartPair(item.Slot, item.Package, value.Package, item.ChunkId, value.ChunkId));
				continue;
			}
			unpaired.Add($"{item.Slot}: {item.Package} (target has no {item.Slot})");
		}
		foreach (CharacterPart t in target)
		{
			if (!source.Any((CharacterPart s) => s.Slot == t.Slot))
			{
				unpaired.Add($"{t.Slot}: {t.Package} (source has no {t.Slot})");
			}
		}
		return list;
	}

	private string SlotOf(string package)
	{
		try
		{
			string text = FindByPackage(package);
			if (text != null)
			{
				Match match = SlotType.Match(DumpJson(text));
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}
		}
		catch
		{
		}
		string text2 = LeafOf(package);
		string[] array = new string[8] { "Body", "Head", "Hat", "Face", "Backpack", "Charm", "Tail", "Gunspec" };
		foreach (string text3 in array)
		{
			if (text2.Contains("_" + text3 + "_", StringComparison.OrdinalIgnoreCase) || text2.EndsWith("_" + text3, StringComparison.OrdinalIgnoreCase))
			{
				return text3;
			}
		}
		return "Unknown";
	}

	public string? PackageFile(string package)
	{
		EnsureMounted();
		return FindByPackage(package);
	}

	public (int Lod0Sections, List<string> Materials) MeshSections(string package)
	{
		EnsureMounted();
		string text = FindByPackage(package) ?? throw new FileNotFoundException(package);
		USkeletalMesh uSkeletalMesh = _provider.LoadPackageObject<USkeletalMesh>(text.Substring(0, text.Length - ".uasset".Length) + "." + LeafOf(package));
		FStaticLODModel[] lODModels = uSkeletalMesh.LODModels;
		int item = ((lODModels != null && lODModels.Length > 0) ? (lODModels[0].Sections?.Length ?? 0) : 0);
		List<string> list = new List<string>();
		FSkeletalMaterial[] skeletalMaterials = uSkeletalMesh.SkeletalMaterials;
		if (skeletalMaterials != null)
		{
			FSkeletalMaterial[] array = skeletalMaterials;
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[i].MaterialInterface?.ResolvedObject?.GetPathName();
				if (text2 != null && text2.Length > 0)
				{
					list.Add(PackageNames.Normalize(text2));
				}
			}
		}
		return (Lod0Sections: item, Materials: list);
	}

	public bool HasLocalPackage(string package)
	{
		EnsureMounted();
		return FindByPackage(package) != null;
	}

	public string? LocalContainerName(string package)
	{
		EnsureMounted();
		try
		{
			string text = FindByPackage(package);
			if (text != null && _provider.Files.TryGetValue(text, out GameFile value) && value is FIoStoreEntry { IoStoreReader: var ioStoreReader })
			{
				string text2 = ioStoreReader?.Path;
				return string.IsNullOrEmpty(text2) ? null : Path.GetFileNameWithoutExtension(text2);
			}
		}
		catch
		{
		}
		return null;
	}

	public ulong LocalChunkId(string package)
	{
		EnsureMounted();
		return RealChunkId(package);
	}

	private ulong RealChunkId(string package)
	{
		try
		{
			string text = FindByPackage(package);
			if (text != null && _provider.Files.TryGetValue(text, out GameFile value) && value is FIoStoreEntry fIoStoreEntry)
			{
				return fIoStoreEntry.ChunkId.ChunkId;
			}
		}
		catch
		{
		}
		return 0uL;
	}

	private string? FindByPackage(string package)
	{
		string text = package.TrimStart('/');
		int num = text.IndexOf('/');
		if (num < 0)
		{
			return null;
		}
		string text2 = text.Substring(0, num);
		string text3 = text.Substring(num + 1);
		string text4 = (text2.Equals("Game", StringComparison.OrdinalIgnoreCase) ? Look("fortnitegame/content/" + text3 + ".uasset") : (Look($"fortnitegame/plugins/gamefeatures/{text2}/content/{text3}.uasset") ?? Look($"fortnitegame/plugins/{text2}/content/{text3}.uasset")));
		if (text4 != null)
		{
			return text4;
		}
		string key = LeafOf(package);
		if (!ByLeaf.Value.TryGetValue(key, out List<string> value))
		{
			return null;
		}
		string tail = "/" + text3 + ".uasset";
		string text5 = value.FirstOrDefault((string k) => k.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
		if (text5 == null)
		{
			if (value.Count != 1)
			{
				return null;
			}
			text5 = value[0];
		}
		return text5;
		string? Look(string p)
		{
			if (!_provider.Files.ContainsKey(p))
			{
				return null;
			}
			return p;
		}
	}

	private Dictionary<string, List<string>> BuildLeafIndex()
	{
		Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (string key in _provider.Files.Keys)
		{
			if (!key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int num = key.LastIndexOf('/');
			string text = key;
			int num2 = num + 1;
			string text2 = text.Substring(num2, text.Length - 7 - num2);
			if (text2.StartsWith("CP_", StringComparison.OrdinalIgnoreCase))
			{
				if (!dictionary.TryGetValue(text2, out var value))
				{
					value = (dictionary[text2] = new List<string>());
				}
				value.Add(key);
			}
		}
		return dictionary;
	}

	private static bool IsFollowable(string leaf)
	{
		if (!leaf.StartsWith("HID_", StringComparison.OrdinalIgnoreCase) && !leaf.Contains("Specialization", StringComparison.OrdinalIgnoreCase))
		{
			return leaf.StartsWith("Hero", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string LeafOf(string path)
	{
		string text = path.Substring(path.LastIndexOf('/') + 1);
		int num = text.IndexOf('.');
		if (num < 0)
		{
			return text;
		}
		return text.Substring(0, num);
	}

	private void EnsureMounted()
	{
		if (_provider == null)
		{
			throw new InvalidOperationException("Call MountAsync() first.");
		}
	}

	public void Dispose()
	{
		_provider?.Dispose();
		_provider = null;
		_byLeaf = null;
		GC.Collect();
		GC.WaitForPendingFinalizers();
	}
}
