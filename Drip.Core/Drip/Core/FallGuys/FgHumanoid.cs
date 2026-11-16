using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Assimp;
using Assimp.Configs;

namespace Drip.Core.FallGuys;

public static class FgHumanoid
{
	private enum BlockPart
	{
		None,
		Head,
		Torso,
		Arm,
		Leg
	}

	private sealed class Landmarks
	{
		public Vector3[] Hip = new Vector3[2];

		public Vector3[] Knee = new Vector3[2];

		public Vector3[] Ankle = new Vector3[2];

		public Vector3[] Ball = new Vector3[2];

		public Vector3[] Toe = new Vector3[2];

		public Vector3[] Shoulder = new Vector3[2];

		public Vector3[] Elbow = new Vector3[2];

		public Vector3[] Wrist = new Vector3[2];

		public Vector3[] HandTip = new Vector3[2];

		public float HipY;

		public float ShoulderY;

		public float NeckY;

		public float TopY;
	}

	private sealed record BeanSide(Vector3 Shoulder, Vector3 Elbow, Vector3 Wrist, Vector3 HandTip, Vector3 Hip, Vector3 Knee, Vector3 Ankle, Vector3 Ball, Vector3 Toe);

	public const int RoleCount = 17;

	public static Action<string>? Trace;

	public const int BeanBoneCount = 36;

	private const int bRoot = 0;

	private const int bChest = 1;

	private const int bHead = 2;

	private const int bTorso = 24;

	private const int bPelvis = 25;

	private static readonly int[] bShoulder = new int[2] { 6, 15 };

	private static readonly int[] bElbow = new int[2] { 7, 16 };

	private static readonly int[] bWrist = new int[2] { 8, 17 };

	private static readonly int[] bHip = new int[2] { 31, 26 };

	private static readonly int[] bKnee = new int[2] { 32, 27 };

	private static readonly int[] bAnkle = new int[2] { 33, 28 };

	private static readonly int[] bBall = new int[2] { 34, 29 };

	private static readonly float[] Y = new float[3] { 0.323f, 0.684f, 0.972f };

	private static readonly string[] Face = new string[14]
	{
		"eye", "brow", "lip", "jaw", "teeth", "tongue", "cheek", "nose", "lid", "mouth",
		"ear", "hair", "head", "neck"
	};

	private static Vector3 L(float x, float y, float z, int side)
	{
		return new Vector3((side == 0) ? (0f - x) : x, y, z);
	}

	public static FgBodyPart? RoleOf(string bone)
	{
		string n = bone.ToLowerInvariant();
		int num = n.LastIndexOf(':');
		if (num >= 0)
		{
			n = n.Substring(num + 1);
		}
		string[] tok = n.Split(new char[5] { '_', '.', ' ', '-', '|' }, StringSplitOptions.RemoveEmptyEntries);
		if (Sub(new string[9] { "ik_", "_ik", "pole", "weapon", "twist_helper", "attach", "socket", "ctrl", "cnt" }))
		{
			return null;
		}
		int side = -1;
		if (Has("l") || Has("left") || n.StartsWith("left") || n.EndsWith("left") || n.StartsWith("l_"))
		{
			side = 0;
		}
		else if (Has("r") || Has("right") || n.StartsWith("right") || n.EndsWith("right") || n.StartsWith("r_"))
		{
			side = 1;
		}
		else if (n.Length > 1)
		{
			string text = n;
			if (text[text.Length - 2] != '_')
			{
				string text2 = n;
				if (text2[text2.Length - 2] != '.')
				{
					goto IL_020b;
				}
			}
			string text3 = n;
			if (text3[text3.Length - 1] != 'l')
			{
				string text4 = n;
				if (text4[text4.Length - 1] != 'r')
				{
					goto IL_020b;
				}
			}
			string text5 = n;
			side = ((text5[text5.Length - 1] != 'l') ? 1 : 0);
		}
		goto IL_020b;
		IL_020b:
		if (Sub(new string[11]
		{
			"eye", "brow", "lip", "jaw", "teeth", "tongue", "cheek", "nose", "lid", "mouth",
			"hair"
		}))
		{
			return FgBodyPart.Head;
		}
		if (Sub(new string[4] { "thigh", "upleg", "upperleg", "up_leg" }))
		{
			return Pair(FgBodyPart.ThighL, FgBodyPart.ThighR);
		}
		if (Sub(new string[6] { "calf", "shin", "lowerleg", "lowleg", "low_leg", "knee" }) || n.Replace("left", "").Replace("right", "") == "leg" || (tok.Length != 0 && tok[0] == "leg"))
		{
			return Pair(FgBodyPart.CalfL, FgBodyPart.CalfR);
		}
		if (Sub(new string[2] { "foot", "ankle" }))
		{
			return Pair(FgBodyPart.FootL, FgBodyPart.FootR);
		}
		if (Has("ball") || Sub(new string[1] { "toe" }))
		{
			return Pair(FgBodyPart.BallL, FgBodyPart.BallR);
		}
		if (Sub(new string[4] { "forearm", "lowerarm", "lowarm", "low_arm" }))
		{
			return Pair(FgBodyPart.LoArmL, FgBodyPart.LoArmR);
		}
		if (Sub(new string[3] { "upperarm", "uparm", "up_arm" }) || ((Has("arm") || n.Replace("left", "").Replace("right", "") == "arm") && !Sub(new string[1] { "forearm" })))
		{
			return Pair(FgBodyPart.UpArmL, FgBodyPart.UpArmR);
		}
		if (Sub(new string[10] { "hand", "wrist", "thumb", "index", "middle", "ring", "pinky", "finger", "palm", "metacarpal" }))
		{
			return Pair(FgBodyPart.HandL, FgBodyPart.HandR);
		}
		if (Sub(new string[7] { "clavicle", "shoulder", "spine", "chest", "abdomen", "torso", "waist" }))
		{
			return FgBodyPart.Spine;
		}
		if (Sub(new string[2] { "neck", "head" }))
		{
			return FgBodyPart.Head;
		}
		if (Sub(new string[4] { "pelvis", "hips", "hip", "root" }))
		{
			return FgBodyPart.Pelvis;
		}
		return null;
		bool Has(string t)
		{
			return tok.Contains(t);
		}
		FgBodyPart? Pair(FgBodyPart l, FgBodyPart r)
		{
			if (side >= 0)
			{
				return (side == 0) ? l : r;
			}
			return null;
		}
		bool Sub(params string[] s)
		{
			return s.Any(n.Contains);
		}
	}

	public static FgRigSource? TryRead(string path)
	{
		Scene scene = Import(path);
		Trace?.Invoke("import: " + ((scene == null) ? "null" : (scene.MeshCount + " meshes, bones " + string.Join(",", scene.Meshes.Select((Mesh m) => m.BoneCount)))));
		if (scene == null || !scene.HasMeshes)
		{
			return null;
		}
		return Read(scene, path, blocky: false) ?? Read(scene, path, blocky: true);
	}

	private static Scene? Import(string path)
	{
		using AssimpContext assimpContext = new AssimpContext();
		assimpContext.SetConfig(new NormalSmoothingAngleConfig(80f));
		return assimpContext.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.LimitBoneWeights | PostProcessSteps.FindInvalidData);
	}

	private static FgRigSource? Read(Scene scene, string path, bool blocky)
	{
		bool num;
		if (!blocky)
		{
			num = !scene.Meshes.Any((Mesh m) => m.HasBones && m.BoneCount >= 15);
		}
		else
		{
			if (scene.MeshCount < 4)
			{
				goto IL_00bf;
			}
			num = (from b in scene.Meshes.SelectMany((Mesh m) => m.Bones)
				select b.Name).Distinct().Count() < 8;
		}
		FgRigSource res;
		List<Vector3> pos;
		List<Vector3> nrm;
		List<Vector2> uv;
		List<int> idx;
		List<int> mat;
		List<float> role;
		Dictionary<string, FgBodyPart?> roleCache;
		Vector3 blockyRight;
		Vector3 legL;
		Vector3 legR;
		int cl;
		int cr;
		if (!num)
		{
			string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
			res = new FgRigSource
			{
				Name = Path.GetFileNameWithoutExtension(path)
			};
			for (int num2 = 0; num2 < scene.MaterialCount; num2++)
			{
				res.Materials.Add(FgModelImport.ReadMaterial(scene, scene.Materials[num2], dir));
			}
			if (res.Materials.Count == 0)
			{
				res.Materials.Add(new FgSourceMaterial
				{
					Name = "default",
					Color = new Vector4(0.8f, 0.8f, 0.8f, 1f)
				});
			}
			pos = new List<Vector3>();
			nrm = new List<Vector3>();
			uv = new List<Vector2>();
			idx = new List<int>();
			mat = new List<int>();
			role = new List<float>();
			roleCache = new Dictionary<string, FgBodyPart?>();
			blockyRight = Vector3.Zero;
			if (blocky)
			{
				legL = Vector3.Zero;
				legR = Vector3.Zero;
				cl = 0;
				cr = 0;
				Pre(scene.RootNode, Assimp.Matrix4x4.Identity);
				Trace?.Invoke($"blocky legs L {cl} R {cr}");
				if (cl == 0 || cr == 0)
				{
					return null;
				}
				blockyRight = legR / cr - legL / cl;
				blockyRight.Y = 0f;
				if (blockyRight.LengthSquared() < 1E-08f)
				{
					return null;
				}
				blockyRight = Vector3.Normalize(blockyRight);
			}
			Walk(scene.RootNode, Assimp.Matrix4x4.Identity);
			Trace?.Invoke($"{(blocky ? "blocky" : "bones")} read: {pos.Count} verts");
			if (pos.Count == 0 || idx.Count == 0)
			{
				return null;
			}
			res.Pos = pos.ToArray();
			res.Nrm = nrm.ToArray();
			res.Uv = uv.ToArray();
			res.Idx = idx.ToArray();
			res.Mat = mat.ToArray();
			res.Role = role.ToArray();
			float[] array = new float[17];
			for (int num3 = 0; num3 < res.Pos.Length; num3++)
			{
				for (int num4 = 0; num4 < 17; num4++)
				{
					array[num4] += res.Role[num3 * 17 + num4];
				}
			}
			float num5 = array.Sum();
			FgBodyPart[] array2 = new FgBodyPart[7]
			{
				FgBodyPart.Head,
				FgBodyPart.ThighL,
				FgBodyPart.ThighR,
				FgBodyPart.CalfL,
				FgBodyPart.CalfR,
				FgBodyPart.UpArmL,
				FgBodyPart.UpArmR
			};
			foreach (FgBodyPart fgBodyPart in array2)
			{
				if (array[(int)fgBodyPart] < Math.Max(8f, num5 * 0.002f))
				{
					Trace?.Invoke($"{(blocky ? "blocky" : "bones")}: not a humanoid, {fgBodyPart} has {array[(int)fgBodyPart]:0.#} of {num5:0.#}");
					return null;
				}
			}
			return res;
		}
		goto IL_00bf;
		IL_00bf:
		return null;
		void Pre(Node n, Assimp.Matrix4x4 parent)
		{
			Assimp.Matrix4x4 matrix4x = parent * n.Transform;
			foreach (int meshIndex in n.MeshIndices)
			{
				var (blockPart, num7) = PartOf(n.Name + "_" + scene.Meshes[meshIndex].Name);
				if (blockPart == BlockPart.Leg && num7 >= 0)
				{
					foreach (Vector3D vertex in scene.Meshes[meshIndex].Vertices)
					{
						Vector3D vector3D = matrix4x * vertex;
						if (num7 == 0)
						{
							legL += new Vector3(vector3D.X, vector3D.Y, vector3D.Z);
							cl++;
						}
						else
						{
							legR += new Vector3(vector3D.X, vector3D.Y, vector3D.Z);
							cr++;
						}
					}
				}
			}
			foreach (Node child in n.Children)
			{
				Pre(child, matrix4x);
			}
		}
		void Walk(Node n, Assimp.Matrix4x4 parent)
		{
			Assimp.Matrix4x4 matrix4x = parent * n.Transform;
			foreach (int meshIndex2 in n.MeshIndices)
			{
				Mesh mesh = scene.Meshes[meshIndex2];
				if (mesh.HasFaces && mesh.HasVertices && (mesh.HasBones || blocky))
				{
					bool flag = mesh.HasTextureCoords(0);
					int count = pos.Count;
					int vertexCount = mesh.VertexCount;
					float[] array3 = new float[vertexCount * 17];
					if (blocky)
					{
						BlockyRoles(mesh, matrix4x, n.Name + "_" + mesh.Name, blockyRight, array3);
					}
					else
					{
						foreach (Bone bone in mesh.Bones)
						{
							if (!roleCache.TryGetValue(bone.Name, out var value))
							{
								value = (roleCache[bone.Name] = RoleOf(bone.Name));
							}
							if (value.HasValue)
							{
								foreach (VertexWeight vertexWeight in bone.VertexWeights)
								{
									array3[(int)(vertexWeight.VertexID * 17 + value.Value)] += vertexWeight.Weight;
								}
							}
						}
					}
					bool flag2 = false;
					for (int i = 0; i < vertexCount; i++)
					{
						if (flag2)
						{
							break;
						}
						for (int j = 0; j < 17; j++)
						{
							if (array3[i * 17 + j] > 0f)
							{
								flag2 = true;
								break;
							}
						}
					}
					if (flag2)
					{
						if (flag)
						{
							res.HasUv = true;
						}
						int item = Math.Clamp(mesh.MaterialIndex, 0, res.Materials.Count - 1);
						for (int k = 0; k < vertexCount; k++)
						{
							Vector3D vector3D = matrix4x * mesh.Vertices[k];
							pos.Add(new Vector3(vector3D.X, vector3D.Y, vector3D.Z));
							if (mesh.HasNormals)
							{
								Vector3D vector3D2 = mesh.Normals[k];
								Vector3 vector = new Vector3(vector3D2.X, vector3D2.Y, vector3D2.Z);
								Vector3 value2 = new Vector3(matrix4x.A1 * vector.X + matrix4x.A2 * vector.Y + matrix4x.A3 * vector.Z, matrix4x.B1 * vector.X + matrix4x.B2 * vector.Y + matrix4x.B3 * vector.Z, matrix4x.C1 * vector.X + matrix4x.C2 * vector.Y + matrix4x.C3 * vector.Z);
								nrm.Add((value2.LengthSquared() > 1E-12f) ? Vector3.Normalize(value2) : Vector3.Zero);
							}
							else
							{
								nrm.Add(Vector3.Zero);
							}
							if (flag)
							{
								Vector3D vector3D3 = mesh.TextureCoordinateChannels[0][k];
								uv.Add(new Vector2(vector3D3.X, vector3D3.Y));
							}
							else
							{
								uv.Add(Vector2.Zero);
							}
							mat.Add(item);
							float num7 = 0f;
							for (int l = 0; l < 17; l++)
							{
								num7 += array3[k * 17 + l];
							}
							for (int m = 0; m < 17; m++)
							{
								role.Add((num7 > 1E-06f) ? (array3[k * 17 + m] / num7) : 0f);
							}
						}
						foreach (Face face in mesh.Faces)
						{
							if (face.IndexCount == 3)
							{
								idx.Add(count + face.Indices[0]);
								idx.Add(count + face.Indices[1]);
								idx.Add(count + face.Indices[2]);
							}
						}
					}
				}
			}
			foreach (Node child2 in n.Children)
			{
				Walk(child2, matrix4x);
			}
		}
	}

	private static (BlockPart Part, int Side) PartOf(string name)
	{
		string text = name.ToLowerInvariant();
		string[] array = text.Split(new char[6] { '_', '.', ' ', '-', '|', ':' }, StringSplitOptions.RemoveEmptyEntries);
		int item = ((!array.Contains("left") && !array.Contains("l")) ? ((array.Contains("right") || array.Contains("r")) ? 1 : (-1)) : 0);
		if (text.Contains("head") || text.Contains("skull"))
		{
			return (Part: BlockPart.Head, Side: item);
		}
		if (text.Contains("arm"))
		{
			return (Part: BlockPart.Arm, Side: item);
		}
		if (text.Contains("leg"))
		{
			return (Part: BlockPart.Leg, Side: item);
		}
		if (text.Contains("torso") || text.Contains("body") || text.Contains("chest"))
		{
			return (Part: BlockPart.Torso, Side: item);
		}
		return (Part: BlockPart.None, Side: item);
	}

	private static void BlockyRoles(Mesh m, Assimp.Matrix4x4 w, string name, Vector3 right, float[] rw)
	{
		var (blockPart, num) = PartOf(name);
		switch (blockPart)
		{
		case BlockPart.Arm:
		case BlockPart.Leg:
			if (num < 0)
			{
				return;
			}
			break;
		case BlockPart.None:
			return;
		}
		int vertexCount = m.VertexCount;
		Vector3[] array = new Vector3[vertexCount];
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		Vector3 zero = Vector3.Zero;
		for (int i = 0; i < vertexCount; i++)
		{
			Vector3D vector3D = w * m.Vertices[i];
			array[i] = new Vector3(vector3D.X, vector3D.Y, vector3D.Z);
			num2 = Math.Min(num2, array[i].Y);
			num3 = Math.Max(num3, array[i].Y);
			zero += array[i];
		}
		zero /= (float)vertexCount;
		Vector3 vector = Vector3.Cross(Vector3.UnitY, right);
		for (int j = 0; j < vertexCount; j++)
		{
			float num4 = (num3 - array[j].Y) / Math.Max(num3 - num2, 1E-06f);
			switch (blockPart)
			{
			case BlockPart.Head:
				rw[j * 17 + 2] = 1f;
				break;
			case BlockPart.Torso:
				Put(j, FgBodyPart.Spine, FgBodyPart.Pelvis, num4, 0.8f);
				break;
			case BlockPart.Arm:
			{
				FgBodyPart a2 = ((num == 0) ? FgBodyPart.UpArmL : FgBodyPart.UpArmR);
				FgBodyPart fgBodyPart4 = ((num == 0) ? FgBodyPart.LoArmL : FgBodyPart.LoArmR);
				FgBodyPart b = ((num == 0) ? FgBodyPart.HandL : FgBodyPart.HandR);
				if (num4 < 0.56f)
				{
					Put(j, a2, fgBodyPart4, num4, 0.5f);
				}
				else
				{
					Put(j, fgBodyPart4, b, num4, 0.86f);
				}
				break;
			}
			case BlockPart.Leg:
			{
				FgBodyPart a = ((num == 0) ? FgBodyPart.ThighL : FgBodyPart.ThighR);
				FgBodyPart fgBodyPart = ((num == 0) ? FgBodyPart.CalfL : FgBodyPart.CalfR);
				FgBodyPart fgBodyPart2 = ((num == 0) ? FgBodyPart.FootL : FgBodyPart.FootR);
				FgBodyPart fgBodyPart3 = ((num == 0) ? FgBodyPart.BallL : FgBodyPart.BallR);
				if (num4 < 0.56f)
				{
					Put(j, a, fgBodyPart, num4, 0.5f);
				}
				else if (num4 < 0.94f)
				{
					Put(j, fgBodyPart, fgBodyPart2, num4, 0.88f);
				}
				else
				{
					rw[(int)(j * 17 + ((Vector3.Dot(array[j] - zero, vector) > 0f) ? fgBodyPart3 : fgBodyPart2))] = 1f;
				}
				break;
			}
			}
		}
		void Put(int v, FgBodyPart fgBodyPart5, FgBodyPart fgBodyPart6, float t, float edge)
		{
			float num5 = Math.Clamp((t - (edge - 0.06f)) / 0.12f, 0f, 1f);
			rw[(int)(v * 17 + fgBodyPart5)] += 1f - num5;
			rw[(int)(v * 17 + fgBodyPart6)] += num5;
		}
	}

	internal static FgFittedModel Fit(FgModelSource src, FgFit fit, FgFittedModel? reuseLook)
	{
		FgRigSource rig = src.Rig;
		FgFittedModel fgFittedModel = Retarget(rig);
		if (fit.OffsetX != 0f || fit.OffsetY != 0f || fit.OffsetZ != 0f)
		{
			for (int i = 0; i < fgFittedModel.Pos.Length; i++)
			{
				fgFittedModel.Pos[i] += new Vector3(fit.OffsetX, fit.OffsetY, fit.OffsetZ);
			}
		}
		if (reuseLook != null && reuseLook.Rgba.Length != 0 && reuseLook.Uv.Length == fgFittedModel.Pos.Length)
		{
			fgFittedModel.Uv = reuseLook.Uv;
			fgFittedModel.Rgba = reuseLook.Rgba;
			fgFittedModel.Width = reuseLook.Width;
			fgFittedModel.Height = reuseLook.Height;
		}
		else
		{
			FgModelFit.BuildLook(new FgModelSource
			{
				Name = rig.Name,
				Pos = fgFittedModel.Pos,
				Nrm = fgFittedModel.Nrm,
				Uv = rig.Uv,
				Idx = fgFittedModel.Idx,
				Mat = rig.Mat,
				Materials = rig.Materials,
				HasUv = rig.HasUv
			}, fit, fgFittedModel);
		}
		return fgFittedModel;
	}

	public static FgFittedModel Retarget(FgRigSource s)
	{
		int n = s.Pos.Length;
		Vector3 vector = Centroid(FgBodyPart.Head);
		Vector3 vector2 = (Centroid(FgBodyPart.FootL) + Centroid(FgBodyPart.FootR) + Centroid(FgBodyPart.BallL) + Centroid(FgBodyPart.BallR)) / 4f;
		Vector3 vector3 = Vector3.Normalize(vector - vector2);
		Vector3 vector4 = Centroid(FgBodyPart.ThighR) - Centroid(FgBodyPart.ThighL);
		vector4 -= vector3 * Vector3.Dot(vector4, vector3);
		vector4 = Vector3.Normalize(vector4);
		Vector3 vector5 = (Centroid(FgBodyPart.BallL) + Centroid(FgBodyPart.BallR)) / 2f - (Centroid(FgBodyPart.CalfL) + Centroid(FgBodyPart.CalfR)) / 2f;
		vector5 -= vector3 * Vector3.Dot(vector5, vector3);
		vector5 -= vector4 * Vector3.Dot(vector5, vector4);
		if (vector5.LengthSquared() < 1E-10f)
		{
			vector5 = Vector3.Cross(vector3, vector4);
		}
		vector5 = Vector3.Normalize(vector5);
		bool flag = Vector3.Dot(vector4, Vector3.Cross(vector3, vector5)) < 0f;
		Vector3[] pos = new Vector3[n];
		Vector3[] nrm = new Vector3[n];
		for (int i = 0; i < n; i++)
		{
			pos[i] = new Vector3(Vector3.Dot(s.Pos[i], vector4), Vector3.Dot(s.Pos[i], vector3), Vector3.Dot(s.Pos[i], vector5));
			nrm[i] = new Vector3(Vector3.Dot(s.Nrm[i], vector4), Vector3.Dot(s.Nrm[i], vector3), Vector3.Dot(s.Nrm[i], vector5));
		}
		float num = pos.Min((Vector3 p) => p.Y);
		float num2 = pos.Max((Vector3 p) => p.Y);
		float num3 = 1.8f / Math.Max(num2 - num, 0.0001f);
		Vector3 zero = Vector3.Zero;
		Vector3 vector6 = (Centroid(FgBodyPart.FootL) + Centroid(FgBodyPart.FootR)) / 2f;
		zero = new Vector3(Vector3.Dot(vector6, vector4), 0f, Vector3.Dot(vector6, vector5));
		for (int num4 = 0; num4 < n; num4++)
		{
			pos[num4] = new Vector3(pos[num4].X - zero.X, pos[num4].Y - num, pos[num4].Z - zero.Z) * num3;
		}
		Landmarks lm = FindLandmarks(s, pos);
		(Vector3 From, Vector3 To, System.Numerics.Matrix4x4 M)?[] xf = new (Vector3, Vector3, System.Numerics.Matrix4x4)?[17];
		for (int num5 = 0; num5 < 2; num5++)
		{
			BeanSide beanSide = BeanLandmarks(num5);
			xf[(num5 == 0) ? 3 : 4] = Limb(lm.Shoulder[num5], lm.Elbow[num5], beanSide.Shoulder, beanSide.Elbow, 1.25f);
			xf[(num5 == 0) ? 5 : 6] = Limb(lm.Elbow[num5], lm.Wrist[num5], beanSide.Elbow, beanSide.Wrist, 1.25f);
			xf[(num5 == 0) ? 7 : 8] = Limb(lm.Wrist[num5], lm.HandTip[num5], beanSide.Wrist, beanSide.HandTip, 1.4f);
			xf[(num5 == 0) ? 9 : 10] = Limb(lm.Hip[num5], lm.Knee[num5], beanSide.Hip, beanSide.Knee, 1.5f);
			xf[(num5 == 0) ? 11 : 12] = Limb(lm.Knee[num5], lm.Ankle[num5], beanSide.Knee, beanSide.Ankle, 1.5f);
			xf[(num5 == 0) ? 13 : 14] = Limb(lm.Ankle[num5], lm.Ball[num5], beanSide.Ankle, beanSide.Ball, 1.5f);
			xf[(num5 == 0) ? 15 : 16] = Limb(lm.Ball[num5], lm.Toe[num5], beanSide.Ball, beanSide.Toe, 1.5f);
		}
		(float From, float To)[] knots = new (float, float)[4]
		{
			(lm.HipY, 0.262f),
			(lm.ShoulderY, 1.133f),
			(lm.NeckY, 1.3f),
			(lm.TopY, 1.925f)
		};
		Vector3[] outPos = new Vector3[n];
		Vector3[] outNrm = new Vector3[n];
		float[] skinW = new float[n * 4];
		int[] skinB = new int[n * 4];
		Parallel.For(0, n, (int v) =>
		{
			Vector3 p = pos[v];
			Vector3 vector7 = nrm[v];
			Vector3 zero2 = Vector3.Zero;
			Vector3 zero3 = Vector3.Zero;
			float num9 = 0f;
			Dictionary<int, float> dictionary = new Dictionary<int, float>(8);
			float num10 = 0f;
			for (int j = 0; j < 17; j++)
			{
				float num11 = s.Role[v * 17 + j];
				if (!(num11 <= 0f))
				{
					(Vector3, Vector3, System.Numerics.Matrix4x4)? tuple = xf[j];
					if (tuple.HasValue)
					{
						(Vector3, Vector3, System.Numerics.Matrix4x4) valueOrDefault = tuple.GetValueOrDefault();
						Vector3 vector8 = valueOrDefault.Item2 + Vector3.Transform(p - valueOrDefault.Item1, valueOrDefault.Item3);
						zero2 += vector8 * num11;
						zero3 += NormalXf(valueOrDefault.Item3, vector7) * num11;
						num9 += num11;
						int num12 = (j - 3) % 2;
						switch ((FgBodyPart)j)
						{
						case FgBodyPart.UpArmL:
						case FgBodyPart.UpArmR:
							Add(dictionary, bShoulder[num12], num11);
							break;
						case FgBodyPart.LoArmL:
						case FgBodyPart.LoArmR:
							Add(dictionary, bElbow[num12], num11);
							break;
						case FgBodyPart.HandL:
						case FgBodyPart.HandR:
							Add(dictionary, bWrist[num12], num11);
							break;
						case FgBodyPart.ThighL:
						case FgBodyPart.ThighR:
							Add(dictionary, bHip[num12], num11);
							break;
						case FgBodyPart.CalfL:
						case FgBodyPart.CalfR:
							Add(dictionary, bKnee[num12], num11);
							break;
						case FgBodyPart.FootL:
						case FgBodyPart.FootR:
							Add(dictionary, bAnkle[num12], num11);
							break;
						case FgBodyPart.BallL:
						case FgBodyPart.BallR:
							Add(dictionary, bBall[num12], num11);
							break;
						}
					}
					else
					{
						num10 += num11;
					}
				}
			}
			if (num10 > 0f)
			{
				float num13 = Warp(p.Y);
				float num14 = Width(p.Y) * Widen(p.Y);
				float num15 = Width(p.Y) * (1f + (Widen(p.Y) - 1f) * 0.5f);
				Vector3 vector9 = new Vector3(p.X * num14, num13, p.Z * num15);
				Vector3 value = new Vector3(vector7.X / num14, vector7.Y / Math.Max(Slope(Math.Clamp(Array.FindLastIndex(knots, ((float From, float To) kk) => kk.From <= p.Y), 0, knots.Length - 2)), 0.05f), vector7.Z / num15);
				zero2 += vector9 * num10;
				zero3 += ((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : vector7) * num10;
				num9 += num10;
				float num16 = s.Role[v * 17 + 2];
				float num17 = s.Role[v * 17 + 1];
				float num18 = s.Role[v * 17];
				if (num16 > 0f)
				{
					Add(dictionary, 2, num16);
				}
				float num19 = num17 + num18;
				if (num19 > 0f)
				{
					float num20 = Hat(num13, float.NegativeInfinity, Y[0], Y[1]);
					float num21 = Hat(num13, Y[0], Y[1], Y[2]);
					float num22 = Hat(num13, Y[1], Y[2], float.PositiveInfinity);
					float num23 = num20 + num21 + num22;
					if (num23 <= 1E-05f)
					{
						num20 = 1f;
						num23 = 1f;
					}
					Add(dictionary, 25, num19 * num20 / num23);
					Add(dictionary, 24, num19 * num21 / num23);
					Add(dictionary, 1, num19 * num22 / num23);
				}
			}
			if (num9 <= 0f)
			{
				outPos[v] = p;
				outNrm[v] = vector7;
				Add(dictionary, 25, 1f);
			}
			else
			{
				outPos[v] = zero2 / num9;
				Vector3 value2 = zero3 / num9;
				outNrm[v] = ((value2.LengthSquared() > 1E-12f) ? Vector3.Normalize(value2) : vector7);
			}
			List<KeyValuePair<int, float>> list = dictionary.OrderByDescending((KeyValuePair<int, float> x) => x.Value).Take(4).ToList();
			float num24 = list.Sum((KeyValuePair<int, float> x) => x.Value);
			if (num24 <= 0f)
			{
				skinW[v * 4] = 1f;
				skinB[v * 4] = 25;
			}
			else
			{
				for (int num25 = 0; num25 < list.Count; num25++)
				{
					skinW[v * 4 + num25] = list[num25].Value / num24;
					skinB[v * 4 + num25] = list[num25].Key;
				}
			}
		});
		int[] array = (int[])s.Idx.Clone();
		if (flag)
		{
			for (int num6 = 0; num6 + 2 < array.Length; num6 += 3)
			{
				ref int reference = ref array[num6 + 1];
				ref int reference2 = ref array[num6 + 2];
				int num7 = array[num6 + 2];
				int num8 = array[num6 + 1];
				reference = num7;
				reference2 = num8;
			}
		}
		return new FgFittedModel
		{
			Pos = outPos,
			Nrm = outNrm,
			Idx = array,
			SkinW = skinW,
			SkinB = skinB
		};
		Vector3 Centroid(FgBodyPart r)
		{
			Vector3 zero2 = Vector3.Zero;
			double num9 = 0.0;
			for (int j = 0; j < n; j++)
			{
				float num10 = s.Role[(int)(j * 17 + r)];
				if (!(num10 <= 0f))
				{
					zero2 += s.Pos[j] * num10;
					num9 += (double)num10;
				}
			}
			if (!(num9 > 0.0))
			{
				return Vector3.Zero;
			}
			return zero2 / (float)num9;
		}
		float Slope(int seg)
		{
			return (knots[seg + 1].To - knots[seg].To) / Math.Max(knots[seg + 1].From - knots[seg].From, 0.0001f);
		}
		float Warp(float y)
		{
			if (y <= knots[0].From)
			{
				return Math.Max(0f, knots[0].To + (y - knots[0].From) * Slope(0));
			}
			for (int j = 1; j < knots.Length; j++)
			{
				if (y <= knots[j].From)
				{
					float num9 = (y - knots[j - 1].From) / Math.Max(knots[j].From - knots[j - 1].From, 0.0001f);
					return knots[j - 1].To + num9 * (knots[j].To - knots[j - 1].To);
				}
			}
			return knots[^1].To + (y - knots[^1].From) * Slope(knots.Length - 2);
		}
		float Widen(float y)
		{
			float num9 = Math.Clamp((y - lm.ShoulderY) / Math.Max(lm.NeckY - lm.ShoulderY, 0.001f), 0f, 1f);
			return 1.3f + -0.29999995f * num9;
		}
		float Width(float y)
		{
			float num9 = 0.07f;
			return Math.Clamp((Warp(y + num9) - Warp(y - num9)) / (2f * num9), 1f, 3.4f);
		}
	}

	private static void Add(Dictionary<int, float> d, int bone, float w)
	{
		if (w > 0f)
		{
			d[bone] = d.GetValueOrDefault(bone) + w;
		}
	}

	private static float Hat(float x, float lo, float mid, float hi)
	{
		if (x <= mid)
		{
			if (!float.IsNegativeInfinity(lo))
			{
				return Math.Clamp((x - lo) / (mid - lo), 0f, 1f);
			}
			return 1f;
		}
		if (!float.IsPositiveInfinity(hi))
		{
			return Math.Clamp((hi - x) / (hi - mid), 0f, 1f);
		}
		return 1f;
	}

	private static Vector3 NormalXf(System.Numerics.Matrix4x4 m, Vector3 n)
	{
		if (!System.Numerics.Matrix4x4.Invert(m, out var result))
		{
			return n;
		}
		Vector3 value = new Vector3(result.M11 * n.X + result.M12 * n.Y + result.M13 * n.Z, result.M21 * n.X + result.M22 * n.Y + result.M23 * n.Z, result.M31 * n.X + result.M32 * n.Y + result.M33 * n.Z);
		if (!(value.LengthSquared() > 1E-12f))
		{
			return n;
		}
		return Vector3.Normalize(value);
	}

	private static (Vector3 From, Vector3 To, System.Numerics.Matrix4x4 M) Limb(Vector3 j0, Vector3 j1, Vector3 b0, Vector3 b1, float radial)
	{
		Vector3 vector = j1 - j0;
		Vector3 vector2 = b1 - b0;
		float num = Math.Max(vector.Length(), 0.001f);
		float num2 = Math.Max(vector2.Length(), 0.001f);
		Vector3 vector3 = vector / num;
		Vector3 to = vector2 / num2;
		System.Numerics.Matrix4x4 matrix4x = System.Numerics.Matrix4x4.CreateFromQuaternion(ShortestArc(vector3, to));
		float along = Math.Clamp(num2 / num, 0.2f, 4f);
		System.Numerics.Matrix4x4 matrix4x2 = ScaleAlong(vector3, along, radial);
		return (From: j0, To: b0, M: matrix4x2 * matrix4x);
	}

	private static System.Numerics.Matrix4x4 ScaleAlong(Vector3 axis, float along, float radial)
	{
		float num = along - radial;
		return new System.Numerics.Matrix4x4(radial + num * axis.X * axis.X, num * axis.X * axis.Y, num * axis.X * axis.Z, 0f, num * axis.Y * axis.X, radial + num * axis.Y * axis.Y, num * axis.Y * axis.Z, 0f, num * axis.Z * axis.X, num * axis.Z * axis.Y, radial + num * axis.Z * axis.Z, 0f, 0f, 0f, 0f, 1f);
	}

	private static System.Numerics.Quaternion ShortestArc(Vector3 from, Vector3 to)
	{
		float num = Vector3.Dot(from, to);
		if (num > 0.99999f)
		{
			return System.Numerics.Quaternion.Identity;
		}
		if (num < -0.99999f)
		{
			Vector3 value = Vector3.Cross(from, Vector3.UnitX);
			if (value.LengthSquared() < 0.0001f)
			{
				value = Vector3.Cross(from, Vector3.UnitZ);
			}
			return System.Numerics.Quaternion.CreateFromAxisAngle(Vector3.Normalize(value), (float)Math.PI);
		}
		Vector3 vector = Vector3.Cross(from, to);
		return System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(vector.X, vector.Y, vector.Z, 1f + num));
	}

	private static BeanSide BeanLandmarks(int side)
	{
		return new BeanSide(L(0.487f, 1.133f, 0.079f, side), L(0.833f, 1.12f, 0.086f, side), L(1.097f, 1.11f, 0.153f, side), L(1.4f, 1.09f, 0.2f, side), L(0.157f, 0.244f, 0.015f, side), L(0.157f, 0.176f, 0.055f, side), L(0.157f, 0.119f, 0.013f, side), L(0.157f, 0.042f, 0.109f, side), L(0.157f, 0.04f, 0.292f, side));
	}

	private static Landmarks FindLandmarks(FgRigSource s, Vector3[] pos)
	{
		int n = pos.Length;
		Landmarks lm = new Landmarks();
		for (int side = 0; side < 2; side++)
		{
			FgBodyPart thigh = ((side == 0) ? FgBodyPart.ThighL : FgBodyPart.ThighR);
			FgBodyPart calf = ((side == 0) ? FgBodyPart.CalfL : FgBodyPart.CalfR);
			FgBodyPart fgBodyPart = ((side == 0) ? FgBodyPart.FootL : FgBodyPart.FootR);
			FgBodyPart fgBodyPart2 = ((side == 0) ? FgBodyPart.BallL : FgBodyPart.BallR);
			FgBodyPart upA = ((side == 0) ? FgBodyPart.UpArmL : FgBodyPart.UpArmR);
			FgBodyPart loA = ((side == 0) ? FgBodyPart.LoArmL : FgBodyPart.LoArmR);
			FgBodyPart fgBodyPart3 = ((side == 0) ? FgBodyPart.HandL : FgBodyPart.HandR);
			lm.Hip[side] = Between(thigh, FgBodyPart.Pelvis, () => End(thigh, 1, max: true) - new Vector3(0f, 0.03f, 0f));
			lm.Knee[side] = Between(thigh, calf, () => (lm.Hip[side] + End(calf, 1, max: false)) / 2f);
			lm.Ankle[side] = Between(calf, fgBodyPart, () => End(calf, 1, max: false));
			lm.Ball[side] = Between(fgBodyPart, fgBodyPart2, () => lm.Ankle[side] + new Vector3(0f, -0.05f, 0.12f));
			Vector3 vector = End(fgBodyPart2, 2, max: true);
			lm.Toe[side] = ((vector == Vector3.Zero || vector.Z - lm.Ball[side].Z < 0.04f) ? (lm.Ball[side] + new Vector3(0f, 0f, 0.1f)) : new Vector3(lm.Ball[side].X, Math.Min(lm.Ball[side].Y, vector.Y + 0.02f), vector.Z));
			lm.Shoulder[side] = Between(upA, FgBodyPart.Spine, () => End(upA, 1, max: true) - new Vector3(0f, 0.04f, 0f));
			lm.Elbow[side] = Between(upA, loA, () => (lm.Shoulder[side] + End(loA, 1, max: false)) / 2f);
			lm.Wrist[side] = Between(loA, fgBodyPart3, () => End(loA, 1, max: false));
			Vector3 vector2 = End(fgBodyPart3, 1, max: false);
			lm.HandTip[side] = ((vector2 == Vector3.Zero) ? (lm.Wrist[side] + new Vector3(0f, -0.1f, 0f)) : new Vector3(lm.Wrist[side].X, vector2.Y, vector2.Z));
		}
		lm.HipY = (lm.Hip[0].Y + lm.Hip[1].Y) / 2f;
		lm.ShoulderY = (lm.Shoulder[0].Y + lm.Shoulder[1].Y) / 2f;
		lm.TopY = pos.Max((Vector3 p) => p.Y);
		Vector3 vector3 = Between(FgBodyPart.Spine, FgBodyPart.Head, () => new Vector3(0f, lm.ShoulderY + 0.08f, 0f));
		lm.NeckY = Math.Clamp(vector3.Y, lm.ShoulderY + 0.02f, lm.TopY - 0.12f);
		if (lm.ShoulderY <= lm.HipY + 0.1f)
		{
			lm.ShoulderY = lm.HipY + (lm.NeckY - lm.HipY) * 0.85f;
		}
		if (Trace != null)
		{
			for (int num = 0; num < 2; num++)
			{
				Trace($"side {num}: hip {lm.Hip[num]:0.00} knee {lm.Knee[num]:0.00} ankle {lm.Ankle[num]:0.00} ball {lm.Ball[num]:0.00} toe {lm.Toe[num]:0.00} | shoulder {lm.Shoulder[num]:0.00} elbow {lm.Elbow[num]:0.00} wrist {lm.Wrist[num]:0.00} tip {lm.HandTip[num]:0.00}");
			}
		}
		Trace?.Invoke($"hipY {lm.HipY:0.00} shoulderY {lm.ShoulderY:0.00} neckY {lm.NeckY:0.00} topY {lm.TopY:0.00}");
		return lm;
		Vector3 Between(FgBodyPart a, FgBodyPart b, Func<Vector3>? fallback = null)
		{
			Vector3 zero = Vector3.Zero;
			double num2 = 0.0;
			for (int i = 0; i < n; i++)
			{
				float num3 = Math.Min(s.Role[(int)(i * 17 + a)], s.Role[(int)(i * 17 + b)]);
				if (!(num3 <= 0.02f))
				{
					zero += pos[i] * num3;
					num2 += (double)num3;
				}
			}
			if (num2 > 0.001)
			{
				return zero / (float)num2;
			}
			return fallback?.Invoke() ?? Vector3.Zero;
		}
		Vector3 End(FgBodyPart r, int axis, bool max, float slice = 0.12f)
		{
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			for (int i = 0; i < n; i++)
			{
				if (s.Role[(int)(i * 17 + r)] > 0.5f)
				{
					float val = Comp(pos[i], axis);
					num2 = Math.Min(num2, val);
					num3 = Math.Max(num3, val);
				}
			}
			if (num2 > num3)
			{
				return Vector3.Zero;
			}
			float num4 = (max ? (num3 - (num3 - num2) * slice) : (num2 + (num3 - num2) * slice));
			Vector3 zero = Vector3.Zero;
			int num5 = 0;
			for (int j = 0; j < n; j++)
			{
				if (!(s.Role[(int)(j * 17 + r)] <= 0.5f))
				{
					float num6 = Comp(pos[j], axis);
					if (max ? (num6 >= num4) : (num6 <= num4))
					{
						zero += pos[j];
						num5++;
					}
				}
			}
			Vector3 result = ((num5 > 0) ? (zero / num5) : Vector3.Zero);
			switch (axis)
			{
			case 0:
				result.X = (max ? num3 : num2);
				break;
			case 1:
				result.Y = (max ? num3 : num2);
				break;
			default:
				result.Z = (max ? num3 : num2);
				break;
			}
			return result;
		}
	}

	private static float Comp(Vector3 v, int axis)
	{
		return axis switch
		{
			1 => v.Y, 
			0 => v.X, 
			_ => v.Z, 
		};
	}
}
