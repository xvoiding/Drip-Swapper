using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Drip.Core.Mods;

public static class PakIndex
{
	public sealed record Result(int Version, string MountPoint, List<PakFileEntry> Files);

	private sealed class Reader
	{
		private readonly byte[] _b;

		public int Position { get; private set; }

		public Reader(byte[] b)
		{
			_b = b;
		}

		private void Need(long n)
		{
			if (n < 0 || Position + n > _b.Length)
			{
				throw new InvalidDataException("Pak index is damaged.");
			}
		}

		public void Skip(long n)
		{
			Need(n);
			Position += (int)n;
		}

		public byte Byte()
		{
			Need(1L);
			return _b[Position++];
		}

		public int Int32()
		{
			Need(4L);
			int result = BitConverter.ToInt32(_b, Position);
			Position += 4;
			return result;
		}

		public long Int64()
		{
			Need(8L);
			long result = BitConverter.ToInt64(_b, Position);
			Position += 8;
			return result;
		}

		public string FString()
		{
			int num = Int32();
			if (num == 0)
			{
				return "";
			}
			if (num < 0)
			{
				long num2 = -num * 2;
				Need(num2);
				string result = Encoding.Unicode.GetString(_b, Position, (int)num2 - 2);
				Position += (int)num2;
				return result;
			}
			Need(num);
			string result2 = Encoding.Latin1.GetString(_b, Position, num - 1);
			Position += num;
			return result2;
		}
	}

	private const uint Magic = 1517228769u;

	public static Result Read(Stream s)
	{
		long length = s.Length;
		int num = (int)Math.Min(length, 400L);
		byte[] array = new byte[num];
		s.Seek(length - num, SeekOrigin.Begin);
		s.ReadExactly(array);
		int num2 = -1;
		for (int num3 = num - 4; num3 >= 0; num3--)
		{
			if (BitConverter.ToUInt32(array, num3) == 1517228769)
			{
				num2 = num3;
				break;
			}
		}
		if (num2 < 0 || num2 + 4 + 20 > num)
		{
			throw new InvalidDataException("Not an Unreal .pak (no footer).");
		}
		int num4 = BitConverter.ToInt32(array, num2 + 4);
		long num5 = BitConverter.ToInt64(array, num2 + 8);
		long num6 = BitConverter.ToInt64(array, num2 + 16);
		if (num4 < 1 || num4 > 11)
		{
			throw new InvalidDataException($"Unknown pak version {num4}.");
		}
		if (num5 < 0 || num6 <= 0 || num5 + num6 > length || num6 > 268435456)
		{
			throw new InvalidDataException("Pak index is out of range (file damaged or encrypted).");
		}
		if (num4 >= 10)
		{
			throw new NotSupportedException($"Pak version {num4} uses a hashed index that isn't listed here.");
		}
		if (num4 >= 4 && num2 >= 1 && array[num2 - 1] != 0)
		{
			throw new NotSupportedException("The pak index is encrypted.");
		}
		byte[] array2 = new byte[num6];
		s.Seek(num5, SeekOrigin.Begin);
		s.ReadExactly(array2);
		Reader reader = new Reader(array2);
		string mountPoint = reader.FString();
		int num7 = reader.Int32();
		if (num7 < 0 || num7 > 1000000)
		{
			throw new InvalidDataException("Pak index is damaged.");
		}
		List<PakFileEntry> list = new List<PakFileEntry>(num7);
		for (int i = 0; i < num7; i++)
		{
			string path = reader.FString();
			reader.Skip(8L);
			reader.Skip(8L);
			long size = reader.Int64();
			uint num8 = (uint)((num4 >= 8) ? reader.Byte() : reader.Int32());
			if (num4 <= 1)
			{
				reader.Skip(8L);
			}
			reader.Skip(20L);
			if (num4 >= 3)
			{
				if (num8 != 0)
				{
					int num9 = reader.Int32();
					if (num9 < 0 || num9 > 1000000)
					{
						throw new InvalidDataException("Pak index is damaged.");
					}
					reader.Skip((long)num9 * 16L);
				}
				reader.Skip(1L);
				reader.Skip(4L);
			}
			list.Add(new PakFileEntry(path, size));
		}
		return new Result(num4, mountPoint, list);
	}
}
