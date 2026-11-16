using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Drip.Core.Localization;

public static class Loc
{
	public static readonly (string Code, string Name)[] Known = new (string, string)[11]
	{
		("en", "English"),
		("fr", "Français"),
		("ro", "Română"),
		("es", "Español"),
		("de", "Deutsch"),
		("pt", "Português"),
		("it", "Italiano"),
		("ru", "Русский"),
		("tr", "Türkçe"),
		("pl", "Polski"),
		("nl", "Nederlands")
	};

	private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();

	private static Dictionary<string, string> _exact = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly List<(Regex Rx, string Template, int Groups)> Templates = new List<(Regex, string, int)>();

	private static List<string> _long = new List<string>();

	public static string Language { get; private set; } = "en";

	public static bool IsEnglish => Language == "en";

	private static string SettingFile => Path.Combine(AppPaths.DataDir, "language.txt");

	public static string Choice
	{
		get
		{
			try
			{
				if (File.Exists(SettingFile))
				{
					string text = File.ReadAllText(SettingFile).Trim().ToLowerInvariant();
					if (text.Length > 0)
					{
						return text;
					}
				}
			}
			catch
			{
			}
			return "auto";
		}
	}

	public static void SaveChoice(string code)
	{
		try
		{
			File.WriteAllText(SettingFile, code);
		}
		catch
		{
		}
	}

	public static void Init()
	{
		string want = Choice;
		if (want == "auto" || !Known.Any(((string Code, string Name) k) => k.Code == want))
		{
			want = Detect();
		}
		Load(want);
	}

	public static string Detect()
	{
		CultureInfo[] array = new CultureInfo[3]
		{
			CultureInfo.CurrentUICulture,
			CultureInfo.InstalledUICulture,
			CultureInfo.CurrentCulture
		};
		foreach (CultureInfo cultureInfo in array)
		{
			string code = cultureInfo.TwoLetterISOLanguageName.ToLowerInvariant();
			if (Known.Any(((string Code, string Name) k) => k.Code == code))
			{
				return code;
			}
			string text = code;
			if ((text == "ca" || text == "gl") ? true : false)
			{
				return "es";
			}
			bool flag;
			switch (code)
			{
			case "uk":
			case "be":
			case "kk":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				return "ru";
			}
			if (code == "mo")
			{
				return "ro";
			}
		}
		return "en";
	}

	public static void Load(string code)
	{
		Cache.Clear();
		Templates.Clear();
		_long = new List<string>();
		_exact = new Dictionary<string, string>(StringComparer.Ordinal);
		Language = "en";
		if (code == "en")
		{
			return;
		}
		Assembly assembly = typeof(Loc).Assembly;
		string text = assembly.GetManifestResourceNames().FirstOrDefault((string text6) => text6.EndsWith(".Lang." + code + ".json", StringComparison.OrdinalIgnoreCase));
		if (text == null)
		{
			return;
		}
		try
		{
			using Stream utf8Json = assembly.GetManifestResourceStream(text);
			foreach (var (text4, text5) in JsonSerializer.Deserialize<Dictionary<string, string>>(utf8Json) ?? new Dictionary<string, string>())
			{
				if (string.IsNullOrWhiteSpace(text5))
				{
					continue;
				}
				if (Regex.IsMatch(text4, "\\{\\d+\\}"))
				{
					string input = Regex.Escape(text4);
					int n = 0;
					input = Regex.Replace(input, "\\\\\\{(\\d+)\\}", (Match m) =>
					{
						n = Math.Max(n, int.Parse(m.Groups[1].Value) + 1);
						return "(?<g" + m.Groups[1].Value + ">.+?)";
					});
					Templates.Add((new Regex("^" + input + "$", RegexOptions.Singleline | RegexOptions.CultureInvariant), text5, n));
				}
				else
				{
					_exact[text4] = text5;
				}
			}
			_long = (from k in _exact.Keys
				where k.Length >= 14 && k.Contains(' ')
				orderby k.Length descending
				select k).ToList();
			Language = code;
		}
		catch
		{
			Language = "en";
			_exact = new Dictionary<string, string>(StringComparer.Ordinal);
			Templates.Clear();
		}
	}

	public static string T(string? s)
	{
		if (string.IsNullOrEmpty(s) || Language == "en")
		{
			return s ?? "";
		}
		if (s.Length > 1500)
		{
			return s;
		}
		if (Cache.Count > 8000)
		{
			Cache.Clear();
		}
		return Cache.GetOrAdd(s, Translate);
	}

	public static string F(string format, params object?[] args)
	{
		string format2 = ((Language == "en") ? format : Cache.GetOrAdd(format, Translate));
		try
		{
			return string.Format(CultureInfo.CurrentCulture, format2, args);
		}
		catch (FormatException)
		{
			return string.Format(CultureInfo.CurrentCulture, format, args);
		}
	}

	private static string Translate(string s)
	{
		if (_exact.TryGetValue(s, out string value))
		{
			return value;
		}
		string text = s.Trim();
		if (text.Length == 0)
		{
			return s;
		}
		if (text != s)
		{
			string text2 = Translate(text);
			if ((object)text2 != text && text2 != text)
			{
				int length = s.Length - s.TrimStart().Length;
				int num = s.Length - s.TrimEnd().Length;
				string text3 = s.Substring(0, length);
				int num2 = num;
				return text3 + text2 + s.Substring(s.Length - num2);
			}
			return s;
		}
		foreach (var template in Templates)
		{
			Regex item = template.Rx;
			string item2 = template.Template;
			int item3 = template.Groups;
			Match match = item.Match(s);
			if (match.Success)
			{
				string text4 = item2;
				for (int i = 0; i < item3; i++)
				{
					text4 = text4.Replace("{" + i + "}", T(match.Groups["g" + i].Value));
				}
				return text4;
			}
		}
		if (s.Contains('\n'))
		{
			string[] array = s.Split('\n');
			bool flag = false;
			for (int j = 0; j < array.Length; j++)
			{
				string text5 = array[j].TrimEnd('\r');
				if (text5.Trim().Length != 0)
				{
					string text6 = Translate(text5);
					if (text6 != text5)
					{
						array[j] = text6;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return string.Join("\n", array);
			}
		}
		string text7 = s;
		bool flag2 = false;
		foreach (string item4 in _long)
		{
			if (text7.Contains(item4, StringComparison.Ordinal))
			{
				text7 = text7.Replace(item4, _exact[item4], StringComparison.Ordinal);
				flag2 = true;
			}
		}
		if (!flag2)
		{
			return s;
		}
		return text7;
	}
}
