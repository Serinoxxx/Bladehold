using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Tunable ScriptableObject for dash cooldown, max charges, distance, and buffer timings.")]
    [SerializeField] private PlayerDodgeSO config;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera facingCamera;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Juice & Effects")]
    [Tooltip("Particle VFX prefab spawned during the dodge dash.")]
    [SerializeField] private GameObject dashVfxPrefab;
    [SerializeField] private GameObject fireDashVfxPrefab;
    [SerializeField] private GameObject iceDashVfxPrefab;
    [SerializeField] private GameObject lightningDashVfxPrefab;
    [SerializeField] private GameObject poisonDashVfxPrefab;
    [Tooltip("Blazing Trail: looping fire VFX parented to each stationary ground segment the dash leaves behind. Must emit over time (a rate-over-distance trail such as FX_Trail_Fire_01 shows nothing once it stops moving). Empty = ElementalEffectsManager.fireStatusVfx.")]
    [SerializeField] private GameObject fireTrailSegmentVfxPrefab;
    
    [Tooltip("MMF_Player played at the player on dodge initiation (whoosh).")]
    [SerializeField] private MMF_Player dodgeFeedback;
    [Tooltip("Optional feedback for the Nimble Strike sword swing. Falls back to the sword's woosh.")]
    [SerializeField] private MMF_Player nimbleStrikeFeedback;
    [Tooltip("MMF_Player played at the player when the Frost Step chill pulse triggers (ice VFX + chill sound). Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player frostStepFeedback;
    [SerializeField] private SwordHitFeedback swordHitFeedback;
    [SerializeField] private PlayerBow playerBow;

    public event Action<float, float> OnCooldownUpdated;
    public event Action OnAbilityReady;
    public event Action OnDodgeStarted;
    public event Action<int, int> OnChargesChanged;

    private int currentCharges;
    private float chargeRechargeTimer;
    private float bufferedDashUntilTime;
    private bool isDodging;
    private bool anyError;
    private float lastDodgeEndTime = -999f;
    // Nimble Strike's swing animation is cosmetic: the dash-path sphere already deals its damage, so the
    // swing's hitbox animation event is swallowed once inside this window (else every enemy is hit twice,
    // the swing at whatever charge multiplier the last real attack left behind).
    private float nimbleSwingSuppressUntil = -999f;
    private int attackTriggerHash;
    private bool loggedMissingTrailVfx;
    private const float FireTrailSpacing = 0.6f;
    private float invulnerableUntilTime = -999f;
    private int dodgeAnimTriggerHash;
    private int isMountedHash;
    private bool subscribedIFrames;

#if UNITY_EDITOR
    private float lastCachedConfigCooldown = -1f;
    private int lastCachedConfigCharges = -1;
#endif

    public int CurrentCharges => currentCharges;
    public int MaxCharges => player != null && player.Stats != null 
        ? Mathf.Max(1, Mathf.RoundToInt(player.Stats.GetValue(StatType.DodgeMaxCharges))) 
        : (config != null ? config.baseMaxCharges : 2);
    public float MaxCooldown => player != null && player.Stats != null 
        ? Mathf.Max(0.05f, player.Stats.GetValue(StatType.DodgeCooldown)) 
        : (config != null ? config.baseCooldown : 1.2f);
    public float RemainingCooldown => chargeRechargeTimer;
    public bool IsCooldownActive => currentCharges < MaxCharges;
    public bool CanDodge => !isDodging && currentCharges > 0 && (player == null || !player.Health.IsDead) && (player == null || player.Stats.GetValue(StatType.DodgeUnlocked) > 0f);

    public bool IsDodging => isDodging;

    // This component sits on the static Player root; the CharacterController's object is the body that moves.
    // Every dash position (trail, hit sphere, VFX, Frost Step) must come from here, never transform.
    private Transform Body => characterController != null ? characterController.transform : transform;
    /// <summary>True during a dodge's i-frames (<see cref="StatType.DodgeIFrameDuration" /> from the dash start): all incoming damage is ignored.</summary>
    public bool IsInvulnerable => Time.time < invulnerableUntilTime;
    public float TimeSinceDodge => Time.time - lastDodgeEndTime;
    public bool IsLungeWindowActive => isDodging || (TimeSinceDodge <= 1.0f);

    /// <summary>
    ///     True (once) when a melee hitbox event belongs to Nimble Strike's cosmetic swing and should not
    ///     deal damage. Called by <see cref="AnimationEvents.OneHandedSwordAttack" />.
    /// </summary>
    public bool ConsumeNimbleSwingHit()
    {
        if (Time.time > nimbleSwingSuppressUntil)
        {
            return false;
        }
        nimbleSwingSuppressUntil = -999f;
        return true;
    }

    private void OnValidate()
    {
        if (player == null) player = GetComponent<Player>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        if (swordHitFeedback == null) swordHitFeedback = GetComponentInChildren<SwordHitFeedback>();
        if (playerBow == null && player != null) playerBow = player.GetComponentInChildren<PlayerBow>();
    }

    private void Start()
    {
        if (player == null) player = GetComponent<Player>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        if (facingCamera == null) facingCamera = Camera.main;
        if (swordHitFeedback == null) swordHitFeedback = GetComponentInChildren<SwordHitFeedback>();
        if (playerBow == null && player != null) playerBow = player.GetComponentInChildren<PlayerBow>();

        if (player == null || characterController == null)
        {
            Debug.LogError("[PlayerDodge] Essential dependencies missing on Player GameObject!");
            anyError = true;
            return;
        }

        // Missing feedbacks only cost looks and sound, so they don't set anyError.
        if (dodgeFeedback == null) Debug.LogError("[PlayerDodge] dodgeFeedback is not assigned.", this);
        if (frostStepFeedback == null) Debug.LogError("[PlayerDodge] frostStepFeedback is not assigned.", this);

        if (config != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.DodgeCooldown, config.baseCooldown);
            player.Stats.SetBase(StatType.DodgeMaxCharges, config.baseMaxCharges);
            player.Stats.SetBase(StatType.DodgeDistance, config.baseDistance);
            player.Stats.SetBase(StatType.DodgeIFrameDuration, config.iFrameDuration);
            dashDuration = config.dashDuration;
#if UNITY_EDITOR
            lastCachedConfigCooldown = config.baseCooldown;
            lastCachedConfigCharges = config.baseMaxCharges;
#endif
        }

        currentCharges = MaxCharges;
        chargeRechargeTimer = 0f;
        InitAttackTriggerHash();
        InitDodgeAnimTrigger();

        if (player.Health != null)
        {
            player.Health.TryBlockDamage += BlockDuringIFrames;
            subscribedIFrames = true;
        }
    }

    private void OnDestroy()
    {
        if (subscribedIFrames && player != null && player.Health != null)
        {
            player.Health.TryBlockDamage -= BlockDuringIFrames;
        }
    }

    /// <summary>Health.TryBlockDamage hook: a dodge's i-frames negate every hit (attacks, hazards, DoT ticks).</summary>
    private bool BlockDuringIFrames(Damage damage) => IsInvulnerable;

    /// <summary>Hashes the SO's dodge trigger if the animator has it. A missing parameter is logged once, not every dash.</summary>
    private void InitDodgeAnimTrigger()
    {
        dodgeAnimTriggerHash = 0;
        string triggerName = config != null ? config.dodgeAnimTrigger : "Dodge";
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;
        foreach (var param in animator.parameters)
        {
            if (param.name == "IsMounted" && param.type == AnimatorControllerParameterType.Bool) isMountedHash = param.nameHash;
        }
        foreach (var param in animator.parameters)
        {
            if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
            {
                dodgeAnimTriggerHash = param.nameHash;
                return;
            }
        }
        Debug.LogError($"[PlayerDodge] The player animator has no '{triggerName}' trigger, so dashes play no animation. Run Bladehold/Captains/Build Captain Mogra Assets (it adds the Dodge layer).", this);
    }

    private void InitAttackTriggerHash()
    {
        if (animator == null) return;
        foreach (var param in animator.parameters)
        {
            if (param.name == "StartAttack")
            {
                attackTriggerHash = param.nameHash;
                return;
            }
            if (param.name == "Attack")
            {
                attackTriggerHash = param.nameHash;
            }
        }
    }

    private void Update()
    {
        if (anyError || player == null || player.Health.IsDead) return;

#if UNITY_EDITOR
        if (config != null && player.Stats != null)
        {
            if (!Mathf.Approximately(config.baseCooldown, lastCachedConfigCooldown))
            {
                lastCachedConfigCooldown = config.baseCooldown;
                player.Stats.SetBase(StatType.DodgeCooldown, config.baseCooldown);
            }
            if (config.baseMaxCharges != lastCachedConfigCharges)
            {
                lastCachedConfigCharges = config.baseMaxCharges;
                player.Stats.SetBase(StatType.DodgeMaxCharges, config.baseMaxCharges);
            }
        }
#endif

        int maxCharges = MaxCharges;
        float maxCd = MaxCooldown;

        if (currentCharges > maxCharges)
        {
            currentCharges = maxCharges;
            OnChargesChanged?.Invoke(currentCharges, maxCharges);
        }

        if (currentCharges < maxCharges)
        {
            chargeRechargeTimer -= Time.deltaTime;
            if (chargeRechargeTimer <= 0f)
            {
                currentCharges++;
                OnChargesChanged?.Invoke(currentCharges, maxCharges);

                if (currentCharges < maxCharges)
                {
                    chargeRechargeTimer += maxCd;
                }
                else
                {
                    chargeRechargeTimer = 0f;
                    OnAbilityReady?.Invoke();
                }
            }
            OnCooldownUpdated?.Invoke(chargeRechargeTimer, maxCd);
        }
        else
        {
            chargeRechargeTimer = 0f;
        }

        if (player.Stats.GetValue(StatType.DodgeUnlocked) <= 0f) return;

        bool dashPressed = false;
        if (Keyboard.current != null)
        {
            dashPressed = Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.rightCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.spaceKey.wasPressedThisFrame;
        }
        if (Gamepad.current != null)
        {
            // Left Bumper opens the Ultimate Wheel (plan 21 phase 5), so it no longer dashes.
            dashPressed = dashPressed || Gamepad.current.buttonEast.wasPressedThisFrame;
        }
        // Space/B also cancel the Ultimate Wheel; a dash mustn't fire underneath it.
        if (UltimateWheelUI.IsOpen) dashPressed = false;

        float bufferDuration = config != null ? config.inputBufferDuration : 0.15f;
        if (dashPressed)
        {
            bufferedDashUntilTime = Time.time + bufferDuration;
        }

        bool wantsDash = dashPressed || (Time.time <= bufferedDashUntilTime);

        if (!isDodging && currentCharges > 0 && wantsDash)
        {
            bufferedDashUntilTime = 0f;
            StartCoroutine(PerformDodge());
        }
    }

    private IEnumerator PerformDodge()
    {
        isDodging = true;
        currentCharges--;
        int maxCharges = MaxCharges;
        float maxCd = MaxCooldown;
        OnChargesChanged?.Invoke(currentCharges, maxCharges);
        OnDodgeStarted?.Invoke();

        invulnerableUntilTime = Time.time + Mathf.Max(0f, player.Stats.GetValue(StatType.DodgeIFrameDuration));
        // Nimble Strike swings out of the dash, so it keeps its attack animation; a mounted dash keeps the riding pose.
        if (animator != null && dodgeAnimTriggerHash != 0 && player.Stats.GetValue(StatType.SwordNimbleStrike) <= 0f &&
            (isMountedHash == 0 || !animator.GetBool(isMountedHash)))
        {
            animator.SetTrigger(dodgeAnimTriggerHash);
        }

        if (dodgeFeedback != null)
        {
            dodgeFeedback.PlayFeedbacks(Body.position);
        }

        GameObject activeVfx = null;
        GameObject prefabToUse = dashVfxPrefab;
        string elementId = RunSession.GetActiveElement("SLOT_MOBILITY");
        switch (elementId?.ToUpper())
        {
            case "FIRE": if (fireDashVfxPrefab != null) prefabToUse = fireDashVfxPrefab; break;
            case "ICE": if (iceDashVfxPrefab != null) prefabToUse = iceDashVfxPrefab; break;
            case "LIGHTNING": if (lightningDashVfxPrefab != null) prefabToUse = lightningDashVfxPrefab; break;
            case "POISON": if (poisonDashVfxPrefab != null) prefabToUse = poisonDashVfxPrefab; break;
        }

        float effectiveDashDuration = config != null ? config.dashDuration : dashDuration;
        if (prefabToUse != null)
        {
            activeVfx = Instantiate(prefabToUse, Body.position, Body.rotation, Body);
            Destroy(activeVfx, effectiveDashDuration + 1f);
        }

        if (chargeRechargeTimer <= 0f)
        {
            chargeRechargeTimer = maxCd;
            OnCooldownUpdated?.Invoke(chargeRechargeTimer, maxCd);
        }

        float distance = player.Stats.GetValue(StatType.DodgeDistance);
        
        // Find dash direction (use camera-relative movement input if active, otherwise Body.forward)
        Vector3 dashDir = Body.forward;
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        Camera cam = facingCamera != null ? facingCamera : Camera.main;

        if (inputReader != null && inputReader._moveComposite.sqrMagnitude > 0.01f && cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector2 moveInput = inputReader._moveComposite;
            Vector3 calculatedDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;
            if (calculatedDir.sqrMagnitude > 0.001f)
            {
                dashDir = calculatedDir;
            }
        }

        if (dashDir.sqrMagnitude > 0.001f)
        {
            Body.rotation = Quaternion.LookRotation(dashDir);
        }
        
        float damageMultiplier = player.Stats.GetValue(StatType.DodgeDamageMultiplier);
        float knockback = player.Stats.GetValue(StatType.DodgeKnockbackForce);
        float chainReduction = player.Stats.GetValue(StatType.DodgeChainCooldownReduction);
        float nimbleStrike = player.Stats.GetValue(StatType.SwordNimbleStrike);
        HashSet<Health> hitEnemies = new HashSet<Health>();

        if (nimbleStrike > 0f)
        {
            nimbleSwingSuppressUntil = Time.time + effectiveDashDuration + 0.5f;
            TriggerNimbleAttack();
        }

        float bowAutoShot = player.Stats.GetValue(StatType.BowAutoShotOnDash);
        if (bowAutoShot > 0f)
        {
            PlayerBow bow = playerBow;
            if (bow == null && player != null)
            {
                bow = player.GetComponentInChildren<PlayerBow>();
                playerBow = bow;
            }

            if (bow != null && bow.isActiveAndEnabled)
            {
                bow.FireAutoShotAtNearest();
            }
        }

        float timePassed = 0f;
        float fireDPS = player.Stats.GetValue(StatType.FireBlazingTrailDPS);
        Vector3 lastTrailPos = Body.position;
        if (fireDPS > 0f)
        {
            SpawnFireTrailSegment(fireDPS, Body.position);
        }

        float frostSlow = (player != null && player.Stats != null) ? player.Stats.GetValue(StatType.IceFrostStepSlowPercent) : 0f;
        if (frostSlow > 0f)
        {
            TriggerFrostStepPulse(Body.position, frostSlow);
        }

        while (timePassed < effectiveDashDuration)
        {
            if (player.Health.IsDead) break;

            float moveStep = (distance / effectiveDashDuration) * Time.deltaTime;
            characterController.Move(dashDir * moveStep);

            // Walk the gap in fixed steps so a long frame (hitch, low fps) can't leave holes in the trail.
            while (fireDPS > 0f && Vector3.Distance(lastTrailPos, Body.position) >= FireTrailSpacing)
            {
                lastTrailPos = Vector3.MoveTowards(lastTrailPos, Body.position, FireTrailSpacing);
                SpawnFireTrailSegment(fireDPS, lastTrailPos);
            }
            
            if (damageMultiplier > 0f || knockback > 0f || nimbleStrike > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(Body.position, 1.5f);
                foreach (var hit in hits)
                {
                    Health enemyHealth = hit.GetComponentInParent<Health>();
                    if (enemyHealth != null && enemyHealth != player.Health && !enemyHealth.IsDead && hitEnemies.Add(enemyHealth))
                    {
                        float baseDamage = player.Stats.GetValue(StatType.SwordDamage);
                        if (baseDamage <= 0f) baseDamage = 10f;
                        float effectiveMultiplier = damageMultiplier;
                        if (nimbleStrike > 0f)
                        {
                            effectiveMultiplier = Mathf.Max(effectiveMultiplier, 1.0f);
                        }
                        float finalDamage = baseDamage * effectiveMultiplier * player.Stats.GetValue(StatType.AllDamageMultiplier);
                        DamageType dmgType = (nimbleStrike > 0f) ? DamageType.slash : DamageType.sharp;
                        enemyHealth.ReceiveDamage(new Damage { 
                            value = finalDamage, 
                            type = dmgType,
                            isCritical = false, 
                            knockbackForce = knockback,
                            sourcePosition = Body.position,
                            source = player.Damageable,
                            isPlayerDamage = true,
                            elementId = RunSession.GetActiveElement("SLOT_MOBILITY")
                        });

                        if (enemyHealth.IsDead && chainReduction > 0f)
                        {
                            chargeRechargeTimer -= chainReduction;
                            if (chargeRechargeTimer <= 0f && currentCharges < maxCharges)
                            {
                                currentCharges++;
                                OnChargesChanged?.Invoke(currentCharges, maxCharges);
                                if (currentCharges < maxCharges)
                                {
                                    chargeRechargeTimer += maxCd;
                                }
                                else
                                {
                                    chargeRechargeTimer = 0f;
                                    OnAbilityReady?.Invoke();
                                }
                            }
                            OnCooldownUpdated?.Invoke(chargeRechargeTimer, maxCd);
                        }
                    }
                }
            }

            timePassed += Time.deltaTime;
            yield return null;
        }

        if (frostSlow > 0f && player != null && player.Health != null && !player.Health.IsDead)
        {
            TriggerFrostStepPulse(Body.position, frostSlow);
        }

        isDodging = false;
        lastDodgeEndTime = Time.time;
    }

    private void TriggerNimbleAttack()
    {
        if (animator != null)
        {
            if (attackTriggerHash != 0)
            {
                animator.SetTrigger(attackTriggerHash);
            }
            else
            {
                animator.SetTrigger("StartAttack");
            }
        }

        if (nimbleStrikeFeedback != null)
        {
            nimbleStrikeFeedback.PlayFeedbacks(Body.position);
        }
        else if (swordHitFeedback != null)
        {
            swordHitFeedback.PlayWoosh();
        }
    }

    private void SpawnFireTrailSegment(float fireDPS, Vector3 position)
    {
        // Not fireDashVfxPrefab: that's a moving trail (emits by distance), so a parked segment copy is invisible.
        GameObject vfxToUse = fireTrailSegmentVfxPrefab;
        if (vfxToUse == null && ElementalEffectsManager.Instance != null)
        {
            vfxToUse = ElementalEffectsManager.Instance.fireStatusVfx;
        }
        if (vfxToUse == null && !loggedMissingTrailVfx)
        {
            loggedMissingTrailVfx = true;
            Debug.LogError("[PlayerDodge] Blazing Trail has no VFX: assign fireTrailSegmentVfxPrefab (or ElementalEffectsManager.fireStatusVfx).", this);
        }

        GameObject segmentObj = new GameObject("FireTrailSegment");
        segmentObj.transform.position = position;
        segmentObj.transform.rotation = Quaternion.identity;

        FireTrailSegment segment = segmentObj.AddComponent<FireTrailSegment>();
        segment.Init(fireDPS, 3.0f, vfxToUse);
    }

    private void TriggerFrostStepPulse(Vector3 origin, float frostSlow)
    {
        Collider[] hits = Physics.OverlapSphere(origin, 4.5f);
        HashSet<Health> processed = new HashSet<Health>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (player != null && (enemyHealth == player.Health || enemyHealth.gameObject == player.gameObject || enemyHealth.transform.root == player.transform.root)) continue;
            if (!processed.Add(enemyHealth)) continue;

            SlowStatus.GetOrAdd(enemyHealth)?.ApplySlow(frostSlow, 3.0f);
            EnemyStatusManager.GetOrAdd(enemyHealth)?.ApplyStatus("Ice", frostSlow);
        }

        if (frostStepFeedback != null)
        {
            frostStepFeedback.PlayFeedbacks(origin);
        }
    }
}

