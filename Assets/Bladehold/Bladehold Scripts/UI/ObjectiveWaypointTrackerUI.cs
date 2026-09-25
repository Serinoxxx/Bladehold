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

    public Sprite CleanupEnemySkullIcon => cleanupEnemySkullIcon != null ? cleanupEnemySkullIcon : slayerBossIcon;

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


        // Also check for between-wave active upgrade powerup in arena
        if (GameLoopManager.Instance != null && GameLoopManager.Instance.ActivePowerup != null)
        {
            targetBuffer.Add(new ObjectiveWaypointTarget(
                GameLoopManager.Instance.ActivePowerup.transform,
                worldOffset: new Vector3(0f, 1.5f, 0f),
                customIcon: defaultObjectiveIcon,
                tintColor: new Color(1f, 0.85f, 0.2f, 1f),
                label: $"{GameLoopManager.Instance.ActivePowerup.BountyName}"
            ));
        }

        // Check for active banners during intermission
        if (GameLoopManager.Instance != null && GameLoopManager.Instance.IsIntermission && GameLoopManager.Instance.ActiveBanners != null)
        {
            foreach (var banner in GameLoopManager.Instance.ActiveBanners)
            {
                if (banner != null)
                {
                    targetBuffer.Add(new ObjectiveWaypointTarget(
                        banner.transform,
                        worldOffset: new Vector3(0f, 2.0f, 0f),
                        customIcon: destinationGateIcon,
                        tintColor: new Color(0.9f, 0.3f, 0.3f, 1f),
                        label: "War Banner"
                    ));
                }
            }
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

                // If marker was not bound to this target, bind it
                if (marker.TargetTransform != target.Transform)
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

    private Sprite ResolveIcon(ObjectiveWaypointTarget target)
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
