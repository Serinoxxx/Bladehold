using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A ground segment spawned along the dash path when the player has the Blazing Trail upgrade
///     (StatType.FireBlazingTrailDPS). Deals elemental fire damage and applies the Fire status to enemies.
/// </summary>
public class FireTrailSegment : MonoBehaviour
{
    private const int MaxHits = 16;
    private readonly Collider[] hitBuffer = new Collider[MaxHits];

    private float dps;
    private float duration;
    private GameObject vfxInstance;
    private Coroutine burnRoutine;

    public void Init(float fireDPS, float lifeDuration = 3.0f, GameObject vfxPrefab = null)
    {
        dps = fireDPS;
        duration = lifeDuration;

        if (vfxPrefab != null)
        {
            vfxInstance = Instantiate(vfxPrefab, transform.position, transform.rotation, transform);
        }

        burnRoutine = StartCoroutine(BurnRoutine());
    }

    private IEnumerator BurnRoutine()
    {
        float elapsed = 0f;
        const float tickInterval = 0.33f;

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;

            TickDamage(tickInterval);
        }

        Destroy(gameObject);
    }

    private void TickDamage(float interval)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, 1.2f, hitBuffer);
        HashSet<Health> processed = new HashSet<Health>();

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            Health enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead || processed.Contains(enemyHealth))
            {
                continue;
            }

            // Exclude player
            if (Player.Instance != null && (enemyHealth == Player.Instance.Health || enemyHealth.gameObject == Player.Instance.gameObject))
            {
                continue;
            }

            processed.Add(enemyHealth);

            float damageAmount = dps * interval;
            if (Player.Instance != null && Player.Instance.Stats != null)
            {
                damageAmount *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
            }

            enemyHealth.ReceiveDamage(new Damage
            {
                value = damageAmount,
                type = DamageType.elemental,
                elementId = "Fire",
                sourcePosition = transform.position,
                source = Player.Instance != null ? Player.Instance.Damageable : null,
                isPlayerDamage = true
            });

            EnemyStatusManager.GetOrAdd(enemyHealth)?.ApplyStatus("Fire");
        }
    }

    private void OnDestroy()
    {
        if (burnRoutine != null)
        {
            StopCoroutine(burnRoutine);
        }

        if (vfxInstance != null)
        {
            Destroy(vfxInstance);
        }
    }
}
