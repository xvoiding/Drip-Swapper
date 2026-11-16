using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Assimp;
using Drip.Core.FallGuys;

namespace Drip.Core.RocketLeague.Custom;

public static class ModelFileReader
{
	private static readonly string[] ZUp = new string[12]
	{
		".stl", ".ply", ".3ds", ".dxf", ".off", ".ac", ".lwo", ".lws", ".mdl",
		".md2", ".md3", ".ase"
	};

	public static IReadOnlyList<string> Extensions => FgModelImport.Extensions;

	public static string DialogFilter()
	{
		return FgModelImport.DialogFilter();
	}

	public static bool IsModelFile(string path)
	{
		return Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
	}

	public static FbxScene Load(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("That model file isn't there.", path);
		}
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text == ".fbx")
		{
			try
			{
				return FbxScene.Load(path);
			}
			catch (FbxFormatException)
			{
			}
		}
		if (text == ".blend")
		{
			string glb = BlenderBridge.ToGlb(path, out string tmp);
			try
			{
				return FromAssimp(glb, ".glb");
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
		return FromAssimp(path, text);
	}

	private static FbxScene FromAssimp(string path, string ext)
	{
		Scene scene;
		List<FbxMaterial> mats;
		FbxScene res;
		void Walk(Node n, Assimp.Matrix4x4 parent, List<string> chain)
		{
			Assimp.Matrix4x4 matrix4x = parent * n.Transform;
			List<string> list = new List<string>(chain) { n.Name };
			foreach (int meshIndex in n.MeshIndices)
			{
				Mesh mesh = scene.Meshes[meshIndex];
				if (mesh.HasVertices && mesh.HasFaces)
				{
					FbxMesh fbxMesh = new FbxMesh
					{
						Name = (string.IsNullOrWhiteSpace(mesh.Name) ? n.Name : mesh.Name)
					};
					fbxMesh.Path = new List<string>(list.AsEnumerable().Reverse());
					if (!string.IsNullOrWhiteSpace(mesh.Name) && mesh.Name != n.Name)
					{
						fbxMesh.Path.Insert(0, mesh.Name);
					}
					FbxMaterial fbxMaterial = ((mats.Count > 0) ? mats[Math.Clamp(mesh.MaterialIndex, 0, mats.Count - 1)] : new FbxMaterial
					{
						Name = "material"
					});
					fbxMesh.MaterialNames.Add(fbxMaterial.Name);
					fbxMesh.Materials.Add(fbxMaterial);
					bool flag = mesh.HasTextureCoords(0);
					bool hasNormals = mesh.HasNormals;
					foreach (Face face in mesh.Faces)
					{
						if (face.IndexCount == 3)
						{
							for (int j = 0; j < 3; j++)
							{
								int index = face.Indices[j];
								Vector3D vector3D = matrix4x * mesh.Vertices[index];
								fbxMesh.Pos.Add(new Vector3(vector3D.X, vector3D.Y, vector3D.Z));
								if (hasNormals)
								{
									Vector3D vector3D2 = mesh.Normals[index];
									Vector3 value = new Vector3(matrix4x.A1 * vector3D2.X + matrix4x.A2 * vector3D2.Y + matrix4x.A3 * vector3D2.Z, matrix4x.B1 * vector3D2.X + matrix4x.B2 * vector3D2.Y + matrix4x.B3 * vector3D2.Z, matrix4x.C1 * vector3D2.X + matrix4x.C2 * vector3D2.Y + matrix4x.C3 * vector3D2.Z);
									fbxMesh.Nrm.Add((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : Vector3.UnitY);
								}
								else
								{
									fbxMesh.Nrm.Add(Vector3.UnitY);
								}
								if (flag)
								{
									Vector3D vector3D3 = mesh.TextureCoordinateChannels[0][index];
									fbxMesh.Uv.Add(new Vector2(vector3D3.X, vector3D3.Y));
								}
								else
								{
									fbxMesh.Uv.Add(Vector2.Zero);
								}
							}
							fbxMesh.Material.Add(0);
						}
					}
					if (fbxMesh.TriangleCount > 0)
					{
						res.Meshes.Add(fbxMesh);
					}
				}
			}
			foreach (Node child in n.Children)
			{
				Walk(child, matrix4x, list);
			}
		}
		try
		{
			using AssimpContext assimpContext = new AssimpContext();
			scene = assimpContext.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.FindInvalidData);
		}
		catch (Exception ex)
		{
			throw new FbxFormatException("Couldn't read that model: " + ex.Message);
		}
		if (scene == null || !scene.HasMeshes)
		{
			throw new FbxFormatException("That file has no 3D shapes in it.");
		}
		res = new FbxScene();
		if (Array.IndexOf(ZUp, ext) >= 0)
		{
			res.UpAxis = 2;
			res.UpSign = 1;
			res.FrontAxis = 1;
			res.FrontSign = -1;
		}
		mats = new List<FbxMaterial>();
		for (int i = 0; i < scene.MaterialCount; i++)
		{
			Material material = scene.Materials[i];
			Color4D color4D = (material.HasColorDiffuse ? material.ColorDiffuse : new Color4D(0.8f, 0.8f, 0.8f, 1f));
			string text = (material.HasTextureDiffuse ? (material.TextureDiffuse.FilePath ?? "") : "");
			if (text.StartsWith('*'))
			{
				text = "";
			}
			text = text.Replace('\\', '/');
			int num = text.LastIndexOf('/');
			if (num >= 0)
			{
				text = text.Substring(num + 1);
			}
			mats.Add(new FbxMaterial
			{
				Name = (string.IsNullOrWhiteSpace(material.Name) ? ("material" + i) : material.Name),
				Color = new Vector3(color4D.R, color4D.G, color4D.B),
				HasTexture = material.HasTextureDiffuse,
				TextureFile = text
			});
		}
		Walk(scene.RootNode, Assimp.Matrix4x4.Identity, new List<string>());
		if (res.Meshes.Count == 0)
		{
			throw new FbxFormatException("That file has no 3D shapes in it.");
		}
		return res;
	}
}
