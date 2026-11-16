using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Drip.Core.Fun;

public static class FitCode
{
	private sealed class Wire
	{
		[JsonPropertyName("g")]
		public string G { get; set; } = "";

		[JsonPropertyName("n")]
		public string N { get; set; } = "";

		[JsonPropertyName("e")]
		public List<string[]> E { get; set; } = new List<string[]>();
	}

	public const string Prefix = "DRIP1-";

	public const int MaxEntries = 40;

	private const int MaxJson = 32768;

	public static string Encode(Fit fit)
	{
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(new Wire
		{
			G = fit.Game,
			N = fit.Name,
			E = (from e in fit.Entries.Take(40)
				select new string[4] { e.Kind, e.Source, e.Target, e.Style }).ToList()
		});
		using MemoryStream memoryStream = new MemoryStream();
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionLevel.SmallestSize, leaveOpen: true))
		{
			gZipStream.Write(array, 0, array.Length);
		}
		byte[] array2 = memoryStream.ToArray();
		return "DRIP1-" + Convert.ToBase64String(array2).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_') + "-" + Crc(array2).ToString("x4");
	}

	public static Fit Decode(string code)
	{
		code = (code ?? "").Trim();
		if (!code.StartsWith("DRIP1-", StringComparison.Ordinal))
		{
			throw new FormatException("That isn't a Drip loadout code. It should start with DRIP1-");
		}
		string text = code.Substring("DRIP1-".Length);
		int num = text.LastIndexOf('-');
		if (num <= 0)
		{
			throw new FormatException("That code is cut off. Copy the whole line.");
		}
		string text2 = text.Substring(0, num).Replace('-', '+').Replace('_', '/');
		text2 = text2.PadRight(text2.Length + (4 - text2.Length % 4) % 4, '=');
		byte[] array;
		try
		{
			array = Convert.FromBase64String(text2);
		}
		catch
		{
			throw new FormatException("That code has stray characters in it. Copy the whole line.");
		}
		if (!int.TryParse(text.Substring(num + 1), NumberStyles.HexNumber, null, out var result) || result != Crc(array))
		{
			throw new FormatException("That code is damaged (the check failed). Ask your friend to send it again.");
		}
		byte[] array3;
		try
		{
			using GZipStream gZipStream = new GZipStream(new MemoryStream(array), CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array2 = new byte[4096];
			int count;
			while ((count = gZipStream.Read(array2, 0, array2.Length)) > 0)
			{
				memoryStream.Write(array2, 0, count);
				if (memoryStream.Length > 32768)
				{
					throw new FormatException("That code is too big to be a loadout.");
				}
			}
			array3 = memoryStream.ToArray();
		}
		catch (InvalidDataException)
		{
			throw new FormatException("That code isn't readable.");
		}
		Wire wire;
		try
		{
			wire = JsonSerializer.Deserialize<Wire>(array3);
		}
		catch
		{
			throw new FormatException("That code isn't readable.");
		}
		if (wire == null || !FunGames.All.Contains(wire.G))
		{
			throw new FormatException("That code is for a game this app doesn't know.");
		}
		if (wire.E.Count == 0)
		{
			throw new FormatException("That loadout has no swaps in it.");
		}
		if (wire.E.Count > 40)
		{
			throw new FormatException("That loadout has too many swaps.");
		}
		Fit fit = new Fit
		{
			Game = wire.G,
			Name = FunStore.CleanName(wire.N)
		};
		foreach (string[] item in wire.E)
		{
			if (item.Length < 3)
			{
				throw new FormatException("That code has a broken swap in it.");
			}
			string kind = Safe(item[0], 24);
			string text3 = Safe(item[1], 120);
			string text4 = Safe(item[2], 120);
			string style = ((item.Length > 3) ? Safe(item[3], 60) : "");
			if (text3.Length == 0 || text4.Length == 0)
			{
				throw new FormatException("That code has a broken swap in it.");
			}
			fit.Entries.Add(new FitEntry
			{
				Kind = kind,
				Source = text3,
				Target = text4,
				Style = style
			});
		}
		return fit;
	}

	private static string Safe(string? s, int max)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = s ?? "";
		foreach (char c in text)
		{
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2;
				switch (c)
				{
				case ' ':
				case '\'':
				case '(':
				case ')':
				case '-':
				case '.':
				case ':':
				case '_':
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			if (flag)
			{
				stringBuilder.Append(c);
			}
			if (stringBuilder.Length >= max)
			{
				break;
			}
		}
		return stringBuilder.ToString().Trim();
	}

	private static int Crc(byte[] data)
	{
		uint num = 2166136261u;
		foreach (byte b in data)
		{
			num ^= b;
			num *= 16777619;
		}
		return (int)(num & 0xFFFF);
	}
}
