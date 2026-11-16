using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Drip.Core;
using Drip.Core.RocketLeague;

namespace Drip.App;

internal static class DiagnosticsHelper
{
	public const string Explain = "Saves one small zip to your Downloads folder with the activity log, the tweak report and a few settings files. No keys, no game files. Send it when something doesn't work.";

	public static string Save(string game, IEnumerable<KeyValuePair<string, string>>? extra = null)
	{
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("Game window", game),
			new KeyValuePair<string, string>("App", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?"),
			new KeyValuePair<string, string>("Windows", Environment.OSVersion.VersionString),
			new KeyValuePair<string, string>(".NET", Environment.Version.ToString()),
			new KeyValuePair<string, string>("Data folder", AppPaths.DataDir)
		};
		try
		{
			list.Add(new KeyValuePair<string, string>("Rocket League open", RlLocator.IsRunning().ToString()));
		}
		catch
		{
		}
		try
		{
			list.Add(new KeyValuePair<string, string>("Anti-cheat running", RlLocator.IsAntiCheatRunning().ToString()));
		}
		catch
		{
		}
		if (extra != null)
		{
			list.AddRange(extra);
		}
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		if (!Directory.Exists(text))
		{
			text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
		}
		string text2 = DiagnosticsBundle.Build(AppPaths.DataDir, text, list);
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + text2 + "\"")
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
		return text2;
	}
}
