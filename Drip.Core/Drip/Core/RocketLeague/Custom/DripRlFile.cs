using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Drip.Core.RocketLeague.Custom;

public sealed class DripRlFile
{
	private sealed class Manifest
	{
		public string Format { get; set; } = "dripRL";

		public int Version { get; set; } = 1;

		public string? Collision { get; set; }

		public string Name { get; set; } = "";

		public string Author { get; set; } = "";

		public string Slot { get; set; } = "Body";

		public string SwapsFor { get; set; } = "";

		public string Base { get; set; } = "";

		public string Mesh { get; set; } = "mesh.bin";

		public string? Picture { get; set; }

		public int Vertices { get; set; }

		public int Triangles { get; set; }

		public string Notes { get; set; } = "";

		public bool OwnWheels { get; set; }
	}

	public const string Extension = ".dripRL";

	public const int MaxMapPieces = 80;

	public string Name { get; set; } = "Custom model";

	public string Author { get; set; } = "";

	public string Slot { get; set; } = "Body";

	public string Base { get; set; } = "Body_Import";

	public CarMesh Mesh { get; set; } = new CarMesh();

	public CarMesh Collision { get; set; } = new CarMesh();

	public byte[] Picture { get; set; } = Array.Empty<byte>();

	public string PictureName { get; set; } = "";

	public string Notes { get; set; } = "";

	public bool OwnWheels { get; set; }

	public static string DescribeSlot(string slot)
	{
		return slot switch
		{
			"Body" => "Rocket League cars (replaces whichever car you pick)", 
			"Ball" => "Rocket League balls (replaces whichever ball you pick)", 
			"Wheels" => "Rocket League wheels (replaces whichever wheels you pick)", 
			"Map" => "Rocket League's Underpass (Labs) map (offline play only)", 
			_ => "Rocket League " + slot.ToLowerInvariant() + " items", 
		};
	}

	public void Save(string path)
	{
		string text = path + ".tmp";
		using (FileStream stream = File.Create(text))
		{
			using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
			object obj;
			if (Picture.Length == 0)
			{
				obj = null;
			}
			else
			{
				string extension = Path.GetExtension(PictureName);
				obj = "picture" + ((extension != null && extension.Length > 0) ? extension.ToLowerInvariant() : ".png");
			}
			string text2 = (string)obj;
			bool flag = Collision.VertexCount >= 3;
			Manifest value = new Manifest
			{
				Version = ((!(Slot == "Map")) ? 1 : 2),
				Collision = (flag ? "collision.bin" : null),
				Name = Name,
				Author = Author,
				Slot = Slot,
				SwapsFor = DescribeSlot(Slot),
				Base = Base,
				Picture = text2,
				Vertices = Mesh.VertexCount,
				Triangles = Mesh.TriangleCount,
				Notes = Notes,
				OwnWheels = OwnWheels
			};
			Put(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			}));
			Put(zip, "mesh.bin", WriteMesh(Mesh));
			if (flag)
			{
				Put(zip, "collision.bin", WriteMesh(Collision));
			}
			if (text2 != null)
			{
				Put(zip, text2, Picture);
			}
		}
		File.Move(text, path, overwrite: true);
	}

	private static void Put(ZipArchive zip, string name, byte[] data)
	{
		using Stream stream = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open();
		stream.Write(data);
	}

	public static DripRlFile Load(string path)
	{
		return Read(File.ReadAllBytes(path));
	}

	public static DripRlFile Read(byte[] file)
	{
		try
		{
			ZipArchive zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
			try
			{
				Manifest manifest = JsonSerializer.Deserialize<Manifest>(Get("manifest.json", 1048576L), new JsonSerializerOptions
				{
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				}) ?? throw new UpkException("That .dripRL file's manifest is empty.");
				if (!string.Equals(manifest.Format, "dripRL", StringComparison.OrdinalIgnoreCase))
				{
					throw new UpkException("That isn't a .dripRL file.");
				}
				if (manifest.Version > 2)
				{
					throw new UpkException("That .dripRL file was made by a newer version of Drip Swapper. Update the app and try again.");
				}
				DripRlFile dripRlFile = new DripRlFile
				{
					Name = Clean(manifest.Name, "Custom model"),
					Author = Clean(manifest.Author, "")
				};
				string slot = manifest.Slot;
				dripRlFile.Slot = ((slot != null && slot.Length > 0) ? manifest.Slot : "Body");
				dripRlFile.Base = Clean(manifest.Base, "Body_Import");
				dripRlFile.Notes = Clean(manifest.Notes, "");
				dripRlFile.OwnWheels = manifest.OwnWheels;
				DripRlFile dripRlFile2 = dripRlFile;
				slot = manifest.Mesh;
				dripRlFile2.Mesh = ReadMesh(Get((slot != null && slot.Length > 0) ? manifest.Mesh : "mesh.bin", 536870912L));
				if (!string.IsNullOrEmpty(manifest.Collision))
				{
					dripRlFile2.Collision = ReadMesh(Get(manifest.Collision, 536870912L));
				}
				if (!string.IsNullOrEmpty(manifest.Picture))
				{
					dripRlFile2.Picture = Get(manifest.Picture, 67108864L);
					dripRlFile2.PictureName = manifest.Picture;
				}
				return dripRlFile2;
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
				ZipArchiveEntry? zipArchiveEntry = zip.GetEntry(n) ?? throw new UpkException("That .dripRL file is missing " + n + ".");
				if (zipArchiveEntry.Length > max)
				{
					throw new UpkException(n + " in that .dripRL file is too big.");
				}
				using Stream stream = zipArchiveEntry.Open();
				MemoryStream memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}
		catch (InvalidDataException)
		{
			throw new UpkException("That file isn't a valid .dripRL (it isn't a zip).");
		}
		catch (JsonException)
		{
			throw new UpkException("That .dripRL file's manifest is damaged.");
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

	public static byte[] WriteMesh(CarMesh m)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(1296847428);
		binaryWriter.Write(1);
		binaryWriter.Write(m.VertexCount);
		binaryWriter.Write(m.Idx.Length);
		Vector3[] pos = m.Pos;
		for (int i = 0; i < pos.Length; i++)
		{
			Vector3 vector = pos[i];
			binaryWriter.Write(vector.X);
			binaryWriter.Write(vector.Y);
			binaryWriter.Write(vector.Z);
		}
		pos = m.Nrm;
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
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	public static CarMesh ReadMesh(byte[] b)
	{
		using BinaryReader binaryReader = new BinaryReader(new MemoryStream(b));
		if (binaryReader.ReadInt32() != 1296847428 || binaryReader.ReadInt32() != 1)
		{
			throw new UpkException("The model in that .dripRL file isn't readable.");
		}
		int num = binaryReader.ReadInt32();
		int num2 = binaryReader.ReadInt32();
		if (num < 3 || num > 8000000 || num2 < 3 || num2 % 3 != 0 || num2 > 30000000 || b.Length < 16 + (long)num * 32L + (long)num2 * 4L)
		{
			throw new UpkException("The model in that .dripRL file isn't readable.");
		}
		CarMesh carMesh = new CarMesh
		{
			Pos = new Vector3[num],
			Nrm = new Vector3[num],
			Uv = new Vector2[num],
			Idx = new int[num2]
		};
		for (int i = 0; i < num; i++)
		{
			carMesh.Pos[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int j = 0; j < num; j++)
		{
			carMesh.Nrm[j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int k = 0; k < num; k++)
		{
			carMesh.Uv[k] = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int l = 0; l < num2; l++)
		{
			int num3 = binaryReader.ReadInt32();
			if (num3 < 0 || num3 >= num)
			{
				throw new UpkException("The model in that .dripRL file isn't readable.");
			}
			carMesh.Idx[l] = num3;
		}
		return carMesh;
	}

	public static DripRlFile FromFbx(string fbxPath, string? picturePath, ModelFit fit, string name, string slot = "Body", string author = "", MapCollisionMode collision = MapCollisionMode.Auto)
	{
		return FromScene(ModelFileReader.Load(fbxPath), picturePath, fit, name, slot, author, collision);
	}

	public static DripRlFile FromScene(FbxScene scene, string? picturePath, ModelFit fit, string name, string slot = "Body", string author = "", MapCollisionMode collision = MapCollisionMode.Auto)
	{
		IReadOnlyList<string> picturePaths;
		if (!string.IsNullOrWhiteSpace(picturePath))
		{
			IReadOnlyList<string> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>(picturePath);
			picturePaths = readOnlyList;
		}
		else
		{
			IReadOnlyList<string> readOnlyList = Array.Empty<string>();
			picturePaths = readOnlyList;
		}
		return FromScene(scene, picturePaths, fit, name, slot, author, collision);
	}

	public static DripRlFile FromScene(FbxScene scene, IReadOnlyList<string> picturePaths, ModelFit fit, string name, string slot = "Body", string author = "", MapCollisionMode collision = MapCollisionMode.Auto)
	{
		byte[] array = null;
		TexImage picture = null;
		List<NamedPicture> list = new List<NamedPicture>();
		foreach (string item in picturePaths.Where((string p) => !string.IsNullOrWhiteSpace(p)))
		{
			byte[] array2 = File.ReadAllBytes(item);
			TexImage texImage = TexImage.Load(array2);
			list.Add(new NamedPicture(Path.GetFileName(item), texImage));
			if (array == null)
			{
				array = array2;
				picture = texImage;
			}
		}
		CarMesh collision2 = new CarMesh();
		CarMesh carMesh;
		if (slot == "Map")
		{
			MapMesh mapMesh = MapGeometry.Build(scene, fit, collision, fit.UseMaterialColors, picture, list);
			carMesh = mapMesh.Visual;
			MapGeometry.Split(carMesh, 65535, 80, 65535);
			CarMesh carMesh2 = MapMeshWriter.CollisionWelded(mapMesh.Collision);
			collision2 = new CarMesh
			{
				Pos = carMesh2.Pos,
				Nrm = new Vector3[carMesh2.Pos.Length],
				Uv = new Vector2[carMesh2.Pos.Length],
				Idx = carMesh2.Idx
			};
		}
		else
		{
			carMesh = CarMeshBuilder.Build(scene, fit, null, slot == "Ball", fit.UseMaterialColors, picture, list, slot == "Wheels");
		}
		DripRlFile dripRlFile = new DripRlFile
		{
			Name = name,
			Author = author,
			Slot = slot,
			Mesh = carMesh,
			Collision = collision2,
			Base = slot switch
			{
				"Map" => "Labs_Underpass_P", 
				"Wheels" => "WHEEL_Hydra", 
				"Ball" => "Mutators_Balls", 
				_ => "Body_Import", 
			}
		};
		if (carMesh.Atlas != null)
		{
			dripRlFile.Picture = carMesh.Atlas.ToPng();
			dripRlFile.PictureName = ((list.Count > 1) ? "textures.png" : "colors.png");
		}
		else if (array != null)
		{
			dripRlFile.Picture = array;
			dripRlFile.PictureName = Path.GetFileName(picturePaths.First((string p) => !string.IsNullOrWhiteSpace(p)));
		}
		return dripRlFile;
	}
}
