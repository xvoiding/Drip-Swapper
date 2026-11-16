using System.IO;

namespace Drip.Core.FallGuys;

public sealed record FgInstall(string Root)
{
	public string DataDir => Path.Combine(Root, "FallGuys_client_game_Data");

	public string AaDir => Path.Combine(DataDir, "StreamingAssets", "aa");

	public string BundleDir => Path.Combine(AaDir, "StandaloneWindows64");

	public string CatalogPath => Path.Combine(AaDir, "catalog.bundle");

	public bool IsValid
	{
		get
		{
			if (File.Exists(CatalogPath))
			{
				return Directory.Exists(BundleDir);
			}
			return false;
		}
	}
}
