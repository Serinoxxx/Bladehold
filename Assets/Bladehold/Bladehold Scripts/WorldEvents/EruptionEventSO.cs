using UnityEngine;

/// <summary>Volcano Eruption tunables (Desert / fire biome). Read by <see cref="EruptionEvent" />.</summary>
[CreateAssetMenu(fileName = "EruptionEvent", menuName = "Scriptable Objects/World Events/Eruption")]
public class EruptionEventSO : WorldEventSO
{
    [Header("Volleys")]
    [Tooltip("Seconds between meteor volleys (random between x and y).")]
    public Vector2 volleyInterval = new Vector2(1.8f, 2.8f);
    [Tooltip("Meteors per volley (random between x and y, inclusive).")]
    public Vector2Int meteorsPerVolley = new Vector2Int(2, 3);
    [Tooltip("Chance a meteor aims at where the player stands (the rest go for enemy packs or random ground).")]
    [Range(0f, 1f)] public float targetPlayerChance = 0.25f;
    [Tooltip("Chance a meteor aims at an enemy pack (after the player roll).")]
    [Range(0f, 1f)] public float targetEnemyChance = 0.45f;
    [Tooltip("Random meteors land within this distance of the player.")]
    public float scatterRadius = 22f;

    [Header("Each meteor")]
    [Tooltip("Seconds the red circle shows before impact. Long enough to walk out of at normal speed.")]
    public float telegraphSeconds = 1.6f;
    [Tooltip("Seconds of the visible fall (the last part of the telegraph).")]
    public float fallSeconds = 0.6f;
    public float impactRadius = 3.5f;
    public WorldHazardDamage impactDamage = new WorldHazardDamage
    {
        playerDamage = 6f, enemyDamage = 30f, enemyMaxHealthFraction = 0.35f, enemyDamageCap = 150f, knockbackForce = 4f
    };

    [Header("Magma pool left behind")]
    [Range(0f, 1f)] public float poolChance = 0.5f;
    public float poolRadius = 2.2f;
    public float poolSeconds = 5f;
    [Tooltip("Damage per tick (ticks every 0.5 s) to anything standing in the pool.")]
    public WorldHazardDamage poolTickDamage = new WorldHazardDamage
    {
        playerDamage = 1.5f, enemyDamage = 4f, enemyMaxHealthFraction = 0.04f, enemyDamageCap = 25f
    };
}
