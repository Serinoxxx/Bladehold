using UnityEngine;

/// <summary>Stampede tunables (green / kingdom biome). Read by <see cref="StampedeEvent" />.</summary>
[CreateAssetMenu(fileName = "StampedeEvent", menuName = "Scriptable Objects/World Events/Stampede")]
public class StampedeEventSO : WorldEventSO
{
    [Header("Runs")]
    [Tooltip("Seconds between herd runs (random between x and y).")]
    public Vector2 runInterval = new Vector2(5f, 8f);
    [Tooltip("Chance a lane is laid through the player's spot (the rest go through an enemy pack).")]
    [Range(0f, 1f)] public float targetPlayerChance = 0.5f;

    [Header("Lane")]
    [Tooltip("Seconds the lane shows (with rumble and dust at its start) before the herd arrives.")]
    public float telegraphSeconds = 2.2f;
    public float laneWidth = 5f;
    [Tooltip("Preferred lane length; it's shortened to stay on unbroken NavMesh (no running through walls or over ravines).")]
    public float laneLength = 60f;
    [Tooltip("Lanes shorter than this after the NavMesh check are rejected and another direction is tried.")]
    public float minLaneLength = 24f;

    [Header("Herd")]
    [Tooltip("Metres per second. Fast: the telegraph is the warning, not the herd itself.")]
    public float herdSpeed = 20f;
    [Tooltip("Animals per run (random between x and y, inclusive).")]
    public Vector2Int herdSize = new Vector2Int(5, 7);
    [Tooltip("How far back the herd straggles, in metres (the back rank arrives this much later).")]
    public float herdDepth = 6f;
    public WorldHazardDamage trampleDamage = new WorldHazardDamage
    {
        playerDamage = 10f, enemyDamage = 40f, enemyMaxHealthFraction = 0.5f, enemyDamageCap = 200f, knockbackForce = 7f
    };
}
