using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     A tower plot on the minimap: a faint ring while empty; once built, the tower's icon, an ammo ring
///     (red and pulsing at 0), and its element badge. Tooltip: what's built and its upgrades.
/// </summary>
public class MinimapTowerMarker : MinimapMarker
{
    [SerializeField] private Image plotRing;
    [SerializeField] private Image icon;
    [Tooltip("Radial fill showing remaining ammo.")]
    [SerializeField] private Image ammoFill;
    [SerializeField] private Image elementBadge;

    private TowerPlot plot;

    public TowerPlot Plot => plot;
    public override Vector3 WorldPosition => plot != null ? plot.BuildPosition : transform.position;

    public void Bind(TowerPlot towerPlot)
    {
        plot = towerPlot;
    }

    private void Start()
    {
        if (plotRing == null) Debug.LogError($"[MinimapTowerMarker] {name}: plotRing is not assigned.", this);
        if (icon == null) Debug.LogError($"[MinimapTowerMarker] {name}: icon is not assigned.", this);
    }

    public override void Refresh(MinimapConfigSO config)
    {
        if (plot == null || config == null || plotRing == null || icon == null) return;
        DefenseStructure tower = plot.CurrentDefense;
        bool built = tower != null;

        icon.enabled = built;
        if (built)
        {
            Sprite sprite = config.IconFor(tower.DefenseType);
            if (icon.sprite != sprite) icon.sprite = sprite;
        }

        bool dry = built && tower.IsDepleted;
        Color ring = !built ? config.emptyPlotColor : dry ? config.noSupplyColor : config.builtPlotColor;
        if (dry) ring.a *= 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 3f, 1f);
        plotRing.color = ring;

        if (ammoFill != null)
        {
            ammoFill.enabled = built;
            if (built) ammoFill.fillAmount = tower.MaxSupply > 0 ? Mathf.Clamp01((float)tower.CurrentSupply / tower.MaxSupply) : 0f;
        }

        if (elementBadge != null)
        {
            bool hasElement = built && tower.Upgrades.HasElement;
            elementBadge.enabled = hasElement;
            if (hasElement && DefenseSceneRules.Config != null) elementBadge.sprite = DefenseSceneRules.Config.ElementIcon(tower.Element);
            if (hasElement && ColorUtility.TryParseHtmlString(tower.Element.Hex(), out Color c)) elementBadge.color = c;
        }
    }

    public override void GetTooltip(out string title, out string body)
    {
        DefenseStructure tower = plot != null ? plot.CurrentDefense : null;
        if (tower == null)
        {
            title = Loc.Get("minimap.tower_plot_empty", "Empty Tower Plot");
            body = plot != null && plot.IsBuilding
                ? Loc.Get("minimap.tower_plot_building", "Under construction…")
                : Loc.Get("minimap.tower_plot_empty_body", "Build a tower here during the prep phase.");
            return;
        }

        title = tower.UpgradeTitle;
        var sb = new StringBuilder();
        if (tower.IsDepleted) sb.AppendLine($"<color=#FF5A4A>{Loc.Get("minimap.no_supply", "Out of ammo, refill it!")}</color>");
        if (tower.IsHexed) sb.AppendLine($"<color=#C890FF>{Loc.Get("minimap.hexed", "Hexed: can't fire")}</color>");
        StructureUpgradeState up = tower.Upgrades;
        if (up.fireRateTier > 0) sb.AppendLine(string.Format(Loc.Get("minimap.fire_rate", "Fire Rate {0}"), Roman(up.fireRateTier)));
        if (up.hasSpikes) sb.AppendLine(Loc.Get("minimap.spikes", "Spikes"));
        if (up.HasElement) sb.AppendLine($"<color={tower.Element.Hex()}>{ElementName(tower.Element)}</color>");
        if (sb.Length == 0) sb.Append(Loc.Get("minimap.no_upgrades", "No upgrades yet."));
        body = sb.ToString().TrimEnd();
    }

    internal static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString() };

    internal static string ElementName(StructureElement element) => element switch
    {
        StructureElement.Fire => Loc.Get("minimap.element_fire", "Fire"),
        StructureElement.Ice => Loc.Get("minimap.element_ice", "Ice"),
        StructureElement.Lightning => Loc.Get("minimap.element_storm", "Storm"),
        _ => ""
    };
}
