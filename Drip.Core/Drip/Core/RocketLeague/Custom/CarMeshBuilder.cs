using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Drip.Core.RocketLeague.Custom;

public static class CarMeshBuilder
{
	public const float CarLength = 140f;

	public const float CarWidth = 84f;

	public const float BottomZ = -8f;

	public const float CenterX = 10f;

	public const float BallDiameter = 182f;

	public const float WheelDiameter = 32.6f;

	public static bool IsLeftOut(FbxMesh m, ModelFit fit)
	{
		List<string> list;
		if (m.Path.Count <= 0)
		{
			int num = 1;
			list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = m.Name;
		}
		else
		{
			list = m.Path;
		}
		return list.Any((string n) => fit.LeaveOut.Any((string k) => k.Length > 0 && n.Contains(k, StringComparison.OrdinalIgnoreCase)));
	}

	public static CarMesh Build(FbxScene scene, ModelFit fit, Func<FbxMesh, bool>? keep = null, bool ball = false, bool useMaterialColors = false, TexImage? picture = null, IReadOnlyList<NamedPicture>? pictures = null, bool wheel = false)
	{
		Vector3 up = Axis(scene.UpAxis, scene.UpSign);
		Vector3 front = Axis(scene.FrontAxis, scene.FrontSign);
		if (Math.Abs(Vector3.Dot(up, front)) > 0.5f)
		{
			front = ((Vector3.Cross(up, Vector3.UnitX).LengthSquared() > 0.1f) ? Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX)) : Vector3.UnitY);
		}
		Quaternion rotation = Quaternion.CreateFromAxisAngle(up, (float)fit.Turns * (float)Math.PI / 2f);
		front = Vector3.Transform(front, rotation);
		if (fit.FlipFrontBack)
		{
			front = -front;
		}
		Vector3 right = Vector3.Cross(front, up);
		Quaternion fine = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)fit.Yaw * ((float)Math.PI / 180f)) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, (0f - (float)fit.Pitch) * ((float)Math.PI / 180f)) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, (0f - (float)fit.Roll) * ((float)Math.PI / 180f));
		bool rotated = fit.Yaw != 0.0 || fit.Pitch != 0.0 || fit.Roll != 0.0;
		Quaternion axleQ = Quaternion.Identity;
		bool permute = false;
		List<FbxMesh> list = scene.Meshes.Where((FbxMesh m) => keep?.Invoke(m) ?? (!IsLeftOut(m, fit))).ToList();
		if (list.Count == 0)
		{
			throw new FbxFormatException("Every mesh in that file was left out. Check the mesh list.");
		}
		if (wheel)
		{
			Vector3 vector = new Vector3(float.MaxValue);
			Vector3 vector2 = new Vector3(float.MinValue);
			foreach (FbxMesh item in list)
			{
				foreach (Vector3 po in item.Pos)
				{
					Vector3 value = ToRl(po);
					vector = Vector3.Min(vector, value);
					vector2 = Vector3.Max(vector2, value);
				}
			}
			Vector3 vector3 = vector2 - vector;
			if (vector3.X <= vector3.Y && vector3.X <= vector3.Z)
			{
				axleQ = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2f);
			}
			else if (vector3.Z <= vector3.X && vector3.Z <= vector3.Y)
			{
				axleQ = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -(float)Math.PI / 2f);
			}
			permute = !(vector3.Y <= vector3.X) || !(vector3.Y <= vector3.Z);
		}
		List<Vector3> list2 = new List<Vector3>();
		List<Vector3> list3 = new List<Vector3>();
		List<Vector2> list4 = new List<Vector2>();
		foreach (FbxMesh item2 in list)
		{
			for (int num = 0; num < item2.Pos.Count; num++)
			{
				list2.Add(ToRl(item2.Pos[num]));
				list3.Add(ToRl(item2.Nrm[num]));
				list4.Add(new Vector2(item2.Uv[num].X, 1f - item2.Uv[num].Y));
			}
		}
		TexImage atlas = null;
		if (pictures != null && pictures.Count > 1)
		{
			atlas = PackMaterialColors(list, list4, pictures.Select((NamedPicture x) => x.Image).ToList(), AssignPictures(list, pictures));
		}
		else if (useMaterialColors)
		{
			IReadOnlyList<TexImage> pictures2;
			if (picture != null)
			{
				IReadOnlyList<TexImage> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<TexImage>(picture);
				pictures2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<TexImage> readOnlyList = Array.Empty<TexImage>();
				pictures2 = readOnlyList;
			}
			atlas = PackMaterialColors(list, list4, pictures2, (FbxMaterial m) => (!m.HasTexture || picture == null) ? (-1) : 0);
		}
		Vector3 vector4 = new Vector3(float.MaxValue);
		Vector3 vector5 = new Vector3(float.MinValue);
		foreach (Vector3 item3 in list2)
		{
			vector4 = Vector3.Min(vector4, item3);
			vector5 = Vector3.Max(vector5, item3);
		}
		Vector3 vector6 = vector5 - vector4;
		if (vector6.X < 1E-06f || vector6.Y < 1E-06f || vector6.Z < 1E-06f)
		{
			throw new FbxFormatException("That model is flat or empty.");
		}
		float num2;
		if (wheel)
		{
			num2 = 32.6f / MathF.Max(vector6.X, vector6.Z) * (float)fit.Size;
		}
		else
		{
			num2 = (ball ? (182f / MathF.Max(vector6.X, MathF.Max(vector6.Y, vector6.Z)) * (float)fit.Size) : (MathF.Min(140f / vector6.X, 96.6f / vector6.Y) * (float)fit.Size));
		}
		if (!float.IsFinite(num2) || num2 <= 0f)
		{
			throw new FbxFormatException("Couldn't work out a size for that model.");
		}
		Vector3 vector7 = ((ball | wheel) ? ((vector4 + vector5) / 2f) : new Vector3((vector4.X + vector5.X) / 2f, (vector4.Y + vector5.Y) / 2f, vector4.Z));
		Vector3 vector8 = ((ball | wheel) ? new Vector3((float)fit.OffsetX, (float)fit.OffsetY, (float)fit.Lift) : new Vector3(10f + (float)fit.OffsetX, (float)fit.OffsetY, -8f + (float)fit.Lift));
		for (int num3 = 0; num3 < list2.Count; num3++)
		{
			list2[num3] = (list2[num3] - vector7) * num2 + vector8;
		}
		CarMesh carMesh = Weld(list2, list3, list4, list);
		carMesh.Atlas = atlas;
		return carMesh;
		static Vector3 Axis(int axis, int sign)
		{
			return axis switch
			{
				0 => Vector3.UnitX * sign, 
				1 => Vector3.UnitY * sign, 
				_ => Vector3.UnitZ * sign, 
			};
		}
		Vector3 ToRl(Vector3 v)
		{
			Vector3 vector9 = new Vector3(Vector3.Dot(v, front), Vector3.Dot(v, right), Vector3.Dot(v, up));
			if (permute)
			{
				vector9 = Vector3.Transform(vector9, axleQ);
			}
			if (!rotated)
			{
				return vector9;
			}
			return Vector3.Transform(vector9, fine);
		}
	}

	public static Func<FbxMaterial, int> AssignPictures(List<FbxMesh> meshes, IReadOnlyList<NamedPicture> pics)
	{
		List<FbxMaterial> list = new List<FbxMaterial>();
		foreach (FbxMesh mesh in meshes)
		{
			foreach (FbxMaterial x in mesh.Materials)
			{
				if (!list.Any((FbxMaterial y) => y.Name == x.Name))
				{
					list.Add(x);
				}
			}
		}
		Dictionary<string, int> pick = new Dictionary<string, int>();
		HashSet<int> used = new HashSet<int>();
		foreach (FbxMaterial item in list)
		{
			string t = Base(item.TextureFile);
			if (t.Length != 0)
			{
				int num = Find((string b) => b == t);
				if (num >= 0)
				{
					pick[item.Name] = num;
					used.Add(num);
				}
			}
		}
		foreach (FbxMaterial item2 in list.Where((FbxMaterial m) => !pick.ContainsKey(m.Name)))
		{
			HashSet<string> mw = Words(item2.Name);
			mw.UnionWith(Words(Base(item2.TextureFile)));
			int num2 = Find((string b) => Words(b).Overlaps(mw));
			if (num2 >= 0)
			{
				pick[item2.Name] = num2;
				used.Add(num2);
			}
		}
		bool anyHooked = list.Any((FbxMaterial m) => m.HasTexture);
		foreach (FbxMaterial item3 in list.Where((FbxMaterial m) => !pick.ContainsKey(m.Name) && (m.HasTexture || !anyHooked)))
		{
			int num3 = Find((string _) => true);
			if (num3 < 0)
			{
				break;
			}
			pick[item3.Name] = num3;
			used.Add(num3);
		}
		return (FbxMaterial m) => (!pick.TryGetValue(m.Name, out var value)) ? (-1) : value;
		static string Base(string s)
		{
			return Path.GetFileNameWithoutExtension(s ?? "").ToLowerInvariant();
		}
		int Find(Func<string, bool> ok)
		{
			for (int i = 0; i < pics.Count; i++)
			{
				if (!used.Contains(i) && ok(Base(pics[i].Name)))
				{
					return i;
				}
			}
			return -1;
		}
		static HashSet<string> Words(string text)
		{
			HashSet<string> generic = new HashSet<string>
			{
				"mat", "material", "diffuse", "color", "colour", "albedo", "tex", "texture", "basecolor", "base",
				"map", "default", "d"
			};
			return (from w in Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+")
				where w.Length >= 3 && !generic.Contains(w)
				select w).ToHashSet();
		}
	}

	internal static TexImage PackMaterialColors(List<FbxMesh> meshes, List<Vector2> uv, IReadOnlyList<TexImage> pictures, Func<FbxMaterial, int> pick)
	{
		List<(string Name, Vector3 Color, int Tex)> keys = new List<(string, Vector3, int)>();
		List<int[]> list = new List<int[]>();
		foreach (FbxMesh mesh in meshes)
		{
			int[] array = new int[mesh.TriangleCount];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Cell(mesh, i);
			}
			list.Add(array);
		}
		int num = (int)Math.Ceiling(Math.Sqrt(Math.Max(1, keys.Count)));
		int cell = 1024 / num / 4 * 4;
		TexImage texImage = new TexImage
		{
			Width = 1024,
			Height = 1024,
			Rgba = new byte[4194304]
		};
		for (int j = 0; j < texImage.Rgba.Length; j += 4)
		{
			texImage.Rgba[j] = 200;
			texImage.Rgba[j + 1] = 200;
			texImage.Rgba[j + 2] = 200;
			texImage.Rgba[j + 3] = byte.MaxValue;
		}
		List<TexImage> list2 = pictures.Select((TexImage p) => p.Resample(cell, cell)).ToList();
		for (int num2 = 0; num2 < keys.Count; num2++)
		{
			int num3 = num2 % num * cell;
			int num4 = num2 / num * cell;
			for (int num5 = 0; num5 < cell; num5++)
			{
				for (int num6 = 0; num6 < cell; num6++)
				{
					int num7 = ((num4 + num5) * 1024 + num3 + num6) * 4;
					if (keys[num2].Tex >= 0)
					{
						Array.Copy(list2[keys[num2].Tex].Rgba, (num5 * cell + num6) * 4, texImage.Rgba, num7, 4);
						continue;
					}
					Vector3 item = keys[num2].Color;
					texImage.Rgba[num7] = (byte)Math.Clamp(item.X * 255f + 0.5f, 0f, 255f);
					texImage.Rgba[num7 + 1] = (byte)Math.Clamp(item.Y * 255f + 0.5f, 0f, 255f);
					texImage.Rgba[num7 + 2] = (byte)Math.Clamp(item.Z * 255f + 0.5f, 0f, 255f);
					texImage.Rgba[num7 + 3] = byte.MaxValue;
				}
			}
		}
		int num8 = 0;
		for (int num9 = 0; num9 < meshes.Count; num9++)
		{
			for (int num10 = 0; num10 < meshes[num9].TriangleCount; num10++)
			{
				int num11 = list[num9][num10];
				float num12 = (float)(num11 % num * cell) / 1024f;
				float num13 = (float)(num11 / num * cell) / 1024f;
				float num14 = (float)cell / 1024f;
				int num15 = 0;
				while (num15 < 3)
				{
					if (keys[num11].Tex >= 0)
					{
						Vector2 vector = uv[num8];
						float num16 = vector.X - MathF.Floor(vector.X);
						float num17 = vector.Y - MathF.Floor(vector.Y);
						float num18 = 1f / (float)cell;
						uv[num8] = new Vector2(num12 + (num18 + num16 * (1f - 2f * num18)) * num14, num13 + (num18 + num17 * (1f - 2f * num18)) * num14);
					}
					else
					{
						uv[num8] = new Vector2(num12 + num14 / 2f, num13 + num14 / 2f);
					}
					num15++;
					num8++;
				}
			}
		}
		return texImage;
		int Cell(FbxMesh m, int tri)
		{
			int num19 = m.Material[tri];
			FbxMaterial fbxMaterial = ((num19 >= 0 && num19 < m.Materials.Count) ? m.Materials[num19] : null);
			int num20 = ((fbxMaterial == null) ? (-1) : pick(fbxMaterial));
			(string, Vector3, int) item2 = (fbxMaterial?.Name ?? "", fbxMaterial?.Color ?? new Vector3(0.8f), (num20 >= 0 && num20 < pictures.Count) ? num20 : (-1));
			int num21 = keys.IndexOf(item2);
			if (num21 < 0)
			{
				keys.Add(item2);
				num21 = keys.Count - 1;
			}
			return num21;
		}
	}

	internal static CarMesh Weld(List<Vector3> pos, List<Vector3> nrm, List<Vector2> uv, List<FbxMesh> chosen)
	{
		Dictionary<(int, int, int, int, int, int, int), int> dictionary = new Dictionary<(int, int, int, int, int, int, int), int>();
		List<Vector3> list = new List<Vector3>();
		List<Vector3> list2 = new List<Vector3>();
		List<Vector2> list3 = new List<Vector2>();
		List<int> list4 = new List<int>(pos.Count);
		for (int i = 0; i + 2 < pos.Count; i += 3)
		{
			Vector3 vector = pos[i];
			Vector3 vector2 = pos[i + 1];
			Vector3 vector3 = Vector3.Cross(vector2: pos[i + 2] - vector, vector1: vector2 - vector);
			if (vector3.LengthSquared() < 1E-14f)
			{
				continue;
			}
			Vector3 vector4 = nrm[i] + nrm[i + 1] + nrm[i + 2];
			int[] array = ((!(vector4.LengthSquared() > 1E-08f) || !(Vector3.Dot(vector3, vector4) > 0f)) ? new int[3]
			{
				i,
				i + 1,
				i + 2
			} : new int[3]
			{
				i,
				i + 2,
				i + 1
			});
			foreach (int index in array)
			{
				Vector3 item = pos[index];
				Vector3 item2 = nrm[index];
				Vector2 item3 = uv[index];
				(int, int, int, int, int, int, int) key = ((int)MathF.Round(item.X * 200f), (int)MathF.Round(item.Y * 200f), (int)MathF.Round(item.Z * 200f), (int)MathF.Round(item2.X * 100f), (int)MathF.Round(item2.Y * 100f) * 1000 + (int)MathF.Round(item2.Z * 100f), (int)MathF.Round(item3.X * 4000f), (int)MathF.Round(item3.Y * 4000f));
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = list.Count);
					list.Add(item);
					list2.Add(item2);
					list3.Add(item3);
				}
				list4.Add(value);
			}
		}
		if (list4.Count == 0)
		{
			throw new FbxFormatException("That model has no usable triangles.");
		}
		if (list2.Any((Vector3 n) => n.LengthSquared() < 1E-08f))
		{
			Vector3[] array2 = new Vector3[list.Count];
			for (int num = 0; num < list4.Count; num += 3)
			{
				Vector3 vector5 = Vector3.Cross(list[list4[num + 1]] - list[list4[num]], list[list4[num + 2]] - list[list4[num]]);
				for (int num2 = 0; num2 < 3; num2++)
				{
					array2[list4[num + num2]] -= vector5;
				}
			}
			for (int num3 = 0; num3 < list2.Count; num3++)
			{
				if (list2[num3].LengthSquared() < 1E-08f)
				{
					list2[num3] = ((array2[num3].LengthSquared() > 0f) ? Vector3.Normalize(array2[num3]) : Vector3.UnitZ);
				}
			}
		}
		return new CarMesh
		{
			Pos = list.ToArray(),
			Nrm = list2.ToArray(),
			Uv = list3.ToArray(),
			Idx = list4.ToArray()
		};
	}
}
