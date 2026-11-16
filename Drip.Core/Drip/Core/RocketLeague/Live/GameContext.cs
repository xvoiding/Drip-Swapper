using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Drip.Core.RocketLeague.Live;

public sealed class GameContext : IDisposable
{
	private readonly ulong _nameData;

	private readonly int _nameCount;

	private readonly int _nameTextAt;

	private readonly bool _wide;

	private readonly int _nameField;

	private readonly int _outerOff;

	private readonly int _classOff;

	private ulong[] _ptrs;

	private readonly Dictionary<ulong, int> _index = new Dictionary<ulong, int>();

	private byte[]?[] _recs;

	private readonly ulong _objAt;

	private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

	public ProcessMemory Mem { get; }

	public int Count => _ptrs.Length;

	public ulong AddressOf(int i)
	{
		return _ptrs[i];
	}

	public bool IsObject(ulong ptr, out int index)
	{
		return _index.TryGetValue(ptr, out index);
	}

	private GameContext(ProcessMemory mem, ulong nameData, int nameCount, int textAt, bool wide, int nameField, ulong objAt, ulong[] ptrs, byte[]?[] recs)
	{
		_objAt = objAt;
		Mem = mem;
		_nameData = nameData;
		_nameCount = nameCount;
		_nameTextAt = textAt;
		_wide = wide;
		_nameField = nameField;
		_outerOff = nameField - 8;
		_classOff = nameField + 8;
		_ptrs = ptrs;
		_recs = recs;
		for (int i = 0; i < ptrs.Length; i++)
		{
			if (GameScan.LooksLikePointer(ptrs[i]))
			{
				_index[ptrs[i]] = i;
			}
		}
	}

	private string? NameAt(int index)
	{
		if (index < 0 || index >= _nameCount)
		{
			return null;
		}
		ulong? num = Mem.ReadU64(_nameData + (ulong)((long)index * 8L));
		if (!num.HasValue || !GameScan.LooksLikePointer(num.Value))
		{
			return null;
		}
		byte[] array = Mem.Read(num.Value + (ulong)_nameTextAt, _wide ? 160 : 80);
		if (array == null)
		{
			return null;
		}
		if (_wide)
		{
			int i;
			for (i = 0; i + 1 < array.Length && (array[i] != 0 || array[i + 1] != 0); i += 2)
			{
			}
			return Encoding.Unicode.GetString(array, 0, i);
		}
		int num2 = Array.IndexOf(array, (byte)0);
		return Encoding.ASCII.GetString(array, 0, (num2 < 0) ? array.Length : num2);
	}

	public string Nm(int i)
	{
		byte[] array = _recs[i];
		if (array == null)
		{
			return "?";
		}
		int num = BitConverter.ToInt32(array, _nameField);
		if (!_names.TryGetValue(num, out string value))
		{
			Dictionary<int, string> names = _names;
			string? obj = NameAt(num) ?? "?";
			value = obj;
			names[num] = obj;
		}
		return value;
	}

	public int Outer(int i)
	{
		byte[] array = _recs[i];
		if (array == null || !_index.TryGetValue(BitConverter.ToUInt64(array, _outerOff), out var value))
		{
			return -1;
		}
		return value;
	}

	public int ClassOf(int i)
	{
		byte[] array = _recs[i];
		if (array == null || !_index.TryGetValue(BitConverter.ToUInt64(array, _classOff), out var value))
		{
			return -1;
		}
		return value;
	}

	public string Cls(int i)
	{
		int num = ClassOf(i);
		if (num >= 0)
		{
			return Nm(num);
		}
		return "?";
	}

	public bool Readable(int i)
	{
		return _recs[i] != null;
	}

	public string Root(int i)
	{
		int num = 0;
		while (num++ < 12)
		{
			int num2 = Outer(i);
			if (num2 < 0)
			{
				break;
			}
			i = num2;
		}
		return Nm(i);
	}

	public string PathOf(int i)
	{
		List<string> list = new List<string>();
		int num = 0;
		while (i >= 0 && num++ < 8)
		{
			list.Add(Nm(i));
			i = Outer(i);
		}
		list.Reverse();
		return string.Join(".", list);
	}

	public static GameContext? Attach(bool write, out string error)
	{
		error = "";
		if (RlLocator.IsAntiCheatRunning())
		{
			error = "Anti-cheat is running, so the app will not touch the game.";
			return null;
		}
		Process process = Process.GetProcessesByName("RocketLeague").FirstOrDefault();
		if (process == null)
		{
			error = "Rocket League isn't running.";
			return null;
		}
		ulong modBase;
		ulong modSize;
		ProcessMemory mem;
		try
		{
			modBase = (ulong)process.MainModule.BaseAddress;
			modSize = (ulong)process.MainModule.ModuleMemorySize;
			mem = ProcessMemory.Open(process, write);
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return null;
		}
		List<(ulong, ulong, int, int)> list = new List<(ulong, ulong, int, int)>();
		foreach (var item3 in mem.WritableRegions(modBase, modBase + modSize))
		{
			ulong item = item3.Base;
			ulong item2 = item3.Size;
			for (ulong num = 0uL; num < item2; num += 1048576)
			{
				int num2 = (int)Math.Min(1048576uL, item2 - num);
				byte[] array = mem.Read(item + num, num2);
				if (array == null)
				{
					continue;
				}
				for (int i = 0; i + 16 <= num2; i += 8)
				{
					ulong num3 = BitConverter.ToUInt64(array, i);
					if (GameScan.LooksLikePointer(num3))
					{
						int num4 = BitConverter.ToInt32(array, i + 8);
						int num5 = BitConverter.ToInt32(array, i + 12);
						if (num4 >= 20000 && num4 <= 3000000 && num5 >= num4 && num5 <= (long)num4 * 4L + 4096)
						{
							list.Add((item + num + (ulong)i, num3, num4, num5));
						}
					}
				}
			}
		}
		(ulong, ulong, int, int)? tuple = null;
		int textAt = -1;
		bool wide = false;
		foreach (var item4 in list)
		{
			ulong? num6 = mem.ReadU64(item4.Item2);
			if (num6.HasValue && GameScan.LooksLikePointer(num6.Value))
			{
				byte[] array2 = mem.Read(num6.Value, 64);
				if (array2 != null && GameScan.ContainsWord(array2, "None"))
				{
					int num7 = ((ReadOnlySpan<byte>)array2.AsSpan()).IndexOf((ReadOnlySpan<byte>)Encoding.ASCII.GetBytes("None\0"));
					wide = num7 < 0;
					textAt = ((num7 >= 0) ? num7 : ((ReadOnlySpan<byte>)array2.AsSpan()).IndexOf((ReadOnlySpan<byte>)Encoding.Unicode.GetBytes("None\0")));
					tuple = item4;
					break;
				}
			}
		}
		if (!tuple.HasValue)
		{
			mem.Dispose();
			error = "The game's name table was not found.";
			return null;
		}
		(ulong At, ulong Data, int Count, int Max) nm = tuple.Value;
		(ulong, ulong, int, int)? tuple2 = null;
		int num8 = -1;
		foreach (var item5 in list.Where(((ulong At, ulong Data, int Count, int Max) c) => c.At != nm.At))
		{
			byte[] table = mem.Read(item5.Item2, 512);
			if (table == null)
			{
				continue;
			}
			List<ulong> list2 = (from num15 in Enumerable.Range(0, 64)
				select BitConverter.ToUInt64(table, num15 * 8)).Where(GameScan.LooksLikePointer).Take(40).ToList();
			if (list2.Count < 20)
			{
				continue;
			}
			List<byte[]> list3 = (from p in list2
				select mem.Read(p, 128) into b
				where b != null
				select (b)).ToList();
			if (list3.Count < 20 || Enumerable.Count(list3, (byte[] b) =>
			{
				ulong num15 = BitConverter.ToUInt64(b, 0);
				return num15 >= modBase && num15 < modBase + modSize;
			}) < list3.Count * 9 / 10)
			{
				continue;
			}
			for (int num9 = 8; num9 <= 112; num9 += 4)
			{
				HashSet<string> hashSet = new HashSet<string>();
				int num10 = 0;
				foreach (byte[] item6 in list3)
				{
					string text = Name(BitConverter.ToInt32(item6, num9));
					if (text != null && GameScan.LooksLikeIdentifier(text))
					{
						num10++;
						hashSet.Add(text);
					}
				}
				if (num10 >= list3.Count * 9 / 10 && hashSet.Count >= list3.Count / 3)
				{
					num8 = num9;
					break;
				}
			}
			if (num8 >= 0)
			{
				tuple2 = item5;
				break;
			}
		}
		if (!tuple2.HasValue)
		{
			mem.Dispose();
			error = "The game's object table was not found.";
			return null;
		}
		(ulong, ulong, int, int) value = tuple2.Value;
		ulong[] array3 = new ulong[value.Item3];
		for (int num11 = 0; num11 < value.Item3; num11 += 100000)
		{
			int num12 = Math.Min(100000, value.Item3 - num11);
			byte[] array4 = mem.Read(value.Item2 + (ulong)((long)num11 * 8L), num12 * 8);
			if (array4 != null)
			{
				for (int num13 = 0; num13 < num12; num13++)
				{
					array3[num11 + num13] = BitConverter.ToUInt64(array4, num13 * 8);
				}
			}
		}
		byte[][] array5 = new byte[value.Item3][];
		for (int num14 = 0; num14 < value.Item3; num14++)
		{
			if (GameScan.LooksLikePointer(array3[num14]))
			{
				array5[num14] = mem.Read(array3[num14], 96);
			}
		}
		return new GameContext(mem, nm.Data, nm.Count, textAt, wide, num8, value.Item1, array3, array5);
		string? Name(int index)
		{
			if (index < 0 || index >= nm.Count)
			{
				return null;
			}
			ulong? num15 = mem.ReadU64(nm.Data + (ulong)((long)index * 8L));
			if (!num15.HasValue || !GameScan.LooksLikePointer(num15.Value))
			{
				return null;
			}
			byte[] array6 = mem.Read(num15.Value + (ulong)textAt, wide ? 160 : 80);
			if (array6 == null)
			{
				return null;
			}
			if (wide)
			{
				int j;
				for (j = 0; j + 1 < array6.Length && (array6[j] != 0 || array6[j + 1] != 0); j += 2)
				{
				}
				return Encoding.Unicode.GetString(array6, 0, j);
			}
			int num16 = Array.IndexOf(array6, (byte)0);
			return Encoding.ASCII.GetString(array6, 0, (num16 < 0) ? array6.Length : num16);
		}
	}

	public List<int> Refresh()
	{
		List<int> list = new List<int>();
		byte[] array = Mem.Read(_objAt, 16);
		if (array == null)
		{
			return list;
		}
		ulong num = BitConverter.ToUInt64(array, 0);
		int num2 = BitConverter.ToInt32(array, 8);
		if (!GameScan.LooksLikePointer(num) || num2 < _ptrs.Length * 9 / 10 || num2 > 6000000)
		{
			return list;
		}
		if (num2 != _ptrs.Length)
		{
			Array.Resize(ref _ptrs, num2);
			Array.Resize(ref _recs, num2);
		}
		for (int i = 0; i < num2; i += 100000)
		{
			int num3 = Math.Min(100000, num2 - i);
			byte[] array2 = Mem.Read(num + (ulong)((long)i * 8L), num3 * 8);
			if (array2 == null)
			{
				continue;
			}
			for (int j = 0; j < num3; j++)
			{
				ulong num4 = BitConverter.ToUInt64(array2, j * 8);
				int num5 = i + j;
				if (num4 != _ptrs[num5])
				{
					if (GameScan.LooksLikePointer(_ptrs[num5]) && _index.TryGetValue(_ptrs[num5], out var value) && value == num5)
					{
						_index.Remove(_ptrs[num5]);
					}
					_ptrs[num5] = num4;
					if (GameScan.LooksLikePointer(num4))
					{
						_index[num4] = num5;
						_recs[num5] = Mem.Read(num4, 96);
					}
					else
					{
						_recs[num5] = null;
					}
					list.Add(num5);
				}
			}
		}
		return list;
	}

	public void Dispose()
	{
		Mem.Dispose();
	}
}
