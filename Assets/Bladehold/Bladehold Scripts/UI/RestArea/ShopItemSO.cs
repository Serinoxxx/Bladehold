using UnityEngine;

public enum ShopItemEffectType
{
    HealInstant,
    MaxHealthRun,
    MoveSpeedTemporary,
    WaveEndHealTemporary,
    AmmoRefill,
    RetiredUnlockUltimate, // unused since plan 21 phase 5 (ultimates are always unlocked); kept so later values keep their serialized ints
    ReplaceMount,   // a new warhorse after the run's horse died (ShopUI pins it while RunSession.MountLost)
    HealMount,      // heals the run's warhorse by effectValue (0..1 fraction of its max health)
    RunStatModifier, // adds effectValue to `stat` (as `statKind`) for the rest of the run
    ArcaneCore      // adds effectValue (at least 1) Arcane Cores; ShopUI pins it in the featured row
}

/// <summary>
///     Configurable Rest Area Shop item definition.
/// </summary>
[CreateAssetMenu(fileName = "ShopItemSO", menuName = "Scriptable Objects/ShopItemSO")]
public class ShopItemSO : ScriptableObject
{
    public string itemId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public int goldCost = 10;
    public Sprite icon;
    public ShopItemEffectType effectType;
    public float effectValue = 5f;
    public int durationWaves = 5;

    [Header("RunStatModifier only")]
    [Tooltip("Stat the RunStatModifier effect raises for the rest of the run (effectValue is the amount).")]
    public StatType stat;
    public ModifierKind statKind = ModifierKind.Flat;
}
