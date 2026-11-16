using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FallGuys;

public sealed class FgDripFile
{
	private sealed class Manifest
	{
		public string Format { get; set; } = "dripFG";

		public int Version { get; set; } = 1;

		public bool Rigged { get; set; }

		public string Name { get; set; } = "";

		public string Author { get; set; } = "";

		public string Slot { get; set; } = "Bean";

		public string SwapsFor { get; set; } = "";

		public string Notes { get; set; } = "";

		public int Vertices { get; set; }

		public int Triangles { get; set; }

		public int TextureWidth { get; set; }

		public int TextureHeight { get; set; }
	}

	public const string Extension = ".dripFG";

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private const uint Magic = 827148102u;

	private const uint MagicRigged = 843925318u;

	public string Name { get; set; } = "Custom model";

	public string Author { get; set; } = "";

	public string Notes { get; set; } = "";

	public string Slot { get; set; } = "Bean";

	public FgFittedModel Model { get; set; } = new FgFittedModel();

	public int Vertices => Model.Pos.Length;

	public int Triangles => Model.Idx.Length / 3;

	public byte[] PreviewPng { get; set; } = Array.Empty<byte>();

	public static FgDripFile FromFitted(string name, FgFittedModel m, string author = "", string notes = "")
	{
		return new FgDripFile
		{
			Name = name,
			Author = author,
			Notes = notes,
			Model = m
		};
	}

	public void Save(string path)
	{
		string text = path + ".tmp";
		using (FileStream stream = File.Create(text))
		{
			using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
			Manifest value = new Manifest
			{
				Version = ((Model.SkinW == null) ? 1 : 2),
				Rigged = (Model.SkinW != null),
				Name = Name,
				Author = Author,
				Slot = Slot,
				Notes = Notes,
				Vertices = Vertices,
				Triangles = Triangles,
				SwapsFor = "Fall Guys: the bean (the plain character everyone plays as)",
				TextureWidth = Model.Width,
				TextureHeight = Model.Height
			};
			Put(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(value, Json));
			Put(zip, "mesh.bin", WriteMesh(Model));
			TexImage texImage = new TexImage
			{
				Width = Model.Width,
				Height = Model.Height,
				Rgba = Model.Rgba
			};
			Put(zip, "picture.png", texImage.ToPng());
		}
		File.Move(text, path, overwrite: true);
	}

	private static void Put(ZipArchive zip, string name, byte[] data)
	{
		using Stream stream = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open();
		stream.Write(data);
	}

	public static FgDripFile Load(string path)
	{
		return Read(File.ReadAllBytes(path));
	}

	public static FgDripFile Read(byte[] file)
	{
		try
		{
			ZipArchive zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
			try
			{
				Manifest manifest = JsonSerializer.Deserialize<Manifest>(Get("manifest.json", 1048576L), Json) ?? throw new InvalidDataException("That .dripFG file's manifest is empty.");
				if (!string.Equals(manifest.Format, "dripFG", StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("That isn't a .dripFG file.");
				}
				if (manifest.Version > 2)
				{
					throw new InvalidDataException("That .dripFG file was made by a newer Drip Swapper. Update the app and try again.");
				}
				FgDripFile fgDripFile = new FgDripFile
				{
					Name = Clean(manifest.Name, "Custom model"),
					Author = Clean(manifest.Author, ""),
					Notes = Clean(manifest.Notes, "")
				};
				string slot = manifest.Slot;
				fgDripFile.Slot = ((slot != null && slot.Length > 0) ? manifest.Slot : "Bean");
				FgFittedModel fgFittedModel = ReadMesh(Get("mesh.bin", 134217728L));
				TexImage texImage = TexImage.Load(Get("picture.png", 67108864L));
				fgFittedModel.Width = texImage.Width;
				fgFittedModel.Height = texImage.Height;
				fgFittedModel.Rgba = texImage.Rgba;
				fgDripFile.Model = fgFittedModel;
				return fgDripFile;
			}
			finally
			{
				if (zip != null)
				{
					((IDisposable)zip).Dispose();
				}
			}
			byte[] Get(string n, long max)
			{
				ZipArchiveEntry? zipArchiveEntry = zip.GetEntry(n) ?? throw new InvalidDataException("That .dripFG file is missing " + n + ".");
				if (zipArchiveEntry.Length > max)
				{
					throw new InvalidDataException(n + " in that .dripFG file is too big.");
				}
				using Stream stream = zipArchiveEntry.Open();
				MemoryStream memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}
		catch (InvalidDataException)
		{
			throw;
		}
		catch (JsonException)
		{
			throw new InvalidDataException("That .dripFG file's manifest is damaged.");
		}
		catch (Exception ex3) when ((ex3 is EndOfStreamException || ex3 is IOException || ex3 is ArgumentException) ? true : false)
		{
			throw new InvalidDataException("That file isn't a valid .dripFG.");
		}
	}

	private static string Clean(string? s, string def)
	{
		s = (s ?? "").Trim();
		if (s.Length == 0)
		{
			return def;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text = s;
		foreach (char c in text)
		{
			if (!char.IsControl(c))
			{
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length <= 80)
		{
			return stringBuilder.ToString();
		}
		return stringBuilder.ToString(0, 80);
	}

	public static byte[] WriteMesh(FgFittedModel m)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		bool flag = m.SkinW != null && m.SkinB != null && m.SkinW.Length == m.Pos.Length * 4 && m.SkinB.Length == m.Pos.Length * 4;
		binaryWriter.Write(flag ? 843925318u : 827148102u);
		binaryWriter.Write(m.Pos.Length);
		binaryWriter.Write(m.Idx.Length);
		Vector3[] pos = m.Pos;
		for (int i = 0; i < pos.Length; i++)
		{
			Vector3 vector = pos[i];
			binaryWriter.Write(vector.X);
			binaryWriter.Write(vector.Y);
			binaryWriter.Write(vector.Z);
		}
		pos = ((m.Nrm.Length == m.Pos.Length) ? m.Nrm : new Vector3[m.Pos.Length]);
		for (int i = 0; i < pos.Length; i++)
		{
			Vector3 vector2 = pos[i];
			binaryWriter.Write(vector2.X);
			binaryWriter.Write(vector2.Y);
			binaryWriter.Write(vector2.Z);
		}
		Vector2[] uv = m.Uv;
		for (int i = 0; i < uv.Length; i++)
		{
			Vector2 vector3 = uv[i];
			binaryWriter.Write(vector3.X);
			binaryWriter.Write(vector3.Y);
		}
		int[] idx = m.Idx;
		foreach (int value in idx)
		{
			binaryWriter.Write(value);
		}
		if (flag)
		{
			float[] skinW = m.SkinW;
			foreach (float value2 in skinW)
			{
				binaryWriter.Write(value2);
			}
			idx = m.SkinB;
			foreach (int num in idx)
			{
				binaryWriter.Write((byte)num);
			}
		}
		return memoryStream.ToArray();
	}

	public static FgFittedModel ReadMesh(byte[] data)
	{
		using BinaryReader binaryReader = new BinaryReader(new MemoryStream(data));
		uint num = binaryReader.ReadUInt32();
		if (num != 827148102 && num != 843925318)
		{
			throw new InvalidDataException("The model data in that .dripFG file isn't readable.");
		}
		int num2 = binaryReader.ReadInt32();
		int num3 = binaryReader.ReadInt32();
		if (num2 <= 0 || num2 > 2000000 || num3 <= 0 || num3 > 12000000 || num3 % 3 != 0 || data.Length < 12 + num2 * 32 + num3 * 4)
		{
			throw new InvalidDataException("The model in that .dripFG file is damaged.");
		}
		FgFittedModel fgFittedModel = new FgFittedModel
		{
			Pos = new Vector3[num2],
			Nrm = new Vector3[num2],
			Uv = new Vector2[num2],
			Idx = new int[num3]
		};
		for (int i = 0; i < num2; i++)
		{
			fgFittedModel.Pos[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int j = 0; j < num2; j++)
		{
			fgFittedModel.Nrm[j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int k = 0; k < num2; k++)
		{
			fgFittedModel.Uv[k] = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int l = 0; l < num3; l++)
		{
			int num4 = binaryReader.ReadInt32();
			if (num4 < 0 || num4 >= num2)
			{
				throw new InvalidDataException("The model in that .dripFG file is damaged.");
			}
			fgFittedModel.Idx[l] = num4;
		}
		if (num == 843925318)
		{
			if (data.Length < 12 + num2 * 32 + num3 * 4 + num2 * 20)
			{
				throw new InvalidDataException("The model in that .dripFG file is damaged.");
			}
			fgFittedModel.SkinW = new float[num2 * 4];
			fgFittedModel.SkinB = new int[num2 * 4];
			for (int m = 0; m < num2 * 4; m++)
			{
				fgFittedModel.SkinW[m] = binaryReader.ReadSingle();
			}
			for (int n = 0; n < num2 * 4; n++)
			{
				fgFittedModel.SkinB[n] = binaryReader.ReadByte();
			}
		}
		return fgFittedModel;
	}
}
