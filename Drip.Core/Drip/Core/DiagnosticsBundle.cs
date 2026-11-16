using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Drip.Core;

public static class DiagnosticsBundle
{
	public const int MaxFileBytes = 1500000;

	private static readonly string[] Wanted = new string[10]
	{
		"activity.log",
		"settings.json",
		"swaps.json",
		"state.json",
		"fun.json",
		"preinstalled.json",
		Path.Combine("RocketLeague", "tweaks-report.txt"),
		Path.Combine("RocketLeague", "activity.log"),
		Path.Combine("FallGuys", "activity.log"),
		Path.Combine("FallGuys", "state.json")
	};

	public static IReadOnlyList<string> Included(string dataDir)
	{
		return Wanted.Where((string w) => File.Exists(Path.Combine(dataDir, w))).ToList();
	}

	public static string Build(string dataDir, string outDir, IEnumerable<KeyValuePair<string, string>> facts, DateTime? now = null)
	{
		Directory.CreateDirectory(outDir);
		string text = (now ?? DateTime.Now).ToString("yyyyMMdd-HHmmss");
		string text2 = Path.Combine(outDir, "Drip_Diagnostics_" + text + ".zip");
		using FileStream stream = new FileStream(text2, FileMode.Create, FileAccess.Write);
		using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Drip Swapper diagnostics " + text);
		foreach (KeyValuePair<string, string> fact in facts)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder2);
			handler.AppendFormatted(fact.Key);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(fact.Value);
			stringBuilder2.AppendLine(ref handler);
		}
		stringBuilder.AppendLine("Files in this zip are copies of small files from the app's data folder. No keys, item lists, game files or backups.");
		Add(zip, "info.txt", Encoding.UTF8.GetBytes(stringBuilder.ToString()));
		string[] wanted = Wanted;
		foreach (string text3 in wanted)
		{
			string path = Path.Combine(dataDir, text3);
			if (File.Exists(path))
			{
				try
				{
					Add(zip, text3.Replace('\\', '/'), ReadTail(path));
				}
				catch
				{
				}
			}
		}
		return text2;
	}

	internal static byte[] ReadTail(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		long length = fileStream.Length;
		int num = (int)Math.Min(length, 1500000L);
		fileStream.Seek(length - num, SeekOrigin.Begin);
		byte[] array = new byte[num];
		int i;
		int num2;
		for (i = 0; i < num; i += num2)
		{
			num2 = fileStream.Read(array, i, num - i);
			if (num2 <= 0)
			{
				break;
			}
		}
		return (i == num) ? array : array[..i];
	}

	private static void Add(ZipArchive zip, string name, byte[] data)
	{
		using Stream stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
		stream.Write(data, 0, data.Length);
	}
}
