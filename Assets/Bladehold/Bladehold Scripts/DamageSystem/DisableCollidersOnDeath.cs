using UnityEngine;

/// <summary>
///     Disables every <see cref="Collider" /> on this object (and its children) when its
///     <see cref="Health" /> dies, so a corpse no longer blocks movement, attacks or further hits.
///     It listens to <see cref="Health.OnDied" />; Health stays unaware of the colliders.
/// </summary>
public class DisableCollidersOnDeath : MonoBehaviour
{
    [SerializeField] private Health health;

    private Collider[] colliders;
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

        // Cache colliders now; the rig's collider set doesn't change at runtime, and we want them
        // even after they're disabled.
        colliders = GetComponentsInChildren<Collider>(includeInactive: true);

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
        foreach (Collider c in colliders)
        {
            if (c != null)
            {
                c.enabled = false;
            }
        }
    }
}
