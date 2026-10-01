using System;
using UnityEngine;

/// <summary>
///     One line of tutorial guidance: a localized text plus the input it's about. The glyph comes from
///     <see cref="actionName" /> (a rebindable action on the player's Controls map, e.g. "Attack",
///     "Aim", "Interact", "StartWave"), or from fixed per-device paths when the input isn't a single
///     rebindable button (movement).
/// </summary>
[Serializable]
public class TutorialHint
{
    [Tooltip("Localization key (Strings.csv), e.g. tutorial.move.")]
    public string locKey;

    [TextArea] public string english;

    [Tooltip("Action on the player's Controls map whose glyph is shown (Attack, Aim, Interact, StartWave, Jump, …). Blank = use the paths below.")]
    public string actionName;

    [Tooltip("Fixed keyboard/mouse control path when actionName is blank, e.g. <Keyboard>/w.")]
    public string kbmPath;

    [Tooltip("Fixed gamepad control path when actionName is blank, e.g. <Gamepad>/leftStick.")]
    public string gamepadPath;

    public bool IsEmpty => string.IsNullOrEmpty(locKey) && string.IsNullOrEmpty(english);

    public string Text => Loc.Get(locKey, english);

    public bool HasGlyph => !string.IsNullOrEmpty(actionName) || !string.IsNullOrEmpty(kbmPath) || !string.IsNullOrEmpty(gamepadPath);

    public static TutorialHint Of(string locKey, string english, string actionName = null)
    {
        return new TutorialHint { locKey = locKey, english = english, actionName = actionName };
    }
}
