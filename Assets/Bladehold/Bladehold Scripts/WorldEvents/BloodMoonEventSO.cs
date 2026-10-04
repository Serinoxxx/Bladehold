using UnityEngine;

/// <summary>Blood Moon tunables (graveyard biome). Read by <see cref="BloodMoonEvent" />.</summary>
[CreateAssetMenu(fileName = "BloodMoonEvent", menuName = "Scriptable Objects/World Events/Blood Moon")]
public class BloodMoonEventSO : WorldEventSO
{
    [Header("The restless dead")]
    [Tooltip("Chance (0-1) an enemy killed during the Blood Moon rises again as a skeleton.")]
    [Range(0f, 1f)] public float riseChance = 0.35f;
    [Tooltip("Seconds the grave glow shows on the corpse before the skeleton climbs out. Kill it fast, or step away.")]
    public float riseDelaySeconds = 1.8f;
    [Tooltip("At most this many risen skeletons alive at once.")]
    [Min(1)] public int maxRisenAlive = 10;
    [Tooltip("Only corpses within this distance of the player rise, so the player sees every one.")]
    public float riseRadiusFromPlayer = 35f;
    [Tooltip("Enemies tougher than this (max health, after wave scaling) stay dead: elites, captains and bosses never rise. 150 matches the game loop's elite threshold.")]
    public float maxRisableHealth = 150f;
    [Tooltip("Roster ids a corpse can rise as (picked at random). Must be in the scene's roster.")]
    public string[] risenEnemyIds = { "skeleton_soldier", "skeleton_soldier_shield" };
    [Tooltip("Risen skeletons are frailer than wave skeletons: their max health is scaled by this.")]
    [Range(0.1f, 1f)] public float risenHealthMultiplier = 0.6f;
    [Tooltip("Radius of the grave-glow marker on the corpse.")]
    public float markerRadius = 1.2f;
}
