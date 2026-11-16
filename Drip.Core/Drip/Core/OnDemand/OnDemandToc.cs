using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Drip.Core.OnDemand;

public sealed class OnDemandToc
{
	public sealed class IdRun
	{
		public int Offset;

		public int Count;

		public int Stride;

		public long HashBase = -1L;

		public int LatticeHits;

		public int Slack = -1;

		public int LatticeFirst;

		public string Confidence = "none";

		public string Note = "";

		public bool Usable
		{
			get
			{
				bool flag = HashBase >= 0;
				if (flag)
				{
					string confidence = Confidence;
					bool flag2 = ((confidence == "paired" || confidence == "anchored") ? true : false);
					flag = flag2;
				}
				return flag;
			}
		}
	}

	private sealed record Chain(int Stride, int First, int Last, int Count);

	private const int MinRun = 2000;

	private readonly byte[] _d;

	private CasIndex _cas = new CasIndex();

	public string FilePath { get; }

	public List<IdRun> Runs { get; private set; } = new List<IdRun>();

	public int TotalHits { get; private set; }

	public string Notes { get; private set; } = "";

	private OnDemandToc(string path, byte[] data)
	{
		FilePath = path;
		_d = data;
	}

	public void SetCas(CasIndex cas)
	{
		_cas = cas;
	}

	public static OnDemandToc Load(string path, CasIndex cas, Action<string>? log = null)
	{
		byte[] array;
		using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			array = new byte[fileStream.Length];
			fileStream.ReadExactly(array);
		}
		return FromBytes(path, array, cas, log);
	}

	internal static OnDemandToc FromBytes(string path, byte[] data, CasIndex cas, Action<string>? log = null)
	{
		OnDemandToc onDemandToc = new OnDemandToc(path, data)
		{
			_cas = cas
		};
		onDemandToc.Runs = onDemandToc.FindIdRuns();
		log?.Invoke($"  {Path.GetFileName(path)}: {onDemandToc.Runs.Count} id table(s) [{string.Join(", ", onDemandToc.Runs.Select((IdRun r) => r.Count))}]");
		if (onDemandToc.Runs.Count > 0 && cas.Present && cas.Count > 0)
		{
			onDemandToc.LocateEntries();
		}
		return onDemandToc;
	}

	private List<IdRun> FindIdRuns()
	{
		byte[] d = _d;
		List<IdRun> list = new List<IdRun>();
		int num = d.Length;
		for (int i = 0; i < 12; i++)
		{
			int offset = -1;
			int num2 = 0;
			int start = -1;
			for (int j = i; j + 12 <= num; j += 12)
			{
				byte b = d[j + 11];
				bool flag = b >= 1 && b <= 15 && d[j + 8] == 0 && d[j + 9] == 0 && d[j + 10] == 0;
				if (flag && (num2 == 0 || ((ReadOnlySpan<byte>)d.AsSpan(start, 8)).SequenceCompareTo((ReadOnlySpan<byte>)d.AsSpan(j, 8)) <= 0))
				{
					if (num2 == 0)
					{
						offset = j;
					}
					num2++;
					start = j;
					continue;
				}
				if (num2 >= 2000)
				{
					list.Add(new IdRun
					{
						Offset = offset,
						Count = num2
					});
				}
				if (flag)
				{
					offset = j;
					num2 = 1;
					start = j;
				}
				else
				{
					num2 = 0;
					offset = -1;
				}
			}
			if (num2 >= 2000)
			{
				list.Add(new IdRun
				{
					Offset = offset,
					Count = num2
				});
			}
		}
		list.Sort((IdRun a, IdRun idRun) => idRun.Count.CompareTo(a.Count));
		List<IdRun> list2 = new List<IdRun>();
		foreach (IdRun run in list)
		{
			if (!list2.Any((IdRun k) => run.Offset < k.Offset + 12L * (long)k.Count && k.Offset < run.Offset + 12L * (long)run.Count))
			{
				list2.Add(run);
			}
		}
		list2.Sort((IdRun a, IdRun idRun) => a.Offset.CompareTo(idRun.Offset));
		return list2;
	}

	private void LocateEntries()
	{
		byte[] d = _d;
		ulong[] array = new ulong[2097152];
		foreach (ulong prefix in _cas.Prefixes)
		{
			uint num = Mix(prefix);
			array[num >> 6] |= (ulong)(1L << (int)num);
		}
		List<int> list = new List<int>(131072);
		for (int i = 0; i + 8 <= d.Length; i++)
		{
			ulong num2 = Unsafe.ReadUnaligned<ulong>(in d[i]);
			uint num3 = Mix(num2);
			if ((array[num3 >> 6] & (ulong)(1L << (int)num3)) != 0L && _cas.Contains(num2))
			{
				list.Add(i);
			}
		}
		TotalHits = list.Count;
		if (list.Count < 100)
		{
			Notes = $"only {list.Count} installed-chunk hashes found in this table";
			return;
		}
		List<Chain> list2 = new List<Chain>();
		for (int j = 16; j <= 256; j++)
		{
			int first = list[0];
			int last = list[0];
			int num4 = 1;
			for (int k = 1; k < list.Count; k++)
			{
				int num5 = list[k] - list[k - 1];
				if (num5 % j == 0 && num5 <= 200 * j)
				{
					last = list[k];
					num4++;
					continue;
				}
				if (num4 >= 200)
				{
					list2.Add(new Chain(j, first, last, num4));
				}
				first = (last = list[k]);
				num4 = 1;
			}
			if (num4 >= 200)
			{
				list2.Add(new Chain(j, first, last, num4));
			}
		}
		foreach (IdRun run in Runs)
		{
			Chain chain = null;
			foreach (Chain item in list2)
			{
				int num6 = (item.Last - item.First) / item.Stride + 1;
				if (num6 <= run.Count && num6 >= run.Count - 150 && ((object)chain == null || item.Count > chain.Count))
				{
					chain = item;
				}
			}
			if ((object)chain == null)
			{
				run.Note = "no entry table with a matching length found";
				continue;
			}
			run.Stride = chain.Stride;
			run.LatticeHits = chain.Count;
			int num7 = (chain.Last - chain.First) / chain.Stride + 1;
			run.Slack = run.Count - num7;
			run.LatticeFirst = chain.First;
			List<int> list3 = new List<int>();
			for (int l = 0; l + 1 < run.Count; l++)
			{
				if (list3.Count >= 40000)
				{
					break;
				}
				int num8 = run.Offset + 12 * l;
				if (d[num8 + 11] <= 3 && d[num8 + 23] <= 3 && ((ReadOnlySpan<byte>)d.AsSpan(num8, 8)).SequenceEqual((ReadOnlySpan<byte>)d.AsSpan(num8 + 12, 8)))
				{
					list3.Add(l);
				}
			}
			double[] array2 = new double[run.Slack + 1];
			for (int m = 0; m <= run.Slack; m++)
			{
				long num9 = chain.First - (long)m * (long)chain.Stride;
				int num10 = 0;
				int num11 = 0;
				foreach (int item2 in list3)
				{
					long num12 = num9 + (long)chain.Stride * (long)item2;
					long num13 = num12 + chain.Stride;
					if (num12 >= 0 && num13 + 8 <= d.Length)
					{
						bool flag = _cas.Contains(BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan((int)num12)));
						bool flag2 = _cas.Contains(BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan((int)num13)));
						if (flag & flag2)
						{
							num10++;
						}
						else if (flag != flag2)
						{
							num11++;
						}
					}
				}
				array2[m] = ((num10 + num11 >= 30) ? ((double)num11 / (double)(num10 + num11)) : double.NaN);
			}
			run.Note = $"pairs {list3.Count}; mismatch by start offset: [{string.Join(" ", array2.Select((double r) => (!double.IsNaN(r)) ? r.ToString("0.00") : "n/a"))}]";
			int num14 = -1;
			for (int num15 = 0; num15 < array2.Length; num15++)
			{
				if (!double.IsNaN(array2[num15]) && (num14 < 0 || array2[num15] < array2[num14]))
				{
					num14 = num15;
				}
			}
			double num16 = double.MaxValue;
			for (int num17 = 0; num17 < array2.Length; num17++)
			{
				if (num17 != num14 && !double.IsNaN(array2[num17]))
				{
					num16 = Math.Min(num16, array2[num17]);
				}
			}
			if (num14 >= 0 && array2[num14] <= 0.35 && (num16 == double.MaxValue || num16 - array2[num14] >= 0.3))
			{
				run.HashBase = chain.First - (long)num14 * (long)chain.Stride;
				run.Confidence = "paired";
				continue;
			}
			HashSet<int> hashSet = new HashSet<int>();
			for (int num18 = 0; num18 <= run.Slack; num18++)
			{
				long num19 = chain.First - (long)num18 * (long)chain.Stride;
				for (int num20 = 0; num20 <= chain.Stride - 8; num20 += 4)
				{
					long num21 = num19 - num20;
					if (num21 - 4 >= 0 && BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan((int)num21 - 4)) == (uint)run.Count)
					{
						hashSet.Add(num18);
					}
					else if (num21 - 8 >= 0 && BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan((int)num21 - 8)) == (ulong)run.Count)
					{
						hashSet.Add(num18);
					}
				}
			}
			if (hashSet.Count == 1)
			{
				run.HashBase = chain.First - (long)hashSet.First() * (long)chain.Stride;
				run.Confidence = "anchored";
				continue;
			}
			run.Confidence = "ambiguous";
			run.Note += $" · table start uncertain by {run.Slack} entries ({hashSet.Count} count anchors)";
		}
	}

	private static uint Mix(ulong v)
	{
		return (uint)((ulong)((long)v * -7046029254386353131L) >> 37);
	}

	public ChunkState CheckPackage(ulong chunkId)
	{
		ChunkState chunkState = ChunkState.NotInIndex;
		Span<byte> span = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(span, chunkId);
		foreach (IdRun run in Runs)
		{
			int num = 0;
			int num2 = run.Count;
			while (num < num2)
			{
				int num3 = num + num2 >> 1;
				if (((ReadOnlySpan<byte>)_d.AsSpan(run.Offset + 12 * num3, 8)).SequenceCompareTo((ReadOnlySpan<byte>)span) < 0)
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			for (int i = num; i < run.Count && ((ReadOnlySpan<byte>)_d.AsSpan(run.Offset + 12 * i, 8)).SequenceEqual((ReadOnlySpan<byte>)span); i++)
			{
				if (_d[run.Offset + 12 * i + 11] > 3)
				{
					continue;
				}
				if (!run.Usable)
				{
					if (chunkState == ChunkState.NotInIndex)
					{
						chunkState = ChunkState.Unknown;
					}
					continue;
				}
				long num4 = run.HashBase + (long)run.Stride * (long)i;
				if (num4 < 0 || num4 + 8 > _d.Length)
				{
					chunkState = ChunkState.Unknown;
					continue;
				}
				if (!_cas.Contains(BinaryPrimitives.ReadUInt64LittleEndian(_d.AsSpan((int)num4))))
				{
					return ChunkState.NotInstalled;
				}
				chunkState = ChunkState.Installed;
			}
		}
		return chunkState;
	}

	public string Detail(ulong chunkId)
	{
		List<string> list = new List<string>();
		Span<byte> span = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(span, chunkId);
		foreach (IdRun run in Runs)
		{
			int num = 0;
			int num2 = run.Count;
			while (num < num2)
			{
				int num3 = num + num2 >> 1;
				if (((ReadOnlySpan<byte>)_d.AsSpan(run.Offset + 12 * num3, 8)).SequenceCompareTo((ReadOnlySpan<byte>)span) < 0)
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			for (int i = num; i < run.Count && ((ReadOnlySpan<byte>)_d.AsSpan(run.Offset + 12 * i, 8)).SequenceEqual((ReadOnlySpan<byte>)span); i++)
			{
				byte value = _d[run.Offset + 12 * i + 11];
				if (!run.Usable)
				{
					list.Add($"t{value}:?");
				}
				else
				{
					long num4 = run.HashBase + (long)run.Stride * (long)i;
					bool flag = num4 >= 0 && num4 + 8 <= _d.Length && _cas.Contains(BinaryPrimitives.ReadUInt64LittleEndian(_d.AsSpan((int)num4)));
					list.Add($"t{value}:{(flag ? "installed" : "missing")}");
				}
			}
		}
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return "not listed";
	}

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(33, 3, stringBuilder2);
		handler.AppendFormatted(Path.GetFileName(FilePath));
		handler.AppendLiteral("  (");
		handler.AppendFormatted(_d.Length, "N0");
		handler.AppendLiteral(" bytes)  installed-hash hits: ");
		handler.AppendFormatted(TotalHits, "N0");
		stringBuilder3.AppendLine(ref handler);
		foreach (IdRun run in Runs)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(84, 9, stringBuilder2);
			handler.AppendLiteral("  id table @0x");
			handler.AppendFormatted(run.Offset, "X");
			handler.AppendLiteral(" x");
			handler.AppendFormatted(run.Count, "N0");
			handler.AppendLiteral(" · entries: stride ");
			handler.AppendFormatted(run.Stride);
			handler.AppendLiteral(", first hit 0x");
			handler.AppendFormatted(run.LatticeFirst, "X");
			handler.AppendLiteral(", base 0x");
			handler.AppendFormatted(Math.Max(run.HashBase, 0L), "X");
			handler.AppendLiteral(", lattice hits ");
			handler.AppendFormatted(run.LatticeHits, "N0");
			handler.AppendLiteral(", slack ");
			handler.AppendFormatted(run.Slack);
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(run.Confidence);
			handler.AppendFormatted((run.Note.Length > 0) ? (" · " + run.Note) : "");
			stringBuilder4.AppendLine(ref handler);
		}
		if (Notes.Length > 0)
		{
			stringBuilder.AppendLine("  " + Notes);
		}
		return stringBuilder.ToString();
	}

	public string DumpEntry(IdRun run, int index, int bytes = 48)
	{
		if (!run.Usable)
		{
			return "(table not located)";
		}
		long num = run.HashBase + (long)run.Stride * (long)index;
		if (num < 0 || num + bytes > _d.Length)
		{
			return "(out of range)";
		}
		return Convert.ToHexString(_d.AsSpan((int)num, bytes));
	}
}
