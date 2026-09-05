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

    [Tooltip("Template GameObject for waypoint markers.")]
    [SerializeField] private ObjectiveWaypointMarkerUI markerTemplate;

    [Header("Default Visual Fallbacks")]
    [SerializeField] private Sprite defaultObjectiveIcon;
    [SerializeField] private Sprite prisonerCageIcon;
    [SerializeField] private Sprite supplyWagonIcon;
    [SerializeField] private Sprite destinationGateIcon;
    [SerializeField] private Sprite slayerBossIcon;
    [SerializeField] private Sprite siegeEngineIcon;
    [SerializeField] private Sprite arrowIcon;
    [SerializeField] private Sprite iconBackground;

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

        LoadDefaultSpritesIfMissing();
    }

    private void Start()
    {
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindFirstObjectByType<SurvivorsObjectiveManager>();
        }

        mainCamera = Camera.main;

        // Ensure marker template exists (or build one programmatically)
        EnsureMarkerTemplate();
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

        // Check for gate waypoint when round is finished and gate opens
        if (GameLoopManager.Instance != null && GameLoopManager.Instance.IsRestGateOpen && GameLoopManager.Instance.CastleGateTransform != null)
        {
            targetBuffer.Add(new ObjectiveWaypointTarget(
                GameLoopManager.Instance.CastleGateTransform,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: destinationGateIcon,
                tintColor: new Color(0.2f, 0.85f, 1f, 1f),
                label: "Return to Fortress"
            ));
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

        // Also check for endgame Siegebreaker boss
        var sgm = SurvivorsGameManager.Instance;
        if (sgm != null && sgm.HasSurvivedSiege && sgm.SpawnedSiegebreaker != null)
        {
            var bossHealth = sgm.SpawnedSiegebreaker.GetComponent<Health>();
            if (bossHealth != null && !bossHealth.IsDead)
            {
                targetBuffer.Add(new ObjectiveWaypointTarget(
                    sgm.SpawnedSiegebreaker.transform,
                    worldOffset: new Vector3(0f, 3.0f, 0f),
                    customIcon: slayerBossIcon,
                    tintColor: new Color(1f, 0.2f, 0.2f, 1f),
                    label: "Siegebreaker"
                ));
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
        if (label.Contains("catapult") || label.Contains("siege"))
        {
            return siegeEngineIcon != null ? siegeEngineIcon : defaultObjectiveIcon;
        }

        return defaultObjectiveIcon;
    }

    private void EnsureMarkerTemplate()
    {
        if (markerTemplate != null)
        {
            markerTemplate.gameObject.SetActive(false);
            return;
        }

        // Programmatically generate a complete, beautiful marker template under markersContainer
        GameObject templateGo = new GameObject("WaypointMarker_Template", typeof(RectTransform), typeof(CanvasGroup), typeof(ObjectiveWaypointMarkerUI));
        templateGo.transform.SetParent(markersContainer != null ? markersContainer : transform, false);

        RectTransform rootRt = templateGo.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(72f, 72f);

        CanvasGroup cg = templateGo.GetComponent<CanvasGroup>();
        cg.alpha = 0f;

        // 1. Background image (sibling 0: behind icon)
        GameObject bgGo = new GameObject("Icon_Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(templateGo.transform, false);
        bgGo.transform.SetSiblingIndex(0);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(64f, 64f);
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = iconBackground;
        bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f); // Subtle dark badge frame
        bgImg.raycastTarget = false;

        // 2. Icon image (sibling 1: in front of background)
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(templateGo.transform, false);
        iconGo.transform.SetSiblingIndex(1);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(40f, 40f);
        Image iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = defaultObjectiveIcon;
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;

        // 3. Directional arrow (sibling 2: for offscreen pointing)
        GameObject arrowGo = new GameObject("Arrow_Indicator", typeof(RectTransform), typeof(Image));
        arrowGo.transform.SetParent(templateGo.transform, false);
        arrowGo.transform.SetSiblingIndex(2);
        RectTransform arrowRt = arrowGo.GetComponent<RectTransform>();
        arrowRt.sizeDelta = new Vector2(28f, 28f);
        Image arrowImg = arrowGo.GetComponent<Image>();
        arrowImg.sprite = arrowIcon;
        arrowImg.raycastTarget = false;
        arrowGo.SetActive(false);

        // 4. Distance text (sibling 3: below marker)
        GameObject textGo = new GameObject("Distance_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(templateGo.transform, false);
        textGo.transform.SetSiblingIndex(3);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(100f, 26f);
        textRt.anchoredPosition = new Vector2(0f, -42f);
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // Wire references directly at runtime — works in both Editor and Standalone Builds
        markerTemplate = templateGo.GetComponent<ObjectiveWaypointMarkerUI>();
        markerTemplate.SetupReferences(rootRt, cg, bgImg, iconImg, arrowImg, tmp);

        templateGo.SetActive(false);
    }

    private void LoadDefaultSpritesIfMissing()
    {
#if UNITY_EDITOR
        if (defaultObjectiveIcon == null)
            defaultObjectiveIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_FantasyWarrior_Map_Objective_01_Clean.png");
        if (prisonerCageIcon == null)
            prisonerCageIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Lock_01_Clean.png");
        if (supplyWagonIcon == null)
            supplyWagonIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Treasure_01_Clean.png");
        if (destinationGateIcon == null)
            destinationGateIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Flag_01_Clean.png");
        if (slayerBossIcon == null)
            slayerBossIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Skull_01_Clean.png");
        if (siegeEngineIcon == null)
            siegeEngineIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Target_01_Clean.png");
        if (arrowIcon == null)
            arrowIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow_01_Clean.png");
        if (iconBackground == null)
            iconBackground = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Map_IconBackground_01_Clean.png");
#endif
    }
}
