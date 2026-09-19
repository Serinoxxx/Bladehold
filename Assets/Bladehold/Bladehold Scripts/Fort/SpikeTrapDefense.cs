using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Spike Trap: High damage ground defense that impales enemies walking across it.
/// </summary>
public class SpikeTrapDefense : DefenseStructure
{
    [Header("Spike Trap Config")]
    [SerializeField] private float triggerRadius = 2.6f;
    [SerializeField] private float impaleDamage = 85f;
    [SerializeField] private float triggerCooldown = 1.2f;
    [SerializeField] private AudioClip impaleSfx;
    [SerializeField] private GameObject impaleVfxPrefab;

    private float nextTriggerTime = 0f;

    protected override void Awake()
    {
        defenseType = FortDefenseType.Spikes;
        supplyPerAction = 2;
        base.Awake();
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                impaleDamage = 85f;
                triggerCooldown = 1.2f;
                break;
            case 2:
                impaleDamage = 145f;
                triggerCooldown = 0.85f;
                break;
            case 3:
                impaleDamage = 230f;
                triggerCooldown = 0.55f;
                break;
        }
    }

    private void Update()
    {
        if (Time.time < nextTriggerTime) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, triggerRadius);
        List<Health> enemiesOnSpikes = new List<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || enemiesOnSpikes.Contains(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            enemiesOnSpikes.Add(h);
        }

        if (enemiesOnSpikes.Count > 0)
        {
            TriggerImpale(enemiesOnSpikes);
            nextTriggerTime = Time.time + triggerCooldown;
        }
    }

    private void TriggerImpale(List<Health> targets)
    {
        if (!ConsumeSupply()) return;

        if (impaleSfx != null)
        {
            AudioSource.PlayClipAtPoint(impaleSfx, transform.position);
        }

        if (impaleVfxPrefab != null)
        {
            Instantiate(impaleVfxPrefab, transform.position, Quaternion.identity);
        }

        foreach (Health target in targets)
        {
            if (target != null && !target.IsDead)
            {
                Damage dmg = new Damage
                {
                    value = impaleDamage,
                    type = DamageType.sharp,
                    isPlayerDamage = true,
                    sourcePosition = transform.position,
                    source = Player.Instance != null ? Player.Instance.Damageable : null
                };
                target.ReceiveDamage(dmg);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
#endif
}
