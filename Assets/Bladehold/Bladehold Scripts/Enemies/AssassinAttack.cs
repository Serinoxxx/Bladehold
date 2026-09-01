using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Assassin's 3-phase attack cycle:
///     1. Approaches the target, pauses movement, and displays a red circular telegraph for <see cref="AssassinAttackSO.windupSeconds" />.
///     2. Unleashes a stationary whirlwind spin dealing multi-hit damage (<see cref="AssassinAttackSO.spinHits" /> ticks
///        over <see cref="AssassinAttackSO.spinDuration" /> seconds for <see cref="AssassinAttackSO.damagePerHit" /> per pop),
///        accompanied by a Synty whirlwind VFX and slash sound effects on each pulse.
///     3. Enters a stunned/dizzy state for <see cref="AssassinAttackSO.stunDuration" /> seconds with overhead star VFX.
///     4. Recovers and resumes pursuit after <see cref="AssassinAttackSO.attackCooldown" />.
/// </summary>
public class AssassinAttack : MonoBehaviour
{
    [Header("Core Dependencies")]
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private AssassinAttackSO attackData;
    [Tooltip("Optional target-selection layer; without it the assassin targets the player.")]
    [SerializeField] private AITargetSelector targetSelector;

    [Header("Telegraph & Visual Effects")]
    [Tooltip("Flat circular decal/quad revealed on the ground during wind-up (scaled to diameter).")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("Whirlwind particle VFX active during the spinning attack.")]
    [SerializeField] private GameObject whirlwindVfxPrefab;
    [Tooltip("Dizzy / stunned star VFX displayed above the head during stun duration.")]
    [SerializeField] private GameObject stunVfxPrefab;

    [Header("Audio & Feedbacks")]
    [SerializeField] private MMF_Player windupFeedback;
    [SerializeField] private MMF_Player slashFeedback;
    [SerializeField] private AudioClip slashAudioClip;
    [SerializeField] private AudioSource audioSource;

    private const int MaxOverlapResults = 128;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitThisPulse = new HashSet<IDamageable>();

    private int windupTriggerHash;
    private int stunTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;

    private GameObject activeTelegraph;
    private GameObject activeWhirlwindVfx;
    private GameObject activeStunVfx;

    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isAttacking = false;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override applied by <see cref="WaveSpawner" /> from Enemies.csv.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
        }
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("[AssassinAttack] Animator component is not assigned or found on GameObject.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[AssassinAttack] Health component is not assigned or found on GameObject.", this);
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("[AssassinAttack] AIMovement component is not assigned or found on GameObject.", this);
            anyError = true;
        }
        if (attackData == null)
        {
            Debug.LogError("[AssassinAttack] AssassinAttackSO is not assigned in the inspector.", this);
            anyError = true;
        }
        if (telegraphPrefab == null)
        {
            Debug.LogError("[AssassinAttack] Telegraph prefab is not assigned in the inspector.", this);
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        windupTriggerHash = !string.IsNullOrEmpty(attackData.windupTrigger) ? Animator.StringToHash(attackData.windupTrigger) : 0;
        stunTriggerHash = !string.IsNullOrEmpty(attackData.stunTrigger) ? Animator.StringToHash(attackData.stunTrigger) : 0;

        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("[AssassinAttack] Player.Instance is null; no target available.", this);
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        health.OnDied += HandleDied;

        if (playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }

        CleanupEffects();
    }

    private void HandleDied()
    {
        isDead = true;
        isAttacking = false;
        CleanupEffects();
        StopAllCoroutines();
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        isAttacking = false;
        CleanupEffects();
        StopAllCoroutines();
    }

    private void CleanupEffects()
    {
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
        if (activeWhirlwindVfx != null)
        {
            Destroy(activeWhirlwindVfx);
            activeWhirlwindVfx = null;
        }
        if (activeStunVfx != null)
        {
            Destroy(activeStunVfx);
            activeStunVfx = null;
        }
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || isAttacking) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        // Do not attack if there is no attackable target (e.g. flocking to an objective and waiting for player)
        if (targetSelector != null && targetSelector.TargetDamageable == null) return;

        if (IsTargetInRange())
        {
            StartAttack();
        }
    }

    private Vector3 TargetPosition()
    {
        return targetSelector != null ? targetSelector.TargetPosition : player.position;
    }

    private bool IsTargetInRange()
    {
        float sqrDistance = (TargetPosition() - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private void StartAttack()
    {
        isAttacking = true;
        movement.SetMovementPaused(true);

        if (windupFeedback != null)
        {
            windupFeedback.PlayFeedbacks();
        }

        if (windupTriggerHash != 0)
        {
            animator.SetTrigger(windupTriggerHash);
        }

        // Lock telegraph at current position on ground
        Vector3 groundPos = transform.position + Vector3.up * 0.05f;
        activeTelegraph = Instantiate(telegraphPrefab, groundPos, Quaternion.identity);
        Vector3 scale = activeTelegraph.transform.localScale;
        activeTelegraph.transform.localScale = new Vector3(attackData.spinRadius * 2f, scale.y, attackData.spinRadius * 2f);

        StartCoroutine(ExecuteAttackCycle());
    }

    private IEnumerator ExecuteAttackCycle()
    {
        // ----------------------------------------------------
        // Phase 1: Wind-up & Telegraph
        // ----------------------------------------------------
        yield return new WaitForSeconds(attackData.windupSeconds);

        if (isDead || playerDead)
        {
            isAttacking = false;
            movement.SetMovementPaused(false);
            yield break;
        }

        // ----------------------------------------------------
        // Phase 2: Whirlwind Spin (Multi-hit damage over 2 seconds)
        // ----------------------------------------------------
        if (whirlwindVfxPrefab != null)
        {
            activeWhirlwindVfx = Instantiate(whirlwindVfxPrefab, transform.position + Vector3.up * 0.2f, Quaternion.identity, transform);
        }

        int totalHits = Mathf.Max(1, attackData.spinHits);
        float tickInterval = attackData.spinDuration / totalHits;

        for (int i = 0; i < totalHits; i++)
        {
            if (isDead || playerDead) break;

            ApplySpinDamagePulse();

            if (slashFeedback != null)
            {
                slashFeedback.PlayFeedbacks();
            }

            PlaySlashAudio();

            // Rotate model while waiting for the next damage tick
            float elapsed = 0f;
            while (elapsed < tickInterval)
            {
                if (isDead || playerDead) break;

                float dt = Time.deltaTime;
                elapsed += dt;
                transform.Rotate(Vector3.up, attackData.spinDegreesPerSecond * dt, Space.World);
                yield return null;
            }
        }

        if (activeWhirlwindVfx != null)
        {
            Destroy(activeWhirlwindVfx);
            activeWhirlwindVfx = null;
        }

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        if (isDead || playerDead)
        {
            isAttacking = false;
            movement.SetMovementPaused(false);
            yield break;
        }

        // ----------------------------------------------------
        // Phase 3: Stunned / Dizzy (4 seconds)
        // ----------------------------------------------------
        if (stunTriggerHash != 0)
        {
            animator.SetTrigger(stunTriggerHash);
        }

        if (stunVfxPrefab != null)
        {
            activeStunVfx = Instantiate(stunVfxPrefab, transform.position + Vector3.up * 1.8f, Quaternion.identity, transform);
        }

        yield return new WaitForSeconds(attackData.stunDuration);

        if (activeStunVfx != null)
        {
            Destroy(activeStunVfx);
            activeStunVfx = null;
        }

        if (isDead || playerDead)
        {
            isAttacking = false;
            movement.SetMovementPaused(false);
            yield break;
        }

        // ----------------------------------------------------
        // Phase 4: Recovery & Cooldown
        // ----------------------------------------------------
        movement.SetMovementPaused(false);
        lastAttackTime = Time.time;
        isAttacking = false;
    }

    private void PlaySlashAudio()
    {
        if (slashAudioClip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(slashAudioClip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(slashAudioClip, transform.position);
        }
    }

    private void ApplySpinDamagePulse()
    {
        hitThisPulse.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackData.spinRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (!hitThisPulse.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damagePerHit,
                type = attackData.damageType,
                sourcePosition = transform.position,
                knockbackForce = attackData.knockbackForce,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
