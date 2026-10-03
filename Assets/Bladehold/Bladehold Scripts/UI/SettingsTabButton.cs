using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Selected/unselected look for one settings tab: background and label colours plus an underline
///     that eases its width in when the tab becomes active, on unscaled time. Driven by
///     <see cref="SettingsPanelView" />'s tab switching via <see cref="SetSelected" />; hover/press
///     juice comes from a sibling <see cref="UISelectableJuice" />.
/// </summary>
public class SettingsTabButton : MonoBehaviour
{
    [SerializeField] private Graphic background;
    [SerializeField] private TMP_Text label;
    [Tooltip("Underline shown under the active tab; its X scale eases 0 → 1.")]
    [SerializeField] private RectTransform underline;
    [SerializeField] private Color backgroundSelected = new Color(0.831f, 0.776f, 0.639f, 1f);
    [SerializeField] private Color backgroundUnselected = new Color(0.165f, 0.145f, 0.125f, 0.9f);
    [SerializeField] private Color labelSelected = new Color(0.247f, 0.212f, 0.176f, 1f);
    [SerializeField] private Color labelUnselected = new Color(0.851f, 0.820f, 0.749f, 1f);
    [SerializeField] private float easeSpeed = 14f;

    private bool selected;
    private float amount;

    public void SetSelected(bool isSelected, bool instant = false)
    {
        selected = isSelected;
        if (instant)
        {
            amount = selected ? 1f : 0f;
            Apply();
        }
    }

    private void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Approximately(amount, target))
        {
            return;
        }
        amount = Mathf.MoveTowards(Mathf.Lerp(amount, target, 1f - Mathf.Exp(-easeSpeed * Time.unscaledDeltaTime)), target, 0.001f);
        Apply();
    }

    private void Apply()
    {
        if (background != null)
        {
            background.color = Color.Lerp(backgroundUnselected, backgroundSelected, amount);
        }
        if (label != null)
        {
            label.color = Color.Lerp(labelUnselected, labelSelected, amount);
        }
        if (underline != null)
        {
            underline.localScale = new Vector3(amount, 1f, 1f);
        }
    }
}
