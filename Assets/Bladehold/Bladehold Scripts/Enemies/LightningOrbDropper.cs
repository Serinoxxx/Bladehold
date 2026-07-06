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
    [SerializeField] private LightningOrb orbPrefab;
    [Tooltip("World-space offset from this transform where the orb spawns.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);
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
        if (orbPrefab == null)
        {
            Debug.LogError("LightningOrb prefab is not assigned in the inspector.");
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

        Instantiate(orbPrefab, transform.position + dropOffset, Quaternion.identity);
    }
}
