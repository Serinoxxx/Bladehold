using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Shows the button glyph for one <see cref="InputAction" /> under the currently active input
///     family — the view side of the button-prompt system (<see cref="GlyphMapSO" /> is the data).
///     Resolution order: pick the action's binding for the active family (by binding group, so the
///     Synty Controls asset's "Keyboard and Mouse"/"Gamepad" schemes route correctly), read its
///     <b>effective</b> path (which honors the player's rebind overrides), and look the control name
///     up in the glyph map. A mapped control shows its sprite alone; an unmapped keyboard control
///     shows the blank keycap with the key's short display name overlaid, so any rebind renders.
///     Re-resolves on <see cref="InputDeviceWatcher.SchemeChanged" /> and
///     <see cref="InputDeviceWatcher.BindingsChanged" />, so prompts flip live between LMB/RT etc.
/// </summary>
public class InputGlyph : MonoBehaviour
{
    [Tooltip("Glyph sprite table (Synty InterfaceCore icons). Falls back to text-only when unassigned.")]
    [SerializeField] private GlyphMapSO glyphMap;
    [Tooltip("Image displaying the glyph sprite (or the blank keycap under overlay text).")]
    [SerializeField] private Image image;
    [Tooltip("Text overlaid on the blank keycap for controls without a dedicated sprite (e.g. rebound keys).")]
    [SerializeField] private TMP_Text overlayText;
    [Tooltip("Optional: when set, keys without dedicated art draw on this 9-sliced keycap instead of the map's blank keycaps, and the glyph widens to fit the key name (the settings rebind grid, where 'Left Shift' must stay legible).")]
    [SerializeField] private Sprite stretchKeycap;
    [Tooltip("Horizontal padding (px) around the key name on the stretch keycap.")]
    [SerializeField] private float stretchPadding = 16f;

    private InputAction action;
    private string fixedKbmPath;
    private string fixedGamepadPath;
    private int fixedBindingIndex = -1;
    private ControlScheme fixedScheme;

    private void OnValidate()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
        if (overlayText == null)
        {
            overlayText = GetComponentInChildren<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        InputDeviceWatcher.BindingsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        InputDeviceWatcher.BindingsChanged -= Refresh;
    }

    /// <summary>Points this glyph at an action; it re-resolves itself on scheme/binding changes from then on.</summary>
    public void SetAction(InputAction newAction)
    {
        action = newAction;
        fixedKbmPath = null;
        fixedGamepadPath = null;
        fixedBindingIndex = -1;
        Refresh();
    }

    /// <summary>
    ///     Pins this glyph to one binding of an action, always drawn in the given family regardless of
    ///     the active device — for the settings rebind grid, whose Keyboard/Mouse and Gamepad columns
    ///     show side by side. Still follows the binding's rebind overrides. Pass index -1 to show nothing.
    /// </summary>
    public void SetBinding(InputAction newAction, int bindingIndex, ControlScheme scheme)
    {
        action = bindingIndex >= 0 ? newAction : null;
        fixedKbmPath = null;
        fixedGamepadPath = null;
        fixedBindingIndex = bindingIndex;
        fixedScheme = scheme;
        Refresh();
    }

    /// <summary>
    ///     Points this glyph at fixed per-family control paths (for hints no rebindable action covers,
    ///     e.g. skill-tree pan on "&lt;Gamepad&gt;/rightStick" vs mouse drag). Either may be null/empty
    ///     to mean "no prompt in that family" — the entry hides itself there.
    /// </summary>
    public void SetPaths(string kbmPath, string gamepadPath)
    {
        fixedKbmPath = kbmPath;
        fixedGamepadPath = gamepadPath;
        fixedBindingIndex = -1;
        action = null;
        Refresh();
    }

    /// <summary>True when the current scheme has something to show — hint rows hide themselves when not.</summary>
    public bool HasBinding { get; private set; }

    private void HandleSchemeChanged(ControlScheme scheme) => Refresh();

    /// <summary>Re-resolves the glyph now, e.g. right after a rebind finishes.</summary>
    public void Refresh()
    {
        bool pinned = action != null && fixedBindingIndex >= 0;
        ControlScheme scheme = pinned ? fixedScheme : InputDeviceWatcher.Current;
        string fixedPath = scheme == ControlScheme.Gamepad ? fixedGamepadPath : fixedKbmPath;
        string path;
        if (pinned)
        {
            path = fixedBindingIndex < action.bindings.Count ? action.bindings[fixedBindingIndex].effectivePath : null;
        }
        else
        {
            path = action != null ? ResolveActionPath() : fixedPath;
        }
        HasBinding = !string.IsNullOrEmpty(path);
        if (!HasBinding)
        {
            SetVisual(null, null);
            return;
        }

        string controlName = LastPathComponent(path);
        Sprite sprite = glyphMap != null ? glyphMap.Resolve(scheme, controlName) : null;
        if (sprite != null)
        {
            SetVisual(sprite, null);
            return;
        }

        // No dedicated art: blank keycap + the control's short human-readable name ("F", "Left Shift").
        // Prompts drop the modifier's side; the rebind grid (pinned) keeps it, since that's the actual binding.
        string label = InputControlPath.ToHumanReadableString(
            path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        if (!pinned) label = ShortLabel(label);
        if (stretchKeycap != null)
        {
            SetVisual(stretchKeycap, label, stretch: true);
            return;
        }
        Sprite keycap = glyphMap != null
            ? (label.Length > 2 ? glyphMap.BlankKeycapWide : glyphMap.BlankKeycap)
            : null;
        SetVisual(keycap, label);
    }

    /// <summary>The effective (override-honoring) path of this action's binding in the active family, or null.</summary>
    private string ResolveActionPath()
    {
        if (action == null)
        {
            return null;
        }

        string group = InputDeviceWatcher.GamepadActive ? "Gamepad" : "Keyboard and Mouse";
        int index = action.GetBindingIndex(InputBinding.MaskByGroup(group));
        if (index < 0)
        {
            // Code-built maps (MenuInputActions) have no groups — fall back to matching by device prefix.
            index = FindBindingByDevice(InputDeviceWatcher.GamepadActive);
        }
        return index >= 0 ? action.bindings[index].effectivePath : null;
    }

    private int FindBindingByDevice(bool gamepad)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite)
            {
                continue;
            }
            string path = binding.effectivePath ?? "";
            bool isGamepad = path.StartsWith("<Gamepad>") || path.StartsWith("<Joystick>");
            if (isGamepad == gamepad)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>"Left Shift" → "Shift", "Right Ctrl" → "Ctrl": the side of a modifier is noise on a prompt keycap.</summary>
    private static string ShortLabel(string label)
    {
        foreach (string side in new[] { "Left ", "Right " })
        {
            if (!label.StartsWith(side)) continue;
            string key = label.Substring(side.Length);
            if (key == "Shift" || key == "Ctrl" || key == "Control" || key == "Alt") return key;
        }
        return label;
    }

    private static string LastPathComponent(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
    }

    private void SetVisual(Sprite sprite, string label, bool stretch = false)
    {
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (stretchKeycap != null)
            {
                image.type = stretch ? Image.Type.Sliced : Image.Type.Simple;
                image.preserveAspect = !stretch;
            }
        }
        if (overlayText != null)
        {
            overlayText.text = label ?? "";
            overlayText.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }
        if (stretchKeycap != null)
        {
            FitWidth(stretch ? label : null);
        }
    }

    /// <summary>Square for sprite glyphs; as wide as the key name (never narrower than square) on the stretch keycap.</summary>
    private void FitWidth(string label)
    {
        var rt = (RectTransform)transform;
        float height = rt.rect.height > 0f ? rt.rect.height : rt.sizeDelta.y;
        float width = height;
        if (!string.IsNullOrEmpty(label) && overlayText != null)
        {
            width = Mathf.Max(height, overlayText.GetPreferredValues(label, 9999f, height).x + stretchPadding);
        }
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        var layout = GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = width;
        }
    }
}
