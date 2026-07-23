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

    [Tooltip("How thick is the blade? leave at zero for raycast instead of spherecast")]
    [SerializeField] float bladeRadius = 0f; 

    [Tooltip("Blade Sweep only: number of sample points along the blade at 100% Sword Range. Scales with the Sword Range stat when 'Reads Player Stats' is on.")]
    [SerializeField] int basePointCount = 5;

    [Tooltip("Blade Sweep only: layers the sweep can hit.")]
    [SerializeField] LayerMask hitLayers = ~0;

    [Header("Player stats")]
    [Tooltip("When true, this is the player's weapon: damage and range come from Player.Instance.Stats (base + upgrades) instead of the raw SOs, and crit/knockback/charge/cap are applied. Leave false for any non-player hitbox.")]
    [SerializeField] bool readsPlayerStats = false;

    /// <summary>True when this trigger is the player's weapon hitbox (see the serialized flag above).
    /// Lets systems like <see cref="RunTelemetry" /> find the sword among the player's triggers.</summary>
    public bool ReadsPlayerStats => readsPlayerStats;

    [Tooltip("Base critical-strike damage multiplier, registered as the CritMultiplier stat base. 1.5 = crits deal 1.5x before Critical Damage skill nodes raise it.")]
    [SerializeField] float baseCritMultiplier = 1.5f;

    [Tooltip("Optional: the player's attack/charge component. When set, the swing's charge multiplier scales damage. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] PlayerAttack playerAttack;

    [Tooltip("Optional: the player's Impulse buff. When set and active, hits are stamped with impulse force/power (flinging enemies — see ImpulseReceiver) and gain the buff's stack damage multiplier. Falls back to the Player's own ImpulseBuff when left empty. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] ImpulseBuff impulseBuff;

    [Tooltip("Optional: the Berserker's Rage buff. While raging, damage scales by its multiplier. Falls back to the Player's own RageBuff when left empty. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] RageBuff rageBuff;

    [Tooltip("Optional: the Berserker's Pain into Power. Damage banked from hits taken mid-charge is consumed on Activate and added flat to every target of that swing. Falls back to the Player's own PainIntoPower when left empty. Only used when 'Reads Player Stats' is on.")]
    [SerializeField] PainIntoPower painIntoPower;

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
    IDamageable ignoredTarget;
    PlayerStats stats;

    bool isActive;
    float deactivateTime;
    int activePointCount;
    float reachBonus;
    float activationPainBonus;

    bool anyError = false;

    bool initialized = false;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;

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
            stats.SetBase(StatType.KnockbackForce, knockbackForce);
            stats.SetBase(StatType.ChargeKnockbackBonus, 0f);
            stats.SetBase(StatType.MaxHitsPerSwing, damageTriggerSO.maxHits);
            stats.SetBase(StatType.IceBreakerDamageBonus, 0f);

            stats.OnStatChanged += HandleStatChanged;
            ApplyRangeScale();

            // Missing buff is not an error — the Impulse feature is optional (the DeathNova
            // stats-fallback idiom). Same for the Berserker's rage/pain components: on the
            // Swordsman they're disabled and stay permanently neutral.
            if (impulseBuff == null && Player.Instance != null)
            {
                impulseBuff = Player.Instance.GetComponentInChildren<ImpulseBuff>();
            }
            if (rageBuff == null && Player.Instance != null)
            {
                rageBuff = Player.Instance.GetComponentInChildren<RageBuff>();
            }
            if (painIntoPower == null && Player.Instance != null)
            {
                painIntoPower = Player.Instance.GetComponentInChildren<PainIntoPower>();
            }
        }
        else if (Player.Instance != null && ReferenceEquals(ownerDamageable, Player.Instance.Damageable))
        {
            // Player-owned ability hitboxes (e.g. the Death Nova) don't read weapon stats, but they
            // are still "all damage sources" — keep stats around just for the global multiplier.
            stats = Player.Instance.Stats;
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

    /// <summary>
    ///     Extra reach as a fraction of the blade length (0.6 = 60% longer sweep), with no visual
    ///     change: blade-sweep sample points extrapolate past Blade Tip instead of scaling the
    ///     transform the way the SwordRange stat does. Used by <c>PlayerMount</c> while riding, where
    ///     the saddle height would otherwise put grounded enemies outside the sword's arc. 0 = off.
    /// </summary>
    public void SetReachBonus(float value)
    {
        reachBonus = Mathf.Max(0f, value);
    }

    float thicknessBonus;
    public void SetThicknessBonus(float value)
    {
        thicknessBonus = Mathf.Max(0f, value);
    }

    /// <summary>
    ///     A target this trigger skips in addition to the wielder — e.g. the horse under a mounted
    ///     player, which sits directly inside the blade's (reach-extended) arc. Null = none.
    /// </summary>
    public void SetIgnoredTarget(IDamageable target)
    {
        ignoredTarget = target;
    }

    public void Activate()
    {
        Init();
        if (anyError) return;

        isActive = true;
        deactivateTime = Time.time + damageTriggerSO.duration;
        hitTargets.Clear();

        // Pain into Power (Berserker): the pool banked from hits taken mid-charge fuels this whole
        // activation — consumed once, so every target of the swing shares the same flat bonus.
        activationPainBonus = readsPlayerStats && painIntoPower != null ? painIntoPower.ConsumeBonus() : 0f;

        if (detectionMode == DetectionMode.BladeSweep)
        {
            float rangeMultiplier = readsPlayerStats ? stats.GetValue(StatType.SwordRange) : 1f;
            // Reach bonus lengthens the sweep past the tip (see SetReachBonus), so sample the longer
            // line proportionally more densely to keep the anti-tunneling spacing constant.
            rangeMultiplier *= 1f + reachBonus;
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


            var totalRadius = bladeRadius + thicknessBonus;
            RaycastHit hit;
            if (totalRadius > 0)
            {
                if (!Physics.SphereCast(previousPos, totalRadius, delta / distance, out hit, distance, hitLayers, QueryTriggerInteraction.Collide))
                {
                    continue;
                }
            }
            else
            {
                if (!Physics.Raycast(previousPos, delta / distance, out hit, distance, hitLayers, QueryTriggerInteraction.Collide))
                {
                    continue;
                }
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
        // Never damage the wielder of this trigger, nor the explicitly ignored target (the mounted
        // player's horse — see SetIgnoredTarget).
        if (damageable == ownerDamageable) return true;
        if (ignoredTarget != null && damageable == ignoredTarget) return true;
        if (hitTargets.Contains(damageable)) return true;

        if (hitTargets.Count >= cap)
        {
            OnBlocked?.Invoke();
            isActive = false;
            return false;
        }

        hitTargets.Add(damageable);
        Damage damage = BuildDamage();

        // Ice Breaker: the player's melee hits slowed/chilled enemies harder. Per-target (unlike
        // BuildDamage's rolls) because it depends on the target's SlowStatus, not the swing.
        if (readsPlayerStats && damageable is Component targetComponent)
        {
            float iceBreakerBonus = stats.GetValue(StatType.IceBreakerDamageBonus);
            if (iceBreakerBonus > 0f)
            {
                SlowStatus slow = targetComponent.GetComponentInParent<SlowStatus>();
                if (slow != null && slow.IsSlowed)
                {
                    damage.value *= 1f + iceBreakerBonus;
                }
            }
        }

        damageable.ReceiveDamage(damage);
        OnHit?.Invoke(damageable, damage, hitPoint);
        return true;
    }

    int EffectiveMaxHits()
    {
        return readsPlayerStats ? Mathf.RoundToInt(stats.GetValue(StatType.MaxHitsPerSwing)) : damageTriggerSO.maxHits;
    }

    /// <summary>
    ///     The effective <see cref="StatType.AllDamageMultiplier" /> ("Raw Power" skill family) for
    ///     player-owned triggers, 1 for everything else (enemy hitboxes never get player stats).
    /// </summary>
    float GlobalDamageMultiplier()
    {
        if (stats == null)
        {
            return 1f;
        }
        float multiplier = stats.GetValue(StatType.AllDamageMultiplier);
        return multiplier > 0f ? multiplier : 1f;
    }

    Vector3 BladePointPosition(int index)
    {
        float t = activePointCount > 1 ? (float)index / (activePointCount - 1) : 0f;
        // t beyond 1 extrapolates past the tip: extra reach with no visual blade change.
        return Vector3.LerpUnclamped(bladeBase.position, bladeTip.position, t * (1f + reachBonus));
    }

    Damage BuildDamage()
    {
        if (!readsPlayerStats)
        {
            return new Damage
            {
                value = damageSO.baseDamage * GlobalDamageMultiplier(),
                isCritical = damageSO.isCritical,
                knockbackForce = knockbackForce,
                sourcePosition = transform.position,
            };
        }

        float value = stats.GetValue(StatType.SwordDamage) * GlobalDamageMultiplier();

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

        // Impulse buff: extra damage per orb stack, plus the fling stamp. Charge amplifies the
        // launch through the same ChargeKnockbackBonus stat as knockback.
        if (impulseBuff != null && impulseBuff.IsActive)
        {
            value *= impulseBuff.DamageMultiplier;
            knockback *= impulseBuff.KnockbackMultiplier;
        }

        // Rage (Berserker): more damage the angrier the player is (the ImpulseBuff read pattern).
        if (rageBuff != null && rageBuff.IsActive)
        {
            value *= rageBuff.DamageMultiplier;
        }

        // Pain into Power (Berserker): damage tanked mid-charge comes back flat, on top of every
        // multiplier — "adds to the damage of that attack".
        value += activationPainBonus;

        return new Damage
        {
            value = value,
            isCritical = crit,
            knockbackForce = knockback,
            sourcePosition = transform.position,
            // Player-owned hit: lets Runestones tell a player blast from enemy splash damage. Safe
            // for every other consumer — Counterstrike only reads source off hits the *player*
            // receives, and this trigger never damages its owner.
            source = ownerDamageable,
        };
    }
}
