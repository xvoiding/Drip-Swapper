using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public static class FnModelApplier
{
	public static FnBuild Build(Func<string, byte, byte[]> read, FnRigInfo rig, FnTarget target, Action<string>? log = null)
	{
		FnBuild fnBuild = new FnBuild();
		byte[] type = read(rig.BodyMesh, 1);
		byte[] array = (byte[])read(rig.BodyMesh, 2).Clone();
		List<FnLod> list = FnMesh.FindLods(array);
		FnMeshInfo fnMeshInfo = FnMeshInfo.Read(type, list);
		double[][] skinMatrices = FnPose.TPoseSkin(fnMeshInfo);
		FnLodFit fnLodFit = null;
		int num = 0;
		for (int i = 0; i < Math.Min(rig.MaxLods, list.Count); i++)
		{
			if (!fnMeshInfo.BoneMaps.TryGetValue(i, out ushort[] value))
			{
				L($"Body detail level {i}: skipped (no bone list found).");
				continue;
			}
			FnLodFit fnLodFit2 = FnFit.FitLod(target, array, list[i], skinMatrices, value);
			if (fnLodFit == null)
			{
				fnLodFit = fnLodFit2;
				fnBuild.BodyMove = fnLodFit2.MedianMove;
			}
			num++;
		}
		if (fnLodFit == null)
		{
			throw new InvalidDataException("The character's body mesh isn't laid out the way this version expects, so the model can't be fitted.");
		}
		L($"Body reshaped ({num} detail level(s)); it moved {fnLodFit.MedianMove:0.0} cm on average.");
		CheckFinite(array, list, num, "body");
		fnBuild.Patches.Add(new FnPatch(rig.BodyMesh, 2, array, "body shape"));
		TexImage texImage = FnFit.Bake(target, fnLodFit, rig.BodyTextureWidth, rig.BodyTextureHeight);
		fnBuild.Preview.Add(new FnPreviewPart
		{
			Pos = FnMesh.ReadPositions(array, list[0]),
			Tri = fnLodFit.Tri,
			Uv = fnLodFit.Uv,
			Texture = texImage
		});
		byte[] array2 = (byte[])read(rig.HeadMesh, 2).Clone();
		List<FnLod> list2 = FnMesh.FindLods(array2);
		FnLodFit fnLodFit3 = null;
		for (int j = 0; j < Math.Min(rig.MaxLods, list2.Count); j++)
		{
			FnLodFit fnLodFit4 = FnFit.FitLod(target, array2, list2[j], null, null);
			if (fnLodFit3 == null)
			{
				fnLodFit3 = fnLodFit4;
			}
		}
		fnBuild.HeadMove = fnLodFit3.MedianMove;
		L($"Head reshaped; it moved {fnLodFit3.MedianMove:0.0} cm on average.");
		CheckFinite(array2, list2, Math.Min(rig.MaxLods, list2.Count), "head");
		fnBuild.Patches.Add(new FnPatch(rig.HeadMesh, 2, array2, "head shape"));
		TexImage texImage2 = FnFit.Bake(target, fnLodFit3, rig.HeadTextureWidth, rig.HeadTextureHeight);
		fnBuild.Preview.Add(new FnPreviewPart
		{
			Pos = FnMesh.ReadPositions(array2, list2[0]),
			Tri = fnLodFit3.Tri,
			Uv = fnLodFit3.Uv,
			Texture = texImage2
		});
		string[] hiddenMeshes = rig.HiddenMeshes;
		foreach (string text in hiddenMeshes)
		{
			byte[] array3 = (byte[])read(text, 2).Clone();
			foreach (FnLod item in FnMesh.FindLods(array3))
			{
				FnFit.Hide(array3, item);
			}
			fnBuild.Patches.Add(new FnPatch(text, 2, array3, "hidden part"));
		}
		fnBuild.Patches.Add(new FnPatch(rig.BodyTexture, 1, FnTexturePackage.Build(read(rig.BodyTexture, 1), texImage), "body picture"));
		fnBuild.Patches.Add(new FnPatch(rig.HeadTexture, 1, FnTexturePackage.Build(read(rig.HeadTexture, 1), texImage2), "face picture"));
		foreach (FnPatch item2 in fnBuild.Patches.Where((FnPatch p) => p.Type == 1 && p.What.EndsWith("picture")))
		{
			FnTexturePackage.Info info = FnTexturePackage.Read(item2.Data);
			if (info.MipCount < 1 || info.Width < 4)
			{
				throw new InvalidDataException("The rebuilt picture didn't read back correctly.");
			}
		}
		return fnBuild;
		void L(string s)
		{
			log?.Invoke(s);
		}
	}

	private static void CheckFinite(byte[] bulk, IReadOnlyList<FnLod> lods, int count, string what)
	{
		for (int i = 0; i < count; i++)
		{
			double[] array = FnMesh.ReadPositions(bulk, lods[i]);
			foreach (double num in array)
			{
				if (double.IsNaN(num) || double.IsInfinity(num) || Math.Abs(num) > 5000.0)
				{
					throw new InvalidDataException("The fitted " + what + " came out broken (a vertex ran away), so nothing was written.");
				}
			}
		}
	}
}
