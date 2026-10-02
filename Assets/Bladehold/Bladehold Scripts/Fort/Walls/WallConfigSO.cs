using System;
using UnityEngine;

/// <summary>
///     Art and layout for plan-17 walls: the segment prefab per material tier (wood, stone, metal) and per
///     damage stage, the rubble left when a wall falls, the looping smoke/fire that shows damage from a
///     distance, and the door. <see cref="WallStructure" /> tiles the segments across the plot's width,
///     leaving the doorway in the middle. Numbers (HP, costs) live on <see cref="FortUpgradeConfigSO" />.
/// </summary>
[CreateAssetMenu(fileName = "WallConfig", menuName = "Scriptable Objects/Defenses/Wall Config")]
public class WallConfigSO : ScriptableObject
{
    [Serializable]
    public class TierArt
    {
        public string displayName = "Wood";
        [Tooltip("Wall segment, intact. Pivot at the base centre, length along local X.")]
        public GameObject segment;
        [Tooltip("Optional damaged variants (light, medium, heavy). Empty slots fall back to the previous stage.")]
        public GameObject segmentLightDamage;
        public GameObject segmentMediumDamage;
        public GameObject segmentHeavyDamage;
        [Tooltip("Length of one segment along local X, metres (measured from the prefab; used for tiling).")]
        [Min(0.5f)] public float segmentLength = 4f;
        [Tooltip("Door leaf for this tier. Pivot at the hinge, closed along local X.")]
        public GameObject door;
        [Tooltip("Rubble pile spawned per segment when this tier's wall falls.")]
        public GameObject rubble;
    }

    [Tooltip("Wood, stone, metal (index = StructureUpgradeState.materialTier).")]
    public TierArt[] tiers = new TierArt[3];

    [Header("Layout")]
    [Tooltip("Width of the doorway in the middle of the wall. At least 2.4 m so the Large Enemy agent (r 1) fits through when it's open.")]
    [Min(2.4f)] public float doorWidth = 3f;
    [Tooltip("Collider height for the wall sections and door.")]
    [Min(1f)] public float wallHeight = 3.5f;
    [Tooltip("Collider depth (thickness) of the wall.")]
    [Min(0.3f)] public float wallThickness = 0.8f;
    [Tooltip("Door swing when opened, degrees.")]
    public float doorOpenAngle = 100f;
    [Min(0.05f)] public float doorSwingSeconds = 0.6f;

    [Header("Upgrade props")]
    [Tooltip("Spike row placed along the outside face of each segment when Spikes is bought. Pivot at the base centre, length along local X.")]
    public GameObject spikesProp;
    [Tooltip("Fixture on top of the wall over the door for each element: boiling-oil cauldron, icy-water barrel, lightning rod.")]
    public GameObject fireFixture;
    public GameObject iceFixture;
    public GameObject lightningFixture;

    [Header("Damage smoke (looping, visible from a distance)")]
    [Tooltip("At or below this HP fraction: light damage (wisps of smoke).")]
    [Range(0f, 1f)] public float lightDamageAt = 0.75f;
    [Tooltip("At or below this HP fraction: medium damage (thick smoke, embers).")]
    [Range(0f, 1f)] public float mediumDamageAt = 0.5f;
    [Tooltip("At or below this HP fraction: heavy damage (fire).")]
    [Range(0f, 1f)] public float heavyDamageAt = 0.25f;
    public ParticleSystem lightDamageVfx;
    public ParticleSystem mediumDamageVfx;
    public ParticleSystem heavyDamageVfx;

    public TierArt Tier(int tier)
    {
        if (tiers == null || tiers.Length == 0) return null;
        return tiers[Mathf.Clamp(tier, 0, tiers.Length - 1)];
    }

    /// <summary>0 intact, 1 light, 2 medium, 3 heavy.</summary>
    public int DamageStage(float healthFraction)
    {
        if (healthFraction <= heavyDamageAt) return 3;
        if (healthFraction <= mediumDamageAt) return 2;
        if (healthFraction <= lightDamageAt) return 1;
        return 0;
    }

    /// <summary>The segment prefab for a tier at a damage stage, falling back to the nearest less-damaged one.</summary>
    public GameObject SegmentFor(int tier, int stage)
    {
        TierArt art = Tier(tier);
        if (art == null) return null;
        if (stage >= 3 && art.segmentHeavyDamage != null) return art.segmentHeavyDamage;
        if (stage >= 2 && art.segmentMediumDamage != null) return art.segmentMediumDamage;
        if (stage >= 1 && art.segmentLightDamage != null) return art.segmentLightDamage;
        return art.segment;
    }

    public GameObject ElementFixture(StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => fireFixture,
            StructureElement.Ice => iceFixture,
            StructureElement.Lightning => lightningFixture,
            _ => null
        };
    }

    public ParticleSystem DamageVfx(int stage)
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
