using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Draws a bright outline round a <see cref="Selectable" />'s target graphic while it is pad-focused
///     or hovered, for buttons whose art is too dark for a ColorTint selected state to read (the main
///     menu's title column). Added at runtime by the menus that need it; no Editor wiring.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIFocusOutline : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField] private Vector2 distance = new Vector2(3f, -3f);

    private Outline outline;
    private bool focused;
    private bool hovered;

    /// <summary>Adds (or returns) the outline on <paramref name="selectable" /> with the given colour.</summary>
    public static UIFocusOutline Attach(Selectable selectable, Color color)
    {
        if (selectable == null) return null;
        UIFocusOutline focus = selectable.GetComponent<UIFocusOutline>();
        if (focus == null) focus = selectable.gameObject.AddComponent<UIFocusOutline>();
        focus.color = color;
        if (focus.outline != null) focus.outline.effectColor = color;
        return focus;
    }

    private void Awake()
    {
        Selectable selectable = GetComponent<Selectable>();
        Graphic target = selectable.targetGraphic != null ? selectable.targetGraphic : GetComponent<Graphic>();
        if (target == null) return;
        outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = false;
        outline.enabled = false;
    }

    private void OnDisable()
    {
        focused = false;
        hovered = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData) { focused = true; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { focused = false; Refresh(); }
    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); }

    private void Refresh()
    {
        if (outline != null) outline.enabled = focused || hovered;
    }
}
