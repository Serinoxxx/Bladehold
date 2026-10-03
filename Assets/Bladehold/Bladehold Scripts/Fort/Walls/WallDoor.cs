using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The gate in the middle of a <see cref="WallStructure" /> (plan 17). [E] on it opens or shuts it.
///     Open: the doorway's NavMesh area drops to cost 1 and both enemies and the player walk through.
///     Shut: a collider blocks the player and a non-carving <see cref="NavMeshObstacle" /> makes enemy
///     avoidance steer off it, while the wall's target claim stops them at the face. It won't shut on
///     anything standing in the doorway.
///
///     Sits at the doorway's centre (local X across the bridge). The model's hand-placed leaf (the
///     portcullis) is moved onto a mount child that slides straight down into the ground to open (and is
///     hidden once fully sunk, so it never pokes out under a bridge deck), then rises back up to shut. The
///     blocker collider doesn't move. The blocker and obstacle are assigned by hand in Wall.prefab (they can
///     sit on any non-moving child); an unassigned one is built on this object from <see cref="WallConfigSO" />.
/// </summary>
public class WallDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRadius = 3f;
    [Tooltip("Where the [E] prompt sits and the radius is measured from. Not on the door leaf (it sinks). Empty = 1 m above this object.")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("Door grinds down into the ground (chains, stone scrape).")]
    [SerializeField] private MMF_Player openFeedback;
    [Tooltip("Door rises back up and locks (grind, thud).")]
    [SerializeField] private MMF_Player closeFeedback;
    [Tooltip("Tried to shut it on something in the doorway (rattle).")]
    [SerializeField] private MMF_Player blockedFeedback;
    [Tooltip("Blocks the player while shut; also the box checked for anyone standing in the doorway. Not on the door leaf (its colliders are stripped). Empty = built from WallConfig.")]
    [SerializeField] private BoxCollider blocker;
    [Tooltip("Non-carving Box obstacle that makes enemy avoidance steer off the shut door. Empty = built from WallConfig.")]
    [SerializeField] private NavMeshObstacle obstacle;

    private WallStructure wall;
    private WallConfigSO art;
    private Transform mount;
    private GameObject leaf;
    private Renderer[] leafRenderers = System.Array.Empty<Renderer>();
    private float sinkDepth;
    private Coroutine slide;
    private bool collapsed;
    private readonly Collider[] doorwayBuffer = new Collider[8];

    public bool IsOpen { get; private set; }
    /// <summary>The hand-assigned blocker (null until built if unassigned), spared by the wall's decor-collider strip.</summary>
    public BoxCollider Blocker => blocker;

    public string PromptText => IsOpen ? "Close Gate" : "Open Gate";
    public bool CanInteract => wall != null && wall.IsStanding && !collapsed;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position + Vector3.up;
    public float InteractionRadius => interactionRadius;

    private void Start()
    {
        if (openFeedback == null) Debug.LogError($"[WallDoor] {name}: openFeedback is not assigned.", this);
        if (closeFeedback == null) Debug.LogError($"[WallDoor] {name}: closeFeedback is not assigned.", this);
        if (blockedFeedback == null) Debug.LogError($"[WallDoor] {name}: blockedFeedback is not assigned.", this);
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    /// <param name="doorLeaf">The authored leaf (may be null), moved onto the sliding mount where it stands.</param>
    public void Init(WallStructure owner, WallConfigSO wallArt, GameObject doorLeaf)
    {
        wall = owner;
        float doorWidth = wallArt != null ? wallArt.doorWidth : 3f;
        float height = wallArt != null ? wallArt.wallHeight : 3.5f;
        float thickness = wallArt != null ? wallArt.wallThickness : 0.8f;

        mount = new GameObject("Mount").transform;
        mount.SetParent(transform, false);
        sinkDepth = height + 0.2f;
        art = wallArt;
        leaf = doorLeaf;
        if (leaf != null)
        {
            leaf.transform.SetParent(mount, true);
            foreach (Collider c in leaf.GetComponentsInChildren<Collider>(true)) Destroy(c);
            leafRenderers = leaf.GetComponentsInChildren<Renderer>(true);
            // Sink far enough that the whole leaf ends up underground.
            foreach (Renderer r in leafRenderers) sinkDepth = Mathf.Max(sinkDepth, r.bounds.max.y - transform.position.y + 0.2f);
        }

        // Hand-assigned blocker/obstacle win; only an unassigned one is built from the config.
        if (blocker == null)
        {
            blocker = gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, height * 0.5f, 0f);
            blocker.size = new Vector3(doorWidth, height, thickness);
        }

        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, height * 0.5f, 0f);
            obstacle.size = new Vector3(doorWidth, height, thickness);
        }
        // Carving would cut the doorway out of the NavMesh and break WallNavCost routing.
        obstacle.carving = false;
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;
        if (IsOpen && DoorwayOccupied())
        {
            if (blockedFeedback != null) blockedFeedback.PlayFeedbacks(transform.position + Vector3.up);
            return;
        }
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (collapsed || IsOpen == open) return;
        IsOpen = open;
        blocker.enabled = !open;
        obstacle.enabled = !open;
        MMF_Player fb = open ? openFeedback : closeFeedback;
        if (fb != null) fb.PlayFeedbacks(transform.position + Vector3.up);
        if (slide != null) StopCoroutine(slide);
        slide = StartCoroutine(Slide(open ? -sinkDepth : 0f));
        if (wall != null) wall.OnDoorChanged();
    }

    /// <summary>The wall fell: the door goes with it.</summary>
    public void Collapse()
    {
        collapsed = true;
        IsOpen = true;
        if (blocker != null) blocker.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
        if (leaf != null) leaf.SetActive(false);
    }

    private bool DoorwayOccupied()
    {
        if (blocker == null) return false;
        Transform t = blocker.transform;
        Vector3 centre = t.TransformPoint(blocker.center);
        Vector3 halfExtents = Vector3.Scale(blocker.size * 0.5f, t.lossyScale);
        int mask = LayerMask.GetMask("Enemy", "Player");
        int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, doorwayBuffer, t.rotation, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) doorwayBuffer[i] = null;
        return count > 0;
    }

    private IEnumerator Slide(float targetY)
    {
        if (mount == null) yield break;
        SetLeafVisible(true);
        Vector3 from = mount.localPosition;
        Vector3 to = new Vector3(from.x, targetY, from.z);
        // Duration scales with how far it still has to go, so reversing mid-slide isn't slower.
        float fullDuration = art != null ? art.doorSlideSeconds : 0.8f;
        float duration = fullDuration * Mathf.Clamp01(Mathf.Abs(targetY - from.y) / Mathf.Max(0.01f, sinkDepth));
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            mount.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        mount.localPosition = to;
        // Fully sunk: hide it so it doesn't show through under a bridge deck.
        if (IsOpen) SetLeafVisible(false);
        slide = null;
    }

    private void SetLeafVisible(bool visible)
    {
        foreach (Renderer r in leafRenderers) if (r != null) r.enabled = visible;
    }
}
