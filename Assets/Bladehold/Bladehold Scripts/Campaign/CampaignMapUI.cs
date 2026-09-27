using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Demo")]
    [Tooltip("Thanks for playing / Wishlist panel, shown once the demo's cutoff tier is cleared. Required while DemoConfig has the demo enabled.")]
    [SerializeField] private DemoEndScreenUI demoEndScreen;

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

    [Header("Keyboard / Gamepad Navigation")]
    [Tooltip("Left-stick deflection that counts as a direction press.")]
    [SerializeField] private float stickThreshold = 0.6f;
    [Tooltip("Seconds between repeated moves while a stick or key is held.")]
    [SerializeField] private float navRepeatDelay = 0.3f;
    [Tooltip("How much sideways offset counts against a candidate node versus distance along the pressed direction.")]
    [SerializeField] private float navPerpendicularWeight = 2f;

    private const string CursorOwner = "CampaignMap";

    private readonly Dictionary<string, CampaignNodeButtonUI> spawnedButtons = new Dictionary<string, CampaignNodeButtonUI>();
    private readonly List<GameObject> spawnedPaths = new List<GameObject>();

    private CampaignNodeButtonUI focusedButton;
    private Vector2 heldDirection;
    private float nextRepeatTime;
    private bool demoEndShown;
    private bool deploying;

    private bool anyError = false;

    private void Start()
    {
        // The map is a pure UI screen: free the cursor (gameplay scenes leave it locked) so it can click and drag.
        CursorLockManager.SetUnlock(CursorOwner, true);

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

        if (CampaignManager.Instance.IsDemoEndReached)
        {
            ShowDemoEnd();
        }
        else if (InputDeviceWatcher.GamepadActive)
        {
            FocusFirstAvailable();
        }
    }

    private void Update()
    {
        if (anyError || demoEndShown || deploying || DevConsole.IsVisible)
        {
            return;
        }

        Vector2 direction = ReadNavigationDirection();
        if (direction != Vector2.zero)
        {
            if (focusedButton == null)
            {
                FocusFirstAvailable();
            }
            else
            {
                MoveFocus(direction);
            }
        }

        if (focusedButton != null && SubmitPressed())
        {
            if (focusedButton.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Available)
            {
                HandleNodeSelected(focusedButton.NodeData);
            }
        }
    }

    /// <summary>
    ///     WASD / arrow keys / d-pad give one step per press; holding (or the left stick) repeats every
    ///     <see cref="navRepeatDelay" />. Read straight from the devices: nodes aren't EventSystem-selected,
    ///     because locked nodes are non-interactable Buttons and could never take a selection.
    /// </summary>
    private Vector2 ReadNavigationDirection()
    {
        Vector2 raw = Vector2.zero;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) raw.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) raw.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) raw.y += 1f;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 dpad = pad.dpad.ReadValue();
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.magnitude < stickThreshold) stick = Vector2.zero;
            raw += dpad + stick;
        }

        // Snap to one of four directions so a diagonal stick doesn't jitter between rows.
        Vector2 snapped = Vector2.zero;
        if (Mathf.Abs(raw.x) > 0.01f || Mathf.Abs(raw.y) > 0.01f)
        {
            snapped = Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
                ? new Vector2(Mathf.Sign(raw.x), 0f)
                : new Vector2(0f, Mathf.Sign(raw.y));
        }

        if (snapped == Vector2.zero)
        {
            heldDirection = Vector2.zero;
            return Vector2.zero;
        }

        if (snapped != heldDirection)
        {
            heldDirection = snapped;
            nextRepeatTime = Time.unscaledTime + navRepeatDelay;
            return snapped;
        }

        if (Time.unscaledTime >= nextRepeatTime)
        {
            nextRepeatTime = Time.unscaledTime + navRepeatDelay;
            return snapped;
        }

        return Vector2.zero;
    }

    private static bool SubmitPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonSouth.wasPressedThisFrame;
    }

    private void FocusFirstAvailable()
    {
        CampaignNodeButtonUI best = null;
        foreach (CampaignNodeButtonUI btn in spawnedButtons.Values)
        {
            if (btn == null || btn.CurrentStatus != CampaignNodeButtonUI.NodeVisualStatus.Available) continue;
            if (best == null || btn.Rect.anchoredPosition.x < best.Rect.anchoredPosition.x)
            {
                best = btn;
            }
        }

        if (best == null)
        {
            // Nothing playable (e.g. every node cleared): still give focus somewhere so the tooltip browses.
            foreach (CampaignNodeButtonUI btn in spawnedButtons.Values)
            {
                if (btn != null) { best = btn; break; }
            }
        }

        SetFocus(best);
    }

    /// <summary>
    ///     Picks the nearest node in the pressed direction, weighting sideways offset so "right" follows
    ///     the row you're on before jumping lanes. Locked nodes are browsable (their tooltip shows) but
    ///     can't be deployed to.
    /// </summary>
    private void MoveFocus(Vector2 direction)
    {
        Vector2 from = focusedButton.Rect.anchoredPosition;
        CampaignNodeButtonUI best = null;
        float bestScore = float.MaxValue;

        foreach (CampaignNodeButtonUI btn in spawnedButtons.Values)
        {
            if (btn == null || btn == focusedButton) continue;

            Vector2 delta = btn.Rect.anchoredPosition - from;
            float along = Vector2.Dot(delta, direction);
            if (along <= 1f) continue;

            float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
            float score = along + across * navPerpendicularWeight;
            if (score < bestScore)
            {
                bestScore = score;
                best = btn;
            }
        }

        if (best != null)
        {
            SetFocus(best);
        }
    }

    private void SetFocus(CampaignNodeButtonUI button)
    {
        if (button == focusedButton)
        {
            return;
        }

        if (focusedButton != null)
        {
            focusedButton.SetFocused(false);
        }

        focusedButton = button;

        if (focusedButton != null)
        {
            focusedButton.SetFocused(true);
            FocusOnNode(focusedButton.Rect);
        }
    }

    private void ShowDemoEnd()
    {
        if (demoEndScreen == null || !demoEndScreen.IsValid)
        {
            // Every node past the cutoff is locked, so without the panel the player would be stuck here.
            Debug.LogError("[CampaignMapUI] Demo end reached but no valid DemoEndScreenUI is assigned. Ending the run without the end screen.");
            CampaignManager.Instance.EndCampaign();
            return;
        }

        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }
        demoEndShown = true;
        demoEndScreen.Show();
    }

    private void OnDestroy()
    {
        CursorLockManager.SetUnlock(CursorOwner, false);

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

        if (DemoConfigSO.IsCampaignNodeLocked(node))
        {
            return CampaignNodeButtonUI.NodeVisualStatus.DemoLocked;
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
        if (node == null || deploying) return;
        deploying = true;

        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }

        CampaignManager.Instance.SelectNode(node);
    }

    private void HandleNodeHovered(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status, Vector2 screenPos)
    {
        // A mouse hover takes over keyboard/gamepad focus, so the next key press moves on from here.
        if (node != null && spawnedButtons.TryGetValue(node.nodeId, out CampaignNodeButtonUI hovered) && hovered != focusedButton)
        {
            if (focusedButton != null)
            {
                focusedButton.SetFocused(false);
            }
            focusedButton = hovered;
        }

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
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null || nodeRect == null) return;

        // Node x is measured from the content's left edge (node prefab anchored at (0, 0.5)).
        // Centre it in the viewport: normalized 0 = content's left edge at the viewport's left.
        float scrollable = scrollRect.content.rect.width - scrollRect.viewport.rect.width;
        if (scrollable <= 0f) return;

        float targetLeft = nodeRect.anchoredPosition.x - scrollRect.viewport.rect.width * 0.5f;
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetLeft / scrollable);
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
