using System;
using System.Collections;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Controller for an individual item slot card in the Rest Area Shop.
///     Displays item details and triggers Feel MMF_Player feedbacks on buy attempts:
///     - Invalid (insufficient gold): card shake, red flash, and error sound.
///     - Purchase: coins audio, spring scale bounce, and card disappearance.
/// </summary>
public class ShopSlotUI : MonoBehaviour
{
    [Header("Card UI References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image cardBackground;

    [Header("Feel Feedbacks")]
    [Tooltip("Played when player clicks buy but lacks enough gold: shakes card, flashes red, plays error sound.")]
    [SerializeField] private MMF_Player invalidFeedback;

    [Tooltip("Played when item is successfully purchased: coins sound, spring scale, then card disappears.")]
    [SerializeField] private MMF_Player purchaseFeedback;

    private int slotIndex;
    private ShopItemSO currentItem;
    private Action<int, ShopSlotUI> onBuyAttemptCallback;
    private bool isPurchasing = false;

    private Vector3 originalScale = Vector3.one;

    private void Awake()
    {
        originalScale = transform.localScale;
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyClick);
        }
    }

    public void Setup(ShopItemSO item, int index, bool isPurchased, Action<int, ShopSlotUI> onBuyAttempt)
    {
        currentItem = item;
        slotIndex = index;
        onBuyAttemptCallback = onBuyAttempt;
        isPurchasing = false;

        // Reset transform scale
        transform.localScale = originalScale;

        if (isPurchased)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (nameText != null) nameText.text = item != null ? item.displayName : "Item";
        if (descText != null) descText.text = item != null ? item.description : "";

        if (iconImage != null && item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyClick);
            buyButton.interactable = true;
        }

        if (costText != null && item != null)
        {
            bool canAfford = RunSession.InRunGold >= item.goldCost;
            costText.text = $"{item.goldCost} Gold";
            costText.color = canAfford ? Color.white : new Color(1f, 0.4f, 0.4f, 1f);
        }
    }

    private void HandleBuyClick()
    {
        if (isPurchasing || currentItem == null) return;
        onBuyAttemptCallback?.Invoke(slotIndex, this);
    }

    /// <summary>
    ///     Plays the invalid feedback: shakes the card, flashes red, and plays error audio.
    /// </summary>
    public void PlayInvalidFeedback()
    {
        if (invalidFeedback != null)
        {
            invalidFeedback.PlayFeedbacks();
        }
    }

    /// <summary>
    ///     Plays the purchase feedback: plays coins sound, spring scales the card,
    ///     and smoothly shrinks/disappears the card.
    /// </summary>
    public void PlayPurchaseFeedback(Action onComplete)
    {
        if (isPurchasing) return;
        isPurchasing = true;

        if (buyButton != null) buyButton.interactable = false;

        StartCoroutine(PurchaseSequenceRoutine(onComplete));
    }

    private IEnumerator PurchaseSequenceRoutine(Action onComplete)
    {
        if (purchaseFeedback != null)
        {
            purchaseFeedback.PlayFeedbacks();
        }

        // Wait for the spring scale pop and sound (using unscaled time since game might be paused)
        yield return new WaitForSecondsRealtime(0.35f);

        // Smooth shrink transition
        float elapsed = 0f;
        float shrinkDuration = 0.2f;
        Vector3 startScale = transform.localScale;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            // Ease in quad for snappy vanish
            float curve = t * t;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, curve);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);

        onComplete?.Invoke();
    }
}
