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
public class BuildWheelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    [SerializeField] private float unaffordableAlpha = 0.5f;

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

    public void SetAffordable(bool canAfford)
    {
        isAffordable = canAfford;
        if (button != null)
        {
            button.interactable = canAfford;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = canAfford ? 1f : unaffordableAlpha;
        }
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
}
