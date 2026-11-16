using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Drip.Core.FortniteModels;

internal sealed class NativeRig
{
	private sealed class PointGrid
	{
		private const double Cell = 2.5;

		private readonly double[] _p;

		private readonly Dictionary<(int, int, int), List<int>> _cells = new Dictionary<(int, int, int), List<int>>();

		private readonly int _span;

		public PointGrid(double[] p)
		{
			_p = p;
			int num = int.MaxValue;
			int num2 = int.MinValue;
			for (int i = 0; i < p.Length / 3; i++)
			{
				int num3 = C(p[i * 3]);
				int num4 = C(p[i * 3 + 1]);
				int num5 = C(p[i * 3 + 2]);
				if (!_cells.TryGetValue((num3, num4, num5), out List<int> value))
				{
					value = (_cells[(num3, num4, num5)] = new List<int>());
				}
				value.Add(i);
				num = Math.Min(num, Math.Min(num3, Math.Min(num4, num5)));
				num2 = Math.Max(num2, Math.Max(num3, Math.Max(num4, num5)));
			}
			_span = num2 - num + 2;
		}

		private static int C(double v)
		{
			return (int)Math.Floor(v / 2.5);
		}

		public int Nearest(double x, double y, double z)
		{
			int num = C(x);
			int num2 = C(y);
			int num3 = C(z);
			double num4 = double.MaxValue;
			int num5 = -1;
			int num6 = _span + Math.Max(Math.Abs(num), Math.Max(Math.Abs(num2), Math.Abs(num3))) + 1;
			for (int i = 0; i <= num6; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						int num7 = ((Math.Abs(j) == i || Math.Abs(k) == i) ? 1 : Math.Max(1, 2 * i));
						for (int l = -i; l <= i; l += num7)
						{
							if (!_cells.TryGetValue((num + l, num2 + k, num3 + j), out List<int> value))
							{
								continue;
							}
							foreach (int item in value)
							{
								double num8 = _p[item * 3] - x;
								double num9 = _p[item * 3 + 1] - y;
								double num10 = _p[item * 3 + 2] - z;
								double num11 = num8 * num8 + num9 * num9 + num10 * num10;
								if (num11 < num4)
								{
									num4 = num11;
									num5 = item;
								}
							}
						}
					}
				}
				if (num5 >= 0 && Math.Sqrt(num4) <= (double)i * 2.5)
				{
					break;
				}
			}
			return num5;
		}
	}

	private readonly FnSkin _skin;

	private readonly int[] _toSkeleton;

	private readonly PointGrid _grid;

	private static readonly Regex Finger = new Regex("(^|[_\\s.])(thumb|index|middle|ring|pinky|pinkie)(_|\\d|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private NativeRig(FnSkin skin, int[] toSkeleton, PointGrid grid)
	{
		_skin = skin;
		_toSkeleton = toSkeleton;
		_grid = grid;
	}

	public static NativeRig Create(FnSkin skin, FnMeshInfo info, double[] modelPos, Action<string>? log = null)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < info.BoneNames.Length; i++)
		{
			dictionary.TryAdd(info.BoneNames[i], i);
		}
		int num = skin.BoneNames.Length;
		int[] array = new int[num];
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		for (int j = 0; j < num; j++)
		{
			int value;
			if (IsFingerBone(skin.BoneNames[j]) && skin.Parent[j] >= 0 && array[skin.Parent[j]] >= 0)
			{
				array[j] = array[skin.Parent[j]];
				num5++;
			}
			else if (dictionary.TryGetValue(skin.BoneNames[j], out value))
			{
				array[j] = value;
				num2++;
			}
			else if (skin.Parent[j] >= 0 && array[skin.Parent[j]] >= 0)
			{
				array[j] = array[skin.Parent[j]];
				num3++;
			}
			else
			{
				array[j] = -1;
				num4++;
			}
		}
		log?.Invoke($"Rig: {num2} of the model's {num} bones match the game's skeleton by name, {num3} borrow their parent's, {num4} have no match; {num5} finger bones follow the hand.");
		if (num2 == 0)
		{
			throw new InvalidDataException("None of this model's bones are named like the character's bones, so its own rig can't be used.");
		}
		return new NativeRig(skin, array, new PointGrid(modelPos));
	}

	internal static bool IsFingerBone(string name)
	{
		return Finger.IsMatch(name);
	}

	internal int SkeletonBone(int modelBone)
	{
		return _toSkeleton[modelBone];
	}

	public void Weights(FnPart part, Dictionary<int, int> local, int[] skeletonParent, int k, byte[] boneOut, byte[] wOut)
	{
		int vertexCount = part.VertexCount;
		int sk = _skin.K;
		int fallback = ((local.Count > 0) ? local.Keys.Min() : 0);
		Parallel.For(0, vertexCount, (int j) =>
		{
			Dictionary<int, double> dictionary = new Dictionary<int, double>(8);
			int num = _grid.Nearest(part.Pos[j * 3], part.Pos[j * 3 + 1], part.Pos[j * 3 + 2]);
			if (num >= 0)
			{
				for (int i = 0; i < sk; i++)
				{
					int num2 = _skin.Weight[num * sk + i];
					if (num2 != 0)
					{
						int num3 = Resolve(_toSkeleton[_skin.Bone[num * sk + i]], skeletonParent, local);
						if (num3 >= 0)
						{
							dictionary[num3] = dictionary.GetValueOrDefault(num3) + (double)num2 / 255.0;
						}
					}
				}
			}
			if (dictionary.Count == 0)
			{
				dictionary[fallback] = 1.0;
			}
			FnReplace.Pack(dictionary, local, k, boneOut.AsSpan(j * k, k), wOut.AsSpan(j * k, k));
		});
	}

	internal static int Resolve(int bone, int[] parent, Dictionary<int, int> local)
	{
		int num = 0;
		while (bone >= 0 && !local.ContainsKey(bone) && num++ < 4096)
		{
			bone = ((bone < parent.Length) ? parent[bone] : (-1));
		}
		return bone;
	}
}
