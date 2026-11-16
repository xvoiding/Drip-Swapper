using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core.Net;

namespace Drip.Core.Keys;

public sealed class AesKeys
{
	public const string Endpoint = "https://fortnite-api.com/v2/aes";

	public const string ZeroGuid = "00000000000000000000000000000000";

	private readonly Dictionary<string, byte[]> _keys = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

	public string Build { get; private set; } = "";

	public int Count => _keys.Count;

	public string Version
	{
		get
		{
			Match match = Regex.Match(Build ?? "", "Release-(\\d+\\.\\d+)");
			if (!match.Success)
			{
				return "";
			}
			return match.Groups[1].Value;
		}
	}

	public double? VersionNumber
	{
		get
		{
			if (!double.TryParse(Version, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return null;
			}
			return result;
		}
	}

	public void Add(string guid, string hexKey)
	{
		_keys[guid] = ParseKey(hexKey);
	}

	public IEnumerable<(string Guid, string Key)> AllKeys()
	{
		return _keys.Select((KeyValuePair<string, byte[]> kv) => (kv.Key.ToUpperInvariant(), "0x" + Convert.ToHexString(kv.Value)));
	}

	public byte[]? Find(string guid)
	{
		if (!_keys.TryGetValue(guid, out byte[] value))
		{
			return null;
		}
		return value;
	}

	public static byte[] ParseKey(string hex)
	{
		hex = hex.Trim();
		if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			hex = hex.Substring(2);
		}
		if (hex.Length != 64)
		{
			throw new FormatException("AES key must be 32 bytes (64 hex chars).");
		}
		return Convert.FromHexString(hex);
	}

	public static AesKeys Parse(string json)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement property = jsonDocument.RootElement.GetProperty("data");
		AesKeys aesKeys = new AesKeys();
		if (property.TryGetProperty("build", out var value))
		{
			aesKeys.Build = value.GetString() ?? "";
		}
		if (property.TryGetProperty("mainKey", out var value2) && value2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value2.GetString()))
		{
			aesKeys.Add("00000000000000000000000000000000", value2.GetString());
		}
		if (property.TryGetProperty("dynamicKeys", out var value3) && value3.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in value3.EnumerateArray())
			{
				string text = (item.TryGetProperty("pakGuid", out var value4) ? value4.GetString() : null);
				string text2 = (item.TryGetProperty("key", out var value5) ? value5.GetString() : null);
				if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
				{
					aesKeys.Add(text.Replace("-", ""), text2);
				}
			}
		}
		return aesKeys;
	}

	public static async Task<AesKeys> FetchAsync(HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		bool own = http == null;
		if (http == null)
		{
			http = Web.CreateClient(TimeSpan.FromSeconds(30L));
		}
		try
		{
			return Parse(await http.GetStringAsync("https://fortnite-api.com/v2/aes", ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		finally
		{
			if (own)
			{
				http.Dispose();
			}
		}
	}
}
