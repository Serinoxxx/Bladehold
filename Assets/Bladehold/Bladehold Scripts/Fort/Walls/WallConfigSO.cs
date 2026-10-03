using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     Shared look and layout for plan-17 walls: each material tier's name and the material swapped onto the
///     model, the doorway size, collider height/thickness, door and collapse timing, and the damage-stage
///     thresholds with their looping smoke/fire. The model itself (one hand-placed building, rubble, door
///     leaf, spikes, element fixtures) lives in the wall inside WallPlot.prefab. Numbers (HP, costs) live on
///     <see cref="FortUpgradeConfigSO" />.
/// </summary>
[CreateAssetMenu(fileName = "WallConfig", menuName = "Scriptable Objects/Defenses/Wall Config")]
public class WallConfigSO : ScriptableObject
{
    [Serializable]
    public class TierLook
    {
        public string displayName = "Wood";
        [Tooltip("Swapped into every model material slot that uses any tier's material (Castle_Wall_01/02/03). Other slots (wood, iron) keep theirs.")]
        public Material material;
    }

    [Tooltip("Wood, stone, metal (index = material tier).")]
    public TierLook[] tiers = new TierLook[3];

    [Header("Layout")]
    [Tooltip("Width of the doorway in the middle of the wall. At least 2.4 m so the Large Enemy agent (r 1) fits through when it's open.")]
    [Min(2.4f)] public float doorWidth = 3f;
    [Tooltip("Collider height for the wall sections and door.")]
    [Min(1f)] public float wallHeight = 3.5f;
    [Tooltip("Collider depth (thickness) of the wall.")]
    [Min(0.3f)] public float wallThickness = 0.8f;
    [Tooltip("Seconds for the door to sink into the ground (or rise back up).")]
    [FormerlySerializedAs("doorSwingSeconds")]
    [Min(0.05f)] public float doorSlideSeconds = 0.8f;

    [Tooltip("Seconds for a fallen wall's model to sink into the ground, leaving its rubble.")]
    [Min(0.1f)] public float collapseSinkSeconds = 1.4f;

    [Header("Damage smoke (looping, visible from a distance)")]
    [Tooltip("At or below this HP fraction: light damage (wisps of smoke).")]
    [Range(0f, 1f)] public float lightDamageAt = 0.75f;
    [Tooltip("At or below this HP fraction: medium damage (thick smoke, embers).")]
    [Range(0f, 1f)] public float mediumDamageAt = 0.5f;
    [Tooltip("At or below this HP fraction: heavy damage (fire).")]
    [Range(0f, 1f)] public float heavyDamageAt = 0.25f;
    [Tooltip("Looping effect prefabs (any number of particle systems); spawned once per side of the doorway.")]
    public GameObject lightDamageVfx;
    public GameObject mediumDamageVfx;
    public GameObject heavyDamageVfx;

    public TierLook Tier(int tier)
    {
        if (tiers == null || tiers.Length == 0) return null;
        return tiers[Mathf.Clamp(tier, 0, tiers.Length - 1)];
    }

    /// <summary>True when <paramref name="material" /> is one of the tier materials (so it's swapped on upgrade).</summary>
    public bool IsTierMaterial(Material material)
    {
        if (material == null || tiers == null) return false;
        foreach (TierLook t in tiers) if (t != null && t.material == material) return true;
        return false;
    }

    /// <summary>0 intact, 1 light, 2 medium, 3 heavy.</summary>
    public int DamageStage(float healthFraction)
    {
        if (healthFraction <= heavyDamageAt) return 3;
        if (healthFraction <= mediumDamageAt) return 2;
        if (healthFraction <= lightDamageAt) return 1;
        return 0;
    }

    public GameObject DamageVfx(int stage)
    {
        return stage switch
        {
            1 => lightDamageVfx,
            2 => mediumDamageVfx,
            3 => heavyDamageVfx,
            _ => null
        };
    }
}
