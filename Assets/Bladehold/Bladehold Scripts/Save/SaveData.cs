using System;
using System.Collections.Generic;

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

    /// <summary>
    ///     Ids of every skill-tree node the player has purchased. Re-applied as stat modifiers on each run
    ///     by <see cref="SkillTreeService" />, making upgrades permanent meta-progression like gold.
    /// </summary>
    public List<string> purchasedNodeIds = new List<string>();
}
