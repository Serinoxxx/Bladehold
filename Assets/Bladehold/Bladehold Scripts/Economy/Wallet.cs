using System;
using UnityEngine;

/// <summary>
///     The player's coin purse. The total is persisted to disk via <see cref="SaveSystem" />: it is
///     loaded on <see cref="Awake" /> (so progress carries across runs and scene reloads) and saved
///     whenever it changes. Raises <see cref="OnCoinsChanged" /> so UI can update. Pickups deposit
///     here via <see cref="Add" />.
/// </summary>
public class Wallet : MonoBehaviour
{
    private SaveData saveData;
    private int coins;

    /// <summary>Current total number of coins.</summary>
    public int Coins => coins;

    /// <summary>Raised whenever the coin total changes, carrying the new total.</summary>
    public event Action<int> OnCoinsChanged;

    private void Awake()
    {
        // Load persisted progress so the total carries over from previous runs.
        saveData = SaveSystem.Load();
        coins = saveData.totalGold;
    }

    public void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;

        // Persist immediately so progress survives even an unexpected quit.
        saveData.totalGold = coins;
        SaveSystem.Save(saveData);

        OnCoinsChanged?.Invoke(coins);
    }
}
