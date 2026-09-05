using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Implements the charged-attack mechanic on top of the vendored
///     <see cref="SamplePlayerAnimationController" />, which already owns attack input and animation.
///     Includes the "Earth Splitter" final charge mechanic that smashes the earth in a line of rock explosions.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("Synty InputReader that raises the attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional: the class controller, polled for the active class's aim weapon. While that weapon is aiming, attack presses fire it instead of swinging, so the melee hold-to-charge is skipped.")]
    [SerializeField] private PlayerClassController classController;
    [Tooltip("Optional: the player animation controller, checked for attack cooldown to prevent resetting charge timing mid-swing.")]
    [SerializeField] private SamplePlayerAnimationController animController;

    [Tooltip("Seconds of holding the attack button to gain each charge level (level 1 at 1×, level 2 at 2×, ...).")]
    [SerializeField] private float chargeTimePerLevel = 0.33f;

    [Header("Earth Splitter")]
    [Tooltip("Red box telegraph prefab shown in front of the player when Earth Splitter is at full charge.")]
    [SerializeField] private GameObject earthSplitterTelegraphPrefab;
    [Tooltip("Synty rock explosion particle effect spawned along the line on Earth Splitter release.")]
    [SerializeField] private GameObject rockExplosionVfxPrefab;
    [Tooltip("Smash sound effect played on Earth Splitter ground impact.")]
    [SerializeField] private AudioClip earthSplitterSfx;
    [Tooltip("Optional MMF_Player feedback (screenshake) on Earth Splitter release.")]
    [SerializeField] private MMF_Player earthSplitterFeedback;
    [SerializeField] private float earthSplitterLineLength = 8f;
    [SerializeField] private float earthSplitterLineWidth = 2.5f;
    [SerializeField] private float earthSplitterDamageMultiplier = 4.0f;
    [SerializeField] private float earthSplitterKnockback = 20.0f;

    private GameObject activeTelegraph;
    private bool isEarthSplitterReady;

    private bool charging;
    private float chargeStartTime;
    private bool subscribed;
    private bool anyError = false;

    /// <summary>Charge level of the swing in progress (or the last one), 0..MaxChargeLevels. Useful for VFX/feedback.</summary>
    public int ChargeLevel { get; private set; }

    /// <summary>Levels the current hold can reach; 1 by default, upgraded by Heavy Strike nodes.</summary>
    public int MaxChargeLevels => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.MaxChargeLevels));

    /// <summary>True while the attack button is held and the swing is charging up.</summary>
    public bool IsCharging => charging;

    /// <summary>
    ///     Damage multiplier for the current swing. Scales continuously from 0.1x (uncharged) to 2.0x (base level 1 charge).
    /// </summary>
    public float AttackDamageMultiplier { get; private set; } = 1f;

    /// <summary>
    ///     The damage multiplier for a fully charged attack at maximum charge levels,
    ///     matching the value reached at the end of a full hold.
    /// </summary>
    public float FullyChargedDamageMultiplier
    {
        get
        {
            int maxLevels = MaxChargeLevels;
            float damagePerLevel = 1.9f + (stats != null ? stats.GetValue(StatType.ChargeDamageBonus) : 0f);
            return 0.1f + damagePerLevel * maxLevels;
        }
    }

    /// <summary>Time in seconds required per charge level.</summary>
    public float ChargeTimePerLevel => chargeTimePerLevel;

    /// <summary>Total time in seconds required to reach maximum charge levels.</summary>
    public float MaxChargeTime => MaxChargeLevels * chargeTimePerLevel;

    /// <summary>Elapsed time in seconds of the current attack charge, clamped to [0, MaxChargeTime].</summary>
    public float CurrentChargeTime => charging ? Mathf.Min(Time.time - chargeStartTime, MaxChargeTime) : 0f;

    /// <summary>Normalized charge progress [0..1] of the current hold.</summary>
    public float ChargeProgress => MaxChargeTime > 0f ? Mathf.Clamp01(CurrentChargeTime / MaxChargeTime) : 0f;

    /// <summary>
    ///     Per-class charge pacing (heavier weapons charge slower). Called by
    ///     <see cref="PlayerClassController" /> in Awake; the serialized value is the Swordsman default.
    /// </summary>
    public void SetChargeTimePerLevel(float seconds)
    {
        chargeTimePerLevel = seconds;
    }

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
        if (classController == null)
        {
            classController = GetComponentInParent<PlayerClassController>();
        }
        if (animController == null)
        {
            animController = GetComponent<SamplePlayerAnimationController>();
            if (animController == null)
            {
                animController = GetComponentInChildren<SamplePlayerAnimationController>();
            }
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; charged attack can't time the hold.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Heavy Strike charge is active by default at base max charge level 1.
        stats.SetBase(StatType.ChargeDamageBonus, 0f);
        stats.SetBase(StatType.MaxChargeLevels, 1f);
        stats.SetBase(StatType.EarthSplitterUnlocked, 0f);

        Subscribe();
    }

    private void OnEnable()
    {
        // Re-subscribe if this component is toggled (e.g. re-enabled after a non-death disable).
        if (!anyError && inputReader != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        charging = false;
        HideTelegraph();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
        }
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated += HandlePressed;
        inputReader.onAttackDeactivated += HandleReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandlePressed;
        inputReader.onAttackDeactivated -= HandleReleased;
        subscribed = false;
    }

    public void ResetCharge()
    {
        charging = false;
        ChargeLevel = 0;
        AttackDamageMultiplier = 1f;
        HideTelegraph();
    }

    private void Update()
    {
        if (anyError) return;

        if (charging)
        {
            // Auto-recover if the attack button was released during pause/UI or if input got consumed
            if (inputReader != null && !inputReader.IsAttackPressed)
            {
                HandleReleased();
                return;
            }

            // Keep the multiplier live as the hold grows
            RecomputeMultiplier();

            // Earth Splitter telegraph handling
            if (stats.GetValue(StatType.EarthSplitterUnlocked) > 0f)
            {
                bool fullyCharged = ChargeProgress >= 0.99f;
                if (fullyCharged && !isEarthSplitterReady)
                {
                    isEarthSplitterReady = true;
                    if (earthSplitterTelegraphPrefab != null && activeTelegraph == null)
                    {
                        activeTelegraph = Instantiate(earthSplitterTelegraphPrefab);
                        foreach (var ps in activeTelegraph.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = ps.main;
                            main.simulationSpace = ParticleSystemSimulationSpace.Local;
                            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                            var rend = ps.GetComponent<ParticleSystemRenderer>();
                            if (rend != null && rend.renderMode == ParticleSystemRenderMode.Billboard)
                            {
                                rend.alignment = ParticleSystemRenderSpace.Local;
                            }
                        }
                    }
                }

                if (activeTelegraph != null)
                {
                    activeTelegraph.SetActive(isEarthSplitterReady);
                    if (isEarthSplitterReady)
                    {
                        Vector3 center = transform.position + transform.forward * (earthSplitterLineLength * 0.5f);
                        activeTelegraph.transform.position = center + Vector3.up * 0.05f;
                        activeTelegraph.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                        activeTelegraph.transform.localScale = new Vector3(earthSplitterLineWidth, 1f, earthSplitterLineLength);
                    }
                }
            }
        }
        else
        {
            HideTelegraph();
        }
    }

    private void HandlePressed()
    {
        if (anyError) return;

        // While whirlwind is active on the equipped melee weapon, melee hold-to-charge is skipped
        if (classController != null && classController.ActiveMeleeTrigger != null && classController.ActiveMeleeTrigger.IsWhirlwindActive) return;

        // While the active class's aim weapon (bow/axe/wand) is drawn, this press fires it instead
        IChargedAimWeapon aimWeapon = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance.ActiveAimWeapon : (classController != null ? classController.ActiveAimWeapon : null);
        if (aimWeapon != null && aimWeapon.IsAiming) return;

        // Ignore presses while melee attack is on cooldown (prevents interrupting a swing in progress).
        if (animController != null && animController.IsAttackOnCooldown) return;

        ChargeLevel = 0;
        AttackDamageMultiplier = 0.1f;
        HideTelegraph();

        if (MaxChargeLevels <= 0) return;

        charging = true;
        chargeStartTime = Time.time;
    }

    private void HandleReleased()
    {
        if (anyError || !charging) return;

        // Latch the final value for the strike that plays on release.
        RecomputeMultiplier();
        charging = false;

        if (isEarthSplitterReady && stats.GetValue(StatType.EarthSplitterUnlocked) > 0f)
        {
            ExecuteEarthSplitter();
        }

        HideTelegraph();
    }

    private void HideTelegraph()
    {
        isEarthSplitterReady = false;
        if (activeTelegraph != null)
        {
            activeTelegraph.SetActive(false);
        }
    }

    private void ExecuteEarthSplitter()
    {
        if (earthSplitterFeedback != null)
        {
            earthSplitterFeedback.PlayFeedbacks(transform.position);
        }
        else if (earthSplitterSfx != null)
        {
            AudioSource.PlayClipAtPoint(earthSplitterSfx, transform.position, 1.0f);
        }

        Vector3 forward = transform.forward;
        Vector3 origin = transform.position;

        // Spawn rock explosions sequentially along the line in front of the player
        int explosionCount = 4;
        float step = earthSplitterLineLength / explosionCount;
        for (int i = 1; i <= explosionCount; i++)
        {
            Vector3 targetPos = origin + forward * (i * step);
            if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, LayerMask.GetMask("Default", "Environment")))
            {
                targetPos = hit.point;
            }

            if (rockExplosionVfxPrefab != null)
            {
                GameObject vfx = Instantiate(rockExplosionVfxPrefab, targetPos, Quaternion.identity);
                Destroy(vfx, 3f);
            }
        }

        // Deal devastating line AOE damage
        Vector3 boxCenter = origin + forward * (earthSplitterLineLength * 0.5f) + Vector3.up * 0.75f;
        Vector3 halfExtents = new Vector3(earthSplitterLineWidth * 0.5f, 1.5f, earthSplitterLineLength * 0.5f);
        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, transform.rotation);

        float baseDmg = stats.GetValue(StatType.SwordDamage);
        float allDmg = stats.GetValue(StatType.AllDamageMultiplier);
        float finalDmg = baseDmg * earthSplitterDamageMultiplier * (allDmg > 0f ? allDmg : 1f);

        HashSet<Health> damagedEnemies = new HashSet<Health>();
        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && h != GetComponent<Health>() && damagedEnemies.Add(h))
            {
                Vector3 knockDir = (h.transform.position - origin).normalized + Vector3.up * 0.5f;
                Damage damage = new Damage
                {
                    value = finalDmg,
                    type = DamageType.blunt,
                    isCritical = true,
                    sourcePosition = origin,
                    direction = knockDir.normalized,
                    knockbackForce = earthSplitterKnockback,
                    unparryable = true,
                    isPlayerDamage = true
                };
                h.ReceiveDamage(damage);
            }
        }
    }

    private void RecomputeMultiplier()
    {
        int maxLevels = MaxChargeLevels;
        float elapsed = Mathf.Max(0f, Time.time - chargeStartTime);
        float chargeRatio = chargeTimePerLevel > 0f ? elapsed / chargeTimePerLevel : maxLevels;
        chargeRatio = Mathf.Clamp(chargeRatio, 0f, maxLevels);
        ChargeLevel = Mathf.Clamp(Mathf.FloorToInt(chargeRatio), 0, maxLevels);

        float damagePerLevel = 1.9f + stats.GetValue(StatType.ChargeDamageBonus);
        AttackDamageMultiplier = 0.1f + damagePerLevel * chargeRatio;
    }
}
