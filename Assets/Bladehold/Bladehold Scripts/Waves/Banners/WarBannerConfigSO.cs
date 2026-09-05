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
    [Header("Modular Clan Buffs")]
    [Tooltip("Pool of modular Clan Buff ScriptableObjects available to spawn on War Banners.")]
    public List<WarBannerClanSO> clans = new List<WarBannerClanSO>();

    [Header("Interchangeable Rewards")]
    [Tooltip("Pool of modular Reward ScriptableObjects available to spawn on War Banners.")]
    public List<WarBannerRewardSO> rewards = new List<WarBannerRewardSO>();

    [Header("Legacy Config (Fallback)")]
    public List<BannerBuffDef> buffs = new List<BannerBuffDef>();
    public List<BannerBountyDef> bounties = new List<BannerBountyDef>();

    public List<WarBannerClanSO> GetActiveClans()
    {
        if (clans != null && clans.Count > 0)
            return clans;

        // Fallback to legacy buffs if clans list is empty
        List<WarBannerClanSO> fallback = new List<WarBannerClanSO>();
        foreach (var b in buffs)
        {
            if (b == null) continue;
            var so = ScriptableObject.CreateInstance<WarBannerClanSO>();
            so.clanName = b.clanName;
            so.buffType = b.buffType;
            so.quickFact = !string.IsNullOrEmpty(b.inGameDescription) ? b.inGameDescription : b.clanName;
            so.clanIcon = b.clanSigil;
            fallback.Add(so);
        }
        return fallback;
    }

    public List<WarBannerRewardSO> GetActiveRewards()
    {
        if (rewards != null && rewards.Count > 0)
            return rewards;

        // Fallback to legacy bounties if rewards list is empty
        List<WarBannerRewardSO> fallback = new List<WarBannerRewardSO>();
        foreach (var b in bounties)
        {
            if (b == null) continue;
            var so = ScriptableObject.CreateInstance<WarBannerRewardSO>();
            so.rewardName = b.inGameDisplay;
            so.bountyType = b.bountyType;
            so.quantityText = "x 1";
            fallback.Add(so);
        }
        return fallback;
    }

    public BannerBuffDef GetBuffDef(BannerBuffType type)
    {
        return buffs.Find(b => b.buffType == type);
    }

    public BannerBountyDef GetBountyDef(BannerBountyType type)
    {
        return bounties.Find(b => b.bountyType == type);
    }
}
