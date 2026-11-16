using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Drip.Core;
using Drip.Core.Retrac;

namespace Drip.App;

public sealed class RetracGuard : IDisposable
{
	private static readonly string[] GameNames = new string[3] { "FortniteClient-Win64-Shipping", "FortniteClient-Win64-Shipping_EAC", "FortniteLauncher" };

	private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(3L);

	private readonly RetracSwapper _rt;

	private readonly string _gameRoot;

	private readonly Func<List<RetracSwap>> _desired;

	private readonly Action<string> _log;

	private readonly Action<string, string> _problem;

	private readonly CancellationTokenSource _stop = new CancellationTokenSource();

	private Task? _loop;

	private static readonly Mutex GuardOwner = new Mutex(initiallyOwned: false, "Local\\DripSwapper.RetracGuard");

	private bool _owner;

	private const uint ProcessSuspendResume = 2048u;

	public RetracGuard(RetracSwapper rt, string paksFolder, Func<List<RetracSwap>> desired, Action<string> log, Action<string, string> problem)
	{
		_rt = rt;
		_desired = desired;
		string file = Path.Combine(AppPaths.DataDir, "retrac-guard.log");
		_log = (string line) =>
		{
			try
			{
				File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {line}{Environment.NewLine}");
			}
			catch
			{
			}
			log(line);
		};
		_problem = problem;
		string text = Path.GetFullPath(paksFolder).TrimEnd(new char[2] { '\\', '/' });
		_gameRoot = ((Directory.GetParent(text)?.Parent?.Parent)?.FullName ?? text).TrimEnd('\\') + "\\";
	}

	public void Start()
	{
		if (_loop != null)
		{
			return;
		}
		CloseOtherSwappers();
		for (int i = 0; i < 20; i++)
		{
			if (_owner)
			{
				break;
			}
			try
			{
				_owner = GuardOwner.WaitOne(0);
			}
			catch (AbandonedMutexException)
			{
				_owner = true;
			}
			if (!_owner)
			{
				Thread.Sleep(150);
			}
		}
		if (!_owner)
		{
			_log("Another Drip Swapper window is already open and handling Retrac, and it wouldn't close.");
			_problem("Drip Swapper is already open", "Another swapper window is open and Drip Swapper couldn't close it. Two windows at once would collide and the swap would fail.\n\nOpen Task Manager, end every DripSwapper / Drip Swapper / Retrac Swapper entry, then open this one again.");
			_loop = Task.CompletedTask;
			return;
		}
		if (!AnyGameNamed())
		{
			Cleanup("Put Retrac's files back to normal.");
			try
			{
				_rt.Tidy();
			}
			catch
			{
			}
		}
		_loop = Task.Run((Action)Loop);
	}

	public void Stop(bool cleanUp = true)
	{
		_stop.Cancel();
		try
		{
			_loop?.Wait(2000);
		}
		catch
		{
		}
		if ((_owner & cleanUp) && !AnyGameNamed())
		{
			Cleanup(null);
		}
		if (_owner)
		{
			_owner = false;
			try
			{
				GuardOwner.ReleaseMutex();
			}
			catch
			{
			}
		}
	}

	public void Dispose()
	{
		_stop.Cancel();
	}

	private void Cleanup(string? logLine)
	{
		try
		{
			if (_rt.Active().Count != 0)
			{
				RetracResult retracResult = _rt.Undo();
				if (logLine != null && retracResult.Ok)
				{
					_log(logLine);
				}
			}
		}
		catch
		{
		}
	}

	private void Loop()
	{
		bool flag = false;
		_ = DateTime.MinValue;
		DateTime dateTime = DateTime.MinValue;
		DateTime dateTime2 = DateTime.MinValue;
		while (!_stop.IsCancellationRequested)
		{
			try
			{
				List<Process> list = GameProcesses();
				if (list.Count > 0)
				{
					dateTime = DateTime.UtcNow;
					if (!flag)
					{
						flag = true;
						_ = DateTime.Now;
						OnGameStarted(list);
					}
					foreach (Process item in list)
					{
						item.Dispose();
					}
				}
				else if (flag && DateTime.UtcNow - dateTime > SettleTime && !AnyGameNamed() && DateTime.UtcNow >= dateTime2)
				{
					if (_rt.Undo().Ok)
					{
						flag = false;
						_log("Game closed. Retrac's files are back to normal.");
					}
					else
					{
						dateTime2 = DateTime.UtcNow.AddSeconds(3.0);
					}
				}
			}
			catch
			{
			}
			try
			{
				Task.Delay(30, _stop.Token).Wait();
			}
			catch
			{
				break;
			}
		}
	}

	private void OnGameStarted(List<Process> procs)
	{
		List<RetracSwap> list = _desired();
		if (list.Count == 0)
		{
			return;
		}
		List<nint> list2 = new List<nint>();
		try
		{
			foreach (Process proc in procs)
			{
				nint num = OpenProcess(2048u, inherit: false, proc.Id);
				if (num != IntPtr.Zero)
				{
					if (NtSuspendProcess(num) == 0)
					{
						list2.Add(num);
					}
					else
					{
						CloseHandle(num);
					}
				}
			}
			_log("Retrac is starting. Loading your swaps...");
			RetracResult retracResult = _rt.Replace(list, "Swaps are in.");
			if (retracResult.Ok)
			{
				_log($"{list.Count} swap{((list.Count == 1) ? " is" : "s are")} in for this session. They come out again when the game closes.");
			}
			else
			{
				_log("ERROR: " + retracResult.Message.Replace('\n', ' '));
				_problem("Swaps didn't load", retracResult.Message);
			}
		}
		finally
		{
			foreach (nint item in list2)
			{
				NtResumeProcess(item);
				CloseHandle(item);
			}
		}
	}

	private void CloseOtherSwappers()
	{
		int processId = Environment.ProcessId;
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (process.Id != processId)
				{
					string processName = process.ProcessName;
					if (processName.StartsWith("Drip Swapper", StringComparison.OrdinalIgnoreCase) || processName.StartsWith("DripSwapper", StringComparison.OrdinalIgnoreCase) || processName.StartsWith("Retrac Swapper", StringComparison.OrdinalIgnoreCase) || processName.StartsWith("RetracSwapper", StringComparison.OrdinalIgnoreCase))
					{
						_log($"Closing another swapper window: {processName}, PID {process.Id}.");
						process.Kill(entireProcessTree: true);
						process.WaitForExit(3000);
					}
				}
			}
			catch (Exception ex)
			{
				_log("Couldn't close another swapper window: " + ex.Message);
			}
			finally
			{
				process.Dispose();
			}
		}
	}

	private static bool AnyGameNamed()
	{
		string[] gameNames = GameNames;
		for (int i = 0; i < gameNames.Length; i++)
		{
			Process[] processesByName = Process.GetProcessesByName(gameNames[i]);
			bool flag = processesByName.Length != 0;
			Process[] array = processesByName;
			for (int j = 0; j < array.Length; j++)
			{
				array[j].Dispose();
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	private List<Process> GameProcesses()
	{
		List<Process> list = new List<Process>();
		bool flag = false;
		string[] gameNames = GameNames;
		for (int i = 0; i < gameNames.Length; i++)
		{
			Process[] processesByName = Process.GetProcessesByName(gameNames[i]);
			foreach (Process process in processesByName)
			{
				string text = null;
				try
				{
					text = process.MainModule?.FileName;
				}
				catch
				{
				}
				if (text != null)
				{
					if (text.StartsWith(_gameRoot, StringComparison.OrdinalIgnoreCase))
					{
						list.Add(process);
					}
					else
					{
						process.Dispose();
					}
					continue;
				}
				flag = flag || Process.GetProcessesByName("retrac").Length != 0;
				if (flag)
				{
					list.Add(process);
				}
				else
				{
					process.Dispose();
				}
			}
		}
		return list;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint handle);

	[DllImport("ntdll.dll")]
	private static extern int NtSuspendProcess(nint handle);

	[DllImport("ntdll.dll")]
	private static extern int NtResumeProcess(nint handle);
}
