using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Drip.Core.RocketLeague.Custom;

public static class ReferenceCar
{
	public const string OctaneFile = "Body_Octane_SF.upk";

	public static CarMesh FromImage(byte[] img)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		UpkExport upkExport = (from e in UpkTables.Exports(img, s)
			where e.ClassName == "SkeletalMesh" && e.SerialSize > 10000
			orderby e.SerialSize descending
			select e).FirstOrDefault() ?? throw new UpkException("No body mesh in that package.");
		byte[] array = img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray();
		SkeletalMeshWriter.Layout layout = SkeletalMeshWriter.ReadLayout(array, names);
		int vertexCount = layout.VertexCount;
		Vector3[] array2 = new Vector3[vertexCount];
		for (int num = 0; num < vertexCount; num++)
		{
			int num2 = layout.VertexAt + num * layout.VertexStride + 16;
			array2[num] = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2, 4)), BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2 + 4, 4)), BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2 + 8, 4)));
		}
		int num3 = BinaryPrimitives.ReadInt32LittleEndian(array.AsSpan(layout.IndexCountAt, 4));
		int[] array3 = new int[num3];
		for (int num4 = 0; num4 < num3; num4++)
		{
			int value = BinaryPrimitives.ReadInt32LittleEndian(array.AsSpan(layout.IndexCountAt + 4 + num4 * 4, 4));
			array3[num4] = Math.Clamp(value, 0, vertexCount - 1);
		}
		return new CarMesh
		{
			Pos = array2,
			Nrm = new Vector3[vertexCount],
			Uv = new Vector2[vertexCount],
			Idx = array3
		};
	}

	public static CarMesh StaticFromImage(byte[] img, string meshName)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> names = UpkNames.Read(img, s, out var _);
		UpkExport upkExport = UpkTables.Exports(img, s).FirstOrDefault((UpkExport e) => e.ClassName == "StaticMesh" && e.Name.Equals(meshName, StringComparison.OrdinalIgnoreCase) && e.SerialSize > 5000) ?? throw new UpkException("No mesh called " + meshName + " in that package.");
		byte[] array = img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray();
		StaticMeshWriter.Layout layout = StaticMeshWriter.ReadLayout(array, names);
		int oldVertices = layout.OldVertices;
		Vector3[] array2 = new Vector3[oldVertices];
		for (int num = 0; num < oldVertices; num++)
		{
			int num2 = layout.PosDataAt + num * 12;
			array2[num] = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2, 4)), BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2 + 4, 4)), BinaryPrimitives.ReadSingleLittleEndian(array.AsSpan(num2 + 8, 4)));
		}
		int num3 = layout.OldIndices - layout.OldIndices % 3;
		int[] array3 = new int[num3];
		for (int num4 = 0; num4 < num3; num4++)
		{
			array3[num4] = Math.Clamp(BinaryPrimitives.ReadUInt16LittleEndian(array.AsSpan(layout.IndexAt + 8 + num4 * 2, 2)), 0, oldVertices - 1);
		}
		return new CarMesh
		{
			Pos = array2,
			Nrm = new Vector3[oldVertices],
			Uv = new Vector2[oldVertices],
			Idx = array3
		};
	}
}
