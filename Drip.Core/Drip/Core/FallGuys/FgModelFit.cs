using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FallGuys;

public static class FgModelFit
{
	public static FgUpAxis GuessUp(FgModelSource src)
	{
		if (src.Rig != null || src.Pos.Length == 0)
		{
			return FgUpAxis.Y;
		}
		Vector3 vector = new Vector3(float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue);
		Vector3[] pos = src.Pos;
		foreach (Vector3 value in pos)
		{
			vector = Vector3.Min(vector, value);
			vector2 = Vector3.Max(vector2, value);
		}
		Vector3 vector3 = vector2 - vector;
		bool flag;
		switch (Path.GetExtension(src.Path ?? "").ToLowerInvariant())
		{
		case ".stl":
		case ".ply":
		case ".3ds":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag && vector3.Z >= vector3.Y * 0.9f)
		{
			return FgUpAxis.Z;
		}
		if (vector3.Z > vector3.Y * 1.5f && vector3.Z >= vector3.X * 0.8f)
		{
			return FgUpAxis.Z;
		}
		return FgUpAxis.Y;
	}

	public static FgFittedModel Fit(FgModelSource src, FgFit fit, FgFittedModel? reuseLook = null)
	{
		if (src.Rig != null)
		{
			return FgHumanoid.Fit(src, fit, reuseLook);
		}
		int vertexCount = src.VertexCount;
		Vector3[] array = new Vector3[vertexCount];
		Vector3[] array2 = new Vector3[vertexCount];
		Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, fit.Yaw * (float)Math.PI / 180f);
		for (int i = 0; i < vertexCount; i++)
		{
			Vector3 value = Up(src.Pos[i], fit.Up);
			Vector3 value2 = Up(src.Nrm[i], fit.Up);
			if (fit.MirrorX)
			{
				value.X = 0f - value.X;
				value2.X = 0f - value2.X;
			}
			array[i] = Vector3.Transform(value, rotation);
			array2[i] = Vector3.Transform(value2, rotation);
		}
		int[] array3 = (int[])src.Idx.Clone();
		if (fit.MirrorX)
		{
			for (int j = 0; j + 2 < array3.Length; j += 3)
			{
				ref int reference = ref array3[j + 1];
				ref int reference2 = ref array3[j + 2];
				int num = array3[j + 2];
				int num2 = array3[j + 1];
				reference = num;
				reference2 = num2;
			}
		}
		Vector3 value3 = new Vector3(float.MaxValue);
		Vector3 value4 = new Vector3(float.MinValue);
		Vector3[] array4 = array;
		foreach (Vector3 value5 in array4)
		{
			value3 = Vector3.Min(value3, value5);
			value4 = Vector3.Max(value4, value5);
		}
		float num3 = Math.Max(value4.Y - value3.Y, 0.0001f);
		float num4 = fit.Height / num3 * fit.Scale;
		Vector3 vector = new Vector3((value3.X + value4.X) / 2f, value3.Y, (value3.Z + value4.Z) / 2f);
		for (int k = 0; k < vertexCount; k++)
		{
			array[k] = (array[k] - vector) * num4 + new Vector3(fit.OffsetX, fit.OffsetY, fit.OffsetZ);
		}
		for (int l = 0; l < vertexCount; l++)
		{
			float num5 = array2[l].Length();
			array2[l] = ((num5 > 1E-05f) ? (array2[l] / num5) : Vector3.Zero);
		}
		FgFittedModel fgFittedModel = new FgFittedModel
		{
			Pos = array,
			Nrm = array2,
			Idx = array3
		};
		if (reuseLook != null && reuseLook.Rgba.Length != 0 && reuseLook.Uv.Length == vertexCount)
		{
			fgFittedModel.Uv = reuseLook.Uv;
			fgFittedModel.Rgba = reuseLook.Rgba;
			fgFittedModel.Width = reuseLook.Width;
			fgFittedModel.Height = reuseLook.Height;
		}
		else
		{
			BuildLook(src, fit, fgFittedModel);
		}
		return fgFittedModel;
	}

	private static Vector3 Up(Vector3 v, FgUpAxis up)
	{
		return up switch
		{
			FgUpAxis.Y => v, 
			FgUpAxis.NegY => new Vector3(0f - v.X, 0f - v.Y, v.Z), 
			FgUpAxis.Z => new Vector3(v.X, v.Z, 0f - v.Y), 
			FgUpAxis.NegZ => new Vector3(v.X, 0f - v.Z, v.Y), 
			FgUpAxis.X => new Vector3(0f - v.Y, v.X, v.Z), 
			FgUpAxis.NegX => new Vector3(v.Y, 0f - v.X, v.Z), 
			_ => v, 
		};
	}

	internal static void BuildLook(FgModelSource src, FgFit fit, FgFittedModel res)
	{
		int vertexCount = src.VertexCount;
		Vector2[] array = new Vector2[vertexCount];
		if (fit.PictureOverride != null)
		{
			TexImage texImage = Limit(fit.PictureOverride, fit.MaxTexture);
			res.Width = texImage.Width;
			res.Height = texImage.Height;
			res.Rgba = texImage.Rgba;
			for (int i = 0; i < vertexCount; i++)
			{
				array[i] = (src.HasUv ? src.Uv[i] : AutoUv(res.Pos[i], res.Pos));
			}
			res.Uv = array;
			return;
		}
		List<int> list = (from x in src.Mat.Distinct()
			orderby x
			select x).ToList();
		List<List<int>> list2 = (from g in list.GroupBy(Key)
			select g.ToList()).ToList();
		if (list2.Count == 1 && src.Materials[list[0]].Picture != null && src.HasUv)
		{
			TexImage texImage2 = Limit(src.Materials[list[0]].Picture, fit.MaxTexture);
			res.Width = texImage2.Width;
			res.Height = texImage2.Height;
			res.Rgba = texImage2.Rgba;
			ShiftIntoRange(src, list[0], array);
			res.Uv = array;
			return;
		}
		int count = list2.Count;
		int num = (int)Math.Ceiling(Math.Sqrt(count));
		int num2 = ((num == 1) ? 256 : Math.Min(fit.MaxTexture, 2048));
		int num3 = num2 / num;
		byte[] array2 = new byte[num2 * num2 * 4];
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		for (int num4 = 0; num4 < count; num4++)
		{
			int index = list2[num4][0];
			foreach (int item in list2[num4])
			{
				dictionary[item] = num4;
			}
			int num5 = num4 % num;
			int num6 = num4 / num;
			FgSourceMaterial fgSourceMaterial = src.Materials[index];
			TexImage texImage3 = ((fgSourceMaterial.Picture != null && src.HasUv) ? fgSourceMaterial.Picture.Resample(num3, num3) : null);
			int num7 = (num - 1 - num6) * num3;
			for (int num8 = 0; num8 < num3; num8++)
			{
				for (int num9 = 0; num9 < num3; num9++)
				{
					int num10 = ((num7 + num8) * num2 + num5 * num3 + num9) * 4;
					if (texImage3 != null)
					{
						Array.Copy(texImage3.Rgba, (num8 * num3 + num9) * 4, array2, num10, 4);
						continue;
					}
					Vector4 vector = fgSourceMaterial.Color;
					Vector3? tintColor = fit.TintColor;
					if (tintColor.HasValue)
					{
						Vector3 valueOrDefault = tintColor.GetValueOrDefault();
						if (vector == Vector4.One)
						{
							vector = new Vector4(valueOrDefault, 1f);
						}
					}
					array2[num10] = (byte)Math.Clamp(vector.X * 255f + 0.5f, 0f, 255f);
					array2[num10 + 1] = (byte)Math.Clamp(vector.Y * 255f + 0.5f, 0f, 255f);
					array2[num10 + 2] = (byte)Math.Clamp(vector.Z * 255f + 0.5f, 0f, 255f);
					array2[num10 + 3] = byte.MaxValue;
				}
			}
		}
		res.Width = num2;
		res.Height = num2;
		res.Rgba = array2;
		float num11 = Math.Min(0.02f, 2f / (float)num3);
		foreach (int item2 in list)
		{
			int num12 = dictionary[item2];
			int num13 = num12 % num;
			int num14 = num12 / num;
			bool flag = src.Materials[item2].Picture != null && src.HasUv;
			float num15 = 0f;
			float num16 = 0f;
			if (flag)
			{
				List<float> list3 = new List<float>();
				List<float> list4 = new List<float>();
				for (int num17 = 0; num17 < vertexCount; num17++)
				{
					if (src.Mat[num17] == item2)
					{
						list3.Add(src.Uv[num17].X);
						list4.Add(src.Uv[num17].Y);
					}
				}
				list3.Sort();
				list4.Sort();
				num15 = MathF.Floor(list3[list3.Count / 2]);
				num16 = MathF.Floor(list4[list4.Count / 2]);
			}
			for (int num18 = 0; num18 < vertexCount; num18++)
			{
				if (src.Mat[num18] == item2)
				{
					float num19 = (flag ? Math.Clamp(src.Uv[num18].X - num15, 0f, 1f) : 0.5f);
					float num20 = (flag ? Math.Clamp(src.Uv[num18].Y - num16, 0f, 1f) : 0.5f);
					num19 = num11 + num19 * (1f - 2f * num11);
					num20 = num11 + num20 * (1f - 2f * num11);
					array[num18] = new Vector2(((float)num13 + num19) / (float)num, ((float)num14 + num20) / (float)num);
				}
			}
		}
		res.Uv = array;
		string Key(int mi)
		{
			FgSourceMaterial fgSourceMaterial2 = src.Materials[mi];
			if (fgSourceMaterial2.Picture != null && src.HasUv)
			{
				ulong num21 = 1469598103934665603uL;
				byte[] rgba = fgSourceMaterial2.Picture.Rgba;
				for (int j = 0; j < rgba.Length; j += 97)
				{
					num21 = (num21 ^ rgba[j]) * 1099511628211L;
				}
				return $"P{fgSourceMaterial2.Picture.Width}x{fgSourceMaterial2.Picture.Height}:{num21:x}";
			}
			return "C" + fgSourceMaterial2.Color;
		}
	}

	private static void ShiftIntoRange(FgModelSource src, int mat, Vector2[] uv)
	{
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		for (int i = 0; i < src.VertexCount; i++)
		{
			num = Math.Min(num, src.Uv[i].X);
			num2 = Math.Min(num2, src.Uv[i].Y);
		}
		float num3 = MathF.Floor(num + 0.0001f);
		float num4 = MathF.Floor(num2 + 0.0001f);
		for (int j = 0; j < src.VertexCount; j++)
		{
			uv[j] = new Vector2(src.Uv[j].X - num3, src.Uv[j].Y - num4);
		}
	}

	private static Vector2 AutoUv(Vector3 p, Vector3[] all)
	{
		Vector3 vector = new Vector3(float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue);
		foreach (Vector3 value in all)
		{
			vector = Vector3.Min(vector, value);
			vector2 = Vector3.Max(vector2, value);
		}
		Vector3 vector3 = Vector3.Max(vector2 - vector, new Vector3(0.0001f));
		return new Vector2((p.X - vector.X) / vector3.X, (p.Y - vector.Y) / vector3.Y);
	}

	private static TexImage Limit(TexImage t, int max)
	{
		if (t.Width <= max && t.Height <= max)
		{
			return t;
		}
		float num = (float)max / (float)Math.Max(t.Width, t.Height);
		return t.Resample(Math.Max(4, (int)((float)t.Width * num)), Math.Max(4, (int)((float)t.Height * num)));
	}
}
