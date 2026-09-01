using System;

/// <summary>
///     Defines the contract for an objective in Survivors Mode (e.g. kill wave, destroy siege engines,
///     escort wagon, free prisoners).
/// </summary>
public interface ISurvivorsObjective
{
    /// <summary>Unique identifier for this objective type.</summary>
    string ObjectiveId { get; }

    /// <summary>Display title shown in HUD header and quest banners.</summary>
    string Title { get; }

    /// <summary>Short descriptive text explaining what the player must do.</summary>
    string Description { get; }

    /// <summary>Formatted progress text (e.g. "Slay goblins: 14/50").</summary>
    string ProgressText { get; }

    /// <summary>Normalized progress 0..1 for progress bars.</summary>
    float ProgressNormalized { get; }

    /// <summary>Whether this objective has met its completion condition.</summary>
    bool IsComplete { get; }

    /// <summary>Whether this objective has failed (e.g. timeout or VIP perished).</summary>
    bool IsFailed { get; }

    /// <summary>Whether this objective is currently active and updating.</summary>
    bool IsActive { get; }

    /// <summary>Fired whenever objective progress changes.</summary>
    event Action<ISurvivorsObjective> OnProgressChanged;

    /// <summary>Fired when the objective successfully completes.</summary>
    event Action<ISurvivorsObjective> OnCompleted;

    /// <summary>Fired when the objective fails (e.g. timeout expired).</summary>
    event Action<ISurvivorsObjective> OnFailed;

    /// <summary>Initializes or activates the objective.</summary>
    void StartObjective();

    /// <summary>Updates the objective each frame while active.</summary>
    void UpdateObjective(float deltaTime);

    /// <summary>Cleans up spawned entities and listeners when the objective ends.</summary>
    void CleanupObjective();

    /// <summary>
    ///     Gets the active target position for enemies to flock to during this objective, if any.
    /// </summary>
    UnityEngine.Vector3? GetObjectiveTargetPosition(UnityEngine.Vector3 searchFromPosition);

    /// <summary>
    ///     Gets the active target damageable for enemies during this objective, if any.
    /// </summary>
    IDamageable GetObjectiveDamageable(UnityEngine.Vector3 searchFromPosition);

    /// <summary>
    ///     Gets the active waypoint target transforms for this objective to overlay on the HUD.
    /// </summary>
    void GetActiveWaypointTargets(System.Collections.Generic.List<ObjectiveWaypointTarget> results);
}

/// <summary>
///     Information describing a single world-space objective waypoint target for HUD overlays.
/// </summary>
public struct ObjectiveWaypointTarget
{
    public UnityEngine.Transform Transform;
    public UnityEngine.Vector3 WorldOffset;
    public UnityEngine.Sprite CustomIcon;
    public UnityEngine.Color TintColor;
    public string Label;

    public ObjectiveWaypointTarget(
        UnityEngine.Transform transform,
        UnityEngine.Vector3 worldOffset = default,
        UnityEngine.Sprite customIcon = null,
        UnityEngine.Color tintColor = default,
        string label = null)
    {
        Transform = transform;
        WorldOffset = worldOffset;
        CustomIcon = customIcon;
        TintColor = tintColor == default ? UnityEngine.Color.white : tintColor;
        Label = label;
    }
}

