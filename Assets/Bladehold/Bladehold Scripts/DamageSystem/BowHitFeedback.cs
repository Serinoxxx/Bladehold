using UnityEngine;

/// <summary>
///     Reactive impact feedback for the bow — the <see cref="SwordHitFeedback" /> sibling: subscribes
///     to <see cref="PlayerBow.OnArrowImpact" /> and plays a hit sound plus a blood-particle burst at
///     the hit point, with distinct variants for critical hits and <see cref="VulnerableSpot" />
///     (headshot) hits. Vulnerable outranks crit when both apply — the headshot sting is the
///     distinctive read; each variant falls back to the normal pool/prefab when unassigned. Blood
///     sprays back along the arrow's flight path (unlike the sword's omnidirectional splash), and its
///     burst size/speed scale with damage up to a cap, the SwordHitFeedback numbers.
/// </summary>
public class BowHitFeedback : MonoBehaviour
{
    [Tooltip("The PlayerBow whose impacts this reacts to. Auto-wired from this object or its parents.")]
    [SerializeField] private PlayerBow bow;
    [SerializeField] private AudioSource audioSource;

    [Header("Hit sounds")]
    [SerializeField] private AudioClip[] hitSounds;
    [Tooltip("Used instead of Hit Sounds on a critical hit, if any are assigned.")]
    [SerializeField] private AudioClip[] critHitSounds;
    [Tooltip("Used on a VulnerableSpot (headshot) hit, if any are assigned — outranks the crit pool when both apply.")]
    [SerializeField] private AudioClip[] vulnerableHitSounds;

    [Header("Volume Overrides")]
    [Range(0f, 3f)] [SerializeField] private float hitVolume = 1.4f;
    [Range(0f, 3f)] [SerializeField] private float critHitVolume = 1.6f;
    [Range(0f, 3f)] [SerializeField] private float vulnerableHitVolume = 1.8f;

    [Header("Blood particles")]
    [SerializeField] private ParticleSystem bloodParticlePrefab;
    [Tooltip("Used instead of Blood Particle Prefab on a critical hit, if assigned.")]
    [SerializeField] private ParticleSystem critBloodParticlePrefab;
    [Tooltip("Used on a VulnerableSpot (headshot) hit, if assigned — outranks the crit prefab when both apply.")]
    [SerializeField] private ParticleSystem vulnerableBloodParticlePrefab;
    [Tooltip("Particle burst size and speed both scale with damage up to this many points of damage, then cap.")]
    [SerializeField] private float damageForMaxParticles = 20f;
    [SerializeField] private int minParticles = 3;
    [SerializeField] private int maxParticles = 40;
    [SerializeField] private float minSpeedMultiplier = 0.5f;
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [SerializeField] private float particleCleanupDelay = 3f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponentInParent<PlayerBow>();
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned or found in parents; arrow impacts will play no feedback.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        bow.OnArrowImpact += HandleImpact;
    }

    private void OnDestroy()
    {
        if (bow != null)
        {
            bow.OnArrowImpact -= HandleImpact;
        }
    }

    private void HandleImpact(ArrowImpact impact)
    {
        PlayRandomClip(PickSounds(impact), PickVolume(impact));
        SpawnBlood(impact);
    }

    private AudioClip[] PickSounds(ArrowImpact impact)
    {
        if (impact.hitVulnerableSpot && vulnerableHitSounds != null && vulnerableHitSounds.Length > 0)
        {
            return vulnerableHitSounds;
        }
        if (impact.damage.isCritical && critHitSounds != null && critHitSounds.Length > 0)
        {
            return critHitSounds;
        }
        return hitSounds;
    }

    private float PickVolume(ArrowImpact impact)
    {
        if (impact.hitVulnerableSpot && vulnerableHitSounds != null && vulnerableHitSounds.Length > 0)
        {
            return vulnerableHitVolume;
        }
        if (impact.damage.isCritical && critHitSounds != null && critHitSounds.Length > 0)
        {
            return critHitVolume;
        }
        return hitVolume;
    }

    private ParticleSystem PickBloodPrefab(ArrowImpact impact)
    {
        if (impact.hitVulnerableSpot && vulnerableBloodParticlePrefab != null)
        {
            return vulnerableBloodParticlePrefab;
        }
        if (impact.damage.isCritical && critBloodParticlePrefab != null)
        {
            return critBloodParticlePrefab;
        }
        return bloodParticlePrefab;
    }

    private void PlayRandomClip(AudioClip[] clips, float volumeScale = 1.0f)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volumeScale);
    }

    private void SpawnBlood(ArrowImpact impact)
    {
        ParticleSystem prefab = PickBloodPrefab(impact);
        if (prefab == null) return;

        float damageFactor = damageForMaxParticles > 0f ? Mathf.Clamp01(impact.damage.value / damageForMaxParticles) : 1f;
        int particleCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, damageFactor)), minParticles, maxParticles);

        // Spray back the way the arrow came, so exit-wound-style bursts read as the shot's doing.
        ParticleSystem instance = ParticlePool.Get(prefab, impact.point, Quaternion.LookRotation(-impact.direction));
        if (instance != null)
        {
            ParticleSystem.MainModule main = instance.main;
            main.startSpeedMultiplier = Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, damageFactor);
            instance.Emit(particleCount);

            ParticlePool.Release(prefab, instance, particleCleanupDelay);
        }
    }
}
