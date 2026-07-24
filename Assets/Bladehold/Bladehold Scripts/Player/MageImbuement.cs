using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Mage's elemental imbuement — the class mechanic. One shared, timed, charge-stacked buff
///     (the <see cref="ImpulseBuff" /> skeleton plus an element identity) that both weapons feed:
///     it listens to the staff's <see cref="DamageTrigger.OnHit" /> and the wand's
///     <see cref="PlayerWand.OnHit" /> and lays elemental effects on top of every hit.
///
///     Grant semantics (see <see cref="CollectNode" />/<see cref="CollectRunestone" />): picking up
///     an <see cref="ElementNode" /> of the current element adds a charge and RESETS the shared
///     timer (never adds time); a different element replaces the imbuement at one charge; blasting a
///     <see cref="Runestone" /> of a different element replaces it with
///     <see cref="StatType.MageRunestoneCharges" /> charges, while a same-element blast only resets
///     the timer (so camping a runestone can't farm stacks). Expiry clears element and all charges
///     at once. Charges scale both the flat damage rider (per-charge) and the element's effect
///     magnitudes (<see cref="ChargePotencyMultiplier" />, the ImpulseBuff stack idiom).
///
///     Per-element effects on every imbued hit (all magnitudes × potency):
///     - <b>Fire</b>: an extra damage rider, plus — once the "Combustion" node raises
///       <see cref="StatType.MageFireExplosionDamagePercent" /> — an explosion around the hit point
///       (a direct OverlapSphere, the <see cref="ChainLightning" /> style: explosions happen at
///       arbitrary hit points, not at a player-child transform), and — once "Scorched Earth" raises
///       <see cref="StatType.MageFlameZoneDuration" /> — a burning <see cref="FlameZone" /> on the
///       ground (rate-limited by <see cref="MageImbuementSO.flameZoneCooldownSeconds" />).
///     - <b>Lightning</b>: hits arc via the shared <see cref="ChainLightning.ForceChain" /> (its
///       ChainLightning* stats are base 0 and class-agnostic — the Mage's lightning nodes raise
///       them; a Mage never activates the orb buff, so the two chain paths can't double-fire).
///     - <b>Ice</b>: applies a <see cref="SlowStatus" /> (the FreezingDraw pattern); the damage
///       bonus vs slowed enemies is the existing Ice Breaker stat, read per-target by the staff's
///       DamageTrigger and by <see cref="PlayerWand.CreateHitDamage" />.
///
///     Riders/explosions/zones derive their damage from the triggering hit's value — which already
///     folds in charge, crit, and AllDamageMultiplier — and never re-apply multipliers (the chain
///     lightning precedent). Enabled only in the Mage's classComponents slot; disabled it never runs
///     Start, so every Mage stat base stays unregistered and world nodes/runestones are inert
///     (<see cref="CollectNode" /> guards on <c>isActiveAndEnabled</c> because
///     <c>GetComponentInParent</c> finds disabled components).
/// </summary>
public class MageImbuement : MonoBehaviour
{
    /// <summary>One element's cosmetic identity — aura, pickup feedback, HUD icon/tint.</summary>
    [Serializable]
    public class ElementStyle
    {
        public ElementType element;
        [Tooltip("Looping aura child object shown while this element is active. Optional.")]
        public GameObject auraVisual;
        [Tooltip("Played when this element becomes the active imbuement. Optional.")]
        public MMF_Player activationFeedback;
        [Tooltip("HUD icon for this element (read by MageElementUI). Optional.")]
        public Sprite icon;
        [Tooltip("HUD tint for this element (read by MageElementUI).")]
        public Color tint = Color.white;
    }

    [SerializeField] private MageImbuementSO config;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("The staff's DamageTrigger whose hits carry the imbuement. Assign explicitly — the player has other DamageTriggers (the VampiricBlade precedent). Mage-only, so no class re-pointing is needed.")]
    [SerializeField] private DamageTrigger staffTrigger;
    [Tooltip("The Mage's wand — its missile hits carry the imbuement too.")]
    [SerializeField] private PlayerWand wand;
    [Tooltip("Optional; defaults to the ChainLightning on Player.Instance. Lightning-imbued hits arc through it.")]
    [SerializeField] private ChainLightning chainLightning;
    [Tooltip("Layers enemies live on — explosions and flame zones only search these (exclude the player, gates, runestones, and environment).")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Element visuals")]
    [Tooltip("Per-element aura/feedback/HUD styling. One entry per ElementType.")]
    [SerializeField] private ElementStyle[] elementStyles;
    [Tooltip("Played when the imbuement expires or is replaced. Optional.")]
    [SerializeField] private MMF_Player deactivationFeedback;

    [Header("Fire prefabs")]
    [Tooltip("Cosmetic explosion VFX instantiated at fire-hit points once Combustion is owned. Optional.")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [Tooltip("Burning-ground zone spawned by fire hits once Scorched Earth is owned. Required for that node to do anything.")]
    [SerializeField] private FlameZone flameZonePrefab;

    /// <summary>Raised whenever the element, charges, or remaining time change discretely (pickup, swap, expiry).</summary>
    public event Action OnChanged;

    /// <summary>The active element, or null while un-imbued.</summary>
    public ElementType? CurrentElement { get; private set; }

    /// <summary>Element charges currently held (0 while un-imbued).</summary>
    public int ChargeCount { get; private set; }

    /// <summary>Seconds until the imbuement expires.</summary>
    public float RemainingSeconds { get; private set; }

    public bool IsActive => !anyError && CurrentElement != null && RemainingSeconds > 0f;

    /// <summary>Remaining time as a fraction of the full (stat-scaled) duration, for the HUD fill.</summary>
    public float DurationFraction => IsActive
        ? Mathf.Clamp01(RemainingSeconds / Mathf.Max(0.01f, stats.GetValue(StatType.MageImbuementDuration)))
        : 0f;

    /// <summary>Effect-magnitude multiplier from stacked charges (1 while inactive or single-charged — the ImpulseBuff stack idiom).</summary>
    public float ChargePotencyMultiplier =>
        IsActive ? 1f + (ChargeCount - 1) * config.potencyPerExtraChargePercent : 1f;

    /// <summary>The active element's cosmetic style (HUD icon/tint), or null while un-imbued.</summary>
    public ElementStyle CurrentStyle => IsActive ? FindStyle(CurrentElement.Value) : null;

    private const int MaxOverlapResults = 32;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> explosionTargets = new HashSet<IDamageable>();

    private IDamageable ownerDamageable;
    private IDamageable ignoredTarget;

    /// <summary>
    ///     A target elemental explosions skip in addition to the wielder — the horse under a mounted player. Null = none. Set/cleared by <see cref="PlayerMount" />.
    /// </summary>
    public void SetIgnoredTarget(IDamageable target)
    {
        ignoredTarget = target;
    }
    private float nextFlameZoneTime;
    private bool anyError = false;

    private void OnValidate()
    {
        if (stats == null)
        {
            stats = GetComponentInParent<PlayerStats>();
        }
        if (wand == null)
        {
            wand = GetComponent<PlayerWand>();
        }
    }

    private PlayerBow bow;
    private PlayerThrownAxe thrownAxe;
    private DamageTrigger activeMeleeTrigger;

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("MageImbuementSO is not assigned in the inspector.");
            anyError = true;
        }
        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("MageImbuement could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }
        if (chainLightning == null)
        {
            chainLightning = Player.Instance != null ? Player.Instance.GetComponentInChildren<ChainLightning>() : null;
        }

        if (anyError)
        {
            return;
        }

        // The imbuement's riders/zones never hit their caster (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();
        if (ownerDamageable == null && Player.Instance != null)
        {
            ownerDamageable = Player.Instance.Damageable;
        }

        PlayerClassController classController = GetComponentInParent<PlayerClassController>();
        bool isMage = classController != null && classController.ActiveClass != null &&
                      classController.ActiveClass.id.Equals("mage", StringComparison.OrdinalIgnoreCase);

        // Register authored SO values as stat bases. Non-Mage classes start with 0 runestone charges until unlocked in skill tree.
        stats.SetBase(StatType.MageImbuementDuration, config.imbuementDurationSeconds);
        stats.SetBase(StatType.MageImbuementMaxCharges, config.maxCharges);
        stats.SetBase(StatType.MageImbuementBonusPerCharge, config.bonusDamagePercentPerCharge);
        stats.SetBase(StatType.MageRunestoneCharges, isMage ? config.runestoneBaseCharges : 0f);
        stats.SetBase(StatType.MageFireDamagePercent, config.fireBonusDamagePercent);
        stats.SetBase(StatType.MageFireExplosionDamagePercent, 0f);
        stats.SetBase(StatType.MageFireExplosionRadius, config.explosionRadiusMetres);
        stats.SetBase(StatType.MageFlameZoneDuration, 0f);
        stats.SetBase(StatType.MageFlameZoneDamagePercent, config.flameZoneDamagePercent);
        stats.SetBase(StatType.MageIceSlowPercent, config.iceSlowFraction);
        stats.SetBase(StatType.MageIceSlowDurationSeconds, config.iceSlowDurationSeconds);
        stats.SetBase(StatType.SlowDurationBonusSeconds, 0f);

        SetAllAuras(null);

        SubscribeWeaponHits();
    }

    private void SubscribeWeaponHits()
    {
        if (staffTrigger != null)
        {
            staffTrigger.OnHit += HandleHit;
        }

        PlayerClassController classController = GetComponentInParent<PlayerClassController>();
        if (classController != null && classController.ActiveMeleeTrigger != null && classController.ActiveMeleeTrigger != staffTrigger)
        {
            activeMeleeTrigger = classController.ActiveMeleeTrigger;
            activeMeleeTrigger.OnHit += HandleHit;
        }

        if (wand != null)
        {
            wand.OnHit += HandleHit;
        }

        bow = GetComponentInParent<PlayerBow>();
        if (bow != null)
        {
            bow.OnHit += HandleHit;
        }

        thrownAxe = GetComponentInParent<PlayerThrownAxe>();
        if (thrownAxe != null)
        {
            thrownAxe.OnHit += HandleHit;
        }
    }

    private void UnsubscribeWeaponHits()
    {
        if (staffTrigger != null) staffTrigger.OnHit -= HandleHit;
        if (activeMeleeTrigger != null) activeMeleeTrigger.OnHit -= HandleHit;
        if (wand != null) wand.OnHit -= HandleHit;
        if (bow != null) bow.OnHit -= HandleHit;
        if (thrownAxe != null) thrownAxe.OnHit -= HandleHit;
    }

    private void OnDestroy()
    {
        UnsubscribeWeaponHits();
    }

    private void Update()
    {
        if (CurrentElement == null)
        {
            return;
        }

        RemainingSeconds -= Time.deltaTime;
        if (RemainingSeconds <= 0f)
        {
            Deactivate();
        }
    }

    /// <summary>
    ///     Grants one element node's worth of imbuement: same element = +1 charge (capped) and a
    ///     timer reset; a different element (or an inactive buff) replaces the imbuement at one
    ///     charge. Returns false when this component can't accept it (another class's disabled
    ///     imbuement — <c>GetComponentInParent</c> finds disabled components, so the node stays
    ///     uncollected on the ground).
    /// </summary>
    public bool CollectNode(ElementType element)
    {
        if (anyError || !isActiveAndEnabled)
        {
            return false;
        }

        if (IsActive && CurrentElement == element)
        {
            int maxCharges = Mathf.Max(1, Mathf.RoundToInt(stats.GetValue(StatType.MageImbuementMaxCharges)));
            ChargeCount = Mathf.Min(ChargeCount + 1, maxCharges);
        }
        else
        {
            SwapElement(element, 1);
        }

        RemainingSeconds = stats.GetValue(StatType.MageImbuementDuration);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     A runestone blast: a different element replaces the imbuement with
    ///     <see cref="StatType.MageRunestoneCharges" /> charges (capped); the same element only
    ///     resets the timer — no free stacks from camping the stone. Returns false when this
    ///     component can't accept it (another class — the stone plays its fizzle instead).
    /// </summary>
    public bool CollectRunestone(ElementType element)
    {
        if (anyError || !isActiveAndEnabled || stats == null)
        {
            return false;
        }

        int runestoneCharges = Mathf.RoundToInt(stats.GetValue(StatType.MageRunestoneCharges));
        if (runestoneCharges <= 0)
        {
            return false;
        }

        if (!IsActive || CurrentElement != element)
        {
            int maxCharges = Mathf.Max(1, Mathf.RoundToInt(stats.GetValue(StatType.MageImbuementMaxCharges)));
            int granted = Mathf.Clamp(runestoneCharges, 1, maxCharges);
            SwapElement(element, granted);
        }

        RemainingSeconds = stats.GetValue(StatType.MageImbuementDuration);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Replaces the active element (losing any held charges — the cost of switching).</summary>
    private void SwapElement(ElementType element, int charges)
    {
        CurrentElement = element;
        ChargeCount = charges;
        SetAllAuras(element);

        ElementStyle style = FindStyle(element);
        if (style != null && style.activationFeedback != null)
        {
            style.activationFeedback.PlayFeedbacks();
        }
    }

    private void Deactivate()
    {
        CurrentElement = null;
        ChargeCount = 0;
        RemainingSeconds = 0f;
        SetAllAuras(null);

        if (deactivationFeedback != null)
        {
            deactivationFeedback.PlayFeedbacks();
        }

        OnChanged?.Invoke();
    }

    private ElementStyle FindStyle(ElementType element)
    {
        if (elementStyles == null)
        {
            return null;
        }
        foreach (ElementStyle style in elementStyles)
        {
            if (style != null && style.element == element)
            {
                return style;
            }
        }
        return null;
    }

    private void SetAllAuras(ElementType? active)
    {
        if (elementStyles == null)
        {
            return;
        }
        foreach (ElementStyle style in elementStyles)
        {
            if (style != null && style.auraVisual != null)
            {
                style.auraVisual.SetActive(active != null && style.element == active.Value);
            }
        }
    }

    /// <summary>
    ///     Lays the active element onto a weapon hit. The rider (and everything derived from it)
    ///     scales off the triggering hit's value, which already folds in charge, crit, and
    ///     AllDamageMultiplier — nothing here re-applies a multiplier. Riders go straight to
    ///     ReceiveDamage; no listener loops back into a weapon OnHit, so this can't self-feed.
    /// </summary>
    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (!IsActive || target == null || (ignoredTarget != null && target == ignoredTarget) || damage == null || damage.value <= 0f)
        {
            return;
        }

        // The flat elemental rider: per-charge bonus, plus Searing Focus while Fire-imbued.
        float bonusPercent = ChargeCount * stats.GetValue(StatType.MageImbuementBonusPerCharge);
        if (CurrentElement == ElementType.Fire)
        {
            bonusPercent += stats.GetValue(StatType.MageFireDamagePercent);
        }
        if (bonusPercent > 0f)
        {
            target.ReceiveDamage(new Damage
            {
                value = damage.value * bonusPercent,
                type = DamageType.elemental,
                sourcePosition = hitPoint,
                source = ownerDamageable,
            });
        }

        float potency = ChargePotencyMultiplier;
        switch (CurrentElement.Value)
        {
            case ElementType.Fire:
                HandleFireHit(target, damage, hitPoint, potency);
                break;
            case ElementType.Lightning:
                if (chainLightning != null)
                {
                    chainLightning.ForceChain(damage.value * potency, hitPoint, target);
                }
                break;
            case ElementType.Ice:
                SlowStatus slow = SlowStatus.GetOrAdd(target as Component);
                if (slow != null)
                {
                    slow.ApplySlow(
                        Mathf.Clamp01(stats.GetValue(StatType.MageIceSlowPercent) * potency),
                        stats.GetValue(StatType.MageIceSlowDurationSeconds) + stats.GetValue(StatType.SlowDurationBonusSeconds));
                }
                break;
        }
    }

    /// <summary>
    ///     Fire: an explosion around the hit point (once Combustion is owned) and a burning ground
    ///     zone (once Scorched Earth is owned, rate-limited so a multi-target sweep spawns one zone,
    ///     not five).
    /// </summary>
    private void HandleFireHit(IDamageable target, Damage damage, Vector3 hitPoint, float potency)
    {
        float explosionPercent = stats.GetValue(StatType.MageFireExplosionDamagePercent);
        float radius = stats.GetValue(StatType.MageFireExplosionRadius);

        if (explosionPercent > 0f && radius > 0f)
        {
            float explosionDamage = damage.value * explosionPercent * potency;

            explosionTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(hitPoint, radius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (!collider.TryGetComponent(out IDamageable damageable))
                {
                    damageable = collider.GetComponentInParent<IDamageable>();
                }
                // The direct target already took the rider — no double dip (and never the caster or player).
                if (damageable == null || IsOwnerOrPlayer(damageable) || (ignoredTarget != null && damageable == ignoredTarget) || damageable == target || !explosionTargets.Add(damageable))
                {
                    continue;
                }

                damageable.ReceiveDamage(new Damage
                {
                    value = explosionDamage,
                    type = DamageType.elemental,
                    sourcePosition = hitPoint,
                    source = ownerDamageable,
                });
            }

            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, hitPoint, Quaternion.identity);
            }
        }

        float zoneDuration = stats.GetValue(StatType.MageFlameZoneDuration);
        if (zoneDuration > 0f && flameZonePrefab != null && Time.time >= nextFlameZoneTime)
        {
            nextFlameZoneTime = Time.time + config.flameZoneCooldownSeconds;

            // Hit points land on chests and torsos — drop the zone onto walkable ground beneath
            // (the ChestSpawner NavMesh-snap idiom).
            Vector3 zonePosition = hitPoint;
            if (NavMesh.SamplePosition(hitPoint, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                zonePosition = navHit.position;
            }

            FlameZone zone = Instantiate(flameZonePrefab, zonePosition, Quaternion.identity);
            zone.Initialize(
                radius,
                zoneDuration,
                config.flameZoneTickInterval,
                damage.value * stats.GetValue(StatType.MageFlameZoneDamagePercent) * potency,
                ownerDamageable,
                enemyLayers);
        }
    }

    private bool IsOwnerOrPlayer(IDamageable damageable)
    {
        if (damageable == null) return true;
        if (ownerDamageable != null && damageable == ownerDamageable) return true;
        if (Player.Instance != null)
        {
            if (damageable == Player.Instance.Damageable || damageable == Player.Instance.Health) return true;
            if (damageable is Component comp && (UnityEngine.Object)comp.transform.root == (UnityEngine.Object)Player.Instance.transform.root) return true;
        }
        return false;
    }
}
