using System;
using System.IO;
using CUE4Parse.Compression;

namespace Drip.Core.IoStore;

public static class OodleBridge
{
	private static readonly object Gate = new object();

	private static bool _ready;

	public static void EnsureReady()
	{
		lock (Gate)
		{
			if (!_ready)
			{
				string path = Path.Combine(AppContext.BaseDirectory, OodleHelper.OodleFileName);
				if (!File.Exists(path) && !OodleHelper.DownloadOodleDll(ref path))
				{
					throw new InvalidOperationException("Could not download the Oodle library (oo2core). Check your connection or place it next to the app.");
				}
				OodleHelper.Initialize(path);
				_ready = true;
			}
		}
	}

	public static byte[] Decompress(ReadOnlySpan<byte> src, int uncompressedSize)
	{
		EnsureReady();
		byte[] array = new byte[uncompressedSize];
		if ((long)OodleHelper.Instance.Decompress(src, array) <= 0L)
		{
			throw new InvalidDataException("Oodle decompression failed.");
		}
		return array;
	}
}
