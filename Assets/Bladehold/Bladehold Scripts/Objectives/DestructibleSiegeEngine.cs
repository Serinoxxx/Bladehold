using System;
using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Destructible siege engine (e.g. Catapult, Ballista, Trebuchet) spawned for survival mode objectives.
///     Integrates with <see cref="Health"/>, triggers <see cref="MMF_Player"/> hit & destruction feedbacks,
///     and plays customizable SFX and particle systems.
/// </summary>
[RequireComponent(typeof(Health))]
public class DestructibleSiegeEngine : MonoBehaviour
{
    [Header("Health & Hit Points")]
    [SerializeField] private float maxHealth = 300f;

    [Header("Feedbacks & Juiciness")]
    [Tooltip("MMF_Player played when the siege engine takes damage (e.g. scale punch, sound, sparks).")]
    [SerializeField] private MMF_Player hitFeedback;

    [Tooltip("MMF_Player played when the siege engine is destroyed (explosion sound + fire burst, on unscaled time).")]
    [SerializeField] private MMF_Player deathFeedback;

    [Tooltip("Delay in seconds before the GameObject is destroyed after death. Set to 0 to destroy immediately.")]
    [SerializeField] private float postDeathDespawnDelay = 0f;

    private Health health;
    private bool isDestroyed;

    public event Action<DestructibleSiegeEngine> OnDestroyed;
    public Health Health => health;
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
        }
    }

    private void Start()
    {
        if (hitFeedback == null) Debug.LogError("[DestructibleSiegeEngine] hitFeedback is not assigned.", this);
        if (deathFeedback == null) Debug.LogError("[DestructibleSiegeEngine] deathFeedback is not assigned.", this);

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (isDestroyed) return;

        if (hitFeedback != null)
        {
            hitFeedback.PlayFeedbacks();
        }
    }

    private void HandleDied()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (deathFeedback != null)
        {
            deathFeedback.PlayFeedbacks(transform.position);
        }

        // Disable colliders and renderers so the catapult immediately disappears
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        OnDestroyed?.Invoke(this);

        if (postDeathDespawnDelay <= 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(DespawnRoutine());
        }
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(postDeathDespawnDelay);
        Destroy(gameObject);
    }
}
