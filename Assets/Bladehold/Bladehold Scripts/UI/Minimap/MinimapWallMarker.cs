using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     A wall plot on the minimap: a bar across the bridge, as wide as the plot, turned to match it.
///     Coloured by HP (dim outline while unbuilt, brown with a cross when breached), with a gap while its
///     door is open and a flash while it's being hit. Tooltip: tier, HP, door and upgrades.
/// </summary>
public class MinimapWallMarker : MinimapMarker
{
    [Tooltip("The bar itself; its width is set from the plot width.")]
    [SerializeField] private Image bar;
    [Tooltip("Left/right halves shown instead of the solid bar while the door is open.")]
    [SerializeField] private GameObject doorGap;
    [SerializeField] private Image[] doorGapHalves;
    [SerializeField] private GameObject breachedCross;
    [Tooltip("Height of the bar in canvas units (thickness on the map).")]
    [SerializeField] private float barThickness = 22f;

    private WallPlot plot;
    private WallStructure boundWall;
    private float lastHealth = -1f;
    private float lastHitTime = -999f;
    private float projectedAngle;

    public WallPlot Plot => plot;
    public override Vector3 WorldPosition => plot != null ? plot.transform.position : transform.position;

    public void Bind(WallPlot wallPlot, float uiAngle)
    {
        plot = wallPlot;
        projectedAngle = uiAngle;
        Rect.localEulerAngles = new Vector3(0f, 0f, projectedAngle);
    }

    private void Start()
    {
        if (bar == null) Debug.LogError($"[MinimapWallMarker] {name}: bar is not assigned.", this);
    }

    public override void ApplyScale(float pixelsPerMeter, float markerScale)
    {
        // Length follows the map (it's a real width); thickness follows the marker scale like an icon.
        float length = plot != null ? plot.Width * pixelsPerMeter : 40f;
        float minLength = owner != null && owner.Config != null ? owner.Config.minWallLength * markerScale : 0f;
        Rect.localScale = Vector3.one;
        Rect.sizeDelta = new Vector2(Mathf.Max(length, minLength), barThickness * markerScale);
    }

    public override void Refresh(MinimapConfigSO config)
    {
        if (plot == null || config == null || bar == null) return;
        WallStructure wall = plot.HasStandingWall ? plot.Wall : null;
        if (wall != boundWall)
        {
            boundWall = wall;
            lastHealth = -1f;
        }

        bool standing = wall != null;
        bool rubble = !standing && plot.HasRubble;
        bool doorOpen = standing && !wall.IsBlocking;

        Color c;
        if (standing)
        {
            float hp = wall.Health.CurrentHealth;
            if (lastHealth >= 0f && hp < lastHealth) lastHitTime = Time.unscaledTime;
            lastHealth = hp;
            c = config.WallColor(wall.HealthFraction);
            float t = Time.unscaledTime - lastHitTime;
            if (t < config.hitFlashSeconds && Mathf.Repeat(t * 6f, 1f) < 0.5f) c = config.hitFlash;
        }
        else
        {
            c = rubble ? config.wallRubble : config.wallUnbuilt;
        }

        // An unbuilt plot outside a tutorial lesson's focus (BuildMarkerFocus) hides its outline.
        bar.enabled = !doorOpen && (standing || rubble || BuildMarkerFocus.Allows(plot));
        bar.color = c;
        if (doorGap != null) doorGap.SetActive(doorOpen);
        if (doorGapHalves != null)
        {
            foreach (Image half in doorGapHalves)
            {
                if (half != null) half.color = c;
            }
        }
        if (breachedCross != null) breachedCross.SetActive(rubble);
    }

    public override void GetTooltip(out string title, out string body)
    {
        int number = plot != null ? plot.PlotIndex + 1 : 0;
        WallStructure wall = plot != null && plot.HasStandingWall ? plot.Wall : null;
        if (wall == null)
        {
            bool rubble = plot != null && plot.HasRubble;
            title = rubble
                ? string.Format(Loc.Get("minimap.wall_breached", "Wall {0}: Breached"), number)
                : string.Format(Loc.Get("minimap.wall_empty", "Wall Plot {0}"), number);
            body = rubble
                ? Loc.Get("minimap.wall_breached_body", "Rebuild it at its workbench during the prep phase.")
                : Loc.Get("minimap.wall_empty_body", "No wall. Build one at its workbench during the prep phase to reroute the horde.");
            return;
        }

        title = string.Format(Loc.Get("minimap.wall_title", "Wall {0}: {1}"), number, wall.UpgradeTitle);
        var sb = new StringBuilder();
        sb.AppendLine(wall.IsBlocking
            ? Loc.Get("minimap.door_shut", "Door shut: enemies route elsewhere or attack it.")
            : $"<color=#FFD24A>{Loc.Get("minimap.door_open", "Door open: enemies walk straight through!")}</color>");
        StructureUpgradeState up = wall.Upgrades;
        if (up.hasSpikes) sb.AppendLine(Loc.Get("minimap.spikes", "Spikes"));
        if (up.HasElement) sb.AppendLine($"<color={up.element.Hex()}>{MinimapTowerMarker.ElementName(up.element)}</color>");
        body = sb.ToString().TrimEnd();
    }
}
