using System;
using UnityEngine;

/// <summary>
///     Tunables for the battlefield minimap (<see cref="MinimapUI" />): expand/collapse animation, how the
///     map frame is fitted to the scene, the top-down snapshot, predicted-route drawing, enemy dot sizing
///     and the per-tower icons. One shared asset; the HUD prefab's minimap points at it.
/// </summary>
[CreateAssetMenu(fileName = "MinimapConfig", menuName = "Scriptable Objects/UI/Minimap Config")]
public class MinimapConfigSO : ScriptableObject
{
    [Serializable]
    public class TowerIcon
    {
        public FortDefenseType type;
        public Sprite icon;
    }

    [Serializable]
    public class LegendEntry
    {
        public string locKey;
        public string english;
        public Sprite icon;
        public Color color = Color.white;
        [Tooltip("Icon size in legend units (enemy dots use this to show small vs large).")]
        public float iconSize = 44f;
    }

    [Header("Expand / collapse")]
    [Tooltip("Expanded map size in canvas units (the HUD canvas is 3840x2160). Centred on screen.")]
    public float expandedSize = 1720f;
    [Tooltip("Expanded map centre offset from screen centre, in canvas units (room for the legend on the left).")]
    public Vector2 expandedOffset = new Vector2(180f, -20f);
    public float expandSeconds = 0.32f;
    public float collapseSeconds = 0.24f;
    [Tooltip("0..1 over the transition. Overshoot above 1 gives the expand a little settle.")]
    public AnimationCurve expandCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.6f), new Keyframe(0.75f, 1.035f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
    public AnimationCurve collapseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Backdrop alpha behind the expanded map.")]
    [Range(0f, 1f)] public float backdropAlpha = 0.55f;
    [Tooltip("Marker scale on the corner map vs the expanded map (icons are authored at the expanded size).")]
    public float collapsedMarkerScale = 0.55f;
    public float expandedMarkerScale = 1f;

    [Header("Map frame")]
    [Tooltip("Fit the map to the baked NavMesh as well as the gate, spawns and plots.")]
    public bool fitToNavMesh = true;
    [Tooltip("Empty margin round the fitted area, metres.")]
    public float paddingMeters = 10f;
    public float minMapMeters = 60f;
    public float maxMapMeters = 420f;
    [Tooltip("Turn the map so enemies come from the top and the gate sits at the bottom. Off = world +Z is up.")]
    public bool orientGateAtBottom = true;
    [Tooltip("Snap that orientation to the nearest 90° so the terrain snapshot stays axis-aligned.")]
    public bool snapOrientationTo90 = true;

    [Header("Terrain snapshot")]
    public int snapshotResolution = 1024;
    [Tooltip("Layers drawn into the top-down snapshot (leave out characters, UI, VFX).")]
    public LayerMask snapshotCullingMask = ~0;
    [Tooltip("Height above the highest NavMesh point the snapshot camera renders from.")]
    public float snapshotHeightAbove = 80f;
    public bool disableFogForSnapshot = true;
    [Tooltip("Multiplied over the snapshot (a warm, slightly dark wash keeps markers readable).")]
    public Color snapshotTint = new Color(0.78f, 0.74f, 0.66f, 1f);

    [Header("Predicted routes")]
    [Tooltip("NavMesh agent type the routes are predicted for (the horde).")]
    public string pathAgentTypeName = "Humanoid";
    [Tooltip("Routes are recomputed this often, and immediately whenever a wall/door changes the costs.")]
    public float pathRefreshSeconds = 0.75f;
    public float spawnSampleRadius = 8f;
    public float gateSampleRadius = 10f;
    [Tooltip("Spawn points closer than this share one marker and one route.")]
    public float spawnClusterMeters = 6f;
    public Color pathColor = new Color(1f, 0.42f, 0.28f, 0.9f);
    [Tooltip("Route width in canvas units on the corner map / the expanded map.")]
    public float pathWidthCollapsed = 18f;
    public float pathWidthExpanded = 26f;
    [Tooltip("Chevron spacing as a multiple of the route width.")]
    public float chevronSpacing = 1.35f;
    [Tooltip("Marching speed, chevrons per second.")]
    public float marchSpeed = 1.6f;

    [Header("Enemy dots")]
    [Tooltip("Dot diameter in canvas units per metre of NavMesh agent radius (Humanoid 0.5 m, Large Enemy 1 m).")]
    public float dotSizePerAgentRadius = 34f;
    public float minDotSize = 12f;
    public float maxDotSize = 54f;
    [Tooltip("Dots shrink to this fraction on the corner map.")]
    public float collapsedDotScale = 0.7f;
    public Color enemyColor = new Color(0.96f, 0.27f, 0.25f, 1f);
    public Color siegeColor = new Color(1f, 0.62f, 0.15f, 1f);
    public Color captainColor = new Color(0.78f, 0.35f, 1f, 1f);
    public Color dotOutlineColor = new Color(0.08f, 0.04f, 0.03f, 0.85f);
    [Tooltip("Outline thickness as a fraction of the dot size.")]
    public float dotOutlineFraction = 0.28f;
    [Tooltip("Seconds a new dot takes to pop in when an enemy spawns.")]
    public float spawnPopSeconds = 0.4f;
    public AnimationCurve spawnPopCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 5f), new Keyframe(0.55f, 1.45f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("Walls")]
    public Color wallHealthy = new Color(0.55f, 0.85f, 0.4f, 1f);
    public Color wallDamaged = new Color(0.95f, 0.75f, 0.25f, 1f);
    public Color wallCritical = new Color(0.95f, 0.3f, 0.25f, 1f);
    public Color wallUnbuilt = new Color(1f, 1f, 1f, 0.35f);
    public Color wallRubble = new Color(0.45f, 0.38f, 0.33f, 1f);
    public Color hitFlash = Color.white;
    public float hitFlashSeconds = 1.2f;
    [Tooltip("Minimum wall bar length in canvas units, so narrow walls still read on the corner map.")]
    public float minWallLength = 30f;

    [Header("Towers")]
    public TowerIcon[] towerIcons = Array.Empty<TowerIcon>();
    public Color emptyPlotColor = new Color(1f, 1f, 1f, 0.45f);
    public Color builtPlotColor = new Color(0.98f, 0.85f, 0.5f, 1f);
    public Color noSupplyColor = new Color(0.95f, 0.3f, 0.25f, 1f);

    [Header("Legend (expanded map)")]
    public LegendEntry[] legend = Array.Empty<LegendEntry>();

    public Sprite IconFor(FortDefenseType type)
    {
        foreach (TowerIcon entry in towerIcons)
        {
            if (entry != null && entry.type == type) return entry.icon;
        }
        return null;
    }

    public Color WallColor(float healthFraction)
    {
        return healthFraction > 0.5f ? wallHealthy : healthFraction > 0.25f ? wallDamaged : wallCritical;
    }
}
