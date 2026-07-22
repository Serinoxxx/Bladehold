using UnityEngine;

/// <summary>
///     Drops a <see cref="LightningOrb" /> when this enemy's <see cref="Health" /> dies. Unlike
///     <see cref="ImpulseGoblin" />'s orb drop (a chance-rolled variant of a regular goblin), the Storm
///     Witch is her own roster type, so the drop always happens — no marked-instance flag needed.
///     Listens to <see cref="Health.OnDied" />; Health stays unaware of loot. VFX/SFX are optional and
///     purely cosmetic: a missing prefab/clip never blocks the orb drop.
/// </summary>
public class LightningOrbDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Instantiated at this enemy's position on death.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private AudioClip deathSfx;

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
        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }
        if (deathSfx != null)
        {
            AudioSource.PlayClipAtPoint(deathSfx, transform.position);
        }

        if (Player.Instance != null)
        {
            Player.Instance.GetComponentInChildren<ChainLightningBuff>()?.CollectOrb();
        }
    }
}
