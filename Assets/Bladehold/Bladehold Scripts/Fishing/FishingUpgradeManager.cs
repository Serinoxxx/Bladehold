using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Manages the active session upgrade levels for the 6 fishing minigame draft cards.
///     Resets on each visit to the Fishing Pond.
/// </summary>
public class FishingUpgradeManager : MonoBehaviour
{
    public static FishingUpgradeManager Instance { get; private set; }

    private readonly Dictionary<FishingUpgradeType, int> upgradeLevels = new Dictionary<FishingUpgradeType, int>();

    public event Action<FishingUpgradeType, int> OnUpgradeApplied;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ResetUpgrades();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetUpgrades()
    {
        upgradeLevels.Clear();
        foreach (FishingUpgradeType type in Enum.GetValues(typeof(FishingUpgradeType)))
        {
            upgradeLevels[type] = 0;
        }
    }

    public int GetLevel(FishingUpgradeType type)
    {
        return upgradeLevels.TryGetValue(type, out int lvl) ? lvl : 0;
    }

    public void ApplyUpgrade(FishingUpgradeType type)
    {
        int current = GetLevel(type);
        if (current < 4)
        {
            upgradeLevels[type] = current + 1;
            OnUpgradeApplied?.Invoke(type, current + 1);
            Debug.Log($"[FishingUpgradeManager] Upgraded {type} to Level {current + 1}");
        }
    }

    // --- Computed Stat Accessors ---

    public int BounceCount => GetLevel(FishingUpgradeType.BounceShot);

    public int FishsploshionDamage => GetLevel(FishingUpgradeType.Fishsploshion);
    public float FishsploshionRadius => FishsploshionDamage > 0 ? 3.5f : 0f;

    public float IceyWaterSlowRatio => GetLevel(FishingUpgradeType.IceyWater) switch
    {
        1 => 0.20f,
        2 => 0.30f,
        3 => 0.40f,
        4 => 0.50f,
        _ => 0f
    };

    public int PierceCount => GetLevel(FishingUpgradeType.FishSkewer) switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        4 => 999,
        _ => 0
    };

    public int BleedMaxStacks => GetLevel(FishingUpgradeType.Bleed) switch
    {
        1 => 2,
        2 => 3,
        3 => 4,
        4 => 5,
        _ => 0
    };

    public float BleedDps => BleedMaxStacks > 0 ? 1f : 0f;
    public float BleedDuration => 5f;

    public float FatFishBonusPercent => GetLevel(FishingUpgradeType.FatFish) switch
    {
        1 => 0.025f,
        2 => 0.050f,
        3 => 0.075f,
        4 => 0.100f,
        _ => 0f
    };

    /// <summary>
    ///     Generates up to 3 randomized candidate draft upgrade cards that have not reached max tier.
    /// </summary>
    public List<FishingUpgradeCardInfo> RollDraftChoices(int count = 3)
    {
        List<FishingUpgradeType> pool = new List<FishingUpgradeType>();
        foreach (FishingUpgradeType type in Enum.GetValues(typeof(FishingUpgradeType)))
        {
            if (GetLevel(type) < 4)
            {
                pool.Add(type);
            }
        }

        // Shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, pool.Count);
            (pool[i], pool[r]) = (pool[r], pool[i]);
        }

        int selectCount = Mathf.Min(count, pool.Count);
        List<FishingUpgradeCardInfo> choices = new List<FishingUpgradeCardInfo>();
        for (int i = 0; i < selectCount; i++)
        {
            FishingUpgradeType type = pool[i];
            int nextLvl = GetLevel(type) + 1;
            choices.Add(new FishingUpgradeCardInfo
            {
                type = type,
                title = GetCardTitle(type),
                description = GetCardDescription(type, nextLvl),
                currentLevel = GetLevel(type),
                maxLevel = 4
            });
        }

        return choices;
    }

    public static string GetCardTitle(FishingUpgradeType type) => type switch
    {
        FishingUpgradeType.BounceShot => "Bounce Shot",
        FishingUpgradeType.Fishsploshion => "Fishsploshion",
        FishingUpgradeType.IceyWater => "Icey Water",
        FishingUpgradeType.FishSkewer => "Fish Skewer",
        FishingUpgradeType.Bleed => "Bleed",
        FishingUpgradeType.FatFish => "Fat Fish",
        _ => type.ToString()
    };

    public static string GetCardDescription(FishingUpgradeType type, int level) => type switch
    {
        FishingUpgradeType.BounceShot => $"Arrows bounce between nearby fish {level} time{(level > 1 ? "s" : "")}.",
        FishingUpgradeType.Fishsploshion => $"Killing a fish detonates an explosion dealing {level} damage in a small area.",
        FishingUpgradeType.IceyWater => $"The pond water chills fish, reducing swim speed by {level * 10 + 10}%.",
        FishingUpgradeType.FishSkewer => level switch
        {
            1 => "Arrows pierce through 1 additional fish.",
            2 => "Arrows pierce through 2 additional fish.",
            3 => "Arrows pierce through 3 additional fish.",
            _ => "Arrows pierce through ALL fish in their path."
        },
        FishingUpgradeType.Bleed => $"Damaging a fish causes it to bleed for 1 DPS for 5s (stacks up to {level + 1}x).",
        FishingUpgradeType.FatFish => $"Fish are {level * 2.5f:0.#}% larger, yielding more resources.",
        _ => ""
    };
}
