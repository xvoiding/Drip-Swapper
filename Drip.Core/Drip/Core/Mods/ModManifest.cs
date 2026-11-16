using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.Mods;

public sealed class ModManifest
{
	public int Format { get; set; } = 1;

	public string Name { get; set; } = "";

	public string Author { get; set; } = "";

	public string Description { get; set; } = "";

	public string Base { get; set; } = "";

	public string Icon { get; set; } = "";

	public List<ModPartSwap> PartSwaps { get; set; } = new List<ModPartSwap>();

	public List<ModSlotSwap> SlotSwaps { get; set; } = new List<ModSlotSwap>();

	public List<ModPackageFile> Packages { get; set; } = new List<ModPackageFile>();

	public ModFnModel? FnModel { get; set; }

	public ModFnBuilds? FnBuilds { get; set; }

	public ModFnItem? FnItem { get; set; }

	internal bool HasDuplicateSlots()
	{
		return SlotSwaps.GroupBy((ModSlotSwap x) => x.Slot ?? "", StringComparer.OrdinalIgnoreCase).Any((IGrouping<string, ModSlotSwap> g) => g.Count() > 1);
	}
}
