using UnityEngine;

/// <summary>
///     The one element a tower or wall can be imbued with (plan 17). Bought on the upgrade wheel with
///     the matching <see cref="RunSession" /> elemental crystals and locked once chosen. Maps onto the
///     <see cref="EnemyStatusManager" /> status ids "Fire" / "Ice" / "Lightning".
/// </summary>
public enum StructureElement
{
    None = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3
}

public static class StructureElements
{
    /// <summary>The three real elements, in wheel order.</summary>
    public static readonly StructureElement[] All = { StructureElement.Fire, StructureElement.Ice, StructureElement.Lightning };

    /// <summary>The <see cref="EnemyStatusManager" /> / <see cref="Damage.elementId" /> id, or "" for None.</summary>
    public static string StatusId(this StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => "Fire",
            StructureElement.Ice => "Ice",
            StructureElement.Lightning => "Lightning",
            _ => ""
        };
    }

    /// <summary>Rich-text colour for costs, counters and labels.</summary>
    public static string Hex(this StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => "#FF7A2E",
            StructureElement.Ice => "#7FD8FF",
            StructureElement.Lightning => "#FFE14D",
            _ => "#FFFFFF"
        };
    }

    public static Color Tint(this StructureElement element)
    {
        return ColorUtility.TryParseHtmlString(element.Hex(), out Color c) ? c : Color.white;
    }

    /// <summary>"Fire Crystal" etc., for cost labels and pickups.</summary>
    public static string CrystalName(this StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => "Fire Crystal",
            StructureElement.Ice => "Ice Crystal",
            StructureElement.Lightning => "Storm Crystal",
            _ => "Crystal"
        };
    }
}
