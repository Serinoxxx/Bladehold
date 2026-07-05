using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Lets the Storm Witch periodically summon a <see cref="LightningStormZone" /> hazard at the
///     player's current position. When the player is within <see cref="LightningStormAttackSO.castRange" />
///     and the ability is off cooldown, the storm is cast instantly (no telegraph/wind-up) — it becomes
///     the player's problem to step out of it. Follows the same validate/cooldown skeleton as
///     <see cref="AIAttack" />. Never touches the <see cref="UnityEngine.AI.NavMeshAgent" />.
/// </summary>
public class LightningStormAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private LightningStormAttackSO attackData;
    [SerializeField] private LightningStormZone stormZonePrefab;
    [SerializeField] private MMF_Player castFeedback;

    // Animator trigger played (cosmetic only — the storm spawns immediately, not on an animation event).
    [SerializeField] private string castTrigger = "Storm";

    private int castTriggerHash;
    private float? damageOverride;
    private Transform player;
    private IDamageable ownerDamageable;
    private Health playerHealth;
    private float lastCastTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="LightningStormAttackSO" />
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
        if (attackData == null)
        {
            Debug.LogError("LightningStormAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (stormZonePrefab == null)
        {
            Debug.LogError("LightningStormZone prefab is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        castTriggerHash = Animator.StringToHash(castTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the witch has no one to attack.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        // Stop attacking once this witch dies.
        health.OnDied += HandleDied;

        // Stop attacking once the player dies.
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
        // Corpses have nothing left to tick.
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastCastTime < attackData.castCooldown) return;

        if (IsPlayerInRange())
        {
            CastStorm();
        }
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return false;

        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.castRange * attackData.castRange;
    }

    private void CastStorm()
    {
        lastCastTime = Time.time;

        if (castFeedback != null)
        {
            castFeedback.PlayFeedbacks();
        }
        if (animator != null)
        {
            animator.SetTrigger(castTriggerHash);
        }

        LightningStormZone zone = Instantiate(stormZonePrefab, player.position, Quaternion.identity);
        zone.Initialize(
            attackData.stormRadius,
            attackData.stormDuration,
            attackData.strikeInterval,
            damageOverride ?? attackData.strikeDamage,
            attackData.damageType,
            ownerDamageable);
    }
}
