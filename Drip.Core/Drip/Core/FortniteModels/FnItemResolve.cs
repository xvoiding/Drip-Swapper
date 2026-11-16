using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Drip.Core.Cosmetics;
using Drip.Core.Swapping;
using Newtonsoft.Json.Linq;

namespace Drip.Core.FortniteModels;

public static class FnItemResolve
{
	internal enum TexKind
	{
		Diffuse,
		Normal,
		Specular,
		Mask,
		Other
	}

	private static readonly Regex PathStr = new Regex("\"(/[A-Za-z0-9_]+(?:/[A-Za-z0-9_.\\-]+)+)\"", RegexOptions.Compiled);

	public const string SlotItem = "ModItem";

	public const string SlotPlain = "ModItemPlain";

	public const string SlotHide = "ModItemHide";

	public static FnItemRig Resolve(PartResolver resolver, Func<string, byte, byte[]> read, string cosmeticId, FnItemKind kind, string name, Action<string>? log = null)
	{
		string value = FnItem.Label(kind);
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		try
		{
			if (kind == FnItemKind.BackBling)
			{
				foreach (CharacterPart item3 in from p in resolver.Resolve(cosmeticId)
					where p.Slot.Contains("Backpack", StringComparison.OrdinalIgnoreCase)
					select p)
				{
					list.AddRange(FnCarriers.MeshesOf(resolver, item3.Package));
				}
			}
			else
			{
				string text = resolver.FindAssetPath(cosmeticId);
				if (text != null)
				{
					foreach (string item4 in References(resolver, text, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
					{
						if (IsMeshPath(item4))
						{
							list.Add(item4);
						}
						else if (item4.Contains("/Materials/", StringComparison.OrdinalIgnoreCase) || Leaf(item4).StartsWith("MI_", StringComparison.OrdinalIgnoreCase))
						{
							list2.Add(item4);
						}
					}
				}
			}
		}
		catch (FileNotFoundException)
		{
			throw new InvalidDataException(name + " isn't stored in your game files yet (Fortnite only downloads some items when you use them), so it can't carry a model. Pick one you've used in the Locker.");
		}
		list = list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			throw new InvalidDataException($"Couldn't find {name}'s {value} mesh in your game files. Pick another one.");
		}
		List<(string, int)> list3 = new List<(string, int)>();
		foreach (string item5 in list)
		{
			try
			{
				List<FnLod> list4 = FnMesh.FindLods(read(item5, 2));
				list3.Add((item5, list4[0].VertexCount));
			}
			catch (Exception ex2)
			{
				L($"  {Leaf(item5)}: not usable ({ex2.Message.Split('\n')[0]})");
			}
		}
		if (list3.Count == 0)
		{
			throw new InvalidDataException(name + "'s mesh is built in a way this version can't rewrite (it isn't one of the layouts it knows). Pick another one.");
		}
		string main = list3.OrderByDescending(((string Mesh, int Verts) u) => u.Verts).First().Item1;
		List<string> list5 = (from m in list3.Select(((string Mesh, int Verts) u) => u.Mesh)
			where !m.Equals(main, StringComparison.OrdinalIgnoreCase)
			select m).ToList();
		(int Lod0Sections, List<string> Materials) tuple = resolver.MeshSections(main);
		int item = tuple.Lod0Sections;
		List<string> item2 = tuple.Materials;
		string text2 = ItemRoot(main);
		List<string> list6 = list2.Concat(item2).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		string text3 = "";
		List<FnPlain> list7 = new List<FnPlain>();
		foreach (string item6 in list6)
		{
			List<(string, string)> list8 = TexturesOf(resolver, item6, 0);
			(string, string) tuple2 = list8.FirstOrDefault(((string Param, string Package) tuple4) => Classify(tuple4.Param) == TexKind.Diffuse);
			if (tuple2.Item2 == null)
			{
				continue;
			}
			if (!tuple2.Item2.StartsWith(text2 + "/", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException(name + "'s colour picture is shared with other items, so changing it would change them too. Pick another one.");
			}
			text3 = tuple2.Item2;
			foreach (var t in list8)
			{
				TexKind texKind = Classify(t.Item1);
				bool flag = ((texKind == TexKind.Diffuse || texKind == TexKind.Other) ? true : false);
				if (flag || !t.Item2.StartsWith(text2 + "/", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				try
				{
					FnTexturePackage.Info info = FnTexturePackage.Read(read(t.Item2, 1));
					string pixelFormat = info.PixelFormat;
					flag = ((pixelFormat == "PF_DXT1" || pixelFormat == "PF_BC5") ? true : false);
					if (!flag || info.BulkEntries < 3)
					{
						L($"  {Leaf(t.Item2)}: {info.PixelFormat}, left as it is.");
						continue;
					}
				}
				catch (Exception ex3)
				{
					L($"  {Leaf(t.Item2)}: unreadable ({ex3.Message.Split('\n')[0]}), left as it is.");
					continue;
				}
				var (r, g, b, what) = texKind switch
				{
					TexKind.Normal => ((byte)128, (byte)128, byte.MaxValue, "flat normal map"), 
					TexKind.Specular => ((byte)120, (byte)8, (byte)185, "plain shine map"), 
					_ => ((byte)0, (byte)0, (byte)0, "empty mask"), 
				};
				if (!list7.Any((FnPlain p) => p.Package.Equals(t.Item2, StringComparison.OrdinalIgnoreCase)))
				{
					list7.Add(new FnPlain(t.Item2, r, g, b, what));
				}
			}
			break;
		}
		if (text3.Length == 0)
		{
			throw new InvalidDataException("Couldn't find " + name + "'s colour picture, so there's nowhere to put the model's picture. Pick another one.");
		}
		L($"{name}: mesh {Leaf(main)}, picture {Leaf(text3)}, {list7.Count} other picture(s) to flatten, {list5.Count} extra piece(s) to hide.");
		return new FnItemRig
		{
			Kind = kind,
			Name = name,
			Mesh = main,
			Diffuse = text3,
			Plain = list7,
			Hide = list5,
			Sections = item
		};
		void L(string s)
		{
			log?.Invoke(s);
		}
	}

	public static string? Problem(FnItemRig rig, Func<string, byte, byte[]> read)
	{
		if (rig.Sections != 1)
		{
			return $"{rig.Name} is made of {rig.Sections} separate material parts, and a model needs exactly one. Pick another one.";
		}
		string value = "mesh";
		try
		{
			List<FnLod> list = FnMesh.FindLods(read(rig.Mesh, 2));
			if (!FnMeshInfo.Read(read(rig.Mesh, 1), list).BoneMaps.ContainsKey(0))
			{
				return rig.Name + ": no bone list for its closest detail level.";
			}
			if (list[0].VertexCount < 40)
			{
				return rig.Name + "'s mesh is too small to hold a model.";
			}
			foreach (string item in rig.Hide)
			{
				value = "an extra piece";
				if (FnMesh.FindLods(read(item, 2)).Count < 1)
				{
					return rig.Name + ": an extra piece isn't laid out the way this version expects.";
				}
			}
			value = "the colour picture";
			FnTexturePackage.Info info = FnTexturePackage.Read(read(rig.Diffuse, 1));
			if (info.PixelFormat != "PF_DXT1")
			{
				return rig.Name + ": its colour picture is " + info.PixelFormat + ", which can't be rewritten. Pick another one.";
			}
			if (info.BulkEntries < 7)
			{
				return rig.Name + ": its colour picture is too small for a model picture.";
			}
			return null;
		}
		catch (Exception ex)
		{
			return $"{rig.Name}: couldn't read {value} ({Friendly.Explain(ex.Message).Split('\n')[0]}).";
		}
	}

	public static List<SwappedPart> Remember(FnItemRig rig, string modKey)
	{
		List<SwappedPart> list = new List<SwappedPart>
		{
			new SwappedPart
			{
				Slot = "ModItem",
				SourcePackage = rig.Mesh,
				TargetPackage = rig.Diffuse
			}
		};
		foreach (FnPlain item in rig.Plain)
		{
			list.Add(new SwappedPart
			{
				Slot = "ModItemPlain",
				SourcePackage = item.Package,
				TargetPackage = $"{item.R},{item.G},{item.B},{item.What}"
			});
		}
		foreach (string item2 in rig.Hide)
		{
			list.Add(new SwappedPart
			{
				Slot = "ModItemHide",
				SourcePackage = item2,
				TargetPackage = modKey
			});
		}
		return list;
	}

	public static FnItemRig? Recall(SwappedItem item)
	{
		SwappedPart swappedPart = item.Parts.FirstOrDefault((SwappedPart p) => p.Slot == "ModItem");
		FnItemKind? fnItemKind;
		if (item.Kind == "Pickaxe")
		{
			fnItemKind = FnItemKind.Pickaxe;
		}
		else
		{
			fnItemKind = ((item.Kind == "Back Bling") ? new FnItemKind?(FnItemKind.BackBling) : ((FnItemKind?)null));
		}
		if (swappedPart == null || !fnItemKind.HasValue)
		{
			return null;
		}
		List<FnPlain> list = new List<FnPlain>();
		foreach (SwappedPart item2 in item.Parts.Where((SwappedPart p) => p.Slot == "ModItemPlain"))
		{
			string[] array = item2.TargetPackage.Split(',', 4);
			if (array.Length == 4 && byte.TryParse(array[0], out var result) && byte.TryParse(array[1], out var result2) && byte.TryParse(array[2], out var result3))
			{
				list.Add(new FnPlain(item2.SourcePackage, result, result2, result3, array[3]));
			}
		}
		return new FnItemRig
		{
			Kind = fnItemKind.Value,
			Name = item.SourceName,
			Mesh = swappedPart.SourcePackage,
			Diffuse = swappedPart.TargetPackage,
			Plain = list,
			Hide = (from p in item.Parts
				where p.Slot == "ModItemHide"
				select p.SourcePackage).ToList(),
			Sections = 1
		};
	}

	internal static TexKind Classify(string param)
	{
		string text = param.Trim().ToLowerInvariant();
		if (text.Contains("gradient") || text.Contains("layer") || text.Contains("thin film") || text.Contains("fuzz"))
		{
			return TexKind.Other;
		}
		if (text.Contains("normal"))
		{
			return TexKind.Normal;
		}
		if (text.Contains("spec") || text == "srm" || text == "orm" || text.Contains("rough"))
		{
			return TexKind.Specular;
		}
		if (!text.Contains("diffuse") && !text.Contains("basecolor") && !text.Contains("base color") && !text.Contains("albedo"))
		{
			switch (text)
			{
			case "color":
			case "d":
				break;
			default:
				if (!text.Contains("mask") && !text.Contains("emissive") && !(text == "e") && !text.Contains("fx"))
				{
					return TexKind.Other;
				}
				goto case "m";
			case "m":
				return TexKind.Mask;
			}
		}
		return TexKind.Diffuse;
	}

	internal static string ItemRoot(string mesh)
	{
		string[] array = new string[2] { "/Meshes/", "/Mesh/" };
		foreach (string value in array)
		{
			int num = mesh.LastIndexOf(value, StringComparison.OrdinalIgnoreCase);
			if (num > 0)
			{
				return mesh.Substring(0, num);
			}
		}
		string text = mesh.Substring(0, mesh.LastIndexOf('/'));
		int num2 = text.LastIndexOf('/');
		if (num2 <= 0)
		{
			return text;
		}
		return text.Substring(0, num2);
	}

	private static string Leaf(string p)
	{
		string text = p.Substring(p.LastIndexOf('/') + 1);
		int num = text.IndexOf('.');
		if (num < 0)
		{
			return text;
		}
		return text.Substring(0, num);
	}

	internal static bool IsMeshPath(string p)
	{
		if (p.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("/Engine/", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string text = Leaf(p);
		if (text.Contains("Skeleton", StringComparison.OrdinalIgnoreCase) || text.Contains("Physics", StringComparison.OrdinalIgnoreCase) || text.Contains("_Anim", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (p.Contains("/Materials/", StringComparison.OrdinalIgnoreCase) || p.Contains("/Textures/", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!p.Contains("/Mesh", StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith("SK_", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static List<string> References(PartResolver resolver, string file, int depth, HashSet<string> seen)
	{
		List<string> list = new List<string>();
		foreach (Match item in PathStr.Matches(resolver.DumpJson(file)))
		{
			string text = PackageNames.Normalize(item.Groups[1].Value);
			if (!seen.Add(text))
			{
				continue;
			}
			list.Add(text);
			string text2 = Leaf(text);
			if ((text2.StartsWith("WID_", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("B_", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("BP_", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("Pickaxe_", StringComparison.OrdinalIgnoreCase)) && depth < 2 && !text.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase))
			{
				string text3 = resolver.PackageFile(text);
				if (text3 != null)
				{
					list.AddRange(References(resolver, text3, depth + 1, seen));
				}
			}
		}
		return list;
	}

	internal static List<(string Param, string Package)> TexturesOf(PartResolver resolver, string material, int depth)
	{
		List<(string, string)> list = new List<(string, string)>();
		string text = resolver.PackageFile(material);
		if (text == null)
		{
			return list;
		}
		JToken jToken = JToken.Parse(resolver.DumpJson(text));
		string text2 = null;
		IEnumerable<JToken> source;
		if (!(jToken is JContainer jContainer))
		{
			IEnumerable<JToken> enumerable = new JToken[1] { jToken };
			source = enumerable;
		}
		else
		{
			source = jContainer.DescendantsAndSelf();
		}
		foreach (JObject item in source.OfType<JObject>())
		{
			if (item["TextureParameterValues"] is JArray source2)
			{
				foreach (JObject item2 in source2.OfType<JObject>())
				{
					string text3 = item2["ParameterInfo"]?["Name"]?.ToString() ?? "";
					string text4 = item2["ParameterValue"]?["ObjectPath"]?.ToString() ?? "";
					if (text3.Length > 0 && text4.Length > 0)
					{
						list.Add((text3, PackageNames.Normalize(text4)));
					}
				}
			}
			if (item["Parent"] is JObject jObject)
			{
				string text5 = jObject["ObjectPath"]?.ToString();
				if (text5 != null && text5.Length > 0 && text2 == null)
				{
					text2 = PackageNames.Normalize(text5);
				}
			}
		}
		if (list.Count == 0 && text2 != null && depth < 3)
		{
			return TexturesOf(resolver, text2, depth + 1);
		}
		return list;
	}
}
