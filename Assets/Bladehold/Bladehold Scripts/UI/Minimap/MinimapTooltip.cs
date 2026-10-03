using TMPro;
using UnityEngine;

/// <summary>
///     The expanded minimap's hover card: heading + body beside the hovered marker, flipped to stay on
///     screen, faded in/out on unscaled time.
/// </summary>
public class MinimapTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [Tooltip("Gap between the marker and the card, canvas units.")]
    [SerializeField] private float offset = 70f;
    [SerializeField] private float fadeSeconds = 0.12f;

    private RectTransform target;
    private bool shown;
    private bool anyError;

    private void Start()
    {
        if (panel == null) { Debug.LogError($"[MinimapTooltip] {name}: panel is not assigned.", this); anyError = true; }
        if (group == null) { Debug.LogError($"[MinimapTooltip] {name}: group is not assigned.", this); anyError = true; }
        if (title == null || body == null) { Debug.LogError($"[MinimapTooltip] {name}: title/body are not assigned.", this); anyError = true; }
        if (!anyError) group.alpha = 0f;
    }

    public void Show(MinimapMarker marker)
    {
        if (anyError || marker == null) return;
        marker.GetTooltip(out string heading, out string text);
        title.text = heading;
        body.text = text;
        target = marker.Rect;
        shown = true;
        Place();
    }

    public void Hide()
    {
        shown = false;
        target = null;
    }

    private void LateUpdate()
    {
        if (anyError) return;
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, step);
        if (shown) Place();
    }

    private void Place()
    {
        if (target == null) return;
        RectTransform parent = (RectTransform)panel.parent;
        Vector3 world = target.TransformPoint(target.rect.center);
        Vector2 local = parent.InverseTransformPoint(world);

        // Prefer the right of the marker; flip left/down when the card would leave the screen.
        Rect area = parent.rect;
        Vector2 size = panel.rect.size;
        bool flipX = local.x + offset + size.x > area.xMax;
        bool flipY = local.y - size.y * 0.5f < area.yMin;
        panel.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 0.5f);
        panel.anchoredPosition = Vector2.zero;
        Vector2 pos = local + new Vector2(flipX ? -offset : offset, 0f);
        pos.y = Mathf.Clamp(pos.y, area.yMin + (flipY ? 0f : size.y * 0.5f), area.yMax - (flipY ? size.y : size.y * 0.5f));
        panel.localPosition = pos;
    }
}
