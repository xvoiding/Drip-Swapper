using System;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Drip.Core.FallGuys;

public static class FgMaterialTool
{
	public const long CostumeBaseShaderPathId = -8735875998284758389L;

	public static void SetCostumeLook(AssetsManager am, AssetsFileInstance afi, AssetFileInfo matInfo, long albedoTex, long metalRoughTex, long normalTex)
	{
		AssetTypeValueField baseField = am.GetBaseField(afi, matInfo);
		baseField["m_Shader"]["m_PathID"].AsLong = -8735875998284758389L;
		AssetTypeValueField assetTypeValueField = baseField["m_SavedProperties"];
		(string Name, long Id)[] tex = new (string, long)[4]
		{
			("_MainTex", albedoTex),
			("_MetallicRoughness", metalRoughTex),
			("_NormalMap", normalTex),
			("_ScrollingTex", 0L)
		};
		SetPairs(assetTypeValueField["m_TexEnvs"], tex.Length, (AssetTypeValueField item, int i) =>
		{
			item["first"].AsString = tex[i].Name;
			AssetTypeValueField assetTypeValueField3 = item["second"];
			assetTypeValueField3["m_Texture"]["m_FileID"].AsInt = 0;
			assetTypeValueField3["m_Texture"]["m_PathID"].AsLong = tex[i].Id;
			assetTypeValueField3["m_Scale"]["x"].AsFloat = 1f;
			assetTypeValueField3["m_Scale"]["y"].AsFloat = 1f;
			assetTypeValueField3["m_Offset"]["x"].AsFloat = 0f;
			assetTypeValueField3["m_Offset"]["y"].AsFloat = 0f;
		});
		(string Name, float V)[] fl = new (string, float)[6]
		{
			("_EmissionMax", 1f),
			("_EmissionMin", 1f),
			("_EmissivePower", 3f),
			("_RimPower", 3f),
			("_RoughnessRange", 0.85f),
			("_UseScrollingTex", 0f)
		};
		SetPairs(assetTypeValueField["m_Floats"], fl.Length, (AssetTypeValueField item, int i) =>
		{
			item["first"].AsString = fl[i].Name;
			item["second"].AsFloat = fl[i].V;
		});
		(string Name, float R, float G, float B, float A)[] col = new (string, float, float, float, float)[3]
		{
			("_EmissiveColor", 0.26f, 0.19f, 0.16f, 1f),
			("_RimColor", 0.26f, 0.19f, 0.16f, 1f),
			("_TeamColor", 1f, 1f, 1f, 1f)
		};
		SetPairs(assetTypeValueField["m_Colors"], col.Length, (AssetTypeValueField item, int i) =>
		{
			item["first"].AsString = col[i].Name;
			AssetTypeValueField assetTypeValueField3 = item["second"];
			assetTypeValueField3["r"].AsFloat = col[i].R;
			assetTypeValueField3["g"].AsFloat = col[i].G;
			assetTypeValueField3["b"].AsFloat = col[i].B;
			assetTypeValueField3["a"].AsFloat = col[i].A;
		});
		SetPairs(assetTypeValueField["m_Ints"], 0, (AssetTypeValueField _, int _) =>
		{
		});
		string[] array = new string[2] { "m_ValidKeywords", "m_InvalidKeywords" };
		foreach (string name in array)
		{
			AssetTypeValueField assetTypeValueField2 = baseField[name];
			if (!assetTypeValueField2.IsDummy)
			{
				assetTypeValueField2["Array"].Children.Clear();
			}
		}
		matInfo.SetNewData(baseField);
	}

	private static void SetPairs(AssetTypeValueField vec, int count, Action<AssetTypeValueField, int> fill)
	{
		AssetTypeValueField assetTypeValueField = vec["Array"];
		while (assetTypeValueField.Children.Count > count)
		{
			assetTypeValueField.Children.RemoveAt(assetTypeValueField.Children.Count - 1);
		}
		while (assetTypeValueField.Children.Count < count)
		{
			assetTypeValueField.Children.Add(ValueBuilder.DefaultValueFieldFromArrayTemplate(assetTypeValueField));
		}
		for (int i = 0; i < count; i++)
		{
			fill(assetTypeValueField.Children[i], i);
		}
	}

	public static Dictionary<string, long> TextureIds(AssetsManager am, AssetsFileInstance afi)
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (AssetFileInfo item in afi.file.GetAssetsOfType(AssetClassID.Texture2D))
		{
			AssetTypeValueField baseField = am.GetBaseField(afi, item);
			dictionary.TryAdd(baseField["m_Name"].AsString, item.PathId);
		}
		return dictionary;
	}
}
