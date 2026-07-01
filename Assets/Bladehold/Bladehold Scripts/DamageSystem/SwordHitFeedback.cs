using UnityEngine;

/// <summary>
///     Reactive feedback for the sword: subscribes to <see cref="DamageTrigger.OnHit" />/
///     <see cref="DamageTrigger.OnBlocked" /> the same way <see cref="DamageNumberSpawner" /> reacts to
///     <see cref="Health.OnDamaged" /> - DamageTrigger stays unaware of what plays when it hits or blocks.
///     Every field below is optional and degrades gracefully if unassigned; only <see cref="damageTrigger" />
///     is required.
/// </summary>
public class SwordHitFeedback : MonoBehaviour
{
    [SerializeField] private DamageTrigger damageTrigger;
    [SerializeField] private AudioSource audioSource;

    [Header("Blocked reaction")]
    [Tooltip("The player rig's Animator. Set when a swing is blocked by the cut-through cap.")]
    [SerializeField] private Animator animator;

    [Header("Hit sounds")]
    [SerializeField] private AudioClip[] hitSounds;
    [Tooltip("Used instead of Hit Sounds on a critical hit, if any are assigned.")]
    [SerializeField] private AudioClip[] critHitSounds;

    [Header("Swing sound")]
    [SerializeField] private AudioClip[] wooshSounds;

    [Header("Blood particles")]
    [SerializeField] private ParticleSystem bloodParticlePrefab;
    [Tooltip("Used instead of Blood Particle Prefab on a critical hit, if assigned.")]
    [SerializeField] private ParticleSystem critBloodParticlePrefab;
    [Tooltip("Particle burst size and speed both scale with damage up to this many points of damage, then cap.")]
    [SerializeField] private float damageForMaxParticles = 20f;
    [SerializeField] private int minParticles = 3;
    [SerializeField] private int maxParticles = 40;
    [SerializeField] private float minSpeedMultiplier = 0.5f;
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [SerializeField] private float particleCleanupDelay = 3f;

    private int blockedTriggerHash;
    private bool anyError = false;

    private void OnValidate()
    {
        if (damageTrigger == null)
        {
            damageTrigger = GetComponent<DamageTrigger>();
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (damageTrigger == null)
        {
            Debug.LogError("DamageTrigger is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        blockedTriggerHash = Animator.StringToHash("Blocked");

        damageTrigger.OnHit += HandleHit;
        damageTrigger.OnBlocked += HandleBlocked;
    }

    private void OnDestroy()
    {
        if (damageTrigger != null)
        {
            damageTrigger.OnHit -= HandleHit;
            damageTrigger.OnBlocked -= HandleBlocked;
        }
    }

    /// <summary>Called from an animation event earlier in the swing, before the hitbox activates.</summary>
    public void PlayWoosh()
    {
        PlayRandomClip(wooshSounds);
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 point)
    {
        PlayRandomClip(damage.isCritical && critHitSounds.Length > 0 ? critHitSounds : hitSounds);
        SpawnBlood(point, damage.value, damage.isCritical);
    }

    private void HandleBlocked()
    {
        if (animator != null)
        {
            animator.SetTrigger(blockedTriggerHash);
        }
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }

    private void SpawnBlood(Vector3 point, float damageValue, bool isCritical)
    {
        ParticleSystem prefab = isCritical && critBloodParticlePrefab != null ? critBloodParticlePrefab : bloodParticlePrefab;
        if (prefab == null) return;

        float damageFactor = damageForMaxParticles > 0f ? Mathf.Clamp01(damageValue / damageForMaxParticles) : 1f;
        int particleCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, damageFactor)), minParticles, maxParticles);

        ParticleSystem instance = Instantiate(prefab, point, Quaternion.identity);
        ParticleSystem.MainModule main = instance.main;
        main.startSpeedMultiplier = Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, damageFactor);
        instance.Emit(particleCount);

        Destroy(instance.gameObject, particleCleanupDelay);
    }
}
