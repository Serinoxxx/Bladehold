using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Hover tooltip for Campaign map nodes.
///     Presents tactical encounter information, Clan Captain identity, difficulty skulls,
///     reward bounties, and sector lore.
/// </summary>
public class CampaignTooltipUI : MonoBehaviour
{
    [Header("Root Frame")]
    [SerializeField] private GameObject rootContainer;
    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Node Header")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text nodeTypeBadgeText;
    [SerializeField] private Image nodeTypeBadgeBg;

    [Header("Clan Captain Section")]
    [SerializeField] private GameObject captainSection;
    [SerializeField] private TMP_Text captainNameText;
    [SerializeField] private TMP_Text difficultyTierText;
    [SerializeField] private TMP_Text difficultySkullsText;
    [SerializeField] private Image captainIcon;
    [SerializeField] private TMP_Text clanBuffText;

    [Header("Rewards Section")]
    [SerializeField] private GameObject rewardsSection;
    [SerializeField] private TMP_Text rewardsSummaryText;
    [SerializeField] private GameObject goldContainer;
    [SerializeField] private TMP_Text goldRewardText;
    [SerializeField] private GameObject bloodContainer;
    [SerializeField] private TMP_Text bloodRewardText;
    [SerializeField] private GameObject metalContainer;
    [SerializeField] private TMP_Text metalRewardText;
    [SerializeField] private GameObject bountyContainer;
    [SerializeField] private TMP_Text bountyNameText;

    [Header("Sector Lore / Description")]
    [SerializeField] private TMP_Text loreDescriptionText;

    [Header("Footer Prompt")]
    [SerializeField] private TMP_Text actionPromptText;

    private Canvas parentCanvas;

    private void Awake()
    {
        if (tooltipRect == null) tooltipRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        Hide();
    }

    /// <summary>
    ///     Allows procedural/editor construction to wire all child text and containers cleanly.
    /// </summary>
    public void InitializeReferences(
        GameObject root,
        RectTransform rect,
        CanvasGroup cg,
        TMP_Text title,
        TMP_Text subtitle,
        TMP_Text badgeText,
        Image badgeBg,
        GameObject captainSec,
        TMP_Text captainName,
        TMP_Text diffTier,
        TMP_Text diffSkulls,
        TMP_Text clanBuff,
        GameObject rewardsSec,
        TMP_Text rewardsSummary,
        GameObject goldBox,
        TMP_Text goldText,
        GameObject bloodBox,
        TMP_Text bloodText,
        GameObject metalBox,
        TMP_Text metalText,
        TMP_Text lore,
        TMP_Text actionPrompt)
    {
        rootContainer = root;
        tooltipRect = rect;
        canvasGroup = cg;
        titleText = title;
        subtitleText = subtitle;
        nodeTypeBadgeText = badgeText;
        nodeTypeBadgeBg = badgeBg;
        captainSection = captainSec;
        captainNameText = captainName;
        difficultyTierText = diffTier;
        difficultySkullsText = diffSkulls;
        clanBuffText = clanBuff;
        rewardsSection = rewardsSec;
        rewardsSummaryText = rewardsSummary;
        goldContainer = goldBox;
        goldRewardText = goldText;
        bloodContainer = bloodBox;
        bloodRewardText = bloodText;
        metalContainer = metalBox;
        metalRewardText = metalText;
        loreDescriptionText = lore;
        actionPromptText = actionPrompt;
    }

    /// <summary>
    ///     Populates and renders the tooltip at the target screen/anchored position.
    /// </summary>
    public void Show(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status, Vector2 targetScreenPos)
    {
        if (node == null)
        {
            Hide();
            return;
        }

        if (rootContainer != null) rootContainer.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 1. Header Information
        if (titleText != null) titleText.text = node.nodeTitle;
        if (subtitleText != null) subtitleText.text = node.subtitle;

        if (nodeTypeBadgeText != null)
        {
            nodeTypeBadgeText.text = FormatNodeType(node.nodeType);
        }

        if (nodeTypeBadgeBg != null)
        {
            nodeTypeBadgeBg.color = GetNodeTypeColor(node.nodeType);
        }

        // 2. Clan Captain Section
        if (captainSection != null)
        {
            if (node.HasCaptain)
            {
                captainSection.SetActive(true);
                if (captainNameText != null) captainNameText.text = node.captainName;

                string tierName = BannerDifficultyHelper.GetTierName(node.difficultyTier);
                Color tierColor = BannerDifficultyHelper.GetTierColor(node.difficultyTier);
                string skulls = BannerDifficultyHelper.GetSkullString(node.difficultyTier);

                if (difficultyTierText != null)
                {
                    difficultyTierText.text = tierName.ToUpper();
                    difficultyTierText.color = tierColor;
                }

                if (difficultySkullsText != null)
                {
                    difficultySkullsText.text = skulls;
                    difficultySkullsText.color = tierColor;
                }

                if (captainIcon != null)
                {
                    captainIcon.gameObject.SetActive(node.captainIcon != null);
                    if (node.captainIcon != null) captainIcon.sprite = node.captainIcon;
                }

                if (clanBuffText != null)
                {
                    if (node.clanBuff != null)
                    {
                        clanBuffText.gameObject.SetActive(true);
                        clanBuffText.text = $"Clan: {node.clanBuff.clanName} ({node.clanBuff.quickFact})";
                    }
                    else
                    {
                        clanBuffText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                captainSection.SetActive(false);
            }
        }

        // 3. Rewards Preview Section
        if (rewardsSection != null)
        {
            bool hasAnyReward = node.HasRewards || !string.IsNullOrEmpty(node.rewardsDescription);
            rewardsSection.SetActive(hasAnyReward);

            if (rewardsSummaryText != null)
            {
                rewardsSummaryText.text = !string.IsNullOrEmpty(node.rewardsDescription) ? node.rewardsDescription : "Sector Clearing Spoils";
            }

            if (goldContainer != null) goldContainer.SetActive(node.goldReward > 0);
            if (goldRewardText != null && node.goldReward > 0) goldRewardText.text = $"+{node.goldReward} Gold";

            if (bloodContainer != null) bloodContainer.SetActive(node.bloodReward > 0);
            if (bloodRewardText != null && node.bloodReward > 0) bloodRewardText.text = $"+{node.bloodReward} Blood";

            if (metalContainer != null) metalContainer.SetActive(node.metalReward > 0);
            if (metalRewardText != null && node.metalReward > 0) metalRewardText.text = $"+{node.metalReward} Metal";

            if (bountyContainer != null)
            {
                bountyContainer.SetActive(node.bountyType != BannerBountyType.None);
                if (bountyNameText != null && node.bountyType != BannerBountyType.None)
                {
                    bountyNameText.text = FormatBountyName(node.bountyType);
                }
            }
        }

        // 4. Sector Lore Description
        if (loreDescriptionText != null)
        {
            loreDescriptionText.text = node.description;
        }

        // 5. Action Prompt
        if (actionPromptText != null)
        {
            switch (status)
            {
                case CampaignNodeButtonUI.NodeVisualStatus.Available:
                    actionPromptText.text = "<color=#FFCC00>[Click to Deploy]</color>";
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.Completed:
                    actionPromptText.text = "<color=#55FF55>[Sector Liberated]</color>";
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.Locked:
                default:
                    actionPromptText.text = "<color=#AAAAAA>[Locked - Clear Prior Sector]</color>";
                    break;
            }
        }

        // 6. Position & Clamping
        PositionTooltip(targetScreenPos);
    }

    /// <summary>
    ///     Hides the tooltip dialog.
    /// </summary>
    public void Hide()
    {
        if (rootContainer != null) rootContainer.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void PositionTooltip(Vector2 targetScreenPos)
    {
        if (tooltipRect == null) return;

        Vector2 offset = new Vector2(25f, 25f);
        Vector2 finalPos = targetScreenPos + offset;

        // Keep inside screen viewport bounds
        float width = tooltipRect.rect.width > 0 ? tooltipRect.rect.width : 340f;
        float height = tooltipRect.rect.height > 0 ? tooltipRect.rect.height : 300f;

        if (finalPos.x + width > Screen.width)
        {
            finalPos.x = targetScreenPos.x - width - 25f;
        }

        if (finalPos.y + height > Screen.height)
        {
            finalPos.y = Screen.height - height - 10f;
        }

        if (finalPos.y < 10f)
        {
            finalPos.y = 10f;
        }

        transform.position = finalPos;
    }

    private static string FormatNodeType(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return "Combat Sector - 5 Waves";
            case CampaignNodeType.RestArea: return "Sanctuary Rest Area";
            case CampaignNodeType.SupplyRoom: return "Supply Depot - Zero Enemies";
            case CampaignNodeType.PreBoss: return "Inner Portico - Elite Horde";
            case CampaignNodeType.NecromancerEncounter: return "The Revelation - Boss Encounter";
            case CampaignNodeType.PrincessBoss: return "Throne Room - The Corrupted Princess";
            case CampaignNodeType.NecromancerBoss: return "Sanctum Crypt - The Necromancer Lord";
            default: return "Sector";
        }
    }

    private static Color GetNodeTypeColor(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return new Color(0.85f, 0.35f, 0.2f, 1f);
            case CampaignNodeType.RestArea: return new Color(0.2f, 0.75f, 0.45f, 1f);
            case CampaignNodeType.SupplyRoom: return new Color(0.25f, 0.65f, 0.85f, 1f);
            case CampaignNodeType.PreBoss: return new Color(0.9f, 0.45f, 0.1f, 1f);
            case CampaignNodeType.NecromancerEncounter:
            case CampaignNodeType.PrincessBoss:
            case CampaignNodeType.NecromancerBoss:
                return new Color(0.75f, 0.2f, 0.85f, 1f);
            default: return Color.gray;
        }
    }

    private static string FormatBountyName(BannerBountyType bounty)
    {
        switch (bounty)
        {
            case BannerBountyType.WeaponDraft: return "Weapon Upgrade Draft";
            case BannerBountyType.ElementDraft: return "Elemental Powerup Draft";
            case BannerBountyType.FortressDraft: return "Fortress Wall Upgrade";
            case BannerBountyType.GoldCache: return "Gold Cache Bounty";
            case BannerBountyType.OrcishMetal: return "Orcish Metal Cache";
            case BannerBountyType.GoblinBlood: return "Goblin Blood Phials";
            case BannerBountyType.TrollHeart: return "Troll Heart Vitality";
            default: return "Special Bounty";
        }
    }
}
