using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Builds and maintains the visual skill tree. Instantiates a <see cref="SkillNodeView" /> per node at
///     its authored (x, y) coordinates inside <see cref="content" /> (place that under a ScrollRect to pan a
///     large tree), draws connector images from each node to its prerequisites, and refreshes everything
///     whenever the tree changes or the player's gold changes. Clicking an available node buys it.
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

    private readonly Dictionary<string, SkillNodeView> views = new Dictionary<string, SkillNodeView>();
    private bool built = false;
    private bool anyError = false;

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
    }

    private void Build()
    {
        if (built || service.Tree == null)
        {
            return;
        }

        IReadOnlyList<SkillNode> nodes = service.Tree.Nodes;

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
            view.GetComponent<RectTransform>().anchoredPosition = GridToLocal(node);
            view.Bind(node, service, HandlePurchase);
            views[node.id] = view;
        }

        built = true;
    }

    private Vector2 GridToLocal(SkillNode node)
    {
        // y grows downward in tree terms, so negate for UI space (up is positive).
        return new Vector2(node.x * spacing, -node.y * spacing);
    }

    private void CreateConnector(SkillNode from, SkillNode to)
    {
        Vector2 a = GridToLocal(from);
        Vector2 b = GridToLocal(to);

        RectTransform line = Instantiate(connectorPrefab, content);
        Vector2 delta = b - a;
        line.anchoredPosition = a;
        line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void HandlePurchase(string id)
    {
        // OnTreeChanged from the service drives RefreshAll on success.
        service.TryPurchase(id);
    }

    private void HandleCoinsChanged(int _) => RefreshAll();

    private void RefreshAll()
    {
        foreach (SkillNodeView view in views.Values)
        {
            view.Refresh();
        }
        UpdateGold();
    }

    private void UpdateGold()
    {
        if (goldText != null && wallet != null)
        {
            goldText.text = $"Gold: {wallet.Coins}";
        }
    }
}
