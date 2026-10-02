using UnityEngine;

/// <summary>
///     Tutorial T3 (plan 17): build a wall in one of the field's rock gaps. Completes when a wall is built
///     (<see cref="WallStructure.OnAnyWallBuilt" />) on <see cref="targetPlot" /> if set, else on any plot.
///     The second hint line explains the rerouting: goblins go round a walled gap, and only break through
///     when every gap is walled. Like <see cref="BuildDefenseStep" />, it also completes if the player
///     starts the wave without building (walls are prep-only, so it could never finish otherwise).
///     Point the waypoint at the target plot's crafting station.
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
