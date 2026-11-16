using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Drip.Core.Stellar;

/// <summary>
/// Minimal editor for cooked UE 4.24 style packages (.uasset header + .uexp body) as shipped in Fortnite 12.41.
/// It can rename the asset a definition refers to as itself, fixing every header offset that moves.
/// </summary>
public static class LegacyPackage
{
	private const uint PackageTag = 0x9E2A83C1;
	private const int ExportRecordSize = 104;

	public sealed record Summary(
		int TotalHeaderSizePos, int NameCount, int NameOffset,
		int ExportCount, int ExportOffset, int ImportCount, int ImportOffset, int DependsOffset,
		int[] OffsetFieldPositions, int BulkStartPos, int SummaryEnd);

	/// <summary>Reads the names in a .uasset (used to describe what a definition points at).</summary>
	public static List<string> ReadNames(byte[] uasset)
	{
		Summary s = ParseSummary(uasset);
		return ReadNameTable(uasset, s, out _).Select(n => n.Name).ToList();
	}

	/// <summary>
	/// Returns a copy of <paramref name="uasset"/> in which every name that mentions asset <paramref name="from"/>
	/// now mentions <paramref name="to"/>, with all moved offsets patched. The matching .uexp doesn't change.
	/// </summary>
	public static byte[] RenameAsset(byte[] uasset, string from, string to)
	{
		Summary s = ParseSummary(uasset);
		List<(string Name, ushort H1, ushort H2)> names = ReadNameTable(uasset, s, out int namesEnd);
		Regex rx = new Regex("(?<![A-Za-z0-9_])" + Regex.Escape(from) + "(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
		int renamed = 0;
		for (int i = 0; i < names.Count; i++)
		{
			string n = names[i].Name;
			string r = rx.Replace(n, to);
			if (!ReferenceEquals(r, n) && r != n)
			{
				names[i] = (r, names[i].H1, names[i].H2);
				renamed++;
			}
		}
		if (renamed == 0)
		{
			throw new InvalidDataException("Couldn't find " + from + " inside its own package, so it can't be renamed to " + to + ".");
		}
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var (name, _, _) in names)
		{
			if (!seen.Add(name))
			{
				throw new InvalidDataException("Renaming " + from + " to " + to + " would create a duplicate name (" + name + ").");
			}
		}
		byte[] newNames = WriteNameTable(names);
		int delta = newNames.Length - (namesEnd - s.NameOffset);
		byte[] result = new byte[uasset.Length + delta];
		Array.Copy(uasset, 0, result, 0, s.NameOffset);
		Array.Copy(newNames, 0, result, s.NameOffset, newNames.Length);
		Array.Copy(uasset, namesEnd, result, s.NameOffset + newNames.Length, uasset.Length - namesEnd);

		// Header offsets (all measured from the start of the combined .uasset + .uexp).
		Add32(result, s.TotalHeaderSizePos, delta, always: true);
		foreach (int pos in s.OffsetFieldPositions)
		{
			Add32(result, pos, delta, always: false);
		}
		long bulk = BinaryPrimitives.ReadInt64LittleEndian(result.AsSpan(s.BulkStartPos));
		BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(s.BulkStartPos), bulk + delta);

		// Export serial offsets.
		int exportTable = s.ExportOffset + delta;
		for (int i = 0; i < s.ExportCount; i++)
		{
			int rec = exportTable + i * ExportRecordSize;
			long serial = BinaryPrimitives.ReadInt64LittleEndian(result.AsSpan(rec + 36));
			BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(rec + 36), serial + delta);
		}
		return result;
	}

	private static void Add32(byte[] data, int pos, int delta, bool always)
	{
		int v = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(pos));
		if (v != 0 || always)
		{
			BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(pos), v + delta);
		}
	}

	public static Summary ParseSummary(byte[] d)
	{
		int p = 0;
		int I32() { int v = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p)); p += 4; return v; }
		uint U32() { uint v = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(p)); p += 4; return v; }
		string FStr()
		{
			int n = I32();
			if (n == 0) return "";
			if (n < 0) { string s = Encoding.Unicode.GetString(d, p, (-n - 1) * 2); p += -n * 2; return s; }
			string a = Encoding.Latin1.GetString(d, p, n - 1); p += n; return a;
		}
		if (d.Length < 64 || U32() != PackageTag)
		{
			throw new InvalidDataException("Not a cooked package (missing tag).");
		}
		int legacy = I32();
		if (legacy != -7)
		{
			throw new NotSupportedException("Package legacy version " + legacy + " isn't the 4.24 layout.");
		}
		I32();
		int ue4 = I32();
		I32();
		int cv = I32();
		p += cv * 20;
		int totalPos = p;
		int total = I32();
		if (total != d.Length)
		{
			throw new InvalidDataException("Package header size doesn't match the file (" + total + " vs " + d.Length + ").");
		}
		FStr();
		uint flags = U32();
		int nameCount = I32();
		int nameOffset = I32();
		if ((flags & 0x80000000u) == 0)
		{
			FStr();
		}
		List<int> offs = new List<int>();
		I32(); // gatherable text count
		offs.Add(p); I32();
		int exportCount = I32();
		int exportOffPos = p; int exportOffset = I32();
		int importCount = I32();
		int importOffPos = p; int importOffset = I32();
		int dependsPos = p; int dependsOffset = I32();
		I32(); // soft package reference count
		offs.Add(p); I32();
		offs.Add(p); I32(); // searchable names
		offs.Add(p); I32(); // thumbnail table
		offs.Add(exportOffPos);
		offs.Add(importOffPos);
		offs.Add(dependsPos);
		p += 16; // guid
		if ((flags & 0x80000000u) == 0)
		{
			p += 16;
		}
		int gens = I32();
		p += gens * 8;
		p += 10; FStr(); // saved by engine version
		p += 10; FStr(); // compatible with
		U32(); // compression flags
		int chunks = I32();
		if (chunks != 0)
		{
			throw new NotSupportedException("Compressed package chunks aren't supported.");
		}
		U32(); // package source
		int addl = I32();
		for (int i = 0; i < addl; i++) FStr();
		offs.Add(p); I32(); // asset registry data offset
		int bulkPos = p; p += 8;
		offs.Add(p); I32(); // world tile info
		int chunkIds = I32();
		p += chunkIds * 4;
		I32(); // preload dependency count
		offs.Add(p); I32(); // preload dependency offset
		if (p != nameOffset)
		{
			throw new InvalidDataException("Summary ended at " + p + " but the name table starts at " + nameOffset + "; this package layout isn't the one this swapper knows.");
		}
		if (exportCount > 0 && (dependsOffset - exportOffset) != exportCount * ExportRecordSize)
		{
			throw new NotSupportedException("Export table isn't 104 bytes per entry; layout unknown.");
		}
		_ = ue4;
		return new Summary(totalPos, nameCount, nameOffset, exportCount, exportOffset, importCount, importOffset, dependsOffset, offs.ToArray(), bulkPos, p);
	}

	private static List<(string Name, ushort H1, ushort H2)> ReadNameTable(byte[] d, Summary s, out int end)
	{
		int p = s.NameOffset;
		List<(string, ushort, ushort)> list = new List<(string, ushort, ushort)>(s.NameCount);
		for (int i = 0; i < s.NameCount; i++)
		{
			int n = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p));
			p += 4;
			string name;
			if (n < 0)
			{
				name = Encoding.Unicode.GetString(d, p, (-n - 1) * 2);
				p += -n * 2;
			}
			else
			{
				name = Encoding.Latin1.GetString(d, p, Math.Max(0, n - 1));
				p += n;
			}
			ushort h1 = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(p));
			ushort h2 = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(p + 2));
			p += 4;
			list.Add((name, h1, h2));
		}
		end = p;
		if (p != s.ImportOffset)
		{
			throw new InvalidDataException("Name table doesn't end where the import table starts.");
		}
		return list;
	}

	private static byte[] WriteNameTable(List<(string Name, ushort H1, ushort H2)> names)
	{
		using MemoryStream ms = new MemoryStream();
		foreach (var (name, h1, h2) in names)
		{
			bool wide = name.Any(c => c > 255);
			Span<byte> b4 = stackalloc byte[4];
			if (wide)
			{
				BinaryPrimitives.WriteInt32LittleEndian(b4, -(name.Length + 1));
				ms.Write(b4);
				ms.Write(Encoding.Unicode.GetBytes(name));
				ms.WriteByte(0);
				ms.WriteByte(0);
			}
			else
			{
				BinaryPrimitives.WriteInt32LittleEndian(b4, name.Length + 1);
				ms.Write(b4);
				ms.Write(Encoding.Latin1.GetBytes(name));
				ms.WriteByte(0);
			}
			BinaryPrimitives.WriteUInt16LittleEndian(b4, h1);
			ms.Write(b4[..2]);
			BinaryPrimitives.WriteUInt16LittleEndian(b4, h2);
			ms.Write(b4[..2]);
		}
		return ms.ToArray();
	}
}
