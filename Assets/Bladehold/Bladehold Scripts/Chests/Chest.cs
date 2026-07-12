using UnityEngine;

/// <summary>
///     A smashable loot chest: an <see cref="IDamageable" /> (via <see cref="Health" />) the player
///     breaks with the sword. On death it drops guaranteed gold (a <see cref="Coin" />) plus, on a
///     roll, one bonus item from its <see cref="ChestLootTableSO" /> — the
///     <see cref="LightningOrbDropper" /> pattern generalised to a weighted roster of existing pickups.
///
///     Per-hit and break juice live on the <see cref="Health" />'s <c>damageFeedback</c>/
///     <c>deathFeedback</c> MMF_Players (Health plays them, so no separate hit-feedback script is
///     needed), plus an over-the-top break VFX spawned here from an assigned prefab. Chests carry no
///     <see cref="ImpulseReceiver" /> and are kinematic, so the sword's impulse fling never moves them.
///     A health bar is just an <c>MMHealthBar</c> + <see cref="HealthBarUI" /> on the same object.
///
///     Listens to its own <see cref="Health.OnDied" />; Health stays unaware of loot (the
///     <see cref="CoinDropper" /> convention).
/// </summary>
[RequireComponent(typeof(Health))]
public class Chest : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private ChestLootTableSO lootTable;
    [Tooltip("Coin pickup prefab spawned for the guaranteed gold drop.")]
    [SerializeField] private Coin coinPrefab;
    [Tooltip("Optional over-the-top break VFX prefab, spawned at the chest on death (assign your explosion prefab).")]
    [SerializeField] private GameObject breakVfxPrefab;
    [Tooltip("World-space offset from the chest where loot and the break VFX spawn.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Loot is scattered within this radius so multiple drops don't stack in one spot.")]
    [SerializeField] private float scatterRadius = 0.6f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Chest has no Health component.");
            anyError = true;
        }
        if (lootTable == null)
        {
            Debug.LogError("Chest has no ChestLootTableSO assigned.");
            anyError = true;
        }
        if (coinPrefab == null)
        {
            Debug.LogError("Chest has no Coin prefab assigned for its guaranteed gold drop.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, transform.position + dropOffset, Quaternion.identity);
        }

        // Guaranteed gold.
        int gold = lootTable.RollGold();
        if (gold > 0)
        {
            Coin coin = Instantiate(coinPrefab, ScatterPoint(), Quaternion.identity);
            coin.SetAmount(gold);
        }

        // Optional single bonus item, weighted across the roster.
        GameObject bonus = lootTable.RollBonusItem();
        if (bonus != null)
        {
            Instantiate(bonus, ScatterPoint(), Quaternion.identity);
        }
    }

    private Vector3 ScatterPoint()
    {
        Vector2 jitter = Random.insideUnitCircle * scatterRadius;
        return transform.position + dropOffset + new Vector3(jitter.x, 0f, jitter.y);
    }
}
