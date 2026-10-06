using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Narrows the prep-phase BUILD markers (the HUD waypoints from <see cref="ObjectiveWaypointTrackerUI" />
///     and the empty-plot outlines on the minimap) to a few plots, so a tutorial lesson's own waypoint isn't
///     lost among them (plan 21). While an owner has set a focus, only its allowed
///     <see cref="TowerPlot" />s / <see cref="WallPlot" />s get a marker; an empty focus hides them all.
///     One owner at a time: the latest <see cref="Set" /> wins. A destroyed owner counts as cleared.
/// </summary>
public static class BuildMarkerFocus
{
    private static readonly HashSet<Object> allowed = new HashSet<Object>();
    private static Object owner;

    /// <summary>True while a live owner is filtering the markers.</summary>
    public static bool IsActive => owner != null;

    public static void Set(Object focusOwner, IEnumerable<Object> allowedPlots)
    {
        owner = focusOwner;
        allowed.Clear();
        if (allowedPlots == null) return;
        foreach (Object plot in allowedPlots)
        {
            if (plot != null) allowed.Add(plot);
        }
    }

    public static void Clear(Object focusOwner)
    {
        if (owner != focusOwner) return;
        owner = null;
        allowed.Clear();
    }

    /// <summary>Whether <paramref name="plot" /> may show its marker (always, with no focus set).</summary>
    public static bool Allows(Object plot) => !IsActive || allowed.Contains(plot);
}
