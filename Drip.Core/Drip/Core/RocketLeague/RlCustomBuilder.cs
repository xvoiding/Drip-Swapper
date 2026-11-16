using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.RocketLeague;

public static class RlCustomBuilder
{
	public const string CarBase = "Body_Import";

	public const string BallBase = "Mutators_Balls";

	public const string BallMesh = "Ball_Soccer_SM";

	public const string BallPicture = "SoccerBall_D";

	public static readonly (string Mesh, string Picture)[] GameBalls = new (string, string)[2]
	{
		("Ball_DefaultBall00", "Ball_Default00_D"),
		("Ball_Basketball_SM", "Ball_Basketball_D")
	};

	public const string WheelBase = "WHEEL_Hydra";

	public static WheelTextureMode WheelMode { get; set; } = WheelTextureMode.GameMask;

	public static CustomTextureMode Mode { get; set; } = CustomTextureMode.Unpainted;

	public static string BaseFor(string slot)
	{
		if (!slot.Equals("Body", StringComparison.OrdinalIgnoreCase))
		{
			if (!slot.Equals("Wheels", StringComparison.OrdinalIgnoreCase))
			{
				if (!slot.Equals("Ball", StringComparison.OrdinalIgnoreCase))
				{
					return "";
				}
				return "Mutators_Balls";
			}
			return "WHEEL_Hydra";
		}
		return "Body_Import";
	}

	public static byte[] Build(DripRlFile model, byte[] basePackage, byte[] key)
	{
		bool flag = model.Slot.Equals("Wheels", StringComparison.OrdinalIgnoreCase);
		bool flag2 = model.Slot.Equals("Ball", StringComparison.OrdinalIgnoreCase);
		if (!flag && !flag2 && !model.Slot.Equals("Body", StringComparison.OrdinalIgnoreCase))
		{
			throw new UpkException("Custom " + model.Slot.ToLowerInvariant() + " models aren't supported yet. Cars, wheels and balls work for now.");
		}
		UpkImage upkImage = UpkCodec.Decrypt(basePackage, key);
		List<(long, int, int)> list = new List<(long, int, int)>();
		if (flag | flag2)
		{
			byte[] data = StaticMeshWriter.Apply(upkImage.Data, model.Mesh, out (long, int, int) edit, flag2 ? "Ball_Soccer_SM" : null);
			list.Add(edit);
			data = (flag ? DressWheel(data, model, list) : DressBall(data, model, list));
			byte[] array = UpkCodec.Encrypt(upkImage, data, key, list);
			if (UpkCodec.Decrypt(array, key).Data.Length != data.Length)
			{
				throw new UpkException("The custom package didn't rebuild cleanly.");
			}
			return array;
		}
		byte[] data2 = SkeletalMeshWriter.Apply(upkImage.Data, model.Mesh, out (long, int, int) edit2, Mode == CustomTextureMode.Unpainted);
		list.Add(edit2);
		data2 = DressBody(data2, model, list);
		byte[] array2 = UpkCodec.Encrypt(upkImage, data2, key, list);
		if (UpkCodec.Decrypt(array2, key).Data.Length != data2.Length)
		{
			throw new UpkException("The custom package didn't rebuild cleanly.");
		}
		return array2;
	}

	private static byte[] DressBody(byte[] data, DripRlFile model, List<(long Start, int OldLen, int NewLen)> edits)
	{
		UpkSummary s = UpkSummary.Parse(data);
		List<NameEntry> names = UpkNames.Read(data, s, out var _);
		List<UpkExport> list = UpkTables.Exports(data, s);
		UpkExport upkExport = (from e in list
			where e.ClassName == "SkeletalMesh" && e.SerialSize > 10000
			orderby e.SerialSize descending
			select e).First();
		SkeletalMeshWriter.Layout layout = SkeletalMeshWriter.ReadLayout(data.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names);
		bool flag = Mode == CustomTextureMode.Unpainted;
		int num = (flag ? SkeletalMeshWriter.UnpaintedMaterialExport(data, s, names, list, layout.MaterialRefs) : SkeletalMeshWriter.BodyMaterialExport(data, s, names, list, layout.MaterialRefs));
		if (num <= 0 || Mode == CustomTextureMode.MeshOnly)
		{
			return data;
		}
		UpkExport upkExport2 = list[num - 1];
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, int> dictionary2 = MaterialTools.TextureParams(data.AsSpan(upkExport2.SerialOffset, upkExport2.SerialSize).ToArray(), names, dictionary);
		if (flag)
		{
			if (model.Picture.Length != 0 && dictionary2.TryGetValue("Diffuse", out var value) && value > 0)
			{
				TexImage pic0 = TexImage.Load(model.Picture).ForGame();
				if (Texture2DWriter.Variant == 3)
				{
					pic0 = pic0.Resample(64, 64);
				}
				if (Texture2DWriter.Variant == 4)
				{
					pic0 = pic0.Resample(2048, 2048);
				}
				data = Put(data, value, (byte[] old, List<NameEntry> nm) => pic0);
			}
			if (dictionary2.TryGetValue("Masks", out var value2) && value2 > 0)
			{
				data = Put(data, value2, (byte[] old, List<NameEntry> nm) => Flat(64, 64, 0, 0, 0, Texture2DWriter.AverageColor(old, nm).A));
			}
			if (dictionary2.TryGetValue("Normal", out var value3) && value3 > 0)
			{
				data = Put(data, value3, (byte[] old, List<NameEntry> nm) =>
				{
					(byte, byte, byte, byte) tuple = Texture2DWriter.AverageColor(old, nm);
					return Flat(64, 64, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
				});
			}
			return data;
		}
		if (model.Picture.Length != 0 && dictionary2.TryGetValue("Diffuse", out var value4) && value4 > 0)
		{
			TexImage pic1 = TexImage.Load(model.Picture).ForGame();
			data = Put(data, value4, (byte[] old, List<NameEntry> nm) =>
			{
				byte[] array = (byte[])pic1.Rgba.Clone();
				byte b = (byte)((Mode == CustomTextureMode.FullPaintOn) ? byte.MaxValue : 0);
				if (Texture2DWriter.FormatOf(old, nm) != "PF_DXT1")
				{
					for (int i = 3; i < array.Length; i += 4)
					{
						array[i] = b;
					}
				}
				return new TexImage
				{
					Width = pic1.Width,
					Height = pic1.Height,
					Rgba = array
				};
			});
		}
		if (Mode == CustomTextureMode.PictureOnly)
		{
			return data;
		}
		if (dictionary2.TryGetValue("CurvaturePack", out var value5) && value5 > 0)
		{
			data = Put(data, value5, (byte[] old, List<NameEntry> nm) =>
			{
				(byte, byte, byte, byte) tuple = Texture2DWriter.AverageColor(old, nm);
				return Flat(64, 64, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
			});
		}
		if (dictionary2.TryGetValue("Skin", out var value6) && value6 < 0 && dictionary.TryGetValue("Skin", out var value7) && value7 >= 0)
		{
			UpkExport upkExport3 = UpkTables.Exports(data, UpkSummary.Parse(data)).FirstOrDefault((UpkExport e) => e.ClassName == "Texture2D" && e.Name.Contains("Skin_RGB", StringComparison.OrdinalIgnoreCase));
			if (upkExport3 != null)
			{
				data = Put(data, upkExport3.Index, (byte[] old, List<NameEntry> nm) => Flat(64, 64, 0, 0, 0, 0));
				UpkExport upkExport4 = UpkTables.Exports(data, UpkSummary.Parse(data))[num - 1];
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(upkExport4.SerialOffset + value7, 4), upkExport3.Index);
			}
		}
		return data;
		static TexImage Flat(int w, int h, byte r, byte g, byte b, byte a)
		{
			return FlatImage(w, h, r, g, b, a);
		}
		byte[] Put(byte[] img, int exportNumber, Func<byte[], List<NameEntry>, TexImage> make, bool fixGroup = true)
		{
			return PutTexture(img, exportNumber, make, edits, fixGroup);
		}
	}

	private static TexImage FlatImage(int w, int h, byte r, byte g, byte b, byte a)
	{
		byte[] array = new byte[w * h * 4];
		for (int i = 0; i < array.Length; i += 4)
		{
			array[i] = r;
			array[i + 1] = g;
			array[i + 2] = b;
			array[i + 3] = a;
		}
		return new TexImage
		{
			Width = w,
			Height = h,
			Rgba = array
		};
	}

	private static int MipsFor(int w, int h)
	{
		int num = 1;
		for (int num2 = Math.Max(w, h); num2 > 1; num2 >>= 1)
		{
			num++;
		}
		return num;
	}

	internal static byte[] PutTexture(byte[] img, int exportNumber, Func<byte[], List<NameEntry>, TexImage> make, List<(long Start, int OldLen, int NewLen)> edits, bool fixGroup = true)
	{
		UpkSummary s = UpkSummary.Parse(img);
		List<NameEntry> list = UpkNames.Read(img, s, out var _);
		UpkExport upkExport = UpkTables.Exports(img, s)[exportNumber - 1];
		if (upkExport.ClassName != "Texture2D")
		{
			return img;
		}
		byte[] array = img.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray();
		TexImage pic = make(array, list);
		byte[] array2 = Texture2DWriter.Replace(array, list, pic);
		edits.Add((upkExport.SerialOffset, upkExport.SerialSize, array2.Length));
		img = UpkTables.ReplaceExport(img, s, upkExport, array2);
		if (!fixGroup)
		{
			return img;
		}
		int num = MipsFor(pic.Width, pic.Height);
		UpkTextureGroup upkTextureGroup = UpkTextureAllocs.Read(img, s).FirstOrDefault((UpkTextureGroup g) => g.Exports.Contains(exportNumber - 1));
		if (upkTextureGroup == null || (upkTextureGroup.SizeX == pic.Width && upkTextureGroup.SizeY == pic.Height && upkTextureGroup.NumMips == num))
		{
			return img;
		}
		UpkTextureAllocs.Set(img, upkTextureGroup, pic.Width, pic.Height, num);
		foreach (int item in upkTextureGroup.Exports.Where((int e) => e != exportNumber - 1))
		{
			img = PutTexture(img, item + 1, (byte[] o, List<NameEntry> n) =>
			{
				(byte, byte, byte, byte) tuple = Texture2DWriter.AverageColor(o, n);
				return FlatImage(pic.Width, pic.Height, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
			}, edits, fixGroup: false);
		}
		return img;
	}

	public static byte[]? BuildGameBalls(byte[] file, byte[] key, DripRlFile model, List<string> done)
	{
		UpkImage upkImage = UpkCodec.Decrypt(file, key);
		byte[] array = upkImage.Data;
		List<(long, int, int)> list = new List<(long, int, int)>();
		(string, string)[] gameBalls = GameBalls;
		for (int i = 0; i < gameBalls.Length; i++)
		{
			(string, string) tuple = gameBalls[i];
			string mesh = tuple.Item1;
			string item = tuple.Item2;
			UpkSummary s = UpkSummary.Parse(array);
			if (UpkTables.Exports(array, s).Any((UpkExport e) => e.ClassName == "StaticMesh" && e.Name.Equals(mesh, StringComparison.OrdinalIgnoreCase)))
			{
				array = StaticMeshWriter.Apply(array, model.Mesh, out (long, int, int) edit, mesh);
				list.Add(edit);
				done.Add(mesh);
				array = DressBall(array, model, list, item);
			}
		}
		if (done.Count == 0)
		{
			return null;
		}
		byte[] array2 = UpkCodec.Encrypt(upkImage, array, key, list);
		if (UpkCodec.Decrypt(array2, key).Data.Length != array.Length)
		{
			throw new UpkException("The custom package didn't rebuild cleanly.");
		}
		return array2;
	}

	private static byte[] DressBall(byte[] data, DripRlFile model, List<(long Start, int OldLen, int NewLen)> edits, string picture = "SoccerBall_D")
	{
		if (Mode == CustomTextureMode.MeshOnly || model.Picture.Length == 0)
		{
			return data;
		}
		UpkSummary s = UpkSummary.Parse(data);
		UpkExport upkExport = UpkTables.Exports(data, s).FirstOrDefault((UpkExport e) => e.ClassName == "Texture2D" && e.Name.Equals(picture, StringComparison.OrdinalIgnoreCase));
		if (upkExport == null)
		{
			return data;
		}
		TexImage pic = TexImage.Load(model.Picture).ForGame(1024, 1024);
		try
		{
			return PutTexture(data, upkExport.Index, (byte[] old, List<NameEntry> nm) => pic, edits);
		}
		catch (UpkException)
		{
			return data;
		}
	}

	private static byte[] DressWheel(byte[] data, DripRlFile model, List<(long Start, int OldLen, int NewLen)> edits)
	{
		UpkSummary s = UpkSummary.Parse(data);
		List<NameEntry> names = UpkNames.Read(data, s, out var _);
		UpkExport upkExport = UpkTables.Exports(data, s).FirstOrDefault((UpkExport e) => e.ClassName == "MaterialInstanceConstant" && e.Name.EndsWith("_MIC", StringComparison.OrdinalIgnoreCase));
		if (upkExport == null)
		{
			return data;
		}
		Dictionary<string, int> dictionary = MaterialTools.TextureParams(data.AsSpan(upkExport.SerialOffset, upkExport.SerialSize).ToArray(), names);
		if (Mode == CustomTextureMode.MeshOnly)
		{
			return data;
		}
		TexImage pic = ((model.Picture.Length != 0) ? TexImage.Load(model.Picture).ForGame() : null);
		if (pic != null && WheelMode == WheelTextureMode.Small)
		{
			pic = pic.Resample(64, 64);
		}
		int fw = pic?.Width ?? 64;
		int fh = pic?.Height ?? 64;
		if (pic != null && dictionary.TryGetValue("RimDiffuse", out var value) && value > 0)
		{
			data = PutTexture(data, value, (byte[] old, List<NameEntry> nm) => pic, edits);
		}
		if (pic != null && dictionary.TryGetValue("TireDiffuse", out var value2) && value2 > 0)
		{
			data = PutTexture(data, value2, (byte[] old, List<NameEntry> nm) => pic, edits);
		}
		if (Mode == CustomTextureMode.PictureOnly || WheelMode == WheelTextureMode.Small)
		{
			return data;
		}
		if (dictionary.TryGetValue("RimRGB", out var value3) && value3 > 0)
		{
			data = PutTexture(data, value3, (byte[] old, List<NameEntry> nm) =>
			{
				if (WheelMode == WheelTextureMode.GameMask)
				{
					(byte, byte, byte, byte) tuple = Texture2DWriter.AverageColor(old, nm);
					return FlatImage(fw, fh, tuple.Item1, tuple.Item2, tuple.Item3, byte.MaxValue);
				}
				byte b = (byte)((WheelMode == WheelTextureMode.Bright) ? byte.MaxValue : 0);
				return FlatImage(fw, fh, b, b, b, byte.MaxValue);
			}, edits);
		}
		if (dictionary.TryGetValue("RimNormal", out var value4) && value4 > 0)
		{
			data = PutTexture(data, value4, (byte[] old, List<NameEntry> nm) =>
			{
				(byte, byte, byte, byte) tuple = Texture2DWriter.AverageColor(old, nm);
				return FlatImage(64, 64, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
			}, edits);
		}
		return data;
	}
}
