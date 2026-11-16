using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core.Net;

namespace Drip.Core.RocketLeague;

public static class RlCatalog
{
	public const string Url = "https://api.velocityrl.tech/items.json?l=INT";

	public static readonly string[] SwappableSlots = new string[7] { "Body", "Antenna", "Topper", "Wheels", "Rocket Boost", "Trail", "Ball" };

	public static readonly string[] ExperimentalSlots = new string[3] { "Body", "Ball", "Wheels" };

	public static string CachePath()
	{
		return Path.Combine(AppPaths.DataDir, "RocketLeague", "items.json");
	}

	public static List<RlItem> Parse(string json)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		List<RlItem> list = new List<RlItem>();
		foreach (JsonElement item in jsonDocument.RootElement.GetProperty("items").EnumerateArray())
		{
			string text = Str(item, "AssetPackage");
			string text2 = Str(item, "Slot");
			if (text.Length != 0 && text2.Length != 0)
			{
				list.Add(new RlItem((item.TryGetProperty("ID", out var value) && value.TryGetInt32(out var value2)) ? value2 : 0, Str(item, "Product"), text2, Str(item, "Quality"), text, Str(item, "ObjectName"), item.TryGetProperty("IsMultiAssetPackage", out var value3) && value3.ValueKind == JsonValueKind.True));
			}
		}
		return list;
	}

	private static string Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}

	public static async Task<List<RlItem>> LoadAsync(HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		string cache = CachePath();
		try
		{
			bool own = http == null;
			if (http == null)
			{
				http = Web.CreateClient(TimeSpan.FromSeconds(60L));
			}
			try
			{
				http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
				string text = Decode(await http.GetByteArrayAsync("https://api.velocityrl.tech/items.json?l=INT", ct).ConfigureAwait(continueOnCapturedContext: false));
				List<RlItem> items = Parse(text);
				if (items.Count > 0)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(cache));
					await File.WriteAllTextAsync(cache, text, ct).ConfigureAwait(continueOnCapturedContext: false);
					return items;
				}
			}
			finally
			{
				if (own)
				{
					http.Dispose();
				}
			}
		}
		catch (Exception) when (File.Exists(cache))
		{
		}
		if (File.Exists(cache))
		{
			return Parse(await File.ReadAllTextAsync(cache, ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		throw new IOException("Couldn't download the Rocket League item list and there's no saved copy yet. Check your internet and try again.");
	}

	private static string Decode(byte[] bytes)
	{
		if (bytes.Length > 2 && bytes[0] == 31 && bytes[1] == 139)
		{
			using (GZipStream stream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
			{
				using StreamReader streamReader = new StreamReader(stream);
				return streamReader.ReadToEnd();
			}
		}
		return Encoding.UTF8.GetString(bytes);
	}
}
