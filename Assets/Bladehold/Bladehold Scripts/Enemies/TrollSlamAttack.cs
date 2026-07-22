using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Troll's massive ground slam. When its target (the player, or a gate via an optional
///     <see cref="AITargetSelector" />) comes within <see cref="TrollSlamAttackSO.triggerRange" />
///     and the attack is off cooldown, the troll starts a long wind-up: the damage area is revealed
///     on the ground (a telegraph prefab scaled to the slam radius), and after a configurable
///     <see cref="TrollSlamAttackSO.telegraphSeconds" /> the slam lands — dealing massive damage to
///     <b>everything</b> in the area (the player, gates, and other enemies alike, never the troll
///     itself) and stamping <see cref="Damage.impulsePower" />/<see cref="Damage.impulseForce" />
///     onto every hit so victims with an <see cref="ImpulseReceiver" /> are ragdoll-flung through the
///     player's own impulse/resistance system.
///
///     Follows the <see cref="AIAttack" /> skeleton (validate in Start, cooldown in Update, coroutine
///     to the apex, stops on own/player death) but — unlike <see cref="AIAttack" /> — pauses
///     <see cref="AIMovement" /> for the wind-up and landing via <see cref="AIMovement.SetMovementPaused" />:
///     the telegraph is locked to the troll's position when the swing starts, so it can't be allowed
///     to chase out from under it. All tunables live on <see cref="TrollSlamAttackSO" />.
/// </summary>
public class TrollSlamAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private TrollSlamAttackSO attackData;
    [Tooltip("Optional target-selection layer; without it the troll only ever slams at the player.")]
    [SerializeField] private AITargetSelector targetSelector;
    [Tooltip("Flat decal/quad revealed on the ground over the damage area during the wind-up. Scaled on x/z to the slam's diameter.")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("Optional VFX instantiated at the slam centre on impact (assumed to clean itself up).")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private MMF_Player windupFeedback;
    [SerializeField] private MMF_Player slamFeedback;

    // Animator trigger that starts the slam wind-up animation. Wire a long wind-up state driven by this.
    [SerializeField] private string slamTrigger = "Slam";

    private const int MaxOverlapResults = 128;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int slamTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private GameObject activeTelegraph;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="TrollSlamAttackSO" />
    ///     is never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
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
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("AIMovement component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (attackData == null)
        {
            Debug.LogError("TrollSlamAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (telegraphPrefab == null)
        {
            Debug.LogError("Telegraph prefab is not assigned in the inspector; the slam area must be revealed before it lands.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        slamTriggerHash = Animator.StringToHash(slamTrigger);

        // The slam never damages the troll itself (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the troll has no one to attack.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        // Stop attacking once this troll dies.
        health.OnDied += HandleDied;

        // Stop attacking once the player dies (combat's over — time to celebrate).
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
    }

    private void HandleDied()
    {
        isDead = true;
        // Corpses have nothing left to tick. (Coroutines survive a disable; SlamAfterTelegraph
        // bails on isDead and cleans up the telegraph.)
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        if (IsTargetInRange())
        {
            StartSlam();
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

    private void StartSlam()
    {
        lastAttackTime = Time.time;

        // The telegraph is locked to this position — the troll must hold still until the slam
        // lands, or the model would visibly drift out from under it.
        movement.SetMovementPaused(true);

        if (windupFeedback != null)
        {
            windupFeedback.PlayFeedbacks();
        }
        animator.SetTrigger(slamTriggerHash);

        // The area is locked in when the wind-up starts — that's what makes the telegraph honest
        // and the slam dodgeable.
        Vector3 center = transform.position + transform.forward * attackData.forwardOffset;

        activeTelegraph = Instantiate(telegraphPrefab, center + Vector3.up * 0.05f, Quaternion.identity);
        Vector3 scale = activeTelegraph.transform.localScale;
        activeTelegraph.transform.localScale = new Vector3(attackData.slamRadius * 2f, scale.y, attackData.slamRadius * 2f);

        StartCoroutine(SlamAfterTelegraph(center));
    }

    private IEnumerator SlamAfterTelegraph(Vector3 center)
    {
        yield return new WaitForSeconds(attackData.telegraphSeconds);

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        // A troll killed mid-wind-up never lands the slam; a dead player means the run is over.
        if (isDead || playerDead)
        {
            movement.SetMovementPaused(false);
            yield break;
        }

        if (slamFeedback != null)
        {
            slamFeedback.PlayFeedbacks();
        }
        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, center, Quaternion.identity);
        }

        ApplySlamDamage(center);
        movement.SetMovementPaused(false);
    }

    /// <summary>
    ///     Damages every unique <see cref="IDamageable" /> in the area except the troll itself —
    ///     the player, gates, and other enemies alike. Victims with an <see cref="ImpulseReceiver" />
    ///     get flung by the stamped impulse; the player just takes the (massive) damage.
    /// </summary>
    private void ApplySlamDamage(Vector3 center)
    {
        hitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, attackData.slamRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = center,
                knockbackForce = attackData.knockbackForce,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
