using System.Collections.Generic;
using UnityEngine;

public class DamageTrigger : MonoBehaviour
{
    [SerializeField] DamageTriggerSO damageTriggerSO;
    [SerializeField] DamageSO damageSO;

    [Tooltip("The attacker that wields this trigger; it is never damaged by it. Leave empty to use the nearest IDamageable up the parent hierarchy (e.g. the character this weapon is attached to).")]
    [SerializeField] GameObject owner;

    [Header("Player stats")]
    [Tooltip("When true, this is the player's weapon: damage and range come from Player.Instance.Stats (base + upgrades) instead of the raw SOs, and crit/knockback/charge are applied. Leave false for any non-player hitbox.")]
    [SerializeField] bool readsPlayerStats = false;

    [Tooltip("Base critical-strike damage multiplier, registered as the CritMultiplier stat base. 2 = crits deal double.")]
    [SerializeField] float baseCritMultiplier = 2f;

    [Tooltip("Optional: the player's attack/charge component. When set, the swing's charge multiplier scales damage. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] PlayerAttack playerAttack;

    readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    readonly Collider[] overlapBuffer = new Collider[32];

    IDamageable ownerDamageable;
    PlayerStats stats;

    bool isActive;
    float deactivateTime;

    bool anyError = false;

    private void Start()
    {
        if (damageTriggerSO == null)
        {
            Debug.LogError("DamageTriggerSO is not assigned in the inspector.");
            anyError = true;
        }

        if (damageSO == null)
        {
            Debug.LogError("DamageSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Resolve the wielder so the trigger never damages whoever swings it. The weapon is usually
        // a child of the attacker, so default to the nearest IDamageable up the hierarchy.
        GameObject ownerRoot = owner != null ? owner : gameObject;
        ownerDamageable = ownerRoot.GetComponentInParent<IDamageable>();

        if (readsPlayerStats)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
            if (stats == null)
            {
                Debug.LogError("DamageTrigger reads player stats but Player.Instance.Stats is missing.");
                anyError = true;
                return;
            }

            // Register the authored SO values as the stat bases; upgrades layer on top of these without
            // ever mutating the (shared, editor-persisted) SO assets.
            stats.SetBase(StatType.SwordDamage, damageSO.baseDamage);
            stats.SetBase(StatType.SwordRange, damageTriggerSO.radius);
            stats.SetBase(StatType.CritChance, 0f);
            stats.SetBase(StatType.CritMultiplier, baseCritMultiplier);
            stats.SetBase(StatType.KnockbackForce, 0f);
        }
    }

    public void Activate()
    {
        if (anyError) return;

        isActive = true;
        deactivateTime = Time.time + damageTriggerSO.duration;
        hitTargets.Clear();
    }

    void Update()
    {
        if (anyError) return;
        if (!isActive) return;

        ApplyDamageInRadius();

        if (hitTargets.Count >= damageTriggerSO.maxHits || Time.time >= deactivateTime)
        {
            isActive = false;
        }
    }

    void ApplyDamageInRadius()
    {
        float radius = readsPlayerStats ? stats.GetValue(StatType.SwordRange) : damageTriggerSO.radius;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            if (hitTargets.Count >= damageTriggerSO.maxHits) return;

            if (!overlapBuffer[i].TryGetComponent(out IDamageable damageable))
            {
                damageable = overlapBuffer[i].GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            // Never damage the wielder of this trigger.
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(BuildDamage());
        }
    }

    Damage BuildDamage()
    {
        if (!readsPlayerStats)
        {
            return new Damage
            {
                value = damageSO.baseDamage,
                isCritical = damageSO.isCritical,
            };
        }

        float value = stats.GetValue(StatType.SwordDamage);

        // Roll crit per target so each enemy in a sweep crits independently.
        bool crit = Random.value < stats.GetValue(StatType.CritChance);
        if (crit)
        {
            value *= stats.GetValue(StatType.CritMultiplier);
        }

        // Charged-attack bonus, latched by PlayerAttack at the moment this swing started.
        if (playerAttack != null)
        {
            value *= playerAttack.AttackDamageMultiplier;
        }

        return new Damage
        {
            value = value,
            isCritical = crit,
            knockbackForce = stats.GetValue(StatType.KnockbackForce),
            sourcePosition = transform.position,
        };
    }
}
