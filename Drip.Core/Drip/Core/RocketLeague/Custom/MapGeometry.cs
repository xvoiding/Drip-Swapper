using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Drip.Core.RocketLeague.Custom;

public static class MapGeometry
{
	public const float FieldWidth = 8512f;

	public const float FieldLength = 12164f;

	public const float HalfWidth = 4096f;

	public const float HalfLength = 5120f;

	public const float Height = 2044f;

	public const float GoalHalfWidth = 892f;

	public const float GoalHeight = 642f;

	public const float GoalDepth = 880f;

	public static bool IsCollisionName(FbxMesh m)
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
		foreach (string item in list)
		{
			string text = item.Trim().ToLowerInvariant();
			if (text.StartsWith("ucx_") || text.StartsWith("ubx_") || text.StartsWith("usp_") || text.StartsWith("ucp_"))
			{
				return true;
			}
			if (text.Contains("collision") || text.Contains("collider") || text.Contains("physics") || text.EndsWith("_col") || text.StartsWith("col_") || text.EndsWith("_phys"))
			{
				return true;
			}
		}
		return false;
	}

	public static MapMesh Build(FbxScene scene, ModelFit fit, MapCollisionMode mode = MapCollisionMode.Auto, bool useMaterialColors = false, TexImage? picture = null, IReadOnlyList<NamedPicture>? pictures = null)
	{
		Vector3 up = Axis(scene.UpAxis, scene.UpSign);
		Vector3 front = Axis(scene.FrontAxis, scene.FrontSign);
		if (Math.Abs(Vector3.Dot(up, front)) > 0.5f)
		{
			front = ((Vector3.Cross(up, Vector3.UnitX).LengthSquared() > 0.1f) ? Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX)) : Vector3.UnitY);
		}
		front = Vector3.Transform(front, Quaternion.CreateFromAxisAngle(up, (float)fit.Turns * (float)Math.PI / 2f));
		if (fit.FlipFrontBack)
		{
			front = -front;
		}
		Vector3 right = Vector3.Cross(front, up);
		Quaternion fine = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)fit.Yaw * ((float)Math.PI / 180f)) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, (0f - (float)fit.Pitch) * ((float)Math.PI / 180f)) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, (0f - (float)fit.Roll) * ((float)Math.PI / 180f));
		bool rotated = fit.Yaw != 0.0 || fit.Pitch != 0.0 || fit.Roll != 0.0;
		List<FbxMesh> list = scene.Meshes.Where(IsCollisionName).ToList();
		List<FbxMesh> list2 = scene.Meshes.Where((FbxMesh m) => !IsCollisionName(m)).ToList();
		if (list2.Count == 0)
		{
			list2 = scene.Meshes.ToList();
		}
		List<FbxMesh> list3 = ((mode == MapCollisionMode.Auto && list.Count > 0 && list.Count < scene.Meshes.Count) ? list : list2);
		List<Vector3> list4 = new List<Vector3>();
		List<Vector3> list5 = new List<Vector3>();
		List<Vector2> list6 = new List<Vector2>();
		foreach (FbxMesh item in list2)
		{
			for (int num = 0; num < item.Pos.Count; num++)
			{
				list4.Add(ToWorld(item.Pos[num]));
				list5.Add(ToWorld(item.Nrm[num]));
				list6.Add(new Vector2(item.Uv[num].X, 1f - item.Uv[num].Y));
			}
		}
		if (list4.Count == 0)
		{
			throw new FbxFormatException("That model has no triangles.");
		}
		TexImage atlas = null;
		if (pictures != null && pictures.Count > 1)
		{
			atlas = CarMeshBuilder.PackMaterialColors(list2, list6, pictures.Select((NamedPicture x) => x.Image).ToList(), CarMeshBuilder.AssignPictures(list2, pictures));
		}
		else if (useMaterialColors)
		{
			List<FbxMesh> meshes = list2;
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
			atlas = CarMeshBuilder.PackMaterialColors(meshes, list6, pictures2, (FbxMaterial m) => (!m.HasTexture || picture == null) ? (-1) : 0);
		}
		Vector3 vector = new Vector3(float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue);
		foreach (Vector3 item2 in list4)
		{
			vector = Vector3.Min(vector, item2);
			vector2 = Vector3.Max(vector2, item2);
		}
		Vector3 vector3 = vector2 - vector;
		if (vector3.X < 1E-06f || vector3.Y < 1E-06f || vector3.Z < 1E-06f)
		{
			throw new FbxFormatException("That model is flat or empty.");
		}
		float num2 = MathF.Min(8512f / vector3.X, 12164f / vector3.Y) * (float)fit.Size;
		if (!float.IsFinite(num2) || num2 <= 0f)
		{
			throw new FbxFormatException("Couldn't work out a size for that model.");
		}
		Vector3 vector4 = new Vector3((vector.X + vector2.X) / 2f, (vector.Y + vector2.Y) / 2f, vector.Z);
		Vector3 vector5 = new Vector3((float)fit.OffsetX, (float)fit.OffsetY, (float)fit.Lift);
		for (int num3 = 0; num3 < list4.Count; num3++)
		{
			list4[num3] = (list4[num3] - vector4) * num2 + vector5;
		}
		MapMesh mapMesh = new MapMesh
		{
			Visual = CarMeshBuilder.Weld(list4, list5, list6, list2)
		};
		mapMesh.Visual.Atlas = atlas;
		if (mode == MapCollisionMode.SimpleBox)
		{
			mapMesh.Collision = ArenaBox();
			return mapMesh;
		}
		List<Vector3> list7 = new List<Vector3>();
		foreach (FbxMesh item3 in list3)
		{
			for (int num4 = 0; num4 < item3.Pos.Count; num4++)
			{
				list7.Add((ToWorld(item3.Pos[num4]) - vector4) * num2 + vector5);
			}
		}
		List<int> list8 = new List<int>();
		for (int num5 = 0; num5 + 2 < list7.Count; num5 += 3)
		{
			list8.Add(num5);
			list8.Add(num5 + 1);
			list8.Add(num5 + 2);
		}
		mapMesh.Collision = new CarMesh
		{
			Pos = list7.ToArray(),
			Nrm = new Vector3[list7.Count],
			Uv = new Vector2[list7.Count],
			Idx = list8.ToArray()
		};
		return mapMesh;
		static Vector3 Axis(int axis, int sign)
		{
			return axis switch
			{
				0 => Vector3.UnitX * sign, 
				1 => Vector3.UnitY * sign, 
				_ => Vector3.UnitZ * sign, 
			};
		}
		Vector3 ToWorld(Vector3 v)
		{
			Vector3 value = new Vector3(Vector3.Dot(v, front), Vector3.Dot(v, right), Vector3.Dot(v, up));
			if (rotated)
			{
				value = Vector3.Transform(value, fine);
			}
			return new Vector3(0f - value.Y, value.X, value.Z);
		}
	}

	public static (Vector3[] Pos, int[] Idx) WeldPositions(CarMesh src, float tolerance)
	{
		Dictionary<(int, int, int), int> map = new Dictionary<(int, int, int), int>();
		List<Vector3> all = new List<Vector3>();
		List<int> list = new List<int>(src.Idx.Length);
		float inv = 1f / Math.Max(tolerance, 0.0001f);
		for (int i = 0; i + 2 < src.Idx.Length; i += 3)
		{
			int num = Get(src.Pos[src.Idx[i]]);
			int num2 = Get(src.Pos[src.Idx[i + 1]]);
			int num3 = Get(src.Pos[src.Idx[i + 2]]);
			if (num != num2 && num2 != num3 && num != num3)
			{
				list.Add(num);
				list.Add(num2);
				list.Add(num3);
			}
		}
		int[] array = new int[all.Count];
		Array.Fill(array, -1);
		List<Vector3> list2 = new List<Vector3>();
		int[] array2 = new int[list.Count];
		for (int j = 0; j < list.Count; j++)
		{
			if (array[list[j]] < 0)
			{
				array[list[j]] = list2.Count;
				list2.Add(all[list[j]]);
			}
			array2[j] = array[list[j]];
		}
		return (Pos: list2.ToArray(), Idx: array2);
		int Get(Vector3 p)
		{
			(int, int, int) key = ((int)MathF.Round(p.X * inv), (int)MathF.Round(p.Y * inv), (int)MathF.Round(p.Z * inv));
			if (map.TryGetValue(key, out var value))
			{
				return value;
			}
			value = all.Count;
			map[key] = value;
			all.Add(p);
			return value;
		}
	}

	public static CarMesh ArenaBox()
	{
		List<Vector3> pos = new List<Vector3>();
		List<int> idx = new List<int>();
		float num = 4096f;
		float num2 = 5120f;
		float z = 2044f;
		float num3 = 892f;
		float z2 = 642f;
		float num4 = 880f;
		Quad(new Vector3(0f - num, 0f - num2, 0f), new Vector3(0f - num, num2, 0f), new Vector3(num, num2, 0f), new Vector3(num, 0f - num2, 0f));
		Quad(new Vector3(0f - num, 0f - num2, z), new Vector3(num, 0f - num2, z), new Vector3(num, num2, z), new Vector3(0f - num, num2, z));
		Quad(new Vector3(0f - num, 0f - num2, 0f), new Vector3(0f - num, 0f - num2, z), new Vector3(0f - num, num2, z), new Vector3(0f - num, num2, 0f));
		Quad(new Vector3(num, 0f - num2, 0f), new Vector3(num, num2, 0f), new Vector3(num, num2, z), new Vector3(num, 0f - num2, z));
		float[] array = new float[2] { -1f, 1f };
		foreach (float num5 in array)
		{
			float y = num5 * num2;
			float y2 = num5 * (num2 + num4);
			Quad(new Vector3(0f - num, y, 0f), new Vector3(0f - num3, y, 0f), new Vector3(0f - num3, y, z), new Vector3(0f - num, y, z));
			Quad(new Vector3(num3, y, 0f), new Vector3(num, y, 0f), new Vector3(num, y, z), new Vector3(num3, y, z));
			Quad(new Vector3(0f - num3, y, z2), new Vector3(num3, y, z2), new Vector3(num3, y, z), new Vector3(0f - num3, y, z));
			Quad(new Vector3(0f - num3, y, 0f), new Vector3(0f - num3, y2, 0f), new Vector3(num3, y2, 0f), new Vector3(num3, y, 0f));
			Quad(new Vector3(0f - num3, y, z2), new Vector3(num3, y, z2), new Vector3(num3, y2, z2), new Vector3(0f - num3, y2, z2));
			Quad(new Vector3(0f - num3, y, 0f), new Vector3(0f - num3, y, z2), new Vector3(0f - num3, y2, z2), new Vector3(0f - num3, y2, 0f));
			Quad(new Vector3(num3, y, 0f), new Vector3(num3, y2, 0f), new Vector3(num3, y2, z2), new Vector3(num3, y, z2));
			Quad(new Vector3(0f - num3, y2, 0f), new Vector3(0f - num3, y2, z2), new Vector3(num3, y2, z2), new Vector3(num3, y2, 0f));
		}
		return new CarMesh
		{
			Pos = pos.ToArray(),
			Nrm = new Vector3[pos.Count],
			Uv = new Vector2[pos.Count],
			Idx = idx.ToArray()
		};
		void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			int count = pos.Count;
			pos.Add(a);
			pos.Add(b);
			pos.Add(c);
			pos.Add(d);
			idx.AddRange(new _003C_003Ez__ReadOnlyArray<int>(new int[6]
			{
				count,
				count + 1,
				count + 2,
				count,
				count + 2,
				count + 3
			}));
		}
	}

	public static List<CarMesh> Split(CarMesh mesh, int maxVerts, int maxParts, int firstPartMax)
	{
		if (mesh.VertexCount <= firstPartMax)
		{
			int num = 1;
			List<CarMesh> list = new List<CarMesh>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = mesh;
			return list;
		}
		(Vector3 Min, Vector3 Max) tuple = mesh.Bounds();
		Vector3 item = tuple.Min;
		Vector3 vector = tuple.Max - item;
		int axis = ((!(vector.X >= vector.Y)) ? 1 : 0);
		int triangleCount = mesh.TriangleCount;
		List<int> list2 = (from t in Enumerable.Range(0, triangleCount)
			orderby (Key(mesh.Pos[mesh.Idx[3 * t]]) + Key(mesh.Pos[mesh.Idx[3 * t + 1]]) + Key(mesh.Pos[mesh.Idx[3 * t + 2]])) / 3f
			select t).ToList();
		List<CarMesh> parts = new List<CarMesh>();
		List<Vector3> pos = new List<Vector3>();
		List<Vector3> nrm = new List<Vector3>();
		List<Vector2> uv = new List<Vector2>();
		List<int> idx = new List<int>();
		Dictionary<int, int> remap = new Dictionary<int, int>();
		foreach (int item2 in list2)
		{
			int num2 = ((parts.Count == 0) ? firstPartMax : maxVerts);
			int num3 = 0;
			for (int num4 = 0; num4 < 3; num4++)
			{
				if (!remap.ContainsKey(mesh.Idx[3 * item2 + num4]))
				{
					num3++;
				}
			}
			if (pos.Count + num3 > num2)
			{
				Flush();
				num2 = maxVerts;
			}
			for (int num5 = 0; num5 < 3; num5++)
			{
				int num6 = mesh.Idx[3 * item2 + num5];
				if (!remap.TryGetValue(num6, out var value))
				{
					value = pos.Count;
					remap[num6] = value;
					pos.Add(mesh.Pos[num6]);
					nrm.Add(mesh.Nrm[num6]);
					uv.Add(mesh.Uv[num6]);
				}
				idx.Add(value);
			}
		}
		Flush();
		if (parts.Count > maxParts)
		{
			throw new UpkException($"That map has {mesh.VertexCount:N0} vertices and {mesh.TriangleCount:N0} triangles, which is more than {maxParts} pieces of {maxVerts:N0} vertices, the most any map has room for. Make the model lighter and try again.");
		}
		parts[0].Atlas = mesh.Atlas;
		return parts;
		void Flush()
		{
			if (idx.Count != 0)
			{
				parts.Add(new CarMesh
				{
					Pos = pos.ToArray(),
					Nrm = nrm.ToArray(),
					Uv = uv.ToArray(),
					Idx = idx.ToArray()
				});
				pos.Clear();
				nrm.Clear();
				uv.Clear();
				idx.Clear();
				remap.Clear();
			}
		}
		float Key(Vector3 v)
		{
			if (axis != 0)
			{
				return v.Y;
			}
			return v.X;
		}
	}
}
