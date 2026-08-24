using UnityEngine;

/// <summary>
///     Reactive feedback for impulse-stamped hits: subscribes to <see cref="DamageTrigger.OnHit" /> the
///     same way <see cref="SwordHitFeedback" /> does, firing a particle burst and a light pulse scaled by
///     <see cref="Damage.impulseForce" />. Fires on every impulse-stamped hit; knockdown-vs-fling is
///     decided enemy-side by <see cref="ImpulseReceiver" />. <see cref="damageTrigger" /> must be assigned
///     explicitly — the VampiricBlade precedent, since the player has other DamageTriggers.
/// </summary>
public class ImpulseHitFeedback : MonoBehaviour
{
    [SerializeField] private DamageTrigger damageTrigger;

    [Header("Particle burst")]
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private int minParticles = 3;
    [SerializeField] private int maxParticles = 30;
    [SerializeField] private float particleCleanupDelay = 3f;

    [Header("Light pulse")]
    [Tooltip("Point-light prefab with intensity 0 at rest.")]
    [SerializeField] private Light pulseLightPrefab;
    [SerializeField] private float peakIntensity = 8f;
    [SerializeField] private float pulseInSeconds = 0.05f;
    [SerializeField] private float pulseOutSeconds = 0.3f;
    [SerializeField] private float pulseRange = 8f;

    [Tooltip("Damage.impulseForce that maps to a full-scale pulse/burst; higher forces clamp to it.")]
    [SerializeField] private float forceForMaxPulse = 25f;

    private bool anyError = false;

    /// <summary>
    ///     Re-points at the active class's melee DamageTrigger. Called by
    ///     <see cref="PlayerClassController" /> in Awake, before Start subscribes.
    /// </summary>
    public void SetDamageTrigger(DamageTrigger trigger)
    {
        damageTrigger = trigger;
    }

    private void Start()
    {
        if (damageTrigger == null)
        {
            Debug.LogError("ImpulseHitFeedback: DamageTrigger is not assigned.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        damageTrigger.OnHit += HandleHit;
    }

    private void OnDestroy()
    {
        if (damageTrigger != null)
        {
            damageTrigger.OnHit -= HandleHit;
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (damage.knockbackForce <= 0f)
        {
            return;
        }

        float factor = Mathf.Clamp01(damage.knockbackForce / forceForMaxPulse);
        SpawnBurst(hitPoint, factor);
        SpawnPulse(hitPoint, factor);
    }

    private void SpawnBurst(Vector3 point, float factor)
    {
        if (burstPrefab == null)
        {
            return;
        }

        int particleCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, factor)), minParticles, maxParticles);
        ParticleSystem instance = ParticlePool.Get(burstPrefab, point, Quaternion.identity);
        if (instance != null)
        {
            instance.Emit(particleCount);
            ParticlePool.Release(burstPrefab, instance, particleCleanupDelay);
        }
    }

    private void SpawnPulse(Vector3 point, float factor)
    {
        if (pulseLightPrefab == null)
        {
            return;
        }

        Light light = Instantiate(pulseLightPrefab, point, Quaternion.identity);
        light.range = pulseRange;
        light.intensity = 0f;

        float peak = peakIntensity * factor;
        LeanTween.value(light.gameObject, 0f, peak, pulseInSeconds)
            .setOnUpdate(v => light.intensity = v)
            .setOnComplete(() =>
                LeanTween.value(light.gameObject, peak, 0f, pulseOutSeconds)
                    .setOnUpdate(v => light.intensity = v));

        Destroy(light.gameObject, pulseInSeconds + pulseOutSeconds + 0.1f);
    }
}
