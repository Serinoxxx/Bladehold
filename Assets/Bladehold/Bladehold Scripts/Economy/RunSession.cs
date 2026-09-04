using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Maintains in-run state that survives scene transitions between the Battle Scene and Rest Area Scene.
///     Tracking includes in-run gold, active wave/round, rest visits, elemental lock, temporary shop buffs,
///     and bonus max health from Troll Hearts. Reset on run death or victory.
/// </summary>
public static class RunSession
{
    public static int InRunGold { get; set; }
    public static int CurrentWave { get; set; } = 1;
    public static int RestVisitsCount { get; set; } = 0;
    
    // Elemental Ability Slots Mapping (SlotName -> ElementType)
    public static Dictionary<string, string> ElementalSlots { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> GetActiveElements()
    {
        return new HashSet<string>(ElementalSlots.Values, StringComparer.OrdinalIgnoreCase);
    }

    public static void SetElementalSlot(string slotName, string element)
    {
        if (string.IsNullOrEmpty(slotName) || string.IsNullOrEmpty(element)) return;
        ElementalSlots[slotName] = element;
        OnElementalSlotChanged?.Invoke(slotName, element);
    }

    public static event Action<string, string> OnElementalSlotChanged;

    public static int CrystalWaterWavesRemaining { get; set; } = 0;
    public static int SpecialHerbsWavesRemaining { get; set; } = 0;
    public static float PlayerBonusMaxHealth { get; set; } = 0f;
    public static float PlayerHealthRatio { get; set; } = 1f;
    public static int DraftRerollsRemaining { get; set; } = 0;

    public static int CurrentRound => Mathf.Clamp((CurrentWave - 1) / 3 + 1, 1, 4);

    public static event Action<int> OnInRunGoldChanged;

    /// <summary>
    ///     Checks if a permanent meta-progression perk is owned in SaveData.
    /// </summary>
    public static bool HasMetaPerk(string perkId)
    {
        SaveData data = SaveSystem.Load();
        return data != null && data.purchasedMetaPerks != null && data.purchasedMetaPerks.Contains(perkId);
    }

    /// <summary>
    ///     Checks if a specific weapon is unlocked in SaveData.
    /// </summary>
    public static bool IsWeaponUnlocked(string weaponId)
    {
        SaveData data = SaveSystem.Load();
        return data != null && data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponId);
    }

    public static readonly Dictionary<string, int> InRunUpgradeLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public static string ActiveUltimateId { get; set; } = null;
    public static bool SecondWindUsed { get; set; } = false;

    public static int GetUpgradeLevel(string upgradeId)
    {
        if (string.IsNullOrEmpty(upgradeId)) return 0;
        return InRunUpgradeLevels.TryGetValue(upgradeId, out int lvl) ? lvl : 0;
    }

    public static void SetUpgradeLevel(string upgradeId, int level)
    {
        if (string.IsNullOrEmpty(upgradeId)) return;
        InRunUpgradeLevels[upgradeId] = level;
    }

    /// <summary>
    ///     Initializes or resets state for a brand new run.
    /// </summary>
    public static void StartNewRun()
    {
        CurrentWave = 1;
        RestVisitsCount = 0;
        ElementalSlots.Clear();
        CrystalWaterWavesRemaining = 0;
        SpecialHerbsWavesRemaining = 0;
        PlayerBonusMaxHealth = 0f;
        PlayerHealthRatio = 1f;
        DraftRerollsRemaining = HasMetaPerk("master_tactician") ? 1 : 0;
        SecondWindUsed = false;
        InRunUpgradeLevels.Clear();
        ActiveUltimateId = null;

        // War Chest perk grants 75 starting gold
        InRunGold = HasMetaPerk("war_chest") ? 75 : 0;
        OnInRunGoldChanged?.Invoke(InRunGold);
    }

    /// <summary>
    ///     Clears run state on defeat or return to meta area.
    /// </summary>
    public static void ClearRun()
    {
        StartNewRun();
    }

    /// <summary>
    ///     Rehydrates the player's in-run upgrades and meta perks across scene transitions
    ///     (Survivors Scene <-> Rest Area Scene).
    /// </summary>
    public static void RestoreInRunUpgrades(Player player)
    {
        if (player == null) return;

        // 1. Reapply bonus health from Troll Hearts & health ratio
        if (player.Health != null)
        {
            if (PlayerBonusMaxHealth > 0f)
            {
                player.Health.SetMaxHealth(player.Health.MaxHealth + PlayerBonusMaxHealth);
            }
            if (PlayerHealthRatio > 0f && PlayerHealthRatio <= 1f)
            {
                player.Health.Heal(player.Health.MaxHealth * PlayerHealthRatio);
            }

            // 2. Wire Second Wind permanent meta perk (revive once per run with 50% HP)
            if (HasMetaPerk("second_wind"))
            {
                player.Health.TryPreventDeath -= HandleSecondWindRevive;
                player.Health.TryPreventDeath += HandleSecondWindRevive;
            }
        }

        // 3. Reapply Agility permanent meta perk (+1 dash charge / faster cooldown)
        if (HasMetaPerk("agility") && player.Stats != null)
        {
            player.Stats.AddModifier(StatType.DodgeCooldown, ModifierKind.Percent, -0.25f);
        }

        // 4. Reapply all drafted mid-run upgrades from InRunUpgradeLevels
        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null && player.Stats != null)
        {
            foreach (var kvp in InRunUpgradeLevels)
            {
                string id = kvp.Key;
                int level = kvp.Value;
                DraftUpgradeDefinition def = draftService.GetById(id);
                if (def != null && def.effects != null)
                {
                    foreach (SkillEffect effect in def.effects)
                    {
                        player.Stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(level));
                    }
                }
            }
        }

        // 5. Reapply Active Ultimate
        if (!string.IsNullOrEmpty(ActiveUltimateId) && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateUnlocked, 1f);
            DraftUpgradeService.ConfigureUltimateHandler(player, ActiveUltimateId);
            Debug.Log($"[RunSession] Restored Active Ultimate: {ActiveUltimateId}");
        }

        Debug.Log($"[RunSession] Restored {InRunUpgradeLevels.Count} in-run upgrades on player in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}.");
    }

    private static bool HandleSecondWindRevive()
    {
        if (SecondWindUsed) return false;
        SecondWindUsed = true;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.Revive(Player.Instance.Health.MaxHealth * 0.5f);
            Debug.Log("[RunSession] Second Wind triggered! Player revived with 50% max HP.");
            return true;
        }
        return false;
    }

    public static void AddInRunGold(int amount)
    {
        if (amount <= 0) return;

        // Greed perk gives +10% gold from all sources
        if (HasMetaPerk("greed"))
        {
            amount = Mathf.RoundToInt(amount * 1.10f);
        }

        InRunGold += amount;
        OnInRunGoldChanged?.Invoke(InRunGold);
    }

    public static bool TrySpendInRunGold(int amount)
    {
        if (amount <= 0) return true;
        if (InRunGold < amount) return false;

        InRunGold -= amount;
        OnInRunGoldChanged?.Invoke(InRunGold);
        return true;
    }

    public static void AddGoblinBlood(int amount)
    {
        if (amount <= 0) return;
        SaveData data = SaveSystem.Load();
        data.goblinBlood += amount;
        SaveSystem.Save(data);
    }

    public static void AddOrcishMetal(int amount)
    {
        if (amount <= 0) return;
        SaveData data = SaveSystem.Load();
        data.orcishMetal += amount;
        SaveSystem.Save(data);
    }

    public static string GetElementInSlot(string slotName)
    {
        if (ElementalSlots.TryGetValue(slotName, out string element))
        {
            return element;
        }
        return "";
    }

    /// <summary>
    ///     Called when a wave ends to decrement temporary shop buff wave counters.
    /// </summary>
    public static void OnWaveCompleted()
    {
        if (CrystalWaterWavesRemaining > 0)
        {
            CrystalWaterWavesRemaining--;
        }
        if (SpecialHerbsWavesRemaining > 0)
        {
            SpecialHerbsWavesRemaining--;
        }
    }
}
