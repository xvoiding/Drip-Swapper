using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Drip.Core.Cosmetics;
using Drip.Core.IoStore;
using Drip.Core.Zen;

namespace Drip.Core.Swapping;

public static class SwapVerifier
{
	private static readonly Regex AssetRef = new Regex("\"(/[A-Za-z0-9_\\-/]+(?:\\.[A-Za-z0-9_\\-]+)?)\"", RegexOptions.Compiled);

	public static List<string> Preflight(string paks)
	{
		List<string> list = new List<string>();
		if (!Directory.Exists(paks))
		{
			list.Add(paks + " does not exist.");
			return list;
		}
		if (!File.Exists(Path.Combine(paks, "global.utoc")))
		{
			list.Add("global.utoc is missing, so this does not look like a complete Fortnite install.");
		}
		if (!Directory.EnumerateFiles(paks, "pakchunk*-WindowsClient.utoc").Any())
		{
			list.Add("No pakchunk*-WindowsClient.utoc files found.");
		}
		int num = 0;
		foreach (string item in Directory.EnumerateFiles(paks, "*.utoc").Concat(Directory.EnumerateFiles(paks, "*.ucas")))
		{
			if ((File.GetAttributes(item) & FileAttributes.ReadOnly) != 0)
			{
				num++;
			}
		}
		if (num > 0)
		{
			list.Add($"{num} game file(s) are marked read-only (the app clears this on the files it writes).");
		}
		return list;
	}

	public static VerifyReport Run(string paks, IEnumerable<PatchRecord> records, Func<string, byte[]?> keyForGuid, bool deep = true)
	{
		VerifyReport verifyReport = new VerifyReport();
		List<PatchRecord> list = records.ToList();
		verifyReport.Records = list.Count;
		foreach (IGrouping<string, PatchRecord> item in list.GroupBy((PatchRecord r) => r.Container, StringComparer.OrdinalIgnoreCase))
		{
			verifyReport.Containers++;
			try
			{
				VerifyContainer(paks, item.Key, item.ToList(), keyForGuid, deep, verifyReport);
			}
			catch (Exception ex)
			{
				verifyReport.Add(VerifyLevel.Fail, item.Key, "could not be verified: " + ex.Message);
			}
		}
		return verifyReport;
	}

	public static VerifyReport VerifyRecord(string paks, PatchRecord rec, Func<string, byte[]?> keyForGuid)
	{
		return Run(paks, new _003C_003Ez__ReadOnlySingleElementList<PatchRecord>(rec), keyForGuid, deep: false);
	}

	private static void VerifyContainer(string paks, string name, List<PatchRecord> recs, Func<string, byte[]?> keyForGuid, bool deep, VerifyReport report)
	{
		string text = Path.Combine(paks, name + ".utoc");
		string text2 = text + ".elenr-bak";
		if (!File.Exists(text))
		{
			report.Add(VerifyLevel.Fail, name, "the .utoc is missing.");
			return;
		}
		if ((File.GetAttributes(text) & FileAttributes.ReadOnly) != 0)
		{
			report.Add(VerifyLevel.Warn, name, "the .utoc is marked read-only.");
		}
		TocFile tocFile;
		try
		{
			tocFile = TocFile.Load(text);
		}
		catch (Exception ex)
		{
			report.Add(VerifyLevel.Fail, name, "the .utoc can't be read, so the game can't either (" + ex.Message + "). Use Verify in the Epic launcher to repair it.");
			return;
		}
		bool flag = File.Exists(text2);
		if (!flag)
		{
			report.Add(VerifyLevel.Warn, name, "no backup of the original .utoc exists, so changes can't be compared or fully undone.");
		}
		else
		{
			try
			{
				TocFile other = TocFile.Load(text2);
				string text3 = tocFile.DescribeHeaderDifference(other);
				if (text3 != null)
				{
					report.Add(VerifyLevel.Fail, name, "the live .utoc layout differs from the backup (" + text3 + "). The game was updated or repaired since the swap, or something else rewrote it.");
				}
				else if (deep)
				{
					DiffAgainstBackup(text, text2, name, recs, report);
				}
			}
			catch (Exception ex2)
			{
				report.Add(VerifyLevel.Warn, name, "the backup couldn't be compared: " + ex2.Message);
			}
		}
		IoContainer liveBox = new IoContainer(paks, name, keyForGuid, preferBackup: false);
		IoContainer original = (flag ? new IoContainer(paks, name, keyForGuid) : null);
		foreach (PatchRecord rec in recs)
		{
			VerifyRecordIn(paks, name, rec, tocFile, liveBox, original, report);
		}
	}

	private static void DiffAgainstBackup(string live, string bak, string name, List<PatchRecord> recs, VerifyReport report)
	{
		byte[] array = File.ReadAllBytes(live);
		byte[] array2 = File.ReadAllBytes(bak);
		if (array.Length != array2.Length)
		{
			report.Add(VerifyLevel.Fail, name, $"the live .utoc is {array.Length} bytes but its backup is {array2.Length}.");
			return;
		}
		foreach (PatchRecord rec in recs)
		{
			byte[] array3 = Convert.FromHexString(rec.OriginalBlock);
			byte[] array4 = Convert.FromHexString(rec.OriginalOffsetLength);
			if (rec.BlockPos < 0 || rec.BlockPos + array3.Length > array.Length || rec.OffsetLengthPos < 0 || rec.OffsetLengthPos + array4.Length > array.Length)
			{
				report.Add(VerifyLevel.Fail, name, "a recorded patch position lies outside the .utoc.");
				return;
			}
			array3.CopyTo(array, (int)rec.BlockPos);
			array4.CopyTo(array, (int)rec.OffsetLengthPos);
		}
		int num = 0;
		int num2 = -1;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != array2[i])
			{
				num++;
				if (num2 < 0)
				{
					num2 = i;
				}
			}
		}
		if (num > 0)
		{
			report.Add(VerifyLevel.Warn, name, $"{num} byte(s) of the live .utoc differ from the backup outside this app's swaps (first at 0x{num2:X}). Another tool, an update or a repair changed it.");
		}
	}

	private static void VerifyRecordIn(string paks, string name, PatchRecord rec, TocFile liveToc, IoContainer liveBox, IoContainer? original, VerifyReport report)
	{
		string text = $"{rec.SourcePackage} (chunk {rec.ChunkIndex})";
		if (!IoContainer.RecordIsApplied(paks, rec))
		{
			report.Add(VerifyLevel.Fail, name, text + ": the live .utoc does not hold this swap any more (reverted by a repair or update, or never written).");
			return;
		}
		int num = Math.Max(rec.BlockCount, 1);
		for (int i = 0; i < num; i++)
		{
			TocBlock block = liveToc.GetBlock(rec.BlockIndex + i);
			ulong partitionSize = liveToc.PartitionSize;
			int num2 = (int)((partitionSize != ulong.MaxValue) ? (block.Offset / partitionSize) : 0);
			long num3 = (long)((partitionSize == ulong.MaxValue) ? block.Offset : (block.Offset % partitionSize));
			string text2 = Path.Combine(paks, (num2 == 0) ? (name + ".ucas") : $"{name}_s{num2}.ucas");
			if (!File.Exists(text2))
			{
				report.Add(VerifyLevel.Fail, name, $"{text}: block {i} points into {Path.GetFileName(text2)}, which does not exist.");
				return;
			}
			long num4 = num3 + IoContainer.Align16(block.CompressedSize);
			if (num4 > new FileInfo(text2).Length)
			{
				report.Add(VerifyLevel.Fail, name, $"{text}: block {i} ends at {num4} but {Path.GetFileName(text2)} is only {new FileInfo(text2).Length} bytes (the added data was cut off).");
				return;
			}
		}
		byte[] array;
		try
		{
			array = liveBox.ReadChunk(rec.ChunkIndex);
		}
		catch (Exception ex)
		{
			report.Add(VerifyLevel.Fail, name, text + ": the chunk can't be read back (" + ex.Message + ").");
			return;
		}
		if (liveToc.GetChunkId(rec.ChunkIndex).Type != 1)
		{
			if (array.Length == 0)
			{
				report.Add(VerifyLevel.Fail, name, text + ": the bulk-data chunk reads back empty.");
				return;
			}
		}
		else
		{
			ZenPackage zenPackage;
			try
			{
				zenPackage = ZenPackage.Parse(array);
			}
			catch (Exception ex2)
			{
				report.Add(VerifyLevel.Fail, name, text + ": the chunk reads back but is not a valid package (" + ex2.Message + ").");
				return;
			}
			if (!string.Equals(zenPackage.PackageName, rec.SourcePackage, StringComparison.OrdinalIgnoreCase))
			{
				report.Add(VerifyLevel.Fail, name, text + ": the chunk reads back as '" + zenPackage.PackageName + "', not the package the game looks for.");
				return;
			}
		}
		string text3 = $"{array.Length} bytes";
		if (original != null)
		{
			try
			{
				byte[] array2 = original.ReadChunk(rec.ChunkIndex);
				if (((ReadOnlySpan<byte>)array2.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)array))
				{
					report.Add(VerifyLevel.Fail, name, text + ": the chunk is byte-for-byte the original, so nothing actually changed.");
					return;
				}
				text3 += $" (was {array2.Length}, content {Short(array2)} -> {Short(array)})";
			}
			catch (Exception ex3)
			{
				text3 = text3 + " (original not readable: " + ex3.Message + ")";
			}
		}
		string value = (rec.TargetPackage.StartsWith("mod:", StringComparison.Ordinal) ? ("the mod '" + rec.TargetPackage.Substring(4) + "'") : PartPair.LeafOf(rec.TargetPackage));
		report.Add(VerifyLevel.Ok, name, $"{text} now reads back as {value}: {text3}.");
	}

	private static string Short(byte[] data)
	{
		return Convert.ToHexString(SHA256.HashData(data)).Substring(0, 8).ToLowerInvariant();
	}

	private static HashSet<string> Refs(string json, params string[] ignore)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Match item in AssetRef.Matches(json))
		{
			string v = item.Groups[1].Value;
			if (!ignore.Any((string i) => i.Length > 0 && v.Contains(i, StringComparison.OrdinalIgnoreCase)))
			{
				hashSet.Add(v);
			}
		}
		return hashSet;
	}

	public static VerifyLine Semantic(PartResolver resolver, string sourceId, string targetId, string sourceName, string targetName, bool styled = false)
	{
		try
		{
			string text = resolver.FindAssetPath(sourceId);
			string text2 = resolver.FindAssetPath(targetId);
			if (text == null || text2 == null)
			{
				return new VerifyLine(VerifyLevel.Warn, "", sourceName + ": can't find the " + ((text == null) ? sourceId : targetId) + " asset in the mounted game, so what the game will load can't be shown.");
			}
			HashSet<string> hashSet = Refs(resolver.DumpJson(text), sourceId, targetId);
			HashSet<string> hashSet2 = Refs(resolver.DumpJson(text2), sourceId, targetId);
			List<string> list = hashSet2.Except(hashSet, StringComparer.OrdinalIgnoreCase).ToList();
			List<string> list2 = hashSet.Except(hashSet2, StringComparer.OrdinalIgnoreCase).ToList();
			if (list.Count == 0 && list2.Count == 0)
			{
				return new VerifyLine(VerifyLevel.Ok, "", $"{sourceName} now loads as {targetName}: the game reads the same {hashSet.Count} asset reference(s) from both.");
			}
			string value = string.Join(", ", list.Concat(list2).Take(3).Select(PartPair.LeafOf));
			return new VerifyLine((!styled) ? VerifyLevel.Warn : VerifyLevel.Ok, "", $"{sourceName}: {list.Count} reference(s) of {targetName} are missing and {list2.Count} extra ones remain (e.g. {value})." + (styled ? " That is expected: a style was applied." : " The swap is in the files but the game may still show parts of the old item."));
		}
		catch (Exception ex)
		{
			return new VerifyLine(VerifyLevel.Warn, "", sourceName + ": couldn't compare what the game loads (" + ex.Message + ").");
		}
	}
}
