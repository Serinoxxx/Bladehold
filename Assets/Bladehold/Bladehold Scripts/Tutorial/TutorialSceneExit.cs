using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The door to the next tutorial scene: an [E] interactable, locked until its scene's
///     <see cref="TutorialDirector" /> has finished every step (<see cref="Unlock" />).
/// </summary>
public class TutorialSceneExit : MonoBehaviour, IInteractable
{
    [SerializeField] private string nextSceneName;
    [Tooltip("Where the player stands to use it, and where the waypoint points. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float interactionRadius = 3f;

    [Tooltip("Optional: played on use, before the loading screen (whoosh). Leave empty for none.")]
    [SerializeField] private MMF_Player exitFeedback;

    private bool unlocked;
    private bool used;

    public Transform WaypointAnchor => interactionAnchor != null ? interactionAnchor : transform;
    public Vector3 InteractionPosition => WaypointAnchor.position;
    public float InteractionRadius => interactionRadius > 0f ? interactionRadius : 3f;
    public bool CanInteract => unlocked && !used && !string.IsNullOrEmpty(nextSceneName);
    public string PromptText => Loc.Get("tutorial.exit_prompt", "Continue");

    private void OnEnable() => InteractableRegistry.Register(this);

    private void OnDisable() => InteractableRegistry.Unregister(this);

    private void Start()
    {
        if (string.IsNullOrEmpty(nextSceneName)) Debug.LogError($"[TutorialSceneExit] {name}: nextSceneName is not set.", this);
    }

    public void Unlock() => unlocked = true;

    public void Interact(Player player)
    {
        if (!CanInteract) return;
        used = true;
        if (exitFeedback != null) exitFeedback.PlayFeedbacks(transform.position);
        TutorialRun.LoadScene(nextSceneName);
    }
}
