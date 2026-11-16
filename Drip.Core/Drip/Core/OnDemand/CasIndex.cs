using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Drip.Core.OnDemand;

public sealed class CasIndex
{
	private readonly Dictionary<ulong, uint> _loc = new Dictionary<ulong, uint>();

	public bool Present { get; private set; }

	public string Error { get; private set; } = "";

	public int SnapshotBlocks { get; private set; }

	public int SnapshotLocations { get; private set; }

	public int JournalRecords { get; private set; }

	public int JournalAdds { get; private set; }

	public int JournalBlockDeletes { get; private set; }

	public Dictionary<int, int> JournalOps { get; } = new Dictionary<int, int>();

	public int Count => _loc.Count;

	public IEnumerable<ulong> Prefixes => _loc.Keys;

	public bool Contains(ulong addrPrefix)
	{
		return _loc.ContainsKey(addrPrefix);
	}

	public static string DefaultDirectory()
	{
		InlineArray6<string> buffer = default;
		buffer[0] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		buffer[1] = "FortniteGame";
		buffer[2] = "Saved";
		buffer[3] = "PersistentDownloadDir";
		buffer[4] = "IoStore";
		buffer[5] = "InstallCache";
		return Path.Combine(buffer);
	}

	public static CasIndex Load(string? directory = null)
	{
		CasIndex casIndex = new CasIndex();
		if (directory == null)
		{
			directory = DefaultDirectory();
		}
		try
		{
			string path = Path.Combine(directory, "cas.snp");
			string path2 = Path.Combine(directory, "cas.jrn");
			if (!File.Exists(path) && !File.Exists(path2))
			{
				casIndex.Error = "no cas.snp / cas.jrn found";
				return casIndex;
			}
			if (File.Exists(path))
			{
				casIndex.ReadSnapshot(ReadShared(path));
			}
			if (File.Exists(path2))
			{
				casIndex.ReadJournal(ReadShared(path2));
			}
			casIndex.Present = true;
		}
		catch (Exception ex)
		{
			casIndex.Error = ex.GetType().Name + ": " + ex.Message;
		}
		return casIndex;
	}

	private static byte[] ReadShared(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		byte[] array = new byte[fileStream.Length];
		fileStream.ReadExactly(array);
		return array;
	}

	internal void ReadSnapshot(byte[] d)
	{
		if (d.Length < 48 || !MatchesAscii(d, 0, "+SNAPSHOTHEADER+"))
		{
			throw new InvalidDataException("cas.snp header not recognised");
		}
		int num = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(36));
		long num2 = 40 + 20L * (long)num;
		if (num < 0 || num > 1000000 || num2 + 4 > d.Length)
		{
			throw new InvalidDataException("cas.snp block table doesn't fit");
		}
		uint num3 = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan((int)num2));
		long num4 = num2 + 4;
		if (num4 + 20L * (long)num3 > d.Length)
		{
			throw new InvalidDataException($"cas.snp says {num3} locations but the file is too short");
		}
		SnapshotBlocks = num;
		SnapshotLocations = (int)num3;
		long num5 = 0L;
		long num6 = num4;
		while (num5 < num3)
		{
			ulong key = BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan((int)num6));
			uint value = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan((int)num6 + 12));
			_loc[key] = value;
			num5++;
			num6 += 20;
		}
	}

	internal void ReadJournal(byte[] d)
	{
		if (d.Length < 32 || !MatchesAscii(d, 0, "CASJOURNALHEADER"))
		{
			throw new InvalidDataException("cas.jrn header not recognised");
		}
		for (int i = 32; i + 24 <= d.Length && !MatchesAscii(d, i, "CASJOURNALFOOTER"); i += 24)
		{
			int num = d[i];
			JournalRecords++;
			JournalOps[num] = JournalOps.GetValueOrDefault(num) + 1;
			switch (num)
			{
			case 1:
			{
				uint value = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i + 4));
				ulong key = BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(i + 12));
				_loc[key] = value;
				JournalAdds++;
				break;
			}
			case 3:
			{
				uint dead = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i + 4));
				foreach (ulong item in (from kv in _loc
					where kv.Value == dead
					select kv.Key).ToList())
				{
					_loc.Remove(item);
				}
				JournalBlockDeletes++;
				break;
			}
			}
		}
	}

	private static bool MatchesAscii(byte[] d, int pos, string s)
	{
		if (pos + s.Length > d.Length)
		{
			return false;
		}
		for (int i = 0; i < s.Length; i++)
		{
			if (d[pos + i] != (byte)s[i])
			{
				return false;
			}
		}
		return true;
	}

	internal void AddForTest(ulong addr, uint block)
	{
		_loc[addr] = block;
	}

	internal void MarkPresentForTest()
	{
		Present = true;
	}
}
