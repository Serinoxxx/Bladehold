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

    [Header("Navigation Buttons")]
    [SerializeField] private Button returnToMetaButton;
    [SerializeField] private string metaAreaSceneName = "Bladehold Meta Area Scene";

    [Header("Path Visual Styling")]
    [SerializeField] private Color pathLockedColor = new Color(0.3f, 0.3f, 0.35f, 0.4f);
    [SerializeField] private Color pathAvailableColor = new Color(1f, 0.85f, 0.25f, 0.95f);
    [SerializeField] private Color pathCompletedColor = new Color(0.35f, 0.75f, 0.45f, 0.75f);
    [SerializeField] private float pathThickness = 4f;

    private readonly Dictionary<string, CampaignNodeButtonUI> spawnedButtons = new Dictionary<string, CampaignNodeButtonUI>();
    private readonly List<GameObject> spawnedPaths = new List<GameObject>();

    private void Awake()
    {
        if (returnToMetaButton != null)
        {
            returnToMetaButton.onClick.AddListener(HandleReturnToMeta);
        }
    }

    public CampaignNodeButtonUI NodeButtonPrefab { get => nodeButtonPrefab; set => nodeButtonPrefab = value; }
    public GameObject PathLinePrefab { get => pathLinePrefab; set => pathLinePrefab = value; }
    public CampaignGraphSO CampaignGraph { get => campaignGraph; set => campaignGraph = value; }

    /// <summary>
    ///     Wires references procedurally if configured outside prefab serialization.
    /// </summary>
    public void InitializeReferences(
        CampaignGraphSO graph,
        RectTransform nodesBox,
        RectTransform pathsBox,
        ScrollRect sRect,
        CampaignTooltipUI tooltip,
        TMP_Text titleTxt,
        TMP_Text goldTxt,
        TMP_Text bloodTxt,
        TMP_Text metalTxt,
        Button returnBtn,
        CampaignNodeButtonUI btnPrefab = null,
        GameObject linePrefab = null)
    {
        campaignGraph = graph;
        nodesContainer = nodesBox;
        pathsContainer = pathsBox;
        scrollRect = sRect;
        tooltipUI = tooltip;
        screenTitleText = titleTxt;
        inRunGoldText = goldTxt;
        goblinBloodText = bloodTxt;
        orcishMetalText = metalTxt;
        returnToMetaButton = returnBtn;
        if (btnPrefab != null) nodeButtonPrefab = btnPrefab;
        if (linePrefab != null) pathLinePrefab = linePrefab;

        if (returnToMetaButton != null)
        {
            returnToMetaButton.onClick.RemoveAllListeners();
            returnToMetaButton.onClick.AddListener(HandleReturnToMeta);
        }
    }

    private void Start()
    {
        // Ensure CampaignManager is initialized
        if (CampaignManager.Instance != null)
        {
            if (campaignGraph == null)
            {
                campaignGraph = CampaignManager.Instance.ActiveGraph;
            }
            else
            {
                CampaignManager.Instance.ActiveGraph = campaignGraph;
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
        }

        RefreshCurrencies();
        BuildMap();
    }

    private void OnDestroy()
    {
        if (CampaignManager.Instance != null)
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
        if (campaignGraph == null && CampaignManager.Instance != null)
        {
            campaignGraph = CampaignManager.Instance.ActiveGraph;
        }

        if (campaignGraph == null)
        {
            Debug.LogWarning("[CampaignMapUI] No CampaignGraphSO available to display.");
            return;
        }

        ClearSpawnedElements();

        List<CampaignNodeSO> allNodes = campaignGraph.allNodes;
        if (allNodes == null || allNodes.Count == 0)
        {
            campaignGraph.BuildDefaultGraph();
            allNodes = campaignGraph.allNodes;
        }

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
        if (CampaignManager.Instance == null) return CampaignNodeButtonUI.NodeVisualStatus.Locked;

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
        Transform parentTransform = nodesContainer != null ? nodesContainer : transform;

        CampaignNodeButtonUI btn;
        if (nodeButtonPrefab != null)
        {
            btn = Instantiate(nodeButtonPrefab, parentTransform);
        }
        else
        {
            // Runtime procedural fallback if prefab is not wired in Inspector
            btn = CreateProceduralNodeButton(parentTransform);
        }

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

        Transform pathParent = pathsContainer != null ? pathsContainer : (nodesContainer != null ? nodesContainer : transform);

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
        GameObject lineGo;
        if (pathLinePrefab != null)
        {
            lineGo = Instantiate(pathLinePrefab, parent);
        }
        else
        {
            lineGo = new GameObject("PathLine", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(parent, false);
        }

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
        if (node == null || CampaignManager.Instance == null) return;

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

    private void HandleReturnToMeta()
    {
        Time.timeScale = 1f;
        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(metaAreaSceneName);
        }
        else
        {
            SceneManager.LoadScene(metaAreaSceneName);
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

    private CampaignNodeButtonUI CreateProceduralNodeButton(Transform parent)
    {
        GameObject go = new GameObject("NodeButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CampaignNodeButtonUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(170f, 95f);

        Image bgImg = go.GetComponent<Image>();
        bgImg.color = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        Button btn = go.GetComponent<Button>();

        // 1. Border Frame
        GameObject borderGo = new GameObject("BorderFrame", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(go.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.sizeDelta = new Vector2(4f, 4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.35f, 0.35f, 0.40f, 0.8f);
        borderGo.transform.SetAsFirstSibling();

        // 2. Active Pulse Glow
        GameObject glowGo = new GameObject("ActiveGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(go.transform, false);
        RectTransform glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.sizeDelta = new Vector2(8f, 8f);
        Image glowImg = glowGo.GetComponent<Image>();
        glowImg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        glowGo.SetActive(false);

        // 3. Tier Label (Top)
        GameObject tierGo = new GameObject("TierText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tierGo.transform.SetParent(go.transform, false);
        RectTransform tierRt = tierGo.GetComponent<RectTransform>();
        tierRt.anchorMin = new Vector2(0f, 0.72f);
        tierRt.anchorMax = new Vector2(1f, 0.98f);
        tierRt.offsetMin = Vector2.zero;
        tierRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tierTmp = tierGo.GetComponent<TextMeshProUGUI>();
        tierTmp.fontSize = 11;
        tierTmp.alignment = TextAlignmentOptions.Center;
        tierTmp.fontStyle = FontStyles.Bold;
        tierTmp.color = new Color(0.75f, 0.85f, 1f, 0.85f);

        // 4. Title Label (Middle)
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(go.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.28f);
        titleRt.anchorMax = new Vector2(0.95f, 0.72f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 13;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.textWrappingMode = TextWrappingModes.Normal;
        titleTmp.color = Color.white;

        // 5. Captain Badge (Bottom)
        GameObject captGo = new GameObject("CaptainText", typeof(RectTransform), typeof(TextMeshProUGUI));
        captGo.transform.SetParent(go.transform, false);
        RectTransform captRt = captGo.GetComponent<RectTransform>();
        captRt.anchorMin = new Vector2(0f, 0.04f);
        captRt.anchorMax = new Vector2(1f, 0.28f);
        captRt.offsetMin = Vector2.zero;
        captRt.offsetMax = Vector2.zero;
        TextMeshProUGUI captTmp = captGo.GetComponent<TextMeshProUGUI>();
        captTmp.fontSize = 10;
        captTmp.alignment = TextAlignmentOptions.Center;
        captTmp.fontStyle = FontStyles.Italic;
        captTmp.color = new Color(1f, 0.45f, 0.35f, 0.95f);

        // 6. Lock Overlay
        GameObject lockGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
        lockGo.transform.SetParent(go.transform, false);
        RectTransform lockRt = lockGo.GetComponent<RectTransform>();
        lockRt.anchorMin = Vector2.zero;
        lockRt.anchorMax = Vector2.one;
        lockRt.sizeDelta = Vector2.zero;
        Image lockImg = lockGo.GetComponent<Image>();
        lockImg.color = new Color(0.05f, 0.05f, 0.08f, 0.65f);
        lockGo.SetActive(false);

        // 7. Completed Checkmark
        GameObject checkGo = new GameObject("CompletedBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        checkGo.transform.SetParent(go.transform, false);
        RectTransform checkRt = checkGo.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.75f, 0.65f);
        checkRt.anchorMax = new Vector2(0.98f, 0.95f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;
        TextMeshProUGUI checkTmp = checkGo.GetComponent<TextMeshProUGUI>();
        checkTmp.text = "[V]";
        checkTmp.fontSize = 12;
        checkTmp.fontStyle = FontStyles.Bold;
        checkTmp.color = new Color(0.35f, 0.9f, 0.45f);
        checkGo.SetActive(false);

        CampaignNodeButtonUI btnComp = go.GetComponent<CampaignNodeButtonUI>();
        btnComp.InitializeReferences(
            rt, btn, bgImg, borderImg, titleTmp, tierTmp, captTmp,
            lockGo, checkGo, glowGo);

        return btnComp;
    }
}
