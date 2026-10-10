using System;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Attached to the UI Card prefab for Survivors mode level-up card selection.
///     Encapsulates rendering skill info (icon, title, description, level badge),
///     handling card click events, and playing MMF_Player callouts on hover enter/exit.
/// </summary>
public class SurvivorsCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Card UI References")]
    [Tooltip("Primary button component on the card.")]
    [SerializeField] private Button selectButton;

    [Tooltip("Icon image component.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Title text component.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Description text component.")]
    [SerializeField] private TextMeshProUGUI descText;

    [Tooltip("Level badge text component.")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Theme colour for icons of cards with no element (weapon cards, duos). Elemental cards take their element's tint from SkillTreeIconsSO.")]
    [SerializeField] private UIColorRole defaultIconRole = UIColorRole.Accent;

    [Header("Banish UI Reference (optional)")]
    [Tooltip("Banish button above or on the card.")]
    [SerializeField] private Button banishButton;

    [Header("Feedbacks (optional)")]
    [Tooltip("MMF_Player played when pointer enters/hovers over the card.")]
    [SerializeField] private MMF_Player hoverEnterFeedback;

    [Tooltip("MMF_Player played when pointer exits/unhovers the card.")]
    [SerializeField] private MMF_Player hoverExitFeedback;

    [Tooltip("MMF_Player played when the card is selected/clicked.")]
    [SerializeField] private MMF_Player selectFeedback;

    private Action onClickedCallback;
    private Action onBanishCallback;
    private bool hovered;
    private bool pointerInside;
    private bool padFocused;
    private bool litWhenDisabled;

    private void Awake()
    {
        AutoWireReferences();
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }
        if (banishButton != null)
        {
            banishButton.onClick.RemoveAllListeners();
            banishButton.onClick.AddListener(HandleBanishClick);
        }
        ForceUnscaledTime(hoverEnterFeedback);
        ForceUnscaledTime(hoverExitFeedback);
        ForceUnscaledTime(selectFeedback);
    }

    private void OnValidate()
    {
        AutoWireReferences();
    }

    private static void ForceUnscaledTime(MMF_Player player)
    {
        if (player == null) return;
        player.ForceTimescaleMode = true;
        player.ForcedTimescaleMode = TimescaleModes.Unscaled;
        player.PlayerTimescaleMode = TimescaleModes.Unscaled;
    }

    /// <summary>
    /// Auto-wires component references based on standard Card prefab child object names.
    /// </summary>
    public void AutoWireReferences()
    {
        if (selectButton == null) selectButton = GetComponent<Button>();
        if (titleText == null) titleText = transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (levelText == null) levelText = transform.Find("LevelBadge")?.GetComponent<TextMeshProUGUI>();
        if (descText == null) descText = transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
        if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (banishButton == null) banishButton = transform.Find("Banish_Btn")?.GetComponent<Button>() ?? transform.Find("BanishButton")?.GetComponent<Button>();
        if (hoverEnterFeedback == null)
        {
            hoverEnterFeedback = GetComponent<MMF_Player>();
            if (hoverEnterFeedback == null)
            {
                hoverEnterFeedback = transform.Find("HoverEnterFeedback")?.GetComponent<MMF_Player>();
            }
        }
        if (hoverExitFeedback == null)
        {
            hoverExitFeedback = transform.Find("HoverExitFeedback")?.GetComponent<MMF_Player>();
        }
        if (selectFeedback == null)
        {
            selectFeedback = transform.Find("SelectFeedback")?.GetComponent<MMF_Player>();
        }
    }

    /// <summary>
    /// Populates the card with skill node data, level badge info, icon, click handler, and banish handler.
    /// </summary>
    public void SetData(SkillNode node, int currentLevel, Sprite icon, Action onClicked, Action onBanish = null, bool canBanish = false, Color? iconTint = null)
    {
        onClickedCallback = onClicked;
        onBanishCallback = onBanish;

        int nextLevel = currentLevel + 1;

        if (titleText != null)
        {
            titleText.text = node != null ? node.LocalizedDisplayName : "";
        }

        if (descText != null && node != null)
        {
            string body = currentLevel > 0 && !string.IsNullOrEmpty(node.LocalizedUpgradeText)
                ? node.LocalizedUpgradeText
                : node.LocalizedDescription;
            descText.text = body;
        }

        if (levelText != null && node != null)
        {
            levelText.text = currentLevel > 0
                ? $"Level {currentLevel} -> {nextLevel} (Max {node.maxLevel})"
                : $"Unlock Level 1 (Max {node.maxLevel})";
        }

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.color = iconTint ?? UITheme.For(this).Get(defaultIconRole);
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        if (banishButton != null)
        {
            banishButton.gameObject.SetActive(canBanish && onBanish != null);
            banishButton.interactable = canBanish;
        }
    }

    /// <summary>
    /// Enables or disables button interactability (e.g. for the 0.5s anti-accidental-click delay).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (selectButton != null)
        {
            selectButton.interactable = interactable;
        }
        if (banishButton != null)
        {
            banishButton.interactable = interactable;
        }
    }

    private void HandleBanishClick()
    {
        onBanishCallback?.Invoke();
    }

    // The hidden cursor still sits mid-screen under a pad, so it "hovers" the middle card; only a
    // pointer that keyboard/mouse is actually driving lights the card.
    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        SetLit(!InputDeviceWatcher.GamepadActive, padFocused);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        SetLit(false, padFocused);
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        bool gamepad = scheme == ControlScheme.Gamepad;
        SetLit(pointerInside && !gamepad, gamepad && padFocused);
    }

    /// <summary>
    ///     Pad focus lights the card exactly like a hover (glow fade-in, scale spring), so the focused draft is
    ///     obvious. A mouse click also selects the button; only pad focus counts, hover already covers the mouse.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        if (InputDeviceWatcher.GamepadActive)
        {
            SetLit(hovered, true);
        }
    }

    public void OnDeselect(BaseEventData eventData) => SetLit(hovered, false);

    private void OnDisable()
    {
        // The modal hides with the card still lit; its glow and scale are left mid-state, so the next
        // draft fades it out on enable rather than showing a stale highlight.
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        litWhenDisabled = hovered || padFocused;
        hovered = false;
        padFocused = false;
        pointerInside = false;
    }

    private void OnEnable()
    {
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        if (litWhenDisabled && hoverExitFeedback != null)
        {
            hoverExitFeedback.PlayFeedbacks();
        }
        litWhenDisabled = false;
    }

    /// <summary>Plays the hover enter/exit feedback only when the card goes from unlit to lit or back.</summary>
    private void SetLit(bool nowHovered, bool nowFocused)
    {
        bool wasLit = hovered || padFocused;
        hovered = nowHovered;
        padFocused = nowFocused;
        bool lit = hovered || padFocused;
        if (lit == wasLit)
        {
            return;
        }
        MMF_Player feedback = lit ? hoverEnterFeedback : hoverExitFeedback;
        if (feedback != null)
        {
            feedback.PlayFeedbacks();
        }
    }

    /// <summary>
    /// Triggers the card selection MMF_Player feedback (click sound, punch scale, flash, etc.).
    /// </summary>
    public void PlaySelectFeedback()
    {
        if (selectFeedback != null)
        {
            selectFeedback.PlayFeedbacks();
        }
    }

    private void HandleClick()
    {
        PlaySelectFeedback();
        onClickedCallback?.Invoke();
    }
}

