using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.RocketLeague.Custom;

public sealed class FbxNode
{
	public string Name = "";

	public List<object> Props = new List<object>();

	public List<FbxNode> Children = new List<FbxNode>();

	public FbxNode? Child(string name)
	{
		return Children.FirstOrDefault((FbxNode c) => c.Name == name);
	}

	public IEnumerable<FbxNode> All(string name)
	{
		return Children.Where((FbxNode c) => c.Name == name);
	}

	public long Long(int i = 0)
	{
		if (i < Props.Count)
		{
			object obj = Props[i];
			long result;
			if (obj is long num)
			{
				result = num;
			}
			else if (obj is int num2)
			{
				result = num2;
			}
			else if (obj is short num3)
			{
				result = num3;
			}
			else if (obj is byte b)
			{
				result = b;
			}
			else
			{
				result = ((!(obj is double num4)) ? 0 : ((long)num4));
			}
			return result;
		}
		return 0L;
	}

	public string Str(int i = 0)
	{
		if (i >= Props.Count || !(Props[i] is string result))
		{
			return "";
		}
		return result;
	}

	public double[] Doubles()
	{
		if (Props.Count <= 0 || !(Props[0] is double[] result))
		{
			if (Props.Count <= 0 || !(Props[0] is float[] source))
			{
				return Array.Empty<double>();
			}
			return ((IEnumerable<float>)source).Select((Func<float, double>)((float x) => x)).ToArray();
		}
		return result;
	}

	public int[] Ints()
	{
		if (Props.Count <= 0 || !(Props[0] is int[] result))
		{
			return Array.Empty<int>();
		}
		return result;
	}

	public double[] Prop(string name)
	{
		FbxNode fbxNode = Child("Properties70")?.Children.FirstOrDefault((FbxNode c) => c.Props.Count > 4 && c.Str() == name);
		if (fbxNode == null)
		{
			return Array.Empty<double>();
		}
		List<double> list = new List<double>();
		for (int num = 4; num < fbxNode.Props.Count; num++)
		{
			object obj = fbxNode.Props[num];
			double item;
			if (obj is double num2)
			{
				item = num2;
			}
			else if (obj is float num3)
			{
				item = num3;
			}
			else if (obj is int num4)
			{
				item = num4;
			}
			else if (obj is long num5)
			{
				item = num5;
			}
			else
			{
				item = ((!(obj is short num6)) ? 0.0 : ((double)num6));
			}
			list.Add(item);
		}
		return list.ToArray();
	}
}
