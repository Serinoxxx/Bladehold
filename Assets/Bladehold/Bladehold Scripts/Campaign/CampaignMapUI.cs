using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Master controller for the Castle Campaign Overview Map screen.
///     Builds the multi-tier visual node graph, renders tactical route connections,
///     updates currency status, and directs deployment to selected sectors.
/// </summary>
public class CampaignMapUI : MonoBehaviour
{
    [Header("Graph & Containers")]
    [SerializeField] private CampaignGraphSO campaignGraph;
    [SerializeField] private RectTransform nodesContainer;
    [SerializeField] private RectTransform pathsContainer;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Prefabs")]
    [SerializeField] private CampaignNodeButtonUI nodeButtonPrefab;
    [SerializeField] private GameObject pathLinePrefab;

    [Header("Tooltip")]
    [SerializeField] private CampaignTooltipUI tooltipUI;

    [Header("Top Bar Currencies & Header")]
    [SerializeField] private TMP_Text screenTitleText;
    [SerializeField] private TMP_Text inRunGoldText;
    [SerializeField] private TMP_Text goblinBloodText;
    [SerializeField] private TMP_Text orcishMetalText;

    [Header("Path Visual Styling")]
    [SerializeField] private Color pathLockedColor = new Color(0.3f, 0.3f, 0.35f, 0.4f);
    [SerializeField] private Color pathAvailableColor = new Color(1f, 0.85f, 0.25f, 0.95f);
    [SerializeField] private Color pathCompletedColor = new Color(0.35f, 0.75f, 0.45f, 0.75f);
    [SerializeField] private float pathThickness = 4f;

    private readonly Dictionary<string, CampaignNodeButtonUI> spawnedButtons = new Dictionary<string, CampaignNodeButtonUI>();
    private readonly List<GameObject> spawnedPaths = new List<GameObject>();

    private bool anyError = false;

    private void Start()
    {
        if (nodeButtonPrefab == null)
        {
            Debug.LogError("[CampaignMapUI] Node button prefab is not assigned (CampaignNodeButton.prefab).");
            anyError = true;
        }
        if (pathLinePrefab == null)
        {
            Debug.LogError("[CampaignMapUI] Path line prefab is not assigned (CampaignPathLine.prefab).");
            anyError = true;
        }
        if (nodesContainer == null || pathsContainer == null)
        {
            Debug.LogError("[CampaignMapUI] Nodes and/or paths container is not assigned.");
            anyError = true;
        }

        if (campaignGraph == null)
        {
            campaignGraph = CampaignManager.Instance.ActiveGraph;
        }
        else
        {
            CampaignManager.Instance.ActiveGraph = campaignGraph;
        }

        if (campaignGraph == null || campaignGraph.allNodes == null || campaignGraph.allNodes.Count == 0)
        {
            Debug.LogError("[CampaignMapUI] No campaign graph (or an empty one) to display.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // If not currently a campaign run, start fresh
        if (!RunSession.IsCampaignRun)
        {
            CampaignManager.Instance.StartCampaignRun(campaignGraph);
        }
        else
        {
            CampaignManager.Instance.RestoreFromRunSession();
        }

        CampaignManager.Instance.OnCampaignStateChanged += RefreshMap;

        RefreshCurrencies();
        BuildMap();
    }

    private void OnDestroy()
    {
        if (CampaignManager.HasInstance)
        {
            CampaignManager.Instance.OnCampaignStateChanged -= RefreshMap;
        }
    }

    /// <summary>
    ///     Refreshes the currency counters at the top of the screen.
    /// </summary>
    public void RefreshCurrencies()
    {
        if (inRunGoldText != null)
        {
            inRunGoldText.text = $"{RunSession.InRunGold}g";
        }

        SaveData save = SaveSystem.Load();
        if (save != null)
        {
            if (goblinBloodText != null) goblinBloodText.text = $"{save.goblinBlood}";
            if (orcishMetalText != null) orcishMetalText.text = $"{save.orcishMetal}";
        }
    }

    /// <summary>
    ///     Constructs the full node graph and path connections on the overview canvas.
    /// </summary>
    public void BuildMap()
    {
        if (anyError)
        {
            return;
        }

        ClearSpawnedElements();

        List<CampaignNodeSO> allNodes = campaignGraph.allNodes;

        RectTransform firstAvailableNodeRect = null;

        // 1. Spawn Node Buttons
        for (int i = 0; i < allNodes.Count; i++)
        {
            CampaignNodeSO node = allNodes[i];
            if (node == null) continue;

            CampaignNodeButtonUI.NodeVisualStatus status = EvaluateNodeStatus(node);
            CampaignNodeButtonUI buttonInstance = SpawnNodeButton(node, status);
            if (buttonInstance != null)
            {
                spawnedButtons[node.nodeId] = buttonInstance;

                if (status == CampaignNodeButtonUI.NodeVisualStatus.Available && firstAvailableNodeRect == null)
                {
                    firstAvailableNodeRect = buttonInstance.GetComponent<RectTransform>();
                }
            }
        }

        // 2. Draw Forward Path Connections
        DrawAllPaths();

        // 3. Scroll to focus on active tier
        if (firstAvailableNodeRect != null && scrollRect != null && nodesContainer != null)
        {
            FocusOnNode(firstAvailableNodeRect);
        }
    }

    /// <summary>
    ///     Updates the visual status of existing nodes without respawning.
    /// </summary>
    public void RefreshMap()
    {
        if (anyError)
        {
            return;
        }

        RefreshCurrencies();

        foreach (var kvp in spawnedButtons)
        {
            string nodeId = kvp.Key;
            CampaignNodeButtonUI button = kvp.Value;
            if (button != null && button.NodeData != null)
            {
                CampaignNodeButtonUI.NodeVisualStatus status = EvaluateNodeStatus(button.NodeData);
                button.Setup(button.NodeData, status, HandleNodeSelected, HandleNodeHovered, HandleNodeHoverExited);
            }
        }

        // Re-draw path connections with updated status colors
        ClearPaths();
        DrawAllPaths();
    }

    private CampaignNodeButtonUI.NodeVisualStatus EvaluateNodeStatus(CampaignNodeSO node)
    {
        if (CampaignManager.Instance.CompletedNodeIds.Contains(node.nodeId))
        {
            return CampaignNodeButtonUI.NodeVisualStatus.Completed;
        }

        if (CampaignManager.Instance.AvailableNodeIds.Contains(node.nodeId))
        {
            return CampaignNodeButtonUI.NodeVisualStatus.Available;
        }

        return CampaignNodeButtonUI.NodeVisualStatus.Locked;
    }

    private CampaignNodeButtonUI SpawnNodeButton(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status)
    {
        CampaignNodeButtonUI btn = Instantiate(nodeButtonPrefab, nodesContainer);

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = node.mapPosition;
        }

        btn.Setup(node, status, HandleNodeSelected, HandleNodeHovered, HandleNodeHoverExited);
        return btn;
    }

    private void DrawAllPaths()
    {
        if (campaignGraph == null || campaignGraph.allNodes == null) return;

        Transform pathParent = pathsContainer;

        for (int i = 0; i < campaignGraph.allNodes.Count; i++)
        {
            CampaignNodeSO fromNode = campaignGraph.allNodes[i];
            if (fromNode == null || fromNode.nextNodes == null) continue;

            if (!spawnedButtons.TryGetValue(fromNode.nodeId, out CampaignNodeButtonUI fromBtn) || fromBtn == null)
                continue;

            RectTransform fromRect = fromBtn.GetComponent<RectTransform>();
            Vector2 fromPos = fromRect.anchoredPosition;

            bool fromCompleted = (fromBtn.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Completed);

            for (int n = 0; n < fromNode.nextNodes.Count; n++)
            {
                CampaignNodeSO toNode = fromNode.nextNodes[n];
                if (toNode == null) continue;

                if (!spawnedButtons.TryGetValue(toNode.nodeId, out CampaignNodeButtonUI toBtn) || toBtn == null)
                    continue;

                RectTransform toRect = toBtn.GetComponent<RectTransform>();
                Vector2 toPos = toRect.anchoredPosition;

                Color pathColor = pathLockedColor;
                if (fromCompleted && toBtn.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Available)
                {
                    pathColor = pathAvailableColor;
                }
                else if (fromCompleted && toBtn.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Completed)
                {
                    pathColor = pathCompletedColor;
                }

                GameObject lineGo = CreatePathLine(pathParent, fromPos, toPos, pathColor);
                if (lineGo != null)
                {
                    spawnedPaths.Add(lineGo);
                }
            }
        }
    }

    private GameObject CreatePathLine(Transform parent, Vector2 from, Vector2 to, Color color)
    {
        GameObject lineGo = Instantiate(pathLinePrefab, parent);

        RectTransform rt = lineGo.GetComponent<RectTransform>();
        Image img = lineGo.GetComponent<Image>();

        if (img != null)
        {
            img.color = color;
        }

        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);

        rt.sizeDelta = new Vector2(distance, pathThickness);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        lineGo.transform.SetAsFirstSibling();
        return lineGo;
    }

    private void HandleNodeSelected(CampaignNodeSO node)
    {
        if (node == null) return;

        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }

        CampaignManager.Instance.SelectNode(node);
    }

    private void HandleNodeHovered(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status, Vector2 screenPos)
    {
        if (tooltipUI != null)
        {
            tooltipUI.Show(node, status, screenPos);
        }
    }

    private void HandleNodeHoverExited()
    {
        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }
    }

    private void FocusOnNode(RectTransform nodeRect)
    {
        if (scrollRect == null || nodesContainer == null || nodeRect == null) return;

        float containerWidth = nodesContainer.rect.width;
        if (containerWidth <= 0) return;

        float normalizedX = Mathf.Clamp01(nodeRect.anchoredPosition.x / (containerWidth + 0.001f));
        scrollRect.horizontalNormalizedPosition = normalizedX;
    }

    private void ClearSpawnedElements()
    {
        if (nodesContainer != null)
        {
            for (int i = nodesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = nodesContainer.GetChild(i);
                if (child != null)
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }
        spawnedButtons.Clear();

        ClearPaths();
    }

    private void ClearPaths()
    {
        if (pathsContainer != null)
        {
            for (int i = pathsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = pathsContainer.GetChild(i);
                if (child != null)
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }
        spawnedPaths.Clear();
    }
}
