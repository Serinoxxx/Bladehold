using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Ground area of boiling, burning tar poured from fort oil cauldrons.
///     Applies fire damage ticks and heavy slow (50%) to all enemies inside the area.
/// </summary>
public class BurningOilZone : MonoBehaviour
{
    [Header("Area & Timing")]
    [SerializeField] private float radius = 4f;
    [SerializeField] private float duration = 8f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float damagePerTick = 10f;
    [SerializeField] private float slowFraction = 0.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Audio")]
    [SerializeField] private AudioClip sizzleSound;

    private float lifetimeTimer;
    private float tickTimer;
    private readonly Collider[] hitBuffer = new Collider[32];

    public void Init(float rad, float dur, float dps, float slow)
    {
        radius = rad;
        duration = dur;
        damagePerTick = dps * tickInterval;
        slowFraction = slow;
    }

    private void Start()
    {
        if (sizzleSound != null)
        {
            AudioSource.PlayClipAtPoint(sizzleSound, transform.position, 0.7f);
        }
    }

    private void Update()
    {
        lifetimeTimer += Time.deltaTime;
        if (lifetimeTimer >= duration)
        {
            Destroy(gameObject);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            ApplyBurnTick();
        }
    }

    private void ApplyBurnTick()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
        HashSet<Health> processedHealths = new HashSet<Health>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            Health health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead || processedHealths.Contains(health)) continue;

            // Don't damage player
            if (Player.Instance != null && health.transform.root == Player.Instance.transform.root) continue;

            processedHealths.Add(health);

            // Apply elemental fire damage
            Damage damage = new Damage
            {
                value = damagePerTick,
                type = DamageType.elemental,
                sourcePosition = transform.position,
                isPlayerDamage = true
            };
            health.ReceiveDamage(damage);

            // Apply slow
            SlowStatus slow = SlowStatus.GetOrAdd(col);
            if (slow != null)
            {
                slow.ApplySlow(slowFraction, 1.2f);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
