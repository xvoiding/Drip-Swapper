using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Drip.Core.FallGuys;

public static class FgBundleIo
{
	public static void WriteBundle(BundleFileInstance bun, AssetsFileInstance afi, string outPath)
	{
		bun.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(afi.file);
		string text = outPath + ".raw";
		try
		{
			using (AssetsFileWriter writer = new AssetsFileWriter(text))
			{
				bun.file.Write(writer, 0L);
			}
			AssetBundleFile assetBundleFile = new AssetBundleFile();
			using FileStream stream = File.OpenRead(text);
			assetBundleFile.Read(new AssetsFileReader(stream));
			string text2 = outPath + ".tmp";
			using (AssetsFileWriter writer2 = new AssetsFileWriter(text2))
			{
				assetBundleFile.Pack(writer2, AssetBundleCompressionType.LZ4);
			}
			assetBundleFile.Close();
			File.Move(text2, outPath, overwrite: true);
		}
		finally
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
		}
	}
}
