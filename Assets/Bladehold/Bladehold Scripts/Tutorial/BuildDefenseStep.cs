using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Completes when a defence finishes building (<see cref="TowerPlot.OnBuilt" />) — on
///     <see cref="targetPlot" /> if set, else on any plot. The waypoint should sit on the target plot, and
///     only that plot keeps its BUILD marker. The step holds Ready shut until the build is done; if the
///     player can no longer afford it (the soft lock), it opens Ready and completes when the wave starts,
///     since building is prep-only. While active, the build wheel offers only <see cref="onlyDefense" />
///     (an Arrow Tower), so the first build isn't a wall of choices; the rest open up once it's done.
/// </summary>
public class BuildDefenseStep : TutorialStep
{
    [Tooltip("Optional: the plot the waypoint marks. Leave empty to accept a build on any plot.")]
    [SerializeField] private TowerPlot targetPlot;

    [Tooltip("While this step runs, the build wheel shows only this defence.")]
    [SerializeField] private bool restrictWheel = true;
    [SerializeField] private FortDefenseType onlyDefense = FortDefenseType.ArrowSlits;

    protected override void OnBegin()
    {
        if (restrictWheel) BuildWheelUI.OnlyAllowed = onlyDefense;
        TowerPlot.OnBuilt += HandleBuilt;
        if (GameLoopManager.Instance != null) GameLoopManager.Instance.OnWaveStarted += HandleWaveStarted;
        if (targetPlot != null && targetPlot.IsOccupied) Complete();
    }

    protected override void OnEnd()
    {
        if (restrictWheel) BuildWheelUI.OnlyAllowed = null;
        TowerPlot.OnBuilt -= HandleBuilt;
        if (GameLoopManager.Instance != null) GameLoopManager.Instance.OnWaveStarted -= HandleWaveStarted;
    }

    public override bool BlocksWaveStart => base.BlocksWaveStart && !(IsActive && CannotAffordBuild());

    public override bool GetBuildMarkerFocus(List<Object> allowedPlots)
    {
        if (targetPlot == null) return false;
        allowedPlots.Add(targetPlot);
        return true;
    }

    // Only reachable once the soft lock has opened Ready (see BlocksWaveStart).
    private void HandleWaveStarted(int wave) => Complete();

    private bool CannotAffordBuild()
    {
        if (targetPlot != null && (targetPlot.IsOccupied || targetPlot.IsBuilding)) return false;
        BuildWheelUI wheel = BuildWheelUI.Instance;
        int cost = wheel != null ? wheel.SupplyCostOf(onlyDefense) : -1;
        return cost > 0 && RunSession.InRunSupply < cost;
    }

    private void HandleBuilt(TowerPlot plot)
    {
        if (targetPlot == null || plot == targetPlot) Complete();
    }
}
