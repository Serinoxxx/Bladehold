using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Hover tooltip for Campaign map nodes.
///     Presents tactical encounter information, Clan Captain identity, difficulty skulls,
///     reward bounties, and sector lore. The panel sizes itself to its content (VerticalLayoutGroup +
///     ContentSizeFitter on the root) and only shows sections that have something to say.
/// </summary>
/// <remarks>
///     The tooltip never takes pointer input: its CanvasGroup doesn't block raycasts and every graphic has
///     raycastTarget off. If it did, sliding the cursor onto it would send the node a pointer-exit, hide
///     the tooltip, send a pointer-enter again and so on, which is the flicker this used to have. It also sits
///     beside the hovered node (right, or left when there's no room) and is clamped to the canvas, so it
///     never covers the node it describes.
/// </remarks>
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
    [Tooltip("Optional text fallback for the skulls. Leave empty when Skull Icons are wired (the emoji skulls don't render in Grenze).")]
    [SerializeField] private TMP_Text difficultySkullsText;
    [Tooltip("Skull images, one per difficulty level (up to 4). Shown up to the node's skull count and tinted with the tier colour.")]
    [SerializeField] private Image[] skullIcons;
    [SerializeField] private Image captainIcon;
    [SerializeField] private TMP_Text clanBuffText;

    [Header("Rewards Section")]
    [SerializeField] private GameObject rewardsSection;
    [SerializeField] private TMP_Text rewardsSummaryText;
    [Tooltip("Row holding the gold/blood/metal chips; hidden when the node pays none of them.")]
    [SerializeField] private GameObject rewardChipsRow;
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
    [SerializeField] private Color availablePromptColor = new Color(1f, 0.82f, 0.44f, 1f);   // #FFD170
    [SerializeField] private Color completedPromptColor = new Color(0.56f, 0.75f, 0.48f, 1f); // #8FBF7A
    [SerializeField] private Color lockedPromptColor = new Color(0.78f, 0.42f, 0.35f, 1f);    // muted red
    [SerializeField] private Color demoLockedPromptColor = new Color(0.55f, 0.51f, 0.47f, 1f); // muted grey

    [Header("Placement")]
    [Tooltip("Gap in canvas units between the hovered node's edge and the tooltip.")]
    [SerializeField] private float nodeGap = 28f;
    [Tooltip("Minimum distance in canvas units kept from the canvas edges.")]
    [SerializeField] private float screenMargin = 16f;

    // Node-type tag tints: darker takes on the map node colours so white tag text stays readable.
    private static readonly Color CombatTagColor = new Color(0.55f, 0.18f, 0.16f, 1f);
    private static readonly Color RestTagColor = new Color(0.2f, 0.45f, 0.31f, 1f);
    private static readonly Color FishingTagColor = new Color(0.12f, 0.43f, 0.5f, 1f);
    private static readonly Color EliteTagColor = new Color(0.6f, 0.32f, 0.1f, 1f);
    private static readonly Color BossTagColor = new Color(0.42f, 0.16f, 0.5f, 1f);

    private Canvas parentCanvas;
    private RectTransform anchorRect;
    private Vector2 anchorScreenPos;

    private void Awake()
    {
        if (tooltipRect == null) tooltipRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        MakeNonBlocking();
        Hide();
    }

    /// <summary>
    ///     Populates the tooltip and places it beside the hovered node's rect (preferred: it knows the node's size).
    /// </summary>
    public void Show(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status, RectTransform nodeRect)
    {
        anchorRect = nodeRect;
        Populate(node, status);
    }

    /// <summary>
    ///     Populates the tooltip and places it beside a screen point (no node size known).
    /// </summary>
    public void Show(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status, Vector2 targetScreenPos)
    {
        anchorRect = null;
        anchorScreenPos = targetScreenPos;
        Populate(node, status);
    }

    /// <summary>
    ///     Hides the tooltip dialog.
    /// </summary>
    public void Hide()
    {
        anchorRect = null;
        if (rootContainer != null) rootContainer.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void LateUpdate()
    {
        // Follow the node while the map scrolls (gamepad focus pans the view) or the node's hover scale eases in.
        if (anchorRect != null) PositionTooltip();
    }

    private void Populate(CampaignNodeSO node, CampaignNodeButtonUI.NodeVisualStatus status)
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
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(node.subtitle));
            subtitleText.text = node.subtitle;
        }

        if (nodeTypeBadgeText != null)
        {
            nodeTypeBadgeText.text = FormatNodeType(node.nodeType).ToUpperInvariant();
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

                if (difficultyTierText != null)
                {
                    difficultyTierText.text = tierName.ToUpper();
                    difficultyTierText.color = tierColor;
                }

                if (difficultySkullsText != null)
                {
                    difficultySkullsText.text = BannerDifficultyHelper.GetSkullString(node.difficultyTier);
                    difficultySkullsText.color = tierColor;
                }

                if (skullIcons != null)
                {
                    int skullCount = BannerDifficultyHelper.GetSkullCount(node.difficultyTier);
                    for (int i = 0; i < skullIcons.Length; i++)
                    {
                        if (skullIcons[i] == null) continue;
                        skullIcons[i].gameObject.SetActive(i < skullCount);
                        skullIcons[i].color = tierColor;
                    }
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
                        clanBuffText.text = Loc.Get("campaign.tooltip.clan", "Clan: {0}").Replace("{0}", Loc.Get($"clan.{WaveCard.ClanLocKey(node.clanBuff)}.name", node.clanBuff.clanName));
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

        // 3. Rewards Preview Section. Bounties no longer count (plan 15), so a bounty-only node shows no rewards.
        bool hasCurrency = node.goldReward > 0 || node.bloodReward > 0 || node.metalReward > 0;
        bool hasSummary = !string.IsNullOrEmpty(node.rewardsDescription);
        if (rewardsSection != null)
        {
            rewardsSection.SetActive(hasCurrency || hasSummary);

            // The designer summary repeats the currency amounts, so it only shows for nodes without chips
            // (rest area, fishing, story nodes). Nodes that pay currency get a short heading over the chips instead.
            if (rewardsSummaryText != null)
            {
                rewardsSummaryText.text = hasCurrency
                    ? $"<size=85%><cspace=0.12em>{Loc.Get("campaign.tooltip.spoils", "Sector Clearing Spoils").ToUpperInvariant()}</cspace></size>"
                    : node.rewardsDescription;
            }

            if (rewardChipsRow != null) rewardChipsRow.SetActive(hasCurrency);

            if (goldContainer != null) goldContainer.SetActive(node.goldReward > 0);
            if (goldRewardText != null && node.goldReward > 0) goldRewardText.text = $"+{node.goldReward} {Loc.Get("campaign.tooltip.gold", "Gold")}";

            if (bloodContainer != null) bloodContainer.SetActive(node.bloodReward > 0);
            if (bloodRewardText != null && node.bloodReward > 0) bloodRewardText.text = $"+{node.bloodReward} {Loc.Get("campaign.tooltip.blood", "Blood")}";

            if (metalContainer != null) metalContainer.SetActive(node.metalReward > 0);
            if (metalRewardText != null && node.metalReward > 0) metalRewardText.text = $"+{node.metalReward} {Loc.Get("campaign.tooltip.metal", "Metal")}";

            if (bountyContainer != null)
            {
                bountyContainer.SetActive(false); // plan 15: node bounties no longer seed the sector; wave cards pay instead
                if (bountyNameText != null && node.bountyType != BannerBountyType.None)
                {
                    bountyNameText.text = FormatBountyName(node.bountyType);
                }
            }
        }

        // 4. Sector Lore Description
        if (loreDescriptionText != null)
        {
            loreDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(node.description));
            loreDescriptionText.text = node.description;
        }

        // 5. Action Prompt
        if (actionPromptText != null)
        {
            switch (status)
            {
                case CampaignNodeButtonUI.NodeVisualStatus.Available:
                    actionPromptText.text = Loc.Get("campaign.tooltip.deploy", "Click to Deploy");
                    actionPromptText.color = availablePromptColor;
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.Completed:
                    actionPromptText.text = Loc.Get("campaign.tooltip.liberated", "Sector Liberated");
                    actionPromptText.color = completedPromptColor;
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.DemoLocked:
                    actionPromptText.text = DemoConfigSO.LockedPrompt;
                    actionPromptText.color = demoLockedPromptColor;
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.Bypassed:
                    actionPromptText.text = Loc.Get("campaign.tooltip.bypassed", "Out of reach: your route has moved past it");
                    actionPromptText.color = lockedPromptColor;
                    break;
                case CampaignNodeButtonUI.NodeVisualStatus.Locked:
                default:
                    actionPromptText.text = Loc.Get("campaign.tooltip.locked", "Locked: clear a connected sector first");
                    actionPromptText.color = lockedPromptColor;
                    break;
            }
        }

        // 6. Size to the new content, then place & clamp.
        if (tooltipRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        PositionTooltip();
    }

    private void MakeNonBlocking()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private void PositionTooltip()
    {
        if (tooltipRect == null) return;
        RectTransform space = tooltipRect.parent as RectTransform;
        if (space == null) return;

        // The hovered node's rect in the tooltip's parent space (a zero-size rect for the screen-point overload).
        Vector2 nodeMin;
        Vector2 nodeMax;
        if (anchorRect != null)
        {
            Vector3[] corners = new Vector3[4];
            anchorRect.GetWorldCorners(corners);
            nodeMin = space.InverseTransformPoint(corners[0]);
            nodeMax = space.InverseTransformPoint(corners[2]);
        }
        else
        {
            Camera cam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, anchorScreenPos, cam, out Vector2 point);
            nodeMin = point;
            nodeMax = point;
        }

        Rect bounds = space.rect;
        Vector2 size = tooltipRect.rect.size;

        // Right of the node; flip to the left when it would run off the canvas.
        float left = nodeMax.x + nodeGap;
        if (left + size.x > bounds.xMax - screenMargin)
        {
            left = nodeMin.x - nodeGap - size.x;
        }
        left = Mathf.Clamp(left, bounds.xMin + screenMargin, Mathf.Max(bounds.xMin + screenMargin, bounds.xMax - screenMargin - size.x));

        // Vertically centred on the node, clamped to the canvas.
        float top = (nodeMin.y + nodeMax.y) * 0.5f + size.y * 0.5f;
        top = Mathf.Clamp(top, Mathf.Min(bounds.yMax - screenMargin, bounds.yMin + screenMargin + size.y), bounds.yMax - screenMargin);

        Vector2 pivot = tooltipRect.pivot;
        Vector2 local = new Vector2(left + pivot.x * size.x, top - (1f - pivot.y) * size.y);
        tooltipRect.localPosition = new Vector3(local.x, local.y, tooltipRect.localPosition.z);
    }

    private static string FormatNodeType(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return Loc.Get("campaign.nodetype.combat", "Combat Sector");
            case CampaignNodeType.RestArea: return Loc.Get("campaign.nodetype.rest", "Sanctuary Rest Area");
            case CampaignNodeType.FishingPond: return Loc.Get("campaign.nodetype.fishing", "Fishing Pond");
            case CampaignNodeType.PreBoss: return Loc.Get("campaign.nodetype.preboss", "Elite Horde");
            case CampaignNodeType.NecromancerEncounter: return Loc.Get("campaign.nodetype.revelation", "Boss Encounter");
            case CampaignNodeType.PrincessBoss: return Loc.Get("campaign.nodetype.princess", "Boss: The Corrupted Princess");
            case CampaignNodeType.NecromancerBoss: return Loc.Get("campaign.nodetype.necromancer", "Boss: The Necromancer Lord");
            default: return Loc.Get("campaign.nodetype.sector", "Sector");
        }
    }

    private static Color GetNodeTypeColor(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return CombatTagColor;
            case CampaignNodeType.RestArea: return RestTagColor;
            case CampaignNodeType.FishingPond: return FishingTagColor;
            case CampaignNodeType.PreBoss: return EliteTagColor;
            case CampaignNodeType.NecromancerEncounter:
            case CampaignNodeType.PrincessBoss:
            case CampaignNodeType.NecromancerBoss:
                return BossTagColor;
            default: return new Color(0.3f, 0.28f, 0.26f, 1f);
        }
    }

    private static string FormatBountyName(BannerBountyType bounty)
    {
        switch (bounty)
        {
            case BannerBountyType.WeaponDraft: return "Weapon Upgrade Draft";
            case BannerBountyType.ElementDraft: return "Elemental Powerup Draft";
            case BannerBountyType.FortressDraft: return "Supply Cache";
            case BannerBountyType.GoldCache: return "Gold Cache Bounty";
            case BannerBountyType.OrcishMetal: return "Orcish Metal Cache";
            case BannerBountyType.GoblinBlood: return "Goblin Blood Phials";
            case BannerBountyType.TrollHeart: return "Troll Heart Vitality";
            default: return "Special Bounty";
        }
    }
}
