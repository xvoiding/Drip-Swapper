using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Drip.Core.RocketLeague.Live;

public static class LiveBall
{
	public sealed record Slot(ulong Address, ulong Value, string Owner, string Mesh, bool OnComponent);

	public sealed record Result(string Report, List<string> Meshes);

	private static readonly List<(ulong Address, ulong Original, ulong Written)> Applied = new List<(ulong, ulong, ulong)>();

	private static List<int> Balls(GameContext c)
	{
		return Enumerable.Range(0, c.Count).Where((int i) =>
		{
			bool flag = c.Readable(i) && c.Cls(i) == "Ball_TA" && !c.Nm(i).StartsWith("Default__", StringComparison.Ordinal);
			if (flag)
			{
				string text = c.Root(i);
				bool flag2 = ((text == "Archetypes" || text == "TAGame") ? true : false);
				flag = !flag2;
			}
			return flag;
		}).ToList();
	}

	private static List<Slot> Slots(GameContext c, int ball, StringBuilder? sb)
	{
		List<Slot> list = new List<Slot>();
		ulong num = c.AddressOf(ball);
		byte[] array = c.Mem.Read(num, 4096);
		if (array == null)
		{
			sb?.AppendLine("  The ball's memory couldn't be read.");
			return list;
		}
		HashSet<int> comps = new HashSet<int>();
		for (int i = 0; i + 8 <= array.Length; i += 8)
		{
			ulong num2 = BitConverter.ToUInt64(array, i);
			if (c.IsObject(num2, out var index) && c.Readable(index) && c.Cls(index) == "StaticMesh")
			{
				list.Add(new Slot(num + (ulong)i, num2, $"ball +0x{i:X}", c.Nm(index), OnComponent: false));
			}
			Consider(num2);
			if (i + 16 > array.Length || !GameScan.LooksLikePointer(num2) || c.IsObject(num2, out var _))
			{
				continue;
			}
			int num3 = BitConverter.ToInt32(array, i + 8);
			int num4 = BitConverter.ToInt32(array, i + 12);
			if (num3 <= 0 || num3 > 64 || num4 < num3 || num4 > 256)
			{
				continue;
			}
			byte[] array2 = c.Mem.Read(num2, num3 * 8);
			if (array2 != null)
			{
				for (int j = 0; j < num3; j++)
				{
					Consider(BitConverter.ToUInt64(array2, j * 8));
				}
			}
		}
		foreach (int item in comps)
		{
			ulong num5 = c.AddressOf(item);
			byte[] array3 = c.Mem.Read(num5, 1792);
			if (array3 == null)
			{
				continue;
			}
			for (int k = 0; k + 8 <= array3.Length; k += 8)
			{
				if (c.IsObject(BitConverter.ToUInt64(array3, k), out var index3) && c.Readable(index3) && c.Cls(index3) == "StaticMesh")
				{
					list.Add(new Slot(num5 + (ulong)k, BitConverter.ToUInt64(array3, k), $"{c.Nm(item)} +0x{k:X}", c.Nm(index3), OnComponent: true));
				}
			}
		}
		return list;
		void Consider(ulong v)
		{
			if (c.IsObject(v, out var index4) && c.Readable(index4) && c.Cls(index4).Contains("MeshComponent", StringComparison.Ordinal))
			{
				comps.Add(index4);
			}
		}
	}

	private static List<string> LoadedBallMeshes(GameContext c)
	{
		return (from i in Enumerable.Range(0, c.Count)
			where c.Readable(i) && c.Cls(i) == "StaticMesh" && (c.Nm(i).Contains("ball", StringComparison.OrdinalIgnoreCase) || c.Nm(i).Contains("puck", StringComparison.OrdinalIgnoreCase))
			select i).Select(c.Nm).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string n) => n, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static Result Inspect()
	{
		StringBuilder stringBuilder = new StringBuilder();
		string error;
		using GameContext gameContext = GameContext.Attach(write: false, out error);
		if (gameContext == null)
		{
			return new Result(error, new List<string>());
		}
		List<int> list = Balls(gameContext);
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
		handler.AppendLiteral("Live balls found: ");
		handler.AppendFormatted(list.Count);
		stringBuilder3.AppendLine(ref handler);
		foreach (int item in list)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted(gameContext.PathOf(item));
			handler.AppendLiteral(" at 0x");
			handler.AppendFormatted(gameContext.AddressOf(item), "X");
			stringBuilder4.AppendLine(ref handler);
			foreach (Slot item2 in Slots(gameContext, item, stringBuilder))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(29, 4, stringBuilder2);
				handler.AppendLiteral("    mesh pointer at 0x");
				handler.AppendFormatted(item2.Address, "X");
				handler.AppendLiteral(" (");
				handler.AppendFormatted(item2.Owner);
				handler.AppendLiteral(") -> ");
				handler.AppendFormatted(item2.Mesh);
				handler.AppendFormatted(item2.OnComponent ? "" : "  [only a reference]");
				stringBuilder5.AppendLine(ref handler);
			}
		}
		List<string> list2 = LoadedBallMeshes(gameContext);
		stringBuilder.AppendLine($"Ball models loaded right now ({list2.Count}): " + string.Join(", ", list2));
		if (Applied.Count > 0)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(62, 1, stringBuilder2);
			handler.AppendLiteral("The app has ");
			handler.AppendFormatted(Applied.Count);
			handler.AppendLiteral(" change(s) on the ball. Use Put back to undo them.");
			stringBuilder6.AppendLine(ref handler);
		}
		return new Result(stringBuilder.ToString(), list2);
	}

	public static string Apply(string meshName)
	{
		if (string.IsNullOrWhiteSpace(meshName))
		{
			return "Pick a ball model first.";
		}
		GameContext c = GameContext.Attach(write: true, out string error);
		try
		{
			if (c == null)
			{
				return error;
			}
			List<int> list = Balls(c);
			if (list.Count == 0)
			{
				return "No live ball found. Start a freeplay session and wait for the ball to appear.";
			}
			int num = Enumerable.Range(0, c.Count).FirstOrDefault((int i) => c.Readable(i) && c.Cls(i) == "StaticMesh" && c.Nm(i).Equals(meshName, StringComparison.OrdinalIgnoreCase), -1);
			if (num < 0)
			{
				return meshName + " isn't loaded in this session.";
			}
			ulong newPtr = c.AddressOf(num);
			int num2 = 0;
			foreach (int item in list)
			{
				foreach (Slot s in from slot in Slots(c, item, null)
					where slot.OnComponent && slot.Value != newPtr
					select slot)
				{
					if (!c.Mem.Write(s.Address, BitConverter.GetBytes(newPtr)) || c.Mem.ReadU64(s.Address) != newPtr)
					{
						continue;
					}
					if (!Applied.Any(((ulong Address, ulong Original, ulong Written) a) => a.Address == s.Address))
					{
						Applied.Add((s.Address, s.Value, newPtr));
					}
					else
					{
						int index = Applied.FindIndex(((ulong Address, ulong Original, ulong Written) a) => a.Address == s.Address);
						Applied[index] = (s.Address, Applied[index].Original, newPtr);
					}
					num2++;
				}
			}
			return (num2 == 0) ? "Nothing was changed (the ball may already use that model, or the game blocked the write)." : $"Changed {num2} pointer(s) to {meshName}. The picture on screen may only update when the ball respawns (reset the shot or kick off again).";
		}
		finally
		{
			if (c != null)
			{
				((IDisposable)c).Dispose();
			}
		}
	}

	public static string PutBack()
	{
		if (Applied.Count == 0)
		{
			return "Nothing to put back.";
		}
		string error;
		using GameContext gameContext = GameContext.Attach(write: true, out error);
		if (gameContext == null)
		{
			return error;
		}
		int num = 0;
		int num2 = 0;
		foreach (var item in Applied.ToList())
		{
			if (gameContext.Mem.ReadU64(item.Address) == item.Written && gameContext.Mem.Write(item.Address, BitConverter.GetBytes(item.Original)))
			{
				num++;
			}
			else
			{
				num2++;
			}
		}
		Applied.Clear();
		return $"Put back {num} pointer(s)." + ((num2 > 0) ? $" {num2} had already changed by themselves (the ball was probably respawned)." : "");
	}
}
