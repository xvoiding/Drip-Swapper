using System;
using System.Collections.Generic;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Drip.Core.FallGuys;

public sealed class FgPrefabScan
{
	private readonly AssetsManager _am;

	private readonly AssetsFileInstance _afi;

	private readonly Dictionary<long, AssetFileInfo> _byId = new Dictionary<long, AssetFileInfo>();

	public FgPrefabScan(AssetsManager am, AssetsFileInstance afi)
	{
		_am = am;
		_afi = afi;
		foreach (AssetFileInfo assetInfo in afi.file.AssetInfos)
		{
			_byId[assetInfo.PathId] = assetInfo;
		}
	}

	public AssetFileInfo? Info(long pathId)
	{
		if (!_byId.TryGetValue(pathId, out AssetFileInfo value))
		{
			return null;
		}
		return value;
	}

	public Dictionary<string, long> Container()
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.Ordinal);
		using (List<AssetFileInfo>.Enumerator enumerator = _afi.file.GetAssetsOfType(AssetClassID.AssetBundle).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				AssetFileInfo current = enumerator.Current;
				foreach (AssetTypeValueField child in _am.GetBaseField(_afi, current)["m_Container"]["Array"].Children)
				{
					string asString = child["first"].AsString;
					AssetTypeValueField assetTypeValueField = child["second"]["asset"];
					if (assetTypeValueField["m_FileID"].AsInt == 0)
					{
						dictionary.TryAdd(asString, assetTypeValueField["m_PathID"].AsLong);
					}
				}
			}
		}
		return dictionary;
	}

	private AssetTypeValueField? Field(long pathId)
	{
		AssetFileInfo assetFileInfo = Info(pathId);
		if (assetFileInfo != null)
		{
			return _am.GetBaseField(_afi, assetFileInfo);
		}
		return null;
	}

	public List<FgRenderer> Renderers(long rootGameObject)
	{
		List<FgRenderer> list = new List<FgRenderer>();
		AssetTypeValueField assetTypeValueField = Field(rootGameObject);
		if (assetTypeValueField == null)
		{
			return list;
		}
		long transformId = 0L;
		foreach (AssetTypeValueField child in assetTypeValueField["m_Component"]["Array"].Children)
		{
			long asLong = child["component"]["m_PathID"].AsLong;
			AssetFileInfo assetFileInfo = Info(asLong);
			if (assetFileInfo != null && assetFileInfo.TypeId == 4)
			{
				transformId = asLong;
				break;
			}
		}
		Walk(transformId, list, 0);
		return list;
	}

	private void Walk(long transformId, List<FgRenderer> res, int depth)
	{
		if (transformId == 0L || depth > 64)
		{
			return;
		}
		AssetTypeValueField assetTypeValueField = Field(transformId);
		if (assetTypeValueField == null)
		{
			return;
		}
		long asLong = assetTypeValueField["m_GameObject"]["m_PathID"].AsLong;
		AssetTypeValueField assetTypeValueField2 = Field(asLong);
		if (assetTypeValueField2 != null)
		{
			foreach (AssetTypeValueField child in assetTypeValueField2["m_Component"]["Array"].Children)
			{
				long asLong2 = child["component"]["m_PathID"].AsLong;
				AssetFileInfo assetFileInfo = Info(asLong2);
				if (assetFileInfo == null || assetFileInfo.TypeId != 137)
				{
					continue;
				}
				AssetTypeValueField baseField = _am.GetBaseField(_afi, assetFileInfo);
				AssetTypeValueField assetTypeValueField3 = baseField["m_Mesh"];
				if (assetTypeValueField3["m_FileID"].AsInt == 0 && assetTypeValueField3["m_PathID"].AsLong != 0L)
				{
					long[] materialPathIds = (from m in baseField["m_Materials"]["Array"].Children
						where m["m_FileID"].AsInt == 0
						select m["m_PathID"].AsLong).ToArray();
					res.Add(new FgRenderer(asLong2, assetTypeValueField2["m_Name"].AsString, assetTypeValueField3["m_PathID"].AsLong, materialPathIds, baseField["m_Bones"]["Array"].Children.Count));
				}
			}
		}
		foreach (AssetTypeValueField child2 in assetTypeValueField["m_Children"]["Array"].Children)
		{
			if (child2["m_FileID"].AsInt == 0)
			{
				Walk(child2["m_PathID"].AsLong, res, depth + 1);
			}
		}
	}

	public long? FindGameObject(string name)
	{
		foreach (AssetFileInfo item in _afi.file.GetAssetsOfType(AssetClassID.GameObject))
		{
			if (_am.GetBaseField(_afi, item)["m_Name"].AsString == name)
			{
				return item.PathId;
			}
		}
		return null;
	}
}
