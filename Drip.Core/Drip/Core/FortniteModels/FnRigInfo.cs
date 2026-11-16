using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.FortniteModels;

public sealed class FnRigInfo
{
	public const byte ExportBundleChunk = 1;

	public const byte BulkChunk = 2;

	public static readonly FnRigInfo MaleRebirth = new FnRigInfo
	{
		Id = "male-rebirth",
		Name = "Rebirth Soldier (male)",
		BaseSkin = "CID_A_406_Athena_Commando_M_RebirthFresh",
		BodyMesh = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Meshes/M_MED_Rebirth_Soldier",
		HeadMesh = "/Game/Characters/Player/Male/Medium/Heads/M_MED_CAU_Jonsey_Rebirth_Head_01/Meshes/M_MED_CAU_Jonesy_Rebirth_Head_01",
		HiddenMeshes = new string[1] { "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Meshes/Parts/M_MED_Rebirth_Soldier_FaceAcc" },
		BodyTexture = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Textures/T_M_MED_Rebirth_Soldier_Body_D",
		HeadTexture = "/Game/Characters/Player/Male/Medium/Bodies/M_Med_Soldier_04/Textures/M_Med_Soldier_Head_01_C",
		BodyNormal = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Textures/T_M_MED_Rebirth_Soldier_Body_N",
		BodySpecular = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Textures/T_M_MED_Rebirth_Soldier_Body_S",
		BodyMask = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Textures/T_M_MED_Rebirth_Soldier_Body_M",
		BodyEffects = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Textures/T_M_MED_Rebirth_Soldier_Body_FX",
		HiddenColorCodes = new byte[4] { 12, 51, 76, 204 },
		BodyMaterial = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Rebirth_Soldier/Materials/M_MED_Rebirth_Soldier_Body",
		Tints = new FnTint[3]
		{
			FnTint.Off(3f, 1.645649f, 0.137937f),
			FnTint.Off(0f, 0.790205f, 1f),
			FnTint.Soft(21f / 32f, 0.372142f, 0.345216f, 2f)
		}
	};

	private const string BR = "/BRCosmetics/Characters/Player/Male/Medium/";

	public static readonly FnRigInfo MaleLars = new FnRigInfo
	{
		Id = "male-lars",
		Name = "Lars (male)",
		BaseSkin = "CID_A_186_Athena_Commando_M_Lars",
		BodyMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Meshes/M_MED_Lars",
		HeadMesh = "/BRCosmetics/Characters/Player/Male/Medium/Heads/M_MED_Lars/Mesh/Male_Medium_Lars_Head",
		HiddenMeshes = new string[1] { "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Meshes/Parts/M_MED_Lars_FaceAcc" },
		BodyTexture = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Textures/T_M_MED_Lars_Body_D",
		BodyNormal = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Textures/T_M_MED_Lars_Body_N",
		BodySpecular = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Textures/T_M_MED_Lars_Body_S",
		BodyMask = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Textures/T_M_MED_Lars_Body_M",
		BodyEmissive = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Textures/T_M_MED_Lars_Body_E",
		BodyMaterial = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Lars/Materials/M_MED_Lars_Body",
		Tints = new FnTint[2]
		{
			FnTint.Off(1f, 0f, 0f),
			FnTint.Soft(0.752942f, 0.47932f, 0.462077f, 1f)
		}
	};

	public static readonly FnRigInfo MaleJumpstart = new FnRigInfo
	{
		Id = "male-jumpstart",
		Name = "Jumpstart (male)",
		BaseSkin = "CID_537_Athena_Commando_M_Jumpstart",
		BodyMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Meshes/M_MED_Jumpstart",
		HeadMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Meshes/Parts/M_MED_Jumpstart_FaceAcc",
		BodyTexture = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_D",
		BodyNormal = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_N",
		BodySpecular = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_S",
		BodyMask = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_M",
		BodyEmissive = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_E",
		BodyEffects = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Textures/T_M_MED_Jumpstart_Body_FX",
		BodyMaterial = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Jumpstart/Materials/M_MED_Jumpstart_Body",
		Tints = new FnTint[1] { FnTint.Off(1.85f, 2.3f, 0.75f) }
	};

	public static readonly FnRigInfo MaleDeco = new FnRigInfo
	{
		Id = "male-deco",
		Name = "Deco (male)",
		BaseSkin = "CID_129_Athena_Commando_M_Deco",
		BodyMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Meshes/M_MED_Commando_GreenDeco_Body",
		HeadMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Meshes/Parts/M_MED_Commando_GreenDeco_Head",
		BodyTexture = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Textures/M_MED_Commando_Deco_Body_D",
		BodyNormal = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Textures/M_MED_Commando_Deco_Body_N",
		BodySpecular = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Textures/M_MED_Commando_Deco_Body_S",
		BodyMask = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Textures/M_MED_Commando_Deco_Body_M",
		BodyMaterial = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_Commando_GreenDeco/Materials/M_Med_Deco_Body",
		Tints = new FnTint[1] { FnTint.Soft(0.525f, 0.343875f, 0.367316f, 2f) }
	};

	private const string Gm = "/Game/Characters/Player/Male/Medium/";

	public static readonly FnRigInfo MaleHeistSummer = new FnRigInfo
	{
		Id = "male-heist-summer",
		Name = "Heist Summer (male)",
		BaseSkin = "CID_NPC_Athena_Commando_M_HeistSummerIsland",
		BodyMesh = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Meshes/M_MED_Heist_Summer",
		HeadMesh = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Meshes/Parts/M_MED_Heist_Summer_FaceAcc",
		BodyTexture = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Textures/T_M_MED_Heist_Summer_Body_D",
		BodyNormal = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Textures/T_M_MED_Heist_Summer_Body_N",
		BodySpecular = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Textures/T_M_MED_Heist_Summer_Body_S",
		BodyMask = "/Game/Characters/Player/Male/Medium/Bodies/M_MED_Heist_Summer/Textures/T_M_MED_Heist_Summer_Body_M"
	};

	public static readonly FnRigInfo MaleJunkSamurai = new FnRigInfo
	{
		Id = "male-junk-samurai",
		Name = "Junk Samurai (male)",
		BaseSkin = "CID_838_Athena_Commando_M_JunkSamurai",
		BodyMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Meshes/Junk_Samurai",
		HeadMesh = "/BRCosmetics/Characters/Player/Male/Medium/Heads/M_MED_ASN_Toshiro_Head_01/Meshes/Male_Medium_Toshiro_Head",
		HiddenMeshes = new string[2] { "/BRCosmetics/Accessories/Hats/M_MED_JunkSamurai_FaceAcc/Meshes/Junk_Samurai", "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Meshes/Parts/M_MED_Toshiro_Hair_FaceAcc" },
		BodyTexture = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Textures/T_Junk_Samurai_D",
		BodyNormal = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Textures/T_Junk_Samurai_N",
		BodySpecular = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Textures/T_Junk_Samurai_S",
		BodyMask = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Textures/T_Junk_Samurai_M",
		BodyEffects = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Textures/T_Junk_Samurai_FX",
		BodyMaterial = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_Med_JunkSamurai/Materials/M_Med_JunkSamurai",
		Tints = new FnTint[4]
		{
			FnTint.Off(3.300041f, 3.169856f, 3.5f),
			FnTint.Off(0f, 0.346704f, 1f),
			FnTint.Off(0.450712f, 0.197259f, 1.2f),
			FnTint.Soft(0.2f, 0.120885f, 0.103125f, 1f)
		}
	};

	public static readonly FnRigInfo MaleShockWave = new FnRigInfo
	{
		Id = "male-shock-wave",
		Name = "Shock Wave (male)",
		BaseSkin = "CID_932_Athena_Commando_M_ShockWave",
		BodyMesh = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Meshes/M_MED_Shock_Wave_Commando",
		HeadMesh = "/Game/Characters/Player/Male/Medium/Heads/M_Med_Mannequin/Meshes/M_MED_AUX_Mannequin_Head_01",
		HiddenMeshes = new string[1] { "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Meshes/Parts/M_MED_Shock_Wave_Commando_FaceAcc" },
		BodyTexture = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_D",
		BodyNormal = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_N",
		BodySpecular = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_S",
		BodyMask = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_M",
		BodyEmissive = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_E",
		BodyEffects = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Textures/T_Shock_Wave_Shock_Wave_FX",
		BodyMaterial = "/BRCosmetics/Characters/Player/Male/Medium/Bodies/M_MED_ShockWave_Commando/Material/M_MED_ShockWave_Body",
		Tints = new FnTint[4]
		{
			FnTint.Off(0.395474f, 1f, 0.980102f),
			FnTint.Off(0.42557f, 1f, 0.960176f),
			FnTint.Off(0.410744f, 0.920155f, 1f),
			FnTint.Off(1f, 0.503828f, 0.242888f)
		}
	};

	public static readonly IReadOnlyList<FnRigInfo> Carriers = new _003C_003Ez__ReadOnlyArray<FnRigInfo>(new FnRigInfo[7] { MaleRebirth, MaleLars, MaleJumpstart, MaleDeco, MaleHeistSummer, MaleJunkSamurai, MaleShockWave });

	public string Id { get; init; } = "";

	public string Name { get; init; } = "";

	public string BaseSkin { get; init; } = "";

	public string BodyMesh { get; init; } = "";

	public string HeadMesh { get; init; } = "";

	public string[] HiddenMeshes { get; init; } = Array.Empty<string>();

	public string BodyTexture { get; init; } = "";

	public string HeadTexture { get; init; } = "";

	public string BodyNormal { get; init; } = "";

	public string BodySpecular { get; init; } = "";

	public string BodyMask { get; init; } = "";

	public string BodyEffects { get; init; } = "";

	public string BodyEmissive { get; init; } = "";

	public string BodyMaterial { get; init; } = "";

	public FnTint[] Tints { get; init; } = Array.Empty<FnTint>();

	public byte[] HiddenColorCodes { get; init; } = Array.Empty<byte>();

	public int BodyTextureWidth { get; init; } = 256;

	public int BodyTextureHeight { get; init; } = 256;

	public int HeadTextureWidth { get; init; } = 256;

	public int HeadTextureHeight { get; init; } = 128;

	public int MaxLods { get; init; } = 3;

	public IEnumerable<string> Packages => new string[10] { BodyMesh, HeadMesh, BodyTexture, HeadTexture, BodyNormal, BodySpecular, BodyMask, BodyEffects, BodyEmissive, BodyMaterial }.Where((string p) => p.Length > 0).Concat(HiddenMeshes);

	public bool IsDefault => this == MaleRebirth;

	public static FnRigInfo? Find(string id)
	{
		return Carriers.FirstOrDefault((FnRigInfo r) => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	public static FnRigInfo? ForSkin(string cosmeticId)
	{
		return Carriers.FirstOrDefault((FnRigInfo r) => string.Equals(r.BaseSkin, cosmeticId, StringComparison.OrdinalIgnoreCase));
	}

	public static FnRigInfo? ForBody(string bodyMesh)
	{
		return Carriers.FirstOrDefault((FnRigInfo r) => string.Equals(r.BodyMesh, bodyMesh, StringComparison.OrdinalIgnoreCase));
	}
}
