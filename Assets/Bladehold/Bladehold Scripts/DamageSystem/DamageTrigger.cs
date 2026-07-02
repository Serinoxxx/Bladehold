using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageTrigger : MonoBehaviour
{
    public enum DetectionMode
    {
        Sphere,
        BladeSweep,
    }

    [SerializeField] DamageTriggerSO damageTriggerSO;
    [SerializeField] DamageSO damageSO;

    [Tooltip("The attacker that wields this trigger; it is never damaged by it. Leave empty to use the nearest IDamageable up the parent hierarchy (e.g. the character this weapon is attached to).")]
    [SerializeField] GameObject owner;

    [Header("Detection")]
    [Tooltip("Sphere: OverlapSphere check each physics step (radial hitboxes like the Death Nova blast). Blade Sweep: raycasts along a line of sample points between Blade Base/Blade Tip each physics step, from each point's previous position to its current one (the sword).")]
    [SerializeField] DetectionMode detectionMode = DetectionMode.Sphere;

    [Tooltip("Blade Sweep only: hilt-side end of the blade.")]
    [SerializeField] Transform bladeBase;

    [Tooltip("Blade Sweep only: tip-side end of the blade.")]
    [SerializeField] Transform bladeTip;

    [Tooltip("Blade Sweep only: number of sample points along the blade at 100% Sword Range. Scales with the Sword Range stat when 'Reads Player Stats' is on.")]
    [SerializeField] int basePointCount = 5;

    [Tooltip("Blade Sweep only: layers the sweep can hit.")]
    [SerializeField] LayerMask hitLayers = ~0;

    [Header("Player stats")]
    [Tooltip("When true, this is the player's weapon: damage and range come from Player.Instance.Stats (base + upgrades) instead of the raw SOs, and crit/knockback/charge/cap are applied. Leave false for any non-player hitbox.")]
    [SerializeField] bool readsPlayerStats = false;

    [Tooltip("Base critical-strike damage multiplier, registered as the CritMultiplier stat base. 2 = crits deal double.")]
    [SerializeField] float baseCritMultiplier = 2f;

    [Tooltip("Optional: the player's attack/charge component. When set, the swing's charge multiplier scales damage. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] PlayerAttack playerAttack;

    [Tooltip("Knockback impulse applied by this hitbox when NOT reading player stats (e.g. an ability hitbox like the Death Nova). Ignored when 'Reads Player Stats' is on, where knockback comes from PlayerStats instead.")]
    [SerializeField] float knockbackForce = 0f;

    /// <summary>Fired once per unique target actually damaged by this activation, with the world point it was hit at.</summary>
    public event Action<IDamageable, Damage, Vector3> OnHit;

    /// <summary>Fired when this activation would damage one more unique target than its cap allows; the activation ends immediately without damaging that target.</summary>
    public event Action OnBlocked;

    const int MaxBladePoints = 32;

    readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    readonly Collider[] overlapBuffer = new Collider[32];
    readonly Vector3[] previousPointPositions = new Vector3[MaxBladePoints];

    IDamageable ownerDamageable;
    PlayerStats stats;

    bool isActive;
    float deactivateTime;
    int activePointCount;

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

        if (detectionMode == DetectionMode.BladeSweep && (bladeBase == null || bladeTip == null))
        {
            Debug.LogError("Blade Sweep detection mode requires both Blade Base and Blade Tip to be assigned.");
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
            // ever mutating the (shared, editor-persisted) SO assets. Sword Range is a unitless multiplier
            // (base 1.0, same convention as MoveSpeed/SprintSpeed) rather than a raw distance - it scales
            // both the visual blade length (via transform scale) and the blade-sweep sample point count.
            stats.SetBase(StatType.SwordDamage, damageSO.baseDamage);
            stats.SetBase(StatType.SwordRange, 1f);
            stats.SetBase(StatType.CritChance, 0f);
            stats.SetBase(StatType.CritMultiplier, baseCritMultiplier);
            stats.SetBase(StatType.KnockbackForce, 0f);
            stats.SetBase(StatType.ChargeKnockbackBonus, 0f);
            stats.SetBase(StatType.MaxHitsPerSwing, damageTriggerSO.maxHits);

            stats.OnStatChanged += HandleStatChanged;
            ApplyRangeScale();
        }
    }

    void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnStatChanged -= HandleStatChanged;
        }
    }

    void HandleStatChanged(StatType type)
    {
        if (type == StatType.SwordRange)
        {
            ApplyRangeScale();
        }
    }

    void ApplyRangeScale()
    {
        transform.localScale = Vector3.one * stats.GetValue(StatType.SwordRange);
    }

    public void Activate()
    {
        if (anyError) return;

        isActive = true;
        deactivateTime = Time.time + damageTriggerSO.duration;
        hitTargets.Clear();

        if (detectionMode == DetectionMode.BladeSweep)
        {
            float rangeMultiplier = readsPlayerStats ? stats.GetValue(StatType.SwordRange) : 1f;
            activePointCount = Mathf.Clamp(Mathf.RoundToInt(basePointCount * rangeMultiplier), 2, MaxBladePoints);

            // Seed each point's "previous" position so the first sweep this activation doesn't raycast
            // from a stale rest position left over from the last swing.
            for (int i = 0; i < activePointCount; i++)
            {
                previousPointPositions[i] = BladePointPosition(i);
            }
        }
    }

    void FixedUpdate()
    {
        if (anyError) return;
        if (!isActive) return;

        if (detectionMode == DetectionMode.BladeSweep)
        {
            SweepBlade();
        }
        else
        {
            ApplyDamageInRadius();
        }

        if (Time.time >= deactivateTime)
        {
            isActive = false;
        }
    }

    void SweepBlade()
    {
        int cap = EffectiveMaxHits();

        for (int i = 0; i < activePointCount; i++)
        {
            Vector3 previousPos = previousPointPositions[i];
            Vector3 currentPos = BladePointPosition(i);
            previousPointPositions[i] = currentPos;

            Vector3 delta = currentPos - previousPos;
            float distance = delta.magnitude;
            if (distance <= 0.0001f) continue;

            if (!Physics.Raycast(previousPos, delta / distance, out RaycastHit hit, distance, hitLayers, QueryTriggerInteraction.Collide))
            {
                continue;
            }

            if (!TryHitTarget(hit.collider, cap, hit.point)) return;
        }
    }

    void ApplyDamageInRadius()
    {
        int cap = EffectiveMaxHits();
        int count = Physics.OverlapSphereNonAlloc(transform.position, damageTriggerSO.radius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            if (!TryHitTarget(overlapBuffer[i], cap, transform.position)) return;
        }
    }

    /// <summary>
    ///     Resolves the collider to an <see cref="IDamageable" /> and, if it's a genuine new target (not the
    ///     wielder, not already hit this activation), either damages it or - if the cap is already full -
    ///     blocks the activation. Returns false only when the activation should stop immediately (blocked).
    /// </summary>
    bool TryHitTarget(Collider collider, int cap, Vector3 hitPoint)
    {
        if (!collider.TryGetComponent(out IDamageable damageable))
        {
            damageable = collider.GetComponentInParent<IDamageable>();
        }

        if (damageable == null) return true;
        // Never damage the wielder of this trigger.
        if (damageable == ownerDamageable) return true;
        if (hitTargets.Contains(damageable)) return true;

        if (hitTargets.Count >= cap)
        {
            OnBlocked?.Invoke();
            isActive = false;
            return false;
        }

        hitTargets.Add(damageable);
        Damage damage = BuildDamage();
        damageable.ReceiveDamage(damage);
        OnHit?.Invoke(damageable, damage, hitPoint);
        return true;
    }

    int EffectiveMaxHits()
    {
        return readsPlayerStats ? Mathf.RoundToInt(stats.GetValue(StatType.MaxHitsPerSwing)) : damageTriggerSO.maxHits;
    }

    Vector3 BladePointPosition(int index)
    {
        float t = activePointCount > 1 ? (float)index / (activePointCount - 1) : 0f;
        return Vector3.Lerp(bladeBase.position, bladeTip.position, t);
    }

    Damage BuildDamage()
    {
        if (!readsPlayerStats)
        {
            return new Damage
            {
                value = damageSO.baseDamage,
                isCritical = damageSO.isCritical,
                knockbackForce = knockbackForce,
                sourcePosition = transform.position,
            };
        }

        float value = stats.GetValue(StatType.SwordDamage);

        // Roll crit per target so each enemy in a sweep crits independently.
        bool crit = UnityEngine.Random.value < stats.GetValue(StatType.CritChance);
        if (crit)
        {
            value *= stats.GetValue(StatType.CritMultiplier);
        }

        float knockback = stats.GetValue(StatType.KnockbackForce);

        // Charged-attack bonuses, latched by PlayerAttack at the moment this swing started.
        if (playerAttack != null)
        {
            value *= playerAttack.AttackDamageMultiplier;
            knockback *= 1f + playerAttack.ChargeLevel * stats.GetValue(StatType.ChargeKnockbackBonus);
        }

        return new Damage
        {
            value = value,
            isCritical = crit,
            knockbackForce = knockback,
            sourcePosition = transform.position,
        };
    }
}
