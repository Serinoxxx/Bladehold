using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     "Thanks for playing / Wishlist on Steam" panel on the Campaign Map, shown by
///     <see cref="CampaignMapUI" /> once the demo's cutoff tier is cleared
///     (<see cref="CampaignManager.IsDemoEndReached" />). The map stays visible behind it, so the
///     locked later tiers show what the full game holds. Continue ends the run and returns to the
///     Meta Area; Wishlist opens <see cref="DemoConfigSO.steamStoreUrl" /> (hidden when no URL is set).
///     All text and art are authored on the prefab.
/// </summary>
public class DemoEndScreenUI : MonoBehaviour
{
    [Tooltip("Root of the panel; hidden until Show().")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Ends the run and returns to the Meta Area.")]
    [SerializeField] private Button continueButton;
    [Tooltip("Optional: opens the Steam store page from DemoConfig. Hidden when no URL is set.")]
    [SerializeField] private Button wishlistButton;
    [Tooltip("Optional: played when the panel opens (sound, fade, punch).")]
    [SerializeField] private MMF_Player showFeedback;

    private bool anyError;
    private bool continued;

    public bool IsValid => !anyError;

    private void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[DemoEndScreenUI] Panel root is not assigned.", this);
            anyError = true;
        }
        if (continueButton == null)
        {
            Debug.LogError("[DemoEndScreenUI] Continue button is not assigned.", this);
            anyError = true;
        }
        if (anyError) return;

        panelRoot.SetActive(false);
        continueButton.onClick.AddListener(HandleContinue);
        if (wishlistButton != null) wishlistButton.onClick.AddListener(HandleWishlist);
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinue);
        if (wishlistButton != null) wishlistButton.onClick.RemoveListener(HandleWishlist);
    }

    public void Show()
    {
        if (anyError) return;

        DemoConfigSO cfg = DemoConfigSO.Active;
        if (wishlistButton != null)
        {
            wishlistButton.gameObject.SetActive(cfg != null && !string.IsNullOrWhiteSpace(cfg.steamStoreUrl));
        }

        panelRoot.SetActive(true);
        CursorLockManager.SetUnlock("DemoEnd", true);
        continueButton.Select();
        if (showFeedback != null) showFeedback.PlayFeedbacks();
    }

    private void HandleWishlist()
    {
        DemoConfigSO cfg = DemoConfigSO.Active;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.steamStoreUrl)) return;
        Application.OpenURL(cfg.steamStoreUrl);
    }

    private void HandleContinue()
    {
        if (continued) return;
        continued = true;

        CursorLockManager.SetUnlock("DemoEnd", false);
        CampaignManager.Instance.EndCampaign();
    }
}
