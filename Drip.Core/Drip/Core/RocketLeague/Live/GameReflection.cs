using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.RocketLeague.Live;

public sealed class GameReflection
{
	public sealed record Prop(int Index, string Name, string Type, int Offset, int Size, uint Mask)
	{
		public bool IsBool => Type == "BoolProperty";

		public bool IsFloat => Type == "FloatProperty";

		public bool IsInt => Type == "IntProperty";
	}

	private readonly GameContext _c;

	private readonly Dictionary<int, List<int>> _byClass = new Dictionary<int, List<int>>();

	private readonly Dictionary<string, List<int>> _classesByName = new Dictionary<string, List<int>>(StringComparer.Ordinal);

	private readonly Dictionary<int, int> _superOf = new Dictionary<int, int>();

	private readonly Dictionary<(string, string), Prop?> _found = new Dictionary<(string, string), Prop>();

	private int _offF = -1;

	private int _sizeF = -1;

	private int _maskF = -1;

	private int _superF = -1;

	private const int From = 96;

	private const int To = 256;

	public string Summary { get; private set; } = "";

	private GameReflection(GameContext c)
	{
		_c = c;
	}

	public static GameReflection? Build(GameContext c, out string error)
	{
		error = "";
		GameReflection gameReflection = new GameReflection(c);
		for (int i = 0; i < c.Count; i++)
		{
			if (!c.Readable(i))
			{
				continue;
			}
			string text = c.Cls(i);
			if (text == "Class")
			{
				string key = c.Nm(i);
				if (!gameReflection._classesByName.TryGetValue(key, out List<int> value))
				{
					value = (gameReflection._classesByName[key] = new List<int>());
				}
				value.Add(i);
			}
			else
			{
				if (!text.EndsWith("Property", StringComparison.Ordinal) || text.Contains("Default", StringComparison.Ordinal))
				{
					continue;
				}
				int num = c.Outer(i);
				if (num >= 0 && !(c.Cls(num) != "Class"))
				{
					if (!gameReflection._byClass.TryGetValue(num, out List<int> value2))
					{
						value2 = (gameReflection._byClass[num] = new List<int>());
					}
					value2.Add(i);
				}
			}
		}
		if (gameReflection._byClass.Count < 50)
		{
			error = "The game's class descriptions weren't found.";
			return null;
		}
		if (!gameReflection.WorkOutLayout(out error))
		{
			return null;
		}
		return gameReflection;
	}

	private byte[]? Raw(int obj)
	{
		return _c.Mem.Read(_c.AddressOf(obj), 272);
	}

	private static int I32(byte[] b, int at)
	{
		return BitConverter.ToInt32(b, at);
	}

	private bool WorkOutLayout(out string error)
	{
		error = "";
		List<int> classes = (from kv in (from kv in _byClass
				where kv.Value.Count >= 6 && _c.Nm(kv.Key).EndsWith("_TA", StringComparison.Ordinal)
				orderby kv.Value.Count descending
				select kv).Take(60)
			select kv.Key).ToList();
		if (classes.Count < 8)
		{
			classes = (from kv in (from kv in _byClass
					where kv.Value.Count >= 6
					orderby kv.Value.Count descending
					select kv).Take(60)
				select kv.Key).ToList();
		}
		Dictionary<int, byte[]> raw = new Dictionary<int, byte[]>();
		foreach (int item2 in classes)
		{
			foreach (int item3 in _byClass[item2])
			{
				byte[] array = Raw(item3);
				if (array != null)
				{
					raw[item3] = array;
				}
			}
		}
		if (raw.Count < 60)
		{
			error = "Too few property records could be read.";
			return false;
		}
		List<byte[]> list = (from kv in raw
			where _c.Cls(kv.Key) == "FloatProperty"
			select kv.Value).ToList();
		List<byte[]> list2 = (from kv in raw
			where _c.Cls(kv.Key) == "ObjectProperty"
			select kv.Value).ToList();
		if (list.Count < 5 || list2.Count < 3)
		{
			error = "Not enough float/object fields to learn the layout.";
			return false;
		}
		int g;
		for (g = 100; g < 256; g += 4)
		{
			if (list.All((byte[] b) => I32(b, g) == 4) && list2.All((byte[] b) => I32(b, g) == 8))
			{
				_sizeF = g;
				break;
			}
		}
		if (_sizeF < 0)
		{
			error = "The size field of the game's property records wasn't found.";
			return false;
		}
		double num = -1.0;
		int f;
		for (f = 96; f < 256; f += 4)
		{
			if (f == _sizeF || f == _sizeF - 4)
			{
				continue;
			}
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (int item4 in classes)
			{
				List<(int, int, bool)> list3 = (from p in _byClass[item4].Where(raw.ContainsKey)
					select (Off: I32(raw[p], f), Tot: Total(raw[p]), Bool: _c.Cls(p) == "BoolProperty")).ToList();
				if (list3.Any(((int Off, int Tot, bool Bool) x) => x.Off < 0 || x.Off > 131072 || x.Tot <= 0 || x.Tot > 262144))
				{
					num3 += 1000;
					continue;
				}
				list3.Sort(((int Off, int Tot, bool Bool) a, (int Off, int Tot, bool Bool) b) => a.Off.CompareTo(b.Off));
				for (int num5 = 0; num5 + 1 < list3.Count; num5++)
				{
					num2++;
					if ((list3[num5].Item1 != list3[num5 + 1].Item1 || !list3[num5].Item3 || !list3[num5 + 1].Item3) && list3[num5 + 1].Item1 < list3[num5].Item1 + list3[num5].Item2)
					{
						num3++;
					}
				}
				num4++;
			}
			if (num2 >= 50 && num4 >= classes.Count / 2)
			{
				double num6 = 1.0 - (double)num3 / (double)num2;
				if (num6 > num)
				{
					num = num6;
					_offF = f;
				}
			}
		}
		if (_offF < 0 || num < 0.9)
		{
			_offF = -1;
			error = "The offset field of the game's property records wasn't found.";
			return false;
		}
		List<(int, int)> list4 = _byClass.Where((KeyValuePair<int, List<int>> kv) => classes.Contains(kv.Key) || _c.Nm(kv.Key).StartsWith("Car", StringComparison.Ordinal) || _c.Nm(kv.Key).StartsWith("Vehicle", StringComparison.Ordinal)).SelectMany((KeyValuePair<int, List<int>> kv) => from p in kv.Value
			where _c.Cls(p) == "BoolProperty"
			select (Cls: kv.Key, P: p)).Take(6000)
			.ToList();
		Dictionary<int, byte[]> braw = new Dictionary<int, byte[]>();
		foreach (var item5 in list4)
		{
			int item = item5.Item2;
			byte[] array2 = (raw.TryGetValue(item, out byte[] value) ? value : Raw(item));
			if (array2 != null)
			{
				braw[item] = array2;
			}
		}
		int num7 = -1;
		int m;
		for (m = 96; m < 256; m += 4)
		{
			if (m == _sizeF || m == _sizeF - 4 || m == _offF)
			{
				continue;
			}
			bool flag = true;
			int num8 = 0;
			foreach (IGrouping<(int, int), (int, int)> item6 in list4.Where(((int Cls, int P) x) => braw.ContainsKey(x.P)).GroupBy(((int Cls, int P) x) => (Cls: x.Cls, Off: I32(braw[x.P], _offF))))
			{
				List<int> list5 = item6.Select(((int Cls, int P) x) => I32(braw[x.P], m)).ToList();
				if (list5.Any((int v) => v == 0 || (v & (v - 1)) != 0))
				{
					flag = false;
					break;
				}
				if (list5.Count > 1)
				{
					if (list5.Distinct().Count() != list5.Count)
					{
						flag = false;
						break;
					}
					num8++;
				}
			}
			if (flag && num8 > num7)
			{
				num7 = num8;
				_maskF = m;
			}
		}
		int num9 = ClassIndex("Car_TA") ?? ClassIndex("Vehicle_TA") ?? (-1);
		int num10 = 0;
		if (num9 >= 0)
		{
			for (int num11 = 96; num11 < 320; num11 += 8)
			{
				int num12 = 0;
				int num13 = num9;
				bool flag2 = false;
				while (num12 < 40)
				{
					ulong? num14 = _c.Mem.ReadU64(_c.AddressOf(num13) + (ulong)num11);
					if (!num14.HasValue || !_c.IsObject(num14.Value, out var index) || _c.Cls(index) != "Class" || index == num13)
					{
						break;
					}
					num13 = index;
					num12++;
					if (_c.Nm(num13) == "Object")
					{
						flag2 = true;
						break;
					}
				}
				if (flag2 && num12 >= 3 && num12 > num10)
				{
					num10 = num12;
					_superF = num11;
				}
			}
		}
		Summary = $"layout: size@0x{_sizeF:X} offset@0x{_offF:X} (fit {num:P0}) mask@" + ((_maskF >= 0) ? $"0x{_maskF:X}" : "?") + " super@" + ((_superF >= 0) ? $"0x{_superF:X}" : "?") + $" classes={_classesByName.Count} withFields={_byClass.Count}";
		return true;
		int Total(byte[] b)
		{
			int num15 = I32(b, _sizeF);
			int num16 = I32(b, _sizeF - 4);
			return num15 * ((num16 < 1 || num16 > 4096) ? 1 : num16);
		}
	}

	public int? ClassIndex(string name)
	{
		if (!_classesByName.TryGetValue(name, out List<int> value))
		{
			return null;
		}
		foreach (int item in value)
		{
			if (!_c.Nm(item).StartsWith("Default__", StringComparison.Ordinal))
			{
				return item;
			}
		}
		return null;
	}

	private int? Super(int cls)
	{
		if (_superF < 0)
		{
			return null;
		}
		if (_superOf.TryGetValue(cls, out var value))
		{
			if (value >= 0)
			{
				return value;
			}
			return null;
		}
		ulong? num = _c.Mem.ReadU64(_c.AddressOf(cls) + (ulong)_superF);
		int num2 = ((num.HasValue && _c.IsObject(num.Value, out var index) && _c.Cls(index) == "Class" && index != cls) ? index : (-1));
		_superOf[cls] = num2;
		if (num2 >= 0)
		{
			return num2;
		}
		return null;
	}

	public bool IsA(int cls, string name)
	{
		int? num = cls;
		int num2 = 0;
		while (num.HasValue && num2++ < 40)
		{
			if (_c.Nm(num.Value) == name)
			{
				return true;
			}
			num = Super(num.Value);
		}
		return false;
	}

	private Prop? Make(int propObj)
	{
		byte[] array = Raw(propObj);
		if (array == null)
		{
			return null;
		}
		int num = I32(array, _offF);
		if (num < 0 || num > 262144)
		{
			return null;
		}
		string text = _c.Cls(propObj);
		uint mask = ((text == "BoolProperty" && _maskF >= 0) ? ((uint)I32(array, _maskF)) : 0u);
		int size = I32(array, _sizeF);
		return new Prop(propObj, _c.Nm(propObj), text, num, size, mask);
	}

	public Prop? Find(string className, string field)
	{
		if (_found.TryGetValue((className, field), out Prop value))
		{
			return value;
		}
		Prop prop = null;
		int? num = ClassIndex(className);
		int num2 = 0;
		while (num.HasValue && prop == null && num2++ < 40)
		{
			if (_byClass.TryGetValue(num.Value, out List<int> value2))
			{
				int num3 = value2.FirstOrDefault((int x) => _c.Nm(x) == field, -1);
				if (num3 >= 0)
				{
					prop = Make(num3);
				}
			}
			num = Super(num.Value);
		}
		_found[(className, field)] = prop;
		return prop;
	}

	public List<Prop> All(string className)
	{
		List<Prop> list = new List<Prop>();
		int? num = ClassIndex(className);
		int num2 = 0;
		while (num.HasValue && num2++ < 40)
		{
			if (_byClass.TryGetValue(num.Value, out List<int> value))
			{
				foreach (int item in value)
				{
					Prop prop = Make(item);
					if (prop != null)
					{
						list.Add(prop);
					}
				}
			}
			num = Super(num.Value);
		}
		return list;
	}

	public HashSet<int> ClassesOf(string name)
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (KeyValuePair<string, List<int>> item in _classesByName)
		{
			foreach (int item2 in item.Value)
			{
				if (IsA(item2, name))
				{
					hashSet.Add(item2);
				}
			}
		}
		return hashSet;
	}

	public bool IsLiveObject(int obj, HashSet<int> classes)
	{
		if (_c.Readable(obj) && _c.AddressOf(obj) != 0L)
		{
			return classes.Contains(_c.ClassOf(obj));
		}
		return false;
	}

	public float? ReadFloat(int obj, Prop p)
	{
		byte[] array = _c.Mem.Read(_c.AddressOf(obj) + (ulong)p.Offset, 4);
		if (array != null)
		{
			return BitConverter.ToSingle(array, 0);
		}
		return null;
	}

	public int? ReadInt(int obj, Prop p)
	{
		return _c.Mem.ReadI32(_c.AddressOf(obj) + (ulong)p.Offset);
	}

	public int? ReadByte(int obj, Prop p)
	{
		byte[] array = _c.Mem.Read(_c.AddressOf(obj) + (ulong)p.Offset, 1);
		if (array != null)
		{
			return array[0];
		}
		return null;
	}

	public bool? ReadBool(int obj, Prop p)
	{
		int? num = ReadInt(obj, p);
		if (num.HasValue)
		{
			return ((uint)num.Value & p.Mask) != 0;
		}
		return null;
	}

	public bool WriteFloat(int obj, Prop p, float v)
	{
		return _c.Mem.Write(_c.AddressOf(obj) + (ulong)p.Offset, BitConverter.GetBytes(v));
	}

	public bool WriteInt(int obj, Prop p, int v)
	{
		return _c.Mem.Write(_c.AddressOf(obj) + (ulong)p.Offset, BitConverter.GetBytes(v));
	}

	public bool WriteBool(int obj, Prop p, bool on)
	{
		if (p.Mask == 0)
		{
			return false;
		}
		int? num = ReadInt(obj, p);
		if (!num.HasValue)
		{
			return false;
		}
		uint value = (uint)num.Value;
		uint num2 = (on ? (value | p.Mask) : (value & ~p.Mask));
		if (num2 != value)
		{
			return WriteInt(obj, p, (int)num2);
		}
		return true;
	}

	public string Describe(int obj, Prop p)
	{
		if (p.IsFloat)
		{
			return ReadFloat(obj, p)?.ToString("0.###") ?? "?";
		}
		if (p.IsInt)
		{
			return ReadInt(obj, p)?.ToString() ?? "?";
		}
		if (p.IsBool)
		{
			bool? flag = ReadBool(obj, p);
			if (!flag.HasValue)
			{
				return "?";
			}
			if (flag != true)
			{
				return "false";
			}
			return "true";
		}
		return p.Type;
	}
}
