using System.Collections.Generic;
using System.IO;
using Drip.Core.Mods;

namespace Drip.Core.FortniteModels;

public static class FnItemFile
{
	public static List<string> Validate(ModPack mod)
	{
		List<string> list = new List<string>();
		ModFnItem fnItem = mod.Manifest.FnItem;
		if (!FnItem.Find(fnItem.Kind).HasValue)
		{
			list.Add("This is a '" + fnItem.Kind + "', and this version of Drip Swapper only knows pickaxes and back blings.");
		}
		ModManifest manifest = mod.Manifest;
		if (manifest.Packages.Count > 0 || manifest.PartSwaps.Count > 0 || manifest.SlotSwaps.Count > 0 || manifest.FnModel != null || manifest.FnBuilds != null)
		{
			list.Add("A pickaxe or back bling drip can't also carry other changes.");
		}
		if (list.Count > 0)
		{
			return list;
		}
		FnModelFile.ValidateShape(mod, fnItem.Model, list);
		return list;
	}

	public static FnItemKind Kind(ModPack mod)
	{
		return FnItem.Find(mod.Manifest.FnItem?.Kind) ?? throw new InvalidDataException("This drip has no pickaxe or back bling in it.");
	}

	public static FnTarget Load(ModPack mod)
	{
		ModFnItem modFnItem = mod.Manifest.FnItem ?? throw new InvalidDataException("This drip has no pickaxe or back bling in it.");
		List<string> list = Validate(mod);
		if (list.Count > 0)
		{
			throw new InvalidDataException(string.Join(" ", list));
		}
		return FnModelFile.LoadShape(mod, modFnItem.Model);
	}
}
