using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
///     Base for anything placed on the minimap at a world position (gate, tower plot, wall, spawn). The
///     owning <see cref="MinimapUI" /> anchors it in normalised map space and scales it between the corner
///     and expanded sizes; hoverable markers feed the tooltip on the expanded map (mouse hover, or the
///     gamepad d-pad cycling through them).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class MinimapMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Shown while this marker is hovered/selected on the expanded map.")]
    [SerializeField] protected GameObject highlight;

    protected MinimapUI owner;
    private RectTransform rect;

    public RectTransform Rect => rect != null ? rect : (rect = (RectTransform)transform);

    /// <summary>World position this marker sits at.</summary>
    public abstract Vector3 WorldPosition { get; }

    /// <summary>False for markers without a tooltip.</summary>
    public virtual bool Hoverable => true;

    public void Attach(MinimapUI map)
    {
        owner = map;
        SetHighlighted(false);
    }

    /// <summary>Polled by the owner a few times a second (and every frame while hovered).</summary>
    public abstract void Refresh(MinimapConfigSO config);

    /// <summary>Tooltip heading and body (rich text allowed).</summary>
    public abstract void GetTooltip(out string title, out string body);

    /// <summary>Called by the owner when the map's pixels-per-metre or marker scale changes.</summary>
    public virtual void ApplyScale(float pixelsPerMeter, float markerScale)
    {
        Rect.localScale = new Vector3(markerScale, markerScale, 1f);
    }

    public void SetHighlighted(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null && Hoverable) owner.SetHovered(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null) owner.ClearHovered(this);
    }
}
