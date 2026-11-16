using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core.Net;

namespace Drip.Core.Cosmetics;

public static class CosmeticApi
{
	public const string OutfitsUrl = "https://fortnite-api.com/v2/cosmetics/br/search/all?type=outfit";

	public static string UrlFor(string type)
	{
		return "https://fortnite-api.com/v2/cosmetics/br/search/all?type=" + type;
	}

	public static Task<List<Cosmetic>> FetchOutfitsAsync(HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		return FetchAsync("outfit", http, ct);
	}

	public static async Task<List<Cosmetic>> FetchAsync(string type, HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		bool own = http == null;
		if (http == null)
		{
			http = Web.CreateClient(TimeSpan.FromSeconds(90L));
		}
		try
		{
			List<Cosmetic> result;
			await using (Stream stream = await http.GetStreamAsync(UrlFor(type), ct).ConfigureAwait(continueOnCapturedContext: false))
			{
				using JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(continueOnCapturedContext: false);
				result = Parse(jsonDocument.RootElement);
			}
			return result;
		}
		finally
		{
			if (own)
			{
				http.Dispose();
			}
		}
	}

	public static List<Cosmetic> Parse(JsonElement root)
	{
		List<Cosmetic> list = new List<Cosmetic>();
		foreach (JsonElement item in root.GetProperty("data").EnumerateArray())
		{
			string text = item.GetProperty("id").GetString() ?? "";
			string name = (item.TryGetProperty("name", out var value) ? (value.GetString() ?? text) : text);
			string rarity = ((item.TryGetProperty("rarity", out var value2) && value2.TryGetProperty("displayValue", out var value3)) ? (value3.GetString() ?? "") : "");
			string iconUrl = ((item.TryGetProperty("images", out var value4) && value4.TryGetProperty("smallIcon", out var value5)) ? (value5.GetString() ?? "") : "");
			string introduced = "";
			if (item.TryGetProperty("introduction", out var value6) && value6.ValueKind == JsonValueKind.Object)
			{
				string text2 = (value6.TryGetProperty("chapter", out var value7) ? (value7.GetString() ?? "") : "");
				string text3 = (value6.TryGetProperty("season", out var value8) ? (value8.GetString() ?? "") : "");
				if (text2.Length > 0)
				{
					introduced = "C" + text2 + "S" + text3;
				}
			}
			string added = ((item.TryGetProperty("added", out var value9) && value9.ValueKind == JsonValueKind.String) ? (value9.GetString() ?? "") : "");
			List<CosmeticStyle> list2 = new List<CosmeticStyle>();
			if (item.TryGetProperty("variants", out var value10) && value10.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item2 in value10.EnumerateArray())
				{
					string channel = (item2.TryGetProperty("channel", out var value11) ? (value11.GetString() ?? "") : "");
					if (!item2.TryGetProperty("options", out var value12) || value12.ValueKind != JsonValueKind.Array)
					{
						continue;
					}
					foreach (JsonElement item3 in value12.EnumerateArray())
					{
						string text4 = (item3.TryGetProperty("tag", out var value13) ? (value13.GetString() ?? "") : "");
						string name2 = (item3.TryGetProperty("name", out var value14) ? (value14.GetString() ?? text4) : text4);
						string imageUrl = ((item3.TryGetProperty("image", out var value15) && value15.ValueKind == JsonValueKind.String) ? (value15.GetString() ?? "") : "");
						if (text4.Length > 0)
						{
							list2.Add(new CosmeticStyle(channel, text4, name2, imageUrl));
						}
					}
				}
			}
			if (text.Length > 0)
			{
				list.Add(new Cosmetic(text, name, rarity, iconUrl, introduced, added, (list2.Count > 0) ? list2 : null));
			}
		}
		return list;
	}
}
