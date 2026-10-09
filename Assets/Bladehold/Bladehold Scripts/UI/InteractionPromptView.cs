using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     The HUD's interact prompt (<c>InteractionPrompt.prefab</c>): a backing plate with the Interact
///     action's glyph (follows the active device and rebinds) and the target's prompt text.
///     <see cref="PlayerInteraction" /> shows/hides it and calls <see cref="PlayDenied" /> when the
///     player presses interact on something they can't afford (<see cref="IAffordableInteractable" />).
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private InputGlyph glyph;
    [Tooltip("Action on the player's Controls map whose glyph is shown.")]
    [SerializeField] private string actionName = "Interact";
    [Tooltip("Played when the player can't afford the interaction: a soft denied sound and a red pulse of the plate. Unscaled time.")]
    [SerializeField] private MMF_Player deniedFeedback;

    private bool glyphBound;
    private bool anyError;

    private void OnValidate()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (glyph == null) glyph = GetComponentInChildren<InputGlyph>(true);
    }

    private void Start()
    {
        if (label == null)
        {
            Debug.LogError($"[InteractionPromptView] {name}: label is not assigned.", this);
            anyError = true;
        }
        if (glyph == null) Debug.LogError($"[InteractionPromptView] {name}: glyph is not assigned.", this);
        if (deniedFeedback == null) Debug.LogError($"[InteractionPromptView] {name}: deniedFeedback is not assigned.", this);

        TryBindGlyph();
    }

    public void Show(string text)
    {
        TryBindGlyph();
        gameObject.SetActive(true);
        if (!anyError && label != null && label.text != text) label.text = text;
        if (transform is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    public void Hide()
    {
        if (label != null) label.text = string.Empty;
        gameObject.SetActive(false);
    }

    public void PlayDenied()
    {
        if (deniedFeedback != null) deniedFeedback.PlayFeedbacks();
    }

    /// <summary>The player (and its Controls map) can spawn after the HUD, so bind on first show.</summary>
    private void TryBindGlyph()
    {
        if (glyphBound || glyph == null || Player.Instance == null || Player.Instance.InputSettings == null) return;
        InputActionMap map = Player.Instance.InputSettings.GetRebindableActionMap();
        InputAction action = map != null ? map.FindAction(actionName) : null;
        if (action == null) return;
        glyph.SetAction(action);
        glyphBound = true;
    }
}
