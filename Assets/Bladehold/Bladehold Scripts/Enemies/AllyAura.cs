using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Forest Witch's support aura: every <see cref="AllyAuraSO.tickInterval" /> seconds, heals
///     every living <see cref="Enemy" /> within <see cref="AllyAuraSO.radius" /> by
///     <see cref="AllyAuraSO.healPerTick" /> via <see cref="Health.Heal" /> — heal-only v1, per the
///     enemy plan. Never heals the witch herself, so she stays the priority kill. Stops ticking on
///     her own death and the player's (the fight's over — nothing left worth topping up).
/// </summary>
public class AllyAura : MonoBehaviour
{
    [SerializeField] private AllyAuraSO data;
    [SerializeField] private Health health;
    [Tooltip("Optional feedback played on any tick that actually healed an ally (a green pulse).")]
    [SerializeField] private MMF_Player healFeedback;

    private const int MaxOverlapResults = 64;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<Health> healedThisTick = new HashSet<Health>();

    private Health playerHealth;
    private float lastTickTime;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("AllyAuraSO is not assigned in the inspector.");
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

        health.OnDied += HandleDied;

        Player playerInstance = Player.Instance;
        if (playerInstance != null && playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // De-phase multiple witches so their heal pulses don't land in lockstep.
        lastTickTime = Time.time - Random.value * data.tickInterval;
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
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastTickTime < data.tickInterval) return;
        lastTickTime = Time.time;

        TickAura();
    }

    private void TickAura()
    {
        healedThisTick.Clear();
        bool healedAnyone = false;

        int count = Physics.OverlapSphereNonAlloc(transform.position, data.radius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Enemy ally = overlapBuffer[i].GetComponentInParent<Enemy>();
            if (ally == null)
            {
                continue;
            }

            Health allyHealth = ally.GetComponent<Health>();
            if (allyHealth == null || allyHealth == health || allyHealth.IsDead)
            {
                continue;
            }
            if (!healedThisTick.Add(allyHealth))
            {
                continue;
            }

            allyHealth.Heal(data.healPerTick);
            healedAnyone = true;
        }

        if (healedAnyone && healFeedback != null)
        {
            healFeedback.PlayFeedbacks();
        }
    }
}
