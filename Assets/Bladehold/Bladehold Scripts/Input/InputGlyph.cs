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

    private InputAction action;
    private string fixedKbmPath;
    private string fixedGamepadPath;

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
        action = null;
        Refresh();
    }

    /// <summary>True when the current scheme has something to show — hint rows hide themselves when not.</summary>
    public bool HasBinding { get; private set; }

    private void HandleSchemeChanged(ControlScheme scheme) => Refresh();

    private void Refresh()
    {
        string fixedPath = InputDeviceWatcher.GamepadActive ? fixedGamepadPath : fixedKbmPath;
        string path = action != null ? ResolveActionPath() : fixedPath;
        HasBinding = !string.IsNullOrEmpty(path);
        if (!HasBinding)
        {
            SetVisual(null, null);
            return;
        }

        string controlName = LastPathComponent(path);
        Sprite sprite = glyphMap != null ? glyphMap.Resolve(InputDeviceWatcher.Current, controlName) : null;
        if (sprite != null)
        {
            SetVisual(sprite, null);
            return;
        }

        // No dedicated art: blank keycap + the control's short human-readable name ("F", "Left Shift").
        string label = InputControlPath.ToHumanReadableString(
            path, InputControlPath.HumanReadableStringOptions.OmitDevice);
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

    private static string LastPathComponent(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
    }

    private void SetVisual(Sprite sprite, string label)
    {
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
        if (overlayText != null)
        {
            overlayText.text = label ?? "";
            overlayText.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }
    }
}
