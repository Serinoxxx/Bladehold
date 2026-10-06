using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     One beat of the tutorial ("walk here", "cut the banners"). <see cref="TutorialDirector" /> runs its
///     steps in order: <see cref="Begin" /> when the step becomes current, and the subclass calls
///     <see cref="Complete" /> once its condition is met. Scene reactions that belong to a step (a door
///     slamming when the arena fight starts, a gate opening after it) hang off
///     <see cref="onStepStarted" /> / <see cref="onStepCompleted" /> as persistent listeners.
/// </summary>
public abstract class TutorialStep : MonoBehaviour
{
    [Header("Step")]
    [Tooltip("Telemetry id, e.g. Move, Heavy, Bow.")]
    [SerializeField] private string stepId = "Step";

    [Tooltip("Where the HUD waypoint points while this step is current. Also the fall-respawn point.")]
    [SerializeField] private Transform waypointTarget;

    [SerializeField] private Vector3 waypointOffset = new Vector3(0f, 1.5f, 0f);

    [SerializeField] private TutorialHint hint = new TutorialHint();

    [Tooltip("Optional second line (e.g. \"[Space] Dodge\" during the arena fight).")]
    [SerializeField] private TutorialHint secondaryHint = new TutorialHint();

    [Header("Battle scenes")]
    [Tooltip("While this step is current (or next up), holding Ready can't start the wave (WaveStartGate). WaveEventStep ignores this: its condition is the wave itself.")]
    [SerializeField] private bool blocksWaveStart = true;
    [Tooltip("While this step is current, every tower and spike ring holds fire, so the player's lesson kills are their own (the charge lessons).")]
    [SerializeField] private bool suspendDefenses;

    [Header("Feedback (optional)")]
    [Tooltip("Optional: played at the waypoint when the step starts. Leave empty for none.")]
    [SerializeField] private MMF_Player startFeedback;
    [Tooltip("Optional: played at the waypoint when the step completes. Leave empty for none.")]
    [SerializeField] private MMF_Player completeFeedback;

    [Header("Events")]
    public UnityEvent onStepStarted = new UnityEvent();
    public UnityEvent onStepCompleted = new UnityEvent();

    protected TutorialDirector Director { get; private set; }

    public string StepId => stepId;
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }
    public Transform WaypointTarget => waypointTarget;
    public TutorialHint Hint => hint;
    public TutorialHint SecondaryHint => secondaryHint;
    public bool SuspendDefenses => suspendDefenses;

    /// <summary>
    ///     Whether the Ready hold is shut while this step runs. Polled every frame by the director, so a step
    ///     can open it when it can no longer finish (the build steps when the player can't afford the build).
    /// </summary>
    public virtual bool BlocksWaveStart => blocksWaveStart;

    public void Begin(TutorialDirector director)
    {
        Director = director;
        IsActive = true;
        IsComplete = false;
        if (startFeedback != null) startFeedback.PlayFeedbacks(FeedbackPosition);
        onStepStarted.Invoke();
        OnBegin();
    }

    /// <summary>Subclass hook: subscribe to whatever completes this step. May call <see cref="Complete" /> at once.</summary>
    protected abstract void OnBegin();

    /// <summary>Subclass hook: unsubscribe. Runs on completion and on destroy.</summary>
    protected virtual void OnEnd() { }

    /// <summary>
    ///     The tower/wall plots whose prep BUILD markers stay visible while this step runs
    ///     (<see cref="BuildMarkerFocus" />). Add nothing to hide them all, so only the step's own waypoint shows.
    ///     Return false to leave every marker up.
    /// </summary>
    public virtual bool GetBuildMarkerFocus(List<Object> allowedPlots) => true;

    /// <summary>Extra HUD waypoints beyond <see cref="WaypointTarget" /> (e.g. the ammo crate when low).</summary>
    public virtual void GetExtraWaypoints(List<ObjectiveWaypointTarget> results) { }

    public ObjectiveWaypointTarget BuildWaypoint(Sprite icon, Color tint)
    {
        return new ObjectiveWaypointTarget(waypointTarget, waypointOffset, icon, tint, null);
    }

    protected void Complete()
    {
        if (!IsActive || IsComplete) return;
        IsComplete = true;
        IsActive = false;
        OnEnd();
        if (completeFeedback != null) completeFeedback.PlayFeedbacks(FeedbackPosition);
        onStepCompleted.Invoke();
        if (Director != null) Director.NotifyStepCompleted(this);
    }

    protected virtual void OnDestroy()
    {
        if (IsActive) OnEnd();
    }

    private Vector3 FeedbackPosition => waypointTarget != null ? waypointTarget.position : transform.position;
}
