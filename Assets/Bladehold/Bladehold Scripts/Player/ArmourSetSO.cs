using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Asset-side definition of an armour set. Defines the visual character model
///     to swap onto the player rig, and a list of stat modifiers to apply.
/// </summary>
[CreateAssetMenu(fileName = "ArmourSetSO", menuName = "Scriptable Objects/ArmourSetSO")]
public class ArmourSetSO : ScriptableObject
{
    [Tooltip("Stable identifier persisted in the save file. Never rename once shipped.")]
    public string id = "default_armour";

    [Tooltip("Player-facing name shown in UI.")]
    public string displayName;

    [Tooltip("Short player-facing blurb for UI.")]
    [TextArea] public string description;

    [Tooltip("Permanent cost in Orcish Metal to unlock this armour set.")]
    public int orcishMetalUnlockCost = 10;

    [Tooltip("The icon shown on the HUD or menu for this armour set.")]
    public Sprite icon;

    [Tooltip("Character model prefab swapped onto the shared player rig at scene load — a Synty Sidekick sharing the rig's base skeleton (bone names match; outfit-only bones like cape danglers are grafted on automatically).")]
    public GameObject characterModelPrefab;

    [System.Serializable]
    public struct ArmourStatModifier
    {
        [Tooltip("The stat to modify (e.g. MaxHealth, MoveSpeed).")]
        public StatType stat;

        [Tooltip("Whether to add a flat amount or a percentage multiplier.")]
        public ModifierKind kind;

        [Tooltip("The amount to add or multiply.")]
        public float amount;
    }

    [Header("Stat Modifiers")]
    [Tooltip("List of stat modifiers applied when this armour set is equipped.")]
    public List<ArmourStatModifier> statModifiers = new List<ArmourStatModifier>();

    /// <summary>
    ///     Returns a list of cleanly formatted strings describing each perk/modifier in this armour set.
    /// </summary>
    public List<string> GetFormattedModifiersList()
    {
        var list = new List<string>();
        if (statModifiers == null) return list;

        foreach (var mod in statModifiers)
        {
            list.Add(FormatModifier(mod));
        }
        return list;
    }

    /// <summary>
    ///     Returns a multiline string of bullet-pointed perks for UI display.
    /// </summary>
    public string GetFormattedModifiersText()
    {
        var list = GetFormattedModifiersList();
        if (list.Count == 0) return "None";
        return string.Join("\n", list.ConvertAll(s => $"• {s}"));
    }

    private static string FormatModifier(ArmourStatModifier mod)
    {
        string sign = mod.amount >= 0 ? "+" : "";
        string valueStr = mod.kind == ModifierKind.Percent 
            ? $"{sign}{mod.amount:P0}" 
            : $"{sign}{mod.amount:0.##}";

        switch (mod.stat)
        {
            case StatType.PlayerMaxHealthMultiplier:
                return $"{sign}{mod.amount:P0} Max Health";
            case StatType.SwordDamage:
                return $"{sign}{mod.amount:P0} Sword Damage";
            case StatType.MoveSpeed:
                return $"{sign}{mod.amount:P0} Move Speed";
            case StatType.SprintSpeed:
                return $"{sign}{mod.amount:P0} Sprint Speed";
            case StatType.CritChance:
                return $"{sign}{mod.amount:P0} Crit Chance";
            case StatType.CritMultiplier:
                return $"{sign}{mod.amount:P0} Crit Damage";
            case StatType.LifeStealPercent:
                return $"{sign}{mod.amount:P0} Lifesteal";
            case StatType.BlockCooldown:
                return $"{mod.amount:0.#}s Auto-Block Cooldown";
            case StatType.KnockbackForce:
                return $"{valueStr} Knockback";
            case StatType.AllDamageMultiplier:
                return $"{sign}{mod.amount:P0} All Damage";
            default:
                // Prettify PascalCase stat name
                string rawName = mod.stat.ToString();
                string prettyName = System.Text.RegularExpressions.Regex.Replace(rawName, "(\\B[A-Z])", " $1");
                return $"{valueStr} {prettyName}";
        }
    }
}
