using UnityEngine;

/// <summary>
///     Scales the player's max health based on the <see cref="StatType.PlayerMaxHealthMultiplier" /> stat.
///     Listens to the stat and applies it to the player's <see cref="Health" /> component.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Health))]
public class PlayerMaxHealthBinder : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private Health health;

    private float currentMultiplier = 1f;
    private bool anyError = false;

    private void OnValidate()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (stats == null || health == null)
        {
            Debug.LogError("PlayerMaxHealthBinder missing PlayerStats or Health component.");
            anyError = true;
            return;
        }

        // Base multiplier is 1.0
        stats.SetBase(StatType.PlayerMaxHealthMultiplier, 1f);

        stats.OnStatChanged += HandleStatChanged;
        Apply();
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(StatType stat)
    {
        if (stat == StatType.PlayerMaxHealthMultiplier)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (anyError) return;

        float newMultiplier = stats.GetValue(StatType.PlayerMaxHealthMultiplier);
        if (newMultiplier <= 0f) return;

        float deltaMultiplier = newMultiplier / currentMultiplier;
        
        if (!Mathf.Approximately(deltaMultiplier, 1f))
        {
            health.ScaleMaxHealth(deltaMultiplier);
            currentMultiplier = newMultiplier;
        }
    }
}
