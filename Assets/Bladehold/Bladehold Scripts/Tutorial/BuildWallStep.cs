using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Tutorial T3 (plan 17): build a wall in one of the field's rock gaps. Completes when a wall is built
///     (<see cref="WallStructure.OnAnyWallBuilt" />) on <see cref="targetPlot" /> if set, else on any plot.
///     The second hint line explains the rerouting: goblins go round a walled gap, and only break through
///     when every gap is walled. Like <see cref="BuildDefenseStep" />, it holds Ready shut and keeps only
///     the target workbench's BUILD marker; if the player can't afford a wall it opens Ready and completes
///     when the wave starts (walls are prep-only). Point the waypoint at the target plot's crafting station.
/// </summary>
public class BuildWallStep : TutorialStep
{
    [Tooltip("Optional: the wall plot the waypoint marks. Leave empty to accept a wall on any plot.")]
    [SerializeField] private WallPlot targetPlot;

    protected override void OnBegin()
    {
        WallStructure.OnAnyWallBuilt += HandleBuilt;
        if (GameLoopManager.Instance != null) GameLoopManager.Instance.OnWaveStarted += HandleWaveStarted;
        if (targetPlot != null ? targetPlot.HasStandingWall : AnyWallStanding()) Complete();
    }

    protected override void OnEnd()
    {
        WallStructure.OnAnyWallBuilt -= HandleBuilt;
        if (GameLoopManager.Instance != null) GameLoopManager.Instance.OnWaveStarted -= HandleWaveStarted;
    }

    public override bool BlocksWaveStart => base.BlocksWaveStart && !(IsActive && RunSession.InRunSupply < WallPlot.BuildCost);

    public override bool GetBuildMarkerFocus(List<Object> allowedPlots)
    {
        if (targetPlot == null) return false;
        allowedPlots.Add(targetPlot);
        return true;
    }

    // Only reachable once the soft lock has opened Ready (see BlocksWaveStart).
    private void HandleWaveStarted(int wave) => Complete();

    private void HandleBuilt(WallStructure wall)
    {
        if (targetPlot == null || wall.Plot == targetPlot) Complete();
    }

    private static bool AnyWallStanding()
    {
        foreach (WallStructure wall in WallStructure.All)
        {
            if (wall != null && wall.IsStanding) return true;
        }
        return false;
    }
}
