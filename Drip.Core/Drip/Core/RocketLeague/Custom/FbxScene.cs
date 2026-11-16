using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Drip.Core.RocketLeague.Custom;

public sealed class FbxScene
{
	private sealed class ModelInfo
	{
		public long Id;

		public string Name = "";

		public string Kind = "";

		public long Parent;

		public Matrix4x4 Local = Matrix4x4.Identity;

		public FbxNode? Geometry;

		public List<long> Materials = new List<long>();
	}

	public int Version;

	public int UpAxis = 1;

	public int UpSign = 1;

	public int FrontAxis = 2;

	public int FrontSign = 1;

	public double UnitScale = 1.0;

	public List<FbxMesh> Meshes = new List<FbxMesh>();

	public static FbxScene Load(string path)
	{
		return Parse(File.ReadAllBytes(path));
	}

	public static FbxScene Parse(byte[] d)
	{
		if (d.Length < 27 || Encoding.ASCII.GetString(d, 0, "Kaydara FBX Binary".Length) != "Kaydara FBX Binary")
		{
			throw new FbxFormatException("That isn't a binary FBX file. Re-export it as FBX Binary (not ASCII).");
		}
		int num = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(23, 4));
		bool flag = num >= 7500;
		int pos = 27;
		List<FbxNode> list = new List<FbxNode>();
		while (pos + (flag ? 25 : 13) <= d.Length)
		{
			FbxNode fbxNode = ReadNode(d, ref pos, flag);
			if (fbxNode == null)
			{
				break;
			}
			list.Add(fbxNode);
		}
		FbxScene fbxScene = new FbxScene();
		fbxScene.Version = num;
		fbxScene.Build(list);
		return fbxScene;
	}

	private static FbxNode? ReadNode(byte[] d, ref int pos, bool wide)
	{
		long num;
		long num2;
		int num3;
		if (wide)
		{
			num = BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(pos, 8));
			num2 = BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(pos + 8, 8));
			BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(pos + 16, 8));
			num3 = d[pos + 24];
			pos += 25;
		}
		else
		{
			num = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos, 4));
			num2 = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos + 4, 4));
			BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos + 8, 4));
			num3 = d[pos + 12];
			pos += 13;
		}
		if (num == 0L)
		{
			return null;
		}
		if (num > d.Length || pos + num3 > d.Length)
		{
			throw new FbxFormatException("The FBX file is damaged or cut short.");
		}
		FbxNode fbxNode = new FbxNode
		{
			Name = Encoding.Latin1.GetString(d, pos, num3)
		};
		pos += num3;
		for (long num4 = 0L; num4 < num2; num4++)
		{
			fbxNode.Props.Add(ReadProp(d, ref pos));
		}
		while (pos < num)
		{
			FbxNode fbxNode2 = ReadNode(d, ref pos, wide);
			if (fbxNode2 == null)
			{
				break;
			}
			fbxNode.Children.Add(fbxNode2);
		}
		pos = (int)num;
		return fbxNode;
	}

	private static object ReadProp(byte[] d, ref int pos)
	{
		char c = (char)d[pos++];
		switch (c)
		{
		case 'Y':
		{
			short num12 = BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(pos, 2));
			pos += 2;
			return num12;
		}
		case 'C':
		{
			byte b = d[pos];
			pos++;
			return b;
		}
		case 'I':
		{
			int num10 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(pos, 4));
			pos += 4;
			return num10;
		}
		case 'F':
		{
			float num9 = BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(pos, 4));
			pos += 4;
			return num9;
		}
		case 'D':
		{
			double num8 = BinaryPrimitives.ReadDoubleLittleEndian(d.AsSpan(pos, 8));
			pos += 8;
			return num8;
		}
		case 'L':
		{
			long num11 = BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(pos, 8));
			pos += 8;
			return num11;
		}
		case 'R':
		case 'S':
		{
			int num7 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(pos, 4));
			pos += 4;
			if (num7 < 0 || pos + num7 > d.Length)
			{
				throw new FbxFormatException("The FBX file is damaged.");
			}
			object result = ((c == 'S') ? ((object)Encoding.Latin1.GetString(d, pos, num7)) : ((object)d.AsSpan(pos, num7).ToArray()));
			pos += num7;
			return result;
		}
		case 'b':
		case 'd':
		case 'f':
		case 'i':
		case 'l':
		{
			int num = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(pos, 4));
			int num2 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(pos + 4, 4));
			int num3 = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(pos + 8, 4));
			pos += 12;
			if (num < 0 || num3 < 0 || pos + num3 > d.Length)
			{
				throw new FbxFormatException("The FBX file is damaged.");
			}
			int num4;
			switch (c)
			{
			case 'f':
			case 'i':
				num4 = 4;
				break;
			case 'd':
			case 'l':
				num4 = 8;
				break;
			default:
				num4 = 1;
				break;
			}
			int num5 = num4;
			byte[] array;
			if (num2 == 1)
			{
				array = new byte[(long)num * (long)num5];
				using ZLibStream zLibStream = new ZLibStream(new MemoryStream(d, pos, num3), CompressionMode.Decompress);
				int i;
				int num6;
				for (i = 0; i < array.Length; i += num6)
				{
					num6 = zLibStream.Read(array, i, array.Length - i);
					if (num6 <= 0)
					{
						break;
					}
				}
				if (i < array.Length)
				{
					throw new FbxFormatException("An array in the FBX file is shorter than it says.");
				}
			}
			else
			{
				array = d.AsSpan(pos, num3).ToArray();
			}
			pos += num3;
			switch (c)
			{
			case 'd':
			{
				double[] array5 = new double[num];
				Buffer.BlockCopy(array, 0, array5, 0, array.Length);
				return array5;
			}
			case 'f':
			{
				float[] array4 = new float[num];
				Buffer.BlockCopy(array, 0, array4, 0, array.Length);
				return array4;
			}
			case 'i':
			{
				int[] array3 = new int[num];
				Buffer.BlockCopy(array, 0, array3, 0, array.Length);
				return array3;
			}
			case 'l':
			{
				long[] array2 = new long[num];
				Buffer.BlockCopy(array, 0, array2, 0, array.Length);
				return array2;
			}
			default:
				return array;
			}
		}
		default:
			throw new FbxFormatException("The FBX file has data this reader doesn't know (type '" + c + "').");
		}
	}

	private static string Clean(string s)
	{
		int num = s.IndexOf('\0');
		if (num < 0)
		{
			return s;
		}
		return s.Substring(0, num);
	}

	private void Build(List<FbxNode> top)
	{
		FbxNode gs = top.FirstOrDefault((FbxNode n) => n.Name == "GlobalSettings");
		if (gs != null)
		{
			UpAxis = Get("UpAxis", 1);
			UpSign = Get("UpAxisSign", 1);
			FrontAxis = Get("FrontAxis", 2);
			FrontSign = Get("FrontAxisSign", 1);
			double[] array = gs.Prop("UnitScaleFactor");
			if (array.Length != 0 && array[0] > 0.0)
			{
				UnitScale = array[0];
			}
		}
		FbxNode? fbxNode = top.FirstOrDefault((FbxNode n) => n.Name == "Objects") ?? throw new FbxFormatException("The FBX file has no objects in it.");
		Dictionary<long, ModelInfo> models = new Dictionary<long, ModelInfo>();
		Dictionary<long, FbxNode> dictionary = new Dictionary<long, FbxNode>();
		Dictionary<long, string> mats = new Dictionary<long, string>();
		Dictionary<long, FbxMaterial> matInfo = new Dictionary<long, FbxMaterial>();
		Dictionary<long, string> dictionary2 = new Dictionary<long, string>();
		foreach (FbxNode child in fbxNode.Children)
		{
			long num = child.Long();
			string text = Clean(child.Str(1));
			if (child.Name == "Model")
			{
				models[num] = new ModelInfo
				{
					Id = num,
					Name = text,
					Kind = child.Str(2),
					Local = LocalMatrix(child)
				};
			}
			else if (child.Name == "Geometry" && child.Str(2) == "Mesh")
			{
				dictionary[num] = child;
			}
			else if (child.Name == "Material")
			{
				mats[num] = text;
				double[] array2 = child.Prop("DiffuseColor");
				matInfo[num] = new FbxMaterial
				{
					Name = text,
					Color = ((array2.Length >= 3) ? new Vector3((float)array2[0], (float)array2[1], (float)array2[2]) : new Vector3(0.8f))
				};
			}
			else if (child.Name == "Texture")
			{
				string text2 = child.Child("RelativeFilename")?.Str() ?? "";
				if (text2.Length == 0)
				{
					text2 = child.Child("FileName")?.Str() ?? "";
				}
				text2 = Clean(text2).Replace('\\', '/');
				int num2 = text2.LastIndexOf('/');
				dictionary2[num] = ((num2 >= 0) ? text2.Substring(num2 + 1) : text2);
			}
		}
		FbxNode fbxNode2 = top.FirstOrDefault((FbxNode n) => n.Name == "Connections");
		if (fbxNode2 != null)
		{
			foreach (FbxNode child2 in fbxNode2.Children)
			{
				if (child2.Name == "C" && child2.Str() == "OP" && dictionary2.TryGetValue(child2.Long(1), out var value) && matInfo.TryGetValue(child2.Long(2), out FbxMaterial value2) && child2.Str(3).Contains("Diffuse"))
				{
					value2.HasTexture = true;
					value2.TextureFile = value;
				}
				if (!(child2.Name != "C") && !(child2.Str() != "OO"))
				{
					long num3 = child2.Long(1);
					long num4 = child2.Long(2);
					FbxNode value4;
					ModelInfo value5;
					ModelInfo value6;
					if (models.TryGetValue(num3, out ModelInfo value3) && (models.ContainsKey(num4) || num4 == 0L))
					{
						value3.Parent = num4;
					}
					else if (dictionary.TryGetValue(num3, out value4) && models.TryGetValue(num4, out value5))
					{
						value5.Geometry = value4;
					}
					else if (mats.ContainsKey(num3) && models.TryGetValue(num4, out value6))
					{
						value6.Materials.Add(num3);
					}
				}
			}
		}
		Dictionary<long, Matrix4x4> world = new Dictionary<long, Matrix4x4>();
		foreach (ModelInfo value10 in models.Values)
		{
			if (value10.Geometry == null)
			{
				continue;
			}
			FbxMesh fbxMesh = ExtractMesh(value10.Geometry, World(value10), value10.Name, value10.Materials.Select((long x) => mats[x]).ToList());
			if (fbxMesh != null)
			{
				fbxMesh.Materials = value10.Materials.Select((long x) => matInfo[x]).ToList();
			}
			if (fbxMesh != null)
			{
				List<string> list = new List<string>();
				ModelInfo modelInfo = value10;
				int num5 = 0;
				while (modelInfo != null && num5++ < 64)
				{
					list.Add(modelInfo.Name);
					modelInfo = ((modelInfo.Parent != 0L && models.TryGetValue(modelInfo.Parent, out ModelInfo value7)) ? value7 : null);
				}
				fbxMesh.Path = list;
			}
			if (fbxMesh != null && fbxMesh.TriangleCount > 0)
			{
				Meshes.Add(fbxMesh);
			}
		}
		if (Meshes.Count == 0)
		{
			throw new FbxFormatException("No meshes found in that FBX.");
		}
		int Get(string k, int def)
		{
			double[] array3 = gs.Prop(k);
			if (array3.Length == 0)
			{
				return def;
			}
			return (int)array3[0];
		}
		Matrix4x4 World(ModelInfo m, int depth = 0)
		{
			if (world.TryGetValue(m.Id, out var value8))
			{
				return value8;
			}
			Matrix4x4 matrix4x = m.Local;
			if (depth < 64 && m.Parent != 0L && models.TryGetValue(m.Parent, out ModelInfo value9))
			{
				matrix4x = m.Local * World(value9, depth + 1);
			}
			world[m.Id] = matrix4x;
			return matrix4x;
		}
	}

	private static Matrix4x4 LocalMatrix(FbxNode model)
	{
		Vector3 position = V("Lcl Translation", Vector3.Zero);
		Vector3 deg = V("Lcl Rotation", Vector3.Zero);
		Vector3 scales = V("Lcl Scaling", Vector3.One);
		Vector3 deg2 = V("PreRotation", Vector3.Zero);
		return Matrix4x4.CreateScale(scales) * Rot(deg) * Rot(deg2) * Matrix4x4.CreateTranslation(position);
		static Matrix4x4 Rot(Vector3 vector)
		{
			return Matrix4x4.CreateRotationX(vector.X * (float)Math.PI / 180f) * Matrix4x4.CreateRotationY(vector.Y * (float)Math.PI / 180f) * Matrix4x4.CreateRotationZ(vector.Z * (float)Math.PI / 180f);
		}
		Vector3 V(string k, Vector3 def)
		{
			double[] array = model.Prop(k);
			if (array.Length < 3)
			{
				return def;
			}
			return new Vector3((float)array[0], (float)array[1], (float)array[2]);
		}
	}

	private static FbxMesh? ExtractMesh(FbxNode g, Matrix4x4 world, string name, List<string> materialNames)
	{
		double[] verts = g.Child("Vertices")?.Doubles() ?? Array.Empty<double>();
		int[] array = g.Child("PolygonVertexIndex")?.Ints() ?? Array.Empty<int>();
		if (verts.Length < 9 || array.Length < 3)
		{
			return null;
		}
		FbxNode fbxNode = Layer("LayerElementNormal");
		FbxNode fbxNode2 = Layer("LayerElementUV");
		FbxNode? fbxNode3 = Layer("LayerElementMaterial");
		double[] normals = fbxNode?.Child("Normals")?.Doubles() ?? Array.Empty<double>();
		int[] nIdx = fbxNode?.Child("NormalsIndex")?.Ints() ?? Array.Empty<int>();
		string nMap = Mode(fbxNode, "MappingInformationType");
		string nRef = Mode(fbxNode, "ReferenceInformationType");
		double[] uvs = fbxNode2?.Child("UV")?.Doubles() ?? Array.Empty<double>();
		int[] uIdx = fbxNode2?.Child("UVIndex")?.Ints() ?? Array.Empty<int>();
		string uMap = Mode(fbxNode2, "MappingInformationType");
		string uRef = Mode(fbxNode2, "ReferenceInformationType");
		int[] array2 = fbxNode3?.Child("Materials")?.Ints() ?? Array.Empty<int>();
		string text = Mode(fbxNode3, "MappingInformationType");
		Matrix4x4.Invert(world, out var result);
		Matrix4x4 nrmMat = Matrix4x4.Transpose(result);
		FbxMesh fbxMesh = new FbxMesh
		{
			Name = name,
			MaterialNames = materialNames
		};
		List<int> list = new List<int>();
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			list.Add(i);
			if (array[i] >= 0)
			{
				continue;
			}
			int item = 0;
			if (array2.Length != 0)
			{
				item = ((!(text == "ByPolygon")) ? array2[0] : ((num < array2.Length) ? array2[num] : 0));
			}
			for (int j = 1; j + 1 < list.Count; j++)
			{
				int[] array3 = new int[3]
				{
					list[0],
					list[j],
					list[j + 1]
				};
				foreach (int num2 in array3)
				{
					int num3 = array[num2];
					int num4 = ((num3 < 0) ? (~num3) : num3);
					if (num4 * 3 + 2 >= verts.Length)
					{
						return null;
					}
					fbxMesh.Pos.Add(P(num4));
					fbxMesh.Nrm.Add(N(num2, num4));
					fbxMesh.Uv.Add(UV(num2, num4));
				}
				fbxMesh.Material.Add(item);
			}
			list.Clear();
			num++;
		}
		return fbxMesh;
		FbxNode? Layer(string n)
		{
			return g.Child(n);
		}
		static string Mode(FbxNode? l, string name2)
		{
			return l?.Child(name2)?.Str() ?? "";
		}
		Vector3 N(int corner, int vi)
		{
			if (normals.Length < 3)
			{
				return Vector3.Zero;
			}
			int num5 = ((nMap == "ByVertice" || nMap == "ByVertex") ? vi : corner);
			if (nRef == "IndexToDirect" && nIdx.Length != 0)
			{
				num5 = ((num5 < nIdx.Length) ? nIdx[num5] : 0);
			}
			if (num5 < 0 || num5 * 3 + 2 >= normals.Length)
			{
				return Vector3.Zero;
			}
			Vector3 value = Vector3.TransformNormal(new Vector3((float)normals[num5 * 3], (float)normals[num5 * 3 + 1], (float)normals[num5 * 3 + 2]), nrmMat);
			if (!(value.LengthSquared() > 1E-12f))
			{
				return Vector3.Zero;
			}
			return Vector3.Normalize(value);
		}
		Vector3 P(int vi)
		{
			return Vector3.Transform(new Vector3((float)verts[vi * 3], (float)verts[vi * 3 + 1], (float)verts[vi * 3 + 2]), world);
		}
		Vector2 UV(int corner, int vi)
		{
			if (uvs.Length < 2)
			{
				return Vector2.Zero;
			}
			int num5 = ((uMap == "ByVertice" || uMap == "ByVertex") ? vi : corner);
			if (uRef == "IndexToDirect" || uIdx.Length != 0)
			{
				num5 = ((num5 < uIdx.Length) ? uIdx[num5] : 0);
			}
			if (num5 < 0 || num5 * 2 + 1 >= uvs.Length)
			{
				return Vector2.Zero;
			}
			return new Vector2((float)uvs[num5 * 2], (float)uvs[num5 * 2 + 1]);
		}
	}
}
