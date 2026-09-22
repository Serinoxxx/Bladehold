using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     ScriptableObject representing a single tactical encounter node in the Castle Campaign.
///     Encapsulates destination scene, difficulty scaling, Clan Captain encounters, and rewards preview.
/// </summary>
[CreateAssetMenu(fileName = "CampaignNode_", menuName = "Scriptable Objects/Campaign/Campaign Node")]
public class CampaignNodeSO : ScriptableObject
{
    [Header("Identity & Presentation")]
    [Tooltip("Unique programmatic identifier for this node (e.g. 'tier1_courtyard_entrance').")]
    public string nodeId = "node_id";

    [Tooltip("Display title shown on the map and loading screen (e.g. 'North Ramparts').")]
    public string nodeTitle = "Sector Title";

    [Tooltip("Subtitle or region name (e.g. 'Windy Battlements').")]
    public string subtitle = "Sector Region";

    [TextArea(2, 5)]
    [Tooltip("Area lore or tactical briefing description.")]
    public string description = "Brief tactical overview of this sector.";

    [Header("Encounter Config")]
    [Tooltip("Scene name to load when this node is deployed.")]
    public string sceneName = "Bladehold Survivors Scene";

    [Tooltip("Encounter classification of this node.")]
    public CampaignNodeType nodeType = CampaignNodeType.Combat;

    [Tooltip("Tier index in the campaign graph (1-8).")]
    public int tierIndex = 1;

    [Tooltip("Relative visual position on the overview map canvas.")]
    public Vector2 mapPosition = Vector2.zero;

    [Header("Clan Captain (Optional)")]
    [Tooltip("Name of the Clan Captain commanding this sector (e.g. 'Captain Fraglob', 'Captain Kombusta'). Leave blank for standard waves.")]
    public string captainName = "";

    [Tooltip("Difficulty tier scaling for this encounter.")]
    public BannerDifficultyTier difficultyTier = BannerDifficultyTier.Standard;

    [Tooltip("Optional Clan Buff SO applied to the horde in this sector.")]
    public WarBannerClanSO clanBuff;

    [Tooltip("Portrait or badge sprite representing the captain.")]
    public Sprite captainIcon;

    [Header("Rewards Preview")]
    [Tooltip("Type of banner bounty awarded on wave/sector clear.")]
    public BannerBountyType bountyType = BannerBountyType.None;

    [Tooltip("Summary of rewards granted upon clearing this sector.")]
    public string rewardsDescription = "";

    [Tooltip("In-run gold granted on completion.")]
    public int goldReward = 0;

    [Tooltip("Permanent Goblin Blood awarded on completion.")]
    public int bloodReward = 0;

    [Tooltip("Permanent Orcish Metal awarded on completion.")]
    public int metalReward = 0;

    [Tooltip("Optional icon representing the primary reward.")]
    public Sprite rewardIcon;

    [Header("Graph Connections")]
    [Tooltip("Connected forward child nodes available after completing this node.")]
    public List<CampaignNodeSO> nextNodes = new List<CampaignNodeSO>();

    public bool HasCaptain => !string.IsNullOrEmpty(captainName);
    public bool HasClanBuff => clanBuff != null;
    public bool HasRewards => goldReward > 0 || bloodReward > 0 || metalReward > 0 || bountyType != BannerBountyType.None;
}
