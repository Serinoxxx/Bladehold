using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using HighlightPlus;
using MoreMountains.Feedbacks;

public class WarBannerController : MonoBehaviour
{
    public BannerBuffDef Buff { get; private set; }
    public BannerBountyDef Bounty { get; private set; }

    public WarBannerClanSO Clan { get; private set; }
    public WarBannerRewardSO Reward { get; private set; }

    [Header("Core References")]
    [SerializeField] private Interactable interactable;
    [SerializeField] private GameObject uiPanel;
    [Tooltip("MeshRenderer on the banner model to swap materials per clan.")]
    [SerializeField] private Renderer bannerRenderer;
    
    [Header("Cinematic Effects")]
    [SerializeField] private HighlightEffect highlightEffect;
    [SerializeField] private MMF_Player slamFeedback;
    [SerializeField] private GameObject burnVfxPrefab;
    [SerializeField] private AudioClip burnSfx;

    [Header("Clan Quick Facts UI")]
    [SerializeField] private UnityEngine.UI.Image clanSigilImage;
    [SerializeField] private TMPro.TMP_Text clanNameText;
    [Tooltip("Single-line quick fact description, e.g. 'Enemies heal 2 HP/s'.")]
    [SerializeField] private TMPro.TMP_Text quickFactText;
    [SerializeField] private TMPro.TMP_Text buffDescriptionText; // Legacy fallback

    [Header("Reward Quick Facts UI")]
    [SerializeField] private UnityEngine.UI.Image rewardIconImage;
    [Tooltip("Display text for reward quantity, e.g. 'x 100', 'x 3', 'x 1 Draft'.")]
    [SerializeField] private TMPro.TMP_Text rewardQuantityText;
    [SerializeField] private TMPro.TMP_Text rewardNameText;
    [SerializeField] private TMPro.TMP_Text bountyDescriptionText; // Legacy fallback

    // Use an Action to notify GameLoopManager
    public event System.Action<WarBannerController> OnBannerInteracted;

    private Vector3 spawnPosition;

    private void Awake()
    {
        spawnPosition = transform.position;
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<Interactable>();
            }
        }
        if (bannerRenderer == null)
        {
            bannerRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void StageHighUp()
    {
        transform.position = spawnPosition + Vector3.up * 18f;
    }

    public void SlamDown()
    {
        // LeanTween slam down
        LeanTween.moveY(gameObject, spawnPosition.y, 0.35f).setEase(LeanTweenType.easeInCubic).setOnComplete(() =>
        {
            if (slamFeedback != null)
            {
                slamFeedback.PlayFeedbacks();
            }
        });
    }

    private void Start()
    {
        interactable.OnInteractedEvent -= HandleInteracted;
        interactable.OnInteractedEvent += HandleInteracted;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleInteracted;
        }
    }
    
    public void SetInteractable(bool canInteract)
    {
        if (interactable != null)
        {
            interactable.CanInteract = canInteract;
        }
    }

    /// <summary>
    ///     Initializes the banner using modular ScriptableObjects.
    ///     Displays clean quick facts: Clan Icon, 1-line Buff Fact, and Reward [Icon] x Qty.
    /// </summary>
    public void Initialize(WarBannerClanSO clan, WarBannerRewardSO reward)
    {
        Clan = clan;
        Reward = reward;

        // Populate backward-compatible structs
        Buff = new BannerBuffDef
        {
            buffType = clan != null ? clan.buffType : BannerBuffType.None,
            clanName = clan != null ? clan.clanName : "Unknown Clan",
            inGameDescription = clan != null ? clan.quickFact : "",
            gameplayEffect = clan != null ? clan.detailedEffect : "",
            clanSigil = clan != null ? clan.clanIcon : null
        };

        Bounty = new BannerBountyDef
        {
            bountyType = reward != null ? reward.bountyType : BannerBountyType.None,
            inGameDisplay = reward != null ? reward.rewardName : "",
            rewardDescription = reward != null ? reward.rewardDescription : ""
        };

        // --- Clan Section ---
        if (clanNameText != null && clan != null)
            clanNameText.text = clan.clanName;

        string quickFact = clan != null ? clan.quickFact : "";
        if (quickFactText != null)
            quickFactText.text = quickFact;
        else if (buffDescriptionText != null)
            buffDescriptionText.text = quickFact;

        if (clanSigilImage != null)
        {
            if (clan != null && clan.clanIcon != null)
            {
                clanSigilImage.sprite = clan.clanIcon;
                clanSigilImage.gameObject.SetActive(true);
            }
            else
            {
                clanSigilImage.gameObject.SetActive(false);
            }
        }

        // --- Reward Section ---
        if (rewardIconImage != null)
        {
            if (reward != null && reward.rewardIcon != null)
            {
                rewardIconImage.sprite = reward.rewardIcon;
                rewardIconImage.gameObject.SetActive(true);
            }
            else
            {
                rewardIconImage.gameObject.SetActive(false);
            }
        }

        string qtyStr = reward != null ? reward.GetFormattedQuantity() : "x 1";
        if (rewardQuantityText != null)
            rewardQuantityText.text = qtyStr;

        string rName = reward != null ? reward.rewardName : "";
        if (rewardNameText != null)
            rewardNameText.text = rName;
        else if (bountyDescriptionText != null)
            bountyDescriptionText.text = $"{rName} {qtyStr}";

        if (interactable != null)
        {
            interactable.PromptText = $"Tear Down Banner\n{Buff.clanName}";
            interactable.CanInteract = true;
        }

        // --- Clan Banner Material Swap ---
        if (bannerRenderer != null && clan != null && clan.bannerMaterial != null)
        {
            bannerRenderer.sharedMaterial = clan.bannerMaterial;
        }
        
        ApplyClanGlow(Buff.buffType);
    }
    
    private void ApplyClanGlow(BannerBuffType buffType)
    {
        if (highlightEffect == null) return;
        
        Color glowColor = Color.white;
        switch (buffType)
        {
            case BannerBuffType.Berserk: glowColor = new Color(1f, 0.23f, 0.18f); break; // Crimson Red
            case BannerBuffType.Shield: glowColor = new Color(0f, 0.47f, 1f); break; // Arcane Blue
            case BannerBuffType.Haste: glowColor = new Color(1f, 0.8f, 0f); break; // Golden Amber
            case BannerBuffType.Regen: glowColor = new Color(0.2f, 0.78f, 0.35f); break; // Emerald Green
            case BannerBuffType.Armor: glowColor = new Color(0.68f, 0.32f, 0.87f); break; // Royal Violet
        }
        
        highlightEffect.outlineColor = glowColor;
        highlightEffect.glowHQColor = glowColor;
        highlightEffect.highlighted = true;
        highlightEffect.Refresh();
    }

    /// <summary>
    ///     Legacy initialization support for BannerBuffDef / BannerBountyDef.
    /// </summary>
    public void Initialize(BannerBuffDef buff, BannerBountyDef bounty)
    {
        Buff = buff;
        Bounty = bounty;

        var tempClan = ScriptableObject.CreateInstance<WarBannerClanSO>();
        tempClan.clanName = buff.clanName;
        tempClan.buffType = buff.buffType;
        tempClan.quickFact = !string.IsNullOrEmpty(buff.inGameDescription) ? buff.inGameDescription : buff.clanName;
        tempClan.clanIcon = buff.clanSigil;

        var tempReward = ScriptableObject.CreateInstance<WarBannerRewardSO>();
        tempReward.rewardName = bounty.inGameDisplay;
        tempReward.bountyType = bounty.bountyType;
        tempReward.quantityText = "x 1";

        Initialize(tempClan, tempReward);
    }

    private void HandleInteracted(Player player)
    {
        if (interactable != null)
            interactable.CanInteract = false;

        OnBannerInteracted?.Invoke(this);
    }

    public void TearDown()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
        
        if (burnVfxPrefab != null)
        {
            GameObject fire = Instantiate(burnVfxPrefab, transform.position, Quaternion.identity, transform);
            // offset slightly up
            fire.transform.localPosition = new Vector3(0, 1.5f, 0);
        }
        if (burnSfx != null)
        {
            AudioSource.PlayClipAtPoint(burnSfx, transform.position, 1.0f);
        }
        
        // Simple dissolve simulation: fade out or scale over 3 seconds
        LeanTween.scale(gameObject, Vector3.zero, 3.0f).setEase(LeanTweenType.easeInSine);
        Destroy(gameObject, 3.1f);
    }
    
    public void ShrinkOut()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
        LeanTween.scale(gameObject, Vector3.zero, 0.5f).setEase(LeanTweenType.easeInBack);
        Destroy(gameObject, 0.6f);
    }
}
