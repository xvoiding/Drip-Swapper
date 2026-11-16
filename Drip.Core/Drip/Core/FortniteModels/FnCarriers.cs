using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Drip.Core.Cosmetics;
using Drip.Core.Swapping;

namespace Drip.Core.FortniteModels;

public static class FnCarriers
{
	private static readonly Regex PathStr = new Regex("\"(/[A-Za-z0-9_]+(?:/[A-Za-z0-9_.\\-]+)+)\"", RegexOptions.Compiled);

	public static List<string> MeshesOf(PartResolver resolver, string partPackage)
	{
		List<string> list = new List<string>();
		try
		{
			string text = resolver.PackageFile(PackageNames.Normalize(partPackage));
			if (text == null)
			{
				return list;
			}
			foreach (Match item in PathStr.Matches(resolver.DumpJson(text)))
			{
				string value = item.Groups[1].Value;
				if (value.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("/Engine/", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text2 = value.Substring(value.LastIndexOf('/') + 1);
				int num = text2.IndexOf('.');
				if (num >= 0)
				{
					text2 = text2.Substring(0, num);
				}
				if (!text2.StartsWith("CP_", StringComparison.OrdinalIgnoreCase) && !text2.Contains("Skeleton", StringComparison.OrdinalIgnoreCase) && !text2.Contains("Physics", StringComparison.OrdinalIgnoreCase) && !text2.Contains("_PA", StringComparison.Ordinal) && (!text2.StartsWith("M_", StringComparison.Ordinal) || !value.Contains("/Materials/", StringComparison.OrdinalIgnoreCase)) && value.Contains("/Mesh", StringComparison.OrdinalIgnoreCase))
				{
					string text3 = PackageNames.Normalize(value);
					if (!list.Contains(text3, StringComparer.OrdinalIgnoreCase))
					{
						list.Add(text3);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static string? Problem(FnRigInfo rig, Func<string, byte, byte[]> read)
	{
		string value = "";
		try
		{
			value = "body";
			byte[] type = read(rig.BodyMesh, 1);
			List<FnLod> list = FnMesh.FindLods(read(rig.BodyMesh, 2));
			if (list.Count < 1)
			{
				return rig.Name + ": the body mesh isn't laid out the way this version expects.";
			}
			FnMeshInfo fnMeshInfo = FnMeshInfo.Read(type, list);
			for (int i = 0; i < Math.Min(rig.MaxLods, list.Count); i++)
			{
				if (!fnMeshInfo.BoneMaps.ContainsKey(i))
				{
					return $"{rig.Name}: no bone list for body detail level {i}.";
				}
			}
			foreach (string item in from h in new string[1] { rig.HeadMesh }.Concat(rig.HiddenMeshes)
				where h.Length > 0
				select h)
			{
				value = "a hidden part";
				if (FnMesh.FindLods(read(item, 2)).Count < 1)
				{
					return rig.Name + ": a part that has to be hidden isn't laid out the way this version expects.";
				}
			}
			value = "the body picture";
			FnTexturePackage.Info info = FnTexturePackage.Read(read(rig.BodyTexture, 1));
			if (info.PixelFormat != "PF_DXT1")
			{
				return rig.Name + ": its colour picture is " + info.PixelFormat + ", which can't be rewritten.";
			}
			if (info.BulkEntries < 9)
			{
				return rig.Name + ": its colour picture has too few levels for a model picture.";
			}
			foreach (string item2 in new string[5] { rig.BodyNormal, rig.BodySpecular, rig.BodyMask, rig.BodyEffects, rig.BodyEmissive }.Where((string p) => p.Length > 0))
			{
				value = "a body picture";
				FnTexturePackage.Info info2 = FnTexturePackage.Read(read(item2, 1));
				string pixelFormat = info2.PixelFormat;
				if ((!(pixelFormat == "PF_DXT1") && !(pixelFormat == "PF_BC5")) || 1 == 0)
				{
					return rig.Name + ": one of its pictures is " + info2.PixelFormat + ", which can't be switched to a plain value.";
				}
				if (info2.BulkEntries < 3)
				{
					return rig.Name + ": one of its pictures has too few levels.";
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			return $"{rig.Name}: couldn't read {value} ({Friendly.Explain(ex.Message).Split('\n')[0]}).";
		}
	}
}
