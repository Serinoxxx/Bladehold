using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Plays a looping low-health warning (heartbeat/vignette pulse) while the player's health fraction
///     is at or below <see cref="threshold" />, and stops it once healed back above. Reacts to
///     <see cref="Health.OnHealthChanged" /> the same way <c>HealthBarUI</c> would; <see cref="Health" />
///     stays unaware of it. <see cref="warningFeedback" /> is expected to be a looping MMF_Player (Sound
///     feedback set to Loop, or an MMF Timescale/vignette loop) — this component only starts/stops it.
/// </summary>
public class LowHealthWarning : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Health fraction (0-1) at or below which the warning starts playing.")]
    [SerializeField] [Range(0f, 1f)] private float threshold = 0.25f;
    [Tooltip("A looping feedback (heartbeat sound / vignette pulse). Started when health drops to the threshold, stopped when it rises back above or the player dies.")]
    [SerializeField] private MMF_Player warningFeedback;

    private bool isWarning = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = Player.Instance != null ? Player.Instance.Health : GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            health = Player.Instance != null ? Player.Instance.Health : null;
        }
        if (health == null)
        {
            Debug.LogError("LowHealthWarning could not find Health (set it or ensure Player.Instance.Health exists).");
            anyError = true;
        }
        if (warningFeedback == null)
        {
            Debug.LogError("LowHealthWarning 'warningFeedback' is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
    }

    private void HandleHealthChanged()
    {
        if (anyError || health.MaxHealth <= 0f)
        {
            return;
        }

        float fraction = health.CurrentHealth / health.MaxHealth;
        bool shouldWarn = !health.IsDead && fraction <= threshold;

        if (shouldWarn && !isWarning)
        {
            isWarning = true;
            warningFeedback.PlayFeedbacks();
        }
        else if (!shouldWarn && isWarning)
        {
            isWarning = false;
            warningFeedback.StopFeedbacks();
        }
    }

    private void HandleDied()
    {
        if (isWarning)
        {
            isWarning = false;
            warningFeedback.StopFeedbacks();
        }
    }
}
