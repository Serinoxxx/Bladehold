using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     One wall's HUD icon (plan 17, spawned by <see cref="WallStatusHUD" />): an HP fill, a flash while
///     it's being hit, a cross when breached or unbuilt, an open-door marker, and the plot number so it
///     matches the bridge. Polls its plot a few times a second (walls come and go with builds and rubble).
/// </summary>
public class WallStatusIcon : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Image frame;
    [SerializeField] private GameObject breachedMarker;
    [SerializeField] private GameObject openDoorMarker;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Color healthyColor = new Color(0.55f, 0.85f, 0.4f);
    [SerializeField] private Color damagedColor = new Color(0.95f, 0.75f, 0.25f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.3f, 0.25f);
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashSeconds = 1.5f;

    private WallPlot plot;
    private WallStructure boundWall;
    private float lastHealth = -1f;
    private float lastHitTime = -999f;
    private Color frameBase = Color.white;

    public void Bind(WallPlot owner)
    {
        plot = owner;
        if (frame != null) frameBase = frame.color;
        if (label != null) label.text = (owner.PlotIndex + 1).ToString();
        Refresh();
    }

    private void Start()
    {
        if (fill == null) Debug.LogError($"[WallStatusIcon] {name}: fill is not assigned.", this);
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (plot == null) return;
        WallStructure wall = plot.HasStandingWall ? plot.Wall : null;
        if (wall != boundWall)
        {
            boundWall = wall;
            lastHealth = -1f;
        }

        bool standing = wall != null;
        float frac = standing ? wall.HealthFraction : 0f;
        if (standing)
        {
            float hp = wall.Health.CurrentHealth;
            if (lastHealth >= 0f && hp < lastHealth) lastHitTime = Time.time;
            lastHealth = hp;
        }

        if (fill != null)
        {
            fill.fillAmount = frac;
            fill.color = frac > 0.5f ? healthyColor : frac > 0.25f ? damagedColor : criticalColor;
        }
        if (breachedMarker != null) breachedMarker.SetActive(!standing && plot.HasRubble);
        if (openDoorMarker != null) openDoorMarker.SetActive(standing && !wall.IsBlocking);
        if (frame != null)
        {
            float t = Time.time - lastHitTime;
            bool flashing = t < flashSeconds && Mathf.Repeat(t * 6f, 1f) < 0.5f;
            frame.color = flashing ? flashColor : frameBase;
        }

        // Unbuilt plots are dimmed so the row still shows where walls could go.
        float alpha = standing || plot.HasRubble ? 1f : 0.35f;
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = alpha;
    }
}
