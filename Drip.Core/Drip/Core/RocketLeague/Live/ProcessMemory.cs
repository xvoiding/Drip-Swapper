using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drip.Core.RocketLeague.Live;

public sealed class ProcessMemory : IDisposable
{
	private struct MemoryBasicInformation
	{
		public nuint BaseAddress;

		public nuint AllocationBase;

		public uint AllocationProtect;

		public ushort PartitionId;

		public nuint RegionSize;

		public uint State;

		public uint Protect;

		public uint Type;
	}

	private const uint ProcessVmRead = 16u;

	private const uint ProcessQueryInformation = 1024u;

	private const uint MemCommit = 4096u;

	private nint _handle;

	private readonly bool _canWrite;

	public Process Process { get; }

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint h);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(nint h, nuint address, byte[] buffer, nuint size, out nuint read);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(nint h, nuint address, byte[] buffer, nuint size, out nuint written);

	[DllImport("kernel32.dll")]
	private static extern nuint VirtualQueryEx(nint h, nuint address, out MemoryBasicInformation info, nuint length);

	private ProcessMemory(Process p, nint handle, bool canWrite)
	{
		Process = p;
		_handle = handle;
		_canWrite = canWrite;
	}

	public static ProcessMemory Open(Process p, bool write = false)
	{
		if (write && RlLocator.IsAntiCheatRunning())
		{
			throw new InvalidOperationException("Anti-cheat is running, so the app will not touch the game.");
		}
		nint num = OpenProcess((uint)(0x410 | (write ? 40 : 0)), inherit: false, p.Id);
		if (num == 0)
		{
			throw new InvalidOperationException($"Windows wouldn't let the app read Rocket League's memory (error {Marshal.GetLastWin32Error()}). Run the app as administrator.");
		}
		return new ProcessMemory(p, num, write);
	}

	public byte[]? Read(ulong address, int length)
	{
		if (_handle == 0 || length <= 0)
		{
			return null;
		}
		byte[] array = new byte[length];
		if (!ReadProcessMemory(_handle, (nuint)address, array, (nuint)length, out var read) || (int)read != length)
		{
			return null;
		}
		return array;
	}

	public bool Write(ulong address, byte[] data)
	{
		if (!_canWrite || _handle == 0 || data.Length == 0 || RlLocator.IsAntiCheatRunning())
		{
			return false;
		}
		if (WriteProcessMemory(_handle, (nuint)address, data, (nuint)data.Length, out var written))
		{
			return (int)written == data.Length;
		}
		return false;
	}

	public ulong? ReadU64(ulong address)
	{
		byte[] array = Read(address, 8);
		if (array != null)
		{
			return BitConverter.ToUInt64(array);
		}
		return null;
	}

	public int? ReadI32(ulong address)
	{
		byte[] array = Read(address, 4);
		if (array != null)
		{
			return BitConverter.ToInt32(array);
		}
		return null;
	}

	public IEnumerable<(ulong Base, ulong Size)> WritableRegions(ulong from, ulong to)
	{
		ulong addr = from;
		MemoryBasicInformation info;
		while (addr < to && VirtualQueryEx(_handle, (nuint)addr, out info, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) != 0)
		{
			ulong b = info.BaseAddress;
			ulong size = info.RegionSize;
			bool flag = (info.Protect & 4) != 0 || (info.Protect & 8) != 0 || (info.Protect & 0x40) != 0 || (info.Protect & 0x80) != 0;
			bool flag2 = (info.Protect & 0x100) != 0;
			if (((info.State == 4096) & flag) && !flag2)
			{
				yield return (Base: b, Size: Math.Min(size, to - Math.Min(to, b)));
			}
			if (b + size <= addr)
			{
				break;
			}
			addr = b + size;
		}
	}

	public IEnumerable<(ulong Base, ulong Size)> ReadableRegions()
	{
		ulong addr = 65536uL;
		MemoryBasicInformation info;
		while (addr < 140737488289792L && VirtualQueryEx(_handle, (nuint)addr, out info, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) != 0)
		{
			ulong b = info.BaseAddress;
			ulong size = info.RegionSize;
			uint protect = info.Protect;
			bool flag = (protect & 0xEE) != 0 && (protect & 0x100) == 0 && (protect & 1) == 0;
			if ((info.State == 4096) & flag)
			{
				yield return (Base: b, Size: size);
			}
			if (b + size <= addr)
			{
				break;
			}
			addr = b + size;
		}
	}

	public void Dispose()
	{
		if (_handle != 0)
		{
			CloseHandle(_handle);
			_handle = 0;
		}
	}
}
