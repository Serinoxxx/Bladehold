using System;
using System.Collections.Generic;

/// <summary>
///     What has been bought on one tower or wall through the upgrade wheel, and what it cost, so
///     Deconstruct (and the sector-end dismantle) can hand back exactly 100% (plan 17). Plain data held by
///     <see cref="DefenseStructure" /> and <see cref="WallStructure" />; it never outlives its structure.
/// </summary>
[Serializable]
public class StructureUpgradeState
{
    /// <summary>Tower fire-rate tier, 0 = none.</summary>
    public int fireRateTier;
    /// <summary>Wall material: 0 wood, 1 stone, 2 metal.</summary>
    public int materialTier;
    public bool hasSpikes;
    public StructureElement element = StructureElement.None;

    /// <summary>Supply paid for upgrades and repairs (not the build cost, not tower ammo).</summary>
    public int supplySpent;
    private readonly int[] crystalsSpent = new int[4];

    public bool HasElement => element != StructureElement.None;

    public void RecordSupply(int amount)
    {
        if (amount > 0) supplySpent += amount;
    }

    public void RecordCrystals(StructureElement crystal, int amount)
    {
        if (amount > 0 && crystal != StructureElement.None) crystalsSpent[(int)crystal] += amount;
    }

    public int CrystalsSpent(StructureElement crystal)
    {
        return crystal == StructureElement.None ? 0 : crystalsSpent[(int)crystal];
    }

    /// <summary>"+40 Supply, +3 Fire Crystal" for refund popups. Call before <see cref="RefundCrystals" />.</summary>
    public string DescribeRefund(int supply)
    {
        var parts = new List<string>();
        if (supply > 0) parts.Add($"+{supply} Supply");
        foreach (StructureElement crystal in StructureElements.All)
        {
            int amount = crystalsSpent[(int)crystal];
            if (amount > 0) parts.Add($"+{amount} {crystal.CrystalName()}");
        }
        return string.Join(", ", parts);
    }

    /// <summary>Pays every crystal spent back into <see cref="RunSession" /> and zeroes the record.</summary>
    public void RefundCrystals()
    {
        foreach (StructureElement crystal in StructureElements.All)
        {
            int amount = crystalsSpent[(int)crystal];
            crystalsSpent[(int)crystal] = 0;
            RunSession.AddCrystals(crystal, amount);
        }
    }
}
