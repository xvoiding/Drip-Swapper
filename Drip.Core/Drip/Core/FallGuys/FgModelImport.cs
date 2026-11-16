using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;
using Assimp.Configs;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FallGuys;

public static class FgModelImport
{
	private static string[]? _ext;

	public static IReadOnlyList<string> Extensions
	{
		get
		{
			if (_ext != null)
			{
				return _ext;
			}
			try
			{
				using AssimpContext assimpContext = new AssimpContext();
				_ext = (from e in (from e in assimpContext.GetSupportedImportFormats()
						select e.ToLowerInvariant()).Distinct()
					orderby e
					select e).ToArray();
			}
			catch
			{
				_ext = new string[9] { ".fbx", ".obj", ".glb", ".gltf", ".dae", ".3ds", ".stl", ".ply", ".blend" };
			}
			return _ext;
		}
	}

	public static string DialogFilter()
	{
		string[] source = new string[9] { ".fbx", ".obj", ".glb", ".gltf", ".dae", ".3ds", ".stl", ".ply", ".blend" };
		IReadOnlyList<string> all = Extensions;
		string value = string.Join(";", all.Select((string e) => "*" + e));
		string value2 = string.Join(";", from e in source
			where all.Contains(e)
			select "*" + e);
		return $"3D models ({value2})|{value2}|All supported 3D formats|{value}|All files|*.*";
	}

	public static FgModelSource Load(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("That model file isn't there.", path);
		}
		if (string.Equals(Path.GetExtension(path), ".blend", StringComparison.OrdinalIgnoreCase))
		{
			string glb = BlenderBridge.ToGlb(path, out string tmp);
			try
			{
				FgModelSource viaBlender = LoadFile(glb);
				viaBlender.Name = Path.GetFileNameWithoutExtension(path);
				viaBlender.Path = path;
				return viaBlender;
			}
			finally
			{
				try
				{
					Directory.Delete(tmp, recursive: true);
				}
				catch
				{
				}
			}
		}
		return LoadFile(path);
	}

	private static FgModelSource LoadFile(string path)
	{
		Scene scene;
		try
		{
			using AssimpContext assimpContext = new AssimpContext();
			assimpContext.SetConfig(new NormalSmoothingAngleConfig(80f));
			scene = assimpContext.ImportFile(path, PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.PreTransformVertices | PostProcessSteps.ImproveCacheLocality | PostProcessSteps.RemoveRedundantMaterials | PostProcessSteps.SortByPrimitiveType | PostProcessSteps.FindDegenerates | PostProcessSteps.FindInvalidData);
		}
		catch (Exception ex)
		{
			throw new InvalidDataException("Couldn't read that model: " + ex.Message, ex);
		}
		if (scene == null || !scene.HasMeshes)
		{
			throw new InvalidDataException("That file has no 3D mesh in it.");
		}
		string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
		FgModelSource fgModelSource = new FgModelSource
		{
			Name = Path.GetFileNameWithoutExtension(path),
			Path = path
		};
		for (int i = 0; i < scene.MaterialCount; i++)
		{
			fgModelSource.Materials.Add(ReadMaterial(scene, scene.Materials[i], dir));
		}
		if (fgModelSource.Materials.Count == 0)
		{
			fgModelSource.Materials.Add(new FgSourceMaterial
			{
				Name = "default",
				Color = new Vector4(0.8f, 0.8f, 0.8f, 1f)
			});
		}
		List<Vector3> list = new List<Vector3>();
		List<Vector3> list2 = new List<Vector3>();
		List<Vector2> list3 = new List<Vector2>();
		List<int> list4 = new List<int>();
		List<int> list5 = new List<int>();
		foreach (Mesh mesh in scene.Meshes)
		{
			if (!mesh.HasFaces || !mesh.HasVertices)
			{
				continue;
			}
			int count = list.Count;
			int item = Math.Clamp(mesh.MaterialIndex, 0, fgModelSource.Materials.Count - 1);
			bool flag = mesh.HasTextureCoords(0);
			if (flag)
			{
				fgModelSource.HasUv = true;
			}
			for (int j = 0; j < mesh.VertexCount; j++)
			{
				Vector3D vector3D = mesh.Vertices[j];
				list.Add(new Vector3(vector3D.X, vector3D.Y, vector3D.Z));
				if (mesh.HasNormals)
				{
					Vector3D vector3D2 = mesh.Normals[j];
					list2.Add(new Vector3(vector3D2.X, vector3D2.Y, vector3D2.Z));
				}
				else
				{
					list2.Add(Vector3.Zero);
				}
				if (flag)
				{
					Vector3D vector3D3 = mesh.TextureCoordinateChannels[0][j];
					list3.Add(new Vector2(vector3D3.X, vector3D3.Y));
				}
				else
				{
					list3.Add(Vector2.Zero);
				}
				list4.Add(item);
			}
			foreach (Face face in mesh.Faces)
			{
				if (face.IndexCount == 3)
				{
					list5.Add(count + face.Indices[0]);
					list5.Add(count + face.Indices[1]);
					list5.Add(count + face.Indices[2]);
				}
			}
		}
		if (list.Count == 0 || list5.Count == 0)
		{
			throw new InvalidDataException("That file has no usable triangles in it.");
		}
		fgModelSource.Pos = list.ToArray();
		fgModelSource.Nrm = list2.ToArray();
		fgModelSource.Uv = list3.ToArray();
		fgModelSource.Idx = list5.ToArray();
		fgModelSource.Mat = list4.ToArray();
		try
		{
			fgModelSource.Rig = FgHumanoid.TryRead(path);
		}
		catch
		{
			fgModelSource.Rig = null;
		}
		return fgModelSource;
	}

	internal static FgSourceMaterial ReadMaterial(Scene scene, Material m, string dir)
	{
		FgSourceMaterial fgSourceMaterial = new FgSourceMaterial
		{
			Name = (m.Name ?? "")
		};
		if (m.HasColorDiffuse)
		{
			Color4D colorDiffuse = m.ColorDiffuse;
			fgSourceMaterial.Color = new Vector4(colorDiffuse.R, colorDiffuse.G, colorDiffuse.B, (colorDiffuse.A <= 0f) ? 1f : colorDiffuse.A);
		}
		TextureSlot textureSlot = default;
		bool flag = false;
		TextureSlot texture;
		if (m.HasTextureDiffuse)
		{
			textureSlot = m.TextureDiffuse;
			flag = true;
		}
		else if (m.GetMaterialTexture(TextureType.BaseColor, 0, out texture))
		{
			textureSlot = texture;
			flag = true;
		}
		if (flag && !string.IsNullOrEmpty(textureSlot.FilePath))
		{
			try
			{
				fgSourceMaterial.Picture = LoadPicture(scene, textureSlot.FilePath, dir);
			}
			catch
			{
			}
		}
		return fgSourceMaterial;
	}

	private static TexImage? LoadPicture(Scene scene, string file, string dir)
	{
		if (file.StartsWith('*') && int.TryParse(file.AsSpan(1), out var result) && result >= 0 && result < scene.TextureCount)
		{
			return FromEmbedded(scene.Textures[result]);
		}
		EmbeddedTexture embeddedTexture = scene.Textures.FirstOrDefault((EmbeddedTexture t) => string.Equals(t.Filename, file, StringComparison.OrdinalIgnoreCase));
		if (embeddedTexture != null)
		{
			return FromEmbedded(embeddedTexture);
		}
		string fileName = Path.GetFileName(file.Replace('\\', '/'));
		string[] array = new string[2]
		{
			Path.Combine(dir, file.Replace('\\', Path.DirectorySeparatorChar)),
			Path.Combine(dir, fileName)
		};
		foreach (string path in array)
		{
			if (File.Exists(path))
			{
				return TexImage.Load(File.ReadAllBytes(path));
			}
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		List<string> list = new List<string> { dir };
		string directoryName = Path.GetDirectoryName(dir.TrimEnd(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		}));
		if (!string.IsNullOrEmpty(directoryName) && directoryName.Length > 3)
		{
			list.Add(directoryName);
		}
		foreach (string item in list)
		{
			if (!Directory.Exists(item))
			{
				continue;
			}
			int num2 = 0;
			try
			{
				foreach (string item2 in Directory.EnumerateFiles(item, "*", new EnumerationOptions
				{
					RecurseSubdirectories = true,
					MaxRecursionDepth = 3,
					IgnoreInaccessible = true
				}))
				{
					if (++num2 <= 4000)
					{
						bool flag;
						switch (Path.GetExtension(item2).ToLowerInvariant())
						{
						case ".png":
						case ".jpg":
						case ".jpeg":
						case ".bmp":
						case ".tga":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag && Path.GetFileNameWithoutExtension(item2).Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
						{
							return TexImage.Load(File.ReadAllBytes(item2));
						}
						continue;
					}
					break;
				}
			}
			catch (IOException)
			{
			}
		}
		return null;
	}

	private static TexImage? FromEmbedded(EmbeddedTexture t)
	{
		if (t.IsCompressed)
		{
			return TexImage.Load(t.CompressedData);
		}
		byte[] array = new byte[t.Width * t.Height * 4];
		for (int i = 0; i < t.Width * t.Height; i++)
		{
			Texel texel = t.NonCompressedData[i];
			array[i * 4] = texel.R;
			array[i * 4 + 1] = texel.G;
			array[i * 4 + 2] = texel.B;
			array[i * 4 + 3] = texel.A;
		}
		return new TexImage
		{
			Width = t.Width,
			Height = t.Height,
			Rgba = array
		};
	}
}
