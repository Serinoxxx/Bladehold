using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Reusable UI card for a single permanent meta upgrade in the Main Menu upgrades screen.
    ///     Displays a bold centered icon, title, level badge, and buy button.
    ///     Shows detailed skill descriptions via hover tooltip.
    /// </summary>
    public class MetaSkillCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text levelBadgeText;
        [SerializeField] private Image dividerImage;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Image buyButtonBackground;
        [SerializeField] private TMP_Text buyButtonText;

        [Header("Theme Colors")]
        [SerializeField] private Color parchmentNormal = new Color(0.773f, 0.745f, 0.659f, 1f);
        [SerializeField] private Color parchmentDimmed = new Color(0.48f, 0.45f, 0.40f, 0.95f);
        [SerializeField] private Color textDarkBrown = new Color(0.24f, 0.17f, 0.11f, 1f);
        [SerializeField] private Color textBodyBrown = new Color(0.32f, 0.25f, 0.19f, 1f);
        [SerializeField] private Color badgeLevelColor = new Color(0.55f, 0.42f, 0.30f, 1f);
        [SerializeField] private Color buttonGoldColor = new Color(0.92f, 0.74f, 0.40f, 1f);
        [SerializeField] private Color buttonUnaffordableColor = new Color(0.38f, 0.33f, 0.30f, 0.95f);
        [SerializeField] private Color buttonMaxedColor = new Color(0.42f, 0.38f, 0.35f, 0.90f);
        [SerializeField] private Color textNeedGoldRed = new Color(0.95f, 0.35f, 0.30f, 1f);

        private SkillNode currentNode;
        private int currentLevel;
        private int nextCost;
        private bool isMaxed;
        private bool canAfford;
        private Action onBuyAction;

        public event Action<SkillNode, int, int, bool> OnCardHoverEnter;
        public event Action OnCardHoverExit;

        public void SetData(SkillNode node, int level, Sprite icon, int currentGold, TMP_FontAsset font, Action onBuyClicked)
        {
            if (node == null) return;

            currentNode = node;
            currentLevel = level;
            isMaxed = level >= node.maxLevel;
            nextCost = isMaxed ? 0 : node.CostForLevel(level + 1);
            canAfford = !isMaxed && currentGold >= nextCost;
            onBuyAction = onBuyClicked;

            // Apply font if provided
            if (font != null)
            {
                if (titleText != null) titleText.font = font;
                if (levelBadgeText != null) levelBadgeText.font = font;
                if (descriptionText != null) descriptionText.font = font;
                if (buyButtonText != null) buyButtonText.font = font;
            }

            if (titleText != null)
            {
                titleText.text = node.LocalizedDisplayName;
                titleText.color = textDarkBrown;
            }

            if (descriptionText != null)
            {
                // Description can be hidden on card face since tooltip handles it, but keep populated if active
                descriptionText.text = level > 0 && !string.IsNullOrEmpty(node.LocalizedUpgradeText)
                    ? node.LocalizedUpgradeText
                    : node.LocalizedDescription;
                descriptionText.color = textBodyBrown;
            }

            if (levelBadgeText != null)
            {
                levelBadgeText.text = $"Lv. {level} / {node.maxLevel}";
                levelBadgeText.color = badgeLevelColor;
            }

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = textDarkBrown;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
            }

            if (isMaxed)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0.40f;
                if (cardBackground != null) cardBackground.color = parchmentDimmed;
                if (buyButton != null) buyButton.interactable = false;
                if (buyButtonBackground != null) buyButtonBackground.color = buttonMaxedColor;
                if (buyButtonText != null)
                {
                    buyButtonText.text = "MAXED";
                    buyButtonText.color = new Color(0.60f, 0.58f, 0.55f, 1f);
                }
            }
            else if (canAfford)
            {
                if (canvasGroup != null) canvasGroup.alpha = 1.0f;
                if (cardBackground != null) cardBackground.color = parchmentNormal;
                if (buyButton != null)
                {
                    buyButton.interactable = true;
                    buyButton.onClick.AddListener(TryBuy);
                }
                if (buyButtonBackground != null) buyButtonBackground.color = buttonGoldColor;
                if (buyButtonText != null)
                {
                    buyButtonText.text = $"<b>{nextCost:N0} Gold</b>";
                    buyButtonText.color = textDarkBrown;
                }
            }
            else
            {
                // Unaffordable
                if (canvasGroup != null) canvasGroup.alpha = 0.48f;
                if (cardBackground != null) cardBackground.color = parchmentDimmed;
                if (buyButton != null) buyButton.interactable = false;
                if (buyButtonBackground != null) buyButtonBackground.color = buttonUnaffordableColor;
                if (buyButtonText != null)
                {
                    buyButtonText.text = $"Need {nextCost:N0} Gold";
                    buyButtonText.color = textNeedGoldRed;
                }
            }
        }

        private void TryBuy()
        {
            if (isMaxed || !canAfford) return;
            onBuyAction?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            TryBuy();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentNode != null)
            {
                OnCardHoverEnter?.Invoke(currentNode, currentLevel, nextCost, isMaxed);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnCardHoverExit?.Invoke();
        }

        private void OnDisable()
        {
            OnCardHoverExit?.Invoke();
        }

        public void AutoWireReferences()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (cardBackground == null) cardBackground = GetComponent<Image>();

            Transform inner = transform.Find("Inner");
            if (inner != null)
            {
                if (iconImage == null)
                {
                    iconImage = inner.Find("Icon")?.GetComponent<Image>()
                        ?? inner.Find("IconContainer/Icon")?.GetComponent<Image>()
                        ?? inner.Find("Header/Icon")?.GetComponent<Image>();
                }
                if (titleText == null)
                {
                    titleText = inner.Find("Title")?.GetComponent<TMP_Text>()
                        ?? inner.Find("Header/TitleCol/Title")?.GetComponent<TMP_Text>();
                }
                if (levelBadgeText == null)
                {
                    levelBadgeText = inner.Find("LevelBadge")?.GetComponent<TMP_Text>()
                        ?? inner.Find("Header/TitleCol/LevelBadge")?.GetComponent<TMP_Text>();
                }
                if (dividerImage == null) dividerImage = inner.Find("Divider")?.GetComponent<Image>();
                if (descriptionText == null) descriptionText = inner.Find("Description")?.GetComponent<TMP_Text>();

                Transform btnTr = inner.Find("BuyButton") ?? transform.Find("BuyButton");
                if (btnTr != null)
                {
                    if (buyButton == null) buyButton = btnTr.GetComponent<Button>();
                    if (buyButtonBackground == null) buyButtonBackground = btnTr.GetComponent<Image>();
                    if (buyButtonText == null) buyButtonText = btnTr.GetComponentInChildren<TMP_Text>();
                }
            }
        }
    }
}
