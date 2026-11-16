using System;
using System.Linq;
using Drip.Core.Localization;

namespace Drip.Core.Swapping;

public static class Friendly
{
	private static readonly (string[] Needles, string Plain)[] Rules = new (string[], string)[11]
	{
		(new string[3] { "Close Fortnite", "before swapping", "before applying a mod" }, "Fortnite is still running. Close it completely (also check the system tray and the Epic Games Launcher), then try again."),
		(new string[3] { "not found in any container", "not in any container", "isn't in any container" }, "Drip Swapper can't see one of the game files it needs. That usually means the game just updated or the Epic launcher is still working on it. Close Fortnite and the launcher, press \"Fix problems\" on the Home tab, then try again."),
		(new string[4] { "being used by another process", "UnauthorizedAccess", "Access to the path", "access is denied" }, "Windows won't let the app change a game file right now. Close Fortnite and the Epic Games Launcher (and pause your antivirus for a minute if it keeps happening), then try again. If it still fails, right-click Drip Swapper and choose Run as administrator."),
		(new string[3] { "not enough space", "No space left", "disk is full" }, "Your drive is full, so the game file couldn't be written. Free up a little space (a few hundred MB is enough), then try again."),
		(new string[3] { "isn't in your local game files", "isn't in the local game files", "chunk id from the game index" }, "That item isn't downloaded on this PC, so it can't be swapped yet. Open it in Fortnite once (or pick one you've used before), close Fortnite, then try again."),
		(new string[2] { "(chunk type", "isn't in your game files" }, "The model maker needs a game file that this version of Fortnite doesn't have, probably because Fortnite changed in an update. Make sure Fortnite is fully updated and press Fix problems on the Home tab. If it still fails, this Fortnite version needs a new Drip Swapper."),
		(new string[1] { "only holds" }, "The one you want is bigger than the one you have, so it doesn't fit in the same spot. Pick a smaller or simpler one."),
		(new string[3] { "system proxy refused", "actively refused", "127.0.0.1" }, "The app couldn't reach the internet. Windows is set to use a proxy or VPN program that isn't running (or is blocking this app). Start your VPN or proxy and try again, or turn off the proxy in Windows Settings > Network & internet > Proxy. If the skin list sites are blocked where you live, you'll need the VPN on."),
		(new string[5] { "No such host", "HttpRequestException", "error occurred while sending the request", "The operation was canceled", "timed out" }, "The app couldn't reach the internet (it needs it for the skin list and the game's unlock keys). Check your connection, then press Connect again."),
		(new string[2] { "no AES key", "AES endpoint returned no keys" }, "Fortnite just updated and the unlock keys for the new files aren't published yet. Try again in a few hours."),
		(new string[1] { "doesn't refer to those character parts" }, "These two skins are built too differently for a part swap. Try a different skin.")
	};

	public static string Explain(string? raw)
	{
		raw = (raw ?? "").Trim();
		if (raw.Length == 0)
		{
			return Loc.T("Something went wrong, but there is no message. Press \"Fix problems\" on the Home tab and try again.");
		}
		(string[], string)[] rules = Rules;
		for (int i = 0; i < rules.Length; i++)
		{
			var (source, s) = rules[i];
			if (source.Any((string n) => raw.Contains(n, StringComparison.OrdinalIgnoreCase)))
			{
				return Loc.T(s) + "\n\n" + Loc.T("Details:") + " " + raw;
			}
		}
		return raw;
	}
}
