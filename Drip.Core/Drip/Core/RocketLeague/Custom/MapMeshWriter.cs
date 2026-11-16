using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public static class MapMeshWriter
{
	public const int MaxVertices = 65535;

	private static int I(byte[] d, int p)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p, 4));
	}

	private static byte Pack(float v)
	{
		return (byte)Math.Clamp((int)MathF.Round((v + 1f) * 127.5f), 0, 255);
	}

	public static CarMesh CollisionWelded(CarMesh collision)
	{
		var (array, idx) = MapGeometry.WeldPositions(collision, 0.0625f);
		return new CarMesh
		{
			Pos = array,
			Nrm = array.Select((Vector3 _) => Vector3.UnitZ).ToArray(),
			Uv = new Vector2[array.Length],
			Idx = idx
		};
	}

	public static CarMesh CollisionCarrier(CarMesh collision)
	{
		float[] array = new float[5] { 0.0625f, 0.5f, 2f, 8f, 32f };
		foreach (float tolerance in array)
		{
			var (array2, array3) = MapGeometry.WeldPositions(collision, tolerance);
			if (array3.Length != 0 && array2.Length <= 65535)
			{
				return new CarMesh
				{
					Pos = array2,
					Nrm = array2.Select((Vector3 _) => Vector3.UnitZ).ToArray(),
					Uv = new Vector2[array2.Length],
					Idx = array3
				};
			}
		}
		throw new UpkException("The map's collision shape is too detailed for the game (over 65,535 corner points). Use the simple box collision, or a lighter collision mesh.");
	}

	public static byte[] Build(byte[] old, List<NameEntry> names, CarMesh mesh, bool solid)
	{
		StaticMeshWriter.Layout layout = StaticMeshWriter.ReadLayout(old, names);
		int vertexCount = mesh.VertexCount;
		int num = mesh.Idx.Length;
		if (vertexCount == 0 || num == 0)
		{
			throw new UpkException("That piece of the map has no triangles.");
		}
		if (vertexCount > 65535)
		{
			throw new UpkException($"That piece of the map has {vertexCount:N0} vertices, which is over what the game takes (65,535).");
		}
		Vector3[] pos = mesh.Pos;
		Vector3[] nrm = mesh.Nrm;
		Vector2[] uv = mesh.Uv;
		Vector3[] array = new Vector3[vertexCount];
		for (int i = 0; i < num; i += 3)
		{
			int num2 = mesh.Idx[i];
			int num3 = mesh.Idx[i + 1];
			int num4 = mesh.Idx[i + 2];
			Vector3 vector = pos[num3] - pos[num2];
			Vector3 vector2 = pos[num4] - pos[num2];
			Vector2 vector3 = uv[num3] - uv[num2];
			Vector2 vector4 = uv[num4] - uv[num2];
			float num5 = vector3.X * vector4.Y - vector4.X * vector3.Y;
			if (!(MathF.Abs(num5) < 1E-12f))
			{
				Vector3 vector5 = (vector * vector4.Y - vector2 * vector3.Y) / num5;
				array[num2] += vector5;
				array[num3] += vector5;
				array[num4] += vector5;
			}
		}
		Vector3 vector6 = new Vector3(float.MaxValue);
		Vector3 vector7 = new Vector3(float.MinValue);
		Vector3[] array2 = pos;
		foreach (Vector3 value in array2)
		{
			vector6 = Vector3.Min(vector6, value);
			vector7 = Vector3.Max(vector7, value);
		}
		Vector3 vector8 = (vector6 + vector7) / 2f;
		Vector3 vector9 = (vector7 - vector6) / 2f;
		float num6 = 0f;
		array2 = pos;
		foreach (Vector3 vector10 in array2)
		{
			num6 = MathF.Max(num6, (vector10 - vector8).Length());
		}
		byte[] head = old.AsSpan(0, layout.Props + 28 + 4 + 24).ToArray();
		PutF(layout.Props, vector8.X);
		PutF(layout.Props + 4, vector8.Y);
		PutF(layout.Props + 8, vector8.Z);
		PutF(layout.Props + 12, vector9.X);
		PutF(layout.Props + 16, vector9.Y);
		PutF(layout.Props + 20, vector9.Z);
		PutF(layout.Props + 24, num6);
		if (solid)
		{
			PutF(layout.Props + 32, vector6.X);
			PutF(layout.Props + 36, vector6.Y);
			PutF(layout.Props + 40, vector6.Z);
			PutF(layout.Props + 44, vector7.X);
			PutF(layout.Props + 48, vector7.Y);
			PutF(layout.Props + 52, vector7.Z);
		}
		else
		{
			for (int k = 0; k < 3; k++)
			{
				PutF(layout.Props + 32 + 4 * k, float.MaxValue);
				PutF(layout.Props + 44 + 4 * k, float.MinValue);
			}
		}
		byte[] render = old.AsSpan(layout.Render, layout.PosAt - layout.Render).ToArray();
		PutI(44, solid ? 1 : 0);
		PutI(48, solid ? 1 : 0);
		PutI(56, 0);
		PutI(60, num / 3);
		PutI(64, 0);
		PutI(68, vertexCount - 1);
		PutI(84, num / 3);
		MemoryStream ms = new MemoryStream();
		try
		{
			ms.Write(head);
			if (solid)
			{
				W(6);
				W(1);
				ms.Write(new byte[6]);
				W(8);
				W(num / 3);
				for (int l = 0; l < num / 3; l++)
				{
					W16(mesh.Idx[3 * l]);
					W16(mesh.Idx[3 * l + 1]);
					W16(mesh.Idx[3 * l + 2]);
					W16(0);
				}
			}
			else
			{
				W(6);
				W(0);
				W(8);
				W(0);
			}
			ms.Write(render);
			W(12);
			W(vertexCount);
			W(12);
			W(vertexCount);
			array2 = pos;
			for (int j = 0; j < array2.Length; j++)
			{
				Vector3 vector11 = array2[j];
				WF(vector11.X);
				WF(vector11.Y);
				WF(vector11.Z);
			}
			W(layout.UvSets);
			W(layout.TexStride);
			W(vertexCount);
			W(0);
			W(layout.TexStride);
			W(vertexCount);
			for (int m = 0; m < vertexCount; m++)
			{
				Vector3 vector12 = ((nrm[m].LengthSquared() > 1E-12f) ? Vector3.Normalize(nrm[m]) : Vector3.UnitZ);
				Vector3 value2 = array[m] - vector12 * Vector3.Dot(vector12, array[m]);
				value2 = ((value2.LengthSquared() > 1E-12f) ? Vector3.Normalize(value2) : Vector3.Normalize((MathF.Abs(vector12.Z) < 0.9f) ? Vector3.Cross(vector12, Vector3.UnitZ) : Vector3.Cross(vector12, Vector3.UnitX)));
				ms.WriteByte(Pack(value2.X));
				ms.WriteByte(Pack(value2.Y));
				ms.WriteByte(Pack(value2.Z));
				ms.WriteByte(128);
				ms.WriteByte(Pack(vector12.X));
				ms.WriteByte(Pack(vector12.Y));
				ms.WriteByte(Pack(vector12.Z));
				ms.WriteByte(byte.MaxValue);
				for (int n = 0; n < layout.UvSets; n++)
				{
					WH(uv[m].X);
					WH(uv[m].Y);
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
				for (int num7 = 0; num7 < vertexCount; num7++)
				{
					W(-1);
				}
			}
			W(vertexCount);
			W(2);
			W(num);
			int[] idx = mesh.Idx;
			for (int j = 0; j < idx.Length; j++)
			{
				W16(idx[j]);
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
			BinaryPrimitives.WriteInt32LittleEndian(render.AsSpan(at, 4), v);
		}
		void W(int v)
		{
			Span<byte> span = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(span, v);
			ms.Write(span);
		}
		void W16(int v)
		{
			ms.WriteByte((byte)(v & 0xFF));
			ms.WriteByte((byte)(v >> 8));
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
}
