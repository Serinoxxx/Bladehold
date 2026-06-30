using UnityEngine;

/// <summary>
///     Marks a GameObject as an enemy and reports its death to the run's <see cref="GameStats" />
///     scoreboard. Listens to <see cref="Health.OnDied" />; Health stays unaware of scoring.
/// </summary>
public class Enemy : MonoBehaviour
{
    [SerializeField] private Health health;

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
        if (GameStats.Instance != null)
        {
            GameStats.Instance.RegisterGoblinKilled();
        }
    }
}
