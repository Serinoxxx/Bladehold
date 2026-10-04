using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Selected/unselected look for one settings tab: background and label colours plus an underline
///     that eases its width in when the tab becomes active, on unscaled time. Driven by
///     <see cref="SettingsPanelView" />'s tab switching via <see cref="SetSelected" />; hover/press
///     juice comes from a sibling <see cref="UISelectableJuice" />. Colours are roles of the menu's
///     <see cref="UIThemeSO" />, so the tab follows theme swaps.
/// </summary>
public class SettingsTabButton : MonoBehaviour
{
    [SerializeField] private Graphic background;
    [SerializeField] private TMP_Text label;
    [Tooltip("Underline shown under the active tab; its X scale eases 0 → 1.")]
    [SerializeField] private RectTransform underline;
    [SerializeField] private UIColorRole backgroundSelected = UIColorRole.Parchment;
    [SerializeField] private UIColorRole backgroundUnselected = UIColorRole.Ghost;
    [SerializeField] private UIColorRole labelSelected = UIColorRole.TextOnParchment;
    [SerializeField] private UIColorRole labelUnselected = UIColorRole.Text;
    [SerializeField] private float easeSpeed = 14f;

    private bool selected;
    private float amount;
    private UIThemeSO theme;

    public void SetSelected(bool isSelected, bool instant = false)
    {
        selected = isSelected;
        if (instant)
        {
            amount = selected ? 1f : 0f;
            Apply();
        }
    }

    private void OnEnable()
    {
        UITheme.Changed -= HandleThemeChanged;
        UITheme.Changed += HandleThemeChanged;
        HandleThemeChanged();
    }

    private void OnDisable()
    {
        UITheme.Changed -= HandleThemeChanged;
    }

    private void HandleThemeChanged()
    {
        theme = UITheme.For(this);
        Apply();
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
        if (theme == null)
        {
            theme = UITheme.For(this);
        }
        if (background != null)
        {
            background.color = Color.Lerp(theme.Get(backgroundUnselected), theme.Get(backgroundSelected), amount);
        }
        if (label != null)
        {
            label.color = Color.Lerp(theme.Get(labelUnselected), theme.Get(labelSelected), amount);
        }
        if (underline != null)
        {
            underline.localScale = new Vector3(amount, 1f, 1f);
        }
    }
}
