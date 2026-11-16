using System.IO;

namespace Drip.Core.FallGuys;

public sealed record FgAsset(int Entry, string Address, string InternalId, string BundleFile, string TypeName)
{
	public string FileName
	{
		get
		{
			if (!Address.Contains('/'))
			{
				return Address;
			}
			return Address.Substring(Address.LastIndexOf('/') + 1);
		}
	}

	public string BaseName => Path.GetFileNameWithoutExtension(FileName);

	public string Extension => Path.GetExtension(FileName).TrimStart('.').ToLowerInvariant();
}
