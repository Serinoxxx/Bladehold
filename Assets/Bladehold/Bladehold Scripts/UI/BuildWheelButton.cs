using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Controller component attached to each slice button in the radial Build Wheel.
///     Manages the defense icon, title text, supply cost badge, affordability visual states,
///     and pointer hover/click interactions.
/// </summary>
public class BuildWheelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Core References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;

    [Header("Visual Styling")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image traceryImage;
    [SerializeField] private GameObject highlightGlow;

    [Header("Colors")]
    [SerializeField] private Color normalCostColor = new Color(1f, 0.85f, 0.2f, 1f); // Gold
    [SerializeField] private Color unaffordableCostColor = new Color(1f, 0.3f, 0.3f, 1f); // Red
    [Tooltip("Unaffordable slices darken their icon and tracery by this much instead of fading, so the red cost stays readable.")]
    [Range(0f, 1f)]
    [SerializeField] private float unaffordableDim = 0.7f;

    private Action onClickCallback;
    private Action onHoverCallback;
    private Action onUnhoverCallback;
    private bool isAffordable = true;

    public Button Button => button != null ? button : (button = GetComponent<Button>());
    public Image IconImage => iconImage;
    public TMP_Text TitleText => titleText;
    public TMP_Text CostText => costText;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    public void Setup(
        string displayName,
        int supplyCost,
        Sprite icon,
        bool canAfford,
        Action onClick,
        Action onHover = null,
        Action onUnhover = null)
    {
        onClickCallback = onClick;
        onHoverCallback = onHover;
        onUnhoverCallback = onUnhover;

        if (titleText != null)
        {
            titleText.text = displayName;
        }

        if (costText != null)
        {
            string colorHex = canAfford ? "#FFD700" : "#FF5555";
            costText.text = $"<color={colorHex}>{supplyCost} Supply</color>";
        }

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        SetAffordable(canAfford);
    }

    /// <summary>Upgrade-wheel slice (plan 17): the cost line is pre-formatted rich text (supply and/or crystals, or a blocked reason).</summary>
    public void Setup(
        string displayName,
        string costRichText,
        Sprite icon,
        bool available,
        Action onClick,
        Action onHover = null,
        Action onUnhover = null)
    {
        onClickCallback = onClick;
        onHoverCallback = onHover;
        onUnhoverCallback = onUnhover;

        if (titleText != null) titleText.text = displayName;
        if (costText != null) costText.text = costRichText;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        SetAffordable(available);
    }

    /// <summary>
    ///     Resets the click wiring to this button's own callback. The build wheel's legacy slice-button
    ///     path clears the same Button's listeners, so the upgrade wheel calls this before reusing it.
    /// </summary>
    public void RebindClick()
    {
        Button b = Button;
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(HandleClick);
    }

    public void SetAffordable(bool canAfford)
    {
        isAffordable = canAfford;
        if (button != null)
        {
            button.interactable = canAfford;
        }

        // Darken rather than fade: the title and red cost keep full opacity. The slice background
        // darkens through the Button's disabled tint. The dim goes on the CanvasRenderer, not
        // Image.color, so slices cloned from a dimmed template don't inherit it.
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        Color dim = canAfford ? Color.white : new Color(unaffordableDim, unaffordableDim, unaffordableDim, 1f);
        if (iconImage != null) iconImage.canvasRenderer.SetColor(dim);
        if (traceryImage != null) traceryImage.canvasRenderer.SetColor(dim);
    }

    private void HandleClick()
    {
        if (isAffordable)
        {
            onClickCallback?.Invoke();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightGlow != null) highlightGlow.SetActive(true);
        onHoverCallback?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightGlow != null) highlightGlow.SetActive(false);
        onUnhoverCallback?.Invoke();
    }

    // Pad focus drives the details panel the same way the mouse does.
    public void OnSelect(BaseEventData eventData)
    {
        if (highlightGlow != null) highlightGlow.SetActive(true);
        onHoverCallback?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (highlightGlow != null) highlightGlow.SetActive(false);
        onUnhoverCallback?.Invoke();
    }
}
