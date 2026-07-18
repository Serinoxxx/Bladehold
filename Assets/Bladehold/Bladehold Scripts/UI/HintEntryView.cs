using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     One row of a <see cref="ControlHintBar" />: a button glyph (<see cref="InputGlyph" />) beside a
///     localized action label ("Attack", "Pan", …). Hides itself when the glyph has nothing to show
///     for the active input family (e.g. a gamepad-only hint while on keyboard/mouse). Pure display;
///     the owning bar decides what it binds to and re-binds it on scheme/binding changes.
/// </summary>
public class HintEntryView : MonoBehaviour
{
    [SerializeField] private InputGlyph glyph;
    [SerializeField] private TMP_Text label;

    private void OnValidate()
    {
        if (glyph == null)
        {
            glyph = GetComponentInChildren<InputGlyph>();
        }
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>();
        }
    }

    /// <summary>Binds to a rebindable action; the glyph follows overrides and device switches on its own.</summary>
    public void Bind(InputAction action, string locKey, string englishFallback)
    {
        if (glyph != null)
        {
            glyph.SetAction(action);
        }
        SetLabel(locKey, englishFallback);
        UpdateVisibility();
    }

    /// <summary>Binds to fixed per-family control paths (hints not backed by a rebindable action).</summary>
    public void Bind(string kbmPath, string gamepadPath, string locKey, string englishFallback)
    {
        if (glyph != null)
        {
            glyph.SetPaths(kbmPath, gamepadPath);
        }
        SetLabel(locKey, englishFallback);
        UpdateVisibility();
    }

    /// <summary>Hides the row when the active family has no binding to show. Called by the owning bar on refresh.</summary>
    public void UpdateVisibility()
    {
        bool show = glyph != null && glyph.HasBinding;
        if (gameObject.activeSelf != show)
        {
            gameObject.SetActive(show);
        }
    }

    private void SetLabel(string locKey, string englishFallback)
    {
        if (label != null)
        {
            label.text = Loc.Get(locKey, englishFallback);
        }
    }
}
