using UnityEngine;

/// <summary>
///     Handles the player's death: when the player's <see cref="Health" /> reaches zero it disables
///     the assigned control components (so the player stops moving / responding to input) and fires a
///     death animation trigger. It listens to <see cref="Health.OnDied" />; Health stays unaware of it.
///
///     The components to disable are assigned in the inspector rather than referenced by type, so this
///     stays decoupled from the specific (vendored) player controller in use.
/// </summary>
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;

    [Tooltip("Components disabled on death to stop player control (e.g. the player animation/movement controller and input reader).")]
    [SerializeField] private MonoBehaviour[] componentsToDisable;

    // Animator trigger fired on death. Wire a death state driven by this in the Animator.
    [SerializeField] private string deathTrigger = "Death";

    private int deathTriggerHash;
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

        if (anyError)
        {
            return;
        }

        deathTriggerHash = Animator.StringToHash(deathTrigger);

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
        // Stop player control, then let the death state take over the animator.
        foreach (MonoBehaviour component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = false;
            }
        }

        animator.SetTrigger(deathTriggerHash);
    }
}
