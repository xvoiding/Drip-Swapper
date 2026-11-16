using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Drip.Core.FallGuys;

public static class FgBeanPatch
{
	public const string BeanAsset = "CH_FallGuy_MO.fbx";

	public static FgSkinMesh Rig(FgFittedModel model, FgSkinMesh donor)
	{
		int num = model.Pos.Length;
		FgSkinMesh res = new FgSkinMesh
		{
			Name = donor.Name,
			Pos = model.Pos,
			Nrm = model.Nrm,
			Uv = model.Uv,
			Idx = model.Idx,
			W = new float[num * 4],
			B = new int[num * 4],
			BindPoses = donor.BindPoses
		};
		if (res.Nrm.Length != num)
		{
			res.ComputeNormals();
		}
		res.ComputeTangents();
		if (model.SkinW != null && model.SkinB != null && model.SkinW.Length == num * 4 && model.SkinB.Length == num * 4 && donor.BindPoses.Length == 36)
		{
			Array.Copy(model.SkinW, res.W, num * 4);
			Array.Copy(model.SkinB, res.B, num * 4);
			return res;
		}
		int dn = donor.VertexCount;
		Parallel.For(0, num, (int v) =>
		{
			Span<float> span = stackalloc float[3];
			Span<int> span2 = stackalloc int[3];
			for (int i = 0; i < 3; i++)
			{
				span[i] = float.MaxValue;
				span2[i] = -1;
			}
			Vector3 value = model.Pos[v];
			for (int j = 0; j < dn; j++)
			{
				float num2 = Vector3.DistanceSquared(value, donor.Pos[j]);
				if (!(num2 >= span[2]))
				{
					int num3 = 2;
					while (num3 > 0 && num2 < span[num3 - 1])
					{
						span[num3] = span[num3 - 1];
						span2[num3] = span2[num3 - 1];
						num3--;
					}
					span[num3] = num2;
					span2[num3] = j;
				}
			}
			Dictionary<int, float> dictionary = new Dictionary<int, float>();
			for (int k = 0; k < 3; k++)
			{
				if (span2[k] >= 0)
				{
					float num4 = 1f / (MathF.Sqrt(span[k]) + 0.02f);
					for (int l = 0; l < 4; l++)
					{
						float num5 = donor.W[span2[k] * 4 + l];
						if (!(num5 <= 0f))
						{
							int key = donor.B[span2[k] * 4 + l];
							dictionary[key] = dictionary.GetValueOrDefault(key) + num5 * num4;
						}
					}
				}
			}
			List<KeyValuePair<int, float>> list = dictionary.OrderByDescending((KeyValuePair<int, float> x) => x.Value).Take(4).ToList();
			float num6 = list.Sum((KeyValuePair<int, float> x) => x.Value);
			if (num6 <= 0f)
			{
				res.W[v * 4] = 1f;
				res.B[v * 4] = 0;
			}
			else
			{
				for (int num7 = 0; num7 < list.Count; num7++)
				{
					res.W[v * 4 + num7] = list[num7].Value / num6;
					res.B[v * 4 + num7] = list[num7].Key;
				}
			}
		});
		return res;
	}

	public static void Apply(string srcBundle, string outBundle, FgFittedModel model)
	{
		AssetsManager assetsManager = new AssetsManager();
		try
		{
			BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(srcBundle);
			AssetsFileInstance assetsFileInstance = assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0);
			FgPrefabScan fgPrefabScan = new FgPrefabScan(assetsManager, assetsFileInstance);
			if (!fgPrefabScan.Container().TryGetValue("CH_FallGuy_MO.fbx", out var value))
			{
				throw new InvalidDataException("This bundle does not hold the Fall Guys character.");
			}
			List<FgRenderer> list = fgPrefabScan.Renderers(value);
			if (list.Count == 0)
			{
				throw new InvalidDataException("The character model has no renderers.");
			}
			FgSkinMesh fgSkinMesh = null;
			foreach (long item3 in list.Select((FgRenderer r) => r.MeshPathId).Distinct())
			{
				AssetFileInfo info = fgPrefabScan.Info(item3);
				FgSkinMesh fgSkinMesh2 = FgMeshIo.Read(assetsManager.GetBaseField(assetsFileInstance, info));
				if (fgSkinMesh == null || fgSkinMesh2.VertexCount > fgSkinMesh.VertexCount)
				{
					fgSkinMesh = fgSkinMesh2;
				}
			}
			if (fgSkinMesh == null || fgSkinMesh.VertexCount == 0)
			{
				throw new InvalidDataException("Could not read the character's mesh.");
			}
			FgSkinMesh fgSkinMesh3 = Rig(model, fgSkinMesh);
			foreach (long item4 in list.Select((FgRenderer r) => r.MeshPathId).Distinct())
			{
				AssetFileInfo assetFileInfo = fgPrefabScan.Info(item4);
				AssetTypeValueField baseField = assetsManager.GetBaseField(assetsFileInstance, assetFileInfo);
				FgSkinMesh fgSkinMesh4 = FgMeshIo.Read(baseField);
				FgSkinMesh m = new FgSkinMesh
				{
					Name = fgSkinMesh4.Name,
					Pos = fgSkinMesh3.Pos,
					Nrm = fgSkinMesh3.Nrm,
					Tan = fgSkinMesh3.Tan,
					Uv = fgSkinMesh3.Uv,
					W = fgSkinMesh3.W,
					B = fgSkinMesh3.B,
					Idx = fgSkinMesh3.Idx,
					BindPoses = ((fgSkinMesh4.BindPoses.Length != 0) ? fgSkinMesh4.BindPoses : fgSkinMesh3.BindPoses)
				};
				FgMeshIo.Write(baseField, m);
				assetFileInfo.SetNewData(baseField);
			}
			long num = 0L;
			foreach (AssetFileInfo item5 in assetsFileInstance.file.GetAssetsOfType(AssetClassID.Material))
			{
				if (assetsManager.GetBaseField(assetsFileInstance, item5)["m_Name"].AsString == "CH_FallGuy_MAT")
				{
					num = item5.PathId;
					break;
				}
			}
			if (num == 0L)
			{
				throw new InvalidDataException("The character's material was not where it was expected.");
			}
			(Vector3 Min, Vector3 Max) tuple = fgSkinMesh3.Bounds();
			Vector3 item = tuple.Min;
			Vector3 item2 = tuple.Max;
			Vector3 vector = (item + item2) / 2f;
			Vector3 vector2 = (item2 - item) / 2f * 1.2f + new Vector3(0.1f);
			foreach (FgRenderer item6 in list)
			{
				AssetFileInfo assetFileInfo2 = fgPrefabScan.Info(item6.SmrPathId);
				AssetTypeValueField baseField2 = assetsManager.GetBaseField(assetsFileInstance, assetFileInfo2);
				List<AssetTypeValueField> children = baseField2["m_Materials"]["Array"].Children;
				for (int num2 = 0; num2 < children.Count; num2++)
				{
					children[num2]["m_FileID"].AsInt = 0;
					children[num2]["m_PathID"].AsLong = num;
				}
				AssetTypeValueField assetTypeValueField = baseField2["m_AABB"];
				if (!assetTypeValueField.IsDummy)
				{
					assetTypeValueField["m_Center"]["x"].AsFloat = vector.X;
					assetTypeValueField["m_Center"]["y"].AsFloat = vector.Y;
					assetTypeValueField["m_Center"]["z"].AsFloat = vector.Z;
					assetTypeValueField["m_Extent"]["x"].AsFloat = vector2.X;
					assetTypeValueField["m_Extent"]["y"].AsFloat = vector2.Y;
					assetTypeValueField["m_Extent"]["z"].AsFloat = vector2.Z;
				}
				assetFileInfo2.SetNewData(baseField2);
			}
			Dictionary<string, long> dictionary = FgMaterialTool.TextureIds(assetsManager, assetsFileInstance);
			if (!dictionary.TryGetValue("CH_FallGuy_AM", out var value2) || !dictionary.TryGetValue("CH_FallGuy_RM", out var value3) || !dictionary.TryGetValue("CH_FallGuy_NM", out var value4))
			{
				throw new InvalidDataException("The character's textures were not where they were expected.");
			}
			FgTexIo.WriteRgba(assetsManager, assetsFileInstance, fgPrefabScan.Info(value2), model.Width, model.Height, model.Rgba, mips: true);
			FgTexIo.WriteFlat(assetsManager, assetsFileInstance, fgPrefabScan.Info(value3), 0, 200, 0, byte.MaxValue);
			FgTexIo.WriteFlat(assetsManager, assetsFileInstance, fgPrefabScan.Info(value4), 128, 128, byte.MaxValue, 128);
			FgMaterialTool.SetCostumeLook(assetsManager, assetsFileInstance, fgPrefabScan.Info(num), value2, value3, value4);
			FgBundleIo.WriteBundle(bundleFileInstance, assetsFileInstance, outBundle);
		}
		finally
		{
			assetsManager.UnloadAll();
		}
	}
}
