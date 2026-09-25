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

    public static void ClearElementalSlot(string slotName)
    {
        if (string.IsNullOrEmpty(slotName))
        {
            Debug.LogError("[RunSession] Cannot clear an elemental charge without a slot name.");
            return;
        }
        if (ElementalSlots.Remove(slotName))
        {
            OnElementalSlotChanged?.Invoke(slotName, "");
        }
    }

    public static int CrystalWaterWavesRemaining { get; set; } = 0;
    public static int SpecialHerbsWavesRemaining { get; set; } = 0;
    public static float PlayerBonusMaxHealth { get; set; } = 0f;
    public static float PlayerHealthRatio { get; set; } = 1f;
    public static int DraftRerollsRemaining { get; set; } = 0;

    public static int CurrentRound => Mathf.Clamp((CurrentWave - 1) / 5 + 1, 1, 5);

    public static event Action<int> OnInRunGoldChanged;
    public static int InRunSupply { get; set; } = 60;
    public static event Action<int> OnInRunSupplyChanged;

    public static int CurrentAmmo { get; set; } = 20;
    public static event Action<int, int> OnAmmoChanged;

    // Castle Campaign Progression State
    public static bool IsCampaignRun { get; set; } = false;
    public static string CampaignCurrentNodeId { get; set; } = null;
    public static List<string> CampaignCompletedNodeIds { get; } = new List<string>();
    public static List<string> CampaignAvailableNodeIds { get; } = new List<string>();

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
    public static float PlayerUltimateCharge { get; set; } = 0f;
    public static float FortressGateCurrentHealth { get; set; } = -1f;
    public static float FortressGateMaxHealth { get; set; } = -1f;

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
        foreach (string slot in new List<string>(ElementalSlots.Keys))
        {
            ClearElementalSlot(slot);
        }
        CrystalWaterWavesRemaining = 0;
        SpecialHerbsWavesRemaining = 0;
        PlayerBonusMaxHealth = 0f;
        PlayerHealthRatio = 1f;
        PlayerUltimateCharge = 0f;
        FortressGateCurrentHealth = -1f;
        FortressGateMaxHealth = -1f;
        DraftRerollsRemaining = HasMetaPerk("master_tactician") ? 1 : 0;
        SecondWindUsed = false;
        InRunUpgradeLevels.Clear();
        ActiveUltimateId = null;
        ConsumedBuffFish.Clear();

        // War Chest perk grants 75 starting gold
        InRunGold = HasMetaPerk("war_chest") ? 75 : 0;
        OnInRunGoldChanged?.Invoke(InRunGold);

        InRunSupply = 60;
        OnInRunSupplyChanged?.Invoke(InRunSupply);

        CurrentAmmo = HasMetaPerk("deep_quiver") ? 25 : 20;
        OnAmmoChanged?.Invoke(CurrentAmmo, CurrentAmmo);

        // Reset Castle Campaign State
        IsCampaignRun = false;
        CampaignCurrentNodeId = null;
        CampaignCompletedNodeIds.Clear();
        CampaignAvailableNodeIds.Clear();
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

        // 1. Reapply bonus health from Troll Hearts/shop and Armored buff fish, then the health ratio
        if (player.Health != null)
        {
            float bonusMaxHealth = PlayerBonusMaxHealth + BuffFishBonusMaxHealth;
            if (bonusMaxHealth > 0f)
            {
                player.Health.SetMaxHealth(player.Health.MaxHealth + bonusMaxHealth);
            }
            if (PlayerHealthRatio > 0f && PlayerHealthRatio <= 1f)
            {
                player.Health.SetCurrentHealth(player.Health.MaxHealth * PlayerHealthRatio);
            }

            // 2. Wire Second Wind permanent meta perk (revive once per run with 50% HP)
            if (HasMetaPerk("second_wind"))
            {
                player.Health.TryPreventDeath -= HandleSecondWindRevive;
                player.Health.TryPreventDeath += HandleSecondWindRevive;
            }
        }

        // 3. Reapply Agility permanent meta perk (+1 dash charge)
        if (HasMetaPerk("agility") && player.Stats != null)
        {
            player.Stats.AddModifier(StatType.DodgeMaxCharges, ModifierKind.Flat, 1f);
        }

        // 3b. Reapply Deep Quiver permanent meta perk (+5 max ammo)
        if (HasMetaPerk("deep_quiver") && player.Stats != null)
        {
            player.Stats.AddModifier(StatType.MaxAmmo, ModifierKind.Flat, 5f);
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

        // 6. Reapply preserved Ultimate Charge
        if (PlayerUltimateCharge > 0f)
        {
            PlayerUltimateController ult = player.transform.root.GetComponentInChildren<PlayerUltimateController>(true);
            if (ult != null)
            {
                ult.SetCharge(PlayerUltimateCharge);
            }
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

    public static void AddInRunSupply(int amount)
    {
        if (amount <= 0) return;
        InRunSupply += amount;
        OnInRunSupplyChanged?.Invoke(InRunSupply);
    }

    public static bool TrySpendInRunSupply(int amount)
    {
        if (amount <= 0) return true;
        if (InRunSupply < amount) return false;

        InRunSupply -= amount;
        OnInRunSupplyChanged?.Invoke(InRunSupply);
        return true;
    }

    public static void AddInRunAmmo(int amount)
    {
        if (amount <= 0) return;
        int max = 20;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float maxStat = Player.Instance.Stats.GetValue(StatType.MaxAmmo);
            if (maxStat > 0f) max = Mathf.RoundToInt(maxStat);
        }
        else if (HasMetaPerk("deep_quiver"))
        {
            max = 25;
        }

        CurrentAmmo = Mathf.Clamp(CurrentAmmo + amount, 0, max);
        OnAmmoChanged?.Invoke(CurrentAmmo, max);
    }

    public static bool TrySpendInRunAmmo(int amount = 1)
    {
        if (amount <= 0) return true;
        if (CurrentAmmo < amount) return false;

        CurrentAmmo -= amount;
        int max = 20;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float maxStat = Player.Instance.Stats.GetValue(StatType.MaxAmmo);
            if (maxStat > 0f) max = Mathf.RoundToInt(maxStat);
        }
        else if (HasMetaPerk("deep_quiver"))
        {
            max = 25;
        }

        OnAmmoChanged?.Invoke(CurrentAmmo, max);
        return true;
    }

    public static event Action<int> OnGoblinBloodChanged;
    public static event Action<int> OnOrcishMetalChanged;

    public static void AddGoblinBlood(int amount)
    {
        if (amount <= 0) return;
        SaveData data = SaveSystem.Load();
        data.goblinBlood += amount;
        SaveSystem.Save(data);
        OnGoblinBloodChanged?.Invoke(data.goblinBlood);
    }

    public static void AddOrcishMetal(int amount)
    {
        if (amount <= 0) return;
        SaveData data = SaveSystem.Load();
        data.orcishMetal += amount;
        SaveSystem.Save(data);
        OnOrcishMetalChanged?.Invoke(data.orcishMetal);
    }

    public static event Action<int> OnDiamondFishBonesChanged;

    public static void AddDiamondFishBones(int amount)
    {
        if (amount <= 0) return;
        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            data.diamondFishBones += amount;
            SaveSystem.Save(data);
            OnDiamondFishBonesChanged?.Invoke(data.diamondFishBones);
        }
    }

    // Buff Fish tracking (Max 3 consumed per run). ConsumedBuffFish is the only persistent record:
    // every bonus is derived from it on rehydrate, so reloading a scene never stacks a fish twice.
    public static readonly List<BuffFishType> ConsumedBuffFish = new List<BuffFishType>();
    public static bool CanConsumeBuffFish => ConsumedBuffFish.Count < 3;

    private const float ArmoredFishMaxHealth = 10f;

    /// <summary>Max HP granted by every Armored fish eaten this run; folded in by <see cref="RestoreInRunUpgrades" />.</summary>
    public static float BuffFishBonusMaxHealth
    {
        get
        {
            int armored = 0;
            for (int i = 0; i < ConsumedBuffFish.Count; i++)
            {
                if (ConsumedBuffFish[i] == BuffFishType.Armored) armored++;
            }
            return armored * ArmoredFishMaxHealth;
        }
    }

    public static bool TryConsumeBuffFish(BuffFishType type, Player player = null)
    {
        if (ConsumedBuffFish.Count >= 3) return false;
        ConsumedBuffFish.Add(type);
        ApplyBuffFishBonus(type, player, justEaten: true);
        return true;
    }

    /// <summary>
    ///     Applies one fish's bonus to the live player. <paramref name="justEaten" /> is true only at the
    ///     feast itself; on scene-load rehydrate the Armored max HP is already restored by
    ///     <see cref="RestoreInRunUpgrades" />, so only stat modifiers are re-added.
    /// </summary>
    public static void ApplyBuffFishBonus(BuffFishType type, Player player = null, bool justEaten = false)
    {
        player = (player != null) ? player : Player.Instance;
        if (player == null || player.Stats == null) return;
        var stats = player.Stats;

        switch (type)
        {
            case BuffFishType.Speedy:
                stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, 0.10f);
                break;
            case BuffFishType.Armored:
                if (justEaten && player.Health != null)
                {
                    float current = player.Health.CurrentHealth;
                    player.Health.SetMaxHealth(player.Health.MaxHealth + ArmoredFishMaxHealth);
                    player.Health.SetCurrentHealth(current + ArmoredFishMaxHealth);
                }
                break;
            case BuffFishType.Fire:
                stats.AddModifier(StatType.MageFireDamagePercent, ModifierKind.Percent, 0.10f);
                break;
            case BuffFishType.Frost:
                stats.AddModifier(StatType.IceBreakerDamageBonus, ModifierKind.Percent, 0.10f);
                break;
            case BuffFishType.Spark:
                stats.AddModifier(StatType.ChainLightningDamagePercent, ModifierKind.Percent, 0.10f);
                break;
            case BuffFishType.Savage:
                stats.AddModifier(StatType.AllDamageMultiplier, ModifierKind.Percent, 0.05f);
                break;
        }
    }

    public static void ReapplyBuffFishBonuses(Player player)
    {
        if (player == null || player.Stats == null) return;
        for (int i = 0; i < ConsumedBuffFish.Count; i++)
        {
            ApplyBuffFishBonus(ConsumedBuffFish[i], player);
        }
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
