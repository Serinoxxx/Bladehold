using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
///     Master controller for the Castle Campaign Overview Map screen.
///     The map itself is a 3D miniature landscape (<see cref="CampaignDiorama" />): one castle per node and a
///     glowing route per edge. This screen owns the campaign logic and the overlay UI: it spawns one label
///     button per node and keeps it hanging under its castle every frame, pushes node/route status into the
///     diorama, updates currency status, and directs deployment to selected sectors.
/// </summary>
public class CampaignMapUI : MonoBehaviour
{
    [Header("Graph & Containers")]
    [SerializeField] private CampaignGraphSO campaignGraph;
    [Tooltip("The 3D map (castles, routes, camera) the node labels hang over.")]
    [SerializeField] private CampaignDiorama diorama;
    [Tooltip("Full-screen overlay rect the node labels are spawned into (cleared on every rebuild).")]
    [SerializeField] private RectTransform nodesContainer;

    [Header("Prefabs")]
    [SerializeField] private CampaignNodeButtonUI nodeButtonPrefab;

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

    [Header("Tier Column Headers")]
    [Tooltip("Strip along the top of the screen holding one header per tier column; each header slides with its column as the camera pans.")]
    [SerializeField] private RectTransform tierHeadersContainer;
    [Tooltip("Header label prefab (UI/CampaignTierHeader.prefab), anchored top-left of the strip, pivot top-centre.")]
    [SerializeField] private TMP_Text tierHeaderPrefab;
    [SerializeField] private float tierHeaderTopMargin = 8f;

    [Header("Current Location Marker")]
    [Tooltip("'You are here' marker (overlay, drawn above the node labels). Hangs over the castle of the last cleared node, or the first node before anything is cleared.")]
    [SerializeField] private RectTransform locationMarker;
    [Tooltip("Screen offset (canvas units) from the castle's marker anchor.")]
    [SerializeField] private Vector2 locationMarkerOffset = new Vector2(0f, 12f);
    [SerializeField] private float locationMarkerBobHeight = 6f;
    [SerializeField] private float locationMarkerBobSpeed = 2.5f;

    [Header("Keyboard / Gamepad Navigation")]
    [Tooltip("Left-stick deflection that counts as a direction press.")]
    [SerializeField] private float stickThreshold = 0.6f;
    [Tooltip("Seconds between repeated moves while a stick or key is held.")]
    [SerializeField] private float navRepeatDelay = 0.3f;
    [Tooltip("How much sideways offset counts against a candidate node versus distance along the pressed direction.")]
    [SerializeField] private float navPerpendicularWeight = 2f;

    private const string CursorOwner = "CampaignMap";

    private readonly Dictionary<string, CampaignNodeButtonUI> spawnedButtons = new Dictionary<string, CampaignNodeButtonUI>();
    private readonly List<KeyValuePair<RectTransform, float>> tierHeaders = new List<KeyValuePair<RectTransform, float>>();

    private readonly HashSet<string> reachableNodeIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private CampaignDioramaSite locationSite;
    private Canvas canvas;

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
        if (diorama == null || !diorama.IsValid)
        {
            Debug.LogError("[CampaignMapUI] No valid CampaignDiorama assigned. Run Bladehold > Campaign > Build Map Diorama.");
            anyError = true;
        }
        if (nodesContainer == null)
        {
            Debug.LogError("[CampaignMapUI] Nodes container is not assigned.");
            anyError = true;
        }
        if (locationMarker == null)
        {
            Debug.LogError("[CampaignMapUI] locationMarker is not assigned (the 'you are here' marker).");
        }
        canvas = GetComponentInParent<Canvas>();

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
    ///     Keeps every overlay element glued to the diorama as the camera pans: node labels under their
    ///     castles, the location marker over its castle, tier headers over their columns.
    /// </summary>
    private void LateUpdate()
    {
        if (anyError) return;

        foreach (CampaignNodeButtonUI button in spawnedButtons.Values)
        {
            if (button == null || button.NodeData == null) continue;
            if (diorama.TryGetSite(button.NodeData.nodeId, out CampaignDioramaSite site))
            {
                PlaceOverWorld(button.Rect, nodesContainer, site.LabelAnchor.position, Vector2.zero);
            }
        }

        if (locationMarker != null && locationSite != null && locationMarker.gameObject.activeSelf)
        {
            float bob = Mathf.Sin(Time.unscaledTime * locationMarkerBobSpeed) * locationMarkerBobHeight;
            PlaceOverWorld(locationMarker, locationMarker.parent as RectTransform, locationSite.MarkerAnchor.position, locationMarkerOffset + new Vector2(0f, bob));
        }

        foreach (KeyValuePair<RectTransform, float> header in tierHeaders)
        {
            if (header.Key == null) continue;
            if (!diorama.WorldToScreen(new Vector3(header.Value, 0f, diorama.transform.position.z), out Vector2 screen)) continue;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tierHeadersContainer, screen, UICamera, out Vector2 local))
            {
                Vector3 p = header.Key.localPosition;
                header.Key.localPosition = new Vector3(local.x, p.y, p.z);
            }
        }
    }

    private Camera UICamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

    private void PlaceOverWorld(RectTransform rect, RectTransform space, Vector3 world, Vector2 offset)
    {
        if (rect == null || space == null) return;
        if (!diorama.WorldToScreen(world, out Vector2 screen)) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, UICamera, out Vector2 local))
        {
            Vector3 p = rect.localPosition;
            rect.localPosition = new Vector3(local.x + offset.x, local.y + offset.y, p.z);
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
            if (best == null || btn.NodeData.mapPosition.x < best.NodeData.mapPosition.x)
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
        Vector2 from = focusedButton.Rect.localPosition;
        CampaignNodeButtonUI best = null;
        float bestScore = float.MaxValue;

        foreach (CampaignNodeButtonUI btn in spawnedButtons.Values)
        {
            if (btn == null || btn == focusedButton) continue;

            Vector2 delta = (Vector2)btn.Rect.localPosition - from;
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
            diorama.FocusOnNode(focusedButton.NodeData.nodeId);
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
        ComputeReachable();

        List<CampaignNodeSO> allNodes = campaignGraph.allNodes;

        CampaignNodeSO firstAvailableNode = null;

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

                if (status == CampaignNodeButtonUI.NodeVisualStatus.Available && firstAvailableNode == null)
                {
                    firstAvailableNode = node;
                }
            }
        }

        // Labels further back on the table draw first, so nearer castles' labels overlap them.
        List<CampaignNodeButtonUI> byDepth = new List<CampaignNodeButtonUI>(spawnedButtons.Values);
        byDepth.Sort((a, b) => b.NodeData.mapPosition.y.CompareTo(a.NodeData.mapPosition.y));
        foreach (CampaignNodeButtonUI button in byDepth) button.transform.SetAsLastSibling();

        // 2. Castles and routes on the diorama
        ApplyDioramaStatus();
        PlaceLocationMarker();
        BuildTierHeaders();

        // 3. Open on the next choice (or where the player stands when nothing is open)
        CampaignNodeSO focus = firstAvailableNode != null ? firstAvailableNode : CampaignManager.Instance.CurrentLocationNode;
        if (focus != null)
        {
            diorama.FocusOnNode(focus.nodeId, instant: true);
        }
        LateUpdate();
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
        ComputeReachable();

        foreach (var kvp in spawnedButtons)
        {
            string nodeId = kvp.Key;
            CampaignNodeButtonUI button = kvp.Value;
            if (button != null && button.NodeData != null)
            {
                CampaignNodeButtonUI.NodeVisualStatus status = EvaluateNodeStatus(button.NodeData);
                button.Setup(button.NodeData, status, HandleNodeClicked, HandleNodeHovered, HandleNodeHoverExited);
            }
        }

        ApplyDioramaStatus();
        PlaceLocationMarker();
    }

    /// <summary>Every node still reachable going forward from the open (available) nodes.</summary>
    private void ComputeReachable()
    {
        reachableNodeIds.Clear();
        Queue<CampaignNodeSO> frontier = new Queue<CampaignNodeSO>();
        foreach (string id in CampaignManager.Instance.AvailableNodeIds)
        {
            CampaignNodeSO node = campaignGraph.GetNodeById(id);
            if (node != null && reachableNodeIds.Add(node.nodeId)) frontier.Enqueue(node);
        }
        while (frontier.Count > 0)
        {
            CampaignNodeSO node = frontier.Dequeue();
            if (node.nextNodes == null) continue;
            foreach (CampaignNodeSO next in node.nextNodes)
            {
                if (next != null && reachableNodeIds.Add(next.nodeId)) frontier.Enqueue(next);
            }
        }
    }

    private static readonly string[] RomanNumerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

    /// <summary>One label per tier ("IV  Great Hall") over its column, from the graph's tier list.</summary>
    private void BuildTierHeaders()
    {
        if (tierHeadersContainer == null || tierHeaderPrefab == null || campaignGraph.tiers == null) return;
        for (int i = tierHeadersContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(tierHeadersContainer.GetChild(i).gameObject);
        }
        tierHeaders.Clear();

        foreach (CampaignTier tier in campaignGraph.tiers)
        {
            if (tier == null || tier.nodes == null) continue;
            float columnX = float.MaxValue;
            foreach (CampaignNodeSO node in tier.nodes)
            {
                if (node != null && spawnedButtons.ContainsKey(node.nodeId)) columnX = Mathf.Min(columnX, node.mapPosition.x);
            }
            if (columnX == float.MaxValue) continue;

            TMP_Text header = Instantiate(tierHeaderPrefab, tierHeadersContainer);
            // Tier 0 (the Valley Stronghold's column, plan 21) has a name but no numeral.
            string numeral = tier.tierNumber < 1 ? "" : tier.tierNumber <= RomanNumerals.Length ? RomanNumerals[tier.tierNumber - 1] : tier.tierNumber.ToString();
            header.text = $"<size=140%>{numeral}</size>\n{tier.tierName}";
            header.rectTransform.anchoredPosition = new Vector2(0f, -tierHeaderTopMargin);
            tierHeaders.Add(new KeyValuePair<RectTransform, float>(header.rectTransform, diorama.MapToWorldX(columnX)));
        }
    }

    private void PlaceLocationMarker()
    {
        if (locationMarker == null) return;
        CampaignNodeSO here = CampaignManager.Instance.CurrentLocationNode;
        if (here == null || !diorama.TryGetSite(here.nodeId, out locationSite))
        {
            locationSite = null;
            locationMarker.gameObject.SetActive(false);
            return;
        }
        locationMarker.gameObject.SetActive(true);
        locationMarker.SetAsLastSibling();
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

        // Further along a branch the player can still take: waiting its turn. Anything else is behind them.
        return reachableNodeIds.Contains(node.nodeId)
            ? CampaignNodeButtonUI.NodeVisualStatus.Locked
            : CampaignNodeButtonUI.NodeVisualStatus.Bypassed;
    }

    private CampaignNodeButtonUI SpawnNodeButton(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status)
    {
        CampaignNodeButtonUI btn = Instantiate(nodeButtonPrefab, nodesContainer);

        btn.Setup(node, status, HandleNodeClicked, HandleNodeHovered, HandleNodeHoverExited);
        return btn;
    }

    /// <summary>Pushes every node's status onto its castle and every edge's status onto its route overlay.</summary>
    private void ApplyDioramaStatus()
    {
        foreach (CampaignNodeButtonUI button in spawnedButtons.Values)
        {
            if (button != null && button.NodeData != null)
            {
                diorama.ApplyNodeStatus(button.NodeData.nodeId, button.CurrentStatus);
            }
        }

        foreach (CampaignNodeSO fromNode in campaignGraph.allNodes)
        {
            if (fromNode == null || fromNode.nextNodes == null) continue;
            if (!spawnedButtons.TryGetValue(fromNode.nodeId, out CampaignNodeButtonUI fromBtn) || fromBtn == null) continue;
            bool fromCompleted = fromBtn.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Completed;

            foreach (CampaignNodeSO toNode in fromNode.nextNodes)
            {
                if (toNode == null) continue;
                if (!spawnedButtons.TryGetValue(toNode.nodeId, out CampaignNodeButtonUI toBtn) || toBtn == null) continue;

                CampaignNodeButtonUI.NodeVisualStatus toStatus = toBtn.CurrentStatus;
                CampaignDioramaRoad.RoadStatus road = CampaignDioramaRoad.RoadStatus.Locked;
                if (fromCompleted && toStatus == CampaignNodeButtonUI.NodeVisualStatus.Available)
                {
                    road = CampaignDioramaRoad.RoadStatus.Available;
                }
                else if (fromCompleted && toStatus == CampaignNodeButtonUI.NodeVisualStatus.Completed)
                {
                    road = CampaignDioramaRoad.RoadStatus.Completed;
                }
                else if (fromBtn.CurrentStatus == CampaignNodeButtonUI.NodeVisualStatus.Bypassed ||
                         toStatus == CampaignNodeButtonUI.NodeVisualStatus.Bypassed ||
                         fromCompleted)
                {
                    road = CampaignDioramaRoad.RoadStatus.Bypassed;
                }
                diorama.ApplyRoadStatus(fromNode.nodeId, toNode.nodeId, road);
            }
        }
    }

    /// <summary>A mouse click on a node label; ignored when it ends a camera drag.</summary>
    private void HandleNodeClicked(CampaignNodeSO node)
    {
        if (diorama.DioramaCamera != null && diorama.DioramaCamera.SuppressClick) return;
        HandleNodeSelected(node);
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
        CampaignNodeButtonUI hovered = null;
        if (node != null)
        {
            spawnedButtons.TryGetValue(node.nodeId, out hovered);
        }

        // A mouse hover takes over keyboard/gamepad focus, so the next key press moves on from here.
        if (hovered != null && hovered != focusedButton)
        {
            if (focusedButton != null)
            {
                focusedButton.SetFocused(false);
            }
            focusedButton = hovered;
        }

        if (tooltipUI != null)
        {
            // Anchor to the node's rect so the tooltip sits beside the node instead of over it.
            if (hovered != null && hovered.Rect != null)
            {
                tooltipUI.Show(node, status, hovered.Rect);
            }
            else
            {
                tooltipUI.Show(node, status, screenPos);
            }
        }
    }

    private void HandleNodeHoverExited()
    {
        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }
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
    }
}
