using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The player's bow. Holding aim (right click, the Synty <see cref="InputReader" />'s
///     <c>onAimActivated</c>/<c>onAimDeactivated</c> events) swaps the sword model for the bow and
///     starts drawing: power builds in discrete charge levels while the aim is held (the
///     <see cref="PlayerAttack" /> convention — each level takes another
///     <see cref="BowSO.chargeTimePerLevel" /> seconds, capped at <see cref="StatType.BowMaxChargeLevels" />).
///     Pressing attack (left click) while aiming fires an <see cref="ArrowProjectile" /> — a real
///     projectile (the <see cref="AxeProjectile" /> convention) that flies at
///     <see cref="StatType.BowArrowSpeed" /> and drops under <see cref="BowSO.arrowGravity" />, so
///     distant shots must be aimed above the target; the "Swift Arrows" line speeds arrows up, which
///     also flattens the arc (less flight time = quadratically less drop). The projectile calls back
///     into <see cref="ApplyArrowHit" /> when it strikes something, so every arrow skill line behaves
///     exactly as it did when arrows were hitscan. With no arrow prefab wired the bow degrades to the
///     old hitscan + <see cref="BowTracer" /> streak. After each shot the draw restarts for the next.
///
///     While aiming the sword can't swing: the vendored controller fires its <c>StartAttack</c>
///     animator trigger on every attack press, so this component clears that trigger (and the
///     <c>IsHoldingAttack</c> bool) every frame while aiming — the animator only consumes triggers
///     after all Updates, so the swing state never starts and the sword's animation-event-driven
///     <see cref="DamageTrigger" /> never activates. (<see cref="PlayerAttack" /> also skips its
///     sword-charge timing while <see cref="IsAiming" />.)
///
///     Aim presentation: this component drives the optional <c>IsAiming</c> bool / <c>BowFire</c>
///     trigger animator params (the Synty Bow Combat draw/aim/shoot states live on their own
///     masked layer — see TODO.md; missing params degrade to a one-time warning), while
///     <see cref="BowAimCamera" /> zooms the camera over the shoulder and
///     <see cref="BowCrosshairUI" /> fades in a crosshair, both polling <see cref="IsAiming" />
///     (the <see cref="SwordChargeFeedback" /> pattern).
///
///     Arrow damage reads <see cref="PlayerStats" /> the way the sword's <see cref="DamageTrigger" />
///     does — bases registered in <c>Start</c> from <see cref="BowSO" />, crits rolled against the
///     shared <see cref="StatType.CritChance" />/<see cref="StatType.CritMultiplier" />, and the
///     global <see cref="StatType.AllDamageMultiplier" /> applied. The bow skill lines all hang off
///     stats (base 0 = locked): Multi Shot arcs extra arrows, Bounce Shot chains a hit to one nearby
///     enemy, Impulse/Storm Arrows let the orb buffs (<see cref="ImpulseBuff" /> /
///     <see cref="ChainLightningBuff" />) apply to arrows, Pickup Arrows collect coins/orbs along the
///     flight path, and Precision Shot multiplies damage on <see cref="VulnerableSpot" /> hits.
///
///     Further arrow skill lines (all stat-gated the same way): Exploding Heads detonates an impulse
///     blast on VulnerableSpot hits, Brain Freeze chills the headshot target (via <see cref="SlowStatus" />),
///     Arrows of Midas rolls a chance to convert the hit enemy golden
///     (<see cref="GoldenGoblin.TryConvertToGolden" />), Unstable Orbs lets the main arrow detonate
///     Impulse/Lightning Orbs along its path (an impulse blast, or a buff-independent
///     <see cref="ChainLightning.ForceChain" />, around the orb), and Flaming Arrows adds a bonus
///     elemental fire hit to every arrow plus a chance to remote-detonate Bombers
///     (<see cref="BomberAttack.Detonate" />). Freezing Draw lives in its own
///     <see cref="FreezingDraw" /> component (the <see cref="SwordChargeFeedback" /> polling pattern),
///     but its stat bases are registered here with the rest of the bow's.
/// </summary>
/// <summary>
///     Everything a listener needs to react to one arrow striking a damageable target — richer than
///     the <see cref="DamageTrigger.OnHit" /> triple because arrow reactions need the flight
///     direction (to orient a stuck arrow / blood spray), the exact collider struck (to parent a
///     stuck arrow to the right bone), and whether a <see cref="VulnerableSpot" /> was hit (crit is
///     already on <see cref="Damage.isCritical" />). Raised once per arrow that damaged something;
///     bounces and the Flaming Arrows bonus hit don't re-raise it (no physical arrow lands there).
/// </summary>
public struct ArrowImpact
{
    public IDamageable target;
    public Damage damage;
    /// <summary>World point where the arrow struck.</summary>
    public Vector3 point;
    /// <summary>Normalized flight direction of the arrow.</summary>
    public Vector3 direction;
    /// <summary>The collider the hitscan stopped at — parent stuck-arrow props here so they ride animation/ragdoll.</summary>
    public Collider hitCollider;
    /// <summary>True when the arrow struck one of the target's <see cref="VulnerableSpot" /> colliders.</summary>
    public bool hitVulnerableSpot;
    /// <summary>The specific VulnerableSpot struck, when <see cref="hitVulnerableSpot" /> is true — lets a stuck-arrow prop anchor to that exact spot rather than the general hit collider.</summary>
    public VulnerableSpot vulnerableSpot;
    /// <summary>Charge level of the draw that fired this arrow.</summary>
    public int chargeLevel;
}

public class PlayerBow : MonoBehaviour, IChargedAimWeapon
{
    [Tooltip("Synty InputReader that raises the aim and attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private BowSO config;

    [Header("Aiming")]
    [Tooltip("Camera whose centre the arrow flies toward. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Where arrows (and their tracer streaks) originate — e.g. the bow model or the player's chest. Defaults to this transform.")]
    [SerializeField] private Transform arrowOrigin;
    [Tooltip("Layers an arrow can hit. Exclude the player's own layer if the ray ever clips the player.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("The player rig's Animator — used to cancel the sword-swing trigger while aiming. Synty rigs keep it on a child.")]
    [SerializeField] private Animator playerAnimator;

    [Header("Weapon models (optional)")]
    [Tooltip("Sword model shown while not aiming. Optional.")]
    [SerializeField] private GameObject swordModel;
    [Tooltip("Bow model shown while aiming. Optional (no bow model exists yet).")]
    [SerializeField] private GameObject bowModel;

    [Header("Projectile")]
    [Tooltip("ArrowProjectile prefab instantiated per arrow — a real projectile with travel speed and drop. Unassigned = arrows fall back to instant hitscan (the pre-projectile behaviour).")]
    [SerializeField] private ArrowProjectile arrowPrefab;

    [Header("Visuals & feedback (optional)")]
    [Tooltip("BowTracer prefab instantiated per bounce arc (and per arrow while the bow is still hitscan) to draw the shot.")]
    [SerializeField] private BowTracer tracerPrefab;
    [Tooltip("Played when aiming starts (draw sound / zoom).")]
    [SerializeField] private MMF_Player drawFeedback;
    [Tooltip("Played on every shot.")]
    [SerializeField] private MMF_Player fireFeedback;

    [Header("Skill integrations (optional)")]
    [Tooltip("The player's Impulse buff, for the Impulse Arrow skill. Defaults to Player.Instance's.")]
    [SerializeField] private ImpulseBuff impulseBuff;
    [Tooltip("The player's ChainLightning component, for the Storm Arrow skill. Defaults to Player.Instance's.")]
    [SerializeField] private ChainLightning chainLightning;
    [Tooltip("Layers a Bounce Shot can hit (the ChainLightning convention: exclude player/environment).")]
    [SerializeField] private LayerMask bounceLayers = ~0;

    [Tooltip("Optional: the player's mount. While mounted, aiming is gated behind the Horse Archer skill node.")]
    [SerializeField] private PlayerMount mount;

    /// <summary>Fired once per arrow (or bounce) that actually damaged a target, with the world hit point — the <see cref="DamageTrigger.OnHit" /> shape, so feedback listeners can treat bow and sword alike.</summary>
    public event Action<IDamageable, Damage, Vector3> OnHit;

    /// <summary>
    ///     Fired once per physical arrow that damaged a target, with the full <see cref="ArrowImpact" />
    ///     detail — consumed by <see cref="StuckArrowSpawner" /> (embedded arrow props) and
    ///     <see cref="BowHitFeedback" /> (impact sound + blood). Listeners that don't care about the
    ///     extra detail should keep using <see cref="OnHit" />.
    /// </summary>
    public event Action<ArrowImpact> OnArrowImpact;

    /// <summary>Fired once per shot (hit or miss), the moment the arrows leave the bow — cosmetic listeners like <see cref="BowPropAnimator" /> sync their release to this.</summary>
    public event Action OnFired;

    /// <summary>True once the "Bow" skill node has been bought; while false, aiming does nothing and the sword stays out.</summary>
    public bool IsUnlocked => !anyError && stats.GetValue(StatType.BowUnlocked) >= 1f;

    /// <summary>True while the aim button is held and the bow is drawn.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>Charge level of the draw in progress, 0..BowMaxChargeLevels. Useful for VFX/feedback (the PlayerAttack convention).</summary>
    public int ChargeLevel { get; private set; }

    /// <summary>Levels the current draw can reach.</summary>
    public int MaxChargeLevels => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.BowMaxChargeLevels));

    /// <summary>
    ///     Fraction of the post-shot fire cooldown elapsed: 0 the instant a shot fires, 1 when the
    ///     bow can fire again (and before the first shot). Cosmetic listeners like
    ///     <see cref="BowReloadUI" /> poll this for radial-fill reload indicators.
    /// </summary>
    public float CooldownFraction
    {
        get
        {
            if (anyError || config.fireCooldownSeconds <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01((Time.time - lastFireTime) / config.fireCooldownSeconds);
        }
    }

    /// <summary>True while the bow is between shots and can't fire yet.</summary>
    public bool IsCoolingDown => CooldownFraction < 1f;

    // Aim-camera framing surfaced for BowAimCamera (see IChargedAimWeapon) — the values live on BowSO.
    public int RangedWeaponType => config != null ? config.rangedWeaponType : 0;
    public float AimCameraDistance => config != null ? config.aimCameraDistance : 2.75f;
    public float AimCameraHorizontalOffset => config != null ? config.aimCameraHorizontalOffset : 0.7f;
    public float AimFieldOfViewPercent => config != null ? config.aimFieldOfViewPercent : 1f;
    public float AimBlendSeconds => config != null ? config.aimBlendSeconds : 0.2f;

    /// <summary>
    ///     A target arrows fly through in addition to the wielder — the horse under a mounted
    ///     player, whose neck would otherwise stop every close-range shot (the
    ///     <see cref="DamageTrigger" /> SetIgnoredTarget sibling). Null = none. Bounces and impulse
    ///     blasts skip it too. Set/cleared by <c>PlayerMount</c>.
    /// </summary>
    public void SetIgnoredTarget(IDamageable target)
    {
        ignoredTarget = target;
    }

    private const int MaxRayHits = 64;
    private const int MaxOverlapResults = 64;

    private readonly RaycastHit[] rayBuffer = new RaycastHit[MaxRayHits];
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> blastHitTargets = new HashSet<IDamageable>();

    private IDamageable ownerDamageable;
    private IDamageable ignoredTarget;
    private int startAttackHash;
    private int isHoldingAttackHash;
    private int isAimingHash;
    private int bowFireHash;
    private int rangedWeaponTypeHash;
    private int weaponTypeHash;
    private bool hasAimAnimatorParams;
    private bool hasWeaponTypeParam;
    private float chargeStartTime;
    private float lastFireTime = Mathf.NegativeInfinity;
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
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; the bow can't read aim/fire input.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("BowSO is not assigned in the inspector.");
            anyError = true;
        }
        if (playerAnimator == null)
        {
            Debug.LogError("Player Animator is not assigned or found; the bow can't suppress sword swings while aiming.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (arrowOrigin == null)
        {
            arrowOrigin = transform;
        }
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        startAttackHash = Animator.StringToHash("StartAttack");
        isHoldingAttackHash = Animator.StringToHash("IsHoldingAttack");
        isAimingHash = Animator.StringToHash("IsAiming");
        bowFireHash = Animator.StringToHash("BowFire");
        rangedWeaponTypeHash = Animator.StringToHash("RangedWeaponType");
        weaponTypeHash = Animator.StringToHash("WeaponType");

        // The bow animator states are optional wiring — without the IsAiming/BowFire params the bow
        // still works, the rig just keeps its sword pose (checked once so missing params don't spam
        // per-set warnings every aim).
        bool hasIsAiming = false;
        bool hasBowFire = false;
        hasWeaponTypeParam = false;
        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.nameHash == isAimingHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsAiming = true;
            }
            else if (parameter.nameHash == bowFireHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasBowFire = true;
            }
            else if ((parameter.nameHash == rangedWeaponTypeHash || parameter.nameHash == weaponTypeHash) && parameter.type == AnimatorControllerParameterType.Int)
            {
                hasWeaponTypeParam = true;
            }
        }
        hasAimAnimatorParams = hasIsAiming && hasBowFire;
        if (!hasAimAnimatorParams)
        {
            Debug.LogWarning("PlayerBow: the player Animator has no IsAiming (Bool) / BowFire (Trigger) parameters — bow aim/draw/shoot animations won't play until the Bow layer is wired (see TODO.md).");
        }

        // The bow never damages its wielder (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();

        if (arrowPrefab == null)
        {
            Debug.LogWarning("PlayerBow: no ArrowProjectile prefab assigned — arrows fall back to instant hitscan (no travel speed or drop) until the arrow prefab is wired (see TODO.md).");
        }

        // Register the authored SO values as the stat bases; skill nodes layer on top without ever
        // mutating the asset. Everything at 0 is a locked skill line.
        // The bow itself is gated: base 0 = locked until the "Bow" skill node is bought (unlike the
        // sword, which is free out of the box).
        stats.SetBase(StatType.BowUnlocked, 0f);
        stats.SetBase(StatType.BowDamage, config.baseDamage);
        stats.SetBase(StatType.BowArrowSpeed, config.baseArrowSpeed);
        stats.SetBase(StatType.BowMaxChargeLevels, config.baseMaxChargeLevels);
        stats.SetBase(StatType.BowChargeDamageBonus, config.baseChargeDamageBonus);
        stats.SetBase(StatType.BowMultishotArrows, 0f);
        stats.SetBase(StatType.BowMultishotDamagePercent, config.baseMultishotDamagePercent);
        stats.SetBase(StatType.BowBounceChance, 0f);
        stats.SetBase(StatType.BowImpulseArrows, 0f);
        stats.SetBase(StatType.BowStormArrows, 0f);
        stats.SetBase(StatType.BowPickupArrows, 0f);
        stats.SetBase(StatType.BowPrecisionDamageBonus, 0f);
        stats.SetBase(StatType.FreezingDrawSlowPercent, 0f);
        stats.SetBase(StatType.BrainFreezeSlowPercent, 0f);
        stats.SetBase(StatType.SlowDurationBonusSeconds, 0f);
        stats.SetBase(StatType.ExplodingHeadsDamagePercent, 0f);
        stats.SetBase(StatType.MidasChance, 0f);
        stats.SetBase(StatType.BowUnstableOrbs, 0f);
        stats.SetBase(StatType.FlamingArrowsDamagePercent, 0f);
        stats.SetBase(StatType.FlamingArrowsBomberDetonateChance, 0f);
        // The BowUnlocked gate, mounted edition: base 0 = the bow can't be drawn from horseback
        // until the "Horse Archer" node is bought.
        stats.SetBase(StatType.HorseArcheryUnlocked, 0f);

        // Missing buff/chain components are not errors — those skill lines are optional features
        // (the DamageTrigger ImpulseBuff fallback idiom).
        if (impulseBuff == null && Player.Instance != null)
        {
            impulseBuff = Player.Instance.GetComponentInChildren<ImpulseBuff>();
        }
        if (chainLightning == null && Player.Instance != null)
        {
            chainLightning = Player.Instance.GetComponentInChildren<ChainLightning>();
        }

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
        // E.g. PlayerDeath disabling controls mid-aim: put the sword back so the corpse isn't holding a bow.
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

        // Keep the charge level live as the draw grows (the PlayerAttack convention).
        int maxLevels = MaxChargeLevels;
        int level = config.chargeTimePerLevel > 0f
            ? Mathf.FloorToInt((Time.time - chargeStartTime) / config.chargeTimePerLevel)
            : maxLevels;
        ChargeLevel = Mathf.Clamp(level, 0, maxLevels);

        // The vendored controller sets StartAttack/IsHoldingAttack on every attack press (input
        // callbacks run before Update; the animator consumes triggers after all Updates), so clearing
        // them every aiming frame reliably suppresses the sword swing regardless of event order.
        playerAnimator.ResetTrigger(startAttackHash);
        playerAnimator.SetBool(isHoldingAttackHash, false);
    }

    private void StartAim()
    {
        if (anyError || !IsUnlocked)
        {
            // Bow still locked: leave IsAiming false so the sword swing (and everything else) works normally.
            return;
        }

        // Mounted archery is its own skill: without Horse Archer, aiming from the saddle does
        // nothing (the sword still swings via MountedCombat).
        if (mount != null && mount.IsMounted && stats.GetValue(StatType.HorseArcheryUnlocked) < 1f)
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
        if (hasWeaponTypeParam && playerAnimator != null)
        {
            playerAnimator.SetInteger(rangedWeaponTypeHash, RangedWeaponType);
            playerAnimator.SetInteger(weaponTypeHash, RangedWeaponType);
        }

        if (swordModel != null)
        {
            swordModel.SetActive(false);
        }
        if (bowModel != null)
        {
            bowModel.SetActive(true);
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
            playerAnimator.ResetTrigger(bowFireHash);
        }
        if (hasWeaponTypeParam && playerAnimator != null)
        {
            playerAnimator.SetInteger(rangedWeaponTypeHash, 0);
            playerAnimator.SetInteger(weaponTypeHash, 0);
        }

        if (swordModel != null)
        {
            swordModel.SetActive(true);
        }
        if (bowModel != null)
        {
            bowModel.SetActive(false);
        }
    }

    private void HandleAttackPressed()
    {
        if (anyError || !IsAiming)
        {
            return;
        }
        if (Time.time - lastFireTime < config.fireCooldownSeconds)
        {
            return;
        }

        lastFireTime = Time.time;
        Fire();

        // The draw restarts for the next shot while the aim is still held.
        ChargeLevel = 0;
        chargeStartTime = Time.time;
    }

    private void Fire()
    {
        Vector3 origin = arrowOrigin.position;
        Vector3 mainDirection = ResolveAimDirection(origin);

        if (fireFeedback != null)
        {
            fireFeedback.PlayFeedbacks();
        }
        if (hasAimAnimatorParams)
        {
            playerAnimator.SetTrigger(bowFireHash);
        }
        OnFired?.Invoke();

        FireArrow(origin, mainDirection, 1f, isMainArrow: true);

        // Multi Shot: extra arrows fan out in a flat arc alternating left/right of the main arrow.
        int extraArrows = Mathf.RoundToInt(stats.GetValue(StatType.BowMultishotArrows));
        float extraDamageScale = stats.GetValue(StatType.BowMultishotDamagePercent);
        for (int i = 1; i <= extraArrows; i++)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            Vector3 direction = Quaternion.AngleAxis(sign * step * config.multishotSpreadDegrees, Vector3.up) * mainDirection;
            FireArrow(origin, direction, extraDamageScale, isMainArrow: false);
        }
    }

    /// <summary>
    ///     Arrows fly toward whatever the camera centre is looking at (the third-person hitscan
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
            return arrowOrigin.forward;
        }

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.origin + ray.direction * config.maxRange;
        if (TryHitscan(ray.origin, ray.direction, out RaycastHit hit, out _, out _, out _))
        {
            aimPoint = hit.point;
        }
        return (aimPoint - origin).normalized;
    }

    /// <summary>
    ///     One arrow. With an <see cref="arrowPrefab" /> wired, launches a real
    ///     <see cref="ArrowProjectile" /> that flies, drops, and calls <see cref="ApplyArrowHit" />
    ///     back when it strikes something; without one, falls back to the original instant hitscan.
    ///     Either way the hit gets charge, crit, precision, impulse, and global multipliers, then the
    ///     storm/bounce and per-path pickup/orb skill effects.
    /// </summary>
    private void FireArrow(Vector3 origin, Vector3 direction, float damageScale, bool isMainArrow)
    {
        if (arrowPrefab != null)
        {
            ArrowProjectile arrow = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(direction));
            arrow.Launch(this, new ArrowProjectile.LaunchSpec
            {
                origin = origin,
                direction = direction,
                speed = stats.GetValue(StatType.BowArrowSpeed),
                gravity = config.arrowGravity,
                maxRange = config.maxRange,
                radius = config.arrowRadius,
                hitLayers = hitLayers,
                chargeLevel = ChargeLevel,
                damageScale = damageScale,
                isMainArrow = isMainArrow,
                owner = ownerDamageable,
                ignoredTarget = ignoredTarget,
                collectPickups = stats.GetValue(StatType.BowPickupArrows) >= 1f,
                detonateOrbs = isMainArrow && stats.GetValue(StatType.BowUnstableOrbs) >= 1f,
            });
            return;
        }

        Vector3 endPoint = origin + direction * config.maxRange;

        if (TryHitscan(origin, direction, out RaycastHit hit, out IDamageable target, out bool hitVulnerableSpot, out VulnerableSpot vulnerableSpot))
        {
            endPoint = hit.point;
        }

        SpawnTracer(origin, endPoint);

        // Unstable Orbs: the main arrow detonates any Impulse/Lightning Orbs along its path — before
        // the pickup sweep, so a detonated orb can't also be collected as a buff.
        if (isMainArrow && stats.GetValue(StatType.BowUnstableOrbs) >= 1f)
        {
            DetonateOrbsAlongPath(origin, endPoint, damageScale, ChargeLevel);
        }

        // Pickup Arrows: sweep the flight path for coins/orbs, whoever the arrow hit.
        if (stats.GetValue(StatType.BowPickupArrows) >= 1f)
        {
            CollectPickupsAlongPath(origin, endPoint);
        }

        if (target != null)
        {
            ApplyArrowHit(target, endPoint, direction, hit.collider, hitVulnerableSpot, vulnerableSpot, damageScale, ChargeLevel);
        }
    }

    /// <summary>
    ///     Everything that happens when an arrow strikes a damageable target — damage (charge, crit,
    ///     precision, impulse, global multipliers), the impact events, and the on-hit skill lines
    ///     (Flaming Arrows, Exploding Heads, Brain Freeze, Midas, Storm Arrow, Bounce Shot). Shared by
    ///     the hitscan fallback and <see cref="ArrowProjectile" />'s impact callback;
    ///     <paramref name="chargeLevel" /> is the draw level captured when the arrow left the bow
    ///     (the <see cref="AxeProjectile.LaunchSpec" /> convention — the draw restarts during flight).
    /// </summary>
    public void ApplyArrowHit(IDamageable target, Vector3 hitPoint, Vector3 direction, Collider hitCollider, bool hitVulnerableSpot, VulnerableSpot vulnerableSpot, float damageScale, int chargeLevel)
    {
        if (anyError || target == null)
        {
            return;
        }

        Vector3 endPoint = hitPoint;
        Vector3 origin = arrowOrigin != null ? arrowOrigin.position : transform.position;

        // Flaming Arrows: a winning roll detonates a Bomber on the spot — rolled before the
        // arrow's own damage lands, so a lethal arrow can't rob the skill of its explosion (a
        // detonated bomber is dead by the time the arrow damage would apply, which Health ignores).
        if (UnityEngine.Random.value < stats.GetValue(StatType.FlamingArrowsBomberDetonateChance)
            && target is Component bomberComponent)
        {
            bomberComponent.GetComponentInParent<BomberAttack>()?.Detonate();
        }

        Damage damage = BuildArrowDamage(origin, damageScale, hitVulnerableSpot, chargeLevel);
        target.ReceiveDamage(damage);
        OnHit?.Invoke(target, damage, endPoint);
        OnArrowImpact?.Invoke(new ArrowImpact
        {
            target = target,
            damage = damage,
            point = endPoint,
            direction = direction,
            hitCollider = hitCollider,
            hitVulnerableSpot = hitVulnerableSpot,
            vulnerableSpot = vulnerableSpot,
            chargeLevel = chargeLevel,
        });

        // Flaming Arrows: bonus fire damage as its own elemental hit on the same target (the
        // chain-lightning shape — a separate derived instance, so it pops its own damage number
        // and can never be parried as melee). Direct arrow hits only; bounces don't reroll it.
        float fireDamagePercent = stats.GetValue(StatType.FlamingArrowsDamagePercent);
        if (fireDamagePercent > 0f)
        {
            Damage fireDamage = new Damage
            {
                value = damage.value * fireDamagePercent,
                type = DamageType.elemental,
                sourcePosition = origin,
            };
            target.ReceiveDamage(fireDamage);
            OnHit?.Invoke(target, fireDamage, endPoint);
        }

        if (hitVulnerableSpot)
        {
            // Exploding Heads: the headshot detonates an impulse blast at the point of impact.
            float explodePercent = stats.GetValue(StatType.ExplodingHeadsDamagePercent);
            if (explodePercent > 0f)
            {
                SpawnImpulseBlast(endPoint, damage.value * explodePercent);
            }

            // Brain Freeze: the headshot chills the target's movement and animation.
            float freezeFraction = stats.GetValue(StatType.BrainFreezeSlowPercent);
            if (freezeFraction > 0f && target is Component targetComponent)
            {
                SlowStatus slow = SlowStatus.GetOrAdd(targetComponent);
                if (slow != null)
                {
                    slow.ApplySlow(freezeFraction, config.brainFreezeSeconds + stats.GetValue(StatType.SlowDurationBonusSeconds));
                }
            }
        }

        // Arrows of Midas: chance to convert a regular enemy into a golden one.
        if (UnityEngine.Random.value < stats.GetValue(StatType.MidasChance) && target is Component midasComponent)
        {
            GoldenGoblin golden = midasComponent.GetComponentInParent<GoldenGoblin>();
            if (golden != null)
            {
                golden.TryConvertToGolden();
            }
        }

        // Storm Arrow: hand the hit to the Chain Lightning skill line — it checks its own buff state.
        if (stats.GetValue(StatType.BowStormArrows) >= 1f && chainLightning != null)
        {
            chainLightning.TryChain(target, damage.value, endPoint);
        }

        // Bounce Shot: chance to arc to one additional nearby enemy for the same damage.
        if (UnityEngine.Random.value < stats.GetValue(StatType.BowBounceChance))
        {
            TryBounce(target, damage, endPoint);
        }
    }

    /// <summary>
    ///     Sorted hitscan along a ray: skips the wielder and stray pickup/ability triggers, stopping
    ///     at the first damageable target or solid piece of environment. Returns true when anything
    ///     stopped the arrow; <paramref name="target" /> is null for environment.
    ///     <paramref name="hitVulnerableSpot" /> is true when the arrow struck one of the target's
    ///     <see cref="VulnerableSpot" /> colliders — checked across every hit the ray scored on that
    ///     target, since its body capsule can sit in front of a head sphere along the same ray.
    ///     <paramref name="vulnerableSpot" /> is the specific spot found, or null.
    /// </summary>
    private bool TryHitscan(Vector3 origin, Vector3 direction, out RaycastHit blockingHit, out IDamageable target, out bool hitVulnerableSpot, out VulnerableSpot vulnerableSpot)
    {
        blockingHit = default;
        target = null;
        hitVulnerableSpot = false;
        vulnerableSpot = null;

        int count = Physics.RaycastNonAlloc(origin, direction, rayBuffer, config.maxRange, hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(rayBuffer, 0, count, HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = rayBuffer[i];
            IDamageable damageable = ResolveDamageable(hit.collider);

            if (damageable != null && (damageable == ownerDamageable || (ignoredTarget != null && damageable == ignoredTarget)))
            {
                continue;
            }

            if (damageable != null)
            {
                blockingHit = hit;
                target = damageable;
                for (int j = i; j < count; j++)
                {
                    if (ResolveDamageable(rayBuffer[j].collider) != target)
                    {
                        continue;
                    }
                    VulnerableSpot spot = rayBuffer[j].collider.GetComponentInParent<VulnerableSpot>();
                    if (spot != null)
                    {
                        hitVulnerableSpot = true;
                        vulnerableSpot = spot;
                        break;
                    }
                }
                return true;
            }

            // Trigger colliders with no damageable (coins, orbs, hitboxes) don't stop an arrow.
            if (!hit.collider.isTrigger)
            {
                blockingHit = hit;
                return true;
            }
        }
        return false;
    }

    /// <summary>Distance-sorts raycast hits without allocating a comparison delegate per shot (shared with <see cref="ArrowProjectile" />, the PlayerThrownAxe precedent).</summary>
    public class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    /// <summary>Finds the damageable a collider belongs to, if any (shared with <see cref="ArrowProjectile" />).</summary>
    public static IDamageable ResolveDamageable(Collider collider)
    {
        if (!collider.TryGetComponent(out IDamageable damageable))
        {
            damageable = collider.GetComponentInParent<IDamageable>();
        }
        return damageable;
    }

    private Damage BuildArrowDamage(Vector3 origin, float damageScale, bool hitVulnerableSpot, int chargeLevel)
    {
        float value = stats.GetValue(StatType.BowDamage);
        value *= 1f + chargeLevel * stats.GetValue(StatType.BowChargeDamageBonus);

        // Crits share the sword's stats so Keen Eye/Critical Damage benefit both weapons.
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

        if (hitVulnerableSpot)
        {
            float precisionBonus = stats.GetValue(StatType.BowPrecisionDamageBonus);
            if (precisionBonus > 0f)
            {
                value *= 1f + precisionBonus;
            }
        }

        value *= damageScale;

        // Impulse Arrow: the orb buff stamps its fling rating onto arrows exactly the way the sword's
        // DamageTrigger stamps swings.
        float knockbackForce = 0f;
        if (stats.GetValue(StatType.BowImpulseArrows) >= 1f && impulseBuff != null && impulseBuff.IsActive)
        {
            value *= impulseBuff.DamageMultiplier;
            knockbackForce = config.knockbackBlastForce * impulseBuff.KnockbackMultiplier;
        }

        return new Damage
        {
            value = value,
            type = DamageType.sharp,
            isCritical = crit,
            sourcePosition = origin,
            knockbackForce = knockbackForce,
            isProjectile = true,
        };
    }

    /// <summary>Bounce Shot: the arrow arcs from its hit to the nearest other enemy in range for the same damage.</summary>
    private void TryBounce(IDamageable alreadyHit, Damage damage, Vector3 hitPoint)
    {
        IDamageable best = null;
        Vector3 bestPosition = hitPoint;
        float bestSqrDistance = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(hitPoint, config.bounceRadius, overlapBuffer, bounceLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            IDamageable damageable = ResolveDamageable(collider);
            if (damageable == null || damageable == alreadyHit || damageable == ownerDamageable
                || (ignoredTarget != null && damageable == ignoredTarget))
            {
                continue;
            }

            float sqrDistance = (collider.transform.position - hitPoint).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = damageable;
                bestPosition = collider.transform.position;
            }
        }

        if (best == null)
        {
            return;
        }

        Damage bounceDamage = new Damage
        {
            value = damage.value,
            type = damage.type,
            isCritical = damage.isCritical,
            sourcePosition = hitPoint,
            knockbackForce = damage.knockbackForce,
        };
        best.ReceiveDamage(bounceDamage);
        OnHit?.Invoke(best, bounceDamage, bestPosition);
        SpawnTracer(hitPoint, bestPosition);
    }

    /// <summary>Pickup Arrows: collect any coin/orb pickups within a capsule along the arrow's flight path. <see cref="ArrowProjectile" /> calls this per tick segment as it flies.</summary>
    public void CollectPickupsAlongPath(Vector3 from, Vector3 to)
    {
        GameObject collector = Player.Instance != null ? Player.Instance.gameObject : gameObject;
        int count = Physics.OverlapCapsuleNonAlloc(from, to, config.pickupRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            Coin coin = collider.GetComponentInParent<Coin>();
            if (coin != null)
            {
                coin.TryCollect(collector);
                continue;
            }
            ImpulseOrb impulseOrb = collider.GetComponentInParent<ImpulseOrb>();
            if (impulseOrb != null)
            {
                impulseOrb.TryCollect(collector);
                continue;
            }
            LightningOrb lightningOrb = collider.GetComponentInParent<LightningOrb>();
            if (lightningOrb != null)
            {
                lightningOrb.TryCollect(collector);
                continue;
            }
            HealthPack healthPack = collider.GetComponentInParent<HealthPack>();
            if (healthPack != null)
            {
                healthPack.TryCollect(collector);
            }
        }
    }

    /// <summary>
    ///     Unstable Orbs: detonates every Impulse/Lightning Orb the main arrow's ray crosses — an
    ///     impulse blast around an Impulse Orb, a buff-independent chain lightning around a Lightning
    ///     Orb, each fuelled by this arrow's damage. The orb is consumed (no buff granted).
    ///     <see cref="ArrowProjectile" /> calls this per tick segment as the main arrow flies.
    /// </summary>
    public void DetonateOrbsAlongPath(Vector3 from, Vector3 to, float damageScale, int chargeLevel)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return;
        }

        // Same ~0 mask as the pickup sweep: orb triggers may live off the arrow's hitLayers.
        int count = Physics.RaycastNonAlloc(from, delta / distance, rayBuffer, distance, ~0, QueryTriggerInteraction.Collide);
        float arrowDamage = 0f;
        bool damageRolled = false;

        for (int i = 0; i < count; i++)
        {
            Collider collider = rayBuffer[i].collider;
            ImpulseOrb impulseOrb = collider.GetComponentInParent<ImpulseOrb>();
            LightningOrb lightningOrb = impulseOrb == null ? collider.GetComponentInParent<LightningOrb>() : null;
            if (impulseOrb == null && lightningOrb == null)
            {
                continue;
            }

            if (!damageRolled)
            {
                // One representative (non-precision) damage roll fuels every detonation on this shot.
                arrowDamage = BuildArrowDamage(from, damageScale, hitVulnerableSpot: false, chargeLevel).value;
                damageRolled = true;
            }

            Vector3 orbPosition = collider.transform.position;
            if (impulseOrb != null && impulseOrb.TryDetonate())
            {
                SpawnImpulseBlast(orbPosition, arrowDamage);
            }
            else if (lightningOrb != null && lightningOrb.TryDetonate() && chainLightning != null)
            {
                chainLightning.ForceChain(arrowDamage, orbPosition);
            }
        }
    }

    /// <summary>
    ///     An impulse blast (Exploding Heads / Unstable Orbs): damages every enemy around
    ///     <paramref name="center" />, stamping each hit with the <see cref="BowSO" /> blast
    ///     power/force so <see cref="ImpulseReceiver" /> flings them (the Death Nova shape).
    /// </summary>
    private void SpawnImpulseBlast(Vector3 center, float damageValue)
    {
        if (damageValue <= 0f)
        {
            return;
        }

        blastHitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, config.impulseBlastRadius, overlapBuffer, bounceLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            IDamageable damageable = ResolveDamageable(overlapBuffer[i]);
            if (damageable == null || damageable == ownerDamageable
                || (ignoredTarget != null && damageable == ignoredTarget)
                || !blastHitTargets.Add(damageable))
            {
                continue;
            }

            damageable.ReceiveDamage(new Damage
            {
                value = damageValue,
                type = DamageType.blunt,
                sourcePosition = center,
                knockbackForce = config.knockbackBlastForce,
            });
        }
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        if (tracerPrefab == null)
        {
            return;
        }
        BowTracer tracer = Instantiate(tracerPrefab, from, Quaternion.identity);
        tracer.Show(from, to);
    }
}
