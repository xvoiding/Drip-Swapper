using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.FortniteModels;

public sealed class FnItemRig
{
	public FnItemKind Kind { get; init; }

	public string Name { get; init; } = "";

	public string Mesh { get; init; } = "";

	public string Diffuse { get; init; } = "";

	public List<FnPlain> Plain { get; init; } = new List<FnPlain>();

	public List<string> Hide { get; init; } = new List<string>();

	public int Sections { get; init; } = 1;

	public IEnumerable<string> Packages => from p in new string[2] { Mesh, Diffuse }.Concat(Plain.Select((FnPlain p) => p.Package)).Concat(Hide)
		where p.Length > 0
		select p;
}
