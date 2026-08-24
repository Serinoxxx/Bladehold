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
}
