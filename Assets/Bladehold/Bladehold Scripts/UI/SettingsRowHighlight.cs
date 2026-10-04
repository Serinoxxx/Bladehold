using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Lights up one settings row (label + its control) while the pointer is over it or pad focus is
///     on any control inside it: fades in a row background and a left accent bar and recolours the
///     label, on unscaled time so it animates on the paused menu. Clicking the row's empty space
///     focuses its main control, so the whole row is a target, not just the slider/toggle itself.
///     Pure presentation — the row's controls are wired by <see cref="SettingsPanelView" />. Label
///     colours are roles of the menu's <see cref="UIThemeSO" />; the background and accent take their
///     colour from their own <see cref="UIThemedGraphic" /> (this only fades their alpha).
/// </summary>
public class SettingsRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("Row background, faded from 0 to its authored alpha when lit.")]
    [SerializeField] private Graphic background;
    [Tooltip("Optional accent bar at the row's left edge, faded with the background.")]
    [SerializeField] private Graphic accent;
    [SerializeField] private TMP_Text label;
    [SerializeField] private UIColorRole labelColor = UIColorRole.Text;
    [SerializeField] private UIColorRole labelLitColor = UIColorRole.Accent;
    [Tooltip("Control focused when the row's empty space is clicked; also its toggle flips on that click.")]
    [SerializeField] private Selectable mainControl;
    [SerializeField] private float easeSpeed = 16f;

    private bool hovered;
    private float lit;
    private float backgroundMaxAlpha = 1f;
    private float accentMaxAlpha = 1f;
    private UIThemeSO theme;

    /// <summary>Set by the editor builder; exposed so code-built rows can be configured too.</summary>
    public void Configure(Graphic rowBackground, Graphic rowAccent, TMP_Text rowLabel, Selectable control)
    {
        background = rowBackground;
        accent = rowAccent;
        label = rowLabel;
        mainControl = control;
    }

    private void Awake()
    {
        // The authored alphas are the lit look; rows start unlit.
        if (background != null)
        {
            backgroundMaxAlpha = background.color.a;
        }
        if (accent != null)
        {
            accentMaxAlpha = accent.color.a;
        }
        Apply(0f);
    }

    private void OnEnable()
    {
        UITheme.Changed -= HandleThemeChanged;
        UITheme.Changed += HandleThemeChanged;
        HandleThemeChanged();
    }

    private void HandleThemeChanged()
    {
        theme = UITheme.For(this);
        Apply(lit);
    }

    private void OnDisable()
    {
        UITheme.Changed -= HandleThemeChanged;
        hovered = false;
        lit = 0f;
        Apply(0f);
    }

    private void Update()
    {
        bool focusedInside = false;
        if (InputDeviceWatcher.GamepadActive && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            focusedInside = selected != null && selected.transform.IsChildOf(transform);
        }

        float target = hovered || focusedInside ? 1f : 0f;
        lit = Mathf.Lerp(lit, target, 1f - Mathf.Exp(-easeSpeed * Time.unscaledDeltaTime));
        Apply(lit);
    }

    private void Apply(float amount)
    {
        if (background != null)
        {
            Color c = background.color;
            c.a = backgroundMaxAlpha * amount;
            background.color = c;
        }
        if (accent != null)
        {
            Color c = accent.color;
            c.a = accentMaxAlpha * amount;
            accent.color = c;
        }
        if (label != null)
        {
            if (theme == null)
            {
                theme = UITheme.For(this);
            }
            label.color = Color.Lerp(theme.Get(labelColor), theme.Get(labelLitColor), amount);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only clicks on the row's own background land here — controls swallow their own clicks.
        if (mainControl == null || !mainControl.IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        if (mainControl is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
        }
        mainControl.Select();
    }
}
