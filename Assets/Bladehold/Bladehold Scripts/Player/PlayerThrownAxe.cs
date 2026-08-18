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
///     aiming hurls the axe: a slow, real <b>projectile</b> (<see cref="AxeProjectile" />) that
///     damages and knocks back enemies as it travels — a swept line
///     <see cref="StatType.AxeThrowWidth" /> metres wide, sphere-cast from its last position to its
///     current one every physics tick so nothing tunnels through — up to a pierce budget
///     (<see cref="StatType.AxeThrowPierceCount" /> + charge), stopping at solid environment or when
///     the budget runs out. With the "Boomerang" node (<see cref="StatType.AxeBoomerangUnlocked" />)
///     it then flies back to the hand, hitting enemies on the return leg too. Charge raises damage,
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

    [Header("Projectile")]
    [Tooltip("Spinning-axe projectile instantiated per throw. REQUIRED — the projectile carries the throw's damage, not just its looks.")]
    [SerializeField] private AxeProjectile projectilePrefab;

    [Header("Feedback (optional)")]
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

    /// <summary>True while aiming and charging up a throw.</summary>
    public bool IsCharging => IsAiming && MaxChargeLevels > 0;

    /// <summary>Elapsed seconds of the current axe wind-up, clamped to [0, MaxChargeTime].</summary>
    public float CurrentChargeTime => IsAiming ? Mathf.Min(Time.time - chargeStartTime, MaxChargeTime) : 0f;

    /// <summary>Total time in seconds required to reach maximum charge levels.</summary>
    public float MaxChargeTime => MaxChargeLevels * ChargeTimePerLevel;

    /// <summary>Normalized charge progress [0..1] of the current hold.</summary>
    public float ChargeProgress => MaxChargeTime > 0f ? Mathf.Clamp01(CurrentChargeTime / MaxChargeTime) : 0f;

    /// <summary>Time in seconds required per charge level.</summary>
    public float ChargeTimePerLevel => config != null ? config.chargeTimePerLevel : 0.33f;

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
    public int RangedWeaponType => config != null ? config.rangedWeaponType : 1;
    public float AimCameraDistance => config != null ? config.aimCameraDistance : 2.75f;
    public float AimCameraHorizontalOffset => config != null ? config.aimCameraHorizontalOffset : 0.7f;
    public float AimFieldOfViewPercent => config != null ? config.aimFieldOfViewPercent : 1f;
    public float AimBlendSeconds => config != null ? config.aimBlendSeconds : 0.2f;

    private const int MaxCastHits = 64;

    // Kept only for the aim-direction raycast — the per-hit sweeping lives on AxeProjectile now.
    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];

    private IDamageable ownerDamageable;
    private IDamageable ignoredTarget;

    /// <summary>
    ///     A target thrown axes fly through in addition to the wielder — the horse under a mounted
    ///     player (the <see cref="PlayerBow.SetIgnoredTarget" /> convention). Null = none. Set/cleared by <see cref="PlayerMount" />.
    /// </summary>
    public void SetIgnoredTarget(IDamageable target)
    {
        ignoredTarget = target;
    }
    private int startAttackHash;
    private int isHoldingAttackHash;
    private int isAimingHash;
    private int fireHash;
    private int rangedWeaponTypeHash;
    private int weaponTypeHash;
    private bool hasAimAnimatorParams;
    private bool hasWeaponTypeParam;
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
            stats = GetComponentInParent<PlayerStats>();
        }
        if (playerAnimator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            playerAnimator = GetComponentInChildren<Animator>();
        }
        if (mount == null)
        {
            mount = GetComponentInParent<PlayerMount>();
        }
        if (rageBuff == null)
        {
            rageBuff = GetComponent<RageBuff>();
        }
        if (painIntoPower == null)
        {
            painIntoPower = GetComponentInParent<PainIntoPower>();
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
        if (projectilePrefab == null)
        {
            Debug.LogError("AxeProjectile prefab is not assigned — the projectile carries the throw's damage, so throws would do nothing.");
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
        rangedWeaponTypeHash = Animator.StringToHash("RangedWeaponType");
        weaponTypeHash = Animator.StringToHash("WeaponType");

        // The aim animator states are optional wiring, shared with the bow (the berserker's
        // override controller re-skins them) — without the params the axe still works, the rig just
        // keeps its melee pose (checked once so missing params don't spam warnings every aim).
        bool hasIsAiming = false;
        bool hasFire = false;
        hasWeaponTypeParam = false;
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
            else if ((parameter.nameHash == rangedWeaponTypeHash || parameter.nameHash == weaponTypeHash) && parameter.type == AnimatorControllerParameterType.Int)
            {
                hasWeaponTypeParam = true;
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
        // Boomerang is its own gate: base 0 = the axe lodges where it stops, until the node is bought.
        stats.SetBase(StatType.AxeBoomerangUnlocked, 0f);

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

        if (CursorLockManager.IsCursorUnlocked || (inputReader != null && !inputReader.IsAimPressed))
        {
            EndAim();
            return;
        }

        // Keep the charge level live as the wind-up grows (the PlayerAttack convention).
        int maxLevels = MaxChargeLevels;
        float elapsed = Time.time - chargeStartTime;
        float chargeRatio = ChargeTimePerLevel > 0f ? elapsed / ChargeTimePerLevel : maxLevels;
        chargeRatio = Mathf.Clamp(chargeRatio, 0f, maxLevels);
        ChargeLevel = Mathf.Clamp(Mathf.FloorToInt(chargeRatio), 0, maxLevels);

        // The vendored controller sets StartAttack/IsHoldingAttack on every attack press (input
        // callbacks run before Update; the animator consumes triggers after all Updates), so clearing
        // them every aiming frame reliably suppresses the melee swing.
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

        // Aiming while mounted requires the Horse Archer skill node (the bow convention).
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

        if (meleeWeaponModel != null)
        {
            meleeWeaponModel.SetActive(false);
        }
        if (thrownAxeModel != null)
        {
            thrownAxeModel.SetActive(true);
            foreach (Renderer r in thrownAxeModel.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }
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
        if (hasWeaponTypeParam && playerAnimator != null)
        {
            playerAnimator.SetInteger(rangedWeaponTypeHash, 0);
            playerAnimator.SetInteger(weaponTypeHash, 0);
        }

        if (meleeWeaponModel != null)
        {
            meleeWeaponModel.SetActive(true);
            foreach (Renderer r in meleeWeaponModel.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }
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
    ///     One throw: launches an <see cref="AxeProjectile" /> toward the crosshair carrying
    ///     everything decided at release time (charge, pierce budget, Pain into Power bonus). The
    ///     projectile does the damaging as it flies and calls back into
    ///     <see cref="CreateHitDamage" />/<see cref="ReportHit" /> per enemy swept.
    /// </summary>
    private void Throw()
    {
        Vector3 origin = (throwOrigin == null || throwOrigin == transform)
            ? transform.position + Vector3.up * 1.2f
            : throwOrigin.position;
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

        float currentRatio = ChargeTimePerLevel > 0f ? (Time.time - chargeStartTime) / ChargeTimePerLevel : MaxChargeLevels;
        currentRatio = Mathf.Clamp(currentRatio, 0f, MaxChargeLevels);

        AxeProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        projectile.Launch(this, new AxeProjectile.LaunchSpec
        {
            origin = origin,
            direction = direction,
            speed = config.projectileSpeed,
            maxRange = config.maxRange,
            radius = width * 0.5f,
            pierceBudget = pierceBudget,
            hitLayers = hitLayers,
            chargeLevel = ChargeLevel,
            chargeRatio = currentRatio,
            painBonus = painBonus,
            owner = ownerDamageable ?? GetComponentInParent<IDamageable>() ?? (Player.Instance != null ? Player.Instance.Damageable : null),
            ignoredTarget = ignoredTarget,
            boomerang = stats.GetValue(StatType.AxeBoomerangUnlocked) >= 1f,
            returnSpeedMultiplier = config.returnSpeedMultiplier,
            returnTarget = throwOrigin,
            // Wide Arc widens the damage sweep — scale the prop with it so the upgrade reads
            // visually (the SwordRange localScale convention).
            visualScale = width / Mathf.Max(0.05f, config.baseWidth),
        });
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

    /// <summary>
    ///     Builds one enemy's worth of throw damage — called by the in-flight <see cref="AxeProjectile" />
    ///     per target so every enemy gets a fresh crit roll (the <see cref="DamageTrigger" /> convention).
    ///     Charge and the Pain into Power bonus were fixed at release time and travel with the axe;
    ///     <paramref name="sourcePosition" /> is the axe's position just before the hit, so knockback
    ///     shoves along the flight line.
    /// </summary>
    public Damage CreateHitDamage(int chargeLevel, float painBonus, Vector3 sourcePosition, float chargeRatio = -1f)
    {
        if (anyError)
        {
            return new Damage { value = 0f, type = DamageType.sharp, sourcePosition = sourcePosition };
        }

        float value = stats.GetValue(StatType.AxeThrowDamage);
        float actualRatio = chargeRatio >= 0f ? chargeRatio : chargeLevel;
        float damagePerLevel = 1.9f + stats.GetValue(StatType.AxeThrowChargeDamageBonus);
        value *= 0.1f + damagePerLevel * actualRatio;

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
            sourcePosition = sourcePosition,
            knockbackForce = stats.GetValue(StatType.AxeThrowKnockback)
                * (1f + chargeLevel * config.knockbackPerChargeLevel),
            isProjectile = true,
        };
    }

    /// <summary>Raises <see cref="OnHit" /> for a hit the in-flight <see cref="AxeProjectile" /> landed, so RageBuff/telemetry listeners see projectile hits exactly like the old hitscan ones.</summary>
    public void ReportHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        OnHit?.Invoke(target, damage, hitPoint);
    }

    /// <summary>Distance-sorts cast hits without allocating a comparison delegate per sweep (the PlayerBow shape). Shared with <see cref="AxeProjectile" />.</summary>
    public class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    /// <summary>Finds the IDamageable a collider belongs to, walking up the hierarchy. Shared with <see cref="AxeProjectile" />.</summary>
    public static IDamageable ResolveDamageable(Collider collider)
    {
        if (!collider.TryGetComponent(out IDamageable damageable))
        {
            damageable = collider.GetComponentInParent<IDamageable>();
        }
        return damageable;
    }
}
