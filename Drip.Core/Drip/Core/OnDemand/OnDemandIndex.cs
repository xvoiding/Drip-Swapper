using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Drip.Core.OnDemand;

public sealed class OnDemandIndex
{
	public CasIndex Cas { get; private set; } = new CasIndex();

	public List<OnDemandToc> Tocs { get; } = new List<OnDemandToc>();

	public bool Usable
	{
		get
		{
			if (Cas.Present)
			{
				return Tocs.Any((OnDemandToc t) => t.Runs.Any((OnDemandToc.IdRun r) => r.Usable));
			}
			return false;
		}
	}

	public static OnDemandIndex Load(string paksDirectory, string? installCacheDir = null, Action<string>? log = null)
	{
		OnDemandIndex onDemandIndex = new OnDemandIndex
		{
			Cas = CasIndex.Load(installCacheDir)
		};
		if (!onDemandIndex.Cas.Present)
		{
			log?.Invoke("Install cache index not readable: " + onDemandIndex.Cas.Error);
			return onDemandIndex;
		}
		log?.Invoke($"Install cache: {onDemandIndex.Cas.Count:N0} chunks stored ({onDemandIndex.Cas.SnapshotLocations:N0} in snapshot, +{onDemandIndex.Cas.JournalAdds:N0} journal).");
		foreach (string item in Directory.EnumerateFiles(paksDirectory, "*.uondemandtoc").OrderBy((string f) => f, StringComparer.OrdinalIgnoreCase))
		{
			try
			{
				onDemandIndex.Tocs.Add(OnDemandToc.Load(item, onDemandIndex.Cas, log));
			}
			catch (Exception ex)
			{
				log?.Invoke("  " + Path.GetFileName(item) + ": " + ex.Message);
			}
		}
		return onDemandIndex;
	}

	public void ReloadCas(string? installCacheDir = null)
	{
		Cas = CasIndex.Load(installCacheDir);
		foreach (OnDemandToc toc in Tocs)
		{
			toc.SetCas(Cas);
		}
	}

	public ChunkState CheckPackage(ulong chunkId)
	{
		bool flag = false;
		bool flag2 = false;
		foreach (OnDemandToc toc in Tocs)
		{
			switch (toc.CheckPackage(chunkId))
			{
			case ChunkState.Installed:
				return ChunkState.Installed;
			case ChunkState.NotInstalled:
				flag = true;
				break;
			case ChunkState.Unknown:
				flag2 = true;
				break;
			}
		}
		if (!flag)
		{
			if (!flag2)
			{
				return ChunkState.NotInIndex;
			}
			return ChunkState.Unknown;
		}
		return ChunkState.NotInstalled;
	}

	public string Detail(ulong chunkId)
	{
		return string.Join(" | ", from t in Tocs
			select t.Detail(chunkId) into x
			where x != "not listed"
			select x);
	}

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(23, 2, stringBuilder2);
		handler.AppendLiteral("Install cache present: ");
		handler.AppendFormatted(Cas.Present);
		handler.AppendFormatted((Cas.Error.Length > 0) ? (" (" + Cas.Error + ")") : "");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(37, 2, stringBuilder2);
		handler.AppendLiteral("  snapshot: ");
		handler.AppendFormatted(Cas.SnapshotBlocks);
		handler.AppendLiteral(" blocks, ");
		handler.AppendFormatted(Cas.SnapshotLocations, "N0");
		handler.AppendLiteral(" chunk locations");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(49, 4, stringBuilder2);
		handler.AppendLiteral("  journal: ");
		handler.AppendFormatted(Cas.JournalRecords, "N0");
		handler.AppendLiteral(" records, ");
		handler.AppendFormatted(Cas.JournalAdds, "N0");
		handler.AppendLiteral(" adds, ");
		handler.AppendFormatted(Cas.JournalBlockDeletes);
		handler.AppendLiteral(" block deletes · ops ");
		handler.AppendFormatted(string.Join(", ", from k in Cas.JournalOps
			orderby k.Key
			select $"{k.Key}:{k.Value}"));
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
		handler.AppendLiteral("  chunks stored now: ");
		handler.AppendFormatted(Cas.Count, "N0");
		stringBuilder6.AppendLine(ref handler);
		foreach (OnDemandToc toc in Tocs)
		{
			stringBuilder.Append(toc.Describe());
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
		handler.AppendLiteral("Usable: ");
		handler.AppendFormatted(Usable);
		stringBuilder7.AppendLine(ref handler);
		return stringBuilder.ToString();
	}
}
