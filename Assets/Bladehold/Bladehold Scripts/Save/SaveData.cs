using System;

/// <summary>
///     Serializable snapshot of persisted player progress, written to disk by <see cref="SaveSystem" />.
///     Add new fields here as more progress needs saving; existing saves load missing fields as their
///     defaults.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>The player's accumulated total gold, persisted across runs.</summary>
    public int totalGold;
}
