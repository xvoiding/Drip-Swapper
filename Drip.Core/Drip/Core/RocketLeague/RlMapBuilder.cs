using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.RocketLeague;

public static class RlMapBuilder
{
	public sealed record Capacity(int VisualPieces, int PhysicsPieces)
	{
		public int Vertices => VisualPieces * 65535;
	}

	private sealed class Candidate
	{
		public MapPlacedMesh Comp;

		public MapMeshInfo Mesh;

		public bool AllHidden;
	}

	public const string TargetFile = "Labs_Underpass_P.upk";

	public const string TargetName = "Underpass (Labs)";

	public static string Describe(MapMaterial m)
	{
		return m switch
		{
			MapMaterial.LabsSimple => "Labs panels (stock field look)", 
			MapMaterial.LabsTrim => "Labs trim", 
			MapMaterial.Net => "Goal net", 
			_ => "Boost pad (shows your picture)", 
		};
	}

	private static (string Export, string Class, string? Param, string? TextureName) MaterialInfo(MapMaterial m)
	{
		return m switch
		{
			MapMaterial.LabsSimple => (Export: "MIC_Labs_Simple_NoLazer", Class: "MaterialInstanceConstant", Param: "RGB_Pack", TextureName: (string?)null), 
			MapMaterial.LabsTrim => (Export: "MIC_Labs_Trim00", Class: "MaterialInstanceConstant", Param: "RGB_Pack", TextureName: null), 
			MapMaterial.Net => (Export: "NetNonmove_Mat", Class: "Material", Param: null, TextureName: "Net_D"), 
			_ => (Export: "BoostPad_Large_MIC", Class: "MaterialInstanceConstant", Param: "Diffuse", TextureName: null), 
		};
	}

	private static List<Candidate> FindCarriers(MapAnalysis an, MapBuildOptions opt)
	{
		List<Candidate> list = new List<Candidate>();
		foreach (IGrouping<int, MapPlacedMesh> item in from p in an.Placed
			group p by p.MeshIndex)
		{
			MapMeshInfo mapMeshInfo = an.Meshes[item.Key];
			if (!mapMeshInfo.OneSection || item.Any((MapPlacedMesh p) => p.Keep || p.Movable) || (opt.KeepScenery && item.Any((MapPlacedMesh p) => !p.Hidden && !p.InsideArena)))
			{
				continue;
			}
			MapPlacedMesh mapPlacedMesh = item.Where((MapPlacedMesh p) => p.Identity).FirstOrDefault() ?? (from p in item
				where Matrix4x4.Invert(p.World, out var _) && MathF.Abs(p.World.GetDeterminant()) > 0.0001f
				orderby (p.World.GetDeterminant() < 0f) ? 1 : 0
				select p).FirstOrDefault();
			if (mapPlacedMesh != null)
			{
				list.Add(new Candidate
				{
					Comp = mapPlacedMesh,
					Mesh = mapMeshInfo,
					AllHidden = item.All((MapPlacedMesh p) => p.Hidden)
				});
			}
		}
		return list;
	}

	public static Capacity? Measure(byte[] img, MapBuildOptions? options = null)
	{
		try
		{
			UpkSummary s = UpkSummary.Parse(img);
			List<NameEntry> names = UpkNames.Read(img, s, out var _);
			List<Candidate> list = FindCarriers(MapAnalysis.Analyze(img, s, names), options ?? new MapBuildOptions());
			int num = Enumerable.Count(list, (Candidate c) => c.AllHidden);
			if (list.Count == 0 || num == 0)
			{
				return null;
			}
			return new Capacity(list.Count - 1, num);
		}
		catch (UpkException)
		{
			return null;
		}
	}

	public static List<CarMesh> CollisionPieces(CarMesh collision, int maxPieces, Action<string>? say = null)
	{
		CarMesh carMesh = TryCarrier(collision);
		if (carMesh != null)
		{
			int num = 1;
			List<CarMesh> list = new List<CarMesh>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = carMesh;
			return list;
		}
		maxPieces = Math.Max(1, maxPieces);
		float[] array = new float[5] { 0.0625f, 0.5f, 2f, 8f, 32f };
		foreach (float num2 in array)
		{
			var (array2, array3) = MapGeometry.WeldPositions(collision, num2);
			if (array3.Length == 0)
			{
				continue;
			}
			CarMesh carMesh2 = new CarMesh
			{
				Pos = array2,
				Nrm = array2.Select((Vector3 _) => Vector3.UnitZ).ToArray(),
				Uv = new Vector2[array2.Length],
				Idx = array3
			};
			try
			{
				List<CarMesh> result = MapGeometry.Split(carMesh2, 65535, maxPieces, 65535);
				if (num2 > 0.0625f)
				{
					say?.Invoke($"The collision was too detailed for this map's physics pieces ({collision.TriangleCount:N0} triangles), so it was simplified to {carMesh2.TriangleCount:N0} triangles (corners merged within {num2:0.##} units). Collision is a bit rougher than the picture.");
				}
				return result;
			}
			catch (UpkException)
			{
			}
		}
		say?.Invoke($"The collision was too detailed for this map's physics pieces ({collision.TriangleCount:N0} triangles), so the standard arena shell is used for collision and your model is for looks only. If you want your model's own collision, pick a map with more room or a model with lighter collision.");
		return CollisionPieces(MapGeometry.ArenaBox(), maxPieces, say);
	}

	private static CarMesh? TryCarrier(CarMesh collision)
	{
		var (array, array2) = MapGeometry.WeldPositions(collision, 0.0625f);
		if (array2.Length == 0 || array.Length > 65535)
		{
			return null;
		}
		return new CarMesh
		{
			Pos = array,
			Nrm = array.Select((Vector3 _) => Vector3.UnitZ).ToArray(),
			Uv = new Vector2[array.Length],
			Idx = array2
		};
	}

	public static CarMesh ToLocal(CarMesh world, Matrix4x4 place)
	{
		if (!Matrix4x4.Invert(place, out var result))
		{
			throw new UpkException("A piece of this map is placed in a way that can't be turned around.");
		}
		bool flag = place.GetDeterminant() < 0f;
		Matrix4x4 matrix = Matrix4x4.Transpose(new Matrix4x4(place.M11, place.M12, place.M13, 0f, place.M21, place.M22, place.M23, 0f, place.M31, place.M32, place.M33, 0f, 0f, 0f, 0f, 1f));
		Vector3[] array = new Vector3[world.VertexCount];
		Vector3[] array2 = new Vector3[world.VertexCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Vector3.Transform(world.Pos[i], result);
			Vector3 value = Vector3.TransformNormal(world.Nrm[i], matrix);
			array2[i] = ((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : Vector3.UnitZ);
		}
		int[] array3 = (int[])world.Idx.Clone();
		if (flag)
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
		return new CarMesh
		{
			Pos = array,
			Nrm = array2,
			Uv = world.Uv,
			Idx = array3
		};
	}

	public static byte[] Build(DripRlFile map, byte[] targetFile, byte[] key, List<string>? report = null)
	{
		return Build(map, targetFile, key, new MapBuildOptions(), report);
	}

	public static byte[] Build(DripRlFile map, byte[] targetFile, byte[] key, MapBuildOptions options, List<string>? report = null)
	{
		if (map.Mesh.VertexCount < 3)
		{
			throw new UpkException("That map has no model in it.");
		}
		UpkImage upkImage = UpkCodec.Decrypt(targetFile, key);
		byte[] data = upkImage.Data;
		UpkSummary s = UpkSummary.Parse(data);
		List<NameEntry> names = UpkNames.Read(data, s, out var _);
		MapAnalysis mapAnalysis = MapAnalysis.Analyze(data, s, names);
		List<UpkExport> exports = UpkTables.Exports(data, s);
		List<Candidate> list = FindCarriers(mapAnalysis, options);
		List<Candidate> list2 = (from c in list
			where c.AllHidden
			orderby c.Mesh.OldSize descending
			select c).ToList();
		if (list.Count < 2 || list2.Count == 0)
		{
			throw new UpkException("This isn't a map file the app knows how to edit (it has no meshes the map can be built into). Verify your game files in the launcher and try again.");
		}
		List<CarMesh> list3 = CollisionPieces((map.Collision.VertexCount >= 3) ? map.Collision : map.Mesh, list2.Count, Say);
		List<Candidate> list4 = list2.Take(list3.Count).ToList();
		List<Candidate> list5 = (from c in list.Except(list4)
			orderby c.AllHidden ? 1 : 0, c.Mesh.OldSize descending
			select c).ToList();
		List<CarMesh> list6;
		try
		{
			list6 = MapGeometry.Split(map.Mesh, 65535, list5.Count, 65535);
		}
		catch (UpkException)
		{
			throw new UpkException($"That map has {map.Mesh.VertexCount:N0} vertices and {map.Mesh.TriangleCount:N0} triangles. {options.TargetName} has room for about {list5.Count * 65535:N0} vertices ({list5.Count} pieces of {65535:N0}). Make the model lighter, or pick a map with more room.");
		}
		Say($"Picture: {map.Mesh.TriangleCount:N0} triangles in {list6.Count} of {list5.Count} pieces. Collision: {list3.Sum((CarMesh p) => p.TriangleCount):N0} triangles in {list3.Count} piece{((list3.Count == 1) ? "" : "s")}.");
		(string, string, string, string) tuple = MaterialInfo(options.Material);
		string matName = tuple.Item1;
		string matClass = tuple.Item2;
		string item = tuple.Item3;
		string matTexName = tuple.Item4;
		int index = (exports.FirstOrDefault((UpkExport e) => e.ClassName == matClass && e.Name == matName) ?? throw new UpkException("This map file has no " + matName + ", so the map can't be dressed. Pick another surface look.")).Index;
		Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
		Dictionary<int, Candidate> dictionary2 = new Dictionary<int, Candidate>();
		HashSet<int> hashSet = new HashSet<int>();
		for (int num = 0; num < list6.Count; num++)
		{
			Candidate candidate = list5[num];
			CarMesh mesh = ToLocal(list6[num], candidate.Comp.World);
			dictionary[candidate.Mesh.Index] = MapMeshWriter.Build(Slice(candidate.Mesh.Index), names, mesh, solid: false);
			dictionary2[candidate.Comp.CompIndex] = candidate;
		}
		for (int num2 = 0; num2 < list3.Count; num2++)
		{
			Candidate candidate2 = list4[num2];
			CarMesh mesh2 = ToLocal(list3[num2], candidate2.Comp.World);
			dictionary[candidate2.Mesh.Index] = MapMeshWriter.Build(Slice(candidate2.Mesh.Index), names, mesh2, solid: true);
			hashSet.Add(candidate2.Comp.CompIndex);
		}
		Dictionary<int, MapPlacedMesh> dictionary3 = mapAnalysis.Placed.ToDictionary((MapPlacedMesh p) => p.CompIndex);
		int num3 = 0;
		foreach (UpkExport item2 in exports.Where((UpkExport e) => e.ClassName == "StaticMeshComponent"))
		{
			if (!dictionary3.TryGetValue(item2.Index, out var value) || value.Keep || value.Movable)
			{
				continue;
			}
			bool flag = dictionary2.ContainsKey(item2.Index);
			bool flag2 = hashSet.Contains(item2.Index);
			if (!flag && !flag2 && ((options.KeepScenery && !value.Hidden && !value.InsideArena) || !IsGeometry(mapAnalysis, value)))
			{
				continue;
			}
			UpkPropList upkPropList = UpkPropList.Read(Slice(item2.Index), names);
			UpkProp upkProp = upkPropList.Find("StaticMesh");
			if (upkProp != null)
			{
				if (flag)
				{
					SetArray(upkPropList, names, "Materials", new int[1] { index });
					SetBool(upkPropList, names, "BlockRigidBody", value: false);
					SetBool(upkPropList, names, "HiddenGame", value: false);
					Remove(upkPropList, "bOverrideLightMapRes", "OverriddenLightMapRes", "VertexPositionVersionNumber");
				}
				else if (flag2)
				{
					Remove(upkPropList, "VertexPositionVersionNumber");
				}
				else
				{
					upkProp.SetInt(0);
					Remove(upkPropList, "Materials", "VertexPositionVersionNumber", "bOverrideLightMapRes", "OverriddenLightMapRes");
					num3++;
				}
				upkPropList.Tail = new byte[4];
				dictionary[item2.Index] = upkPropList.Write();
			}
		}
		Say($"{dictionary2.Count + hashSet.Count} map pieces rewritten, {num3} stock pieces switched off.");
		data = UpkTables.ReplaceMany(data, s, dictionary, out List<(long, int, int)> edits);
		if (map.Picture.Length != 0)
		{
			int num4 = 0;
			UpkSummary s2 = UpkSummary.Parse(data);
			List<UpkExport> list7 = UpkTables.Exports(data, s2);
			if (item != null)
			{
				UpkExport upkExport = list7[index - 1];
				if (MaterialTools.TextureParams(data.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names).TryGetValue(item, out var value2) && value2 > 0)
				{
					num4 = value2;
				}
			}
			else if (matTexName != null)
			{
				num4 = list7.FirstOrDefault((UpkExport e) => e.ClassName == "Texture2D" && e.Name == matTexName)?.Index ?? 0;
			}
			if (num4 > 0)
			{
				TexImage pic = TexImage.Load(map.Picture).ForGame();
				data = RlCustomBuilder.PutTexture(data, num4, (byte[] old, List<NameEntry> nm) => pic, edits);
				Say($"Picture {pic.Width}x{pic.Height} put on {UpkTables.Exports(data, UpkSummary.Parse(data))[num4 - 1].Name}.");
			}
			else
			{
				Say("No picture slot found on that material; the map keeps the game's picture.");
			}
		}
		byte[] array = UpkCodec.Encrypt(upkImage, data, key, edits);
		if (UpkCodec.Decrypt(array, key).Data.Length != data.Length)
		{
			throw new UpkException("The custom map didn't rebuild cleanly.");
		}
		return array;
		void Say(string item2)
		{
			report?.Add(item2);
		}
		byte[] Slice(int exportIndex)
		{
			UpkExport upkExport2 = exports[exportIndex - 1];
			return data.AsSpan(upkExport2.SerialOffset, upkExport2.SerialSize).ToArray();
		}
	}

	private static bool IsGeometry(MapAnalysis an, MapPlacedMesh pm)
	{
		if (an.Meshes.TryGetValue(pm.MeshIndex, out MapMeshInfo value))
		{
			return value != null;
		}
		return false;
	}

	private static void Remove(UpkPropList l, params string[] names)
	{
		l.Props.RemoveAll((UpkProp p) => Array.IndexOf(names, p.Name) >= 0);
	}

	private static void SetArray(UpkPropList l, List<NameEntry> names, string name, int[] refs)
	{
		byte[] array = new byte[4 + 4 * refs.Length];
		BinaryPrimitives.WriteInt32LittleEndian(array, refs.Length);
		for (int i = 0; i < refs.Length; i++)
		{
			BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(4 + 4 * i), refs[i]);
		}
		UpkProp upkProp = l.Find(name);
		if (upkProp != null)
		{
			upkProp.Payload = array;
			return;
		}
		int num = UpkPropList.NameIndex(names, name);
		int num2 = UpkPropList.NameIndex(names, "ArrayProperty");
		if (num < 0 || num2 < 0)
		{
			throw new UpkException("This map file has no way to give a piece a material (the names the game uses for it aren't in the file).");
		}
		l.Props.Add(new UpkProp
		{
			NameIdx = num,
			TypeIdx = num2,
			Name = name,
			Type = "ArrayProperty",
			Payload = array
		});
	}

	private static void SetBool(UpkPropList l, List<NameEntry> names, string name, bool value)
	{
		UpkProp upkProp = l.Find(name);
		if (upkProp != null)
		{
			upkProp.Extra = new byte[1] { value ? ((byte)1) : ((byte)0) };
		}
		else if (value)
		{
			int num = UpkPropList.NameIndex(names, name);
			int num2 = UpkPropList.NameIndex(names, "BoolProperty");
			if (num >= 0 && num2 >= 0)
			{
				l.Props.Add(new UpkProp
				{
					NameIdx = num,
					TypeIdx = num2,
					Name = name,
					Type = "BoolProperty",
					Extra = new byte[1] { 1 }
				});
			}
		}
	}
}
