using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Drives a screen-space <see cref="MMProgressBar" /> for the player's health, always visible.
///     Subscribes to <see cref="Health.OnHealthChanged" /> and calls <c>UpdateBar</c> on every change.
///     Auto-wires from <see cref="Player.Instance" /> in <c>Start</c> when <c>health</c> is left empty.
/// </summary>
public class PlayerHealthBarUI : MonoBehaviour
{
    [Tooltip("The player's Health component. Auto-wired from Player.Instance if left empty.")]
    [SerializeField] private Health health;

    [Tooltip("The MMProgressBar that visualises player health.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Optional text field to display exact health (e.g. 10 / 10).")]
    [SerializeField] private TMPro.TextMeshProUGUI healthText;

    private bool _anyError;

    private void Start()
    {
        if (health == null && Player.Instance != null)
            health = Player.Instance.Health;

        if (health == null)
        {
            Player p = FindObjectOfType<Player>();
            if (p != null)
            {
                health = p.Health;
                if (health == null)
                    health = p.GetComponent<Health>();
            }
        }

        if (health == null)
        {
            Debug.LogError("[PlayerHealthBarUI] Health not found — assign it or ensure Player.Instance is present.");
            _anyError = true;
        }

        if (progressBar == null)
        {
            Debug.LogError("[PlayerHealthBarUI] MMProgressBar not assigned.");
            _anyError = true;
        }

        if (_anyError) return;

        health.OnHealthChanged += Refresh;
        // Start-order safety: refresh immediately in case Health.Start already fired.
        Refresh();
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        progressBar.UpdateBar(health.CurrentHealth, 0f, health.MaxHealth);
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
        }
    }
}
