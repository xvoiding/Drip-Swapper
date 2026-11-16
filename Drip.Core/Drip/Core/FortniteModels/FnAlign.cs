using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Drip.Core.FallGuys;

namespace Drip.Core.FortniteModels;

public static class FnAlign
{
	public static float GuessYaw(FgModelSource src, FgUpAxis up, bool mirror = false)
	{
		int vertexCount = src.VertexCount;
		if (vertexCount == 0)
		{
			return 0f;
		}
		int num = Math.Max(1, vertexCount / 30000);
		List<Vector3> pts = new List<Vector3>(vertexCount / num + 1);
		for (int i = 0; i < vertexCount; i += num)
		{
			Vector3 item = FnTarget.ToUp(src.Pos[i], up);
			if (mirror)
			{
				item.X = 0f - item.X;
			}
			pts.Add(item);
		}
		float mn = pts.Min((Vector3 p) => p.Y);
		float num2 = pts.Max((Vector3 p) => p.Y);
		float h = Math.Max(num2 - mn, 0.0001f);
		float[] rel = pts.Select((Vector3 p) => (p.Y - mn) / h).ToArray();
		float num3 = float.MinValue;
		float num4 = 0f;
		for (float num5 = 0f; num5 < 180f; num5 += 2.5f)
		{
			(float XExt, float ZExt, float Toes, float Nose, float Unused) tuple = Measure(pts, rel, num5);
			float item2 = tuple.XExt;
			float item3 = tuple.ZExt;
			float num6 = item2 / Math.Max(item3, 0.001f * h);
			if (num6 > num3)
			{
				num3 = num6;
				num4 = num5;
			}
		}
		float num7 = Facing(num4);
		float num8 = Facing(num4 + 180f);
		float num9 = ((num7 >= num8) ? num4 : (num4 + 180f));
		num9 = (num9 % 360f + 360f) % 360f;
		if (num9 > 180f)
		{
			num9 -= 360f;
		}
		return num9;
		float Facing(float yaw)
		{
			(float XExt, float ZExt, float Toes, float Nose, float Unused) tuple2 = Measure(pts, rel, yaw);
			float item4 = tuple2.Toes;
			float item5 = tuple2.Nose;
			return item4 + item5 * 0.25f;
		}
	}

	public static (float XExt, float ZExt, float Toes, float Nose) Features(FgModelSource src, FgUpAxis up, float yaw)
	{
		List<Vector3> list = src.Pos.Select((Vector3 p) => FnTarget.ToUp(p, up)).ToList();
		float mn = list.Min((Vector3 p) => p.Y);
		float num = list.Max((Vector3 p) => p.Y);
		float h = Math.Max(num - mn, 0.0001f);
		float[] rel = list.Select((Vector3 p) => (p.Y - mn) / h).ToArray();
		var (num2, num3, num4, num5, _) = Measure(list, rel, yaw);
		return (XExt: num2 / h, ZExt: num3 / h, Toes: num4 / h, Nose: num5 / h);
	}

	private static (float XExt, float ZExt, float Toes, float Nose, float Unused) Measure(List<Vector3> pts, float[] rel, float yaw)
	{
		Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * (float)Math.PI / 180f);
		List<float> list = new List<float>();
		List<float> list2 = new List<float>();
		List<float> list3 = new List<float>();
		float num = 0f;
		float num2 = float.MinValue;
		int num3 = 0;
		float num4 = 0f;
		int num5 = 0;
		for (int i = 0; i < pts.Count; i++)
		{
			Vector3 vector = Vector3.Transform(pts[i], rotation);
			float num6 = rel[i];
			if (num6 >= 0.62f && num6 <= 0.9f)
			{
				list.Add(vector.X);
				list2.Add(vector.Z);
			}
			if (num6 < 0.05f)
			{
				list3.Add(vector.Z);
			}
			else if (num6 >= 0.1f && num6 <= 0.3f)
			{
				num += vector.Z;
				num3++;
			}
			if (num6 >= 0.88f)
			{
				num2 = Math.Max(num2, vector.Z);
			}
			if (num6 >= 0.4f && num6 <= 0.7f)
			{
				num4 += vector.Z;
				num5++;
			}
		}
		if (list.Count < 8)
		{
			return (XExt: 1f, ZExt: 1f, Toes: 0f, Nose: 0f, Unused: 0f);
		}
		list.Sort();
		list2.Sort();
		list3.Sort();
		float item = P(list, 0.98f) - P(list, 0.02f);
		float item2 = P(list2, 0.98f) - P(list2, 0.02f);
		float item3 = 0f;
		if (list3.Count >= 8 && num3 > 0)
		{
			float num7 = num / (float)num3;
			item3 = P(list3, 0.98f) - num7 - (num7 - P(list3, 0.02f));
		}
		float item4 = ((num5 > 0 && num2 > float.MinValue) ? (num2 - num4 / (float)num5) : 0f);
		return (XExt: item, ZExt: item2, Toes: item3, Nose: item4, Unused: 0f);
		static float P(List<float> l, float f)
		{
			return l[Math.Clamp((int)(f * (float)(l.Count - 1)), 0, l.Count - 1)];
		}
	}
}
