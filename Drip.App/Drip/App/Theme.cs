using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Drip.App;

internal static class Theme
{
	private sealed record Pal(string Bg, string Panel, string Line, string Ink, string Text, string Text2, string Slot, string Accent, string OnAccent);

	private static readonly (string Key, string L, string D)[] Table = new (string, string, string)[32]
	{
		("Main", "#F8F8F6", "#000000"),
		("Rail", "#F8F8F6", "#000000"),
		("WindowBg", "#F8F8F6", "#000000"),
		("External", "#FFFFFF", "#101010"),
		("Panel", "#FFFFFF", "#181818"),
		("CardBg", "#FFFFFF", "#101010"),
		("HeroBg", "#FFFFFF", "#101010"),
		("PanelHover", "#F1F1EE", "#232323"),
		("Line", "#E4E4E0", "#2A2A2A"),
		("HeroLine", "#E4E4E0", "#2A2A2A"),
		("Edge", "#C9C9C4", "#3D3D3D"),
		("InputBg", "#FFFFFF", "#0C0C0C"),
		("Subtle", "#0D000000", "#12FFFFFF"),
		("Text", "#3A3A3A", "#D0D0D0"),
		("TextBright", "#0A0A0A", "#FAFAFA"),
		("Text2", "#6B6B6B", "#8E8E8E"),
		("Ivory", "#0A0A0A", "#F2F2F2"),
		("OnInk", "#FFFFFF", "#0A0A0A"),
		("Good", "#12894A", "#4ADE80"),
		("Danger", "#D32F2F", "#FF6161"),
		("Warn", "#A85F00", "#FFC247"),
		("WarnBg", "#1FFF9F0A", "#1AFFC247"),
		("WarnLine", "#66D98200", "#40FFC247"),
		("DangerBg", "#14D32F2F", "#1AFF6161"),
		("DangerLine", "#55D32F2F", "#40FF6161"),
		("Switch", "#D6D6D1", "#2E2E2E"),
		("SlotBg", "#FFFFFF", "#1A1A1A"),
		("SlotText", "#0A0A0A", "#FFFFFF"),
		("SlotText2", "#6B6B6B", "#9C9C9C"),
		("SlotChip", "#0F000000", "#2AFFFFFF"),
		("SlotLine", "#E0E0DB", "#00000000"),
		("Glow", "#00000000", "#00000000")
	};

	private static Game? _game;

	public static bool Light { get; private set; } = true;

	private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drip Swapper", "look.txt");

	private static Pal Palette(Game? g, bool light)
	{
		switch (g)
		{
		case Game.RocketLeague:
			if (light)
			{
				return new Pal("#E9F1FF", "#F7FAFF", "#CBDCF7", "#14234A", "#3A4C77", "#7487B0", "#D5E5FF", "#1F6BFF", "#FFFFFF");
			}
			return new Pal("#0F1522", "#172033", "#263553", "#EAF1FF", "#C5D2EE", "#8799BD", "#1F2D4A", "#3A82FF", "#FFFFFF");
		case Game.Fortnite:
			if (light)
			{
				return new Pal("#E8F7FF", "#F6FCFF", "#C2E6F7", "#0F2F45", "#365A72", "#6F94AB", "#CFEEFF", "#2EB8F5", "#062033");
			}
			return new Pal("#0D1A24", "#142532", "#223D50", "#E6F6FF", "#BFDDEE", "#86AEC4", "#1B3547", "#4CC6FF", "#062033");
		case Game.FallGuys:
			if (light)
			{
				return new Pal("#FFEDF5", "#FFF8FB", "#F5CCDF", "#40182E", "#64374D", "#A0728A", "#FFD6E7", "#FF2D87", "#FFFFFF");
			}
			return new Pal("#20111A", "#2B1923", "#452839", "#FFEBF4", "#E8CADA", "#AC869B", "#3B1F2E", "#FF4A9A", "#FFFFFF");
		case Game.Stellar:
			if (light)
			{
				return new Pal("#EEEDFF", "#F8F8FF", "#D2D0F5", "#1F1B47", "#454178", "#837FB5", "#DDDBFF", "#6C5CFF", "#FFFFFF");
			}
			return new Pal("#0F1022", "#171833", "#2A2C55", "#EEF0FF", "#C6C9EE", "#8E92C4", "#222449", "#8A7BFF", "#FFFFFF");
		case Game.Retrac:
			if (light)
			{
				return new Pal("#FFF7D6", "#FFFCEE", "#F0DE95", "#3A2D00", "#6B5710", "#A08B3F", "#FFEFA8", "#F2B705", "#2A2000");
			}
			return new Pal("#17130A", "#211B0D", "#3D3314", "#FFF6D0", "#E6D595", "#B5A265", "#2E260F", "#FFD11A", "#241B00");
		default:
			if (light)
			{
				return new Pal("#EDEFF6", "#F8F9FD", "#D5D9E8", "#212741", "#434B6B", "#7F87A8", "#DCE0F2", "#4B5BD6", "#FFFFFF");
			}
			return new Pal("#14161E", "#1D202B", "#2E3244", "#F0F2FA", "#CDD2E4", "#8F96B0", "#282C3D", "#6C7BFF", "#FFFFFF");
		}
	}

	public static void Load()
	{
		try
		{
			Light = !File.Exists(FilePath) || !File.ReadAllText(FilePath).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			Light = true;
		}
		Apply();
	}

	public static void Toggle()
	{
		Light = !Light;
		Apply();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			File.WriteAllText(FilePath, Light ? "light" : "dark");
		}
		catch
		{
		}
	}

	public static void SetGame(Game? game)
	{
		_game = game;
		Apply();
	}

	private static void Apply()
	{
		(string, string, string)[] table = Table;
		for (int i = 0; i < table.Length; i++)
		{
			var (key, text, text2) = table[i];
			Set(key, Light ? text : text2);
		}
		Pal pal = Palette(_game, Light);
		string[] array = new string[3] { "Main", "Rail", "WindowBg" };
		for (int i = 0; i < array.Length; i++)
		{
			Set(array[i], pal.Bg);
		}
		array = new string[5] { "External", "Panel", "CardBg", "HeroBg", "InputBg" };
		for (int i = 0; i < array.Length; i++)
		{
			Set(array[i], pal.Panel);
		}
		array = new string[2] { "Line", "HeroLine" };
		for (int i = 0; i < array.Length; i++)
		{
			Set(array[i], pal.Line);
		}
		Set("PanelHover", Mix(pal.Panel, pal.Bg, 0.5));
		Set("Edge", Mix(pal.Line, pal.Ink, 0.3));
		Set("Switch", Mix(pal.Line, pal.Ink, 0.12));
		Set("Subtle", Light ? ("#12" + pal.Ink.Substring(1)) : ("#14" + pal.Ink.Substring(1)));
		Set("TextBright", pal.Ink);
		Set("Ivory", pal.Ink);
		Set("Text", pal.Text);
		Set("Text2", pal.Text2);
		Set("OnInk", pal.Panel);
		Set("SlotBg", pal.Slot);
		Set("SlotText", pal.Ink);
		Set("SlotText2", pal.Text2);
		Set("SlotChip", "#1A" + pal.Ink.Substring(1));
		Set("SlotLine", Mix(pal.Slot, pal.Ink, 0.14));
		array = new string[3] { "Accent", "AccentGrad", "GoGrad" };
		for (int i = 0; i < array.Length; i++)
		{
			Set(array[i], pal.Accent);
		}
		Set("OnAccent", pal.OnAccent);
		Set("AccentB", pal.Ink);
	}

	private static string Mix(string a, string b, double t)
	{
		Color color = (Color)ColorConverter.ConvertFromString(a);
		Color color2 = (Color)ColorConverter.ConvertFromString(b);
		return $"#{M(color.R, color2.R):X2}{M(color.G, color2.G):X2}{M(color.B, color2.B):X2}";
		byte M(byte x, byte y)
		{
			return (byte)Math.Round((double)(int)x + (double)(y - x) * t);
		}
	}

	private static void Set(string key, string hex)
	{
		try
		{
			ResourceDictionary resources = Application.Current.Resources;
			Color color = (Color)ColorConverter.ConvertFromString(hex);
			if (resources[key] is SolidColorBrush { IsFrozen: false } solidColorBrush)
			{
				solidColorBrush.Color = color;
			}
			else
			{
				resources[key] = new SolidColorBrush(color);
			}
		}
		catch
		{
		}
	}
}
