using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     The battlefield minimap (gate-defense scenes). A corner map that <b>M</b> / gamepad <b>Back</b>
///     expands into a centred tactical map and back, animated on unscaled time. It shows:
///     <list type="bullet">
///         <item>a top-down snapshot of the battlefield, rendered once on load;</item>
///         <item>the main gate (HP ring), every tower plot (what's built, ammo, element) and wall plot (HP, door, breach);</item>
///         <item>you, turned to your facing, with the camera's view cone;</item>
///         <item>enemy spawn points and the <b>predicted</b> route from each to the gate as marching chevrons,
///             re-pathed with the live wall costs (<see cref="WallNavCost" />) so building a wall or opening
///             a door visibly reroutes the horde, in prep and during waves;</item>
///         <item>every enemy as a dot sized by its NavMesh agent type, popping in as it spawns.</item>
///     </list>
///     Expanded, the cursor is freed and hovering a marker (or d-pad on a gamepad) shows what's there.
///     Hidden in scenes without a <see cref="Gate" />.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [SerializeField] private MinimapConfigSO config;

    [Header("Layout")]
    [Tooltip("The minimap's nested canvas; sorted above the rest of the HUD while expanded.")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private int expandedSortingBoost = 20;
    [Tooltip("Hidden in scenes with no gate.")]
    [SerializeField] private GameObject root;
    [Tooltip("The animated map rect. Its authored anchoring is the corner (collapsed) layout.")]
    [SerializeField] private RectTransform frame;
    [Tooltip("Gates raycasts: only the expanded map takes the mouse.")]
    [SerializeField] private CanvasGroup frameGroup;
    [SerializeField] private CanvasGroup backdrop;
    [Tooltip("Title, legend and close hint; faded in with the expand.")]
    [SerializeField] private CanvasGroup expandedChrome;
    [Tooltip("The corner map's key hint; faded out with the expand.")]
    [SerializeField] private CanvasGroup collapsedChrome;
    [SerializeField] private TMP_Text subtitle;
    [Tooltip("Key hint on the corner map (M / Back: Map).")]
    [SerializeField] private HintEntryView expandHint;
    [Tooltip("Key hint on the expanded map (M / Back: Close).")]
    [SerializeField] private HintEntryView closeHint;

    [Header("Map layers (all stretched over the map area)")]
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private RawImage mapImage;
    [SerializeField] private MinimapPathGraphic routes;
    [SerializeField] private RectTransform wallLayer;
    [SerializeField] private RectTransform markerLayer;
    [SerializeField] private MinimapDotsGraphic enemyDots;
    [SerializeField] private MinimapPlayerMarker playerMarker;
    [SerializeField] private MinimapTooltip tooltip;

    [Header("Element prefabs")]
    [SerializeField] private MinimapGateMarker gateMarkerPrefab;
    [SerializeField] private MinimapTowerMarker towerMarkerPrefab;
    [SerializeField] private MinimapWallMarker wallMarkerPrefab;
    [SerializeField] private MinimapSpawnMarker spawnMarkerPrefab;
    [SerializeField] private MinimapLegendRow legendRowPrefab;
    [SerializeField] private RectTransform legendContainer;

    [Header("Snapshot")]
    [Tooltip("Disabled ortho camera that renders the top-down terrain once. Unparented at runtime.")]
    [SerializeField] private Camera snapshotCamera;

    [Header("Feedbacks (optional)")]
    [SerializeField] private MMF_Player expandFeedback;
    [SerializeField] private MMF_Player collapseFeedback;

    private class DotInfo
    {
        public float firstSeen;
        public float baseSize;
        public Color32 color;
    }

    private const string CursorKey = "Minimap";

    private readonly List<MinimapMarker> markers = new List<MinimapMarker>();
    private readonly HashSet<TowerPlot> knownTowerPlots = new HashSet<TowerPlot>();
    private readonly HashSet<WallPlot> knownWallPlots = new HashSet<WallPlot>();
    private readonly List<Vector3> spawnClusters = new List<Vector3>();
    private readonly List<Vector2[]> routeBuffer = new List<Vector2[]>();
    private readonly Dictionary<AIMovement, DotInfo> dotInfo = new Dictionary<AIMovement, DotInfo>();
    private readonly Dictionary<int, float> agentRadius = new Dictionary<int, float>();
    private readonly List<AIMovement> staleDots = new List<AIMovement>();
    private NavMeshPath navPath;

    private MinimapProjection projection;
    private Vector2 collapsedAnchorMin, collapsedAnchorMax, collapsedPivot, collapsedPosition, collapsedSize;
    private bool ready;
    private bool expanded;
    private float transition; // 0 = corner, 1 = expanded (eased value is derived)
    private float transitionFrom;
    private float transitionStart;
    private float lastAppliedPixelsPerMeter = -1f;
    private float lastAppliedMarkerScale = -1f;
    private float nextMarkerRefresh;
    private float nextPlotSync;
    private float nextRouteRefresh;
    private float nextDotPrune;
    private int routeWallVersion = -1;
    private int pathAgentTypeId;
    private float marchPhase;
    private MinimapMarker hovered;
    private RenderTexture snapshot;
    private Coroutine reenablePause;
    private bool anyError;

    public MinimapConfigSO Config => config;
    public bool IsExpanded => expanded;

    private void Awake()
    {
        if (frame != null)
        {
            collapsedAnchorMin = frame.anchorMin;
            collapsedAnchorMax = frame.anchorMax;
            collapsedPivot = frame.pivot;
            collapsedPosition = frame.anchoredPosition;
            collapsedSize = frame.sizeDelta;
        }
    }

    private void Start()
    {
        if (config == null) { Debug.LogError($"[MinimapUI] {name}: config is not assigned.", this); anyError = true; }
        if (frame == null) { Debug.LogError($"[MinimapUI] {name}: frame is not assigned.", this); anyError = true; }
        if (frameGroup == null) { Debug.LogError($"[MinimapUI] {name}: frameGroup is not assigned.", this); anyError = true; }
        if (backdrop == null) { Debug.LogError($"[MinimapUI] {name}: backdrop is not assigned.", this); anyError = true; }
        if (mapArea == null) { Debug.LogError($"[MinimapUI] {name}: mapArea is not assigned.", this); anyError = true; }
        if (mapImage == null) { Debug.LogError($"[MinimapUI] {name}: mapImage is not assigned.", this); anyError = true; }
        if (routes == null) { Debug.LogError($"[MinimapUI] {name}: routes is not assigned.", this); anyError = true; }
        if (wallLayer == null || markerLayer == null) { Debug.LogError($"[MinimapUI] {name}: wallLayer/markerLayer are not assigned.", this); anyError = true; }
        if (enemyDots == null) { Debug.LogError($"[MinimapUI] {name}: enemyDots is not assigned.", this); anyError = true; }
        if (playerMarker == null) { Debug.LogError($"[MinimapUI] {name}: playerMarker is not assigned.", this); anyError = true; }
        if (tooltip == null) { Debug.LogError($"[MinimapUI] {name}: tooltip is not assigned.", this); anyError = true; }
        if (gateMarkerPrefab == null || towerMarkerPrefab == null || wallMarkerPrefab == null || spawnMarkerPrefab == null)
        {
            Debug.LogError($"[MinimapUI] {name}: a marker prefab is not assigned.", this);
            anyError = true;
        }
        if (snapshotCamera == null) { Debug.LogError($"[MinimapUI] {name}: snapshotCamera is not assigned.", this); anyError = true; }
        if (overlayCanvas == null) { Debug.LogError($"[MinimapUI] {name}: overlayCanvas is not assigned.", this); anyError = true; }
        if (root == null) root = gameObject;

        if (snapshotCamera != null) snapshotCamera.enabled = false;
        if (anyError)
        {
            SetRootVisible(false);
            return;
        }

        SetRootVisible(false);
        ApplyTransition(0f);
        StartCoroutine(InitialiseWhenSceneReady());
    }

    private void OnDestroy()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        if (expanded)
        {
            CursorLockManager.SetUnlock(CursorKey, false);
            if (PauseMenuController.Instance != null) PauseMenuController.Instance.SetToggleEnabled(true);
        }
        if (snapshot != null)
        {
            snapshot.Release();
            Destroy(snapshot);
        }
        if (snapshotCamera != null && snapshotCamera.transform.parent == null) Destroy(snapshotCamera.gameObject);
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null && root != gameObject) root.SetActive(visible);
        else if (frame != null) frame.gameObject.SetActive(visible);
    }

    // ---- Setup ---------------------------------------------------------------------------------

    private IEnumerator InitialiseWhenSceneReady()
    {
        // Plots, gates and spawners register in their own Awake/Start.
        yield return null;
        yield return null;

        if (Gate.All.Count == 0)
        {
            enabled = false;
            yield break;
        }

        pathAgentTypeId = FindAgentType(config.pathAgentTypeName);
        navPath = new NavMeshPath();
        BuildSpawnClusters();
        BuildProjection();
        RenderSnapshot();
        BuildStaticMarkers();
        SyncPlotMarkers();
        BuildLegend();
        enemyDots.SetOutline(config.dotOutlineColor, config.dotOutlineFraction);
        playerMarker.Attach(this);
        Place(playerMarker);

        BindHints();
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;

        SetRootVisible(true);
        ready = true;
        ApplyTransition(0f);
        RefreshRoutes();
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        BindHints();
    }

    private void BindHints()
    {
        if (expandHint != null) expandHint.Bind("<Keyboard>/m", "<Gamepad>/select", "minimap.hint_open", "Map");
        if (closeHint != null) closeHint.Bind("<Keyboard>/m", "<Gamepad>/select", "minimap.hint_close", "Close");
    }

    private static int FindAgentType(string agentName)
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentName) return s.agentTypeID;
        }
        return 0;
    }

    private void BuildSpawnClusters()
    {
        spawnClusters.Clear();
        var counts = new List<int>();
        SurvivorsSpawner spawner = SurvivorsSpawner.Instance;
        if (spawner == null) return;
        float clusterSqr = config.spawnClusterMeters * config.spawnClusterMeters;
        foreach (Transform point in spawner.SpawnPoints)
        {
            if (point == null) continue;
            int found = -1;
            for (int i = 0; i < spawnClusters.Count; i++)
            {
                Vector3 d = spawnClusters[i] - point.position;
                d.y = 0f;
                if (d.sqrMagnitude <= clusterSqr) { found = i; break; }
            }
            if (found < 0)
            {
                spawnClusters.Add(point.position);
                counts.Add(1);
            }
            else
            {
                int n = counts[found] + 1;
                spawnClusters[found] = Vector3.Lerp(spawnClusters[found], point.position, 1f / n);
                counts[found] = n;
            }
        }
        spawnClusterCounts = counts;
    }

    private List<int> spawnClusterCounts = new List<int>();

    private void BuildProjection()
    {
        Gate gate = Gate.All[0];
        Vector3 gatePos = gate.TargetPosition;

        float yaw = 0f;
        if (config.orientGateAtBottom && spawnClusters.Count > 0)
        {
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 p in spawnClusters) centroid += p;
            centroid /= spawnClusters.Count;
            Vector3 up = centroid - gatePos;
            up.y = 0f;
            if (up.sqrMagnitude > 1f)
            {
                yaw = Mathf.Atan2(up.x, up.z) * Mathf.Rad2Deg;
                if (config.snapOrientationTo90) yaw = Mathf.Round(yaw / 90f) * 90f;
            }
        }

        // Fit every point of interest (and optionally the NavMesh) in the turned frame.
        var frameAxes = new MinimapProjection(Vector3.zero, 2f, yaw);
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        float minY = gatePos.y, maxY = gatePos.y;

        void Encapsulate(Vector3 p)
        {
            Vector2 l = frameAxes.WorldToLocalMeters(p);
            min = Vector2.Min(min, l);
            max = Vector2.Max(max, l);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        foreach (Gate g in Gate.All) if (g != null) Encapsulate(g.TargetPosition);
        foreach (Vector3 p in spawnClusters) Encapsulate(p);
        if (TowerPlotManager.Instance != null)
        {
            foreach (TowerPlot p in TowerPlotManager.Instance.Plots) if (p != null) Encapsulate(p.BuildPosition);
            foreach (WallPlot p in TowerPlotManager.Instance.WallPlots) if (p != null) Encapsulate(p.transform.position);
        }
        if (config.fitToNavMesh)
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            foreach (Vector3 v in tri.vertices) Encapsulate(v);
        }

        Vector2 size = max - min;
        float side = Mathf.Clamp(Mathf.Max(size.x, size.y) + config.paddingMeters * 2f, config.minMapMeters, config.maxMapMeters);
        Vector2 mid = (min + max) * 0.5f;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 center = q * new Vector3(mid.x, 0f, mid.y);
        center.y = maxY;
        projection = new MinimapProjection(center, side, yaw);
        snapshotTop = maxY;
        snapshotDepth = maxY - minY;
    }

    private float snapshotTop;
    private float snapshotDepth;

    private void RenderSnapshot()
    {
        int res = Mathf.Clamp(config.snapshotResolution, 128, 4096);
        snapshot = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { name = "MinimapSnapshot", antiAliasing = 4 };
        snapshot.Create();

        Transform camT = snapshotCamera.transform;
        camT.SetParent(null, false);
        camT.position = new Vector3(projection.Center.x, snapshotTop + config.snapshotHeightAbove, projection.Center.z);
        camT.rotation = Quaternion.Euler(90f, projection.YawDegrees, 0f);
        snapshotCamera.orthographic = true;
        snapshotCamera.orthographicSize = projection.SizeMeters * 0.5f;
        snapshotCamera.aspect = 1f;
        snapshotCamera.nearClipPlane = 0.5f;
        snapshotCamera.farClipPlane = config.snapshotHeightAbove + snapshotDepth + 150f;
        snapshotCamera.cullingMask = config.snapshotCullingMask;
        snapshotCamera.targetTexture = snapshot;

        bool fog = RenderSettings.fog;
        if (config.disableFogForSnapshot) RenderSettings.fog = false;
        snapshotCamera.Render();
        RenderSettings.fog = fog;
        snapshotCamera.targetTexture = null;

        mapImage.texture = snapshot;
        mapImage.color = config.snapshotTint;
    }

    private void BuildStaticMarkers()
    {
        foreach (Gate gate in Gate.All)
        {
            if (gate == null) continue;
            MinimapGateMarker m = Instantiate(gateMarkerPrefab, markerLayer);
            m.Bind(gate);
            AddMarker(m);
        }
        for (int i = 0; i < spawnClusters.Count; i++)
        {
            MinimapSpawnMarker m = Instantiate(spawnMarkerPrefab, markerLayer);
            m.Bind(spawnClusters[i], spawnClusterCounts[i]);
            AddMarker(m);
        }
    }

    /// <summary>Adds markers for plots that registered after setup (and on the first call, all of them).</summary>
    private void SyncPlotMarkers()
    {
        TowerPlotManager manager = TowerPlotManager.Instance;
        if (manager == null) return;

        foreach (WallPlot plot in manager.WallPlots)
        {
            if (plot == null || !knownWallPlots.Add(plot)) continue;
            MinimapWallMarker m = Instantiate(wallMarkerPrefab, wallLayer);
            m.Bind(plot, projection.HeadingToUiAngle(plot.transform.right) + 90f);
            AddMarker(m);
        }
        foreach (TowerPlot plot in manager.Plots)
        {
            if (plot == null || !knownTowerPlots.Add(plot)) continue;
            MinimapTowerMarker m = Instantiate(towerMarkerPrefab, markerLayer);
            m.Bind(plot);
            AddMarker(m);
        }
    }

    private void AddMarker(MinimapMarker marker)
    {
        marker.Attach(this);
        Place(marker);
        marker.Refresh(config);
        markers.Add(marker);
        lastAppliedPixelsPerMeter = -1f; // re-scale the newcomer
    }

    private void Place(MinimapMarker marker)
    {
        Vector2 n = projection.WorldToMap(marker.WorldPosition);
        RectTransform r = marker.Rect;
        r.anchorMin = n;
        r.anchorMax = n;
        r.anchoredPosition = Vector2.zero;
    }

    private void BuildLegend()
    {
        if (legendRowPrefab == null || legendContainer == null) return;
        foreach (MinimapConfigSO.LegendEntry entry in config.legend)
        {
            if (entry == null) continue;
            MinimapLegendRow row = Instantiate(legendRowPrefab, legendContainer);
            row.Bind(entry);
        }
    }

    // ---- Per frame -----------------------------------------------------------------------------

    /// <summary>
    ///     The collapsed map sits in the top-right corner, where wide modal panels (shop, draft cards) reach.
    ///     While another screen holds the cursor unlocked, the collapsed map fades out so it never shows
    ///     through or pokes past a modal; the expanded map (which unlocks the cursor itself) stays visible.
    /// </summary>
    private void FadeForModals()
    {
        bool modalOpen = !expanded && transition <= 0.001f && CursorLockManager.IsCursorUnlocked;
        float target = modalOpen ? 0f : 1f;
        frameGroup.alpha = Mathf.MoveTowards(frameGroup.alpha, target, Time.unscaledDeltaTime * 8f);
    }

    private void Update()
    {
        if (anyError || !ready) return;
        float now = Time.unscaledTime;

        HandleInput();
        AnimateTransition(now);
        FadeForModals();

        if (now >= nextPlotSync)
        {
            nextPlotSync = now + 1f;
            SyncPlotMarkers();
        }

        if (now >= nextMarkerRefresh)
        {
            nextMarkerRefresh = now + 0.1f;
            foreach (MinimapMarker m in markers) if (m != null) m.Refresh(config);
        }

        if (hovered != null && expanded) tooltip.Show(hovered);

        Place(playerMarker);
        playerMarker.UpdateHeading(projection);

        if (now >= nextRouteRefresh || WallNavCost.Version != routeWallVersion) RefreshRoutes();

        float p = Mathf.Clamp01(transition);
        float pathWidth = Mathf.Lerp(config.pathWidthCollapsed, config.pathWidthExpanded, p);
        marchPhase = Mathf.Repeat(marchPhase + Time.unscaledDeltaTime * config.marchSpeed, 1000f);
        routes.SetStyle(pathWidth, config.chevronSpacing, marchPhase);

        UpdateEnemyDots(now, p);
        UpdateSubtitle();
    }

    private void HandleInput()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool toggle = (kb != null && kb.mKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
        bool cancel = expanded && ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));

        bool playerDead = Player.Instance != null && Player.Instance.Health != null && Player.Instance.Health.IsDead;
        bool blocked = DevConsole.IsVisible
                       || (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
                       || playerDead;
        if ((toggle || expanded) && BuildWheelUI.Instance != null && BuildWheelUI.Instance.IsOpen) blocked = true;

        if (expanded && blocked)
        {
            SetExpanded(false);
            return;
        }
        if (blocked) return;

        if (cancel || (toggle && expanded)) SetExpanded(false);
        else if (toggle) SetExpanded(true);

        if (expanded && pad != null) HandleGamepadHover(pad);
    }

    public void SetExpanded(bool expand)
    {
        if (expand == expanded || anyError || !ready) return;
        expanded = expand;
        transitionFrom = transition;
        transitionStart = Time.unscaledTime;

        CursorLockManager.SetUnlock(CursorKey, expand);
        if (PauseMenuController.Instance != null)
        {
            if (reenablePause != null) StopCoroutine(reenablePause);
            reenablePause = null;
            if (expand) PauseMenuController.Instance.SetToggleEnabled(false);
            else reenablePause = StartCoroutine(ReenablePauseNextFrame());
        }
        frameGroup.blocksRaycasts = expand;
        backdrop.blocksRaycasts = expand;
        if (expand) SetOverlaySorting(true);
        if (!expand)
        {
            if (hovered != null) hovered.SetHighlighted(false);
            hovered = null;
            tooltip.Hide();
        }

        MMF_Player fb = expand ? expandFeedback : collapseFeedback;
        if (fb != null) fb.PlayFeedbacks();
    }

    private IEnumerator ReenablePauseNextFrame()
    {
        // The Escape that closed the map must not also open the pause menu this frame.
        yield return null;
        if (PauseMenuController.Instance != null) PauseMenuController.Instance.SetToggleEnabled(true);
        reenablePause = null;
    }

    private void AnimateTransition(float now)
    {
        float target = expanded ? 1f : 0f;
        if (Mathf.Approximately(transition, target) && lastAppliedMarkerScale >= 0f && !NeedsRescale()) return;

        float seconds = expanded ? config.expandSeconds : config.collapseSeconds;
        float k = seconds > 0f ? Mathf.Clamp01((now - transitionStart) / seconds) : 1f;
        AnimationCurve curve = expanded ? config.expandCurve : config.collapseCurve;
        float eased = curve != null ? curve.Evaluate(k) : k;
        transition = Mathf.LerpUnclamped(transitionFrom, target, eased);
        if (k >= 1f)
        {
            transition = target;
            if (!expanded) SetOverlaySorting(false);
        }
        ApplyTransition(transition);
    }

    private void SetOverlaySorting(bool onTop)
    {
        // Above the HUD's prompts and banners while expanded; back in normal order in the corner.
        Canvas parent = overlayCanvas.transform.parent != null ? overlayCanvas.transform.parent.GetComponentInParent<Canvas>() : null;
        overlayCanvas.overrideSorting = onTop;
        if (onTop) overlayCanvas.sortingOrder = (parent != null ? parent.sortingOrder : 0) + expandedSortingBoost;
    }

    private bool NeedsRescale()
    {
        return lastAppliedPixelsPerMeter < 0f;
    }

    private void ApplyTransition(float t)
    {
        if (frame == null) return;
        var centre = new Vector2(0.5f, 0.5f);
        frame.anchorMin = Vector2.LerpUnclamped(collapsedAnchorMin, centre, t);
        frame.anchorMax = Vector2.LerpUnclamped(collapsedAnchorMax, centre, t);
        frame.pivot = Vector2.LerpUnclamped(collapsedPivot, centre, t);
        frame.anchoredPosition = Vector2.LerpUnclamped(collapsedPosition, config != null ? config.expandedOffset : Vector2.zero, t);
        float side = config != null ? config.expandedSize : collapsedSize.x;
        frame.sizeDelta = Vector2.LerpUnclamped(collapsedSize, new Vector2(side, side), t);

        float p = Mathf.Clamp01(t);
        if (backdrop != null) backdrop.alpha = p * (config != null ? config.backdropAlpha : 0.5f);
        if (expandedChrome != null) expandedChrome.alpha = Mathf.InverseLerp(0.6f, 1f, p);
        if (collapsedChrome != null) collapsedChrome.alpha = 1f - Mathf.InverseLerp(0f, 0.3f, p);

        if (!ready || config == null || mapArea == null) return;
        // mapArea stretches inside the frame; RectTransform rects resolve synchronously from the size set above.
        float pixelsPerMeter = mapArea.rect.width / projection.SizeMeters;
        float markerScale = Mathf.Lerp(config.collapsedMarkerScale, config.expandedMarkerScale, p);
        if (Mathf.Approximately(pixelsPerMeter, lastAppliedPixelsPerMeter) && Mathf.Approximately(markerScale, lastAppliedMarkerScale)) return;
        lastAppliedPixelsPerMeter = pixelsPerMeter;
        lastAppliedMarkerScale = markerScale;
        foreach (MinimapMarker m in markers) if (m != null) m.ApplyScale(pixelsPerMeter, markerScale);
        playerMarker.ApplyScale(pixelsPerMeter, markerScale);
    }

    // ---- Routes --------------------------------------------------------------------------------

    private void RefreshRoutes()
    {
        nextRouteRefresh = Time.unscaledTime + Mathf.Max(0.1f, config.pathRefreshSeconds);
        routeWallVersion = WallNavCost.Version;
        routeBuffer.Clear();

        var filter = new NavMeshQueryFilter { agentTypeID = pathAgentTypeId, areaMask = NavMesh.AllAreas };
        WallNavCost.ApplyTo(ref filter);

        foreach (Vector3 spawn in spawnClusters)
        {
            Gate gate = Gate.NearestAlive(spawn);
            if (gate == null) break;
            if (!NavMesh.SamplePosition(gate.TargetPosition, out NavMeshHit end, config.gateSampleRadius, filter)) continue;
            if (!NavMesh.SamplePosition(spawn, out NavMeshHit start, config.spawnSampleRadius, filter)) continue;
            if (!NavMesh.CalculatePath(start.position, end.position, filter, navPath)) continue;
            if (navPath.status == NavMeshPathStatus.PathInvalid || navPath.corners.Length < 2) continue;

            Vector3[] corners = navPath.corners;
            var mapped = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++) mapped[i] = projection.WorldToMap(corners[i]);
            routeBuffer.Add(mapped);
        }
        routes.SetRoutes(routeBuffer);
    }

    // ---- Enemy dots ----------------------------------------------------------------------------

    private void UpdateEnemyDots(float now, float expandProgress)
    {
        List<MinimapDotsGraphic.Dot> dots = enemyDots.Dots;
        dots.Clear();
        float scale = Mathf.Lerp(config.collapsedDotScale, 1f, expandProgress);

        IReadOnlyList<AIMovement> enemies = AIMovement.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            AIMovement enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;
            Vector2 n = projection.WorldToMap(enemy.transform.position);
            if (n.x < -0.05f || n.x > 1.05f || n.y < -0.05f || n.y > 1.05f) continue;

            if (!dotInfo.TryGetValue(enemy, out DotInfo info))
            {
                info = CreateDotInfo(enemy, now);
                dotInfo.Add(enemy, info);
            }

            float age = now - info.firstSeen;
            float pop = config.spawnPopSeconds > 0f && age < config.spawnPopSeconds && config.spawnPopCurve != null
                ? config.spawnPopCurve.Evaluate(age / config.spawnPopSeconds)
                : 1f;
            dots.Add(new MinimapDotsGraphic.Dot { position = n, size = info.baseSize * scale * pop, color = info.color });
        }
        enemyDots.Rebuild();

        if (now >= nextDotPrune)
        {
            nextDotPrune = now + 2f;
            staleDots.Clear();
            foreach (KeyValuePair<AIMovement, DotInfo> pair in dotInfo)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.IsDead) staleDots.Add(pair.Key);
            }
            foreach (AIMovement stale in staleDots) dotInfo.Remove(stale);
        }
    }

    private DotInfo CreateDotInfo(AIMovement enemy, float now)
    {
        NavMeshAgent agent = enemy.Agent;
        int typeId = agent != null ? agent.agentTypeID : 0;
        if (!agentRadius.TryGetValue(typeId, out float radius))
        {
            radius = NavMesh.GetSettingsByID(typeId).agentRadius;
            if (radius <= 0f) radius = 0.5f;
            agentRadius[typeId] = radius;
        }

        Color32 color = config.enemyColor;
        if (enemy.GetComponentInParent<ICaptain>() != null) color = config.captainColor;
        else if (WallNavCost.IsSiege(enemy.gameObject)) color = config.siegeColor;

        return new DotInfo
        {
            firstSeen = now,
            baseSize = Mathf.Clamp(radius * config.dotSizePerAgentRadius, config.minDotSize, config.maxDotSize),
            color = color
        };
    }

    // ---- Hover ---------------------------------------------------------------------------------

    public void SetHovered(MinimapMarker marker)
    {
        if (!expanded || marker == null) return;
        if (hovered != null && hovered != marker) hovered.SetHighlighted(false);
        hovered = marker;
        hovered.SetHighlighted(true);
        tooltip.Show(marker);
    }

    public void ClearHovered(MinimapMarker marker)
    {
        if (hovered != marker) return;
        hovered.SetHighlighted(false);
        hovered = null;
        tooltip.Hide();
    }

    private void HandleGamepadHover(Gamepad pad)
    {
        Vector2 dir = Vector2.zero;
        if (pad.dpad.right.wasPressedThisFrame) dir = Vector2.right;
        else if (pad.dpad.left.wasPressedThisFrame) dir = Vector2.left;
        else if (pad.dpad.up.wasPressedThisFrame) dir = Vector2.up;
        else if (pad.dpad.down.wasPressedThisFrame) dir = Vector2.down;
        if (dir == Vector2.zero) return;

        Vector2 from = hovered != null ? projection.WorldToMap(hovered.WorldPosition) : projection.WorldToMap(playerMarker.WorldPosition);
        MinimapMarker best = null;
        float bestScore = float.MaxValue;
        foreach (MinimapMarker m in markers)
        {
            if (m == null || m == hovered || !m.Hoverable) continue;
            Vector2 d = projection.WorldToMap(m.WorldPosition) - from;
            float along = Vector2.Dot(d, dir);
            if (along <= 0.001f) continue;
            float across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
            float score = along + across * 2.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = m;
            }
        }
        if (best != null) SetHovered(best);
    }

    private void UpdateSubtitle()
    {
        if (subtitle == null || !expanded) return;
        GameLoopManager loop = GameLoopManager.Instance;
        if (loop == null || loop.IsPrepPhase)
        {
            subtitle.text = Loc.Get("minimap.subtitle_prep", "Prep phase: arrows show the route the next wave will take");
        }
        else
        {
            subtitle.text = string.Format(Loc.Get("minimap.subtitle_wave", "Wave {0}: {1} enemies on the field"),
                loop.CurrentWave, enemyDots.Dots.Count);
        }
    }
}
