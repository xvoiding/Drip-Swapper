using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Drip.Core.FallGuys;
using Drip.Core.Mods;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class FnModelFile
{
	public const int MaxVertices = 400000;

	public const int MaxTriangles = 800000;

	public const int MaxMaterials = 12;

	public const int MaxPicturePixels = 16777216;

	private const uint Magic = 1296975428u;

	private const int Version = 1;

	private static readonly JsonSerializerOptions Out = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public static string Write(string path, string name, string author, string description, FnTarget tgt, FnRigInfo rig, string mode = "replace", FnSkin? skin = null, byte[]? icon = null)
	{
		ModManifest modManifest = new ModManifest
		{
			Name = name,
			Author = author,
			Description = description,
			Base = rig.BaseSkin,
			FnModel = new ModFnModel
			{
				Rig = rig.Id,
				Mesh = "model/mesh.bin",
				HasUv = tgt.HasUv,
				Mode = ((skin != null) ? "native" : mode),
				Skin = ((skin != null) ? "model/skin.bin" : "")
			}
		};
		return Save(path, modManifest, modManifest.FnModel, tgt, skin, icon);
	}

	public static string WriteItem(string path, string name, string author, string description, FnTarget tgt, string kind, byte[]? icon = null)
	{
		ModManifest modManifest = new ModManifest
		{
			Name = name,
			Author = author,
			Description = description,
			FnItem = new ModFnItem
			{
				Kind = kind,
				Model = new ModFnModel
				{
					Rig = "item",
					Mesh = "model/mesh.bin",
					HasUv = tgt.HasUv,
					Mode = "item"
				}
			}
		};
		return Save(path, modManifest, modManifest.FnItem.Model, tgt, null, icon);
	}

	private static string Save(string path, ModManifest manifest, ModFnModel section, FnTarget tgt, FnSkin? skin, byte[]? icon)
	{
		int num = tgt.Pos.Length / 3;
		int[] map = new int[num];
		Array.Fill(map, -1);
		int num2 = 0;
		int[] tri = tgt.Tri;
		foreach (int num3 in tri)
		{
			if (map[num3] < 0)
			{
				map[num3] = num2++;
			}
		}
		if (skin != null && skin.VertexCount != num)
		{
			throw new InvalidDataException("The rig doesn't have one entry per point of the model.");
		}
		float[] array = new float[num2 * 3];
		float[] array2 = new float[num2 * 2];
		for (int j = 0; j < num; j++)
		{
			int num4 = map[j];
			if (num4 >= 0)
			{
				array[num4 * 3] = (float)tgt.Pos[j * 3];
				array[num4 * 3 + 1] = (float)tgt.Pos[j * 3 + 1];
				array[num4 * 3 + 2] = (float)tgt.Pos[j * 3 + 2];
				array2[num4 * 2] = (float)tgt.Uv[j * 2];
				array2[num4 * 2 + 1] = (float)tgt.Uv[j * 2 + 1];
			}
		}
		if (num2 > 400000 || tgt.Tri.Length / 3 > 800000)
		{
			throw new InvalidDataException($"That model is too detailed ({num2:N0} points). Reduce it in a 3D program first (about {400}k points is the most).");
		}
		List<(string, byte[])> list = new List<(string, byte[])>();
		for (int k = 0; k < tgt.Materials.Count; k++)
		{
			FgSourceMaterial fgSourceMaterial = tgt.Materials[k];
			TexImage texImage = tgt.Override ?? fgSourceMaterial.Picture;
			ModFnMaterial modFnMaterial = new ModFnMaterial
			{
				Name = ((fgSourceMaterial.Name.Length > 60) ? fgSourceMaterial.Name.Substring(0, 60) : fgSourceMaterial.Name)
			};
			int i = 3;
			List<float> list2 = new List<float>(i);
			CollectionsMarshal.SetCount(list2, i);
			Span<float> span = CollectionsMarshal.AsSpan(list2);
			span[0] = fgSourceMaterial.Color.X;
			span[1] = fgSourceMaterial.Color.Y;
			span[2] = fgSourceMaterial.Color.Z;
			modFnMaterial.Color = list2;
			ModFnMaterial modFnMaterial2 = modFnMaterial;
			if (texImage != null && tgt.HasUv)
			{
				TexImage texImage2 = ((texImage.Width > 1024 || texImage.Height > 1024) ? texImage.ForGame(1024, 64) : texImage);
				string text = $"model/material{k}.png";
				list.Add((text, texImage2.ToPng()));
				modFnMaterial2.Picture = text;
			}
			section.Materials.Add(modFnMaterial2);
		}
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
		string text2 = path + ".tmp";
		using (FileStream stream = File.Create(text2))
		{
			using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
			WriteEntry(zip, "mod.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, Out)));
			WriteEntry(zip, "model/mesh.bin", MeshBytes(array, array2, tgt.Tri.Select((int v) => map[v]).ToArray(), tgt.TriMaterial));
			if (skin != null)
			{
				int[] array3 = new int[num2];
				for (int num5 = 0; num5 < num; num5++)
				{
					if (map[num5] >= 0)
					{
						array3[map[num5]] = num5;
					}
				}
				WriteEntry(zip, "model/skin.bin", skin.Subset(array3).ToBytes());
			}
			if (icon != null && icon.Length > 0)
			{
				WriteEntry(zip, "icon.png", icon);
			}
			foreach (var (name, data) in list)
			{
				WriteEntry(zip, name, data);
			}
		}
		File.Move(text2, path, overwrite: true);
		return path;
	}

	private static void WriteEntry(ZipArchive zip, string name, byte[] data)
	{
		using Stream stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
		stream.Write(data);
	}

	private static byte[] MeshBytes(float[] pos, float[] uv, int[] tri, int[] triMat)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(1296975428u);
		binaryWriter.Write(1);
		binaryWriter.Write(pos.Length / 3);
		binaryWriter.Write(tri.Length / 3);
		float[] array = pos;
		foreach (float value in array)
		{
			binaryWriter.Write(value);
		}
		array = uv;
		foreach (float value2 in array)
		{
			binaryWriter.Write(value2);
		}
		int[] array2 = tri;
		foreach (int value3 in array2)
		{
			binaryWriter.Write(value3);
		}
		array2 = triMat;
		foreach (int num in array2)
		{
			binaryWriter.Write((byte)num);
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	public static List<string> Validate(ModPack mod)
	{
		List<string> list = new List<string>();
		ModManifest manifest = mod.Manifest;
		ModFnModel fnModel = manifest.FnModel;
		FnRigInfo fnRigInfo = FnRigInfo.Find(fnModel.Rig);
		if (fnRigInfo == null)
		{
			list.Add("This model is for a character this version of Drip Swapper doesn't know ('" + fnModel.Rig + "').");
			return list;
		}
		if (!string.Equals(manifest.Base, fnRigInfo.BaseSkin, StringComparison.OrdinalIgnoreCase))
		{
			list.Add($"A model for {fnRigInfo.Name} has to use the base skin {fnRigInfo.BaseSkin}.");
		}
		if (manifest.Packages.Count > 0 || manifest.PartSwaps.Count > 0 || manifest.SlotSwaps.Count > 0)
		{
			list.Add("A model drip can't also carry package replacements or part swaps.");
		}
		ValidateShape(mod, fnModel, list);
		return list;
	}

	internal static void ValidateShape(ModPack mod, ModFnModel f, List<string> p)
	{
		if (!ModPack.SafeEntry(f.Mesh))
		{
			p.Add("The model's shape file has a bad path.");
		}
		bool flag = string.Equals(f.Mode, "native", StringComparison.OrdinalIgnoreCase);
		if (flag && (f.Skin.Length == 0 || !ModPack.SafeEntry(f.Skin)))
		{
			p.Add("A model that brings its own rig needs a rig file with a safe path.");
		}
		if (f.Materials.Count == 0 || f.Materials.Count > 12)
		{
			p.Add($"A model needs between 1 and {12} materials.");
		}
		foreach (ModFnMaterial material in f.Materials)
		{
			if (material.Color.Count != 3 || material.Color.Any((float c) => float.IsNaN(c) || c < 0f || c > 4f))
			{
				p.Add("A material has a bad colour.");
			}
			if (material.Picture.Length > 0 && !ModPack.SafeEntry(material.Picture))
			{
				p.Add("A material has a bad picture path.");
			}
		}
		if (!ModPack.IsZipForm(mod.FilePath))
		{
			p.Add("A model needs the .dripFN zip form.");
		}
		else
		{
			if (p.Count > 0)
			{
				return;
			}
			try
			{
				using ZipArchive zipArchive = ZipFile.OpenRead(mod.FilePath);
				ZipArchiveEntry entry = zipArchive.GetEntry(f.Mesh.Replace('\\', '/'));
				if (entry == null)
				{
					p.Add("The model's shape file is missing.");
					return;
				}
				if (entry.Length > 67108864)
				{
					p.Add("The model's shape file is too large.");
				}
				else
				{
					using Stream stream = entry.Open();
					byte[] array = new byte[16];
					stream.ReadExactly(array);
					var (num, _, text) = ReadHeader(array, entry.Length);
					if (text != null)
					{
						p.Add(text);
					}
					else if (flag)
					{
						ZipArchiveEntry entry2 = zipArchive.GetEntry(f.Skin.Replace('\\', '/'));
						if (entry2 == null)
						{
							p.Add("The model's rig file is missing.");
						}
						else if (entry2.Length > 67108864)
						{
							p.Add("The model's rig file is too large.");
						}
						else
						{
							using Stream stream2 = entry2.Open();
							byte[] array2 = new byte[20];
							if (stream2.ReadAtLeast(array2, 20, throwOnEndOfStream: false) < 20 || BitConverter.ToUInt32(array2, 0) != 1397638724 || BitConverter.ToInt32(array2, 4) != 1)
							{
								p.Add("The model's rig file isn't in a format this version understands.");
							}
							else if (BitConverter.ToInt32(array2, 12) != num)
							{
								p.Add("The model's rig file doesn't match its shape (different number of points).");
							}
						}
					}
				}
				foreach (ModFnMaterial item in f.Materials.Where((ModFnMaterial x) => x.Picture.Length > 0))
				{
					ZipArchiveEntry entry3 = zipArchive.GetEntry(item.Picture.Replace('\\', '/'));
					if (entry3 == null)
					{
						p.Add("The picture '" + item.Picture + "' is missing.");
					}
					else if (entry3.Length > 16777216)
					{
						p.Add("The picture '" + item.Picture + "' is too large.");
					}
				}
			}
			catch (Exception ex)
			{
				p.Add("Can't read the model: " + ex.Message);
			}
		}
	}

	private static (int Verts, int Tris, string? Problem) ReadHeader(byte[] h, long length)
	{
		uint num = BitConverter.ToUInt32(h, 0);
		int num2 = BitConverter.ToInt32(h, 4);
		int num3 = BitConverter.ToInt32(h, 8);
		int num4 = BitConverter.ToInt32(h, 12);
		if (num != 1296975428 || num2 != 1)
		{
			return (Verts: 0, Tris: 0, Problem: "The model's shape file isn't in a format this version understands.");
		}
		if (num3 < 3 || num3 > 400000 || num4 < 1 || num4 > 800000)
		{
			return (Verts: 0, Tris: 0, Problem: $"The model has an unreasonable size ({num3} points, {num4} triangles).");
		}
		if (16 + 12L * (long)num3 + 8L * (long)num3 + 12L * (long)num4 + num4 != length)
		{
			return (Verts: 0, Tris: 0, Problem: "The model's shape file is damaged (its size doesn't match what it says it holds).");
		}
		return (Verts: num3, Tris: num4, Problem: null);
	}

	public static FnSkin? LoadSkin(ModPack mod)
	{
		ModFnModel fnModel = mod.Manifest.FnModel;
		if (fnModel == null || !string.Equals(fnModel.Mode, "native", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return FnSkin.FromBytes(mod.ReadEntry(fnModel.Skin, 67108864L));
	}

	public static FnTarget Load(ModPack mod)
	{
		ModFnModel f = mod.Manifest.FnModel ?? throw new InvalidDataException("This drip has no model in it.");
		List<string> list = Validate(mod);
		if (list.Count > 0)
		{
			throw new InvalidDataException(string.Join(" ", list));
		}
		return LoadShape(mod, f);
	}

	internal static FnTarget LoadShape(ModPack mod, ModFnModel f)
	{
		byte[] array = mod.ReadEntry(f.Mesh, 67108864L);
		var (num, num2, text) = ReadHeader(array, array.Length);
		if (text != null)
		{
			throw new InvalidDataException(text);
		}
		int num3 = 16;
		double[] array2 = new double[num * 3];
		int num4 = 0;
		while (num4 < array2.Length)
		{
			double num5 = BitConverter.ToSingle(array, num3);
			if (double.IsNaN(num5) || double.IsInfinity(num5) || Math.Abs(num5) > 2000.0)
			{
				throw new InvalidDataException("The model has points outside any sensible size.");
			}
			array2[num4] = num5;
			num4++;
			num3 += 4;
		}
		double[] array3 = new double[num * 2];
		int num6 = 0;
		while (num6 < array3.Length)
		{
			double num7 = BitConverter.ToSingle(array, num3);
			array3[num6] = ((double.IsNaN(num7) || double.IsInfinity(num7)) ? 0.0 : num7);
			num6++;
			num3 += 4;
		}
		int[] array4 = new int[num2 * 3];
		int num8 = 0;
		while (num8 < array4.Length)
		{
			array4[num8] = BitConverter.ToInt32(array, num3);
			num8++;
			num3 += 4;
		}
		int[] array5 = new int[num2];
		int num9 = 0;
		while (num9 < num2)
		{
			array5[num9] = array[num3];
			num9++;
			num3++;
		}
		List<FgSourceMaterial> list = new List<FgSourceMaterial>();
		foreach (ModFnMaterial material in f.Materials)
		{
			FgSourceMaterial fgSourceMaterial = new FgSourceMaterial
			{
				Name = material.Name,
				Color = new Vector4(material.Color[0], material.Color[1], material.Color[2], 1f)
			};
			if (material.Picture.Length > 0)
			{
				TexImage texImage = TexImage.Load(mod.ReadEntry(material.Picture, 16777216L));
				if ((long)texImage.Width * (long)texImage.Height > 16777216)
				{
					throw new InvalidDataException("A picture in the model is too big.");
				}
				fgSourceMaterial.Picture = texImage;
			}
			list.Add(fgSourceMaterial);
		}
		return FnTarget.FromPlaced(array2, array4, array3, array5, list, f.HasUv);
	}
}
