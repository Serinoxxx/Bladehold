using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
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
    
    [Tooltip("Sound effect played on dodge initiation.")]
    [SerializeField] private AudioClip dodgeSfx;
    [Tooltip("Optional MMF_Player feedback triggered on dodge.")]
    [SerializeField] private MMF_Player dodgeFeedback;
    [Tooltip("Optional feedback for nimble strike sword swing.")]
    [SerializeField] private MMF_Player nimbleStrikeFeedback;
    [Tooltip("Optional sound effect played when Nimble Strike executes an attack during dodge.")]
    [SerializeField] private AudioClip nimbleStrikeSfx;
    [Tooltip("Optional sound effect played when Frost Step chill pulse triggers.")]
    [SerializeField] private AudioClip iceChillSfx;
    [SerializeField] private SwordHitFeedback swordHitFeedback;
    [SerializeField] private PlayerBow playerBow;

    public event Action<float, float> OnCooldownUpdated;
    public event Action OnAbilityReady;
    public event Action OnDodgeStarted;

    private float remainingCooldown;
    private float maxCooldown;
    private bool isCooldownActive;
    private bool isDodging;
    private float lastDodgeEndTime = -999f;
    private int attackTriggerHash;

    public bool IsDodging => isDodging;
    public float TimeSinceDodge => Time.time - lastDodgeEndTime;
    public bool IsLungeWindowActive => isDodging || (TimeSinceDodge <= 1.0f);

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

        InitAttackTriggerHash();
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
        if (player == null || player.Health.IsDead) return;

        if (isCooldownActive)
        {
            remainingCooldown -= Time.deltaTime;
            OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);

            if (remainingCooldown <= 0f)
            {
                isCooldownActive = false;
                OnAbilityReady?.Invoke();
            }
        }

        if (player.Stats.GetValue(StatType.DodgeUnlocked) <= 0f) return;

        bool dashPressed = false;
        if (Keyboard.current != null)
        {
            dashPressed = Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.rightCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (!isDodging && !isCooldownActive && dashPressed)
        {
            StartCoroutine(PerformDodge());
        }
    }

    private IEnumerator PerformDodge()
    {
        isDodging = true;
        OnDodgeStarted?.Invoke();

        if (dodgeFeedback != null)
        {
            dodgeFeedback.PlayFeedbacks(transform.position);
        }
        else if (dodgeSfx != null)
        {
            AudioSource.PlayClipAtPoint(dodgeSfx, transform.position, 1.0f);
        }

        GameObject activeVfx = null;
        GameObject prefabToUse = dashVfxPrefab;
        string elementId = RunSession.ElementalSlots.GetValueOrDefault("SLOT_MOBILITY", "");
        switch (elementId?.ToUpper())
        {
            case "FIRE": if (fireDashVfxPrefab != null) prefabToUse = fireDashVfxPrefab; break;
            case "ICE": if (iceDashVfxPrefab != null) prefabToUse = iceDashVfxPrefab; break;
            case "LIGHTNING": if (lightningDashVfxPrefab != null) prefabToUse = lightningDashVfxPrefab; break;
            case "POISON": if (poisonDashVfxPrefab != null) prefabToUse = poisonDashVfxPrefab; break;
        }

        if (prefabToUse != null)
        {
            activeVfx = Instantiate(prefabToUse, transform.position, transform.rotation, transform);
            Destroy(activeVfx, dashDuration + 1f);
        }

        maxCooldown = player.Stats.GetValue(StatType.DodgeCooldown);
        remainingCooldown = maxCooldown;
        isCooldownActive = true;
        OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);

        float distance = player.Stats.GetValue(StatType.DodgeDistance);
        
        // Find dash direction (use camera-relative movement input if active, otherwise transform.forward)
        Vector3 dashDir = transform.forward;
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
            transform.rotation = Quaternion.LookRotation(dashDir);
        }
        
        float damageMultiplier = player.Stats.GetValue(StatType.DodgeDamageMultiplier);
        float knockback = player.Stats.GetValue(StatType.DodgeKnockbackForce);
        float chainReduction = player.Stats.GetValue(StatType.DodgeChainCooldownReduction);
        float nimbleStrike = player.Stats.GetValue(StatType.SwordNimbleStrike);
        HashSet<Health> hitEnemies = new HashSet<Health>();

        if (nimbleStrike > 0f)
        {
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
        Vector3 lastTrailPos = transform.position;
        if (fireDPS > 0f)
        {
            SpawnFireTrailSegment(fireDPS);
        }

        float frostSlow = (player != null && player.Stats != null) ? player.Stats.GetValue(StatType.IceFrostStepSlowPercent) : 0f;
        if (frostSlow > 0f)
        {
            TriggerFrostStepPulse(transform.position, frostSlow);
        }

        while (timePassed < dashDuration)
        {
            if (player.Health.IsDead) break;

            float moveStep = (distance / dashDuration) * Time.deltaTime;
            characterController.Move(dashDir * moveStep);

            if (fireDPS > 0f && Vector3.Distance(lastTrailPos, transform.position) >= 0.6f)
            {
                SpawnFireTrailSegment(fireDPS);
                lastTrailPos = transform.position;
            }
            
            if (damageMultiplier > 0f || knockback > 0f || nimbleStrike > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
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
                            sourcePosition = transform.position,
                            source = player.Damageable,
                            isPlayerDamage = true,
                            elementId = RunSession.ElementalSlots.GetValueOrDefault("SLOT_MOBILITY", "")
                        });

                        if (enemyHealth.IsDead && chainReduction > 0f)
                        {
                            remainingCooldown -= chainReduction;
                            if (remainingCooldown < 0f) remainingCooldown = 0f;
                            OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);
                        }
                    }
                }
            }

            timePassed += Time.deltaTime;
            yield return null;
        }

        if (frostSlow > 0f && player != null && player.Health != null && !player.Health.IsDead)
        {
            TriggerFrostStepPulse(transform.position, frostSlow);
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
            nimbleStrikeFeedback.PlayFeedbacks(transform.position);
        }
        else if (nimbleStrikeSfx != null)
        {
            AudioSource.PlayClipAtPoint(nimbleStrikeSfx, transform.position, 1.0f);
        }
        else if (swordHitFeedback != null)
        {
            swordHitFeedback.PlayWoosh();
        }
    }

    private void SpawnFireTrailSegment(float fireDPS)
    {
        GameObject vfxToUse = fireDashVfxPrefab;
        if (vfxToUse == null && ElementalEffectsManager.Instance != null)
        {
            vfxToUse = ElementalEffectsManager.Instance.fireStatusVfx;
        }

        GameObject segmentObj = new GameObject("FireTrailSegment");
        segmentObj.transform.position = transform.position;
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

        GameObject vfxPrefab = iceDashVfxPrefab;
        if (vfxPrefab == null && ElementalEffectsManager.Instance != null)
        {
            vfxPrefab = ElementalEffectsManager.Instance.iceStatusVfx;
        }

        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, origin, Quaternion.identity);
            Destroy(vfx, 2.0f);
        }

        AudioClip sfx = iceChillSfx;
        if (sfx == null && ElementalEffectsManager.Instance != null)
        {
            sfx = ElementalEffectsManager.Instance.statusAppliedSfx != null
                ? ElementalEffectsManager.Instance.statusAppliedSfx
                : ElementalEffectsManager.Instance.frozenSfx;
        }

        if (sfx != null)
        {
            AudioSource.PlayClipAtPoint(sfx, origin, 1.0f);
        }
    }
}

