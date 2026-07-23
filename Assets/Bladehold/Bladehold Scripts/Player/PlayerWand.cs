using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The Mage's wand — the class's ranged option, the <see cref="PlayerThrownAxe" /> skeleton with
///     the spinning axe swapped for a fast magic-missile bolt. Holding aim (the Synty
///     <see cref="InputReader" />'s <c>onAimActivated</c>/<c>onAimDeactivated</c> events) charges the
///     shot in discrete levels (the bow-draw convention, tuned on <see cref="WandSO" />); pressing
///     attack while aiming fires a <see cref="MagicMissileProjectile" /> that stops on the first
///     enemy or solid environment it sweeps. Locked until the "Wand" skill node raises
///     <see cref="StatType.WandUnlocked" /> (the <see cref="StatType.BowUnlocked" /> convention).
///
///     Elemental behaviour is deliberately not here: <see cref="MageImbuement" /> listens to this
///     wand's <see cref="OnHit" /> exactly like it listens to the staff's <see cref="DamageTrigger" />,
///     so an imbued missile gets its explosion/chain/chill from the shared listener. The wand only
///     tells the missile the current element so its looks match (fire = fireball).
///
///     Melee suppression, animator params (<c>IsAiming</c>/<c>BowFire</c>, optional), model swap,
///     cooldown, and stat-base registration all follow <see cref="PlayerThrownAxe" /> exactly — see
///     its and <see cref="PlayerBow" />'s header comments for the reasoning. Damage reads the shared
///     <see cref="StatType.CritChance" />/<see cref="StatType.CritMultiplier" />/
///     <see cref="StatType.AllDamageMultiplier" />, with a fresh roll per missile, and stamps
///     <see cref="Damage.source" /> so runestones can tell a player blast from a Storm Witch splash.
///
///     There is no mounted casting (the bow's Horse Archer node has no wand equivalent) — aiming
///     from the saddle does nothing, exactly like the axe.
/// </summary>
public class PlayerWand : MonoBehaviour, IChargedAimWeapon
{
    [Tooltip("Synty InputReader that raises the aim and attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private WandSO config;

    [Header("Aiming")]
    [Tooltip("Camera whose centre the missile flies toward. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Where missiles originate — e.g. the wand tip or the player's chest. Defaults to this transform.")]
    [SerializeField] private Transform castOrigin;
    [Tooltip("Layers a missile can hit. Exclude the player's own layer if the cast ever clips the player.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("The player rig's Animator — used to cancel the melee-swing trigger while aiming. Synty rigs keep it on a child.")]
    [SerializeField] private Animator playerAnimator;

    [Header("Weapon models (optional)")]
    [Tooltip("Melee weapon model (the staff) shown while not aiming. Optional.")]
    [SerializeField] private GameObject meleeWeaponModel;
    [Tooltip("Wand model shown in hand while aiming. Optional.")]
    [SerializeField] private GameObject wandModel;

    [Header("Projectile")]
    [Tooltip("Magic-missile projectile instantiated per shot. REQUIRED — the projectile carries the shot's damage, not just its looks.")]
    [SerializeField] private MagicMissileProjectile projectilePrefab;

    [Header("Feedback (optional)")]
    [Tooltip("Played when aiming starts (charge-up sound).")]
    [SerializeField] private MMF_Player drawFeedback;
    [Tooltip("Played on every shot.")]
    [SerializeField] private MMF_Player fireFeedback;

    [Header("Class mechanics (optional)")]
    [Tooltip("Optional: the Mage's imbuement buff, polled for the current element so the missile's looks match. Defaults to the one on this GameObject.")]
    [SerializeField] private MageImbuement imbuement;

    [Tooltip("Optional: the player's mount. There is no mounted casting — aiming from the saddle does nothing.")]
    [SerializeField] private PlayerMount mount;

    /// <summary>Fired once per enemy a missile damaged, with the world hit point — the <see cref="DamageTrigger.OnHit" /> shape, so imbuement/telemetry listeners treat wand and staff alike.</summary>
    public event Action<IDamageable, Damage, Vector3> OnHit;

    /// <summary>Fired once per shot (hit or miss), the moment the missile leaves the wand — for cosmetic listeners.</summary>
    public event Action OnFired;

    /// <summary>True once the "Wand" skill node has been bought; while false, aiming does nothing and melee works normally.</summary>
    public bool IsUnlocked => !anyError && stats.GetValue(StatType.WandUnlocked) >= 1f;

    /// <summary>True while the aim button is held and the shot is charging.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>Charge level of the wind-up in progress, 0..WandMaxChargeLevels (the PlayerAttack convention).</summary>
    public int ChargeLevel { get; private set; }

    /// <summary>Levels the current wind-up can reach.</summary>
    public int MaxChargeLevels => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.WandMaxChargeLevels));

    /// <summary>Fraction of the post-shot cooldown elapsed: 0 the instant a shot fires, 1 when ready (the PlayerBow convention).</summary>
    public float CooldownFraction
    {
        get
        {
            if (anyError || config.shotCooldownSeconds <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01((Time.time - lastShotTime) / config.shotCooldownSeconds);
        }
    }

    /// <summary>True while the wand is between shots and can't fire yet.</summary>
    public bool IsCoolingDown => CooldownFraction < 1f;

    // Aim-camera framing surfaced for BowAimCamera (see IChargedAimWeapon).
    public int RangedWeaponType => config != null ? config.rangedWeaponType : 2;
    public float AimCameraDistance => config != null ? config.aimCameraDistance : 2.75f;
    public float AimCameraHorizontalOffset => config != null ? config.aimCameraHorizontalOffset : 0.7f;
    public float AimFieldOfViewPercent => config != null ? config.aimFieldOfViewPercent : 1f;
    public float AimBlendSeconds => config != null ? config.aimBlendSeconds : 0.2f;

    private const int MaxCastHits = 64;

    // Kept only for the aim-direction raycast — the per-hit sweeping lives on MagicMissileProjectile.
    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];

    private IDamageable ownerDamageable;
    private IDamageable ignoredTarget;

    /// <summary>
    ///     A target wand missiles fly through in addition to the wielder — the horse under a mounted
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
    private float lastShotTime = Mathf.NegativeInfinity;
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
        if (imbuement == null)
        {
            imbuement = GetComponent<MageImbuement>();
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; the wand can't read aim/fire input.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("WandSO is not assigned in the inspector.");
            anyError = true;
        }
        if (playerAnimator == null)
        {
            Debug.LogError("Player Animator is not assigned or found; the wand can't suppress melee swings while aiming.");
            anyError = true;
        }
        if (projectilePrefab == null)
        {
            Debug.LogError("MagicMissileProjectile prefab is not assigned — the projectile carries the shot's damage, so shots would do nothing.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (castOrigin == null)
        {
            castOrigin = transform;
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

        // The aim animator states are optional wiring, shared with the bow/axe (the Mage's override
        // controller re-skins them) — without the params the wand still works, the rig just keeps
        // its melee pose (checked once so missing params don't spam warnings every aim).
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
            Debug.LogWarning("PlayerWand: the player Animator has no IsAiming (Bool) / BowFire (Trigger) parameters — aim/cast animations won't play until the aim layer is wired (see TODO.md).");
        }

        // The wand never damages its wielder (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();
        if (ownerDamageable == null && Player.Instance != null)
        {
            ownerDamageable = Player.Instance.Damageable;
        }

        // Register the authored SO values as the stat bases; skill nodes layer on top without ever
        // mutating the asset (the PlayerBow convention). The wand itself is gated: base 0 = locked
        // until the "Wand" node is bought (the BowUnlocked convention).
        stats.SetBase(StatType.WandUnlocked, 0f);
        stats.SetBase(StatType.WandDamage, config.baseDamage);
        stats.SetBase(StatType.WandMaxChargeLevels, config.baseMaxChargeLevels);
        stats.SetBase(StatType.WandChargeDamageBonus, config.baseChargeDamageBonus);
        stats.SetBase(StatType.WandKnockback, config.baseKnockback);

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
        // E.g. PlayerDeath disabling controls mid-aim: put the staff back in hand.
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
            // Wand still locked: leave IsAiming false so the melee swing (and everything else) works normally.
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
        if (wandModel != null)
        {
            wandModel.SetActive(true);
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
        }
        if (wandModel != null)
        {
            wandModel.SetActive(false);
        }
    }

    private void HandleAttackPressed()
    {
        if (anyError || !IsAiming)
        {
            return;
        }
        if (Time.time - lastShotTime < config.shotCooldownSeconds)
        {
            return;
        }

        lastShotTime = Time.time;
        Fire();

        // The wind-up restarts for the next shot while the aim is still held.
        ChargeLevel = 0;
        chargeStartTime = Time.time;
    }

    /// <summary>
    ///     One shot: launches a <see cref="MagicMissileProjectile" /> toward the crosshair carrying
    ///     everything decided at release time (charge, current element for looks). The projectile
    ///     does the damaging as it flies and calls back into <see cref="CreateHitDamage" />/
    ///     <see cref="ReportHit" /> for the enemy it stops in.
    /// </summary>
    private void Fire()
    {
        Vector3 origin = castOrigin.position;
        Vector3 direction = ResolveAimDirection(origin);

        if (fireFeedback != null)
        {
            fireFeedback.PlayFeedbacks();
        }
        if (hasAimAnimatorParams)
        {
            playerAnimator.SetTrigger(fireHash);
        }
        OnFired?.Invoke();

        MagicMissileProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        projectile.Launch(this, new MagicMissileProjectile.LaunchSpec
        {
            origin = origin,
            direction = direction,
            speed = config.projectileSpeed,
            maxRange = config.maxRange,
            radius = config.missileRadius,
            hitLayers = hitLayers,
            chargeLevel = ChargeLevel,
            owner = ownerDamageable,
            ignoredTarget = ignoredTarget,
            imbuement = imbuement != null && imbuement.isActiveAndEnabled ? imbuement : null,
        });
        projectile.SetElement(imbuement != null && imbuement.isActiveAndEnabled ? imbuement.CurrentElement : null);
    }

    /// <summary>
    ///     Shots fly toward whatever the camera centre is looking at (the PlayerBow hitscan
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
            return castOrigin.forward;
        }

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = ray.origin + ray.direction * config.maxRange;

        int count = Physics.RaycastNonAlloc(ray.origin, ray.direction, castBuffer, config.maxRange, hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, PlayerThrownAxe.HitDistanceComparer.Instance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = PlayerThrownAxe.ResolveDamageable(hit.collider);
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
    ///     Builds one missile's worth of damage — called by the in-flight
    ///     <see cref="MagicMissileProjectile" /> at impact so the crit rolls fresh per shot (the
    ///     <see cref="DamageTrigger" /> convention). Charge was fixed at release time and travels
    ///     with the missile; <paramref name="sourcePosition" /> is the missile's position just before
    ///     the hit, so knockback shoves along the flight line. <paramref name="target" /> feeds the
    ///     per-target Ice Breaker check (the staff gets the same bonus inside DamageTrigger).
    /// </summary>
    public Damage CreateHitDamage(int chargeLevel, Vector3 sourcePosition, IDamageable target)
    {
        if (anyError)
        {
            return new Damage { value = 0f, type = DamageType.elemental, sourcePosition = sourcePosition };
        }

        float value = stats.GetValue(StatType.WandDamage);
        value *= 1f + chargeLevel * stats.GetValue(StatType.WandChargeDamageBonus);

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

        // Ice Breaker: bonus damage to chilled enemies. The staff's DamageTrigger does this
        // per-target check itself; the wand mirrors it so the Mage's ice line pays off on both.
        float iceBreakerBonus = stats.GetValue(StatType.IceBreakerDamageBonus);
        if (iceBreakerBonus > 0f && target is Component targetComponent)
        {
            SlowStatus slow = targetComponent.GetComponentInParent<SlowStatus>();
            if (slow != null && slow.IsSlowed)
            {
                value *= 1f + iceBreakerBonus;
            }
        }

        IDamageable hitSource = ownerDamageable;
        if (hitSource == null && Player.Instance != null)
        {
            hitSource = Player.Instance.Damageable;
        }

        return new Damage
        {
            value = value,
            type = DamageType.elemental,
            isCritical = crit,
            sourcePosition = sourcePosition,
            knockbackForce = stats.GetValue(StatType.WandKnockback),
            // Player-owned hit: lets runestones tell a wand blast from enemy splash damage.
            source = hitSource,
            isProjectile = true,
        };
    }

    /// <summary>Raises <see cref="OnHit" /> for a hit the in-flight <see cref="MagicMissileProjectile" /> landed, so imbuement/telemetry listeners see missile hits exactly like staff hits.</summary>
    public void ReportHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        OnHit?.Invoke(target, damage, hitPoint);
    }
}
