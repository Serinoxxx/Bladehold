using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Builds and maintains the visual skill tree. Instantiates a <see cref="SkillNodeView" /> per node at
///     its authored (x, y) coordinates inside <see cref="content" /> (place that under a ScrollRect to pan a
///     large tree), draws connector images from each node to its prerequisites, and refreshes everything
///     whenever the tree changes or the player's gold changes. Clicking an available node buys it; hovering
///     a node drives the optional cursor-following <see cref="SkillTooltip" />. Scroll-wheel zooms
///     <see cref="content" /> in/out around the cursor (<see cref="OnScroll" />); the pan/zoom is remembered
///     per tree across reopenings (<see cref="RestoreView" />/<see cref="SaveView" />).
///
///     This is the "upgrade screen" content: drop it on the death-screen canvas so it appears with the
///     death screen on player death.
/// </summary>
public class SkillTreeView : MonoBehaviour, IScrollHandler
{
    [Tooltip("Optional; defaults to SkillTreeService.Instance. Assign explicitly to render a different tree (e.g. ReincarnateService).")]
    [SerializeField] private MonoBehaviour serviceBehaviour;
    [Tooltip("Optional; defaults to Player.Instance.Wallet (for live gold display and refresh).")]
    [SerializeField] private Wallet wallet;

    private ISkillTreeService service;

    [Header("Layout")]
    [Tooltip("Container the node views and connectors are parented to (e.g. a ScrollRect's content).")]
    [SerializeField] private RectTransform content;
    [SerializeField] private SkillNodeView nodePrefab;
    [Tooltip("Optional: an Image (with pivot at left-center, height = line thickness) stretched/rotated between connected nodes.")]
    [SerializeField] private RectTransform connectorPrefab;
    [Tooltip("Pixels between adjacent (x, y) grid steps.")]
    [SerializeField] private float spacing = 160f;
    [Tooltip("Empty space (px) between the outermost nodes and the content edge, so edge nodes aren't flush against the viewport when scrolled to the extremes.")]
    [SerializeField] private float contentPadding = 200f;

    [Header("Zoom")]
    [Tooltip("content.localScale at maximum zoom-out.")]
    [SerializeField] private float minZoom = 0.5f;
    [Tooltip("content.localScale at maximum zoom-in.")]
    [SerializeField] private float maxZoom = 2f;
    [Tooltip("Zoom change per unit of mouse-wheel scroll delta.")]
    [SerializeField] private float zoomStep = 0.05f;
    [Tooltip("How quickly the zoom eases toward its scrolled-to target; higher = snappier, lower = more sluggish.")]
    [SerializeField] private float zoomSmoothSpeed = 10f;
    [Tooltip("Seconds to pan-center the view on a node just purchased.")]
    [SerializeField] private float centerOnUnlockDuration = 0.35f;

    [Header("Optional gold readout")]
    [SerializeField] private TMP_Text goldText;

    [Header("Optional tooltip")]
    [Tooltip("Cursor-following tooltip shown while hovering a node. Leave unassigned to disable.")]
    [SerializeField] private SkillTooltip tooltip;

    private readonly Dictionary<string, SkillNodeView> views = new Dictionary<string, SkillNodeView>();
    private readonly List<(RectTransform line, SkillNode from, SkillNode to)> connectors = new List<(RectTransform, SkillNode, SkillNode)>();
    private SkillNodeView hoveredView;
    private bool built = false;
    private SkillTreeSO builtTree;
    private bool anyError = false;
    private Vector2 treeOffset;

    private ScrollRect scrollRect;
    private Canvas canvas;
    private Coroutine panRoutine;
    private float zoom = 1f;
    private float targetZoom = 1f;
    private Vector2 zoomPivotLocalPoint;
    private string prefsKeyPrefix;

    private void Start()
    {
        service = serviceBehaviour as ISkillTreeService;
        if (service == null)
        {
            service = SkillTreeService.Instance;
        }
        if (wallet == null)
        {
            wallet = Player.Instance != null ? Player.Instance.Wallet : null;
        }

        if (service == null)
        {
            Debug.LogError("SkillTreeView has no SkillTreeService (set it or ensure SkillTreeService.Instance exists).");
            anyError = true;
        }
        if (content == null)
        {
            Debug.LogError("SkillTreeView 'content' container is not assigned in the inspector.");
            anyError = true;
        }
        if (nodePrefab == null)
        {
            Debug.LogError("SkillTreeView 'nodePrefab' is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Keyed by the concrete service type so the gold tree and the Reincarnate tree (two separate
        // SkillTreeView instances sharing this class) remember independent pan/zoom state.
        prefsKeyPrefix = $"SkillTreeView.{service.GetType().Name}.";

        scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            // The wheel now zooms (see OnScroll) instead of panning; dragging and the scrollbars still work.
            scrollRect.scrollSensitivity = 0f;
        }

        Build();
        RestoreView();

        service.OnTreeChanged += RefreshAll;
        Loc.OnLanguageChanged += RefreshAll;
        if (wallet != null)
        {
            wallet.OnCoinsChanged += HandleCoinsChanged;
        }

        Debug.Log($"[SkillTreeView] Start finished. builtTree: {(builtTree != null ? builtTree.name : "null")}, service.Tree: {(service.Tree != null ? service.Tree.name : "null")}");
        RefreshAll();
    }

    private void OnEnable()
    {
        if (built)
        {
            CenterOnRootNode();
        }
    }

    private void OnDestroy()
    {
        if (service != null)
        {
            service.OnTreeChanged -= RefreshAll;
        }
        Loc.OnLanguageChanged -= RefreshAll;
        if (wallet != null)
        {
            wallet.OnCoinsChanged -= HandleCoinsChanged;
        }
        foreach (SkillNodeView view in views.Values)
        {
            if (view != null)
            {
                view.HoverEntered -= HandleHoverEntered;
                view.HoverExited -= HandleHoverExited;
            }
        }

        // Persist whatever zoom/position the player left the tree at. Guarded on 'built' so an errored-out view never writes bogus zeros.
        if (built)
        {
            SaveView();
            PlayerPrefs.Save();
        }
    }

    private void Build()
    {
        if (built)
        {
            if (builtTree == service.Tree)
            {
                return;
            }
            built = false;
        }

        if (service.Tree == null)
        {
            Debug.Log("[SkillTreeView] Build aborted: service.Tree is null.");
            return;
        }
        
        Debug.Log($"[SkillTreeView] Building tree: {service.Tree.name}");
        
        // Wipe any existing children, including Editor-time preview nodes baked into the prefab
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        views.Clear();
        connectors.Clear();
        
        builtTree = service.Tree;

        IReadOnlyList<SkillNode> nodes = service.Tree.Nodes;

        FitContentToTree(nodes);

        // Connectors first so they render behind the nodes. Links are symmetric and may be authored on
        // both ends (a node lists its neighbor and the neighbor lists it back), so dedupe by unordered
        // id-pair to avoid drawing two overlapping lines for one edge.
        if (connectorPrefab != null)
        {
            var drawnEdges = new HashSet<string>();
            foreach (SkillNode node in nodes)
            {
                foreach (string prereqId in node.prereqs)
                {
                    SkillNode prereq = service.Tree.GetById(prereqId);
                    if (prereq == null)
                    {
                        continue;
                    }

                    string edgeKey = string.CompareOrdinal(prereq.id, node.id) < 0
                        ? prereq.id + " " + node.id
                        : node.id + " " + prereq.id;
                    if (drawnEdges.Add(edgeKey))
                    {
                        CreateConnector(prereq, node);
                    }
                }
            }
        }

        foreach (SkillNode node in nodes)
        {
            SkillNodeView view = Instantiate(nodePrefab, content);
            RectTransform rect = view.GetComponent<RectTransform>();
            SetTopLeftAnchor(rect);
            rect.anchoredPosition = GridToLocal(node);
            view.Bind(node, service, HandlePurchase);
            view.HoverEntered += HandleHoverEntered;
            view.HoverExited += HandleHoverExited;
            views[node.id] = view;
        }

        built = true;
    }

    /// <summary>
    ///     Forces a child's anchor to match content's top-left (0, 1) pivot so its anchoredPosition
    ///     reference point stays fixed at content's top-left corner regardless of the prefab's own
    ///     authored anchor (commonly the center default) or content's size. Leaves pivot untouched so
    ///     prefabs that rely on a specific pivot (e.g. the connector's left-center rotation pivot) keep it.
    /// </summary>
    public static void SetTopLeftAnchor(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
    }

    /// <summary>
    ///     Forces the enclosing ScrollRect (if any) to open showing content's top-left corner (where the
    ///     root nodes sit), rather than wherever a Scrollbar's leftover serialized value would otherwise
    ///     snap it to on enable. The default view for a tree with no remembered position (see
    ///     <see cref="RestoreView" />).
    /// </summary>
    private void ScrollToTopLeft()
    {
        if (scrollRect != null)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    ///     Applies the remembered zoom level for this tree (or defaults to 1.0) and centers the view on the root node.
    /// </summary>
    private void RestoreView()
    {
        if (PlayerPrefs.HasKey(prefsKeyPrefix + "zoom"))
        {
            zoom = Mathf.Clamp(PlayerPrefs.GetFloat(prefsKeyPrefix + "zoom", 1f), minZoom, maxZoom);
            targetZoom = zoom;
            content.localScale = new Vector3(zoom, zoom, 1f);
        }

        CenterOnRootNode();
    }

    /// <summary>
    ///     Returns the primary root node for this tree — either the first node explicitly marked with
    ///     <see cref="SkillNode.isRoot"/> in the CSV, or falls back to the first node in the tree.
    /// </summary>
    public SkillNode GetRootNode()
    {
        if (service == null || service.Tree == null || service.Tree.Nodes == null || service.Tree.Nodes.Count == 0)
        {
            return null;
        }

        IReadOnlyList<SkillNode> nodes = service.Tree.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].isRoot)
            {
                return nodes[i];
            }
        }
        return nodes[0];
    }

    /// <summary>
    ///     Centers <see cref="content"/> on the tree's root node immediately.
    /// </summary>
    public void CenterOnRootNode()
    {
        if (!built || anyError || content == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        SkillNode root = GetRootNode();
        if (root != null)
        {
            if (panRoutine != null)
            {
                StopCoroutine(panRoutine);
                panRoutine = null;
            }

            Vector2 target = CenterAnchoredPositionFor(GridToLocal(root));
            content.anchoredPosition = target;
            ClampContentPosition();
        }
        else
        {
            ScrollToTopLeft();
        }
    }

    private void SaveView()
    {
        PlayerPrefs.SetFloat(prefsKeyPrefix + "zoom", zoom);
        PlayerPrefs.SetFloat(prefsKeyPrefix + "x", content.anchoredPosition.x);
        PlayerPrefs.SetFloat(prefsKeyPrefix + "y", content.anchoredPosition.y);
    }

    /// <summary>
    ///     Keeps <see cref="content" /> from scrolling past its own edges once scaled, using content's
    ///     top-left anchor/pivot (see <see cref="FitContentToTree" />): anchoredPosition.x in
    ///     [-(scaledWidth - viewportWidth), 0], anchoredPosition.y in [0, scaledHeight - viewportHeight].
    ///     A tree smaller than the viewport (fully zoomed out) pins to the top-left corner.
    /// </summary>
    private void ClampContentPosition()
    {
        RectTransform viewport = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport : content.parent as RectTransform;
        if (viewport == null)
        {
            return;
        }

        Vector2 scaledSize = content.rect.size * zoom;
        Vector2 viewportSize = viewport.rect.size;
        float maxX = Mathf.Max(0f, scaledSize.x - viewportSize.x);
        float maxY = Mathf.Max(0f, scaledSize.y - viewportSize.y);

        Vector2 pos = content.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -maxX, 0f);
        pos.y = Mathf.Clamp(pos.y, 0f, maxY);
        content.anchoredPosition = pos;
    }

    /// <summary>
    ///     Mouse-wheel zoom. Only nudges <see cref="targetZoom" /> and records the cursor's content point
    ///     as the zoom pivot; <see cref="Update" /> eases <see cref="zoom" /> toward it every frame. Deriving
    ///     the required anchoredPosition shift only needs the point in content's own (scale-independent)
    ///     local space — <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle" /> already
    ///     divides out content's current scale — so this works regardless of the parent ScrollRect/viewport's
    ///     own anchor setup.
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        if (!built || anyError)
        {
            return;
        }

        float scrollY = eventData.scrollDelta.y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            return;
        }

        float newTargetZoom = Mathf.Clamp(targetZoom + scrollY * zoomStep, minZoom, maxZoom);
        if (Mathf.Approximately(newTargetZoom, targetZoom))
        {
            return;
        }

        // Re-pinned on every scroll tick so the point currently under the cursor stays fixed on screen
        // as the eased zoom keeps chasing a moving target.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, eventData.position, EventCamera(), out zoomPivotLocalPoint);
        targetZoom = newTargetZoom;
    }

    private void Update()
    {
        if (!built || anyError || Mathf.Approximately(zoom, targetZoom))
        {
            return;
        }

        float oldZoom = zoom;
        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-zoomSmoothSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(targetZoom - zoom) < 0.001f)
        {
            zoom = targetZoom;
        }

        content.anchoredPosition -= zoomPivotLocalPoint * (zoom - oldZoom);
        content.localScale = new Vector3(zoom, zoom, 1f);

        ClampContentPosition();
        if (Mathf.Approximately(zoom, targetZoom))
        {
            SaveView();
        }
    }

    private Camera EventCamera()
    {
        if (canvas == null)
        {
            canvas = content.GetComponentInParent<Canvas>();
        }
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    /// <summary>
    ///     Smoothly pans so <paramref name="node" /> ends up centered in the viewport — called right after
    ///     a successful purchase. Cancels any pan already in flight (e.g. a second quick purchase) rather
    ///     than fighting it.
    /// </summary>
    private void CenterOnNode(SkillNode node)
    {
        if (scrollRect == null)
        {
            return;
        }

        Vector2 target = CenterAnchoredPositionFor(GridToLocal(node));
        if (panRoutine != null)
        {
            StopCoroutine(panRoutine);
        }
        panRoutine = StartCoroutine(PanTo(target));
    }

    /// <summary>
    ///     The anchoredPosition that puts content-local point <paramref name="contentLocalPoint" /> at the
    ///     viewport's center, at the current zoom.
    /// </summary>
    private Vector2 CenterAnchoredPositionFor(Vector2 contentLocalPoint)
    {
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : content.parent as RectTransform;
        Vector2 half = viewport != null ? viewport.rect.size * 0.5f : Vector2.zero;
        return new Vector2(half.x, -half.y) - contentLocalPoint * zoom;
    }

    private IEnumerator PanTo(Vector2 target)
    {
        Vector2 start = content.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < centerOnUnlockDuration)
        {
            // Unscaled so the pan still plays if something froze Time.timeScale (e.g. a gate-defense loss).
            elapsed += Time.unscaledDeltaTime;
            float t = centerOnUnlockDuration > 0f ? Mathf.Clamp01(elapsed / centerOnUnlockDuration) : 1f;
            t = 1f - (1f - t) * (1f - t); // ease-out
            content.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        content.anchoredPosition = target;
        ClampContentPosition();
        SaveView();
        panRoutine = null;
    }

    private Vector2 GridToLocal(SkillNode node)
    {
        return GridToLocal(node.x, node.y, spacing, treeOffset);
    }

    /// <summary>
    ///     Converts authored (x, y) grid coordinates to a content-local anchored position. y grows
    ///     downward in tree terms, so it is negated for UI space (up is positive). Static so the
    ///     Scene-view skill tree editor lays out its preview with the exact same math.
    /// </summary>
    public static Vector2 GridToLocal(float x, float y, float spacing, Vector2 treeOffset)
    {
        return new Vector2(x * spacing, -y * spacing) + treeOffset;
    }

    /// <summary>
    ///     The <see cref="FitContentToTree" /> math: given every node's grid coordinates, returns the
    ///     treeOffset that shifts them into content's [0, width] x [-height, 0] rect and the content size
    ///     that encloses them (plus one node plus padding on every side).
    /// </summary>
    public static (Vector2 offset, Vector2 size) ComputeContentFit(IEnumerable<Vector2> gridCoords, float spacing, Vector2 nodeSize, float padding)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        foreach (Vector2 coord in gridCoords)
        {
            Vector2 pos = new Vector2(coord.x * spacing, -coord.y * spacing);
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
            any = true;
        }
        if (!any)
        {
            return (Vector2.zero, Vector2.zero);
        }

        Vector2 offset = new Vector2(-minX + nodeSize.x * 0.5f + padding, -maxY - nodeSize.y * 0.5f - padding);
        Vector2 size = new Vector2(maxX - minX + nodeSize.x + padding * 2f, maxY - minY + nodeSize.y + padding * 2f);
        return (offset, size);
    }

    /// <summary>
    ///     Stretches/rotates a connector line between two content-local points: pivot on 'a', length =
    ///     distance, authored thickness kept. Static so the Scene-view editor places its preview
    ///     connectors identically.
    /// </summary>
    public static void PlaceConnector(RectTransform line, Vector2 a, Vector2 b)
    {
        // The placement puts the line's pivot on 'a' and rotates it toward 'b', so the pivot must be
        // left-center regardless of what the prefab was authored with.
        line.pivot = new Vector2(0f, 0.5f);
        Vector2 delta = b - a;
        line.anchoredPosition = a;
        line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    /// <summary>
    ///     Sizes <see cref="content" /> to the authored tree's actual extent (plus one node, plus
    ///     <see cref="contentPadding" /> of breathing room on every side) instead of an arbitrarily large
    ///     fixed rect, so the ScrollRect's drag/scroll limits match the tree. Anchors/pivots content to
    ///     top-left and shifts every node via <see cref="treeOffset" /> so the (possibly negative) grid
    ///     coordinates land inside content's [0, width] x [-height, 0] rect.
    /// </summary>
    private void FitContentToTree(IReadOnlyList<SkillNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        Vector2 nodeSize = nodePrefab.GetComponent<RectTransform>().sizeDelta;

        var coords = new List<Vector2>(nodes.Count);
        foreach (SkillNode node in nodes)
        {
            coords.Add(new Vector2(node.x, node.y));
        }
        (Vector2 offset, Vector2 size) = ComputeContentFit(coords, spacing, nodeSize, contentPadding);
        treeOffset = offset;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = size;
    }

    private void CreateConnector(SkillNode from, SkillNode to)
    {
        Vector2 a = GridToLocal(from);
        Vector2 b = GridToLocal(to);

        RectTransform line = Instantiate(connectorPrefab, content);
        SetTopLeftAnchor(line);
        PlaceConnector(line, a, b);
        connectors.Add((line, from, to));
    }

    private void HandlePurchase(string id)
    {
        // OnTreeChanged from the service drives RefreshAll on success.
        if (service.TryPurchase(id) && views.TryGetValue(id, out SkillNodeView view))
        {
            view.PlayPurchaseFeedback();
        }
    }

    private void HandleHoverEntered(SkillNodeView view)
    {
        hoveredView = view;
        if (tooltip != null)
        {
            tooltip.Show(view.Node, service);
        }
    }

    private void HandleHoverExited(SkillNodeView view)
    {
        if (hoveredView != view)
        {
            return;
        }
        hoveredView = null;
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    private bool IsNodeVisible(SkillNode node) => service.IsRevealed(node) || service.IsTeased(node);

    private void HandleCoinsChanged(int _) => RefreshAll();

    private void RefreshAll()
    {
        Debug.Log($"[SkillTreeView] RefreshAll called. built: {built}, builtTree: {(builtTree != null ? builtTree.name : "null")}, service.Tree: {(service.Tree != null ? service.Tree.name : "null")}");
        if (builtTree != service.Tree)
        {
            Debug.Log($"[SkillTreeView] Tree mismatch detected in RefreshAll! Rebuilding.");
            Build();
            RestoreView();
        }

        foreach (SkillNodeView view in views.Values)
        {
            view.Refresh();
        }

        // A connector only shows once both of its endpoint nodes are visible (revealed or teased) —
        // otherwise lines to hidden nodes would map out the whole tree from the start.
        foreach ((RectTransform line, SkillNode from, SkillNode to) in connectors)
        {
            bool bothVisible = IsNodeVisible(from) && IsNodeVisible(to);
            if (line != null && line.gameObject.activeSelf != bothVisible)
            {
                line.gameObject.SetActive(bothVisible);
            }
        }

        UpdateGold();

        // Keep an open tooltip current (e.g. cost flips to "Owned" the moment the hovered node is bought,
        // and family ladder prices climb when a sibling is purchased).
        if (hoveredView != null && tooltip != null && hoveredView.isActiveAndEnabled)
        {
            tooltip.Show(hoveredView.Node, service);
        }
    }

    private void UpdateGold()
    {
        if (goldText != null && wallet != null)
        {
            goldText.text = Loc.Format("skill.gold_readout", wallet.Coins);
        }
    }

    // ---- Gamepad API (driven by SkillTreePadController) -------------------------------------------

    /// <summary>The service rendering this tree — the pad controller reads reveal/purchase state through it.</summary>
    public ISkillTreeService Service => service;

    /// <summary>True once the node views exist and pan/zoom state is live.</summary>
    public bool IsBuilt => built && !anyError;

    /// <summary>Every node view currently shown (revealed or teased) — the pad's selectable set.</summary>
    public IEnumerable<SkillNodeView> VisibleNodeViews
    {
        get
        {
            foreach (SkillNodeView view in views.Values)
            {
                if (view != null && view.gameObject.activeSelf)
                {
                    yield return view;
                }
            }
        }
    }

    /// <summary>
    ///     Pans the tree by a content-space delta (right stick), clamped to the tree's edges — the
    ///     pad twin of the ScrollRect drag. Cancels an in-flight centering pan rather than fighting it.
    /// </summary>
    public void PanBy(Vector2 delta)
    {
        if (!IsBuilt)
        {
            return;
        }
        if (panRoutine != null)
        {
            StopCoroutine(panRoutine);
            panRoutine = null;
        }
        content.anchoredPosition += delta;
        ClampContentPosition();
        SaveView();
    }

    /// <summary>
    ///     Nudges the zoom target (triggers), pivoting on the viewport center — the pad twin of
    ///     <see cref="OnScroll" />'s cursor-pivot zoom; the same eased <see cref="Update" /> pipeline
    ///     carries it out.
    /// </summary>
    public void ZoomBy(float delta)
    {
        if (!IsBuilt || Mathf.Approximately(delta, 0f))
        {
            return;
        }

        float newTargetZoom = Mathf.Clamp(targetZoom + delta, minZoom, maxZoom);
        if (Mathf.Approximately(newTargetZoom, targetZoom))
        {
            return;
        }

        RectTransform viewport = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport : content.parent as RectTransform;
        if (viewport == null)
        {
            return;
        }
        Vector2 viewportCenterScreen = RectTransformUtility.WorldToScreenPoint(EventCamera(), viewport.TransformPoint(viewport.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, viewportCenterScreen, EventCamera(), out zoomPivotLocalPoint);
        targetZoom = newTargetZoom;
    }

    /// <summary>Whether a node view currently sits (at least partly) inside the viewport.</summary>
    public bool IsNodeInViewport(SkillNodeView view)
    {
        RectTransform viewport = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport : content.parent as RectTransform;
        if (viewport == null || view == null)
        {
            return true;
        }
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, (RectTransform)view.transform);
        Rect rect = viewport.rect;
        return bounds.max.x > rect.xMin && bounds.min.x < rect.xMax && bounds.max.y > rect.yMin && bounds.min.y < rect.yMax;
    }

    /// <summary>Eases the view to center the node — public so pad selection can follow off-screen moves.</summary>
    public void CenterOn(SkillNode node)
    {
        CenterOnNode(node);
    }

    /// <summary>Buys a node through the same path as a click (feedback + centering on success).</summary>
    public void PurchaseNode(string id)
    {
        HandlePurchase(id);
    }

    /// <summary>Shows the tooltip pinned to a node view (pad selection), or hides it when view is null.</summary>
    public void ShowTooltipFor(SkillNodeView view)
    {
        if (tooltip == null)
        {
            return;
        }
        if (view == null)
        {
            tooltip.Hide();
        }
        else
        {
            tooltip.ShowAtRect(view.Node, service, (RectTransform)view.transform);
        }
    }
}
