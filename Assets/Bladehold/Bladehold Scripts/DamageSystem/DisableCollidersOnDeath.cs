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

        // Cache colliders now; we want them even after they're disabled.
        // Exclude ragdoll bone colliders (which are on the Ragdoll layer and managed by EnemyRagdoll)
        // so a corpse flung by a lethal impulse hit keeps them to tumble and land with.
        int ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        var allColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        var nonRagdollColliders = new System.Collections.Generic.List<Collider>();
        foreach (Collider c in allColliders)
        {
            if (ragdollLayer >= 0 && c.gameObject.layer == ragdollLayer)
            {
                continue;
            }
            nonRagdollColliders.Add(c);
        }
        colliders = nonRagdollColliders.ToArray();

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
