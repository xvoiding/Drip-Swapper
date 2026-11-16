using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AssetsTools.NET;

namespace Drip.Core.FallGuys;

public static class FgMeshIo
{
	public static FgSkinMesh Read(AssetTypeValueField mesh)
	{
		FgSkinMesh fgSkinMesh = new FgSkinMesh
		{
			Name = mesh["m_Name"].AsString
		};
		List<AssetTypeValueField> children = mesh["m_BindPose"]["Array"].Children;
		fgSkinMesh.BindPoses = new float[children.Count][];
		for (int i = 0; i < children.Count; i++)
		{
			float[] array = new float[16];
			for (int j = 0; j < 4; j++)
			{
				for (int k = 0; k < 4; k++)
				{
					array[j * 4 + k] = children[i]["e" + j + k].AsFloat;
				}
			}
			fgSkinMesh.BindPoses[i] = array;
		}
		AssetTypeValueField assetTypeValueField = mesh["m_VertexData"];
		int asUInt = (int)assetTypeValueField["m_VertexCount"].AsUInt;
		if (asUInt > 0)
		{
			ReadVertexData(mesh, assetTypeValueField, asUInt, fgSkinMesh);
		}
		else
		{
			ReadCompressed(mesh["m_CompressedMesh"], fgSkinMesh);
		}
		return fgSkinMesh;
	}

	private static AssetTypeValueField Bytes(AssetTypeValueField f)
	{
		AssetTypeValue value = f.Value;
		if (value == null || value.ValueType != AssetValueType.ByteArray)
		{
			return f["Array"];
		}
		return f;
	}

	private static int CompSize(int fmt)
	{
		return fmt switch
		{
			0 => 4, 
			1 => 2, 
			2 => 1, 
			3 => 1, 
			4 => 2, 
			5 => 2, 
			6 => 1, 
			7 => 1, 
			8 => 2, 
			9 => 2, 
			10 => 4, 
			11 => 4, 
			_ => 4, 
		};
	}

	private static float ReadComp(byte[] d, int p, int fmt)
	{
		return fmt switch
		{
			0 => BitConverter.ToSingle(d, p), 
			1 => (float)BitConverter.ToHalf(d, p), 
			2 => (float)(int)d[p] / 255f, 
			3 => Math.Max(-1f, (float)(sbyte)d[p] / 127f), 
			4 => (float)(int)BitConverter.ToUInt16(d, p) / 65535f, 
			5 => Math.Max(-1f, (float)BitConverter.ToInt16(d, p) / 32767f), 
			6 => (int)d[p], 
			7 => (sbyte)d[p], 
			8 => (int)BitConverter.ToUInt16(d, p), 
			9 => BitConverter.ToInt16(d, p), 
			10 => BitConverter.ToUInt32(d, p), 
			11 => BitConverter.ToInt32(d, p), 
			_ => 0f, 
		};
	}

	private static void ReadVertexData(AssetTypeValueField mesh, AssetTypeValueField vd, int n, FgSkinMesh m)
	{
		byte[] data = vd["m_DataSize"].AsByteArray;
		List<AssetTypeValueField> children = vd["m_Channels"]["Array"].Children;
		int nch = children.Count;
		int[] stream = new int[nch];
		int[] off = new int[nch];
		int[] fmt = new int[nch];
		int[] dim = new int[nch];
		for (int i = 0; i < nch; i++)
		{
			stream[i] = children[i]["stream"].AsInt;
			off[i] = children[i]["offset"].AsInt;
			fmt[i] = children[i]["format"].AsInt;
			dim[i] = children[i]["dimension"].AsInt & 0xF;
		}
		int[] stride = new int[4];
		for (int j = 0; j < nch; j++)
		{
			if (dim[j] > 0)
			{
				stride[stream[j]] = Math.Max(stride[stream[j]], off[j] + dim[j] * CompSize(fmt[j]));
			}
		}
		int[] sOff = new int[4];
		int num = 0;
		for (int k = 0; k < 4; k++)
		{
			sOff[k] = num;
			if (stride[k] > 0)
			{
				num = (num + stride[k] * n + 15) & -16;
			}
		}
		float[] array = Chan(0, 3);
		float[] array2 = Chan(1, 3);
		float[] array3 = Chan(2, 4);
		float[] array4 = Chan(4, 2);
		m.Pos = new Vector3[n];
		m.Nrm = new Vector3[n];
		m.Tan = new Vector4[n];
		m.Uv = new Vector2[n];
		for (int l = 0; l < n; l++)
		{
			m.Pos[l] = new Vector3(array[l * 3], array[l * 3 + 1], array[l * 3 + 2]);
			m.Nrm[l] = new Vector3(array2[l * 3], array2[l * 3 + 1], array2[l * 3 + 2]);
			m.Tan[l] = new Vector4(array3[l * 4], array3[l * 4 + 1], array3[l * 4 + 2], array3[l * 4 + 3]);
			m.Uv[l] = new Vector2(array4[l * 2], array4[l * 2 + 1]);
		}
		float[] w = Chan(12, 4);
		float[] array5 = Chan(13, 4);
		m.W = w;
		m.B = new int[n * 4];
		for (int num2 = 0; num2 < m.B.Length; num2++)
		{
			m.B[num2] = (int)array5[num2];
		}
		byte[] asByteArray = Bytes(mesh["m_IndexBuffer"]).AsByteArray;
		bool flag = mesh["m_IndexFormat"].AsInt == 1;
		List<AssetTypeValueField> children2 = mesh["m_SubMeshes"]["Array"].Children;
		List<int> list = new List<int>();
		foreach (AssetTypeValueField item in children2)
		{
			int asUInt = (int)item["firstByte"].AsUInt;
			int asUInt2 = (int)item["indexCount"].AsUInt;
			int asUInt3 = (int)item["baseVertex"].AsUInt;
			if (item["topology"].AsInt == 0)
			{
				for (int num3 = 0; num3 < asUInt2; num3++)
				{
					list.Add(asUInt3 + (int)(flag ? BitConverter.ToUInt32(asByteArray, asUInt + num3 * 4) : BitConverter.ToUInt16(asByteArray, asUInt + num3 * 2)));
				}
			}
		}
		m.Idx = list.ToArray();
		float[] Chan(int ch, int want)
		{
			float[] array6 = new float[n * want];
			if (ch >= nch || dim[ch] == 0)
			{
				return array6;
			}
			int num4 = CompSize(fmt[ch]);
			for (int num5 = 0; num5 < n; num5++)
			{
				int num6 = sOff[stream[ch]] + num5 * stride[stream[ch]] + off[ch];
				for (int num7 = 0; num7 < Math.Min(want, dim[ch]); num7++)
				{
					array6[num5 * want + num7] = ReadComp(data, num6 + num7 * num4, fmt[ch]);
				}
			}
			return array6;
		}
	}

	private static uint[] UnpackInts(AssetTypeValueField pv)
	{
		int asUInt = (int)pv["m_NumItems"].AsUInt;
		int asInt = pv["m_BitSize"].AsInt;
		if (asUInt == 0)
		{
			return Array.Empty<uint>();
		}
		byte[] asByteArray = Bytes(pv["m_Data"]).AsByteArray;
		uint[] array = new uint[asUInt];
		long num = 0L;
		for (int i = 0; i < asUInt; i++)
		{
			ulong num2 = 0uL;
			int num3 = 0;
			while (num3 < asInt)
			{
				if (((asByteArray[num >> 3] >> (int)(num & 7)) & 1) != 0)
				{
					num2 |= (ulong)(1L << num3);
				}
				num3++;
				num++;
			}
			array[i] = (uint)num2;
		}
		return array;
	}

	private static float[] UnpackFloats(AssetTypeValueField pv)
	{
		int asUInt = (int)pv["m_NumItems"].AsUInt;
		int asInt = pv["m_BitSize"].AsInt;
		if (asUInt == 0)
		{
			return Array.Empty<float>();
		}
		float asFloat = pv["m_Range"].AsFloat;
		float asFloat2 = pv["m_Start"].AsFloat;
		float[] array = new float[asUInt];
		if (asInt == 0)
		{
			Array.Fill(array, asFloat2);
			return array;
		}
		uint[] array2 = UnpackInts(pv);
		double num = (double)asFloat / (double)(ulong)((1L << asInt) - 1);
		for (int i = 0; i < asUInt; i++)
		{
			array[i] = (float)((double)array2[i] * num + (double)asFloat2);
		}
		return array;
	}

	private static void ReadCompressed(AssetTypeValueField cm, FgSkinMesh m)
	{
		float[] array = UnpackFloats(cm["m_Vertices"]);
		int num = array.Length / 3;
		m.Pos = new Vector3[num];
		for (int i = 0; i < num; i++)
		{
			m.Pos[i] = new Vector3(array[i * 3], array[i * 3 + 1], array[i * 3 + 2]);
		}
		m.Nrm = new Vector3[num];
		m.Tan = new Vector4[num];
		m.Uv = new Vector2[num];
		float[] array2 = UnpackFloats(cm["m_UV"]);
		if (array2.Length >= num * 2)
		{
			for (int j = 0; j < num; j++)
			{
				m.Uv[j] = new Vector2(array2[j * 2], array2[j * 2 + 1]);
			}
		}
		float[] array3 = UnpackFloats(cm["m_Normals"]);
		if (array3.Length >= num * 2)
		{
			uint[] array4 = UnpackInts(cm["m_NormalSigns"]);
			for (int k = 0; k < num; k++)
			{
				float num2 = array3[k * 2];
				float num3 = array3[k * 2 + 1];
				float num4 = 1f - num2 * num2 - num3 * num3;
				Vector3 vector = ((num4 >= 0f) ? new Vector3(num2, num3, MathF.Sqrt(num4)) : Vector3.Normalize(new Vector3(num2, num3, 0f)));
				if (k < array4.Length && array4[k] == 0)
				{
					vector.Z = 0f - vector.Z;
				}
				m.Nrm[k] = vector;
			}
		}
		m.W = new float[num * 4];
		m.B = new int[num * 4];
		uint[] array5 = UnpackInts(cm["m_Weights"]);
		uint[] array6 = UnpackInts(cm["m_BoneIndices"]);
		if (array5.Length != 0)
		{
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			uint num8 = 0u;
			for (int l = 0; l < array5.Length; l++)
			{
				if (num5 >= num)
				{
					break;
				}
				if (num7 >= array6.Length)
				{
					break;
				}
				m.W[num5 * 4 + num6] = (float)array5[l] / 31f;
				m.B[num5 * 4 + num6] = (int)array6[num7++];
				num6++;
				num8 += array5[l];
				if (num8 >= 31)
				{
					num5++;
					num6 = 0;
					num8 = 0u;
				}
				else if (num6 == 3)
				{
					m.W[num5 * 4 + 3] = (float)(31 - num8) / 31f;
					if (num7 < array6.Length)
					{
						m.B[num5 * 4 + 3] = (int)array6[num7++];
					}
					num5++;
					num6 = 0;
					num8 = 0u;
				}
			}
		}
		uint[] source = UnpackInts(cm["m_Triangles"]);
		m.Idx = source.Select((uint x) => (int)x).ToArray();
	}

	public static void Write(AssetTypeValueField mesh, FgSkinMesh m)
	{
		int vertexCount = m.VertexCount;
		if (vertexCount == 0 || m.Idx.Length < 3)
		{
			throw new ArgumentException("The model has no triangles.");
		}
		if (vertexCount > 1048560)
		{
			throw new ArgumentException("Too many vertices.");
		}
		bool flag = vertexCount > 65535;
		int num = vertexCount * 40;
		int num2 = vertexCount * 8;
		int num3 = vertexCount * 32;
		int num4 = (num + 15) & -16;
		int num5 = (num4 + num2 + 15) & -16;
		int num6 = (num5 + num3 + 15) & -16;
		byte[] data = new byte[num6];
		for (int i = 0; i < vertexCount; i++)
		{
			int num7 = i * 40;
			F(num7, m.Pos[i].X);
			F(num7 + 4, m.Pos[i].Y);
			F(num7 + 8, m.Pos[i].Z);
			Vector3 vector = m.Nrm[i];
			F(num7 + 12, vector.X);
			F(num7 + 16, vector.Y);
			F(num7 + 20, vector.Z);
			Vector4 vector2 = m.Tan[i];
			F(num7 + 24, vector2.X);
			F(num7 + 28, vector2.Y);
			F(num7 + 32, vector2.Z);
			F(num7 + 36, vector2.W);
			int num8 = num4 + i * 8;
			F(num8, m.Uv[i].X);
			F(num8 + 4, m.Uv[i].Y);
			int num9 = num5 + i * 32;
			for (int j = 0; j < 4; j++)
			{
				F(num9 + j * 4, m.W[i * 4 + j]);
				I(num9 + 16 + j * 4, m.B[i * 4 + j]);
			}
		}
		AssetTypeValueField assetTypeValueField = mesh["m_VertexData"];
		assetTypeValueField["m_VertexCount"].AsUInt = (uint)vertexCount;
		List<AssetTypeValueField> children = assetTypeValueField["m_Channels"]["Array"].Children;
		(int, int, int, int)[] array = new (int, int, int, int)[14]
		{
			(0, 0, 0, 3),
			(0, 12, 0, 3),
			(0, 24, 0, 4),
			(0, 0, 0, 0),
			(1, 0, 0, 2),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(0, 0, 0, 0),
			(2, 0, 0, 4),
			(2, 16, 10, 4)
		};
		for (int k = 0; k < children.Count; k++)
		{
			int asInt;
			int asInt2;
			int asInt3;
			int asInt4;
			if (k >= array.Length)
			{
				asInt = 0;
				asInt2 = 0;
				asInt3 = 0;
				asInt4 = 0;
			}
			else
			{
				(asInt, asInt2, asInt3, asInt4) = array[k];
			}
			children[k]["stream"].AsInt = asInt;
			children[k]["offset"].AsInt = asInt2;
			children[k]["format"].AsInt = asInt3;
			children[k]["dimension"].AsInt = asInt4;
		}
		assetTypeValueField["m_DataSize"].AsByteArray = data;
		byte[] array2 = new byte[m.Idx.Length * (flag ? 4 : 2)];
		for (int l = 0; l < m.Idx.Length; l++)
		{
			if (flag)
			{
				BitConverter.TryWriteBytes(array2.AsSpan(l * 4, 4), (uint)m.Idx[l]);
			}
			else
			{
				BitConverter.TryWriteBytes(array2.AsSpan(l * 2, 2), (ushort)m.Idx[l]);
			}
		}
		Bytes(mesh["m_IndexBuffer"]).AsByteArray = array2;
		mesh["m_IndexFormat"].AsInt = (flag ? 1 : 0);
		(Vector3 Min, Vector3 Max) tuple2 = m.Bounds();
		Vector3 item = tuple2.Min;
		Vector3 item2 = tuple2.Max;
		Vector3 c = (item + item2) / 2f;
		Vector3 e = (item2 - item) / 2f;
		AssetTypeValueField assetTypeValueField2 = mesh["m_SubMeshes"]["Array"];
		AssetTypeValueField assetTypeValueField3 = assetTypeValueField2.Children[0];
		assetTypeValueField2.Children.RemoveRange(1, assetTypeValueField2.Children.Count - 1);
		assetTypeValueField3["firstByte"].AsUInt = 0u;
		assetTypeValueField3["indexCount"].AsUInt = (uint)m.Idx.Length;
		assetTypeValueField3["topology"].AsInt = 0;
		assetTypeValueField3["baseVertex"].AsUInt = 0u;
		assetTypeValueField3["firstVertex"].AsUInt = 0u;
		assetTypeValueField3["vertexCount"].AsUInt = (uint)vertexCount;
		SetAabb(assetTypeValueField3["localAABB"], c, e);
		SetAabb(mesh["m_LocalAABB"], c, e);
		(Vector3, Vector3)[] array3 = m.BoneBounds();
		List<AssetTypeValueField> children2 = mesh["m_BonesAABB"]["Array"].Children;
		for (int n = 0; n < children2.Count && n < array3.Length; n++)
		{
			SetVec(children2[n]["m_Min"], array3[n].Item1);
			SetVec(children2[n]["m_Max"], array3[n].Item2);
		}
		mesh["m_MeshCompression"].AsInt = 0;
		ClearCompressed(mesh["m_CompressedMesh"]);
		AssetTypeValueField assetTypeValueField4 = mesh["m_Shapes"];
		string[] array4 = new string[4] { "vertices", "shapes", "channels", "fullWeights" };
		foreach (string name in array4)
		{
			if (!assetTypeValueField4[name].IsDummy && !assetTypeValueField4[name]["Array"].IsDummy)
			{
				assetTypeValueField4[name]["Array"].Children.Clear();
			}
		}
		array4 = new string[2] { "m_BakedConvexCollisionMesh", "m_BakedTriangleCollisionMesh" };
		foreach (string name2 in array4)
		{
			if (!mesh[name2].IsDummy)
			{
				Bytes(mesh[name2]).AsByteArray = Array.Empty<byte>();
			}
		}
		AssetTypeValueField assetTypeValueField5 = mesh["m_StreamData"];
		assetTypeValueField5["offset"].AsUInt = 0u;
		assetTypeValueField5["size"].AsUInt = 0u;
		assetTypeValueField5["path"].AsString = "";
		void F(int p, float v)
		{
			BitConverter.TryWriteBytes(data.AsSpan(p, 4), v);
		}
		void I(int p, int v)
		{
			BitConverter.TryWriteBytes(data.AsSpan(p, 4), v);
		}
	}

	private static void SetVec(AssetTypeValueField f, Vector3 v)
	{
		f["x"].AsFloat = v.X;
		f["y"].AsFloat = v.Y;
		f["z"].AsFloat = v.Z;
	}

	private static void SetAabb(AssetTypeValueField f, Vector3 c, Vector3 e)
	{
		SetVec(f["m_Center"], c);
		SetVec(f["m_Extent"], e);
	}

	private static void ClearCompressed(AssetTypeValueField cm)
	{
		string[] array = new string[11]
		{
			"m_Vertices", "m_UV", "m_Normals", "m_Tangents", "m_Weights", "m_NormalSigns", "m_TangentSigns", "m_FloatColors", "m_BoneIndices", "m_Triangles",
			"m_Colors"
		};
		foreach (string name in array)
		{
			AssetTypeValueField assetTypeValueField = cm[name];
			if (!assetTypeValueField.IsDummy)
			{
				assetTypeValueField["m_NumItems"].AsUInt = 0u;
				if (!assetTypeValueField["m_Range"].IsDummy)
				{
					assetTypeValueField["m_Range"].AsFloat = 0f;
				}
				if (!assetTypeValueField["m_Start"].IsDummy)
				{
					assetTypeValueField["m_Start"].AsFloat = 0f;
				}
				if (!assetTypeValueField["m_BitSize"].IsDummy)
				{
					assetTypeValueField["m_BitSize"].AsInt = 0;
				}
				Bytes(assetTypeValueField["m_Data"]).AsByteArray = Array.Empty<byte>();
			}
		}
		if (!cm["m_UVInfo"].IsDummy)
		{
			cm["m_UVInfo"].AsUInt = 0u;
		}
	}
}
