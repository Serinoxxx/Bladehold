using System;
using UnityEngine;

/// <summary>
///     How many Arcane Cores a captain drops (plan 21 phase 5): a base count, plus a bonus from a banner
///     tier up. Captain SOs (and Fraglob's inspector) carry one, so each captain can be tuned on its own.
/// </summary>
[Serializable]
public class CaptainArcaneCoreReward
{
    [Tooltip("Arcane Cores dropped at any tier.")]
    [Min(0)] public int baseCores = 1;

    [Tooltip("Tier from which the bonus cores are added on top.")]
    public BannerDifficultyTier bonusFromTier = BannerDifficultyTier.Nightmare;

    [Tooltip("Extra cores at bonusFromTier and above.")]
    [Min(0)] public int bonusCores = 1;

    public int CoresFor(BannerDifficultyTier tier) => baseCores + (tier >= bonusFromTier ? bonusCores : 0);
}

/// <summary>Death rewards shared by every captain controller.</summary>
public static class CaptainRewards
{
    /// <summary>
    ///     Grants the captain's Arcane Cores for a death at <paramref name="tier" /> and returns how many were
    ///     granted. Call it once from the captain's <see cref="Health.OnDied" /> handler.
    /// </summary>
    public static int GrantArcaneCores(CaptainArcaneCoreReward reward, BannerDifficultyTier tier, string captainName)
    {
        if (EnemyPrewarmer.IsRehearsing) return 0;
        if (reward == null)
        {
            Debug.LogError($"[CaptainRewards] {captainName} has no Arcane Core reward set, so it drops none.");
            return 0;
        }

        int cores = reward.CoresFor(tier);
        if (cores > 0)
        {
            RunSession.AddArcaneCores(cores);
            Debug.Log($"[CaptainRewards] {captainName} dropped {cores} Arcane Core(s) (tier {tier}).");
        }
        return cores;
    }
}
