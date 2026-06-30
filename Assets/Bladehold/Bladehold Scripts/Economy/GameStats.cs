using System;
using UnityEngine;

/// <summary>
///     Per-run scoreboard: how many goblins the player has killed and how much gold they've earned
///     this run. A singleton like <see cref="Player" />; because it lives in the scene it resets
///     naturally when the scene reloads (Try Again).
/// </summary>
public class GameStats : MonoBehaviour
{
    public static GameStats Instance;

    /// <summary>Goblins killed this run.</summary>
    public int GoblinsKilled { get; private set; }

    /// <summary>Gold collected this run.</summary>
    public int GoldEarnedThisRun { get; private set; }

    /// <summary>Raised whenever a stat changes.</summary>
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterGoblinKilled()
    {
        GoblinsKilled++;
        OnChanged?.Invoke();
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GoldEarnedThisRun += amount;
        OnChanged?.Invoke();
    }
}
