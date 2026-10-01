using System.Collections.Generic;

/// <summary>
///     A HUD waypoint provider outside the objective system (the tutorial's current step, first-encounter
///     hints). Register with <see cref="ObjectiveWaypointTrackerUI.RegisterSource" />; the tracker polls
///     every registered source each frame alongside the active objective.
/// </summary>
public interface IWaypointSource
{
    void GetWaypointTargets(List<ObjectiveWaypointTarget> results);
}
