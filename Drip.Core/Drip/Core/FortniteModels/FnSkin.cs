using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Drip.Core.FortniteModels;

public sealed class FnSkin
{
	private const uint Magic = 1397638724u;

	private const int Version = 1;

	public const int MaxBones = 2000;

	public const int MaxInfluences = 8;

	public string[] BoneNames = Array.Empty<string>();

	public int[] Parent = Array.Empty<int>();

	public int K;

	public ushort[] Bone = Array.Empty<ushort>();

	public byte[] Weight = Array.Empty<byte>();

	public int VertexCount
	{
		get
		{
			if (K != 0)
			{
				return Bone.Length / K;
			}
			return 0;
		}
	}

	public FnSkin Subset(IReadOnlyList<int> keep)
	{
		FnSkin fnSkin = new FnSkin
		{
			BoneNames = BoneNames,
			Parent = Parent,
			K = K,
			Bone = new ushort[keep.Count * K],
			Weight = new byte[keep.Count * K]
		};
		for (int i = 0; i < keep.Count; i++)
		{
			Array.Copy(Bone, keep[i] * K, fnSkin.Bone, i * K, K);
			Array.Copy(Weight, keep[i] * K, fnSkin.Weight, i * K, K);
		}
		return fnSkin;
	}

	public byte[] ToBytes()
	{
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
		binaryWriter.Write(1397638724u);
		binaryWriter.Write(1);
		binaryWriter.Write(BoneNames.Length);
		binaryWriter.Write(VertexCount);
		binaryWriter.Write(K);
		for (int i = 0; i < BoneNames.Length; i++)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(BoneNames[i]);
			binaryWriter.Write((ushort)bytes.Length);
			binaryWriter.Write(bytes);
			binaryWriter.Write(Parent[i]);
		}
		ushort[] bone = Bone;
		foreach (ushort value in bone)
		{
			binaryWriter.Write(value);
		}
		binaryWriter.Write(Weight);
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	public static FnSkin FromBytes(byte[] b)
	{
		try
		{
			using BinaryReader binaryReader = new BinaryReader(new MemoryStream(b), Encoding.UTF8);
			if (binaryReader.ReadUInt32() != 1397638724 || binaryReader.ReadInt32() != 1)
			{
				throw new InvalidDataException("The model's rig file isn't in a format this version understands.");
			}
			int num = binaryReader.ReadInt32();
			int num2 = binaryReader.ReadInt32();
			int num3 = binaryReader.ReadInt32();
			if (num < 1 || num > 2000 || num2 < 1 || num2 > 400000 || num3 < 1 || num3 > 8)
			{
				throw new InvalidDataException("The model's rig file has an unreasonable size.");
			}
			FnSkin fnSkin = new FnSkin
			{
				BoneNames = new string[num],
				Parent = new int[num],
				K = num3,
				Bone = new ushort[num2 * num3],
				Weight = new byte[num2 * num3]
			};
			for (int i = 0; i < num; i++)
			{
				int count = binaryReader.ReadUInt16();
				fnSkin.BoneNames[i] = Encoding.UTF8.GetString(binaryReader.ReadBytes(count));
				fnSkin.Parent[i] = binaryReader.ReadInt32();
				if (fnSkin.Parent[i] < -1 || fnSkin.Parent[i] >= i)
				{
					throw new InvalidDataException("The model's rig file has a bone hierarchy that doesn't make sense.");
				}
			}
			for (int j = 0; j < fnSkin.Bone.Length; j++)
			{
				fnSkin.Bone[j] = binaryReader.ReadUInt16();
				if (fnSkin.Bone[j] >= num)
				{
					throw new InvalidDataException("The model's rig file points at a bone that doesn't exist.");
				}
			}
			byte[] array = binaryReader.ReadBytes(fnSkin.Weight.Length);
			if (array.Length != fnSkin.Weight.Length || binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
			{
				throw new InvalidDataException("The model's rig file is damaged (its size doesn't match what it says it holds).");
			}
			Array.Copy(array, fnSkin.Weight, array.Length);
			return fnSkin;
		}
		catch (EndOfStreamException)
		{
			throw new InvalidDataException("The model's rig file is cut short.");
		}
	}
}
