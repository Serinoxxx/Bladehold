using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Tracks all active objective entities (cages, wagon, slayer boss, siege engines)
///     and renders screen-space / edge-clamped waypoint indicators on the HUD.
/// </summary>
public class ObjectiveWaypointTrackerUI : MonoBehaviour
{
    public static ObjectiveWaypointTrackerUI Instance { get; private set; }

    [Header("Manager Reference")]
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;

    [Header("Marker Template & Container")]
    [Tooltip("Container holding waypoint marker instances.")]
    [SerializeField] private RectTransform markersContainer;

    [Tooltip("Marker prefab, pooled under markersContainer (UI/ObjectiveWaypointMarker.prefab).")]
    [SerializeField] private ObjectiveWaypointMarkerUI markerTemplate;

    [Header("Default Visual Fallbacks")]
    [SerializeField] private Sprite defaultObjectiveIcon;
    [SerializeField] private Sprite prisonerCageIcon;
    [SerializeField] private Sprite supplyWagonIcon;
    [SerializeField] private Sprite destinationGateIcon;
    [SerializeField] private Sprite slayerBossIcon;
    [SerializeField] private Sprite cleanupEnemySkullIcon;
    [SerializeField] private Sprite siegeEngineIcon;
    [SerializeField] private Sprite noSupplyIcon;
    [SerializeField] private Sprite arrowIcon;
    [SerializeField] private Sprite iconBackground;

    [Header("Tower Plots (prep phase)")]
    [Tooltip("Icon on tower plot markers during the prep phase. Optional: falls back to defaultObjectiveIcon.")]
    [SerializeField] private Sprite towerPlotIcon;
    [SerializeField] private Vector3 towerPlotOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private Color towerPlotEmptyTint = new Color(1f, 0.82f, 0.3f, 1f);
    [SerializeField] private Color towerPlotBuiltTint = new Color(1f, 1f, 1f, 0.6f);

    public Sprite CleanupEnemySkullIcon => cleanupEnemySkullIcon != null ? cleanupEnemySkullIcon : slayerBossIcon;

    /// <summary>Default marker icon, for sources that don't bring their own.</summary>
    public Sprite DefaultObjectiveIcon => defaultObjectiveIcon;

    /// <summary>
    ///     Waypoint providers outside the objective system. Register in <c>OnEnable</c>, unregister in
    ///     <c>OnDisable</c>; static so a source can register before the HUD's tracker exists.
    /// </summary>
    public static readonly List<IWaypointSource> ExtraSources = new List<IWaypointSource>();

    public static void RegisterSource(IWaypointSource source)
    {
        if (source != null && !ExtraSources.Contains(source)) ExtraSources.Add(source);
    }

    public static void UnregisterSource(IWaypointSource source) => ExtraSources.Remove(source);

    [Header("Screen Clamping & Juice")]
    [Tooltip("Padding in pixels from screen edges when clamping offscreen waypoints.")]
    [SerializeField] private Vector2 screenEdgePadding = new Vector2(90f, 90f);

    [Tooltip("Gentle float bobbing height for on-screen markers.")]
    [SerializeField] private float bobAmplitude = 5f;
    [SerializeField] private float bobSpeed = 3.5f;

    private readonly List<ObjectiveWaypointTarget> targetBuffer = new List<ObjectiveWaypointTarget>();
    private readonly List<ObjectiveWaypointMarkerUI> markerPool = new List<ObjectiveWaypointMarkerUI>();

    private Canvas rootCanvas;
    private RectTransform canvasRect;
    private Camera mainCamera;
    private bool anyError;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            canvasRect = rootCanvas.GetComponent<RectTransform>();
        }

        if (markersContainer == null)
        {
            markersContainer = GetComponent<RectTransform>();
        }

        ValidateSprites();
    }

    private void Start()
    {
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindFirstObjectByType<SurvivorsObjectiveManager>();
        }

        mainCamera = Camera.main;

        if (markerTemplate == null)
        {
            Debug.LogError("[ObjectiveWaypointTrackerUI] markerTemplate is not assigned (UI/ObjectiveWaypointMarker.prefab).", this);
            anyError = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (anyError) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindFirstObjectByType<SurvivorsObjectiveManager>();
        }

        // 1. Gather all active targets
        targetBuffer.Clear();

        if (objectiveManager != null && objectiveManager.CurrentObjective != null && objectiveManager.CurrentObjective.IsActive)
        {
            objectiveManager.CurrentObjective.GetActiveWaypointTargets(targetBuffer);
        }


        // Prep phase (plan 15): preview where the picked wave card's objective will happen
        GameLoopManager loop = GameLoopManager.Instance;
        if (loop != null && loop.IsAwaitingReady && loop.CurrentWaveCard != null && objectiveManager != null)
        {
            if (objectiveManager.FindObjective(loop.CurrentWaveCard.ObjectiveId) is IObjectivePreview preview)
            {
                preview.GetPreviewWaypointTargets(targetBuffer);
            }
        }

        // Prep phase: mark every tower plot so the player can find where to build (only the focused ones
        // while a tutorial lesson narrows them, see BuildMarkerFocus)
        if (loop != null && loop.IsAwaitingReady && TowerPlotManager.Instance != null)
        {
            AddTowerPlotTargets(TowerPlotManager.Instance.Plots);
            AddWallPlotTargets(TowerPlotManager.Instance.WallPlots);
        }

        // Non-objective sources (the tutorial's step waypoints, first-encounter hints)
        for (int s = ExtraSources.Count - 1; s >= 0; s--)
        {
            // Interface refs skip Unity's fake-null check, so test destroyed components explicitly.
            if (ExtraSources[s] == null || (ExtraSources[s] is Object unityObj && unityObj == null))
            {
                ExtraSources.RemoveAt(s);
                continue;
            }
            ExtraSources[s].GetWaypointTargets(targetBuffer);
        }

        // Check for depleted defenses (NO SUPPLY)
        foreach (var def in DefenseStructure.AllActive)
        {
            if (def != null && def.IsDepleted)
            {
                targetBuffer.Add(new ObjectiveWaypointTarget(
                    def.transform,
                    worldOffset: new Vector3(0f, 3.2f, 0f),
                    customIcon: noSupplyIcon != null ? noSupplyIcon : defaultObjectiveIcon,
                    tintColor: new Color(1.0f, 0.45f, 0.1f, 1f),
                    label: "NO SUPPLY"
                ));
            }
            else if (def != null && def.IsHexed)
            {
                targetBuffer.Add(new ObjectiveWaypointTarget(
                    def.transform,
                    worldOffset: new Vector3(0f, 3.2f, 0f),
                    customIcon: noSupplyIcon != null ? noSupplyIcon : defaultObjectiveIcon,
                    tintColor: new Color(0.7f, 0.35f, 1f, 1f),
                    label: "HEXED"
                ));
            }
        }

        // Fallback for cleanup phase if buffer is empty
        if (targetBuffer.Count == 0 && objectiveManager != null && objectiveManager.Phase == SurvivorsObjectivePhase.Cleanup && SurvivorsSpawner.Instance != null)
        {
            foreach (var h in SurvivorsSpawner.Instance.AliveEnemies)
            {
                if (h != null && !h.IsDead)
                {
                    targetBuffer.Add(new ObjectiveWaypointTarget(
                        h.transform,
                        worldOffset: new Vector3(0f, 1.8f, 0f),
                        customIcon: CleanupEnemySkullIcon,
                        tintColor: new Color(1f, 0.25f, 0.25f, 1f),
                        label: null
                    ));
                }
            }
        }

        // 2. Adjust pool size
        while (markerPool.Count < targetBuffer.Count)
        {
            ObjectiveWaypointMarkerUI newMarker = Instantiate(markerTemplate, markersContainer);
            newMarker.gameObject.SetActive(false);
            markerPool.Add(newMarker);
        }

        Vector3 playerPos = Player.Instance != null ? Player.Instance.transform.position : (mainCamera != null ? mainCamera.transform.position : Vector3.zero);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 3. Update active markers
        for (int i = 0; i < markerPool.Count; i++)
        {
            ObjectiveWaypointMarkerUI marker = markerPool[i];

            if (i < targetBuffer.Count)
            {
                ObjectiveWaypointTarget target = targetBuffer[i];
                if (target.Transform == null)
                {
                    marker.Unbind();
                    continue;
                }

                // Rebind when the target changes, or its label does (e.g. a plot going from BUILD to built)
                if (marker.TargetTransform != target.Transform || marker.CurrentLabel != target.Label)
                {
                    Sprite icon = ResolveIcon(target);
                    marker.Bind(target.Transform, target.WorldOffset, icon, target.TintColor, target.Label);
                }

                Vector3 targetWorldPos = target.Transform.position + target.WorldOffset;
                float distance = Vector3.Distance(playerPos, target.Transform.position);

                // Screen projection
                Vector3 screenPos = mainCamera.WorldToScreenPoint(targetWorldPos);
                bool isBehindCamera = screenPos.z < 0;

                Vector2 fromCenter = (Vector2)screenPos - screenCenter;

                // Offscreen clamping
                bool isOffScreen = isBehindCamera || 
                                   screenPos.x < screenEdgePadding.x || 
                                   screenPos.x > Screen.width - screenEdgePadding.x || 
                                   screenPos.y < screenEdgePadding.y || 
                                   screenPos.y > Screen.height - screenEdgePadding.y;

                float arrowAngle = 0f;
                Vector2 finalScreenPos;

                if (isOffScreen)
                {
                    // Clamp to edge
                    if (isBehindCamera)
                    {
                        fromCenter = -fromCenter;
                        if (fromCenter == Vector2.zero)
                        {
                            fromCenter = Vector2.up;
                        }
                    }

                    if (fromCenter == Vector2.zero)
                    {
                        fromCenter = Vector2.up;
                    }

                    arrowAngle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;

                    float halfW = Mathf.Max(10f, Screen.width * 0.5f - screenEdgePadding.x);
                    float halfH = Mathf.Max(10f, Screen.height * 0.5f - screenEdgePadding.y);

                    float scaleX = Mathf.Abs(fromCenter.x) > 0.001f ? halfW / Mathf.Abs(fromCenter.x) : float.MaxValue;
                    float scaleY = Mathf.Abs(fromCenter.y) > 0.001f ? halfH / Mathf.Abs(fromCenter.y) : float.MaxValue;
                    float scale = Mathf.Min(scaleX, scaleY);

                    finalScreenPos = screenCenter + fromCenter * scale;
                }
                else
                {
                    finalScreenPos = (Vector2)screenPos;
                    // Add subtle float bobbing
                    finalScreenPos.y += Mathf.Sin((Time.unscaledTime + i * 0.8f) * bobSpeed) * bobAmplitude;
                }

                // Convert screen point to canvas local point
                if (canvasRect != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, finalScreenPos, null, out Vector2 localPoint);
                    marker.UpdatePosition(localPoint, isOffScreen, arrowAngle, distance);
                }
            }
            else
            {
                marker.Unbind();
            }
        }
    }

    // Empty plots get a gold "BUILD" marker; built ones a plain marker (they can still be upgraded/refilled).
    // Depleted/hexed defences are skipped — they already get their NO SUPPLY / HEXED marker below.
    private void AddTowerPlotTargets(IReadOnlyList<TowerPlot> plots)
    {
        Sprite icon = towerPlotIcon != null ? towerPlotIcon : defaultObjectiveIcon;
        foreach (TowerPlot plot in plots)
        {
            if (plot == null || !plot.isActiveAndEnabled || !BuildMarkerFocus.Allows(plot)) continue;
            DefenseStructure def = plot.CurrentDefense;
            if (def != null && (def.IsDepleted || def.IsHexed)) continue;

            bool empty = !plot.IsOccupied && !plot.IsBuilding;
            targetBuffer.Add(new ObjectiveWaypointTarget(
                plot.transform,
                worldOffset: plot.BuildPosition - plot.transform.position + towerPlotOffset,
                customIcon: icon,
                tintColor: empty ? towerPlotEmptyTint : towerPlotBuiltTint,
                label: empty ? Loc.Get("wave.prep.plot", "BUILD") : null
            ));
        }
    }

    // Each wall plot's workbench gets the same treatment: gold "BUILD" while there's no standing wall
    // (empty or rubble), a plain marker once built (it still opens the upgrade wheel).
    private void AddWallPlotTargets(IReadOnlyList<WallPlot> plots)
    {
        Sprite icon = towerPlotIcon != null ? towerPlotIcon : defaultObjectiveIcon;
        foreach (WallPlot plot in plots)
        {
            if (plot == null || !plot.isActiveAndEnabled || plot.Station == null || !BuildMarkerFocus.Allows(plot)) continue;
            bool empty = !plot.HasStandingWall;
            targetBuffer.Add(new ObjectiveWaypointTarget(
                plot.Station.transform,
                worldOffset: towerPlotOffset,
                customIcon: icon,
                tintColor: empty ? towerPlotEmptyTint : towerPlotBuiltTint,
                label: empty ? Loc.Get("wave.prep.plot", "BUILD") : null
            ));
        }
    }

    /// <summary>The icon a target shows: its own, else one picked from its label (also used by the minimap).</summary>
    public Sprite ResolveIcon(ObjectiveWaypointTarget target)
    {
        if (target.CustomIcon != null) return target.CustomIcon;

        string label = target.Label != null ? target.Label.ToLower() : "";
        if (label.Contains("prisoner") || label.Contains("cage"))
        {
            return prisonerCageIcon != null ? prisonerCageIcon : defaultObjectiveIcon;
        }
        if (label.Contains("cart") || label.Contains("wagon"))
        {
            return supplyWagonIcon != null ? supplyWagonIcon : defaultObjectiveIcon;
        }
        if (label.Contains("gate"))
        {
            return destinationGateIcon != null ? destinationGateIcon : defaultObjectiveIcon;
        }
        if (label.Contains("slayer") || label.Contains("boss") || label.Contains("siegebreaker"))
        {
            return slayerBossIcon != null ? slayerBossIcon : defaultObjectiveIcon;
        }
        if (label.Contains("enemy") || label.Contains("skull") || label.Contains("straggler") || label.Contains("remaining"))
        {
            return CleanupEnemySkullIcon;
        }
        if (label.Contains("catapult") || label.Contains("siege") || label.Contains("ram"))
        {
            return siegeEngineIcon != null ? siegeEngineIcon : defaultObjectiveIcon;
        }
        if (label.Contains("supply") || label.Contains("ammo"))
        {
            return noSupplyIcon != null ? noSupplyIcon : defaultObjectiveIcon;
        }

        return defaultObjectiveIcon;
    }

    private void ValidateSprites()
    {
        if (defaultObjectiveIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] defaultObjectiveIcon is not assigned.", this);
        if (prisonerCageIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] prisonerCageIcon is not assigned.", this);
        if (supplyWagonIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] supplyWagonIcon is not assigned.", this);
        if (destinationGateIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] destinationGateIcon is not assigned.", this);
        if (slayerBossIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] slayerBossIcon is not assigned.", this);
        if (cleanupEnemySkullIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] cleanupEnemySkullIcon is not assigned.", this);
        if (siegeEngineIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] siegeEngineIcon is not assigned.", this);
        if (noSupplyIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] noSupplyIcon is not assigned.", this);
        if (arrowIcon == null) Debug.LogError("[ObjectiveWaypointTrackerUI] arrowIcon is not assigned.", this);
        if (iconBackground == null) Debug.LogError("[ObjectiveWaypointTrackerUI] iconBackground is not assigned.", this);
    }
}
