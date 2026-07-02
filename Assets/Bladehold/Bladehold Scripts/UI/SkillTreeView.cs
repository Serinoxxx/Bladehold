using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Builds and maintains the visual skill tree. Instantiates a <see cref="SkillNodeView" /> per node at
///     its authored (x, y) coordinates inside <see cref="content" /> (place that under a ScrollRect to pan a
///     large tree), draws connector images from each node to its prerequisites, and refreshes everything
///     whenever the tree changes or the player's gold changes. Clicking an available node buys it; hovering
///     a node drives the optional cursor-following <see cref="SkillTooltip" />.
///
///     This is the "upgrade screen" content: drop it on the death-screen canvas so it appears with the
///     death screen on player death.
/// </summary>
public class SkillTreeView : MonoBehaviour
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

    [Header("Optional gold readout")]
    [SerializeField] private TMP_Text goldText;

    [Header("Optional tooltip")]
    [Tooltip("Cursor-following tooltip shown while hovering a node. Leave unassigned to disable.")]
    [SerializeField] private SkillTooltip tooltip;

    private readonly Dictionary<string, SkillNodeView> views = new Dictionary<string, SkillNodeView>();
    private readonly List<(RectTransform line, SkillNode from, SkillNode to)> connectors = new List<(RectTransform, SkillNode, SkillNode)>();
    private SkillNodeView hoveredView;
    private bool built = false;
    private bool anyError = false;
    private Vector2 treeOffset;

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

        Build();
        ScrollToTopLeft();

        service.OnTreeChanged += RefreshAll;
        if (wallet != null)
        {
            wallet.OnCoinsChanged += HandleCoinsChanged;
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (service != null)
        {
            service.OnTreeChanged -= RefreshAll;
        }
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
    }

    private void Build()
    {
        if (built || service.Tree == null)
        {
            return;
        }

        IReadOnlyList<SkillNode> nodes = service.Tree.Nodes;

        FitContentToTree(nodes);

        // Connectors first so they render behind the nodes.
        if (connectorPrefab != null)
        {
            foreach (SkillNode node in nodes)
            {
                foreach (string prereqId in node.prereqs)
                {
                    SkillNode prereq = service.Tree.GetById(prereqId);
                    if (prereq != null)
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
    private static void SetTopLeftAnchor(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
    }

    /// <summary>
    ///     Forces the enclosing ScrollRect (if any) to open showing content's top-left corner (where the
    ///     root nodes sit), rather than wherever a Scrollbar's leftover serialized value would otherwise
    ///     snap it to on enable.
    /// </summary>
    private void ScrollToTopLeft()
    {
        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private Vector2 GridToLocal(SkillNode node)
    {
        // y grows downward in tree terms, so negate for UI space (up is positive).
        return new Vector2(node.x * spacing, -node.y * spacing) + treeOffset;
    }

    /// <summary>
    ///     Sizes <see cref="content" /> to the authored tree's actual extent (plus one node of padding)
    ///     instead of an arbitrarily large fixed rect, so the ScrollRect's drag/scroll limits match the
    ///     tree. Anchors/pivots content to top-left and shifts every node via <see cref="treeOffset" /> so
    ///     the (possibly negative) grid coordinates land inside content's [0, width] x [-height, 0] rect.
    /// </summary>
    private void FitContentToTree(IReadOnlyList<SkillNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        Vector2 nodeSize = nodePrefab.GetComponent<RectTransform>().sizeDelta;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (SkillNode node in nodes)
        {
            Vector2 pos = new Vector2(node.x * spacing, -node.y * spacing);
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }

        treeOffset = new Vector2(-minX + nodeSize.x * 0.5f, -maxY - nodeSize.y * 0.5f);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(maxX - minX + nodeSize.x, maxY - minY + nodeSize.y);
    }

    private void CreateConnector(SkillNode from, SkillNode to)
    {
        Vector2 a = GridToLocal(from);
        Vector2 b = GridToLocal(to);

        RectTransform line = Instantiate(connectorPrefab, content);
        SetTopLeftAnchor(line);
        // The placement below puts the line's pivot on 'from' and rotates it toward 'to', so the pivot
        // must be left-center regardless of what the prefab was authored with.
        line.pivot = new Vector2(0f, 0.5f);
        Vector2 delta = b - a;
        line.anchoredPosition = a;
        line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
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
            tooltip.Show(view.Node, service.IsPurchased(view.Node.id));
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

        // Keep an open tooltip current (e.g. cost flips to "Owned" the moment the hovered node is bought).
        if (hoveredView != null && tooltip != null && hoveredView.isActiveAndEnabled)
        {
            tooltip.Show(hoveredView.Node, service.IsPurchased(hoveredView.Node.id));
        }
    }

    private void UpdateGold()
    {
        if (goldText != null && wallet != null)
        {
            goldText.text = $"Gold: {wallet.Coins}";
        }
    }
}
