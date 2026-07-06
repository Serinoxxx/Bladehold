using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Binds an <see cref="MMHealthBar" /> to a <see cref="Health" />: refreshes the bar whenever
///     health changes. It listens to <see cref="Health.OnHealthChanged" />; Health stays unaware of
///     the bar. Hiding at zero, lerping and bump-on-change are all handled by the MMHealthBar itself.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private MMHealthBar healthBar;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponent<MMHealthBar>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (healthBar == null)
        {
            Debug.LogError("MMHealthBar component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnHealthChanged += Refresh;

        // Start-order safety: if Health.Start already ran, its initial OnHealthChanged fired before we
        // subscribed — refresh now; if it hasn't run yet, its event will overwrite this shortly.
        Refresh();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        healthBar.UpdateBar(health.CurrentHealth, 0f, health.MaxHealth, show: true);
    }
}
