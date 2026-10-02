using UnityEngine;

/// <summary>
///     Costs and numbers for the plan-17 upgrade wheel: tower fire-rate tiers, tower spike rings,
///     wall material tiers, repair, wall spikes and the elemental imbues (towers and walls). One asset,
///     referenced by the scene's <see cref="DefenseSceneRules" />, so every tower and wall in a defense
///     scene reads the same numbers. Wall art per tier lives on <see cref="WallConfigSO" />.
/// </summary>
[CreateAssetMenu(fileName = "FortUpgradeConfig", menuName = "Scriptable Objects/Defenses/Fort Upgrade Config")]
public class FortUpgradeConfigSO : ScriptableObject
{
    [Header("Elements (towers and walls)")]
    [Tooltip("Crystals of the matching element to imbue one tower or wall. One element per structure, locked once chosen.")]
    [Min(0)] public int elementCrystalCost = 3;

    [Header("Tower: fire rate")]
    [Tooltip("Supply for fire-rate tier 1, 2, 3 (array length = number of tiers).")]
    public int[] fireRateCosts = { 30, 45, 60 };
    [Tooltip("Fire-rate multiplier at tier 1, 2, 3 (1.25 = 25% more shots).")]
    public float[] fireRateMultipliers = { 1.25f, 1.5f, 1.8f };

    [Header("Tower: spike ring")]
    [Min(0)] public int towerSpikesCost = 25;
    [Tooltip("Ring of spikes at the tower base: damages enemies inside this flat radius (it covers the turret's blind spot).")]
    [Min(0f)] public float towerSpikeRadius = 3.2f;
    [Min(0f)] public float towerSpikeDamage = 6f;
    [Tooltip("Seconds between spike pulses.")]
    [Min(0.1f)] public float towerSpikeInterval = 1f;
    [Tooltip("Visual + TowerSpikeRing component, parented to the tower when bought.")]
    public TowerSpikeRing towerSpikeRingPrefab;

    [Header("Tower: elements")]
    [Tooltip("Ice-element towers apply this much Ice per hit (EnemyStatusManager stacks).")]
    [Min(0f)] public float towerIceStacks = 0.5f;

    [Header("Wall: build and materials")]
    [Tooltip("Supply to build a wood wall on an empty (or rubble) wall plot during prep.")]
    [Min(0)] public int wallBuildCost = 40;
    [Tooltip("Supply to upgrade wood to stone, then stone to metal.")]
    public int[] wallMaterialCosts = { 60, 90 };
    [Tooltip("Max HP for wood, stone, metal.")]
    public float[] wallMaterialHealth = { 150f, 300f, 500f };
    [Tooltip("NavMesh area cost of a standing, shut wall's plot (1 = no penalty). The penalty is roughly (cost - 1) x the plot's depth in metres: enemies further than that from another open bridge attack the wall instead of walking round. Siege units ignore it.")]
    [Min(1f)] public float wallAreaCost = 12f;

    [Header("Wall: repair")]
    [Min(1)] public int wallRepairAmount = 10;
    [Min(0)] public int wallRepairCost = 5;

    [Header("Wall: spikes")]
    [Min(0)] public int wallSpikesCost = 25;
    [Tooltip("Damage dealt back to an attacker for every melee hit it lands on a spiked wall.")]
    [Min(0f)] public float wallSpikeDamage = 1f;

    [Header("Wall: elemental defences")]
    [Tooltip("Triggered when the wall is hit; one effect per cooldown.")]
    [Min(0.1f)] public float wallElementCooldown = 4f;
    [Tooltip("Enemies on the outside face within this distance are caught by the wall's element.")]
    [Min(0f)] public float wallElementReach = 4.5f;
    [Tooltip("Fire: boiling-oil pool poured on attackers (BurningOilZone).")]
    public BurningOilZone boilingOilPrefab;
    [Tooltip("Ice: icy-water pool spilled on attackers (slows, builds to a freeze).")]
    public WallIcyWaterZone icyWaterPrefab;
    [Tooltip("Lightning: damage per arc and how many attackers one discharge chains through.")]
    [Min(0f)] public float wallLightningDamage = 12f;
    [Min(1)] public int wallLightningTargets = 3;

    [Header("Wheel icons")]
    public Sprite refillIcon;
    public Sprite fireRateIcon;
    public Sprite spikesIcon;
    public Sprite repairIcon;
    public Sprite materialIcon;
    public Sprite deconstructIcon;
    public Sprite fireIcon;
    public Sprite iceIcon;
    public Sprite lightningIcon;

    public int FireRateTiers => fireRateCosts != null ? fireRateCosts.Length : 0;

    /// <summary>Fire-rate multiplier at <paramref name="tier" /> (0 = none bought).</summary>
    public float FireRateMultiplier(int tier)
    {
        if (tier <= 0 || fireRateMultipliers == null || fireRateMultipliers.Length == 0) return 1f;
        return fireRateMultipliers[Mathf.Clamp(tier - 1, 0, fireRateMultipliers.Length - 1)];
    }

    public int WallMaxTier => wallMaterialHealth != null ? wallMaterialHealth.Length - 1 : 0;

    public float WallHealth(int tier)
    {
        if (wallMaterialHealth == null || wallMaterialHealth.Length == 0) return 150f;
        return wallMaterialHealth[Mathf.Clamp(tier, 0, wallMaterialHealth.Length - 1)];
    }

    public Sprite ElementIcon(StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => fireIcon,
            StructureElement.Ice => iceIcon,
            StructureElement.Lightning => lightningIcon,
            _ => null
        };
    }
}
