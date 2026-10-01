using UnityEngine;

/// <summary>
///     Completes when a defence finishes building (<see cref="TowerPlot.OnBuilt" />) — on
///     <see cref="targetPlot" /> if set, else on any plot. The waypoint should sit on the target plot.
///     Also completes if the player starts the wave without building (building is prep-only, so the step
///     could never finish otherwise). While active, the build wheel offers only <see cref="onlyDefense" />
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

    private void HandleWaveStarted(int wave) => Complete();

    private void HandleBuilt(TowerPlot plot)
    {
        if (targetPlot == null || plot == targetPlot) Complete();
    }
}
