using System;
using System.Buffers.Binary;

namespace Drip.Core.RocketLeague;

public sealed class UpkProp
{
	public int NameIdx;

	public int NameNum;

	public int TypeIdx;

	public int TypeNum;

	public int ArrayIdx;

	public byte[] Extra = Array.Empty<byte>();

	public byte[] Payload = Array.Empty<byte>();

	public string Name = "";

	public string Type = "";

	public int Size => 24 + Extra.Length + Payload.Length;

	public int Int(int at = 0)
	{
		return BinaryPrimitives.ReadInt32LittleEndian(Payload.AsSpan(at, 4));
	}

	public void SetInt(int v)
	{
		Payload = new byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(Payload, v);
	}
}
