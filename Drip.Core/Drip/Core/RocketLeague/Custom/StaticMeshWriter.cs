using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public static class StaticMeshWriter
{
	public sealed class Layout
	{
		public int Props;

		public int Render;

		public int PosAt;

		public int PosDataAt;

		public int TexAt;

		public int TexDataAt;

		public int UvSets;

		public int TexStride;

		public int ColorAt;

		public int ColorDataAt;

		public int ColorCount;

		public int CountAt;

		public int IndexAt;

		public int TailAt;

		public int OldVertices;

		public int OldIndices;

		public uint FirstColor = uint.MaxValue;

		public bool ColorEmpty;
	}

	public const uint RimColor = 4278190080u;

	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	public static Layout ReadLayout(byte[] d, List<NameEntry> names)
	{
		Layout layout = new Layout();
		layout.Props = Texture2DWriter.PropsEnd(d, names);
		int num = layout.Props + 28 + 4 + 24;
		int num2 = I(d, num);
		int num3 = I(d, num + 4);
		num += 8 + num2 * num3;
		num2 = I(d, num);
		num3 = I(d, num + 4);
		num = (layout.Render = num + (8 + num2 * num3));
		if (num + 100 > d.Length)
		{
			throw new UpkException("That wheel's mesh data is shorter than expected.");
		}
		if (I(d, num + 20) != 1 || I(d, num + 36) != 1 || I(d, num + 76) != 1)
		{
			throw new UpkException("That wheel's mesh has a layout the app doesn't know yet (expected one detail level with one section).");
		}
		num = (layout.PosAt = num + 89);
		int num4 = I(d, num);
		int num5 = I(d, num + 4);
		if (num4 != 12 || num5 <= 0 || num5 > 70000 || I(d, num + 8) != 12 || I(d, num + 12) != num5)
		{
			throw new UpkException("That wheel's position buffer isn't laid out the way the app expects.");
		}
		layout.OldVertices = num5;
		layout.PosDataAt = num + 16;
		num = (layout.TexAt = layout.PosDataAt + 12 * num5);
		layout.UvSets = I(d, num);
		layout.TexStride = I(d, num + 4);
		if (layout.UvSets < 1 || layout.UvSets > 4 || layout.TexStride != 8 + 4 * layout.UvSets || I(d, num + 8) != num5 || I(d, num + 16) != layout.TexStride || I(d, num + 20) != num5)
		{
			throw new UpkException("That wheel's vertex buffer isn't laid out the way the app expects.");
		}
		layout.TexDataAt = num + 24;
		num = (layout.ColorAt = layout.TexDataAt + layout.TexStride * num5);
		if (I(d, num) == 0 && I(d, num + 4) == 0)
		{
			layout.ColorEmpty = true;
			layout.ColorDataAt = num + 8;
			layout.ColorCount = 0;
			num += 8;
		}
		else
		{
			if (I(d, num) != 4 || I(d, num + 4) != num5 || I(d, num + 8) != 4 || I(d, num + 12) != num5)
			{
				throw new UpkException("That wheel's colour buffer isn't laid out the way the app expects.");
			}
			layout.ColorDataAt = num + 16;
			layout.ColorCount = num5;
			layout.FirstColor = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(layout.ColorDataAt, 4));
			num = layout.ColorDataAt + 4 * num5;
		}
		layout.CountAt = num;
		if (I(d, num) != num5)
		{
			throw new UpkException("That wheel's vertex count doesn't line up.");
		}
		layout.IndexAt = num + 4;
		if (I(d, layout.IndexAt) != 2)
		{
			throw new UpkException("That wheel's index buffer isn't 16-bit.");
		}
		layout.OldIndices = I(d, layout.IndexAt + 4);
		layout.TailAt = layout.IndexAt + 8 + 2 * layout.OldIndices;
		if (layout.OldIndices <= 0 || layout.TailAt > d.Length)
		{
			throw new UpkException("That wheel's index buffer runs past the end of its data.");
		}
		return layout;
	}

	private static byte Pack(float v)
	{
		return (byte)Math.Clamp((int)MathF.Round((v + 1f) * 127.5f), 0, 255);
	}

	public static byte[] Build(byte[] old, List<NameEntry> names, CarMesh mesh)
	{
		Layout layout = ReadLayout(old, names);
		int vertexCount = mesh.VertexCount;
		int num = mesh.Idx.Length;
		if (vertexCount > 65535)
		{
			throw new UpkException($"That model has {vertexCount:N0} vertices, which is over what a ball or wheel can take (65,535). Decimate it a bit and try again.");
		}
		if (vertexCount == 0 || num == 0)
		{
			throw new UpkException("That model has no triangles.");
		}
		Vector3[] array = new Vector3[vertexCount];
		Vector3[] array2 = new Vector3[vertexCount];
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
				int[] array3 = new int[3] { num2, num3, num4 };
				foreach (int num7 in array3)
				{
					array[num7] += vector5;
					array2[num7] += vector6;
				}
			}
		}
		byte[] head = old.AsSpan(0, layout.PosAt).ToArray();
		(Vector3 Min, Vector3 Max) tuple = mesh.Bounds();
		Vector3 item = tuple.Min;
		Vector3 item2 = tuple.Max;
		Vector3 vector7 = (item + item2) / 2f;
		Vector3 vector8 = (item2 - item) / 2f;
		float num8 = 0f;
		Vector3[] pos = mesh.Pos;
		foreach (Vector3 vector9 in pos)
		{
			num8 = MathF.Max(num8, (vector9 - vector7).Length());
		}
		PutF(layout.Props, vector7.X);
		PutF(layout.Props + 4, vector7.Y);
		PutF(layout.Props + 8, vector7.Z);
		PutF(layout.Props + 12, vector8.X);
		PutF(layout.Props + 16, vector8.Y);
		PutF(layout.Props + 20, vector8.Z);
		PutF(layout.Props + 24, num8);
		PutF(layout.Props + 32, item.X);
		PutF(layout.Props + 36, item.Y);
		PutF(layout.Props + 40, item.Z);
		PutF(layout.Props + 44, item2.X);
		PutF(layout.Props + 48, item2.Y);
		PutF(layout.Props + 52, item2.Z);
		PutI(layout.Render + 44, 0);
		PutI(layout.Render + 48, 0);
		PutI(layout.Render + 56, 0);
		PutI(layout.Render + 60, num / 3);
		PutI(layout.Render + 64, 0);
		PutI(layout.Render + 68, vertexCount - 1);
		PutI(layout.Render + 84, num / 3);
		MemoryStream ms = new MemoryStream();
		try
		{
			ms.Write(head);
			W(12);
			W(vertexCount);
			W(12);
			W(vertexCount);
			pos = mesh.Pos;
			for (int j = 0; j < pos.Length; j++)
			{
				Vector3 vector10 = pos[j];
				WF(vector10.X);
				WF(vector10.Y);
				WF(vector10.Z);
			}
			W(layout.UvSets);
			W(layout.TexStride);
			W(vertexCount);
			W(0);
			W(layout.TexStride);
			W(vertexCount);
			for (int k = 0; k < vertexCount; k++)
			{
				Vector3 vector11 = mesh.Nrm[k];
				Vector3 value = array[k] - vector11 * Vector3.Dot(vector11, array[k]);
				value = ((value.LengthSquared() > 1E-12f) ? Vector3.Normalize(value) : Vector3.Normalize((MathF.Abs(vector11.Z) < 0.9f) ? Vector3.Cross(vector11, Vector3.UnitZ) : Vector3.Cross(vector11, Vector3.UnitX)));
				ms.WriteByte(Pack(value.X));
				ms.WriteByte(Pack(value.Y));
				ms.WriteByte(Pack(value.Z));
				ms.WriteByte(128);
				ms.WriteByte(Pack(vector11.X));
				ms.WriteByte(Pack(vector11.Y));
				ms.WriteByte(Pack(vector11.Z));
				ms.WriteByte(byte.MaxValue);
				for (int l = 0; l < layout.UvSets; l++)
				{
					if (l == 0)
					{
						WH(mesh.Uv[k].X);
						WH(mesh.Uv[k].Y);
					}
					else
					{
						WH(mesh.Uv[k].X);
						WH(mesh.Uv[k].Y);
					}
				}
			}
			if (layout.ColorEmpty)
			{
				W(0);
				W(0);
			}
			else
			{
				W(4);
				W(vertexCount);
				W(4);
				W(vertexCount);
				for (int m = 0; m < vertexCount; m++)
				{
					W(-16777216);
				}
			}
			W(vertexCount);
			W(2);
			W(num);
			int[] array3 = mesh.Idx;
			foreach (int num9 in array3)
			{
				ms.WriteByte((byte)(num9 & 0xFF));
				ms.WriteByte((byte)(num9 >> 8));
			}
			ms.Write(old.AsSpan(layout.TailAt));
			return ms.ToArray();
		}
		finally
		{
			if (ms != null)
			{
				((IDisposable)ms).Dispose();
			}
		}
		void PutF(int at, float v)
		{
			BinaryPrimitives.WriteSingleLittleEndian(head.AsSpan(at, 4), v);
		}
		void PutI(int at, int v)
		{
			BinaryPrimitives.WriteInt32LittleEndian(head.AsSpan(at, 4), v);
		}
		void W(int v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(span, v);
			ms.Write(span);
		}
		void WF(float v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteSingleLittleEndian(span, v);
			ms.Write(span);
		}
		void WH(float v)
		{
			Span<byte> span = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16LittleEndian(span, BitConverter.HalfToUInt16Bits((Half)v));
			ms.Write(span);
		}
	}

	public static byte[] Apply(byte[] img, CarMesh mesh, out (long Start, int OldLen, int NewLen) edit, string? meshName = null)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		IEnumerable<UpkExport> source = from e in UpkTables.Exports(img, s)
			where e.ClassName == "StaticMesh" && e.SerialSize > 5000
			select e;
		UpkExport upkExport = ((meshName != null) ? source.FirstOrDefault((UpkExport e) => e.Name.Equals(meshName, StringComparison.OrdinalIgnoreCase)) : null) ?? source.OrderByDescending((UpkExport e) => e.SerialSize).FirstOrDefault() ?? throw new UpkException("This package has no mesh to replace.");
		byte[] array = Build(img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names, mesh);
		edit = (Start: upkExport.SerialOffset, OldLen: upkExport.SerialSize, NewLen: array.Length);
		return UpkTables.ReplaceExport(img, s, upkExport, array);
	}
}
