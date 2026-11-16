using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core.Net;

namespace Drip.Core.Cosmetics;

public static class MappingsFetcher
{
	public sealed record MappingsInfo(string Version, string? BrotliUrl, string? ZstdUrl);

	public const string IndexUrl = "https://uedb.dev/svc/api/v1/fortnite/mappings";

	public static string CacheDirectory => Path.Combine(AppPaths.DataDir, "mappings");

	public static MappingsInfo ParseIndex(string json)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement rootElement = jsonDocument.RootElement;
		string? version = (rootElement.TryGetProperty("version", out var value) ? (value.GetString() ?? "") : "");
		string brotliUrl = null;
		string zstdUrl = null;
		if (rootElement.TryGetProperty("mappings", out var value2) && value2.ValueKind == JsonValueKind.Object)
		{
			if (value2.TryGetProperty("Brotli", out var value3) && value3.ValueKind == JsonValueKind.String)
			{
				brotliUrl = value3.GetString();
			}
			if (value2.TryGetProperty("ZStandard", out var value4) && value4.ValueKind == JsonValueKind.String)
			{
				zstdUrl = value4.GetString();
			}
		}
		return new MappingsInfo(version, brotliUrl, zstdUrl);
	}

	public static async Task<(string Path, string Version)> EnsureAsync(Action<string>? log = null, string? cacheDir = null, HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		bool own = http == null;
		if (http == null)
		{
			http = Web.CreateClient(TimeSpan.FromMinutes(2L));
		}
		try
		{
			if (cacheDir == null)
			{
				cacheDir = CacheDirectory;
			}
			Directory.CreateDirectory(cacheDir);
			MappingsInfo info = ParseIndex(await http.GetStringAsync("https://uedb.dev/svc/api/v1/fortnite/mappings", ct).ConfigureAwait(continueOnCapturedContext: false));
			string text = info.BrotliUrl ?? info.ZstdUrl;
			if (text == null)
			{
				throw new InvalidOperationException("The mappings index listed no files.");
			}
			string text2 = string.Concat(info.Version.Select((char c) =>
			{
				bool flag = char.IsLetterOrDigit(c);
				if (!flag)
				{
					bool flag2;
					switch (c)
					{
					case '+':
					case '-':
					case '.':
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					flag = flag2;
				}
				return (!flag) ? '_' : c;
			}));
			string file = Path.Combine(cacheDir, text2 + ".usmap");
			if (!File.Exists(file) || new FileInfo(file).Length < 1024)
			{
				log?.Invoke("Downloading mappings for " + info.Version + "...");
				byte[] bytes = await http.GetByteArrayAsync(text, ct).ConfigureAwait(continueOnCapturedContext: false);
				string tmp = file + ".part";
				await File.WriteAllBytesAsync(tmp, bytes, ct).ConfigureAwait(continueOnCapturedContext: false);
				File.Move(tmp, file, overwrite: true);
			}
			return (Path: file, Version: info.Version);
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
