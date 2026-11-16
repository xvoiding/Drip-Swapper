using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.RocketLeague;

public sealed class MapAnalysis
{
	public Dictionary<int, MapMeshInfo> Meshes = new Dictionary<int, MapMeshInfo>();

	public List<MapPlacedMesh> Placed = new List<MapPlacedMesh>();

	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	public static bool IsKeepName(string mesh)
	{
		if (!mesh.Contains("boost", StringComparison.OrdinalIgnoreCase) && !mesh.StartsWith("Plane", StringComparison.OrdinalIgnoreCase))
		{
			return mesh.Contains("sprite", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static Matrix4x4 RotationMatrix(int pitch, int yaw, int roll)
	{
		float num = (float)Math.Sin((double)pitch * 9.587379924285257E-05);
		float num2 = (float)Math.Cos((double)pitch * 9.587379924285257E-05);
		float num3 = (float)Math.Sin((double)yaw * 9.587379924285257E-05);
		float num4 = (float)Math.Cos((double)yaw * 9.587379924285257E-05);
		float num5 = (float)Math.Sin((double)roll * 9.587379924285257E-05);
		float num6 = (float)Math.Cos((double)roll * 9.587379924285257E-05);
		return new Matrix4x4(num2 * num4, num2 * num3, num, 0f, num5 * num * num4 - num6 * num3, num5 * num * num3 + num6 * num4, (0f - num5) * num2, 0f, 0f - (num6 * num * num4 + num5 * num3), num4 * num5 - num6 * num * num3, num6 * num2, 0f, 0f, 0f, 0f, 1f);
	}

	private static Vector3? Vec(UpkPropList l, string name)
	{
		UpkProp upkProp = l.Find(name);
		if (upkProp == null || upkProp.Payload.Length != 12)
		{
			return null;
		}
		return new Vector3(BitConverter.ToSingle(upkProp.Payload, 0), BitConverter.ToSingle(upkProp.Payload, 4), BitConverter.ToSingle(upkProp.Payload, 8));
	}

	private static (int P, int Y, int R)? Rot(UpkPropList l)
	{
		UpkProp upkProp = l.Find("Rotation");
		if (upkProp == null || upkProp.Payload.Length != 12)
		{
			return null;
		}
		return (BinaryPrimitives.ReadInt32LittleEndian(upkProp.Payload), BinaryPrimitives.ReadInt32LittleEndian(upkProp.Payload.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(upkProp.Payload.AsSpan(8)));
	}

	private static float Flt(UpkPropList l, string name, float def)
	{
		UpkProp upkProp = l.Find(name);
		if (upkProp == null || upkProp.Payload.Length != 4)
		{
			return def;
		}
		return BitConverter.ToSingle(upkProp.Payload, 0);
	}

	private static bool Bool(UpkPropList l, string name, bool def)
	{
		UpkProp upkProp = l.Find(name);
		if (upkProp == null || upkProp.Extra.Length == 0)
		{
			return def;
		}
		return upkProp.Extra[0] != 0;
	}

	private static Matrix4x4 Place(Vector3 scale, (int P, int Y, int R)? rot, Vector3 t)
	{
		Matrix4x4 matrix4x = Matrix4x4.CreateScale(scale);
		if (rot.HasValue)
		{
			(int, int, int) valueOrDefault = rot.GetValueOrDefault();
			matrix4x *= RotationMatrix(valueOrDefault.Item1, valueOrDefault.Item2, valueOrDefault.Item3);
		}
		return matrix4x * Matrix4x4.CreateTranslation(t);
	}

	public static MapAnalysis Analyze(byte[] data, UpkSummary s, List<NameEntry> names)
	{
		List<UpkExport> ex = UpkTables.Exports(data, s);
		MapAnalysis mapAnalysis = new MapAnalysis();
		foreach (UpkExport item in ex.Where((UpkExport e) => e.ClassName == "StaticMesh"))
		{
			MapMeshInfo mapMeshInfo = new MapMeshInfo
			{
				Index = item.Index,
				Name = item.Name,
				OldSize = item.SerialSize
			};
			try
			{
				byte[] array = data.AsSpan(item.SerialOffset, item.SerialSize).ToArray();
				StaticMeshWriter.Layout layout = StaticMeshWriter.ReadLayout(array, names);
				mapMeshInfo.OneSection = true;
				mapMeshInfo.Origin = new Vector3(BitConverter.ToSingle(array, layout.Props), BitConverter.ToSingle(array, layout.Props + 4), BitConverter.ToSingle(array, layout.Props + 8));
				mapMeshInfo.Extent = new Vector3(BitConverter.ToSingle(array, layout.Props + 12), BitConverter.ToSingle(array, layout.Props + 16), BitConverter.ToSingle(array, layout.Props + 20));
			}
			catch (Exception ex2) when ((ex2 is UpkException || ex2 is ArgumentException || ex2 is IndexOutOfRangeException) ? true : false)
			{
			}
			mapAnalysis.Meshes[item.Index] = mapMeshInfo;
		}
		Dictionary<int, (bool Ok, Matrix4x4 M, bool Hidden, string Class, string Name)> ownerCache = new Dictionary<int, (bool, Matrix4x4, bool, string, string)>();
		foreach (UpkExport item2 in ex.Where((UpkExport e) => e.ClassName == "StaticMeshComponent"))
		{
			UpkPropList upkPropList;
			try
			{
				upkPropList = UpkPropList.Read(data.AsSpan(item2.SerialOffset, item2.SerialSize).ToArray(), names);
			}
			catch (UpkException)
			{
				continue;
			}
			UpkProp upkProp = upkPropList.Find("StaticMesh");
			if (upkProp == null || upkProp.Payload.Length != 4)
			{
				continue;
			}
			int num = upkProp.Int();
			if (num > 0 && mapAnalysis.Meshes.TryGetValue(num, out MapMeshInfo value))
			{
				(bool, Matrix4x4, bool, string, string) tuple = Owner(I(data, item2.Start + 8));
				Matrix4x4 matrix4x = Place((Vec(upkPropList, "Scale3D") ?? Vector3.One) * Flt(upkPropList, "Scale", 1f), Rot(upkPropList), Vec(upkPropList, "Translation") ?? Vector3.Zero);
				MapPlacedMesh mapPlacedMesh = new MapPlacedMesh
				{
					CompIndex = item2.Index,
					MeshIndex = num,
					MeshName = value.Name,
					OwnerClass = tuple.Item4,
					OwnerName = tuple.Item5,
					World = matrix4x * tuple.Item2,
					Hidden = Bool(upkPropList, "HiddenGame", def: false),
					Movable = (!tuple.Item1 || tuple.Item3),
					Keep = IsKeepName(value.Name)
				};
				mapPlacedMesh.Identity = IsIdentity(mapPlacedMesh.World);
				Vector3 vector = new Vector3(float.MaxValue);
				Vector3 vector2 = new Vector3(float.MinValue);
				for (int num2 = 0; num2 < 8; num2++)
				{
					Vector3 value2 = Vector3.Transform(value.Origin + new Vector3(((num2 & 1) == 0) ? (0f - value.Extent.X) : value.Extent.X, ((num2 & 2) == 0) ? (0f - value.Extent.Y) : value.Extent.Y, ((num2 & 4) == 0) ? (0f - value.Extent.Z) : value.Extent.Z), mapPlacedMesh.World);
					vector = Vector3.Min(vector, value2);
					vector2 = Vector3.Max(vector2, value2);
				}
				mapPlacedMesh.WorldMin = vector;
				mapPlacedMesh.WorldMax = vector2;
				mapAnalysis.Placed.Add(mapPlacedMesh);
			}
		}
		return mapAnalysis;
		(bool Ok, Matrix4x4 M, bool Hidden, string Class, string Name) Owner(int idx)
		{
			if (ownerCache.TryGetValue(idx, out (bool, Matrix4x4, bool, string, string) value3))
			{
				return value3;
			}
			(bool, Matrix4x4, bool, string, string) tuple2 = (false, Matrix4x4.Identity, false, "", "");
			if (idx > 0 && idx <= ex.Count)
			{
				UpkExport upkExport = ex[idx - 1];
				if (upkExport.ClassName.Contains("StaticMesh", StringComparison.Ordinal) && !upkExport.ClassName.Contains("Component", StringComparison.Ordinal))
				{
					try
					{
						UpkPropList l = UpkPropList.Read(data.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names);
						if (upkExport.ClassName == "StaticMeshCollectionActor")
						{
							tuple2 = (true, Matrix4x4.Identity, Bool(l, "bHidden", def: false), upkExport.ClassName, upkExport.Name);
						}
						else
						{
							Vector3 scale = (Vec(l, "DrawScale3D") ?? Vector3.One) * Flt(l, "DrawScale", 1f);
							tuple2 = (true, Place(scale, Rot(l), Vec(l, "Location") ?? Vector3.Zero), Bool(l, "bHidden", def: false), upkExport.ClassName, upkExport.Name);
						}
					}
					catch (UpkException)
					{
						tuple2 = (false, Matrix4x4.Identity, false, upkExport.ClassName, upkExport.Name);
					}
				}
				else
				{
					tuple2 = (false, Matrix4x4.Identity, false, upkExport.ClassName, upkExport.Name);
				}
			}
			ownerCache[idx] = tuple2;
			return tuple2;
		}
	}

	public static bool IsIdentity(Matrix4x4 m)
	{
		Matrix4x4 identity = Matrix4x4.Identity;
		if (MathF.Abs(m.M11 - identity.M11) < 0.0001f && MathF.Abs(m.M12) < 0.0001f && MathF.Abs(m.M13) < 0.0001f && MathF.Abs(m.M21) < 0.0001f && MathF.Abs(m.M22 - 1f) < 0.0001f && MathF.Abs(m.M23) < 0.0001f && MathF.Abs(m.M31) < 0.0001f && MathF.Abs(m.M32) < 0.0001f && MathF.Abs(m.M33 - 1f) < 0.0001f && MathF.Abs(m.M41) < 0.001f && MathF.Abs(m.M42) < 0.001f)
		{
			return MathF.Abs(m.M43) < 0.001f;
		}
		return false;
	}
}
