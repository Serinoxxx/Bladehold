using System.Collections.Generic;

/// <summary>
///     An objective that can show where it will happen before it starts (plan 15 prep phase): ghost
///     cages, catapult spots, the ram's start and route. <see cref="ObjectiveWaypointTrackerUI" /> draws
///     these for the picked wave card's objective while the player builds towers.
/// </summary>
public interface IObjectivePreview
{
    /// <summary>Adds the world points the objective will use. Called every frame during prep; must not spawn anything.</summary>
    void GetPreviewWaypointTargets(List<ObjectiveWaypointTarget> results);
}
