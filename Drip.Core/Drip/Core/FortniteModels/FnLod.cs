namespace Drip.Core.FortniteModels;

public sealed class FnLod
{
	public int Start;

	public int VertexCount;

	public int IndexOffset;

	public int IndexSize;

	public int IndexCount;

	public int PosOffset;

	public int TanOffset;

	public int UvOffset;

	public int UvStride;

	public int NumTexCoords;

	public int SkinOffset;

	public int SkinStride;

	public int Influences;

	public int End;

	public int ColorOffset = -1;

	public int TriangleCount => IndexCount / 3;
}
