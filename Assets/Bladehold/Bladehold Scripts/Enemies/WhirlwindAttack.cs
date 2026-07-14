using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Barbarian Giant's whirlwind: a permanent spinning wall of steel while he advances. Every
///     <see cref="WhirlwindAttackSO.pulseInterval" /> seconds it damages everything within
///     <see cref="WhirlwindAttackSO.radius" /> (<c>unparryable</c>, wide-AoE convention, never
///     himself), and every physics tick it <see cref="IPlayerProjectile.Shatter" />s any player
///     projectile (thrown axes, magic missiles) inside the radius — iterating a copy of
///     <see cref="PlayerProjectileRegistry.Live" />, since shattering unregisters. The bow is
///     hitscan and passes straight through; this component deliberately carries <b>no collider</b>,
///     so it can never eat bow shots by accident. Stops on his own death and the player's.
/// </summary>
public class WhirlwindAttack : MonoBehaviour
{
    [SerializeField] private WhirlwindAttackSO attackData;
    [SerializeField] private Health health;
    [Tooltip("Optional looping feedback while the whirlwind is live (whoosh loop + dust).")]
    [SerializeField] private MMF_Player spinFeedback;

    private const int MaxOverlapResults = 64;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitThisPulse = new HashSet<IDamageable>();
    private readonly List<IPlayerProjectile> projectileBuffer = new List<IPlayerProjectile>();

    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Health playerHealth;
    private float lastPulseTime;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="WhirlwindAttackSO" />
    ///     is never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (attackData == null)
        {
            Debug.LogError("WhirlwindAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        ownerDamageable = GetComponentInParent<IDamageable>();

        health.OnDied += HandleDied;

        Player playerInstance = Player.Instance;
        if (playerInstance != null && playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        lastPulseTime = Time.time - Random.value * attackData.pulseInterval;

        if (spinFeedback != null)
        {
            spinFeedback.PlayFeedbacks();
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
        if (spinFeedback != null)
        {
            spinFeedback.StopFeedbacks();
        }
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        if (spinFeedback != null)
        {
            spinFeedback.StopFeedbacks();
        }
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastPulseTime < attackData.pulseInterval) return;
        lastPulseTime = Time.time;

        Pulse();
    }

    private void FixedUpdate()
    {
        if (anyError || isDead || playerDead) return;

        ShatterProjectiles();
    }

    /// <summary>Eats every player projectile inside the radius — iterate a copy, Shatter unregisters.</summary>
    private void ShatterProjectiles()
    {
        if (PlayerProjectileRegistry.Live.Count == 0)
        {
            return;
        }

        projectileBuffer.Clear();
        projectileBuffer.AddRange(PlayerProjectileRegistry.Live);

        float sqrRadius = attackData.radius * attackData.radius;
        foreach (IPlayerProjectile projectile in projectileBuffer)
        {
            if ((projectile.Position - transform.position).sqrMagnitude <= sqrRadius)
            {
                projectile.Shatter();
            }
        }
    }

    private void Pulse()
    {
        hitThisPulse.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackData.radius, overlapBuffer);
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
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = transform.position,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
