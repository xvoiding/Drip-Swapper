using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.RocketLeague;

public static class UpkTables
{
	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	public static List<UpkExport> Exports(byte[] img, UpkSummary s)
	{
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		int[] starts = UpkNames.ExportStarts(img, s.ExportOffset, s.DependsOffset, s.ExportCount) ?? throw new UpkException("Export table has an unexpected size, so this package can't be edited safely.");
		List<UpkExport> list = new List<UpkExport>(starts.Length);
		for (int i = 0; i < starts.Length; i++)
		{
			int num = starts[i];
			list.Add(new UpkExport(i + 1, num, ClassOf(I(img, num)), Name(I(img, num + 12)), I(img, num + 32), I(img, num + 36)));
		}
		return list;
		string ClassOf(int idx)
		{
			if (idx == 0)
			{
				return "Class";
			}
			if (idx < 0)
			{
				int num2 = s.ImportOffset + (-idx - 1) * 28;
				if (num2 + 28 > img.Length)
				{
					return "";
				}
				return Name(I(img, num2 + 20));
			}
			int num3 = idx - 1;
			if (num3 >= starts.Length)
			{
				return "";
			}
			return Name(I(img, starts[num3] + 12));
		}
		string Name(int num2)
		{
			if (num2 < 0 || num2 >= names.Count)
			{
				return "";
			}
			return names[num2].Text;
		}
	}

	public static byte[] ReplaceMany(byte[] img, UpkSummary s, IReadOnlyDictionary<int, byte[]> repl, out List<(long Start, int OldLen, int NewLen)> edits)
	{
		List<UpkExport> ex = Exports(img, s);
		List<UpkExport> list = (from i in repl.Keys
			select ex[i - 1] into e
			orderby e.SerialOffset
			select e).ToList();
		edits = new List<(long, int, int)>();
		long num = img.Length;
		foreach (UpkExport item in list)
		{
			num += repl[item.Index].Length - item.SerialSize;
		}
		byte[] array = new byte[num];
		int num2 = 0;
		int num3 = 0;
		long num4 = 0L;
		(int, long)[] array2 = new (int, long)[list.Count];
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			UpkExport upkExport = list[num5];
			byte[] array3 = repl[upkExport.Index];
			int num6 = upkExport.SerialOffset - num2;
			if (num6 < 0)
			{
				throw new UpkException("Two parts of the package overlap, so it can't be edited safely.");
			}
			Array.Copy(img, num2, array, num3, num6);
			num3 += num6;
			edits.Add((upkExport.SerialOffset + num4, upkExport.SerialSize, array3.Length));
			if (num4 != 0L)
			{
				BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(upkExport.Start + 36, 4), (int)(upkExport.SerialOffset + num4));
			}
			Array.Copy(array3, 0, array, num3, array3.Length);
			num3 += array3.Length;
			num2 = upkExport.SerialOffset + upkExport.SerialSize;
			num4 += array3.Length - upkExport.SerialSize;
			array2[num5] = (num2, num4);
			BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(upkExport.Start + 32, 4), array3.Length);
		}
		Array.Copy(img, num2, array, num3, img.Length - num2);
		foreach (UpkExport item2 in ex)
		{
			if (!repl.ContainsKey(item2.Index) && item2.SerialSize > 0)
			{
				long num7 = 0L;
				for (int num8 = 0; num8 < array2.Length && array2[num8].Item1 <= item2.SerialOffset; num8++)
				{
					num7 = array2[num8].Item2;
				}
				if (num7 != 0L)
				{
					BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(item2.Start + 36, 4), (int)(item2.SerialOffset + num7));
				}
			}
		}
		return array;
	}

	public static byte[] ReplaceExport(byte[] img, UpkSummary s, UpkExport target, byte[] data)
	{
		int num = data.Length - target.SerialSize;
		byte[] array = new byte[img.Length + num];
		Array.Copy(img, 0, array, 0, target.SerialOffset);
		Array.Copy(data, 0, array, target.SerialOffset, data.Length);
		int num2 = target.SerialOffset + target.SerialSize;
		Array.Copy(img, num2, array, target.SerialOffset + data.Length, img.Length - num2);
		BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(target.Start + 32, 4), data.Length);
		if (num != 0)
		{
			foreach (UpkExport item in Exports(img, s))
			{
				if (item.Index != target.Index && item.SerialSize > 0 && item.SerialOffset >= num2)
				{
					BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(item.Start + 36, 4), item.SerialOffset + num);
				}
			}
		}
		return array;
	}
}
