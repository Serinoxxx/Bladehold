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

        // Nested controls are mapped by their path under the device ("dpad/up"), so try that before the
        // last component alone ("up" never matches, which drew D-pad prompts as a blank keyboard keycap).
        Sprite sprite = null;
        if (glyphMap != null)
        {
            sprite = glyphMap.Resolve(scheme, ControlPathUnderDevice(path));
            if (sprite == null) sprite = glyphMap.Resolve(scheme, LastPathComponent(path));
        }
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

    /// <summary>"&lt;Gamepad&gt;/dpad/up" → "dpad/up"; a path without a device part comes back unchanged.</summary>
    private static string ControlPathUnderDevice(string path)
    {
        if (!path.StartsWith("<")) return path;
        int slash = path.IndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
    }

    /// <summary>
    ///     Turns a plain HUD <see cref="Image" /> into a live glyph for <paramref name="action" />, adding the
    ///     component (and a key-name overlay for keys without art) at runtime, so HUD slots authored with
    ///     fixed sprites follow device switches and rebinds without Editor wiring. Returns the glyph.
    /// </summary>
    public static InputGlyph AttachTo(Image target, InputAction action)
    {
        InputGlyph glyph = Prepare(target);
        if (glyph != null) glyph.SetAction(action);
        return glyph;
    }

    /// <summary><see cref="AttachTo(Image, InputAction)" /> for controls no action covers (see <see cref="SetPaths" />).</summary>
    public static InputGlyph AttachTo(Image target, string kbmPath, string gamepadPath)
    {
        InputGlyph glyph = Prepare(target);
        if (glyph != null) glyph.SetPaths(kbmPath, gamepadPath);
        return glyph;
    }

    private static InputGlyph Prepare(Image target)
    {
        if (target == null) return null;
        InputGlyph glyph = target.GetComponent<InputGlyph>();
        if (glyph == null) glyph = target.gameObject.AddComponent<InputGlyph>();
        if (glyph.image == null) glyph.image = target;
        if (glyph.glyphMap == null) glyph.glyphMap = FindLoadedGlyphMap();
        if (glyph.overlayText == null) glyph.overlayText = CreateOverlay(target.rectTransform);
        return glyph;
    }

    /// <summary>The project's glyph map, found among loaded assets (the HUD prefab references it).</summary>
    public static GlyphMapSO FindLoadedGlyphMap()
    {
        GlyphMapSO[] maps = Resources.FindObjectsOfTypeAll<GlyphMapSO>();
        return maps.Length > 0 ? maps[0] : null;
    }

    private static TMP_Text CreateOverlay(RectTransform parent)
    {
        var go = new GameObject("GlyphKeyName", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 3f);
        rt.offsetMax = new Vector2(-3f, -3f);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 6f;
        text.fontSizeMax = 22f;
        text.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        text.raycastTarget = false;
        // Match the authored glyphs' key-name font where one is loaded.
        foreach (InputGlyph other in Resources.FindObjectsOfTypeAll<InputGlyph>())
        {
            if (other.overlayText != null && other.overlayText != text && other.overlayText.font != null)
            {
                text.font = other.overlayText.font;
                break;
            }
        }
        go.SetActive(false);
        return text;
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
