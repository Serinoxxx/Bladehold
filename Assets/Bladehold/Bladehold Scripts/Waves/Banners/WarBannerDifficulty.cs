using System;
using UnityEngine;

/// <summary>
///     Difficulty tiers for War Banners in Bladehold.
///     Standard = 1 skull (1x rewards)
///     Enraged = 2 skulls (2x rewards, Captain summons with War Cry)
///     Nightmare = 3 skulls (4x rewards, Captain summons with Shockwave Stomp)
///     Omega = 4 skulls (8x rewards, Captain summons with Bulwark/Frenzy)
/// </summary>
public enum BannerDifficultyTier
{
    Standard = 1,
    Enraged = 2,
    Nightmare = 3,
    Omega = 4
}

public static class BannerDifficultyHelper
{
    /// <summary>
    ///     Reward multiplier for the chosen difficulty tier (1x, 2x, 4x, 8x).
    /// </summary>
    public static int GetRewardMultiplier(BannerDifficultyTier tier)
    {
        switch (tier)
        {
            case BannerDifficultyTier.Enraged: return 2;
            case BannerDifficultyTier.Nightmare: return 4;
            case BannerDifficultyTier.Omega: return 8;
            case BannerDifficultyTier.Standard:
            default:
                return 1;
        }
    }

    /// <summary>
    ///     Number of skulls representing this difficulty level (1 to 4).
    /// </summary>
    public static int GetSkullCount(BannerDifficultyTier tier)
    {
        return Mathf.Clamp((int)tier, 1, 4);
    }

    /// <summary>
    ///     Formatted string containing difficulty skull icons.
    /// </summary>
    public static string GetSkullString(BannerDifficultyTier tier)
    {
        switch (tier)
        {
            case BannerDifficultyTier.Enraged:
                return "💀 💀";
            case BannerDifficultyTier.Nightmare:
                return "💀 💀 💀";
            case BannerDifficultyTier.Omega:
                return "💀 💀 💀 💀";
            case BannerDifficultyTier.Standard:
            default:
                return "💀";
        }
    }

    /// <summary>
    ///     Human-readable display name for the difficulty tier.
    /// </summary>
    public static string GetTierName(BannerDifficultyTier tier)
    {
        switch (tier)
        {
            case BannerDifficultyTier.Enraged: return "Enraged";
            case BannerDifficultyTier.Nightmare: return "Nightmare";
            case BannerDifficultyTier.Omega: return "Omega";
            case BannerDifficultyTier.Standard:
            default:
                return "Standard";
        }
    }

    /// <summary>
    ///     Stat scaling multiplier for enemy captain & wave minions (HP and Damage).
    ///     Standard: 1.0x, Enraged: 1.25x, Nightmare: 1.50x, Omega: 2.0x.
    /// </summary>
    public static float GetStatMultiplier(BannerDifficultyTier tier)
    {
        switch (tier)
        {
            case BannerDifficultyTier.Enraged: return 1.25f;
            case BannerDifficultyTier.Nightmare: return 1.50f;
            case BannerDifficultyTier.Omega: return 2.0f;
            case BannerDifficultyTier.Standard:
            default:
                return 1.0f;
        }
    }

    /// <summary>
    ///     UI accent color for the tier.
    /// </summary>
    public static Color GetTierColor(BannerDifficultyTier tier)
    {
        switch (tier)
        {
            case BannerDifficultyTier.Enraged:
                return new Color(1f, 0.6f, 0.1f); // Vibrant Amber
            case BannerDifficultyTier.Nightmare:
                return new Color(0.95f, 0.2f, 0.2f); // Crimson Red
            case BannerDifficultyTier.Omega:
                return new Color(0.75f, 0.25f, 1f); // Royal Violet / Purple
            case BannerDifficultyTier.Standard:
            default:
                return Color.white;
        }
    }

    /// <summary>
    ///     Determines the difficulty tier for a given banner based on player progression:
    ///     - Run 1 (or runsAttempted <= 1): Only Standard (Tier 1) so beginners aren't overwhelmed.
    ///     - Run 2+: Banners 1 and 2 can roll Enraged, Nightmare, or Omega.
    ///     - Always keeps slot 0 as Standard so a safe option is consistently available.
    /// </summary>
    public static BannerDifficultyTier RollTierForBanner(int runCount, int roundNumber, int bannerIndex, float roll = -1f)
    {
        // First run is always safe
        if (runCount <= 1)
        {
            return BannerDifficultyTier.Standard;
        }

        // Only offer enraged / higher difficulty on banners 1 and 2, keeping at least 1 banner safe (bannerIndex 0 = Standard)
        if (bannerIndex == 0)
        {
            return BannerDifficultyTier.Standard;
        }

        float randomVal = roll >= 0f ? roll : UnityEngine.Random.value;

        // Later rounds (Round 3 & 4) can roll Omega and Nightmare
        if (roundNumber >= 3)
        {
            if (bannerIndex == 2)
            {
                // Slot 2 in late rounds can be Omega (25%) or Nightmare (75%)
                return randomVal < 0.25f ? BannerDifficultyTier.Omega : BannerDifficultyTier.Nightmare;
            }
            else // Slot 1
            {
                return randomVal < 0.5f ? BannerDifficultyTier.Nightmare : BannerDifficultyTier.Enraged;
            }
        }
        else if (roundNumber == 2)
        {
            // Round 2: Slot 1 is Enraged, Slot 2 has a 30% chance for Nightmare
            if (bannerIndex == 2)
            {
                return randomVal < 0.30f ? BannerDifficultyTier.Nightmare : BannerDifficultyTier.Enraged;
            }
            return BannerDifficultyTier.Enraged;
        }
        else
        {
            // Round 1 (Run 2+): Slot 1 is Enraged, Slot 2 is Standard or Enraged
            if (bannerIndex == 1)
            {
                return BannerDifficultyTier.Enraged;
            }
            return randomVal < 0.5f ? BannerDifficultyTier.Enraged : BannerDifficultyTier.Standard;
        }
    }
}
