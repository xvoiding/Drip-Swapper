using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Drip.Core.RocketLeague;

public static class UpkNames
{
	private static readonly string[] ProtectedParts = new string[10] { "None", "Core", "Engine", "TAGame", "Package", "Textures", ".tfc", "Class", "Object", "Default" };

	public static List<NameEntry> Read(byte[] img, UpkSummary s, out int endOffset)
	{
		List<NameEntry> list = new List<NameEntry>(s.NameCount);
		int num = s.NameOffset;
		for (int i = 0; i < s.NameCount; i++)
		{
			int num2 = num;
			if (num + 4 > img.Length)
			{
				throw new UpkException("Name table runs past the end.");
			}
			int num3 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(num, 4));
			num += 4;
			bool flag = num3 < 0;
			int num4 = (flag ? (-num3 * 2) : num3);
			string text;
			if (num3 == 0)
			{
				text = "";
			}
			else
			{
				if (num4 < 0 || num + num4 + 8 > img.Length)
				{
					throw new UpkException("Name table entry is cut short.");
				}
				text = (flag ? Encoding.Unicode.GetString(img, num, num4) : Encoding.Latin1.GetString(img, num, num4));
				int num5 = text.IndexOf('\0');
				if (num5 >= 0)
				{
					text = text.Substring(0, num5);
				}
			}
			num += num4;
			ulong flags = BinaryPrimitives.ReadUInt64LittleEndian(img.AsSpan(num, 8));
			num += 8;
			list.Add(new NameEntry(text, flags, flag, num3, num2, num - num2));
		}
		endOffset = num;
		return list;
	}

	public static byte[] Rename(byte[] img, Func<string, string?> rename, bool padShorter, out int renamedCount)
	{
		UpkSummary upkSummary = UpkSummary.Parse(img);
		List<NameEntry> list = Read(img, upkSummary, out var endOffset);
		if (endOffset != upkSummary.ImportOffset)
		{
			throw new UpkException("Name table doesn't end where the import table starts, so this package can't be edited safely.");
		}
		using MemoryStream memoryStream = new MemoryStream();
		renamedCount = 0;
		foreach (NameEntry item in list)
		{
			string text = rename(item.Text);
			if (text == null || text == item.Text)
			{
				memoryStream.Write(img, item.Start, item.TotalBytes);
				continue;
			}
			renamedCount++;
			int num = (item.Wide ? (-(text.Length + 1)) : (text.Length + 1));
			if (padShorter && Math.Abs(item.Length) > Math.Abs(num))
			{
				num = item.Length;
			}
			WriteEntry(memoryStream, text, item.Flags, item.Wide, num);
		}
		byte[] array = memoryStream.ToArray();
		int num2 = upkSummary.ImportOffset - upkSummary.NameOffset;
		int delta = array.Length - num2;
		byte[] o = new byte[img.Length + delta];
		Array.Copy(img, 0, o, 0, upkSummary.NameOffset);
		Array.Copy(array, 0, o, upkSummary.NameOffset, array.Length);
		Array.Copy(img, upkSummary.ImportOffset, o, upkSummary.NameOffset + array.Length, img.Length - upkSummary.ImportOffset);
		if (delta == 0)
		{
			return o;
		}
		int f = upkSummary.FieldsPos;
		Bump(3, onlyIfSet: false);
		Bump(5, onlyIfSet: false);
		Bump(6, onlyIfSet: false);
		Bump(7, onlyIfSet: true);
		Bump(10, onlyIfSet: true);
		int[] array2 = ExportStarts(o, upkSummary.ExportOffset + delta, upkSummary.DependsOffset + delta, upkSummary.ExportCount) ?? throw new UpkException("Export table has an unexpected size, so this package can't be edited safely.");
		foreach (int num3 in array2)
		{
			int num4 = BinaryPrimitives.ReadInt32LittleEndian(o.AsSpan(num3 + 32, 4));
			int num5 = BinaryPrimitives.ReadInt32LittleEndian(o.AsSpan(num3 + 36, 4));
			if (num4 > 0 && num5 >= upkSummary.ImportOffset)
			{
				BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(num3 + 36, 4), num5 + delta);
			}
		}
		return o;
		void Bump(int index, bool onlyIfSet)
		{
			int start = f + index * 4;
			int num6 = BinaryPrimitives.ReadInt32LittleEndian(o.AsSpan(start, 4));
			if (!onlyIfSet || num6 != 0)
			{
				BinaryPrimitives.WriteInt32LittleEndian(o.AsSpan(start, 4), num6 + delta);
			}
		}
	}

	public static int[]? ExportStarts(byte[] img, int exportOffset, int dependsOffset, int count)
	{
		int[] array = new int[count];
		int num = exportOffset;
		bool flag = true;
		for (int i = 0; i < count; i++)
		{
			if (num < 0 || num + 48 > img.Length)
			{
				flag = false;
				break;
			}
			array[i] = num;
			int num2 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(num + 44, 4));
			if (num2 < 0 || num2 > 4096)
			{
				flag = false;
				break;
			}
			num += 68 + 4 * num2;
		}
		if (flag && num == dependsOffset)
		{
			return array;
		}
		int[] array2 = WalkComponentMaps(img, exportOffset, dependsOffset, count);
		if (array2 != null)
		{
			return array2;
		}
		long num3 = (long)dependsOffset - (long)exportOffset;
		if (count > 0 && num3 > 0 && num3 % count == 0L && num3 / count >= 68)
		{
			int num4 = (int)(num3 / count);
			for (int j = 0; j < count; j++)
			{
				array[j] = exportOffset + j * num4;
			}
			if (exportOffset + num3 > img.Length)
			{
				return null;
			}
			return array;
		}
		return WalkWithValidation(img, exportOffset, dependsOffset, count);
	}

	private static int[]? WalkComponentMaps(byte[] img, int exportOffset, int dependsOffset, int count)
	{
		if (count <= 0 || exportOffset < 0 || dependsOffset <= exportOffset || dependsOffset > img.Length)
		{
			return null;
		}
		int[] array = new int[count];
		long num = exportOffset;
		for (int i = 0; i < count; i++)
		{
			if (num + 72 > dependsOffset)
			{
				return null;
			}
			array[i] = (int)num;
			int num2 = I((int)num + 40);
			if (num2 < 0 || num2 > 256)
			{
				return null;
			}
			long num3 = num + 44 + 12L * (long)num2 + 4;
			if (num3 + 4 > dependsOffset)
			{
				return null;
			}
			int num4 = I((int)num3);
			if (num4 < 0 || num4 > 4096)
			{
				return null;
			}
			num = num3 + 4 + 4L * (long)num4 + 20;
		}
		if (num != dependsOffset)
		{
			return null;
		}
		return array;
		int I(int at)
		{
			return BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(at, 4));
		}
	}

	private static int[]? WalkWithValidation(byte[] img, int exportOffset, int dependsOffset, int count)
	{
		if (count <= 0 || count > 20000 || exportOffset < 0 || dependsOffset <= exportOffset || dependsOffset > img.Length)
		{
			return null;
		}
		UpkSummary upkSummary = UpkSummary.Parse(img);
		int names = upkSummary.NameCount;
		int imports = upkSummary.ImportCount;
		HashSet<(int, int)> dead = new HashSet<(int, int)>();
		int[] sizes = new int[count];
		if (!Go(0, exportOffset))
		{
			return null;
		}
		int[] array = new int[count];
		int num = exportOffset;
		for (int i = 0; i < count; i++)
		{
			array[i] = num;
			num += sizes[i];
		}
		return array;
		bool Go(int num2, int p)
		{
			if (num2 == count)
			{
				return p == dependsOffset;
			}
			if (p > dependsOffset || dead.Contains((num2, p)) || !Valid(p))
			{
				return false;
			}
			int num3 = I(p + 44);
			for (int j = 0; j <= 8; j += 4)
			{
				int num4 = 68 + 4 * num3 + j;
				sizes[num2] = num4;
				if (Go(num2 + 1, p + num4))
				{
					return true;
				}
			}
			dead.Add((num2, p));
			return false;
		}
		int I(int at)
		{
			return BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(at, 4));
		}
		bool Valid(int p)
		{
			if (p < 0 || p + 72 > img.Length)
			{
				return false;
			}
			int num2 = I(p);
			int num3 = I(p + 4);
			int num4 = I(p + 8);
			int num5 = I(p + 12);
			int num6 = I(p + 16);
			int num7 = I(p + 44);
			int num8 = I(p + 32);
			int num9 = I(p + 36);
			if (num5 < 0 || num5 >= names || num6 < 0 || num6 >= 100000)
			{
				return false;
			}
			if (num2 < -imports || num2 > count || num3 < -imports || num3 > count || num4 < -imports || num4 > count)
			{
				return false;
			}
			if (num7 >= 0 && num7 <= 4096 && num8 >= 0)
			{
				return num9 >= 0;
			}
			return false;
		}
	}

	public static byte[] RenameKeepingSize(byte[] img, Func<string, string?> rename, out int renamedCount, out int trimmedCount)
	{
		UpkSummary upkSummary = UpkSummary.Parse(img);
		List<NameEntry> list = Read(img, upkSummary, out var endOffset);
		if (endOffset != upkSummary.ImportOffset)
		{
			throw new UpkException("Name table doesn't end where the import table starts, so this package can't be edited safely.");
		}
		string[] array = new string[list.Count];
		int num = 0;
		renamedCount = 0;
		for (int i = 0; i < list.Count; i++)
		{
			string text = rename(list[i].Text);
			if (text == null || text == list[i].Text)
			{
				continue;
			}
			array[i] = text;
			renamedCount++;
			int num2 = Math.Abs(list[i].Length);
			int num3 = text.Length + 1;
			if (num3 > num2)
			{
				if (list[i].Wide)
				{
					throw new UpkException("A wide-character name would need to grow, which isn't supported.");
				}
				num += num3 - num2;
			}
		}
		string[] array2 = new string[list.Count];
		trimmedCount = 0;
		if (num > 0)
		{
			bool[] array3 = UnreferencedNames(img, upkSummary, list.Count);
			List<(int, int)> list2 = new List<(int, int)>();
			for (int j = 0; j < list.Count; j++)
			{
				NameEntry n = list[j];
				if (array3[j] && array[j] == null && !n.Wide && n.Text.Length >= 8 && !ProtectedParts.Any((string x) => n.Text.Contains(x, StringComparison.OrdinalIgnoreCase)))
				{
					int num4 = Math.Abs(n.Length) - 1 - 5;
					if (num4 > 0)
					{
						list2.Add((j, num4));
					}
				}
			}
			int num5 = 0;
			foreach (var (num6, val) in list2.OrderByDescending(((int Index, int Spare) c) => c.Spare))
			{
				if (num5 >= num)
				{
					break;
				}
				int num7 = Math.Min(val, num - num5);
				array2[num6] = list[num6].Text.Substring(0, list[num6].Text.Length - num7);
				num5 += num7;
				trimmedCount++;
			}
			if (num5 < num)
			{
				throw new UpkException($"The new names are {num - num5} bytes too long to fit without changing the package's layout.");
			}
		}
		using MemoryStream memoryStream = new MemoryStream();
		for (int num8 = 0; num8 < list.Count; num8++)
		{
			NameEntry nameEntry = list[num8];
			if (array[num8] != null)
			{
				int num9 = Math.Max(Math.Abs(nameEntry.Length), array[num8].Length + 1);
				WriteEntry(memoryStream, array[num8], nameEntry.Flags, nameEntry.Wide, nameEntry.Wide ? (-num9) : num9);
			}
			else if (array2[num8] != null)
			{
				WriteEntry(memoryStream, array2[num8], nameEntry.Flags, wide: false, array2[num8].Length + 1);
			}
			else
			{
				memoryStream.Write(img, nameEntry.Start, nameEntry.TotalBytes);
			}
		}
		byte[] array4 = memoryStream.ToArray();
		if (array4.Length != upkSummary.ImportOffset - upkSummary.NameOffset)
		{
			throw new UpkException("Internal size mismatch while renaming.");
		}
		byte[] array5 = (byte[])img.Clone();
		Array.Copy(array4, 0, array5, upkSummary.NameOffset, array4.Length);
		return array5;
	}

	public static bool[] UnreferencedNames(byte[] img, UpkSummary s, int count)
	{
		bool[] unref = new bool[count];
		Array.Fill(unref, value: true);
		for (int i = 0; i < s.ImportCount; i++)
		{
			int num = s.ImportOffset + i * 28;
			Mark(num);
			Mark(num + 8);
			Mark(num + 20);
		}
		int[] array = ExportStarts(img, s.ExportOffset, s.DependsOffset, s.ExportCount);
		if (array == null)
		{
			Array.Fill(unref, value: false);
			return unref;
		}
		int[] array2 = array;
		for (int j = 0; j < array2.Length; j++)
		{
			Mark(array2[j] + 12);
		}
		for (int k = Math.Max(0, s.DependsOffset); k + 8 <= img.Length; k++)
		{
			int num2 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(k, 4));
			if (num2 >= 0 && num2 < count && unref[num2])
			{
				int num3 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(k + 4, 4));
				if (num3 >= 0 && num3 <= 16)
				{
					unref[num2] = false;
				}
			}
		}
		return unref;
		void Mark(int at)
		{
			if (at >= 0 && at + 4 <= img.Length)
			{
				int num4 = BinaryPrimitives.ReadInt32LittleEndian(img.AsSpan(at, 4));
				if (num4 >= 0 && num4 < count)
				{
					unref[num4] = false;
				}
			}
		}
	}

	private static void WriteEntry(MemoryStream ms, string text, ulong flags, bool wide, int len)
	{
		Span<byte> span = stackalloc byte[4];
		Span<byte> span2 = stackalloc byte[8];
		if (len == 0 && text.Length == 0)
		{
			BinaryPrimitives.WriteInt32LittleEndian(span, 0);
			ms.Write(span);
		}
		else
		{
			int num = Math.Abs(len);
			BinaryPrimitives.WriteInt32LittleEndian(span, wide ? (-num) : num);
			ms.Write(span);
			byte[] array = new byte[wide ? (num * 2) : num];
			byte[] array2 = (wide ? Encoding.Unicode.GetBytes(text) : Encoding.Latin1.GetBytes(text));
			Array.Copy(array2, 0, array, 0, Math.Min(array2.Length, array.Length));
			ms.Write(array);
		}
		BinaryPrimitives.WriteUInt64LittleEndian(span2, flags);
		ms.Write(span2);
	}
}
