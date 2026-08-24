using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Controls periodic auto-imbuement for the player's weapons across 4 elements:
///     - Fire: AoE combustion explosion + bonus fire damage.
///     - Ice: Slows enemies + chance to freeze solid.
///     - Lightning: Boosts attack charge speed + chain lightning on hit.
///     - Impulse: Massive kinetic knockback fling.
///
///     Each element tracks its own independent periodic timer (cooldown -> active duration).
///     Upgrades reduce cooldown and scale power. Listens to melee, bow, axe, and wand hits.
/// </summary>
public class PeriodicImbuementController : MonoBehaviour
{
    public static PeriodicImbuementController Instance { get; private set; }

    [Header("Element Colors")]
    [SerializeField] private Color fireColor = new Color(1f, 0.4f, 0.1f);
    [SerializeField] private Color iceColor = new Color(0.2f, 0.7f, 1f);
    [SerializeField] private Color lightningColor = new Color(0.9f, 0.9f, 0.2f);
    [SerializeField] private Color impulseColor = new Color(0.3f, 0.5f, 1f);

    [Header("Layer Masks")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Explosion Prefabs (Optional)")]
    [SerializeField] private GameObject fireExplosionVfx;
    [SerializeField] private GameObject iceFreezeVfx;

    private PlayerStats stats;
    private PlayerClassController classController;
    private ChainLightning chainLightning;
    private DamageTrigger activeMeleeTrigger;
    private PlayerBow bow;
    private PlayerThrownAxe thrownAxe;
    private PlayerWand wand;

    // Element active & timer trackers
    public bool IsFireActive { get; private set; }
    public bool IsIceActive { get; private set; }
    public bool IsLightningActive { get; private set; }
    public bool IsImpulseActive { get; private set; }

    private float fireTimer;
    private float iceTimer;
    private float lightningTimer;
    private float impulseTimer;

    private const int MaxOverlapResults = 32;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    public event Action OnImbuementStateChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        if (stats == null) stats = Player.Instance != null ? Player.Instance.Stats : GetComponentInParent<PlayerStats>();
        if (classController == null) classController = GetComponentInParent<PlayerClassController>();
        if (chainLightning == null) chainLightning = Player.Instance != null ? Player.Instance.GetComponentInChildren<ChainLightning>() : null;

        if (stats == null)
        {
            stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        }

        // Register default bases if not set
        if (stats != null)
        {
            stats.SetBase(StatType.PeriodicFireUnlocked, 0f);
            stats.SetBase(StatType.PeriodicFireCooldown, 12f);
            stats.SetBase(StatType.PeriodicFireDuration, 4f);
            stats.SetBase(StatType.PeriodicFireDamagePercent, 0.35f);
            stats.SetBase(StatType.PeriodicFireExplosionRadius, 4f);

            stats.SetBase(StatType.PeriodicIceUnlocked, 0f);
            stats.SetBase(StatType.PeriodicIceCooldown, 12f);
            stats.SetBase(StatType.PeriodicIceDuration, 4f);
            stats.SetBase(StatType.PeriodicIceSlowPercent, 0.5f);
            stats.SetBase(StatType.PeriodicIceFreezeChance, 0.25f);

            stats.SetBase(StatType.PeriodicLightningUnlocked, 0f);
            stats.SetBase(StatType.PeriodicLightningCooldown, 12f);
            stats.SetBase(StatType.PeriodicLightningDuration, 4f);
            stats.SetBase(StatType.PeriodicLightningChargeSpeed, 1.5f);
            stats.SetBase(StatType.PeriodicLightningBounces, 2f);
            stats.SetBase(StatType.PeriodicLightningDamagePercent, 0.5f);

            stats.SetBase(StatType.PeriodicImpulseUnlocked, 0f);
            stats.SetBase(StatType.PeriodicImpulseCooldown, 12f);
            stats.SetBase(StatType.PeriodicImpulseDuration, 4f);
            stats.SetBase(StatType.PeriodicImpulseKnockbackForce, 25f);
        }

        SubscribeHitEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeHitEvents();
    }

    private void SubscribeHitEvents()
    {
        if (classController != null && classController.ActiveMeleeTrigger != null)
        {
            activeMeleeTrigger = classController.ActiveMeleeTrigger;
            activeMeleeTrigger.OnHit += HandleHit;
        }

        bow = GetComponentInParent<PlayerBow>();
        if (bow != null) bow.OnHit += HandleHit;

        thrownAxe = GetComponentInParent<PlayerThrownAxe>();
        if (thrownAxe != null) thrownAxe.OnHit += HandleHit;

        wand = GetComponentInParent<PlayerWand>();
        if (wand != null) wand.OnHit += HandleHit;
    }

    private void UnsubscribeHitEvents()
    {
        if (activeMeleeTrigger != null) activeMeleeTrigger.OnHit -= HandleHit;
        if (bow != null) bow.OnHit -= HandleHit;
        if (thrownAxe != null) thrownAxe.OnHit -= HandleHit;
        if (wand != null) wand.OnHit -= HandleHit;
    }

    private void Update()
    {
        if (stats == null) return;

        UpdateElement(
            stats.GetValue(StatType.PeriodicFireUnlocked) > 0f,
            stats.GetValue(StatType.PeriodicFireCooldown),
            stats.GetValue(StatType.PeriodicFireDuration),
            ref fireTimer,
            () => IsFireActive,
            val => { IsFireActive = val; OnImbuementStateChanged?.Invoke(); });

        UpdateElement(
            stats.GetValue(StatType.PeriodicIceUnlocked) > 0f,
            stats.GetValue(StatType.PeriodicIceCooldown),
            stats.GetValue(StatType.PeriodicIceDuration),
            ref iceTimer,
            () => IsIceActive,
            val => { IsIceActive = val; OnImbuementStateChanged?.Invoke(); });

        UpdateElement(
            stats.GetValue(StatType.PeriodicLightningUnlocked) > 0f,
            stats.GetValue(StatType.PeriodicLightningCooldown),
            stats.GetValue(StatType.PeriodicLightningDuration),
            ref lightningTimer,
            () => IsLightningActive,
            val => { IsLightningActive = val; OnImbuementStateChanged?.Invoke(); });

        UpdateElement(
            stats.GetValue(StatType.PeriodicImpulseUnlocked) > 0f,
            stats.GetValue(StatType.PeriodicImpulseCooldown),
            stats.GetValue(StatType.PeriodicImpulseDuration),
            ref impulseTimer,
            () => IsImpulseActive,
            val => { IsImpulseActive = val; OnImbuementStateChanged?.Invoke(); });
    }

    private void UpdateElement(bool isUnlocked, float cooldown, float duration, ref float timer, Func<bool> getActive, Action<bool> setActive)
    {
        if (!isUnlocked)
        {
            if (getActive()) setActive(false);
            timer = 0f;
            return;
        }

        cooldown = Mathf.Max(2f, cooldown);
        duration = Mathf.Max(1f, duration);

        timer += Time.deltaTime;
        if (getActive())
        {
            if (timer >= duration)
            {
                setActive(false);
                timer = 0f;
            }
        }
        else
        {
            if (timer >= cooldown)
            {
                setActive(true);
                timer = 0f;
            }
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (target == null || damage == null || damage.value <= 0f) return;

        // 1. Fire Imbuement Hit
        if (IsFireActive && stats != null)
        {
            float bonusFire = damage.value * stats.GetValue(StatType.PeriodicFireDamagePercent);
            if (bonusFire > 0f)
            {
                target.ReceiveDamage(new Damage
                {
                    value = bonusFire,
                    type = DamageType.elemental,
                    sourcePosition = hitPoint,
                    source = Player.Instance != null ? Player.Instance.Damageable : null,
                    isPlayerDamage = true,
                });
            }

            float explosionRadius = stats.GetValue(StatType.PeriodicFireExplosionRadius);
            if (explosionRadius > 0f)
            {
                hitTargets.Clear();
                int count = Physics.OverlapSphereNonAlloc(hitPoint, explosionRadius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    Collider col = overlapBuffer[i];
                    if (!col.TryGetComponent(out IDamageable dmgReceiver))
                    {
                        dmgReceiver = col.GetComponentInParent<IDamageable>();
                    }
                    if (dmgReceiver == null || dmgReceiver == target || !hitTargets.Add(dmgReceiver)) continue;

                    dmgReceiver.ReceiveDamage(new Damage
                    {
                        value = bonusFire,
                        type = DamageType.elemental,
                        sourcePosition = hitPoint,
                        source = Player.Instance != null ? Player.Instance.Damageable : null,
                        isPlayerDamage = true,
                    });
                }

                if (fireExplosionVfx != null)
                {
                    Instantiate(fireExplosionVfx, hitPoint, Quaternion.identity);
                }
            }
        }

        // 2. Ice Imbuement Hit
        if (IsIceActive && stats != null)
        {
            float slowFraction = Mathf.Clamp01(stats.GetValue(StatType.PeriodicIceSlowPercent));
            float slowDuration = 3f + stats.GetValue(StatType.SlowDurationBonusSeconds);

            SlowStatus slow = SlowStatus.GetOrAdd(target as Component);
            if (slow != null)
            {
                slow.ApplySlow(slowFraction, slowDuration);
            }

            float freezeChance = stats.GetValue(StatType.PeriodicIceFreezeChance);
            if (UnityEngine.Random.value < freezeChance && slow != null)
            {
                // Freeze solid for 1.5s
                slow.ApplySlow(1f, 1.5f);
                if (iceFreezeVfx != null)
                {
                    Instantiate(iceFreezeVfx, hitPoint, Quaternion.identity);
                }
            }
        }

        // 3. Lightning Imbuement Hit
        if (IsLightningActive && stats != null && chainLightning != null)
        {
            float chainDamage = damage.value * stats.GetValue(StatType.PeriodicLightningDamagePercent);
            chainLightning.ForceChain(chainDamage, hitPoint, target);
        }

        // 4. Impulse Imbuement Hit
        if (IsImpulseActive && stats != null)
        {
            float impulseKnock = stats.GetValue(StatType.PeriodicImpulseKnockbackForce);
            if (impulseKnock > 0f)
            {
                target.ReceiveDamage(new Damage
                {
                    value = 0f,
                    knockbackForce = impulseKnock,
                    type = DamageType.blunt,
                    sourcePosition = Player.Instance != null ? Player.Instance.transform.position : hitPoint,
                    source = Player.Instance != null ? Player.Instance.Damageable : null,
                    isPlayerDamage = true,
                });
            }
        }
    }
}
