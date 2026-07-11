using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The Berserker's throwing axe — the class's ranged option, the <see cref="PlayerBow" /> skeleton
///     with the arrow swapped for a piercing line. Holding aim (the Synty <see cref="InputReader" />'s
///     <c>onAimActivated</c>/<c>onAimDeactivated</c> events) winds the throw up in discrete charge
///     levels (the bow-draw convention, tuned on <see cref="ThrownAxeSO" />); pressing attack while
///     aiming hurls the axe: a hitscan <b>sphere cast</b> — a straight line with
///     <see cref="StatType.AxeThrowWidth" /> metres of width — that damages and knocks back every
///     enemy along it up to a pierce budget (<see cref="StatType.AxeThrowPierceCount" /> + charge),
///     stopping early at solid environment or when the budget runs out. Charge raises damage,
///     knockback, and pierce together — the "wind up and bowl through the horde" fantasy.
///
///     Melee suppression, animator params (<c>IsAiming</c>/<c>BowFire</c>, optional), model swap,
///     cooldown, and stat-base registration all follow <see cref="PlayerBow" /> exactly — see its
///     header comment for the reasoning. Damage reads the shared <see cref="StatType.CritChance" />/
///     <see cref="StatType.CritMultiplier" />/<see cref="StatType.AllDamageMultiplier" />, with a
///     fresh roll per pierced enemy (the <see cref="DamageTrigger" /> per-target convention).
///
///     There is no mounted throwing (the bow's Horse Archer node has no axe equivalent yet) — aiming
///     from the saddle does nothing, exactly like a locked bow.
/// </summary>
public class PlayerThrownAxe : MonoBehaviour, IChargedAimWeapon
{
    [Tooltip("Synty InputReader that raises the aim and attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ThrownAxeSO config;

    [Header("Aiming")]
    [Tooltip("Camera whose centre the throw flies toward. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Where thrown axes originate — e.g. the hand or the player's chest. Defaults to this transform.")]
    [SerializeField] private Transform throwOrigin;
    [Tooltip("Layers a thrown axe can hit. Exclude the player's own layer if the cast ever clips the player.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("The player rig's Animator — used to cancel the melee-swing trigger while aiming. Synty rigs keep it on a child.")]
    [SerializeField] private Animator playerAnimator;

    [Header("Weapon models (optional)")]
    [Tooltip("Melee weapon model shown while not aiming. Optional.")]
    [SerializeField] private GameObject meleeWeaponModel;
    [Tooltip("Throwing-axe model shown in hand while aiming. Optional.")]
    [SerializeField] private GameObject thrownAxeModel;

    [Header("Visuals & feedback (optional)")]
    [Tooltip("Spinning-axe prop instantiated per throw to draw the flight (the BowTracer sibling).")]
    [SerializeField] private AxeProjectileVisual projectilePrefab;
    [Tooltip("Played when aiming starts (wind-up sound).")]
    [SerializeField] private MMF_Player drawFeedback;
    [Tooltip("Played on every throw.")]
    [SerializeField] private MMF_Player throwFeedback;

    [Header("Class mechanics (optional)")]
    [Tooltip("Optional: the Berserker's Rage buff. While raging, throw damage scales by its multiplier. Defaults to the one on this GameObject.")]
    [SerializeField] private RageBuff rageBuff;
    [Tooltip("Optional: the Berserker's Pain into Power. Damage banked from hits taken mid-wind-up is consumed per throw and added flat to every enemy pierced. Defaults to the one on this GameObject.")]
    [SerializeField] private PainIntoPower painIntoPower;

    [Tooltip("Optional: the player's mount. There is no mounted throwing — aiming from the saddle does nothing.")]
    [SerializeField] private PlayerMount mount;

    /// <summary>Fired once per enemy a throw damaged, with the world hit point — the <see cref="DamageTrigger.OnHit" /> shape, so feedback/telemetry listeners treat axe and sword alike.</summary>
    public event Action<IDamageable, Damage, Vector3> OnHit;

    /// <summary>Fired once per throw (hit or miss), the moment the axe leaves the hand — for cosmetic listeners.</summary>
    public event Action OnThrown;

    /// <summary>True once the "Throwing Axe" skill node has been bought; while false, aiming does nothing and melee works normally.</summary>
    public bool IsUnlocked => !anyError && stats.GetValue(StatType.AxeThrowUnlocked) >= 1f;

    /// <summary>True while the aim button is held and the throw is winding up.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>Charge level of the wind-up in progress, 0..AxeThrowMaxChargeLevels (the PlayerAttack convention).</summary>
    public int ChargeLevel { get; private set; }

    /// <summary>Levels the current wind-up can reach.</summary>
    public int MaxChargeLevels => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.AxeThrowMaxChargeLevels));

    /// <summary>Fraction of the post-throw cooldown elapsed: 0 the instant a throw fires, 1 when ready (the PlayerBow convention).</summary>
    public float CooldownFraction
    {
        get
        {
            if (anyError || config.throwCooldownSeconds <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01((Time.time - lastThrowTime) / config.throwCooldownSeconds);
        }
    }

    /// <summary>True while the axe is between throws and can't fire yet.</summary>
    public bool IsCoolingDown => CooldownFraction < 1f;

    // Aim-camera framing surfaced for BowAimCamera (see IChargedAimWeapon).
    public float AimCameraDistance => config != null ? config.aimCameraDistance : 2.75f;
    public float AimCameraHorizontalOffset => config != null ? config.aimCameraHorizontalOffset : 0.7f;
    public float AimFieldOfViewPercent => config != null ? config.aimFieldOfViewPercent : 1f;
    public float AimBlendSeconds => config != null ? config.aimBlendSeconds : 0.2f;

    private const int MaxCastHits = 64;

    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private IDamageable ownerDamageable;
    private int startAttackHash;
    private int isHoldingAttackHash;
    private int isAimingHash;
    private int fireHash;
    private bool hasAimAnimatorParams;
    private float chargeStartTime;
    private float lastThrowTime = Mathf.NegativeInfinity;
    private bool subscribed;
    private bool anyError = false;

    private void OnValidate()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
        if (playerAnimator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            playerAnimator = GetComponentInChildren<Animator>();
        }
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
        if (rageBuff == null)
        {
            rageBuff = GetComponent<RageBuff>();
        }
        if (painIntoPower == null)
        {
            painIntoPower = GetComponent<PainIntoPower>();
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; the thrown axe can't read aim/fire input.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("ThrownAxeSO is not assigned in the inspector.");
            anyError = true;
        }
        if (playerAnimator == null)
        {
            Debug.LogError("Player Animator is not assigned or found; the thrown axe can't suppress melee swings while aiming.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (throwOrigin == null)
        {
            throwOrigin = transform;
        }
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        startAttackHash = Animator.StringToHash("StartAttack");
        isHoldingAttackHash = Animator.StringToHash("IsHoldingAttack");
        isAimingHash = Animator.StringToHash("IsAiming");
        fireHash = Animator.StringToHash("BowFire");

        // The aim animator states are optional wiring, shared with the bow (the berserker's
        // override controller re-skins them) — without the params the axe still works, the rig just
        // keeps its melee pose (checked once so missing params don't spam warnings every aim).
        bool hasIsAiming = false;
        bool hasFire = false;
        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.nameHash == isAimingHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsAiming = true;
            }
            else if (parameter.nameHash == fireHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasFire = true;
            }
        }
        hasAimAnimatorParams = hasIsAiming && hasFire;
        if (!hasAimAnimatorParams)
        {
            Debug.LogWarning("PlayerThrownAxe: the player Animator has no IsAiming (Bool) / BowFire (Trigger) parameters — aim/throw animations won't play until the aim layer is wired (see TODO.md).");
        }

        // The axe never damages its wielder (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();

        // Register the authored SO values as the stat bases; skill nodes layer on top without ever
        // mutating the asset (the PlayerBow convention).
        // The axe itself is gated: base 0 = locked until the "Throwing Axe" node is bought (the
        // BowUnlocked convention).
        stats.SetBase(StatType.AxeThrowUnlocked, 0f);
        stats.SetBase(StatType.AxeThrowDamage, config.baseDamage);
        stats.SetBase(StatType.AxeThrowMaxChargeLevels, config.baseMaxChargeLevels);
        stats.SetBase(StatType.AxeThrowChargeDamageBonus, config.baseChargeDamageBonus);
        stats.SetBase(StatType.AxeThrowKnockback, config.baseKnockback);
        stats.SetBase(StatType.AxeThrowPierceCount, config.basePierceCount);
        stats.SetBase(StatType.AxeThrowWidth, config.baseWidth);

        Subscribe();
    }

    private void OnEnable()
    {
        if (!anyError && inputReader != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        // E.g. PlayerDeath disabling controls mid-aim: put the melee weapon back in hand.
        if (IsAiming)
        {
            EndAim();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAimActivated += StartAim;
        inputReader.onAimDeactivated += EndAim;
        inputReader.onAttackActivated += HandleAttackPressed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAimActivated -= StartAim;
        inputReader.onAimDeactivated -= EndAim;
        inputReader.onAttackActivated -= HandleAttackPressed;
        subscribed = false;
    }

    private void Update()
    {
        if (anyError || !IsAiming)
        {
            return;
        }

        // Keep the charge level live as the wind-up grows (the PlayerAttack convention).
        int maxLevels = MaxChargeLevels;
        int level = config.chargeTimePerLevel > 0f
            ? Mathf.FloorToInt((Time.time - chargeStartTime) / config.chargeTimePerLevel)
            : maxLevels;
        ChargeLevel = Mathf.Clamp(level, 0, maxLevels);

        // The vendored controller sets StartAttack/IsHoldingAttack on every attack press (input
        // callbacks run before Update; the animator consumes triggers after all Updates), so clearing
        // them every aiming frame reliably suppresses the melee swing regardless of event order.
        playerAnimator.ResetTrigger(startAttackHash);
        playerAnimator.SetBool(isHoldingAttackHash, false);
    }

    private void StartAim()
    {
        if (anyError || !IsUnlocked)
        {
            // Axe still locked: leave IsAiming false so the melee swing (and everything else) works normally.
            return;
        }

        // No mounted throwing (the bow's Horse Archer gate has no axe equivalent yet).
        if (mount != null && mount.IsMounted)
        {
            return;
        }

        IsAiming = true;
        ChargeLevel = 0;
        chargeStartTime = Time.time;

        if (hasAimAnimatorParams)
        {
            playerAnimator.SetBool(isAimingHash, true);
        }

        if (meleeWeaponModel != null)
        {
            meleeWeaponModel.SetActive(false);
        }
        if (thrownAxeModel != null)
        {
            thrownAxeModel.SetActive(true);
        }
        if (drawFeedback != null)
        {
            drawFeedback.PlayFeedbacks();
        }
    }

    private void EndAim()
    {
        if (!IsAiming)
        {
            return;
        }

        IsAiming = false;
        ChargeLevel = 0;

        if (hasAimAnimatorParams && playerAnimator != null)
        {
            playerAnimator.SetBool(isAimingHash, false);
            playerAnimator.ResetTrigger(fireHash);
        }

        if (meleeWeaponModel != null)
        {
            meleeWeaponModel.SetActive(true);
        }
        if (thrownAxeModel != null)
        {
            thrownAxeModel.SetActive(false);
        }
    }

    private void HandleAttackPressed()
    {
        if (anyError || !IsAiming)
        {
            return;
        }
        if (Time.time - lastThrowTime < config.throwCooldownSeconds)
        {
            return;
        }

        lastThrowTime = Time.time;
        Throw();

        // The wind-up restarts for the next throw while the aim is still held.
        ChargeLevel = 0;
        chargeStartTime = Time.time;
    }

    /// <summary>
    ///     One throw: a sphere cast (a straight line with width) from the hand toward the crosshair,
    ///     damaging and knocking back every unique enemy along it — each with a fresh damage roll —
    ///     until the pierce budget runs out or solid environment stops the axe.
    /// </summary>
    private void Throw()
    {
        Vector3 origin = throwOrigin.position;
        Vector3 direction = ResolveAimDirection(origin);

        if (throwFeedback != null)
        {
            throwFeedback.PlayFeedbacks();
        }
        if (hasAimAnimatorParams)
        {
            playerAnimator.SetTrigger(fireHash);
        }
        OnThrown?.Invoke();

        // Pain into Power (Berserker): the pool banked from hits taken mid-wind-up fuels this whole
        // throw — consumed once, so every enemy pierced shares the same flat bonus.
        float painBonus = painIntoPower != null ? painIntoPower.ConsumeBonus() : 0f;

        float width = Mathf.Max(0.05f, stats.GetValue(StatType.AxeThrowWidth));
        int pierceBudget = Mathf.Max(1, Mathf.RoundToInt(
            stats.GetValue(StatType.AxeThrowPierceCount) + ChargeLevel * config.piercePerChargeLevel));

        int count = Physics.SphereCastNonAlloc(origin, width * 0.5f, direction, castBuffer, config.maxRange, hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, HitDistanceComparer.Instance);

        Vector3 endPoint = origin + direction * config.maxRange;
        hitTargets.Clear();
        int damaged = 0;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = ResolveDamageable(hit.collider);

            if (damageable == null)
            {
                // Trigger colliders with no damageable (coins, orbs, hitboxes) don't stop the axe.
                if (!hit.collider.isTrigger)
                {
                    endPoint = HitPointOf(hit, origin, direction);
                    break;
                }
                continue;
            }

            if (damageable == ownerDamageable || !hitTargets.Add(damageable))
            {
                continue;
            }

            Vector3 hitPoint = HitPointOf(hit, origin, direction);
            Damage damage = BuildThrowDamage(origin, painBonus);
            damageable.ReceiveDamage(damage);
            OnHit?.Invoke(damageable, damage, hitPoint);
            damaged++;

            if (damaged >= pierceBudget)
            {
                // Out of penetration: the axe lodges in this target.
                endPoint = hitPoint;
                break;
            }
        }

        if (projectilePrefab != null)
        {
            AxeProjectileVisual projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Show(origin, endPoint);
        }
    }

    /// <summary>
    ///     Throws fly toward whatever the camera centre is looking at (the PlayerBow hitscan
    ///     convention), falling back to the origin's forward if no camera is available.
    /// </summary>
    private Vector3 ResolveAimDirection(Vector3 origin)
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
        if (aimCamera == null)
        {
            return throwOrigin.forward;
        }

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.origin + ray.direction * config.maxRange;

        int count = Physics.RaycastNonAlloc(ray.origin, ray.direction, castBuffer, config.maxRange, hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, HitDistanceComparer.Instance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = ResolveDamageable(hit.collider);
            if (damageable != null && damageable == ownerDamageable)
            {
                continue;
            }
            if (damageable == null && hit.collider.isTrigger)
            {
                continue;
            }
            aimPoint = hit.point;
            break;
        }

        return (aimPoint - origin).normalized;
    }

    private Damage BuildThrowDamage(Vector3 origin, float painBonus)
    {
        float value = stats.GetValue(StatType.AxeThrowDamage);
        value *= 1f + ChargeLevel * stats.GetValue(StatType.AxeThrowChargeDamageBonus);

        // Crits share the melee stats so Keen Eye/Critical Damage benefit both weapons (the bow convention).
        bool crit = UnityEngine.Random.value < stats.GetValue(StatType.CritChance);
        if (crit)
        {
            value *= stats.GetValue(StatType.CritMultiplier);
        }

        float allDamage = stats.GetValue(StatType.AllDamageMultiplier);
        if (allDamage > 0f)
        {
            value *= allDamage;
        }

        // Rage (Berserker): more damage the angrier the player is (the DamageTrigger read pattern).
        if (rageBuff != null && rageBuff.IsActive)
        {
            value *= rageBuff.DamageMultiplier;
        }

        // Pain into Power: damage tanked mid-wind-up comes back flat, on top of every multiplier.
        value += painBonus;

        return new Damage
        {
            value = value,
            type = DamageType.sharp,
            isCritical = crit,
            sourcePosition = origin,
            knockbackForce = stats.GetValue(StatType.AxeThrowKnockback)
                * (1f + ChargeLevel * config.knockbackPerChargeLevel),
        };
    }

    /// <summary>
    ///     A sphere cast that starts overlapping a collider reports distance 0 and a zero hit point —
    ///     fall back to the collider's closest point so feedback/lodge positions stay sane.
    /// </summary>
    private static Vector3 HitPointOf(RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        if (hit.distance > 0f)
        {
            return hit.point;
        }
        return hit.collider.ClosestPoint(origin + direction * 0.1f);
    }

    /// <summary>Distance-sorts cast hits without allocating a comparison delegate per throw (the PlayerBow shape).</summary>
    private class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private static IDamageable ResolveDamageable(Collider collider)
    {
        if (!collider.TryGetComponent(out IDamageable damageable))
        {
            damageable = collider.GetComponentInParent<IDamageable>();
        }
        return damageable;
    }
}
