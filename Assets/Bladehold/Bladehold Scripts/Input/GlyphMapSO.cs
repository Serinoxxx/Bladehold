using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Maps Input System control paths to button-glyph sprites, one list per input family — the data
///     side of the button-prompt system (<see cref="InputGlyph" /> is the view). One asset is authored
///     in the inspector from the vendored Synty InterfaceCore icon library
///     (<c>Assets/Synty/InterfaceCore/Sprites/Icons_Input/Xbox</c> + <c>MouseKeyboard</c>): all
///     gamepads show Xbox-style glyphs, keyboard keys without a dedicated sprite render as
///     <see cref="blankKeycap" /> with the key name as a text overlay (so any rebind stays showable
///     without needing hundreds of keycap sprites).
///
///     Entry paths are matched against the <b>control name</b> of a binding's effective path — e.g.
///     authored path <c>&lt;Gamepad&gt;/rightTrigger</c> matches an entry named <c>rightTrigger</c>,
///     <c>&lt;Mouse&gt;/leftButton</c> matches <c>leftButton</c>. Composite parts resolve per-part.
/// </summary>
[CreateAssetMenu(fileName = "GlyphMapSO", menuName = "Scriptable Objects/GlyphMapSO")]
public class GlyphMapSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("Control name within the device, e.g. 'buttonSouth', 'rightTrigger', 'leftStick', 'leftButton' (mouse), 'space'.")]
        public string controlName;
        public Sprite sprite;
    }

    [Tooltip("Glyphs shown while a gamepad is the active device (Xbox style; all pad makes share these).")]
    [SerializeField] private Entry[] gamepadGlyphs;

    [Tooltip("Glyphs shown while keyboard/mouse is active (mouse buttons, arrows, and the few keys with dedicated art).")]
    [SerializeField] private Entry[] keyboardMouseGlyphs;

    [Tooltip("Fallback keycap frame for keyboard keys with no dedicated sprite — InputGlyph overlays the key's display name on it.")]
    [SerializeField] private Sprite blankKeycap;

    [Tooltip("Optional wider keycap for long key names (Space, Enter, PageDown …). Null = always use blankKeycap.")]
    [SerializeField] private Sprite blankKeycapWide;

    public Sprite BlankKeycap => blankKeycap;
    public Sprite BlankKeycapWide => blankKeycapWide != null ? blankKeycapWide : blankKeycap;

    private Dictionary<string, Sprite> gamepadByName;
    private Dictionary<string, Sprite> kbmByName;

    /// <summary>The sprite for a control name under the given family, or null when unmapped.</summary>
    public Sprite Resolve(ControlScheme scheme, string controlName)
    {
        if (string.IsNullOrEmpty(controlName))
        {
            return null;
        }

        EnsureIndexed();
        Dictionary<string, Sprite> table = scheme == ControlScheme.Gamepad ? gamepadByName : kbmByName;
        return table.TryGetValue(controlName, out Sprite sprite) ? sprite : null;
    }

    private void EnsureIndexed()
    {
        if (gamepadByName != null)
        {
            return;
        }
        gamepadByName = BuildIndex(gamepadGlyphs);
        kbmByName = BuildIndex(keyboardMouseGlyphs);
    }

    private static Dictionary<string, Sprite> BuildIndex(Entry[] entries)
    {
        var index = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        if (entries != null)
        {
            foreach (Entry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.controlName) && entry.sprite != null)
                {
                    index[entry.controlName.Trim()] = entry.sprite;
                }
            }
        }
        return index;
    }

    private void OnValidate()
    {
        // Re-index on inspector edits so play-mode tweaking shows up immediately.
        gamepadByName = null;
        kbmByName = null;
    }
}
