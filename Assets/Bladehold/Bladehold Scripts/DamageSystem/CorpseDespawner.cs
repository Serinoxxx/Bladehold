using System;
using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Despawns this enemy's corpse a while after death: first freezes the Animator (once the death
///     clip has settled), then sinks the corpse into the ground and destroys it. A standard reactive
///     <see cref="Health.OnDied" /> listener — death is still signalled through <see cref="Health" />,
///     and every other listener has long since reacted by the time the corpse despawns; nothing may
///     key off the eventual destruction. Registers with <see cref="CorpseManager" /> (when present)
///     so the oldest corpses can be despawned early under heavy fighting; without a manager it simply
///     runs its own timer. All timings live on <see cref="CorpseConfigSO" />.
/// </summary>
public class CorpseDespawner : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private CorpseConfigSO config;
    [Tooltip("Optional: played when the corpse starts sinking (thud/dust).")]
    [SerializeField] private MMF_Player sinkFeedback;

    /// <summary>
    ///     Raised once, when the sink actually begins (timer or early cap-despawn). Listeners that
    ///     must settle the corpse first — e.g. <see cref="EnemyRagdoll" /> freezing still-simulating
    ///     bones so the sink can carry them down with the root — react to it here.
    /// </summary>
    public event Action OnDespawnStarted;

    private Coroutine lifetimeRoutine;
    private bool isDead = false;
    private bool isSinking = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("CorpseConfigSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Despawning reacts to death; Health never reaches back into this component.
        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;

        // Optional: the manager enforces the corpse cap. Without one, the timer below still runs.
        if (CorpseManager.Instance != null)
        {
            CorpseManager.Instance.Register(this);
        }

        lifetimeRoutine = StartCoroutine(CorpseLifetime());
    }

    /// <summary>
    ///     Skips the remaining lifetime and sinks the corpse now. Called by <see cref="CorpseManager" />
    ///     when the corpse cap is exceeded. Safe to call more than once.
    /// </summary>
    public void DespawnNow()
    {
        if (!isDead || isSinking)
        {
            return;
        }

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }
        StartCoroutine(SinkAndDestroy());
    }

    private IEnumerator CorpseLifetime()
    {
        yield return new WaitForSeconds(config.animatorDisableDelay);

        // The death clip has settled; the corpse no longer needs skeleton evaluation at all.
        animator.enabled = false;

        if (config.corpseLifetime <= 0f)
        {
            yield break; // Corpses stay until the cap (if any) claims them.
        }

        yield return new WaitForSeconds(Mathf.Max(0f, config.corpseLifetime - config.animatorDisableDelay));
        yield return SinkAndDestroy();
    }

    private IEnumerator SinkAndDestroy()
    {
        if (isSinking)
        {
            yield break;
        }
        isSinking = true;
        OnDespawnStarted?.Invoke();
        if (sinkFeedback != null)
        {
            sinkFeedback.PlayFeedbacks();
        }

        // An early despawn may arrive before the animator-disable delay has elapsed.
        animator.enabled = false;

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * config.sinkDepth;
        for (float elapsed = 0f; elapsed < config.sinkDuration; elapsed += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, elapsed / config.sinkDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
