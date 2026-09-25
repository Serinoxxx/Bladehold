using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Single source of truth for what the Steam demo lets players reach: which weapons, armour sets
///     and mounts are usable, the highest meta-perk tier, and the campaign tier the demo ends on.
///     Lives at <c>Resources/DemoConfig.asset</c>; untick <see cref="demoEnabled" /> (or delete the
///     asset) and every gate opens for the full game.
///     Locked content is still shown (pedestals, perk rows, map nodes), just marked as not in the demo.
/// </summary>
[CreateAssetMenu(fileName = "DemoConfig", menuName = "Scriptable Objects/Demo Config")]
public class DemoConfigSO : ScriptableObject
{
    private const string ResourcePath = "DemoConfig";

    [Tooltip("Master switch. Off = full game, every gate below is ignored.")]
    public bool demoEnabled = true;

    [Header("Loadout")]
    [Tooltip("Weapons usable in the demo (free or unlockable with Orcish Metal as normal). Every other weapon shows 'Locked for demo'.")]
    public List<WeaponDefinitionSO> allowedWeapons = new List<WeaponDefinitionSO>();

    [Tooltip("Armour sets usable in the demo. Every other set shows 'Locked for demo'.")]
    public List<ArmourSetSO> allowedArmourSets = new List<ArmourSetSO>();

    [Tooltip("Mounts usable in the demo (the basic warhorse). Variants show on pedestals but are locked.")]
    public List<MountDefinitionSO> allowedMounts = new List<MountDefinitionSO>();

    [Header("Meta Perks")]
    [Tooltip("Highest meta-perk tier the demo allows. Higher tiers are visible but can't be unlocked.")]
    [Min(1)] public int maxMetaPerkTier = 1;

    [Header("Campaign")]
    [Tooltip("Clearing any node on this tier ends the demo (Thanks for playing screen, then back to the Meta Area). Nodes on later tiers are visible but locked.")]
    [Min(1)] public int campaignCutoffTier = 4;

    [Tooltip("Steam store page opened by the demo end screen's Wishlist button.")]
    public string steamStoreUrl = "";

    [Header("Text")]
    [Tooltip("Label shown on pedestals, perk cards and map nodes that aren't in the demo.")]
    public string lockedLabel = "LOCKED FOR DEMO";

    [Tooltip("Short prompt/tooltip line for locked content.")]
    public string lockedPrompt = "Not available in the demo";

    private static DemoConfigSO cached;
    private static bool loaded;

    /// <summary>The demo config when the demo is on; null for the full game.</summary>
    public static DemoConfigSO Active
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                cached = Resources.Load<DemoConfigSO>(ResourcePath);
                if (cached == null)
                {
                    Debug.LogError($"[DemoConfigSO] No DemoConfig asset at Resources/{ResourcePath}. Running as the full game with no demo gating.");
                }
            }
            return cached != null && cached.demoEnabled ? cached : null;
        }
    }

    public static bool IsDemo => Active != null;

    public static string LockedLabel => Active != null ? Active.lockedLabel : "";
    public static string LockedPrompt => Active != null ? Active.lockedPrompt : "";

    public static bool IsWeaponLocked(WeaponDefinitionSO weapon) =>
        weapon != null && IsWeaponIdLocked(weapon.id);

    public static bool IsWeaponIdLocked(string weaponId)
    {
        DemoConfigSO cfg = Active;
        if (cfg == null || string.IsNullOrEmpty(weaponId)) return false;
        for (int i = 0; i < cfg.allowedWeapons.Count; i++)
        {
            WeaponDefinitionSO w = cfg.allowedWeapons[i];
            if (w != null && string.Equals(w.id, weaponId, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static bool IsArmourLocked(ArmourSetSO armour)
    {
        DemoConfigSO cfg = Active;
        if (cfg == null || armour == null) return false;
        for (int i = 0; i < cfg.allowedArmourSets.Count; i++)
        {
            ArmourSetSO a = cfg.allowedArmourSets[i];
            if (a != null && string.Equals(a.id, armour.id, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static bool IsMountLocked(MountDefinitionSO mount)
    {
        DemoConfigSO cfg = Active;
        if (cfg == null || mount == null) return false;
        for (int i = 0; i < cfg.allowedMounts.Count; i++)
        {
            MountDefinitionSO m = cfg.allowedMounts[i];
            if (m != null && string.Equals(m.id, mount.id, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static bool IsMetaTierLocked(int tier)
    {
        DemoConfigSO cfg = Active;
        return cfg != null && tier > cfg.maxMetaPerkTier;
    }

    /// <summary>True for nodes past the demo's last tier: shown on the map, never playable.</summary>
    public static bool IsCampaignNodeLocked(CampaignNodeSO node)
    {
        DemoConfigSO cfg = Active;
        return cfg != null && node != null && node.tierIndex > cfg.campaignCutoffTier;
    }

    /// <summary>True when clearing this node ends the demo.</summary>
    public static bool IsDemoCutoffNode(CampaignNodeSO node)
    {
        DemoConfigSO cfg = Active;
        return cfg != null && node != null && node.tierIndex >= cfg.campaignCutoffTier;
    }

#if UNITY_EDITOR
    // Domain reload may be off: re-read the asset (and its toggle) on every Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = null;
        loaded = false;
    }
#endif
}
