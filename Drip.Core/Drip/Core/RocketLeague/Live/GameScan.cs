using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Drip.Core.RocketLeague.Live;

public static class GameScan
{
	private sealed record ArrayCandidate(ulong At, ulong Data, int Count, int Max);

	public static bool LooksLikePointer(ulong v)
	{
		if (v > 65536)
		{
			return v < 140737488355327L;
		}
		return false;
	}

	public static bool ContainsWord(ReadOnlySpan<byte> data, string word)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(word + "\0");
		byte[] bytes2 = Encoding.Unicode.GetBytes(word + "\0");
		if (data.IndexOf(bytes) < 0)
		{
			return data.IndexOf(bytes2) >= 0;
		}
		return true;
	}

	public static bool LooksLikeIdentifier(string s)
	{
		int length = s.Length;
		if (length > 0 && length < 128)
		{
			return s.All((char c) =>
			{
				bool flag = char.IsLetterOrDigit(c);
				if (!flag)
				{
					bool flag2;
					switch (c)
					{
					case ' ':
					case '$':
					case '(':
					case ')':
					case '-':
					case '.':
					case '<':
					case '>':
					case '_':
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					flag = flag2;
				}
				return flag;
			});
		}
		return false;
	}

	public static string Run(Action<string>? progress = null)
	{
		StringBuilder sb = new StringBuilder();
		if (RlLocator.IsAntiCheatRunning())
		{
			return "Anti-cheat is running, so the scan was not started. Close Rocket League and start it without anti-cheat.";
		}
		Process process = Process.GetProcessesByName("RocketLeague").FirstOrDefault();
		if (process == null)
		{
			return "Rocket League isn't running.";
		}
		L($"Rocket League pid {process.Id}");
		ulong modBase;
		ulong modSize;
		try
		{
			modBase = (ulong)process.MainModule.BaseAddress;
			modSize = (ulong)process.MainModule.ModuleMemorySize;
			L($"Module base 0x{modBase:X}, size {modSize / 1048576} MB, version {process.MainModule.FileVersionInfo.FileVersion}");
		}
		catch (Exception ex)
		{
			return "Couldn't read the game's module list: " + ex.Message;
		}
		ProcessMemory mem = ProcessMemory.Open(process);
		ArrayCandidate names;
		int nameEntryOffset;
		bool wide;
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			List<ArrayCandidate> list = new List<ArrayCandidate>();
			int num = 0;
			ulong num2 = 0uL;
			foreach (var item3 in mem.WritableRegions(modBase, modBase + modSize))
			{
				ulong item = item3.Base;
				ulong item2 = item3.Size;
				num++;
				for (ulong num3 = 0uL; num3 < item2; num3 += 1048576)
				{
					int num4 = (int)Math.Min(1048576uL, item2 - num3);
					byte[] array = mem.Read(item + num3, num4);
					if (array == null)
					{
						continue;
					}
					num2 += (ulong)num4;
					for (int i = 0; i + 16 <= num4; i += 8)
					{
						ulong num5 = BitConverter.ToUInt64(array, i);
						if (LooksLikePointer(num5))
						{
							int num6 = BitConverter.ToInt32(array, i + 8);
							int num7 = BitConverter.ToInt32(array, i + 12);
							if (num6 >= 20000 && num6 <= 3000000 && num7 >= num6 && num7 <= (long)num6 * 4L + 4096)
							{
								list.Add(new ArrayCandidate(item + num3 + (ulong)i, num5, num6, num7));
							}
						}
					}
				}
			}
			L($"Looked through {num} data regions ({num2 / 1024} KB): {list.Count} big-array candidates ({stopwatch.ElapsedMilliseconds} ms)");
			names = null;
			nameEntryOffset = -1;
			foreach (ArrayCandidate item4 in list)
			{
				ulong? num8 = mem.ReadU64(item4.Data);
				if (num8.HasValue && LooksLikePointer(num8.Value))
				{
					byte[] array2 = mem.Read(num8.Value, 64);
					if (array2 != null && ContainsWord(array2, "None"))
					{
						names = item4;
						int num9 = ((ReadOnlySpan<byte>)array2.AsSpan()).IndexOf((ReadOnlySpan<byte>)Encoding.ASCII.GetBytes("None\0"));
						nameEntryOffset = ((num9 >= 0) ? num9 : ((ReadOnlySpan<byte>)array2.AsSpan()).IndexOf((ReadOnlySpan<byte>)Encoding.Unicode.GetBytes("None\0")));
						break;
					}
				}
			}
			if (names == null)
			{
				L("Name table: NOT FOUND");
				return sb.ToString();
			}
			wide = false;
			ulong value = mem.ReadU64(names.Data).Value;
			byte[] array3 = mem.Read(value, 64);
			wide = ((ReadOnlySpan<byte>)array3.AsSpan()).IndexOf((ReadOnlySpan<byte>)Encoding.ASCII.GetBytes("None\0")) < 0;
			L($"Name table: struct at 0x{names.At:X}, data 0x{names.Data:X}, {names.Count:N0} names, text at +0x{nameEntryOffset:X} in each entry ({(wide ? "UTF-16" : "ASCII")})");
			L("First names: " + string.Join(", ", from index2 in Enumerable.Range(0, 12)
				select NameAt(index2) ?? "?"));
			ArrayCandidate arrayCandidate = null;
			int num10 = -1;
			foreach (ArrayCandidate item5 in list.Where((ArrayCandidate c) => c.At != names.At))
			{
				byte[] table = mem.Read(item5.Data, 512);
				if (table == null)
				{
					continue;
				}
				List<ulong> list2 = (from num16 in Enumerable.Range(0, 64)
					select BitConverter.ToUInt64(table, num16 * 8)).Where(LooksLikePointer).Take(40).ToList();
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
					ulong num16 = BitConverter.ToUInt64(b, 0);
					return num16 >= modBase && num16 < modBase + modSize;
				}) < list3.Count * 9 / 10)
				{
					continue;
				}
				for (int num11 = 8; num11 <= 112; num11 += 4)
				{
					HashSet<string> hashSet = new HashSet<string>();
					int num12 = 0;
					foreach (byte[] item6 in list3)
					{
						int index = BitConverter.ToInt32(item6, num11);
						string text = NameAt(index);
						if (text != null && LooksLikeIdentifier(text))
						{
							num12++;
							hashSet.Add(text);
						}
					}
					if (num12 >= list3.Count * 9 / 10 && hashSet.Count >= list3.Count / 3)
					{
						num10 = num11;
						break;
					}
				}
				if (num10 >= 0)
				{
					arrayCandidate = item5;
					break;
				}
			}
			if (arrayCandidate == null)
			{
				L("Object table: NOT FOUND");
				return sb.ToString();
			}
			L($"Object table: struct at 0x{arrayCandidate.At:X}, data 0x{arrayCandidate.Data:X}, {arrayCandidate.Count:N0} objects, name index at +0x{num10:X} in each object");
			List<string> list4 = new List<string>();
			for (int num13 = 0; num13 < Math.Min(arrayCandidate.Count, 4000); num13 += 97)
			{
				if (list4.Count >= 30)
				{
					break;
				}
				ulong? num14 = mem.ReadU64(arrayCandidate.Data + (ulong)((long)num13 * 8L));
				if (num14.HasValue && LooksLikePointer(num14.Value))
				{
					int? num15 = mem.ReadI32(num14.Value + (ulong)num10);
					string text2 = ((!num15.HasValue) ? null : NameAt(num15.Value));
					if (text2 != null)
					{
						list4.Add(text2);
					}
				}
			}
			L("Sample objects: " + string.Join(", ", list4));
			try
			{
				FindBalls(mem, NameAt, arrayCandidate.Data, arrayCandidate.Count, num10, L);
			}
			catch (Exception ex2)
			{
				L("Ball search failed: " + ex2.Message);
			}
			L($"Done in {stopwatch.ElapsedMilliseconds} ms. Nothing was written to the game.");
			return sb.ToString();
		}
		finally
		{
			if (mem != null)
			{
				((IDisposable)mem).Dispose();
			}
		}
		void L(string s)
		{
			sb.AppendLine(s);
			progress?.Invoke(s);
		}
		string? NameAt(int num16)
		{
			if (num16 < 0 || num16 >= names.Count)
			{
				return null;
			}
			ulong? num17 = mem.ReadU64(names.Data + (ulong)((long)num16 * 8L));
			if (!num17.HasValue || !LooksLikePointer(num17.Value))
			{
				return null;
			}
			byte[] array4 = mem.Read(num17.Value + (ulong)nameEntryOffset, wide ? 160 : 80);
			if (array4 == null)
			{
				return null;
			}
			if (wide)
			{
				int j;
				for (j = 0; j + 1 < array4.Length && (array4[j] != 0 || array4[j + 1] != 0); j += 2)
				{
				}
				return Encoding.Unicode.GetString(array4, 0, j);
			}
			int num18 = Array.IndexOf(array4, (byte)0);
			return Encoding.ASCII.GetString(array4, 0, (num18 < 0) ? array4.Length : num18);
		}
	}

	private static void FindBalls(ProcessMemory mem, Func<int, string?> nameAt, ulong objData, int objCount, int nameField, Action<string> L)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		ulong[] array = new ulong[objCount];
		for (int i = 0; i < objCount; i += 100000)
		{
			int num = Math.Min(100000, objCount - i);
			byte[] array2 = mem.Read(objData + (ulong)((long)i * 8L), num * 8);
			if (array2 != null)
			{
				for (int j = 0; j < num; j++)
				{
					array[i + j] = BitConverter.ToUInt64(array2, j * 8);
				}
			}
		}
		Dictionary<ulong, int> index = new Dictionary<ulong, int>();
		for (int k = 0; k < objCount; k++)
		{
			if (LooksLikePointer(array[k]))
			{
				index[array[k]] = k;
			}
		}
		byte[][] recs = new byte[objCount][];
		int readable = 0;
		for (int l = 0; l < objCount; l++)
		{
			if (LooksLikePointer(array[l]))
			{
				byte[] array3 = mem.Read(array[l], 96);
				if (array3 != null)
				{
					recs[l] = array3;
					readable++;
				}
			}
		}
		L($"Ball search: {readable:N0} of {objCount:N0} objects readable ({stopwatch.ElapsedMilliseconds} ms)");
		List<(int, int, int)> list = new List<(int, int, int)>();
		for (int m = 8; m <= 88; m += 8)
		{
			if (m == nameField || m + 8 > 96)
			{
				continue;
			}
			int num2 = 0;
			HashSet<ulong> hashSet = new HashSet<ulong>();
			byte[][] array4 = recs;
			foreach (byte[] array5 in array4)
			{
				if (array5 != null)
				{
					ulong num3 = BitConverter.ToUInt64(array5, m);
					if (num3 != 0L && index.ContainsKey(num3))
					{
						num2++;
						hashSet.Add(num3);
					}
				}
			}
			if (num2 >= readable / 25)
			{
				list.Add((m, num2, hashSet.Count));
			}
		}
		L("Pointer fields: " + string.Join("; ", list.Select(((int Off, int Hits, int Distinct) f) => $"+0x{f.Off:X} {f.Hits * 100 / Math.Max(1, readable)}% -> {f.Distinct:N0} distinct")));
		if (list.Count == 0)
		{
			L("Ball search: no object-to-object pointers found.");
			return;
		}
		int outerOff = (list.Any(((int Off, int Hits, int Distinct) f) => f.Off == nameField - 8) ? (nameField - 8) : list.OrderByDescending(((int Off, int Hits, int Distinct) f) => f.Hits).First().Item1);
		int? classCand = (list.Any(((int Off, int Hits, int Distinct) f) => f.Off == nameField + 8) ? new int?(nameField + 8) : ((int?)null));
		L($"Outer pointer taken as +0x{outerOff:X}, class pointer as {((!classCand.HasValue) ? "none" : ("+0x" + classCand.Value.ToString("X")))}");
		Dictionary<int, string> cache = new Dictionary<int, string>();
		List<int> list2 = new List<int>();
		for (int num4 = 0; num4 < objCount; num4++)
		{
			if (recs[num4] != null)
			{
				string text = Nm(num4);
				if (text.Length < 90 && text.Contains("ball", StringComparison.OrdinalIgnoreCase))
				{
					list2.Add(num4);
				}
			}
		}
		List<IGrouping<string, int>> list3 = (from g in list2.GroupBy(Root)
			orderby g.Count() descending
			select g).ToList();
		L($"Objects with \"ball\" in the name: {list2.Count:N0}, in {list3.Count:N0} packages");
		foreach (IGrouping<string, int> item in list3.Take(25))
		{
			L($"  {item.Key}: {item.Count()} - " + string.Join(", ", from i2 in item.Take(5)
				select Cls(i2) + ":" + Nm(i2)));
		}
		string[] keys = new string[9] { "Ball_Default", "MAT_Ball", "Ball_Soccer", "Default00", "BallMesh", "Ball_V3", "BBall", "Basketball", "Hoops" };
		List<int> list4 = list2.Where((int i2) => keys.Any((string value2) => Nm(i2).Contains(value2, StringComparison.OrdinalIgnoreCase))).Take(80).ToList();
		L($"Key objects ({list4.Count}):");
		foreach (int item2 in list4.Take(60))
		{
			L("  " + Cls(item2) + " " + PathOf(item2));
		}
		List<int> list5 = (from num9 in Enumerable.Range(0, objCount)
			where recs[num9] != null && Cls(num9) == "Ball_TA" && !Nm(num9).StartsWith("Default__")
			select num9).Take(30).ToList();
		L($"Ball_TA objects ({list5.Count}):");
		foreach (int item3 in list5)
		{
			L("  " + PathOf(item3));
		}
		HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ball_Default", "Ball_Default_Textures", "Ball_Shared", "Balls_Shared", "Ball_FX", "Archetypes" };
		List<int> list6 = (from num9 in Enumerable.Range(0, objCount)
			where recs[num9] != null && Outer(num9) < 0 && wanted.Contains(Nm(num9)) && Cls(num9) == "Package"
			select num9).ToList();
		L($"Package files ({list6.Count} packages):");
		foreach (int item4 in list6)
		{
			bool flag = false;
			for (int num5 = 8; num5 <= 88; num5 += 8)
			{
				if (flag)
				{
					break;
				}
				if (num5 == nameField || num5 == outerOff || num5 == classCand || !index.TryGetValue(BitConverter.ToUInt64(recs[item4], num5), out var value) || value == item4)
				{
					continue;
				}
				byte[] array6 = mem.Read(array[value], 1024);
				if (array6 == null)
				{
					continue;
				}
				for (int num6 = 0; num6 + 8 <= array6.Length; num6 += 8)
				{
					if (flag)
					{
						break;
					}
					ulong num7 = BitConverter.ToUInt64(array6, num6);
					if (!LooksLikePointer(num7) || index.ContainsKey(num7))
					{
						continue;
					}
					byte[] array7 = mem.Read(num7, 520);
					if (array7 == null)
					{
						continue;
					}
					int num8;
					for (num8 = 0; num8 + 1 < array7.Length && (array7[num8] != 0 || array7[num8 + 1] != 0); num8 += 2)
					{
					}
					if (num8 >= 10)
					{
						string text2 = Encoding.Unicode.GetString(array7, 0, num8);
						if (text2.Contains(".upk", StringComparison.OrdinalIgnoreCase))
						{
							L($"  {Nm(item4)}: {text2}   (object +0x{num5:X}, loader +0x{num6:X})");
							flag = true;
						}
					}
				}
			}
			if (!flag)
			{
				L("  " + Nm(item4) + ": file name not found");
			}
		}
		try
		{
			FindPaths(mem, new string[3] { "Ball_Default", "BBall", "Basketball" }, L);
		}
		catch (Exception ex)
		{
			L("Path search failed: " + ex.Message);
		}
		string Cls(int num9)
		{
			byte[] array8 = recs[num9];
			if (array8 == null || !classCand.HasValue)
			{
				return "?";
			}
			if (!index.TryGetValue(BitConverter.ToUInt64(array8, classCand.Value), out var value2))
			{
				return "?";
			}
			return Nm(value2);
		}
		string Nm(int num9)
		{
			byte[] array8 = recs[num9];
			if (array8 == null)
			{
				return "?";
			}
			int num10 = BitConverter.ToInt32(array8, nameField);
			if (!cache.TryGetValue(num10, out string value2))
			{
				Dictionary<int, string> dictionary = cache;
				string text3 = nameAt(num10) ?? "?";
				value2 = text3;
				dictionary[num10] = text3;
			}
			return value2;
		}
		int Outer(int num9)
		{
			byte[] array8 = recs[num9];
			if (array8 == null)
			{
				return -1;
			}
			if (!index.TryGetValue(BitConverter.ToUInt64(array8, outerOff), out var value2))
			{
				return -1;
			}
			return value2;
		}
		string PathOf(int num10)
		{
			List<string> list7 = new List<string>();
			int num9 = 0;
			while (num10 >= 0 && num9++ < 8)
			{
				list7.Add(Nm(num10));
				num10 = Outer(num10);
			}
			list7.Reverse();
			return string.Join(".", list7);
		}
		string Root(int i2)
		{
			int num9 = 0;
			while (num9++ < 12)
			{
				int num10 = Outer(i2);
				if (num10 < 0)
				{
					break;
				}
				i2 = num10;
			}
			return Nm(i2);
		}
	}

	private static void FindPaths(ProcessMemory mem, string[] words, Action<string> L)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		ulong num = 0uL;
		foreach (var (num2, num3) in mem.ReadableRegions())
		{
			if (stopwatch.Elapsed.TotalSeconds > 40.0)
			{
				L("Path search stopped after 40 s.");
				break;
			}
			for (ulong num4 = 0uL; num4 < num3; num4 += 4194304)
			{
				int num5 = (int)Math.Min(4194904uL, num3 - num4);
				byte[] array = mem.Read(num2 + num4, num5);
				if (array == null)
				{
					continue;
				}
				num += (ulong)num5;
				foreach (string text in words)
				{
					bool[] array2 = new bool[2] { true, false };
					foreach (bool flag in array2)
					{
						byte[] array3 = (flag ? Encoding.Unicode.GetBytes(text) : Encoding.ASCII.GetBytes(text));
						int num6 = ((!flag) ? 1 : 2);
						int num7 = 0;
						while (true)
						{
							int num8 = ((ReadOnlySpan<byte>)array.AsSpan(num7)).IndexOf((ReadOnlySpan<byte>)array3);
							if (num8 < 0)
							{
								break;
							}
							num8 += num7;
							num7 = num8 + 1;
							if (flag && (num8 & 1) != 0)
							{
								continue;
							}
							int num9 = num8;
							int num10 = num8;
							for (int k = 0; k < 300; k++)
							{
								if (num9 - num6 < 0)
								{
									break;
								}
								int num11 = (flag ? (array[num9 - 2] | (array[num9 - 1] << 8)) : array[num9 - 1]);
								if (num11 < 32 || num11 > 126)
								{
									break;
								}
								num9 -= num6;
							}
							for (int l = 0; l < 400; l++)
							{
								if (num10 + num6 > array.Length)
								{
									break;
								}
								int num12 = (flag ? (array[num10] | (array[num10 + 1] << 8)) : array[num10]);
								if (num12 < 32 || num12 > 126)
								{
									break;
								}
								num10 += num6;
							}
							string text2 = (flag ? Encoding.Unicode.GetString(array, num9, num10 - num9) : Encoding.ASCII.GetString(array, num9, num10 - num9));
							if (text2.Length >= text.Length + 1 && (text2.Contains(".upk", StringComparison.OrdinalIgnoreCase) || text2.Contains('\\') || text2.Contains('/')))
							{
								dictionary[text2] = dictionary.GetValueOrDefault(text2) + 1;
							}
						}
					}
				}
			}
		}
		List<KeyValuePair<string, int>> list = (from kv in dictionary
			orderby kv.Key.Contains(".upk", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains('\\') || kv.Key.Contains('/') descending, kv.Key.Length
			select kv).Take(60).ToList();
		L($"File-path text mentioning the ball packages: {dictionary.Count} different ({num / 1048576} MB looked through, {stopwatch.Elapsed.TotalSeconds:0.0} s)");
		foreach (KeyValuePair<string, int> item in list)
		{
			L($"  {item.Key}  (x{item.Value})");
		}
	}
}
