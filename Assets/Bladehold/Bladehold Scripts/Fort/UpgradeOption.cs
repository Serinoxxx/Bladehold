using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     One slice on the upgrade wheel (plan 17): a label, an icon, a supply and/or crystal price and
///     what buying it does. Structures build their own list (<see cref="IUpgradeable" />); the wheel just
///     renders it, so new upgrade kinds never touch the UI code.
/// </summary>
public class UpgradeOption
{
    public string label;
    public string description;
    public Sprite icon;
    public int supplyCost;
    public StructureElement crystalElement = StructureElement.None;
    public int crystalCost;
    /// <summary>Set when the option is shown but can't be bought (maxed, locked element, wrong phase). Shown instead of the cost.</summary>
    public string blockedReason;
    /// <summary>Replaces the cost line (e.g. "Refund").</summary>
    public string costOverride;
    /// <summary>Runs after the cost is paid. Return false to refuse; the cost is handed back.</summary>
    public Func<bool> onPurchase;
    /// <summary>The wheel closes after this one (Deconstruct).</summary>
    public bool closesWheel;

    public bool IsBlocked => !string.IsNullOrEmpty(blockedReason);

    public bool CanAfford =>
        RunSession.InRunSupply >= supplyCost &&
        (crystalCost <= 0 || RunSession.GetCrystals(crystalElement) >= crystalCost);

    public bool IsAvailable => !IsBlocked && CanAfford && onPurchase != null;

    /// <summary>
    ///     One-line rich-text cost for the wheel slice ("10 Supply + 1 Storm"), red when unaffordable.
    ///     The wheel's details panel shows the full breakdown against what the player holds.
    /// </summary>
    public string CostLabel
    {
        get
        {
            if (IsBlocked) return $"<color=#AAAAAA>{blockedReason}</color>";
            if (!string.IsNullOrEmpty(costOverride)) return costOverride;
            var parts = new List<string>();
            if (supplyCost > 0)
            {
                string c = RunSession.InRunSupply >= supplyCost ? "#FFD700" : "#FF5555";
                parts.Add($"<color={c}>{supplyCost} Supply</color>");
            }
            if (crystalCost > 0)
            {
                string c = RunSession.GetCrystals(crystalElement) >= crystalCost ? crystalElement.Hex() : "#FF5555";
                string shortName = crystalElement == StructureElement.Lightning ? "Storm" : crystalElement.ToString();
                parts.Add($"<color={c}>{crystalCost} {shortName}</color>");
            }
            return parts.Count > 0 ? string.Join(" + ", parts) : "<color=#FFD700>Free</color>";
        }
    }

    /// <summary>Spends supply and crystals together (all or nothing), then runs <see cref="onPurchase" />.</summary>
    public bool TryPurchase()
    {
        if (!IsAvailable) return false;
        if (!RunSession.TrySpendInRunSupply(supplyCost)) return false;
        if (!RunSession.TrySpendCrystals(crystalElement, crystalCost))
        {
            RunSession.AddInRunSupply(supplyCost);
            return false;
        }

        if (onPurchase()) return true;

        RunSession.AddInRunSupply(supplyCost);
        RunSession.AddCrystals(crystalElement, crystalCost);
        return false;
    }
}

/// <summary>A tower or wall the upgrade wheel can open on (plan 17).</summary>
public interface IUpgradeable
{
    string UpgradeTitle { get; }
    /// <summary>World point for purchase popups and feedbacks.</summary>
    Vector3 UpgradeAnchor { get; }
    /// <summary>False once the structure is gone (the wheel closes itself).</summary>
    bool IsUpgradeTargetAlive { get; }
    void BuildUpgradeOptions(List<UpgradeOption> options);
}
