using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     One acquired-skill row of <see cref="SurvivorsPlayerInfoSidebarUI" />: makes the row inspectable.
///     Mouse hover or gamepad focus highlights the row and shows its <see cref="SidebarSkillTooltipUI" />
///     beside it; leaving (or losing focus) hides it. The row's <see cref="Selectable" /> (no transition,
///     automatic navigation) is what lets the pad reach the list from the draft cards. Added and bound in
///     code by the sidebar, so the row prefab needs no wiring.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class SidebarSkillEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private SidebarSkillTooltipUI tooltip;
    private SidebarSkillTooltipUI.Content content;
    private Image background;
    private Color baseColor;
    private Color highlightColor;
    private bool bound;

    /// <summary>Wires the row to <paramref name="skillTooltip" />; <paramref name="rowBackground" /> tints to <paramref name="highlight" /> while inspected.</summary>
    public void Bind(SidebarSkillTooltipUI skillTooltip, SidebarSkillTooltipUI.Content tooltipContent, Image rowBackground, Color highlight)
    {
        tooltip = skillTooltip;
        content = tooltipContent;
        background = rowBackground;
        if (background != null) baseColor = background.color;
        highlightColor = highlight;
        bound = true;
    }

    public void OnPointerEnter(PointerEventData eventData) => Inspect(true);

    public void OnPointerExit(PointerEventData eventData) => Inspect(false);

    public void OnSelect(BaseEventData eventData) => Inspect(true);

    public void OnDeselect(BaseEventData eventData) => Inspect(false);

    private void OnDisable()
    {
        if (bound) Inspect(false);
    }

    private void Inspect(bool on)
    {
        if (!bound) return;
        if (background != null) background.color = on ? highlightColor : baseColor;
        if (tooltip == null) return;
        if (on) tooltip.Show(content, (RectTransform)transform, this);
        else tooltip.Hide(this);
    }
}
