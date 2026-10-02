using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     A wall build spot across a bridge head (plan 17), placed by the defense-scene generator (one per
///     bridge) or by hand. Its footprint is baked onto its own NavMesh area "WallPlot{plotIndex}" (the
///     generator's <c>NavMeshModifierVolume</c>), which <see cref="WallNavCost" /> prices while a wall
///     stands. Local +Z faces the enemy.
///
///     The player builds, upgrades and deconstructs through the plot's <see cref="WallCraftingStation" />
///     on the castle side; the wall itself only carries the door. A fallen wall leaves rubble and the plot
///     can be rebuilt at wood tier during prep (upgrades are lost with it). Registered with
///     <see cref="TowerPlotManager" /> so the sector-end dismantle refunds standing walls.
/// </summary>
public class WallPlot : MonoBehaviour
{
    [SerializeField] private int plotIndex;
    [Tooltip("Wall length across the bridge, metres.")]
    [Min(4f)] [SerializeField] private float width = 10f;
    [SerializeField] private WallStructure wallPrefab;
    [SerializeField] private WallConfigSO wallConfig;
    [SerializeField] private WallCraftingStation station;
    [Tooltip("Shown while the plot is empty (stakes / chalk outline marking where the wall goes).")]
    [SerializeField] private GameObject emptyMarker;
    [Tooltip("Marker stakes moved to the wall's two ends (the plot's width varies per bridge).")]
    [SerializeField] private Transform markerLeft;
    [SerializeField] private Transform markerRight;
    [Tooltip("Played when a wall is built (hammering, dust).")]
    [SerializeField] private MMF_Player buildFeedback;
    [SerializeField] private DamageNumbersPro.DamageNumber popupPrefab;

    private WallStructure wall;
    private WallStructure rubble;
    private int navArea = -1;
    private bool anyError;

    public int PlotIndex => plotIndex;
    public float Width => width;
    public WallStructure Wall => wall;
    public bool HasStandingWall => wall != null && wall.IsStanding;
    public bool HasRubble => rubble != null;
    public static int BuildCost => DefenseSceneRules.Config != null ? DefenseSceneRules.Config.wallBuildCost : 40;

    /// <summary>NavMesh area name for plot <paramref name="index" /> (must match NavMeshAreas.asset).</summary>
    public static string AreaName(int index) => $"WallPlot{index}";

    private void OnValidate()
    {
        if (station == null) station = GetComponentInChildren<WallCraftingStation>(true);
    }

    private void Start()
    {
        if (wallPrefab == null) { Debug.LogError($"[WallPlot] {name}: wallPrefab is not assigned.", this); anyError = true; }
        if (wallConfig == null) { Debug.LogError($"[WallPlot] {name}: wallConfig is not assigned.", this); anyError = true; }
        if (station == null) { Debug.LogError($"[WallPlot] {name}: station is not assigned.", this); anyError = true; }
        if (buildFeedback == null) Debug.LogError($"[WallPlot] {name}: buildFeedback is not assigned.", this);
        if (popupPrefab == null) Debug.LogError($"[WallPlot] {name}: popupPrefab is not assigned.", this);

        navArea = NavMesh.GetAreaFromName(AreaName(plotIndex));
        if (navArea < 0) Debug.LogError($"[WallPlot] {name}: NavMesh area '{AreaName(plotIndex)}' doesn't exist; walls won't change enemy routing.", this);
        else WallNavCost.SetCost(navArea, 1f);

        if (station != null) station.Init(this);
        if (markerLeft != null) markerLeft.localPosition = new Vector3(-width * 0.5f, markerLeft.localPosition.y, markerLeft.localPosition.z);
        if (markerRight != null) markerRight.localPosition = new Vector3(width * 0.5f, markerRight.localPosition.y, markerRight.localPosition.z);
        if (TowerPlotManager.Instance != null) TowerPlotManager.Instance.RegisterWallPlot(this);
        RefreshMarker();
    }

    private void OnDestroy()
    {
        if (TowerPlotManager.Instance != null) TowerPlotManager.Instance.UnregisterWallPlot(this);
        if (navArea >= 0) WallNavCost.Clear(navArea);
    }

    /// <summary>Builds a wood wall (prep only, paid in supply). Clears any rubble first.</summary>
    public bool TryBuild()
    {
        if (anyError || HasStandingWall) return false;
        if (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsPrepPhase) return false;
        int cost = BuildCost;
        if (!RunSession.TrySpendInRunSupply(cost)) return false;

        BuildWall(cost);
        if (buildFeedback != null) buildFeedback.PlayFeedbacks(transform.position + Vector3.up);
        if (popupPrefab != null) popupPrefab.Spawn(transform.position + Vector3.up * 2.5f, $"-{cost} Supply");
        return true;
    }

    /// <summary>Spawns the wall without charging (tests and TryBuild).</summary>
    public WallStructure BuildWall(int buildCostPaid)
    {
        if (rubble != null) Destroy(rubble.gameObject);
        rubble = null;
        wall = Instantiate(wallPrefab, transform.position, transform.rotation, transform);
        wall.name = $"Wall_{plotIndex}";
        wall.Init(this, wallConfig, width, navArea, buildCostPaid);
        RefreshMarker();
        return wall;
    }

    /// <summary>The wall reached 0 HP: keep it as rubble until a rebuild.</summary>
    public void OnWallCollapsed(WallStructure fallen)
    {
        if (fallen != wall) return;
        rubble = wall;
        wall = null;
        RefreshMarker();
    }

    /// <summary>Upgrade-wheel Deconstruct: 100% refund (build, upgrades, repairs, crystals), then the plot is empty.</summary>
    public void DeconstructWall()
    {
        if (wall == null) return;
        int refund = wall.DeconstructRefund;
        string popup = wall.Upgrades.DescribeRefund(refund);
        RunSession.AddInRunSupply(refund);
        wall.Upgrades.RefundCrystals();
        if (popupPrefab != null && !string.IsNullOrEmpty(popup)) popupPrefab.Spawn(transform.position + Vector3.up * 3f, popup);
        Destroy(wall.gameObject);
        wall = null;
        RefreshMarker();
    }

    /// <summary>Sector end: a standing wall pays its full refund (crystals straight into RunSession). Returns the supply.</summary>
    public int DismantleForRefund()
    {
        int supply = 0;
        if (wall != null)
        {
            supply = wall.DeconstructRefund;
            wall.Upgrades.RefundCrystals();
        }
        ClearWall();
        return supply;
    }

    public void ClearWall()
    {
        if (wall != null) Destroy(wall.gameObject);
        if (rubble != null) Destroy(rubble.gameObject);
        wall = null;
        rubble = null;
        RefreshMarker();
    }

    private void RefreshMarker()
    {
        if (emptyMarker != null) emptyMarker.SetActive(wall == null && rubble == null);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = HasStandingWall ? Color.green : new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireCube(new Vector3(0f, 1.5f, 0f), new Vector3(width, 3f, 0.8f));
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 3f);
    }
#endif
}
