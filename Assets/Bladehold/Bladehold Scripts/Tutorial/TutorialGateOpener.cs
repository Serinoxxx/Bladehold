using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A gate/door the tutorial opens or slams shut: the portcullis that lifts after the arena fight,
///     the arena entry door that closes behind the player. <see cref="Open" /> / <see cref="Close" /> are
///     public for a step's onStepStarted/onStepCompleted events. The motion itself is the feedback's
///     Position feedback; this only plays it and toggles the blocking collider.
/// </summary>
public class TutorialGateOpener : MonoBehaviour
{
    [SerializeField] private bool startsOpen;

    [Tooltip("Collider that stops the player while closed.")]
    [SerializeField] private Collider blocker;

    [Tooltip("Lifts/slides the gate open: chain rattle, grind, falling dust, impulse.")]
    [SerializeField] private MMF_Player openFeedback;

    [Tooltip("Optional: slams it shut. Leave empty if this gate never closes.")]
    [SerializeField] private MMF_Player closeFeedback;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        if (blocker == null) Debug.LogError($"[TutorialGateOpener] {name}: blocker is not assigned.", this);
        if (openFeedback == null && !startsOpen) Debug.LogError($"[TutorialGateOpener] {name}: openFeedback is not assigned.", this);
        IsOpen = startsOpen;
        if (blocker != null) blocker.enabled = !startsOpen;
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (blocker != null) blocker.enabled = false;
        if (openFeedback != null) openFeedback.PlayFeedbacks(transform.position);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (blocker != null) blocker.enabled = true;
        if (closeFeedback != null) closeFeedback.PlayFeedbacks(transform.position);
    }
}
