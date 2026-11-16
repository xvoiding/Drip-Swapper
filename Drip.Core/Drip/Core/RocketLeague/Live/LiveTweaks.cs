using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Drip.Core.RocketLeague.Live;

public static class LiveTweaks
{
	private sealed class Target
	{
		public HashSet<int> Classes = new HashSet<int>();

		public HashSet<int> Objects = new HashSet<int>();

		public required string Class { get; init; }
	}

	private sealed record ScaleSpec(string Id, string Label, (string Class, string[] Fields)[] Targets, Func<float> Get, string Waits);

	private sealed class Curve
	{
		public ulong Data;

		public int Stride;

		public int Count;

		public int Off;

		public byte[] Orig = Array.Empty<byte>();

		public float AppliedLast;

		public string Where = "";
	}

	private static volatile bool _unlimitedBoost;

	private static volatile bool _unlimitedJumps;

	private static volatile bool _cantLose;

	private static volatile bool _resetNow;

	private static volatile bool _reportNow;

	private static float _carSpeed = 1f;

	private static float _jumpScale = 1f;

	private static float _carSize = 1f;

	private static float _ballSize = 1f;

	private static float _ballWeight = 1f;

	private static float _ballBounce = 1f;

	private static float _gameSpeed = 1f;

	private static int _pendingMinutes = -1;

	private static volatile int _myTeam;

	private static volatile int _curveCount;

	private static readonly object Gate = new object();

	private static CancellationTokenSource? _cts;

	private static Task? _worker;

	private static string _status = "Off.";

	private static string _report = "";

	private static volatile bool _autoReported;

	private static volatile bool _funReported;

	private static readonly Regex JumpFlag = new Regex("^b(Has)?(Double)?(Jumped|Dodged|Flipped|Jump(ed)?Used|DodgeUsed|FlipUsed|DoubleJumpUsed)$|^b(Jump|Dodge|Flip|DoubleJump)Used$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex TeamNumName = new Regex("^(TeamIndex|TeamNum|TeamNumber|Team_Index)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly string[] JumpClasses = new string[5] { "Car_TA", "CarComponent_Jump_TA", "CarComponent_DoubleJump_TA", "CarComponent_Dodge_TA", "CarComponent_FlipCar_TA" };

	private static readonly string[] JumpPower = new string[4] { "JumpImpulse", "JumpForce", "DoubleJumpImpulse", "ImpulseScale" };

	private static readonly (string Class, string[] Fields)[] SpeedFields = new (string, string[])[3]
	{
		("VehicleSim_TA", new string[4] { "DriveTorque", "ThrottleForce", "MaxSpeed", "MaxLinearSpeed" }),
		("CarComponent_Boost_TA", new string[3] { "BoostForce", "BoostPushForce", "MaxBoostSpeed" }),
		("Car_TA", new string[1] { "MaxLinearSpeed" })
	};

	private static readonly ScaleSpec[] ScaleSpecs = new ScaleSpec[5]
	{
		new ScaleSpec("carsize", "car size", new (string, string[])[1] { ("Car_TA", new string[2] { "DrawScale", "ReplicatedCarScale" }) }, () => _carSize, "a car"),
		new ScaleSpec("ballsize", "ball size", new (string, string[])[1] { ("Ball_TA", new string[2] { "DrawScale", "ReplicatedBallScale" }) }, () => _ballSize, "the ball"),
		new ScaleSpec("gravity", "gravity", new (string, string[])[2]
		{
			("WorldInfo", new string[1] { "WorldGravityZ" }),
			("Ball_TA", new string[1] { "ReplicatedBallGravityScale" })
		}, () => _ballWeight, "a match"),
		new ScaleSpec("ballbounce", "ball bounce", new (string, string[])[1] { ("Ball_TA", new string[1] { "ReplicatedWorldBounceScale" }) }, () => _ballBounce, "the ball"),
		new ScaleSpec("gamespeed", "game speed", new (string, string[])[1] { ("WorldInfo", new string[1] { "TimeDilation" }) }, () => _gameSpeed, "a match")
	};

	private static string _funNote = "";

	private static readonly string[] CurveClasses = new string[2] { "VehicleSim_TA", "Car_TA" };

	private static readonly int[] CurveStrides = new int[4] { 20, 16, 12, 8 };

	public static string Status
	{
		get
		{
			lock (Gate)
			{
				return _status;
			}
		}
	}

	public static string Report
	{
		get
		{
			lock (Gate)
			{
				return _report;
			}
		}
	}

	public static bool Running
	{
		get
		{
			Task worker = _worker;
			if (worker != null)
			{
				return !worker.IsCompleted;
			}
			return false;
		}
	}

	private static bool WantSpeed => Math.Abs(_carSpeed - 1f) > 0.001f;

	private static bool WantJump => Math.Abs(_jumpScale - 1f) > 0.001f;

	private static bool AnyFunScale => ScaleSpecs.Any((ScaleSpec sp) => Math.Abs(sp.Get() - 1f) > 0.001f);

	private static bool AnythingWanted
	{
		get
		{
			if (!AnyFunScale && !WantSpeed && !WantJump && !_unlimitedBoost && !_unlimitedJumps && !_cantLose && _pendingMinutes <= 0 && !_resetNow)
			{
				return _reportNow;
			}
			return true;
		}
	}

	private static void SaveReport(string text)
	{
		try
		{
			string text2 = Path.Combine(AppPaths.DataDir, "RocketLeague");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "tweaks-report.txt"), DateTime.Now.ToString("u") + "\n" + text);
		}
		catch
		{
		}
	}

	private static void SetStatus(string s)
	{
		lock (Gate)
		{
			_status = s;
		}
	}

	public static void SetCarSpeed(float v)
	{
		_carSpeed = Math.Clamp(v, 0.25f, 4f);
		Ensure();
	}

	public static void SetJumpScale(float v)
	{
		_jumpScale = Math.Clamp(v, 1f, 8f);
		Ensure();
	}

	public static void SetUnlimitedBoost(bool on)
	{
		_unlimitedBoost = on;
		Ensure();
	}

	public static void SetUnlimitedJumps(bool on)
	{
		_unlimitedJumps = on;
		Ensure();
	}

	public static void SetCantLose(bool on)
	{
		_cantLose = on;
		if (on)
		{
			_autoReported = false;
		}
		Ensure();
	}

	public static void SetMyTeam(int team)
	{
		_myTeam = ((team == 1) ? 1 : 0);
	}

	public static void SetCarSize(float v)
	{
		_carSize = Math.Clamp(v, 0.5f, 4f);
		Ensure();
	}

	public static void SetBallSize(float v)
	{
		_ballSize = Math.Clamp(v, 0.5f, 4f);
		Ensure();
	}

	public static void SetBallWeight(float v)
	{
		_ballWeight = Math.Clamp(v, 0.1f, 2f);
		Ensure();
	}

	public static void SetBallBounce(float v)
	{
		_ballBounce = Math.Clamp(v, 0.5f, 3f);
		Ensure();
	}

	public static void SetGameSpeed(float v)
	{
		_gameSpeed = Math.Clamp(v, 0.2f, 2f);
		Ensure();
	}

	public static void SetMinutes(int minutes)
	{
		_pendingMinutes = Math.Clamp(minutes, 1, 999);
		Ensure();
	}

	public static void RequestReport()
	{
		_reportNow = true;
		Ensure();
	}

	public static void ResetAutoReport()
	{
		_autoReported = false;
	}

	public static void ResetAll()
	{
		_carSpeed = 1f;
		_jumpScale = 1f;
		_unlimitedBoost = false;
		_unlimitedJumps = false;
		_cantLose = false;
		_pendingMinutes = -1;
		_carSize = 1f;
		_ballSize = 1f;
		_ballWeight = 1f;
		_ballBounce = 1f;
		_gameSpeed = 1f;
		_funReported = false;
		if (!Running)
		{
			_resetNow = false;
			SetStatus("Off.");
		}
		else
		{
			_resetNow = true;
		}
	}

	public static void Shutdown()
	{
		_carSpeed = 1f;
		_jumpScale = 1f;
		_unlimitedBoost = false;
		_unlimitedJumps = false;
		_cantLose = false;
		_pendingMinutes = -1;
		_resetNow = false;
		_carSize = 1f;
		_ballSize = 1f;
		_ballWeight = 1f;
		_ballBounce = 1f;
		_gameSpeed = 1f;
		_reportNow = false;
		CancellationTokenSource cts = _cts;
		_cts = null;
		try
		{
			cts?.Cancel();
		}
		catch
		{
		}
	}

	private static void Ensure()
	{
		lock (Gate)
		{
			Task worker = _worker;
			if (worker != null && !worker.IsCompleted)
			{
				CancellationTokenSource cts = _cts;
				if (cts != null && !cts.IsCancellationRequested)
				{
					return;
				}
			}
			if (AnythingWanted)
			{
				_cts = new CancellationTokenSource();
				CancellationToken token = _cts.Token;
				_worker = Task.Run(() =>
				{
					Work(token);
				});
			}
		}
	}

	public static bool LooksLikeSpeedCurve(float[] x, float[] y)
	{
		int num = x.Length;
		if (num < 2 || num > 16)
		{
			return false;
		}
		for (int i = 0; i < num; i++)
		{
			if (!float.IsFinite(x[i]) || !float.IsFinite(y[i]))
			{
				return false;
			}
		}
		if (Math.Abs(x[0]) > 0.5f)
		{
			return false;
		}
		for (int j = 1; j < num; j++)
		{
			if (x[j] <= x[j - 1])
			{
				return false;
			}
		}
		if (x[num - 1] < 1300f || x[num - 1] > 2400f)
		{
			return false;
		}
		if (y[0] < 0.4f || y[0] > 1.6f)
		{
			return false;
		}
		if (y[num - 1] < -0.01f || y[num - 1] > 0.25f)
		{
			return false;
		}
		for (int k = 0; k < num; k++)
		{
			if (y[k] < -0.01f || y[k] > 1.6f)
			{
				return false;
			}
		}
		return true;
	}

	private static List<Curve> FindSpeedCurves(GameContext c, ulong obj, string cls, StringBuilder? log)
	{
		List<Curve> list = new List<Curve>();
		byte[] array = c.Mem.Read(obj, 2048);
		if (array == null)
		{
			return list;
		}
		for (int i = 56; i + 16 <= array.Length; i += 8)
		{
			ulong num = BitConverter.ToUInt64(array, i);
			int num2 = BitConverter.ToInt32(array, i + 8);
			int num3 = BitConverter.ToInt32(array, i + 12);
			if (num2 < 2 || num2 > 16 || num3 < num2 || num3 > 64 || num < 65536 || num > 140737488355327L || (num & 7) != 0L)
			{
				continue;
			}
			int[] curveStrides = CurveStrides;
			foreach (int num4 in curveStrides)
			{
				byte[] array2 = c.Mem.Read(num, num2 * num4);
				if (array2 == null)
				{
					continue;
				}
				float[] x = new float[num2];
				float[] array3 = new float[num2];
				for (int k = 0; k < num2; k++)
				{
					x[k] = BitConverter.ToSingle(array2, k * num4);
					array3[k] = BitConverter.ToSingle(array2, k * num4 + 4);
				}
				if (LooksLikeSpeedCurve(x, array3))
				{
					list.Add(new Curve
					{
						Data = num,
						Stride = num4,
						Count = num2,
						Off = i,
						Orig = array2,
						AppliedLast = x[num2 - 1],
						Where = $"{cls}+0x{i:X}"
					});
					log?.AppendLine($"   speed table at {cls}+0x{i:X}: " + string.Join(", ", x.Zip(array3, (float a, float b) => $"{a:0.#}->{b:0.###}")));
					break;
				}
				if (log != null && num4 == 20 && x.All(float.IsFinite) && Math.Abs(x[0]) < 1f && Enumerable.Range(1, num2 - 1).All((int num5) => x[num5] > x[num5 - 1]))
				{
					log.AppendLine($"   rising table at {cls}+0x{i:X} ({num2} points): " + string.Join(", ", x.Take(6).Zip(array3, (float a, float b) => $"{a:0.#}->{b:0.###}")));
				}
			}
		}
		return list;
	}

	private static bool WriteCurve(GameContext c, Curve cv, float factor)
	{
		byte[] array = c.Mem.Read(cv.Data + (ulong)((cv.Count - 1) * cv.Stride), 4);
		if (array == null)
		{
			return false;
		}
		float num = BitConverter.ToSingle(array, 0);
		float num2 = BitConverter.ToSingle(cv.Orig, (cv.Count - 1) * cv.Stride);
		if (Math.Abs(num - cv.AppliedLast) > 0.5f && Math.Abs(num - num2) > 0.5f)
		{
			return false;
		}
		byte[] array2 = (byte[])cv.Orig.Clone();
		for (int i = 0; i < cv.Count; i++)
		{
			int num3 = i * cv.Stride;
			BitConverter.GetBytes(BitConverter.ToSingle(cv.Orig, num3) * factor).CopyTo(array2, num3);
			if (cv.Stride >= 16)
			{
				for (int j = 8; j <= 12; j += 4)
				{
					BitConverter.GetBytes(BitConverter.ToSingle(cv.Orig, num3 + j) / factor).CopyTo(array2, num3 + j);
				}
			}
		}
		if (!c.Mem.Write(cv.Data, array2))
		{
			return false;
		}
		cv.AppliedLast = num2 * factor;
		return true;
	}

	private static void Work(CancellationToken ct)
	{
		GameContext c = null;
		try
		{
			SetStatus("Connecting to the game…");
			c = GameContext.Attach(write: true, out string error);
			if (c == null)
			{
				SetStatus(error);
				return;
			}
			GameReflection refl = GameReflection.Build(c, out error);
			if (refl == null)
			{
				SetStatus(error + " Nothing was changed.");
				return;
			}
			Dictionary<string, Target> targets = new Dictionary<string, Target>();
			Target target = T("CarComponent_Boost_TA");
			Target target2 = T("GameEvent_Soccar_TA");
			Target target3 = T("Team_TA");
			Target target4 = T("Ball_TA");
			foreach (string item5 in JumpClasses.Concat(SpeedFields.Select(((string Class, string[] Fields) s) => s.Class)).Concat(ScaleSpecs.SelectMany((ScaleSpec s) => s.Targets.Select(((string Class, string[] Fields) t) => t.Class))))
			{
				T(item5);
			}
			Scan(Enumerable.Range(0, c.Count));
			GameReflection.Prop prop = refl.Find("CarComponent_Boost_TA", "bUnlimitedBoost");
			GameReflection.Prop prop2 = refl.Find("CarComponent_Boost_TA", "MaxBoostAmount");
			GameReflection.Prop prop3 = refl.Find("CarComponent_Boost_TA", "CurrentBoostAmount");
			GameReflection.Prop prop4 = refl.Find("GameEvent_Soccar_TA", "SecondsRemaining");
			GameReflection.Prop prop5 = refl.Find("GameEvent_Soccar_TA", "GameTime");
			GameReflection.Prop prop6 = refl.Find("Team_TA", "Score");
			GameReflection.Prop prop7 = refl.Find("GameEvent_Soccar_TA", "bGoalsEnabled");
			GameReflection.Prop prop8 = refl.Find("Ball_TA", "Location");
			GameReflection.Prop prop9 = ((prop8 == null) ? null : prop8 with
			{
				Type = "FloatProperty",
				Offset = prop8.Offset + 4,
				Size = 4,
				Mask = 0u
			});
			bool flag = false;
			int num = 0;
			GameReflection.Prop prop10 = refl.All("Team_TA").FirstOrDefault((GameReflection.Prop p) => (p.IsInt || p.Type == "ByteProperty") && TeamNumName.IsMatch(p.Name));
			Dictionary<string, List<GameReflection.Prop>> dictionary = JumpClasses.ToDictionary((string n) => n, (string n) => (from p in refl.All(n)
				where p.IsBool && p.Mask != 0 && JumpFlag.IsMatch(p.Name)
				select p).ToList());
			List<Curve> list = new List<Curve>();
			HashSet<(int, ulong)> hashSet = new HashSet<(int, ulong)>();
			SetStatus("Connected to the game.");
			Dictionary<(int, ulong, string), bool> origBool = new Dictionary<(int, ulong, string), bool>();
			Dictionary<(int, ulong, string), float> origFloat = new Dictionary<(int, ulong, string), float>();
			Dictionary<(int, ulong), int> dictionary2 = new Dictionary<(int, ulong), int>();
			Dictionary<(int, ulong), int> dictionary3 = new Dictionary<(int, ulong), int>();
			Stopwatch stopwatch = Stopwatch.StartNew();
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			long num5 = 0L;
			long num6 = 0L;
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			bool flag5 = false;
			HashSet<string> hashSet2 = new HashSet<string>();
			long num7 = 0L;
			string text = "";
			while (!ct.IsCancellationRequested)
			{
				if (stopwatch.ElapsedMilliseconds - num3 > 700)
				{
					num3 = stopwatch.ElapsedMilliseconds;
					if (!RlLocator.IsRunning())
					{
						SetStatus("Rocket League closed.");
						return;
					}
					if (RlLocator.IsAntiCheatRunning())
					{
						SetStatus("Anti-cheat started, so the app stopped touching the game.");
						return;
					}
				}
				if (stopwatch.ElapsedMilliseconds - num2 > 1000)
				{
					num2 = stopwatch.ElapsedMilliseconds;
					Scan(c.Refresh());
					foreach (var item6 in origBool.Keys.Where(((int, ulong, string) k) => !c.IsObject(k.Item2, out var index2) || index2 != k.Item1).ToList())
					{
						origBool.Remove(item6);
					}
					foreach (var item7 in origFloat.Keys.Where(((int, ulong, string) k) => !c.IsObject(k.Item2, out var index2) || index2 != k.Item1).ToList())
					{
						origFloat.Remove(item7);
					}
					foreach (var item8 in dictionary3.Keys.Where(((int, ulong) k) => !c.IsObject(k.Item2, out var index2) || index2 != k.Item1).ToList())
					{
						dictionary3.Remove(item8);
					}
				}
				bool resetNow = _resetNow;
				bool flag6 = WantSpeed && !resetNow;
				if (flag6 | flag2)
				{
					(string, string[])[] speedFields = SpeedFields;
					for (int num8 = 0; num8 < speedFields.Length; num8++)
					{
						var (cls, fields) = speedFields[num8];
						Scale(cls, fields, _carSpeed, flag6);
					}
				}
				if (flag6)
				{
					string[] curveClasses = CurveClasses;
					foreach (string text2 in curveClasses)
					{
						foreach (int item9 in T(text2).Objects.ToList())
						{
							if (hashSet.Add((item9, c.AddressOf(item9))))
							{
								list.AddRange(FindSpeedCurves(c, c.AddressOf(item9), text2, null));
							}
						}
					}
					list.RemoveAll((Curve cv) => !WriteCurve(c, cv, _carSpeed));
				}
				else if (flag2)
				{
					foreach (Curve item10 in list)
					{
						WriteCurve(c, item10, 1f);
					}
					list.Clear();
					hashSet.Clear();
				}
				_curveCount = list.Count;
				flag2 = flag6;
				ScaleSpec[] scaleSpecs = ScaleSpecs;
				foreach (ScaleSpec scaleSpec in scaleSpecs)
				{
					float num9 = scaleSpec.Get();
					bool flag7 = Math.Abs(num9 - 1f) > 0.001f && !resetNow;
					if (flag7 || hashSet2.Contains(scaleSpec.Id))
					{
						(string, string[])[] speedFields = scaleSpec.Targets;
						for (int num10 = 0; num10 < speedFields.Length; num10++)
						{
							(string, string[]) tuple2 = speedFields[num10];
							Scale(tuple2.Item1, tuple2.Item2, num9, flag7);
						}
					}
					if (flag7)
					{
						hashSet2.Add(scaleSpec.Id);
					}
					else
					{
						hashSet2.Remove(scaleSpec.Id);
					}
				}
				if (hashSet2.Count == 0)
				{
					num6 = 0L;
				}
				else if (num6 == 0L)
				{
					num6 = Math.Max(1L, stopwatch.ElapsedMilliseconds);
				}
				else if (!_funReported && stopwatch.ElapsedMilliseconds - num6 > 3000)
				{
					_funReported = true;
					_reportNow = true;
				}
				if (stopwatch.ElapsedMilliseconds - num5 > 1000)
				{
					num5 = stopwatch.ElapsedMilliseconds;
					List<string> list2 = new List<string>();
					scaleSpecs = ScaleSpecs;
					foreach (ScaleSpec scaleSpec2 in scaleSpecs)
					{
						if ((Math.Abs(scaleSpec2.Get() - 1f) <= 0.001f) | resetNow)
						{
							continue;
						}
						List<string> list3 = new List<string>();
						(string, string[])[] speedFields = scaleSpec2.Targets;
						for (int num10 = 0; num10 < speedFields.Length; num10++)
						{
							(string, string[]) tuple3 = speedFields[num10];
							string item = tuple3.Item1;
							string[] item2 = tuple3.Item2;
							int num11 = T(item).Objects.FirstOrDefault((int i) => true, -1);
							string[] curveClasses = item2;
							foreach (string text3 in curveClasses)
							{
								GameReflection.Prop prop11 = refl.Find(item, text3);
								if ((object)prop11 == null || !prop11.IsFloat)
								{
									list3.Add(item + "." + text3 + " missing");
									continue;
								}
								float? num13 = ((num11 >= 0) ? refl.ReadFloat(num11, prop11) : ((float?)null));
								list3.Add((!num13.HasValue) ? (text3 + " " + ((num11 < 0) ? "(no object)" : "unread")) : $"{text3}={num13.Value:0.###}");
							}
						}
						list2.Add(scaleSpec2.Label + ": " + string.Join(", ", list3));
					}
					lock (Gate)
					{
						_funNote = ((list2.Count == 0) ? "" : (" [" + string.Join("; ", list2) + "]"));
					}
				}
				bool flag8 = _unlimitedBoost && !resetNow;
				if (flag8 | flag4)
				{
					foreach (int item11 in target.Objects.ToList())
					{
						if (prop != null && prop.Mask != 0)
						{
							SetFlag("Boost", item11, prop, flag8, true);
						}
						else if (flag8 && prop2 != null && prop3 != null)
						{
							float? num14 = refl.ReadFloat(item11, prop2);
							float? num15 = refl.ReadFloat(item11, prop3);
							if (num14.HasValue && num14.GetValueOrDefault() > 0f && num15.HasValue && num15.Value < num14.Value - 0.0001f)
							{
								refl.WriteFloat(item11, prop3, num14.Value);
							}
						}
					}
				}
				flag4 = flag8;
				bool flag9 = WantJump && !resetNow;
				if (flag9 | flag3)
				{
					foreach (string item12 in JumpClasses.Where((string n) => n.Contains("Jump", StringComparison.Ordinal) || n.Contains("Dodge", StringComparison.Ordinal)))
					{
						Scale(item12, JumpPower, _jumpScale, flag9);
					}
				}
				flag3 = flag9;
				if (_unlimitedJumps && !resetNow)
				{
					string[] curveClasses = JumpClasses;
					foreach (string text4 in curveClasses)
					{
						foreach (int item13 in T(text4).Objects.ToList())
						{
							foreach (GameReflection.Prop item14 in dictionary[text4])
							{
								if (refl.ReadBool(item13, item14) == true)
								{
									refl.WriteBool(item13, item14, on: false);
								}
							}
						}
					}
				}
				bool flag10 = _cantLose && !resetNow;
				if (flag10 && !flag5)
				{
					num7 = stopwatch.ElapsedMilliseconds;
				}
				if (flag10 | flag5)
				{
					int num16 = 0;
					if ((prop6 != null) & flag10)
					{
						List<int> list4 = target3.Objects.OrderBy((int i) => i).Take(2).ToList();
						foreach (int item15 in target3.Objects.ToList())
						{
							int num17 = list4.IndexOf(item15);
							int? num18;
							bool flag11;
							if (prop10 != null)
							{
								num18 = (prop10.IsInt ? refl.ReadInt(item15, prop10) : refl.ReadByte(item15, prop10));
								if (num18.HasValue)
								{
									int num8 = num18.GetValueOrDefault();
									if ((uint)num8 <= 1u)
									{
										flag11 = true;
										goto IL_0dc2;
									}
								}
								flag11 = false;
								goto IL_0dc2;
							}
							goto IL_0dcf;
							IL_0dc2:
							if (flag11)
							{
								num17 = num18.Value;
							}
							goto IL_0dcf;
							IL_0dcf:
							if ((uint)num17 > 1u)
							{
								continue;
							}
							int? num19 = refl.ReadInt(item15, prop6);
							if (!num19.HasValue)
							{
								continue;
							}
							(int, ulong) key = (item15, c.AddressOf(item15));
							if (num17 == _myTeam)
							{
								dictionary3[key] = num19.Value;
								continue;
							}
							num16++;
							if (!dictionary3.TryGetValue(key, out var value))
							{
								dictionary3[key] = num19.Value;
							}
							else if (num19.Value != value)
							{
								if (num19.Value > value)
								{
									refl.WriteInt(item15, prop6, value);
								}
								else
								{
									dictionary3[key] = num19.Value;
								}
							}
						}
					}
					if (prop7 != null && prop9 != null)
					{
						float? num20 = null;
						foreach (int item16 in target4.Objects.ToList())
						{
							float? num21 = refl.ReadFloat(item16, prop9);
							if (num21.HasValue)
							{
								float valueOrDefault = num21.GetValueOrDefault();
								if (float.IsFinite(valueOrDefault) && Math.Abs(valueOrDefault) < 7000f)
								{
									num20 = valueOrDefault;
									break;
								}
							}
						}
						if (num20.HasValue)
						{
							num++;
							float num22 = ((_myTeam == 0) ? (0f - num20.Value) : num20.Value);
							bool flag12 = flag10 && (flag ? (num22 > 4000f) : (num22 > 4300f));
							if (flag12 != flag)
							{
								foreach (int item17 in target2.Objects.ToList())
								{
									refl.WriteBool(item17, prop7, !flag12);
								}
								flag = flag12;
							}
						}
					}
					if (!flag10)
					{
						dictionary3.Clear();
					}
					if (flag10 && !_autoReported && (prop6 == null || num16 == 0) && stopwatch.ElapsedMilliseconds - num7 > 4000)
					{
						_autoReported = true;
						_reportNow = true;
					}
					string text5 = ((_myTeam == 0) ? "blue" : "orange");
					if (flag10)
					{
						if (prop6 == null)
						{
							text = " Can't lose: the score field wasn't found.";
						}
						else
						{
							text = ((num16 == 0) ? (" Can't lose is waiting for the teams (start a match as " + text5 + ").") : $" Can't lose on: balls into the {text5} goal do nothing ({((prop7 == null) ? "goal switch not found" : ((prop9 == null) ? "ball position not found" : ((num == 0) ? "waiting for the ball" : "ball tracked")))}).");
						}
					}
					else
					{
						text = "";
					}
				}
				if ((!flag10 & flag) && prop7 != null)
				{
					foreach (int item18 in target2.Objects.ToList())
					{
						refl.WriteBool(item18, prop7, on: true);
					}
					flag = false;
				}
				flag5 = flag10;
				int pendingMinutes = _pendingMinutes;
				if (pendingMinutes > 0 && !resetNow)
				{
					_pendingMinutes = -1;
					int num23 = 0;
					foreach (int item19 in target2.Objects.ToList())
					{
						ulong item3 = c.AddressOf(item19);
						if (!dictionary2.ContainsKey((item19, item3)) && prop5 != null)
						{
							int? num24 = refl.ReadInt(item19, prop5);
							if (num24.HasValue)
							{
								int valueOrDefault2 = num24.GetValueOrDefault();
								if (valueOrDefault2 > 0)
								{
									dictionary2[(item19, item3)] = valueOrDefault2;
								}
							}
						}
						if (prop4 != null && refl.WriteInt(item19, prop4, pendingMinutes * 60))
						{
							num23++;
						}
						if (prop5 != null)
						{
							refl.WriteInt(item19, prop5, pendingMinutes * 60);
						}
					}
					SetStatus((num23 > 0) ? $"Match clock set to {pendingMinutes} minute(s) (SecondsRemaining {((prop4 != null) ? "found" : "missing")}, GameTime {((prop5 != null) ? "found" : "missing")})." : $"Couldn't find the match clock (SecondsRemaining {((prop4 != null) ? "found" : "missing")}, GameTime {((prop5 != null) ? "found" : "missing")}, match objects {target2.Objects.Count}). Start a bot match first, then press Set again.");
					if (num23 == 0)
					{
						_reportNow = true;
					}
				}
				if (resetNow)
				{
					foreach (KeyValuePair<(int, ulong), int> item20 in dictionary2.ToList())
					{
						if (c.IsObject(item20.Key.Item2, out var index) && index == item20.Key.Item1)
						{
							if (prop5 != null)
							{
								refl.WriteInt(index, prop5, item20.Value);
							}
							if (prop4 != null)
							{
								refl.WriteInt(index, prop4, item20.Value);
							}
						}
					}
					dictionary2.Clear();
					_resetNow = false;
					SetStatus("Back to default.");
				}
				if (_reportNow)
				{
					_reportNow = false;
					string text6 = BuildReport(c, refl, targets);
					lock (Gate)
					{
						_report = text6;
					}
					SaveReport(text6);
				}
				if (!AnythingWanted && !flag2 && !flag4 && !flag3 && !flag5 && hashSet2.Count == 0)
				{
					if (ct.WaitHandle.WaitOne(1500))
					{
						break;
					}
					if (!AnythingWanted)
					{
						return;
					}
				}
				else
				{
					if (!resetNow && stopwatch.ElapsedMilliseconds - num4 > 1000)
					{
						num4 = stopwatch.ElapsedMilliseconds;
						SetStatus(StatusLine(refl, targets, prop, prop2, prop3) + text);
					}
					ct.WaitHandle.WaitOne(30);
				}
			}
			if (!flag || !(prop7 != null))
			{
				return;
			}
			foreach (int item21 in target2.Objects.ToList())
			{
				refl.WriteBool(item21, prop7, on: true);
			}
			bool Eligible(int i, Target t)
			{
				bool flag13 = refl.IsLiveObject(i, t.Classes) && !c.Nm(i).StartsWith("Default__", StringComparison.Ordinal);
				if (flag13)
				{
					string text7 = c.Root(i);
					bool flag14 = ((text7 == "Archetypes" || text7 == "TAGame") ? true : false);
					flag13 = !flag14;
				}
				return flag13;
			}
			void Scale(string text7, IEnumerable<string> enumerable, float factor, bool on)
			{
				foreach (int item22 in T(text7).Objects.ToList())
				{
					foreach (string item23 in enumerable)
					{
						GameReflection.Prop prop12 = refl.Find(text7, item23);
						if ((object)prop12 != null && prop12.IsFloat)
						{
							ulong item4 = c.AddressOf(item22);
							float? num25 = refl.ReadFloat(item22, prop12);
							if (num25.HasValue)
							{
								(int, ulong, string) key2 = (item22, item4, text7 + "." + item23);
								float value3;
								if (on)
								{
									if (!origFloat.TryGetValue(key2, out var value2))
									{
										value2 = num25.Value;
										origFloat[key2] = value2;
									}
									float num26 = ((Math.Abs(value2) < 1E-06f) ? 1f : value2) * factor;
									if (Math.Abs(num25.Value - num26) > 0.001f * Math.Max(1f, Math.Abs(num26)))
									{
										refl.WriteFloat(item22, prop12, num26);
									}
								}
								else if (origFloat.TryGetValue(key2, out value3))
								{
									if (Math.Abs(num25.Value - value3) > 0.0001f)
									{
										refl.WriteFloat(item22, prop12, value3);
									}
									origFloat.Remove(key2);
								}
							}
						}
					}
				}
			}
			void Scan(IEnumerable<int> indexes)
			{
				foreach (int index3 in indexes)
				{
					foreach (Target value4 in targets.Values)
					{
						value4.Objects.Remove(index3);
					}
					if (c.Readable(index3))
					{
						int item4 = c.ClassOf(index3);
						foreach (Target value5 in targets.Values)
						{
							if (value5.Classes.Contains(item4) && Eligible(index3, value5))
							{
								value5.Objects.Add(index3);
							}
						}
					}
				}
			}
			void SetFlag(string text7, int i, GameReflection.Prop p, bool on, bool flag14)
			{
				ulong item4 = c.AddressOf(i);
				bool? flag13 = refl.ReadBool(i, p);
				if (flag13.HasValue)
				{
					(int, ulong, string) key2 = (i, item4, text7 + "." + p.Name);
					bool value2;
					if (on)
					{
						if (!origBool.ContainsKey(key2))
						{
							origBool[key2] = flag13.Value;
						}
						if (flag13.Value != flag14)
						{
							refl.WriteBool(i, p, flag14);
						}
					}
					else if (origBool.TryGetValue(key2, out value2))
					{
						if (flag13.Value != value2)
						{
							refl.WriteBool(i, p, value2);
						}
						origBool.Remove(key2);
					}
				}
			}
			Target T(string name)
			{
				if (!targets.TryGetValue(name, out Target value2))
				{
					value2 = new Target
					{
						Class = name
					};
					value2.Classes = refl.ClassesOf(name);
					targets[name] = value2;
				}
				return value2;
			}
		}
		catch (Exception ex)
		{
			SetStatus("Stopped: " + ex.Message);
		}
		finally
		{
			c?.Dispose();
		}
	}

	private static string StatusLine(GameReflection refl, Dictionary<string, Target> t, GameReflection.Prop? pUnlimitedBoost, GameReflection.Prop? pMaxBoost, GameReflection.Prop? pCurBoost)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (WantSpeed)
		{
			if (SpeedFields.Sum(((string Class, string[] Fields) s) => s.Fields.Count((string f) => refl.Find(s.Class, f)?.IsFloat ?? false)) == 0)
			{
				list2.Add("couldn't find the car speed in the game");
			}
			else if (t["Car_TA"].Objects.Count == 0)
			{
				list2.Add("car speed is waiting for a car (start freeplay)");
			}
			else
			{
				list.Add($"car speed {_carSpeed:0.#}x ({((_curveCount > 0) ? "top speed, " : "")}push and boost)");
			}
		}
		if (_unlimitedBoost)
		{
			if (pUnlimitedBoost == null && (pMaxBoost == null || pCurBoost == null))
			{
				list2.Add("couldn't find the boost in the game");
			}
			else if (t["CarComponent_Boost_TA"].Objects.Count == 0)
			{
				list2.Add("boost is waiting for a car (start freeplay)");
			}
			else
			{
				list.Add("unlimited boost");
			}
		}
		if (WantJump)
		{
			list.Add($"jump power {_jumpScale:0.#}x");
		}
		ScaleSpec[] scaleSpecs = ScaleSpecs;
		foreach (ScaleSpec scaleSpec in scaleSpecs)
		{
			float num2 = scaleSpec.Get();
			if (!(Math.Abs(num2 - 1f) <= 0.001f))
			{
				if (!scaleSpec.Targets.Any(((string Class, string[] Fields) tg) => tg.Fields.Any((string n) => refl.Find(tg.Class, n)?.IsFloat ?? false)))
				{
					list2.Add("couldn't find " + scaleSpec.Label + " in the game");
					continue;
				}
				if (scaleSpec.Targets.All(((string Class, string[] Fields) tg) => t.TryGetValue(tg.Class, out Target value) && value.Objects.Count == 0))
				{
					list2.Add(scaleSpec.Label + " is waiting for " + scaleSpec.Waits + " (start freeplay)");
					continue;
				}
				list.Add($"{scaleSpec.Label} {num2:0.#}x");
			}
		}
		if (_unlimitedJumps)
		{
			list.Add("unlimited jumps");
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (list.Count > 0)
		{
			stringBuilder.Append("On: " + string.Join(", ", list) + ". ");
		}
		if (list2.Count > 0)
		{
			stringBuilder.Append("Not working yet: " + string.Join("; ", list2) + ". ");
		}
		string funNote;
		lock (Gate)
		{
			funNote = _funNote;
		}
		if (stringBuilder.Length != 0)
		{
			return stringBuilder.ToString().TrimEnd() + funNote;
		}
		return "Connected to the game.";
	}

	private static string BuildReport(GameContext c, GameReflection refl, Dictionary<string, Target> targets)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(refl.Summary);
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		foreach (Target value4 in targets.Values)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(29, 3, stringBuilder2);
			handler.AppendFormatted(value4.Class);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(value4.Objects.Count);
			handler.AppendLiteral(" live object(s), ");
			handler.AppendFormatted(value4.Classes.Count);
			handler.AppendLiteral(" class(es)");
			stringBuilder3.AppendLine(ref handler);
		}
		foreach (Target item in targets.Values.Where((Target t) => t.Classes.Count > 0 || refl.ClassIndex(t.Class).HasValue))
		{
			stringBuilder.AppendLine("-- " + item.Class + " fields" + ((item.Objects.Count == 0) ? " (no live object yet)" : ""));
			int? num = ((item.Objects.Count > 0) ? new int?(item.Objects.First()) : ((int?)null));
			string text = item.Class;
			bool flag = ((text == "GameEvent_Soccar_TA" || text == "Team_TA") ? true : false);
			bool matchClass = flag;
			foreach (GameReflection.Prop item2 in (from p in refl.All(item.Class)
				where (p.IsBool || p.IsFloat || p.IsInt) && (!matchClass || p.Offset >= 624)
				orderby p.Name
				select p).Take(matchClass ? 600 : 160))
			{
				stringBuilder.AppendLine($"   {item2.Name} [{item2.Type}] @0x{item2.Offset:X}" + (num.HasValue ? (" = " + refl.Describe(num.Value, item2)) : ""));
			}
		}
		stringBuilder.AppendLine("-- size / scale / gravity / bounce / speed fields (name = value on the first live object)");
		string[] array = new string[9] { "Car_TA", "Ball_TA", "RBActor_TA", "WorldInfo", "GameInfo", "GameEvent_Soccar_TA", "Mutator_Ball_TA", "Mutator_Car_TA", "Mutator_GameSpeed_TA" };
		foreach (string text2 in array)
		{
			int? num3 = ((targets.TryGetValue(text2, out Target value) && value.Objects.Count > 0) ? new int?(value.Objects.First()) : ((int?)null));
			foreach (GameReflection.Prop item3 in (from p in refl.All(text2)
				where p.IsFloat && Regex.IsMatch(p.Name, "Scale|Gravity|Bounce|Size|Dilation|Speed", RegexOptions.IgnoreCase)
				orderby p.Name
				select p).Take(60))
			{
				stringBuilder.AppendLine($"   {text2}.{item3.Name} @0x{item3.Offset:X}" + (num3.HasValue ? (" = " + refl.Describe(num3.Value, item3)) : " (no live object)"));
			}
		}
		stringBuilder.AppendLine("-- other kinds of fields (arrays, structs) on the speed classes");
		array = CurveClasses;
		foreach (string text3 in array)
		{
			foreach (GameReflection.Prop item4 in (from p in refl.All(text3)
				where !p.IsBool && !p.IsFloat && !p.IsInt
				orderby p.Name
				select p).Take(80))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(11, 4, stringBuilder2);
				handler.AppendLiteral("   ");
				handler.AppendFormatted(text3);
				handler.AppendLiteral(".");
				handler.AppendFormatted(item4.Name);
				handler.AppendLiteral(" [");
				handler.AppendFormatted(item4.Type);
				handler.AppendLiteral("] @0x");
				handler.AppendFormatted(item4.Offset, "X");
				stringBuilder4.AppendLine(ref handler);
			}
		}
		stringBuilder.AppendLine("-- top-speed table search");
		array = CurveClasses;
		foreach (string text4 in array)
		{
			if (targets.TryGetValue(text4, out Target value2) && value2.Objects.Count > 0)
			{
				int length = stringBuilder.Length;
				FindSpeedCurves(c, c.AddressOf(value2.Objects.First()), text4, stringBuilder);
				if (stringBuilder.Length == length)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder5 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder2);
					handler.AppendLiteral("   nothing in ");
					handler.AppendFormatted(text4);
					stringBuilder5.AppendLine(ref handler);
				}
			}
		}
		stringBuilder.AppendLine("-- live objects that look like match state (class: name)");
		int num4 = 0;
		for (int num5 = 0; num5 < c.Count; num5++)
		{
			if (num4 >= 60)
			{
				break;
			}
			if (!c.Readable(num5))
			{
				continue;
			}
			string text5 = c.Cls(num5);
			if (Regex.IsMatch(text5, "GameEvent|^Team|TeamInfo|GameInfo|GRI|Clock|Timer|Match|Score"))
			{
				string text6 = c.Nm(num5);
				bool flag = text6.StartsWith("Default__", StringComparison.Ordinal);
				if (!flag)
				{
					string text = c.Root(num5);
					bool flag2 = ((text == "Archetypes" || text == "TAGame") ? true : false);
					flag = flag2;
				}
				if (!flag)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder6 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(5, 2, stringBuilder2);
					handler.AppendLiteral("   ");
					handler.AppendFormatted(text5);
					handler.AppendLiteral(": ");
					handler.AppendFormatted(text6);
					stringBuilder6.AppendLine(ref handler);
					num4++;
				}
			}
		}
		GameReflection.Prop prop = refl.Find("GameEvent_Soccar_TA", "bGoalsEnabled");
		GameReflection.Prop prop2 = refl.Find("Ball_TA", "Location");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(66, 3, stringBuilder2);
		handler.AppendLiteral("-- goal switch bGoalsEnabled: ");
		handler.AppendFormatted((prop != null) ? ("found @0x" + prop.Offset.ToString("X")) : "NOT found");
		handler.AppendLiteral("; ball Location: ");
		handler.AppendFormatted((prop2 != null) ? ("found @0x" + prop2.Offset.ToString("X") + " (" + prop2.Type + ", size " + prop2.Size + ")") : "NOT found");
		handler.AppendLiteral("; Ball_TA objects: ");
		handler.AppendFormatted(targets.TryGetValue("Ball_TA", out Target value3) ? value3.Objects.Count : 0);
		stringBuilder7.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(31, 2, stringBuilder2);
		handler.AppendLiteral("-- teams: ");
		handler.AppendFormatted(refl.All("Team_TA").Any((GameReflection.Prop p) => p.Name == "Score") ? "Score found" : "Score NOT found");
		handler.AppendLiteral(", team number field: ");
		handler.AppendFormatted(refl.All("Team_TA").FirstOrDefault((GameReflection.Prop p) => (p.IsInt || p.Type == "ByteProperty") && TeamNumName.IsMatch(p.Name))?.Name ?? "not found (using creation order)");
		stringBuilder8.AppendLine(ref handler);
		return stringBuilder.ToString();
	}
}
