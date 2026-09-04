using UnityEngine;
using System.Collections.Generic;

public enum BannerBuffType
{
    None,
    Shield,
    Haste,
    Berserk,
    Regen,
    Armor
}

public enum BannerBountyType
{
    None,
    WeaponDraft,
    FortressDraft,
    ElementDraft,
    GoldCache,
    OrcishMetal,
    GoblinBlood,
    TrollHeart
}

[System.Serializable]
public class BannerBuffDef
{
    public BannerBuffType buffType;
    public string clanName;
    public string inGameDescription;
    public string gameplayEffect;
    public Sprite clanSigil;
}

[System.Serializable]
public class BannerBountyDef
{
    public BannerBountyType bountyType;
    public string inGameDisplay;
    public string rewardDescription;
}

[CreateAssetMenu(fileName = "WarBannerConfig", menuName = "Scriptable Objects/War Banner Config")]
public class WarBannerConfigSO : ScriptableObject
{
    public List<BannerBuffDef> buffs = new List<BannerBuffDef>();
    public List<BannerBountyDef> bounties = new List<BannerBountyDef>();

    public BannerBuffDef GetBuffDef(BannerBuffType type)
    {
        return buffs.Find(b => b.buffType == type);
    }

    public BannerBountyDef GetBountyDef(BannerBountyType type)
    {
        return bounties.Find(b => b.bountyType == type);
    }
}
