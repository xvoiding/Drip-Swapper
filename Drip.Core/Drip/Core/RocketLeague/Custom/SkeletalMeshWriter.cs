using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public static class SkeletalMeshWriter
{
	private sealed class W
	{
		private readonly MemoryStream _ms = new MemoryStream();

		public void Raw(ReadOnlySpan<byte> b)
		{
			_ms.Write(b);
		}

		public void I32(int v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(span, v);
			_ms.Write(span);
		}

		public void U16(int v)
		{
			Span<byte> span = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)v);
			_ms.Write(span);
		}

		public void U8(int v)
		{
			_ms.WriteByte((byte)v);
		}

		public void F32(float v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteSingleLittleEndian(span, v);
			_ms.Write(span);
		}

		public void F16(float v)
		{
			Span<byte> span = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16LittleEndian(span, BitConverter.HalfToUInt16Bits((Half)v));
			_ms.Write(span);
		}

		public byte[] ToArray()
		{
			return _ms.ToArray();
		}
	}

	public sealed class Layout
	{
		public int BoundsAt;

		public int LodStart;

		public int SectionsAt;

		public int IndexAt;

		public int IndexCountAt;

		public int UsedBonesAt;

		public int ChunksAt;

		public int SizeAt;

		public int RequiredAt;

		public int RequiredLen;

		public int BulkAt;

		public int VertHdrCountAt;

		public int VertexAt;

		public int VertexCount;

		public int AfterVerts;

		public int AdjAt;

		public int AdjCountAt;

		public int AdjEnd;

		public bool ColorStream;

		public int BoneCount;

		public int VertexStride;

		public int UvSets;

		public int[] MaterialRefs = Array.Empty<int>();
	}

	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	public static int PropertiesEnd(byte[] d, List<NameEntry> names, int start)
	{
		int num = start;
		for (int i = 0; i < 512; i++)
		{
			if (num + 8 > d.Length)
			{
				return -1;
			}
			string text = N(I(d, num));
			num += 8;
			if (text == "None")
			{
				return num;
			}
			if (num + 16 > d.Length)
			{
				return -1;
			}
			string text2 = N(I(d, num));
			num += 8;
			int num2 = I(d, num);
			num += 4;
			num += 4;
			switch (text2)
			{
			case "StructProperty":
			case "ByteProperty":
				num += 8;
				break;
			case "BoolProperty":
				num++;
				break;
			}
			if (num2 < 0 || num + num2 > d.Length)
			{
				return -1;
			}
			num += num2;
		}
		return -1;
		string N(int num3)
		{
			if (num3 < 0 || num3 >= names.Count)
			{
				return "?";
			}
			return names[num3].Text;
		}
	}

	public static Layout ReadLayout(byte[] d, List<NameEntry> names)
	{
		Layout layout = new Layout();
		int num = -1;
		int[] array = new int[2] { 4, 0 };
		foreach (int start in array)
		{
			num = PropertiesEnd(d, names, start);
			if (num > 0)
			{
				break;
			}
		}
		if (num < 0)
		{
			throw new UpkException("Couldn't read the car mesh's properties.");
		}
		layout.BoundsAt = num;
		int num2 = num + 28;
		int num3 = I(d, num2);
		num2 += 4;
		if (num3 < 0 || num3 > 64)
		{
			throw new UpkException("The car mesh doesn't look like the layout expected.");
		}
		layout.MaterialRefs = new int[num3];
		for (int j = 0; j < num3; j++)
		{
			layout.MaterialRefs[j] = I(d, num2 + 4 * j);
		}
		num2 += 4 * num3 + 24;
		int num4 = I(d, num2);
		num2 += 4;
		if (num4 < 1 || num4 > 512)
		{
			throw new UpkException("The car mesh doesn't look like the layout expected.");
		}
		layout.BoneCount = num4;
		num2 += 52 * num4;
		num2 += 4;
		int num5 = I(d, num2);
		num2 += 4;
		if (num5 != 1)
		{
			throw new UpkException("This car has more than one level of detail, which isn't supported yet.");
		}
		layout.LodStart = num2;
		layout.SectionsAt = num2;
		int num6 = I(d, num2);
		num2 += 4;
		if (num6 < 1 || num6 > 32)
		{
			throw new UpkException("The car mesh doesn't look like the layout expected.");
		}
		num2 = (layout.IndexAt = num2 + 13 * num6) + 4;
		num2++;
		int num7 = I(d, num2);
		num2 = (layout.IndexCountAt = num2 + 4);
		int num8 = I(d, num2);
		num2 += 4;
		if (num7 != 4 || num8 < 3 || num8 % 3 != 0)
		{
			throw new UpkException("The car mesh doesn't look like the layout expected.");
		}
		num2 = (layout.UsedBonesAt = num2 + num8 * 4);
		int num9 = I(d, num2);
		num2 = (layout.ChunksAt = num2 + (4 + 2 * num9));
		int num10 = I(d, num2);
		num2 += 4;
		if (num10 < 1 || num10 > 32)
		{
			throw new UpkException("The car mesh doesn't look like the layout expected.");
		}
		for (int k = 0; k < num10; k++)
		{
			num2 += 12;
			int num11 = I(d, num2);
			num2 += 4 + 2 * num11;
			num2 += 12;
		}
		layout.SizeAt = num2;
		num2 = (layout.RequiredAt = num2 + 8);
		int num12 = I(d, num2);
		num2 += 4;
		layout.RequiredLen = 4 + num12;
		num2 = (layout.BulkAt = num2 + num12) + 52;
		layout.VertexStride = I(d, num2);
		num2 = (layout.VertHdrCountAt = num2 + 4);
		layout.VertexCount = I(d, num2);
		num2 = (layout.VertexAt = num2 + 4);
		layout.UvSets = I(d, layout.BulkAt + 16);
		if (layout.UvSets < 1 || layout.UvSets > 4 || layout.VertexStride != 28 + 4 * layout.UvSets)
		{
			throw new UpkException($"The car's vertices aren't in the layout expected (stride {layout.VertexStride}, {layout.UvSets} UV sets).");
		}
		num2 = (layout.AfterVerts = num2 + layout.VertexStride * layout.VertexCount);
		if (num2 + 8 <= d.Length && I(d, num2) == 4 && I(d, num2 + 4) == layout.VertexCount && num2 + 8 + 4L * (long)layout.VertexCount <= d.Length)
		{
			layout.ColorStream = true;
			layout.AdjAt = num2;
			layout.AdjCountAt = num2 + 4;
			num2 += 8 + 4 * layout.VertexCount;
			layout.AdjEnd = num2;
			return layout;
		}
		num2 = (layout.AdjCountAt = (layout.AdjAt = num2 + 4) + 9);
		int num13 = I(d, num2);
		num2 += 4;
		if ((layout.AdjEnd = num2 + num13 * 4) > d.Length)
		{
			throw new UpkException("The car mesh runs past the end of its data.");
		}
		return layout;
	}

	private static byte Pack(float v)
	{
		return (byte)Math.Clamp((int)MathF.Round((v + 1f) * 127.5f), 0, 255);
	}

	public static byte[] Build(byte[] old, List<NameEntry> names, CarMesh mesh, int chassisBone = 1, int materialIndex = 0)
	{
		Layout layout = ReadLayout(old, names);
		if (mesh.VertexCount > 60000)
		{
			throw new UpkException($"That model has {mesh.VertexCount:N0} vertices, which is over what a car mesh can take (about 60,000). Decimate it a bit and try again.");
		}
		int vertexCount = mesh.VertexCount;
		int num = mesh.Idx.Length;
		Vector3[] array = new Vector3[vertexCount];
		Vector3[] array2 = new Vector3[vertexCount];
		int[] array3;
		for (int i = 0; i < num; i += 3)
		{
			int num2 = mesh.Idx[i];
			int num3 = mesh.Idx[i + 1];
			int num4 = mesh.Idx[i + 2];
			Vector3 vector = mesh.Pos[num3] - mesh.Pos[num2];
			Vector3 vector2 = mesh.Pos[num4] - mesh.Pos[num2];
			Vector2 vector3 = mesh.Uv[num3] - mesh.Uv[num2];
			Vector2 vector4 = mesh.Uv[num4] - mesh.Uv[num2];
			float num5 = vector3.X * vector4.Y - vector4.X * vector3.Y;
			if (!(MathF.Abs(num5) < 1E-12f))
			{
				float num6 = 1f / num5;
				Vector3 vector5 = (vector * vector4.Y - vector2 * vector3.Y) * num6;
				Vector3 vector6 = (vector2 * vector3.X - vector * vector4.X) * num6;
				array3 = new int[3] { num2, num3, num4 };
				foreach (int num7 in array3)
				{
					array[num7] += vector5;
					array2[num7] += vector6;
				}
			}
		}
		W w = new W();
		byte[] head = old.AsSpan(0, layout.LodStart).ToArray();
		(Vector3 Min, Vector3 Max) tuple = mesh.Bounds();
		Vector3 item = tuple.Min;
		Vector3 item2 = tuple.Max;
		Vector3 vector7 = new Vector3(BitConverter.ToSingle(head, layout.BoundsAt), BitConverter.ToSingle(head, layout.BoundsAt + 4), BitConverter.ToSingle(head, layout.BoundsAt + 8));
		Vector3 vector8 = new Vector3(BitConverter.ToSingle(head, layout.BoundsAt + 12), BitConverter.ToSingle(head, layout.BoundsAt + 16), BitConverter.ToSingle(head, layout.BoundsAt + 20));
		Vector3 vector9 = Vector3.Min(item, vector7 - vector8);
		Vector3 vector10 = Vector3.Max(item2, vector7 + vector8);
		Vector3 vector11 = (vector9 + vector10) / 2f;
		Vector3 vector12 = (vector10 - vector9) / 2f;
		PutF(layout.BoundsAt, vector11.X);
		PutF(layout.BoundsAt + 4, vector11.Y);
		PutF(layout.BoundsAt + 8, vector11.Z);
		PutF(layout.BoundsAt + 12, vector12.X);
		PutF(layout.BoundsAt + 16, vector12.Y);
		PutF(layout.BoundsAt + 20, vector12.Z);
		PutF(layout.BoundsAt + 24, vector12.Length());
		w.Raw(head);
		w.I32(1);
		w.U16(materialIndex);
		w.U16(0);
		w.I32(0);
		w.I32(num / 3);
		w.U8(0);
		w.Raw(old.AsSpan(layout.IndexAt, 4));
		w.U8(4);
		w.I32(4);
		w.I32(num);
		array3 = mesh.Idx;
		foreach (int v in array3)
		{
			w.I32(v);
		}
		w.I32(1);
		w.U16(chassisBone);
		w.I32(1);
		w.I32(0);
		w.I32(0);
		w.I32(0);
		w.I32(1);
		w.U16(chassisBone);
		w.I32(vertexCount);
		w.I32(0);
		w.I32(1);
		w.Raw(old.AsSpan(layout.SizeAt, 4));
		w.I32(vertexCount);
		w.Raw(old.AsSpan(layout.RequiredAt, layout.RequiredLen));
		w.Raw(old.AsSpan(layout.BulkAt, 52));
		w.I32(layout.VertexStride);
		w.I32(vertexCount);
		for (int k = 0; k < vertexCount; k++)
		{
			Vector3 vector13 = mesh.Nrm[k];
			Vector3 value = array[k] - vector13 * Vector3.Dot(vector13, array[k]);
			value = ((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : Vector3.Normalize((MathF.Abs(vector13.Z) < 0.9f) ? Vector3.Cross(vector13, Vector3.UnitZ) : Vector3.Cross(vector13, Vector3.UnitX)));
			float num8 = ((Vector3.Dot(Vector3.Cross(vector13, value), array2[k]) < 0f) ? (-1f) : 1f);
			w.U8(Pack(value.X));
			w.U8(Pack(value.Y));
			w.U8(Pack(value.Z));
			w.U8(128);
			w.U8(Pack(vector13.X));
			w.U8(Pack(vector13.Y));
			w.U8(Pack(vector13.Z));
			w.U8((num8 > 0f) ? 255 : 0);
			w.U8(0);
			w.U8(0);
			w.U8(0);
			w.U8(0);
			w.U8(255);
			w.U8(0);
			w.U8(0);
			w.U8(0);
			w.F32(mesh.Pos[k].X);
			w.F32(mesh.Pos[k].Y);
			w.F32(mesh.Pos[k].Z);
			Vector2 vector14 = mesh.Uv[k];
			for (int l = 0; l < layout.UvSets; l++)
			{
				if (l < 2)
				{
					w.F16(vector14.X);
					w.F16(vector14.Y);
				}
				else
				{
					w.F16(0f);
					w.F16(0f);
				}
			}
		}
		if (layout.ColorStream)
		{
			w.I32(4);
			w.I32(vertexCount);
			for (int m = 0; m < vertexCount; m++)
			{
				w.I32(-1);
			}
		}
		else
		{
			w.Raw(old.AsSpan(layout.AfterVerts, 4));
			w.Raw(old.AsSpan(layout.AdjAt, 9));
			w.I32(num / 3 * 12);
			for (int n = 0; n < num; n += 3)
			{
				int num9 = mesh.Idx[n];
				int num10 = mesh.Idx[n + 1];
				int num11 = mesh.Idx[n + 2];
				array3 = new int[12]
				{
					num9, num10, num11, num9, num10, num10, num11, num11, num9, num9,
					num10, num11
				};
				foreach (int v2 in array3)
				{
					w.I32(v2);
				}
			}
		}
		w.Raw(old.AsSpan(layout.AdjEnd));
		return w.ToArray();
		void PutF(int at, float value2)
		{
			BinaryPrimitives.WriteSingleLittleEndian(head.AsSpan(at, 4), value2);
		}
	}

	public static byte[] Apply(byte[] img, CarMesh mesh, out (long Start, int OldLen, int NewLen) edit, bool unpainted = false)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		List<UpkExport> list = UpkTables.Exports(img, s);
		UpkExport upkExport = (from e in list
			where e.ClassName == "SkeletalMesh" && e.SerialSize > 10000
			orderby e.SerialSize descending
			select e).FirstOrDefault() ?? throw new UpkException("This package has no body mesh to replace.");
		byte[] array = img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray();
		Layout layout = ReadLayout(array, names);
		int materialIndex = (unpainted ? UnpaintedMaterialSlot(img, s, names, list, layout.MaterialRefs) : BodyMaterialSlot(img, s, names, list, layout.MaterialRefs));
		byte[] array2 = Build(array, names, mesh, 1, materialIndex);
		edit = (Start: upkExport.SerialOffset, OldLen: upkExport.SerialSize, NewLen: array2.Length);
		return UpkTables.ReplaceExport(img, s, upkExport, array2);
	}

	public static int BodyMaterialSlot(byte[] img, UpkSummary s, List<NameEntry> names, List<UpkExport> exports, int[] refs)
	{
		for (int i = 0; i < refs.Length; i++)
		{
			string text = MaterialName(img, s, names, exports, refs[i]);
			if (text.Contains("Body", StringComparison.OrdinalIgnoreCase) && !text.Contains("Chass", StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return (refs.Length >= 2) ? 1 : 0;
	}

	public static int UnpaintedMaterialSlot(byte[] img, UpkSummary s, List<NameEntry> names, List<UpkExport> exports, int[] refs)
	{
		for (int i = 0; i < refs.Length; i++)
		{
			if (refs[i] > 0 && MaterialName(img, s, names, exports, refs[i]).Contains("Chass", StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return BodyMaterialSlot(img, s, names, exports, refs);
	}

	public static int UnpaintedMaterialExport(byte[] img, UpkSummary s, List<NameEntry> names, List<UpkExport> exports, int[] refs)
	{
		int num = ((refs.Length != 0) ? refs[UnpaintedMaterialSlot(img, s, names, exports, refs)] : 0);
		if (num <= 0)
		{
			return 0;
		}
		return num;
	}

	public static int BodyMaterialExport(byte[] img, UpkSummary s, List<NameEntry> names, List<UpkExport> exports, int[] refs)
	{
		int num = ((refs.Length != 0) ? refs[BodyMaterialSlot(img, s, names, exports, refs)] : 0);
		if (num <= 0)
		{
			return 0;
		}
		return num;
	}

	private static string MaterialName(byte[] img, UpkSummary s, List<NameEntry> names, List<UpkExport> exports, int r)
	{
		if (r > 0)
		{
			if (r > exports.Count)
			{
				return "";
			}
			return exports[r - 1].Name;
		}
		if (r < 0)
		{
			int num = s.ImportOffset + (-r - 1) * 28;
			if (num + 28 > img.Length)
			{
				return "";
			}
			int num2 = I(img, num + 20);
			if (num2 < 0 || num2 >= names.Count)
			{
				return "";
			}
			return names[num2].Text;
		}
		return "";
	}
}
