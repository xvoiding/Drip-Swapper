using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Drip.Core.FallGuys;

/// <summary>
/// Opens .blend files by asking the person's own Blender to export a .glb first.
/// The built-in importer can't read files saved by recent Blender versions, so we never feed it a .blend.
/// </summary>
public static class BlenderBridge
{
	public const string NotFoundMessage = "Drip Swapper can't open .blend files by itself. Easiest fix: in Blender press File > Export > glTF 2.0, pick \"glTF Binary (.glb)\", then drag the .glb in here. (If Blender is installed somewhere unusual, Drip Swapper can't find it.)";

	public static string? FindBlender()
	{
		string? env = Environment.GetEnvironmentVariable("DRIP_BLENDER");
		if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
		{
			return env;
		}
		List<string> roots = new List<string>();
		foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
		{
			string p = Environment.GetFolderPath(f);
			if (p.Length > 0)
			{
				roots.Add(p);
				roots.Add(Path.Combine(p, "Programs"));
			}
		}
		try
		{
			foreach (DriveInfo d in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
			{
				roots.Add(d.RootDirectory.FullName);
				roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files"));
				roots.Add(Path.Combine(d.RootDirectory.FullName, "Games"));
				roots.Add(Path.Combine(d.RootDirectory.FullName, "SteamLibrary", "steamapps", "common"));
				roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files (x86)", "Steam", "steamapps", "common"));
			}
		}
		catch
		{
		}
		List<(Version v, string path)> found = new List<(Version, string)>();
		foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			try
			{
				if (!Directory.Exists(root))
				{
					continue;
				}
				foreach (string dir in Directory.EnumerateDirectories(root, "Blender*", SearchOption.TopDirectoryOnly))
				{
					Probe(dir, found);
					string foundation = dir;
					if (Directory.Exists(foundation))
					{
						foreach (string sub in Directory.EnumerateDirectories(foundation, "Blender*", SearchOption.TopDirectoryOnly))
						{
							Probe(sub, found);
						}
					}
				}
			}
			catch
			{
			}
		}
		string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "blender.exe");
		if (found.Count == 0 && File.Exists(alias))
		{
			return alias;
		}
		return found.OrderByDescending(x => x.v).Select(x => x.path).FirstOrDefault();
	}

	private static void Probe(string dir, List<(Version v, string path)> found)
	{
		string exe = Path.Combine(dir, "blender.exe");
		if (!File.Exists(exe))
		{
			return;
		}
		Version v = new Version(0, 0);
		string name = Path.GetFileName(dir);
		string digits = new string(name.SkipWhile(c => !char.IsDigit(c)).ToArray()).Trim();
		if (!Version.TryParse(digits, out Version? parsed) || parsed == null)
		{
			string first = new string(digits.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).TrimEnd('.');
			Version.TryParse(first.Contains('.') ? first : first + ".0", out parsed);
		}
		if (parsed != null)
		{
			v = parsed;
		}
		found.Add((v, exe));
	}

	/// <summary>Exports the .blend to a .glb in a fresh temp folder. Caller deletes the folder.</summary>
	public static string ToGlb(string blendPath, out string tempDir)
	{
		string? blender = FindBlender();
		if (blender == null)
		{
			throw new InvalidDataException(NotFoundMessage);
		}
		tempDir = Path.Combine(Path.GetTempPath(), "DripBlend_" + Guid.NewGuid().ToString("N").Substring(0, 10));
		Directory.CreateDirectory(tempDir);
		string glb = Path.Combine(tempDir, "model.glb");
		string py = "import bpy\n" +
			"bpy.ops.export_scene.gltf(filepath=" + PyStr(glb) + ", export_format='GLB', export_apply=True, export_yup=True)\n";
		string script = Path.Combine(tempDir, "export.py");
		File.WriteAllText(script, py, new UTF8Encoding(false));
		ProcessStartInfo psi = new ProcessStartInfo(blender)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		psi.ArgumentList.Add("--background");
		psi.ArgumentList.Add(blendPath);
		psi.ArgumentList.Add("--python");
		psi.ArgumentList.Add(script);
		StringBuilder log = new StringBuilder();
		using Process p = new Process { StartInfo = psi };
		p.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
		p.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
		p.Start();
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();
		if (!p.WaitForExit(240000))
		{
			try
			{
				p.Kill(entireProcessTree: true);
			}
			catch
			{
			}
			throw new InvalidDataException("Blender took too long to open that file.");
		}
		p.WaitForExit();
		if (!File.Exists(glb))
		{
			string tail;
			lock (log)
			{
				tail = string.Join(" ", log.ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(3));
			}
			throw new InvalidDataException("Blender couldn't export that file. " + (tail.Length > 0 ? "(" + tail + ")" : "") + " Try File > Export > glTF 2.0 (.glb) in Blender and drag that in instead.");
		}
		return glb;
	}

	private static string PyStr(string s)
	{
		return "'" + s.Replace("\\", "/").Replace("'", "\\'") + "'";
	}
}
