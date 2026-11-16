using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.RocketLeague;

public static class RlCustom
{
	public sealed record Entry(string Path, DripRlFile Model)
	{
		public RlItem ToItem()
		{
			string text = RlCustomBuilder.BaseFor(Model.Slot);
			string text2 = ((text != null && text.Length > 0) ? text : Model.Base);
			return new RlItem(-1, Model.Name, Model.Slot, "Custom", text2, text2, Multi: false)
			{
				CustomFile = Path
			};
		}
	}

	public const string InvisibleWheelsName = "Invisible wheels";

	public static List<string> Skipped { get; private set; } = new List<string>();

	public static string Dir()
	{
		return Path.Combine(AppPaths.DataDir, "RocketLeague", "custom");
	}

	public static string SafeName(string name)
	{
		char[] bad = Path.GetInvalidFileNameChars();
		string text = new string(name.Select((char c) => (!bad.Contains(c) && c != '.') ? c : '_').ToArray()).Trim(new char[2] { '_', ' ' });
		if (text.Length != 0)
		{
			if (text.Length <= 48)
			{
				return text;
			}
			return text.Substring(0, 48);
		}
		return "model";
	}

	public static List<Entry> List(string? dir = null)
	{
		if (dir == null)
		{
			dir = Dir();
		}
		List<Entry> list = new List<Entry>();
		List<string> list2 = new List<string>();
		if (Directory.Exists(dir))
		{
			foreach (string item in Directory.EnumerateFiles(dir, "*.dripRL").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
			{
				try
				{
					list.Add(new Entry(item, DripRlFile.Load(item)));
				}
				catch (Exception ex)
				{
					list2.Add(Path.GetFileName(item) + ": " + ex.Message);
				}
			}
		}
		Skipped = list2;
		return list;
	}

	public static string Add(DripRlFile model, string? dir = null)
	{
		if (dir == null)
		{
			dir = Dir();
		}
		Directory.CreateDirectory(dir);
		string text = SafeName(model.Name);
		string text2 = Path.Combine(dir, text + ".dripRL");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(dir, $"{text} {num}{".dripRL"}");
			num++;
		}
		model.Save(text2);
		return text2;
	}

	public static Entry EnsureInvisibleWheels(string? dir = null)
	{
		foreach (Entry item in List(dir))
		{
			if (item.Model.Slot == "Wheels" && item.Model.Name == "Invisible wheels")
			{
				return item;
			}
		}
		CarMesh carMesh = new CarMesh();
		carMesh.Pos = new Vector3[4]
		{
			new Vector3(0.01f, 0.01f, 0.01f),
			new Vector3(-0.01f, -0.01f, 0.01f),
			new Vector3(-0.01f, 0.01f, -0.01f),
			new Vector3(0.01f, -0.01f, -0.01f)
		};
		carMesh.Nrm = new Vector3[4]
		{
			Vector3.Normalize(new Vector3(1f, 1f, 1f)),
			Vector3.Normalize(new Vector3(-1f, -1f, 1f)),
			Vector3.Normalize(new Vector3(-1f, 1f, -1f)),
			Vector3.Normalize(new Vector3(1f, -1f, -1f))
		};
		carMesh.Uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		};
		carMesh.Idx = new int[12]
		{
			0, 1, 2, 0, 3, 1, 0, 2, 3, 1,
			3, 2
		};
		CarMesh mesh = carMesh;
		DripRlFile model = new DripRlFile
		{
			Name = "Invisible wheels",
			Slot = "Wheels",
			Base = "WHEEL_Hydra",
			Mesh = mesh,
			Notes = "Hides the wheels it is swapped onto."
		};
		return new Entry(Add(model, dir), model);
	}

	public static Entry Import(string file, string? dir = null)
	{
		DripRlFile model = DripRlFile.Load(file);
		return new Entry(Add(model, dir), model);
	}

	public static void Delete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{
		}
	}

	public static List<RlItem> DiscoverBalls(IEnumerable<string> files)
	{
		List<RlItem> list = new List<RlItem>();
		List<string> list2 = files.ToList();
		if (list2.Any((string f) => Path.GetFileName(f).Equals("Mutators_Balls_SF.upk", StringComparison.OrdinalIgnoreCase)))
		{
			list.Add(new RlItem(-2, "Standard ball", "Ball", "Common", "Mutators_Balls", "Mutators_Balls", Multi: false));
		}
		foreach (string item in list2)
		{
			string fileName = Path.GetFileName(item);
			if (fileName.StartsWith("Ball_", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith("_SF.upk", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith("_T_SF.upk", StringComparison.OrdinalIgnoreCase))
			{
				string text = fileName;
				int length = "_SF.upk".Length;
				string text2 = text.Substring(0, text.Length - length);
				string text3 = text2.Substring("Ball_".Length).Replace('_', ' ');
				if (text3.Length != 0)
				{
					list.Add(new RlItem(-2, text3, "Ball", "Common", text2, text2, Multi: false));
				}
			}
		}
		return list;
	}
}
