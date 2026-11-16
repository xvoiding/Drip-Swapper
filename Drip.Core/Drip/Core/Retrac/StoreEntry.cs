namespace Drip.Core.Retrac;

public sealed record StoreEntry(ulong ExportBundlesSize, int ExportCount, int ExportBundleCount, uint LoadOrder, uint Pad, ulong[] ImportedPackages);
