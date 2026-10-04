using UnityEngine;

/// <summary>Thunderstorm tunables (enchanted forest / graveyard storm biome). Read by <see cref="ThunderstormEvent" />.</summary>
[CreateAssetMenu(fileName = "ThunderstormEvent", menuName = "Scriptable Objects/World Events/Thunderstorm")]
public class ThunderstormEventSO : WorldEventSO
{
    [Header("Strikes")]
    [Tooltip("Seconds between bolts (random between x and y). Bolts come singly but often.")]
    public Vector2 strikeInterval = new Vector2(0.7f, 1.3f);
    [Tooltip("Chance a bolt aims at the player's spot.")]
    [Range(0f, 1f)] public float targetPlayerChance = 0.25f;
    [Tooltip("Chance a bolt aims at an enemy pack (after the player roll).")]
    [Range(0f, 1f)] public float targetEnemyChance = 0.55f;
    [Tooltip("Random bolts land within this distance of the player.")]
    public float scatterRadius = 20f;

    [Header("Each bolt")]
    [Tooltip("Seconds the blue circle shows before the bolt lands.")]
    public float telegraphSeconds = 1.3f;
    public float strikeRadius = 2.6f;
    public WorldHazardDamage strikeDamage = new WorldHazardDamage
    {
        playerDamage = 7f, enemyDamage = 25f, enemyMaxHealthFraction = 0.3f, enemyDamageCap = 120f, knockbackForce = 2f
    };

    [Header("Arc")]
    [Tooltip("Each bolt arcs on to this many extra enemies near the impact (not the player).")]
    [Min(0)] public int arcTargets = 2;
    public float arcRange = 6f;
    [Tooltip("Arc damage as a fraction of the bolt's enemy damage.")]
    [Range(0f, 1f)] public float arcDamageFraction = 0.5f;
}
