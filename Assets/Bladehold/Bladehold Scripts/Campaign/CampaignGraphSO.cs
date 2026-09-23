using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Organized container representing a horizontal tier of nodes in the Campaign Graph.
/// </summary>
[System.Serializable]
public class CampaignTier
{
    public int tierNumber = 1;
    public string tierName = "Tier";
    public List<CampaignNodeSO> nodes = new List<CampaignNodeSO>();

    public CampaignTier() { }

    public CampaignTier(int tierNumber, string tierName)
    {
        this.tierNumber = tierNumber;
        this.tierName = tierName;
        this.nodes = new List<CampaignNodeSO>();
    }
}

/// <summary>
///     ScriptableObject defining the full multi-tier branching node graph of the Castle Campaign.
///     Includes the 8-tier progression flow from Courtyard Entrance to Crypt Sanctum,
///     with branching paths, clan captains, stop scenes, and boss encounters.
/// </summary>
[CreateAssetMenu(fileName = "CampaignGraph_Castle", menuName = "Scriptable Objects/Campaign/Campaign Graph")]
public class CampaignGraphSO : ScriptableObject
{
    [Header("Starting Node")]
    [Tooltip("The initial deployment node for a new campaign run.")]
    public CampaignNodeSO rootNode;

    [Header("Campaign Tiers")]
    [Tooltip("Tiers of progression nodes from Tier 1 through Tier 8.")]
    public List<CampaignTier> tiers = new List<CampaignTier>();

    [Header("Flattened Node Registry")]
    [Tooltip("All nodes in this graph for fast lookup.")]
    public List<CampaignNodeSO> allNodes = new List<CampaignNodeSO>();

    /// <summary>
    ///     Finds a node by its unique programmatic identifier.
    /// </summary>
    public CampaignNodeSO GetNodeById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (allNodes != null)
        {
            for (int i = 0; i < allNodes.Count; i++)
            {
                if (allNodes[i] != null && string.Equals(allNodes[i].nodeId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return allNodes[i];
                }
            }
        }

        if (rootNode != null && string.Equals(rootNode.nodeId, id, StringComparison.OrdinalIgnoreCase))
        {
            return rootNode;
        }

        return null;
    }

    /// <summary>
    ///     Alias for GetNodeById.
    /// </summary>
    public CampaignNodeSO FindNode(string id) => GetNodeById(id);

    /// <summary>
    ///     Creates an in-memory instance of the default Castle Campaign graph with all 8 tiers and nodes connected.
    /// </summary>
    public static CampaignGraphSO CreateDefaultCampaignGraph()
    {
        CampaignGraphSO graph = ScriptableObject.CreateInstance<CampaignGraphSO>();
        graph.name = "CastleCampaignGraph_Runtime";
        graph.BuildDefaultGraph();
        return graph;
    }

    /// <summary>
    ///     Constructs the complete 8-tier Castle Campaign node graph.
    ///     Can be invoked in the Editor via ContextMenu or at runtime.
    /// </summary>
    [ContextMenu("Generate Castle Campaign Graph")]
    public void BuildDefaultGraph()
    {
        tiers.Clear();
        allNodes.Clear();

        // -------------------------------------------------------------
        // TIER 1: Courtyard Entrance (Center Start)
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier1 = CreateNode(
            "tier1_courtyard_entrance",
            "Courtyard Entrance",
            "Outer Castle Courtyard",
            "Defend the courtyard gates against the initial goblin assault. Fortify tower defense slots and secure the entryway.",
            "Bladehold Castle Courtyard",
            CampaignNodeType.Combat,
            1,
            new Vector2(100f, 0f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.WeaponDraft,
            rewardsDesc: "+100 Gold, Weapon Upgrade Draft",
            gold: 100,
            blood: 0,
            metal: 0
        );
        rootNode = nodeTier1;

        // -------------------------------------------------------------
        // TIER 2: Split into 2 options (North Ramparts / Armory Barracks)
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier2A = CreateNode(
            "tier2_north_ramparts",
            "North Ramparts",
            "Windy Battlements",
            "Hold the elevated stone battlements against Captain Fraglob and his vanguard. High elevation, windy gusts.",
            "Bladehold Castle Ramparts",
            CampaignNodeType.Combat,
            2,
            new Vector2(300f, 130f),
            captainName: "Captain Fraglob",
            difficultyTier: BannerDifficultyTier.Enraged,
            bountyType: BannerBountyType.GoldCache,
            rewardsDesc: "+250 Gold Cache, Captain Fraglob Bounty",
            gold: 250,
            blood: 0,
            metal: 0
        );

        CampaignNodeSO nodeTier2B = CreateNode(
            "tier2_armory_barracks",
            "Armory Barracks",
            "Fortified Guardhouse",
            "Clear out Captain Kombusta's incendiary sappers before they ignite the castle's armory storehouse.",
            "Bladehold Castle Armory",
            CampaignNodeType.Combat,
            2,
            new Vector2(300f, -130f),
            captainName: "Captain Kombusta",
            difficultyTier: BannerDifficultyTier.Enraged,
            bountyType: BannerBountyType.OrcishMetal,
            rewardsDesc: "+5 Orcish Metal, Iron Armaments",
            gold: 50,
            blood: 0,
            metal: 5
        );

        // Tier 2 Side-Route: Outer Moat Fishing Pond
        CampaignNodeSO nodeTier2Fishing = CreateNode(
            "tier2_fishing_pond",
            "Outer Moat Pond",
            "Castle Waterway",
            "Take a peaceful diversion along the outer moat. 60-second fishing frenzy for gold, metal, blood, and rare buff fish.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            2,
            new Vector2(300f, -240f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 1 branches to Tier 2 (Combat choices + Fishing side route)
        nodeTier1.nextNodes.Add(nodeTier2A);
        nodeTier1.nextNodes.Add(nodeTier2B);
        nodeTier1.nextNodes.Add(nodeTier2Fishing);

        // -------------------------------------------------------------
        // TIER 3: Rest Area & Fishing Pond Choices
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier3A = CreateNode(
            "tier3_castle_rest_area",
            "Castle Rest Area",
            "Sanctuary Courtyard",
            "Safe sanctuary within the fortress. Visit the shopkeeper, drink from the well, and draft defensive abilities.",
            "Bladehold Rest Area Scene",
            CampaignNodeType.RestArea,
            3,
            new Vector2(500f, 90f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "Shop, Healing Well, Ability Drafts",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Ponds replace supply rooms: Sanctuary Fishing Pond
        CampaignNodeSO nodeTier3Fishing = CreateNode(
            "tier3_fishing_pond",
            "Sanctuary Fishing Pond",
            "Inner Courtyard Springs",
            "A serene spring inside the sanctuary. Use your bow to hunt fish for 1 minute to collect resources and eat buff fish.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            3,
            new Vector2(500f, -90f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Additional Tier 3 Side-Route: Whispering Basin
        CampaignNodeSO nodeTier3Basin = CreateNode(
            "tier3_whispering_basin",
            "Whispering Basin",
            "Forgotten Grotto",
            "A secluded pond teeming with ancient subterranean fish. 60-second fishing frenzy for precious resources.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            3,
            new Vector2(500f, -240f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 2 choices lead forward to Tier 3 options
        nodeTier2A.nextNodes.Add(nodeTier3A);
        nodeTier2A.nextNodes.Add(nodeTier3Fishing);
        nodeTier2B.nextNodes.Add(nodeTier3A);
        nodeTier2B.nextNodes.Add(nodeTier3Fishing);
        nodeTier2B.nextNodes.Add(nodeTier3Basin);
        nodeTier2Fishing.nextNodes.Add(nodeTier3Fishing);
        nodeTier2Fishing.nextNodes.Add(nodeTier3Basin);

        // -------------------------------------------------------------
        // TIER 4: Great Banqueting Hall + Cistern Fishing Pond Side-Route
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier4 = CreateNode(
            "tier4_great_banqueting_hall",
            "Great Banqueting Hall",
            "Ruined Grand Feast Hall",
            "Fight through high-density enemy hordes among long banquet tables, chandeliers, and fallen tapestries.",
            "Bladehold Great Hall",
            CampaignNodeType.Combat,
            4,
            new Vector2(700f, 0f),
            captainName: "Captain Fraglob",
            difficultyTier: BannerDifficultyTier.Nightmare,
            bountyType: BannerBountyType.FortressDraft,
            rewardsDesc: "+200 Gold, +10 Goblin Blood, Fortress Draft",
            gold: 200,
            blood: 10,
            metal: 0
        );

        // Tier 4 Side-Route: Cistern Fishing Pond
        CampaignNodeSO nodeTier4Fishing = CreateNode(
            "tier4_fishing_pond",
            "Cistern Fishing Pond",
            "Subterranean Waterway",
            "A dark fortress cistern where cavern fish breed undisturbed. 60-second fishing frenzy for resources.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            4,
            new Vector2(700f, -150f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 3 options merge into Tier 4
        nodeTier3A.nextNodes.Add(nodeTier4);
        nodeTier3Fishing.nextNodes.Add(nodeTier4);
        nodeTier3Fishing.nextNodes.Add(nodeTier4Fishing);
        nodeTier3Basin.nextNodes.Add(nodeTier4Fishing);

        // -------------------------------------------------------------
        // TIER 5: Split into 2 options + Sunken Grotto Fishing Pond Side-Route
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier5A = CreateNode(
            "tier5_dungeon_oubliette",
            "Dungeon Oubliette",
            "Dark Subterranean Cells",
            "Delve into the castle depths where brute wardens guard heaps of harvested goblin blood.",
            "Bladehold Castle Dungeons",
            CampaignNodeType.Combat,
            5,
            new Vector2(900f, 130f),
            captainName: "Captain Fraglob",
            difficultyTier: BannerDifficultyTier.Nightmare,
            bountyType: BannerBountyType.GoblinBlood,
            rewardsDesc: "+25 Goblin Blood, Dungeon Spoils",
            gold: 50,
            blood: 25,
            metal: 0
        );

        CampaignNodeSO nodeTier5B = CreateNode(
            "tier5_royal_conservatory",
            "Royal Conservatory",
            "Overgrown Glass Arches",
            "Battle through shattered glass greenhouses and toxic fungal blooms guarded by Captain Kombusta's firebrands.",
            "Bladehold Castle Conservatory",
            CampaignNodeType.Combat,
            5,
            new Vector2(900f, -100f),
            captainName: "Captain Kombusta",
            difficultyTier: BannerDifficultyTier.Nightmare,
            bountyType: BannerBountyType.ElementDraft,
            rewardsDesc: "Elemental Upgrade Draft, +150 Gold",
            gold: 150,
            blood: 0,
            metal: 0
        );

        // Tier 5 Side-Route: Sunken Grotto Fishing Pond
        CampaignNodeSO nodeTier5Fishing = CreateNode(
            "tier5_fishing_pond",
            "Sunken Grotto Pond",
            "Flooded Cellars",
            "A flooded grotto deep in the castle cellars. 60-second fishing frenzy for precious resources and buff fish.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            5,
            new Vector2(900f, -240f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 4 branches to Tier 5
        nodeTier4.nextNodes.Add(nodeTier5A);
        nodeTier4.nextNodes.Add(nodeTier5B);
        nodeTier4Fishing.nextNodes.Add(nodeTier5B);
        nodeTier4Fishing.nextNodes.Add(nodeTier5Fishing);

        // -------------------------------------------------------------
        // TIER 6: Stop scene choice (Inner Rest Sanctuary / Royal Reflection Pond)
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier6A = CreateNode(
            "tier6_inner_rest_sanctuary",
            "Inner Rest Sanctuary",
            "High Chapel Sanctuary",
            "A secluded chapel near the keep. Tend to mortal wounds, purchase divine gear, and prepare for the final ascent.",
            "Bladehold Rest Area Scene",
            CampaignNodeType.RestArea,
            6,
            new Vector2(1100f, 90f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "Shop, Healing Well, Ability Drafts",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Ponds replace supply rooms: Royal Reflection Pond
        CampaignNodeSO nodeTier6Fishing = CreateNode(
            "tier6_fishing_pond",
            "Royal Reflection Pond",
            "High Chapel Gardens",
            "The serene ornamental pond of the High Chapel. 60-second fishing frenzy before the final ascent.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            6,
            new Vector2(1100f, -90f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 5 choices lead to Tier 6
        nodeTier5A.nextNodes.Add(nodeTier6A);
        nodeTier5A.nextNodes.Add(nodeTier6Fishing);
        nodeTier5B.nextNodes.Add(nodeTier6A);
        nodeTier5B.nextNodes.Add(nodeTier6Fishing);
        nodeTier5Fishing.nextNodes.Add(nodeTier6Fishing);

        // -------------------------------------------------------------
        // TIER 7: Pre-final battle / Inner Keep + Keep Aqueduct Pond Side-Route
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier7 = CreateNode(
            "tier7_throne_antechamber",
            "Throne Antechamber",
            "The Obsidian Portico",
            "Intense frontline defense outside the throne room doors. Waves of armored elites and siege beasts attempt to halt your advance.",
            "Bladehold Throne Antechamber",
            CampaignNodeType.PreBoss,
            7,
            new Vector2(1300f, 0f),
            captainName: "Captain Kombusta",
            difficultyTier: BannerDifficultyTier.Omega,
            bountyType: BannerBountyType.TrollHeart,
            rewardsDesc: "Omega Bounty: +350 Gold, +20 Blood, +5 Metal",
            gold: 350,
            blood: 20,
            metal: 5
        );

        // Tier 7 Side-Route: Keep Aqueduct Fishing Pond
        CampaignNodeSO nodeTier7Fishing = CreateNode(
            "tier7_fishing_pond",
            "Keep Aqueduct Pond",
            "Throne Waterway",
            "The fortified aqueduct supplying the upper keep. 60-second fishing frenzy to gain final buffs before the throne.",
            "Bladehold Fishing Pond",
            CampaignNodeType.FishingPond,
            7,
            new Vector2(1300f, -140f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "60s Fishing Frenzy: Gold, Metal, Blood, Buff Fish",
            gold: 0,
            blood: 0,
            metal: 0
        );

        // Tier 6 options merge into Tier 7
        nodeTier6A.nextNodes.Add(nodeTier7);
        nodeTier6A.nextNodes.Add(nodeTier7Fishing);
        nodeTier6Fishing.nextNodes.Add(nodeTier7);
        nodeTier6Fishing.nextNodes.Add(nodeTier7Fishing);

        // -------------------------------------------------------------
        // TIER 8: The Revelation / Necromancer Encounter (Crypt Sanctum)
        // -------------------------------------------------------------
        CampaignNodeSO nodeTier8 = CreateNode(
            "tier8_crypt_sanctum",
            "Crypt Sanctum",
            "Crypt of the Necromancer",
            "You breach the inner crypt to rescue Princess Katherine, only to discover the grim truth behind the horde's summoning.",
            "Bladehold Necromancer Crypt",
            CampaignNodeType.NecromancerEncounter,
            8,
            new Vector2(1500f, 0f),
            captainName: "",
            difficultyTier: BannerDifficultyTier.Standard,
            bountyType: BannerBountyType.None,
            rewardsDesc: "The Revelation: Story Dialogue & Boss Choice",
            gold: 0,
            blood: 0,
            metal: 0
        );

        CampaignNodeSO nodeTier8Princess = CreateNode(
            "tier8_princess_boss",
            "Princess Sanctuary",
            "The Royal Throne Annex",
            "Execute Princess Katherine to fulfill the dark covenant and claim the throne.",
            "Bladehold Princess Sanctuary",
            CampaignNodeType.PrincessBoss,
            8,
            new Vector2(1750f, 150f),
            captainName: "Princess Katherine",
            difficultyTier: BannerDifficultyTier.Omega,
            bountyType: BannerBountyType.GoldCache,
            rewardsDesc: "Royal Usurper: +500 Gold, +30 Blood, +10 Metal",
            gold: 500,
            blood: 30,
            metal: 10
        );

        CampaignNodeSO nodeTier8NecroBoss = CreateNode(
            "tier8_necromancer_boss",
            "Necromancer's Crypt",
            "Catacombs of the Fallen",
            "Defy Malakor the Defiler and shatter his dark legion of skeletons.",
            "Bladehold Necromancer Crypt",
            CampaignNodeType.NecromancerBoss,
            8,
            new Vector2(1750f, -150f),
            captainName: "Malakor the Defiler",
            difficultyTier: BannerDifficultyTier.Omega,
            bountyType: BannerBountyType.GoldCache,
            rewardsDesc: "Crypt Liberator: +500 Gold, +30 Blood, +10 Metal",
            gold: 500,
            blood: 30,
            metal: 10
        );

        // Tier 7 leads into Tier 8
        nodeTier7.nextNodes.Add(nodeTier8);
        nodeTier8.nextNodes.Add(nodeTier8Princess);
        nodeTier8.nextNodes.Add(nodeTier8NecroBoss);

        // -------------------------------------------------------------
        // Populate Tiers list
        // -------------------------------------------------------------
        CampaignTier tier1 = new CampaignTier(1, "Outer Gate");
        tier1.nodes.Add(nodeTier1);
        tiers.Add(tier1);

        CampaignTier tier2 = new CampaignTier(2, "Battlements & Barracks");
        tier2.nodes.Add(nodeTier2A);
        tier2.nodes.Add(nodeTier2B);
        tier2.nodes.Add(nodeTier2Fishing);
        tiers.Add(tier2);

        CampaignTier tier3 = new CampaignTier(3, "Fortress Recovery");
        tier3.nodes.Add(nodeTier3A);
        tier3.nodes.Add(nodeTier3Fishing);
        tier3.nodes.Add(nodeTier3Basin);
        tiers.Add(tier3);

        CampaignTier tier4 = new CampaignTier(4, "Great Hall");
        tier4.nodes.Add(nodeTier4);
        tier4.nodes.Add(nodeTier4Fishing);
        tiers.Add(tier4);

        CampaignTier tier5 = new CampaignTier(5, "Subterranean & Grounds");
        tier5.nodes.Add(nodeTier5A);
        tier5.nodes.Add(nodeTier5B);
        tier5.nodes.Add(nodeTier5Fishing);
        tiers.Add(tier5);

        CampaignTier tier6 = new CampaignTier(6, "Inner Sanctum Recovery");
        tier6.nodes.Add(nodeTier6A);
        tier6.nodes.Add(nodeTier6Fishing);
        tiers.Add(tier6);

        CampaignTier tier7 = new CampaignTier(7, "Throne Antechamber");
        tier7.nodes.Add(nodeTier7);
        tier7.nodes.Add(nodeTier7Fishing);
        tiers.Add(tier7);

        CampaignTier tier8 = new CampaignTier(8, "Crypt of Revelation");
        tier8.nodes.Add(nodeTier8);
        tier8.nodes.Add(nodeTier8Princess);
        tier8.nodes.Add(nodeTier8NecroBoss);
        tiers.Add(tier8);

        // -------------------------------------------------------------
        // Populate allNodes list
        // -------------------------------------------------------------
        allNodes.Add(nodeTier1);
        allNodes.Add(nodeTier2A);
        allNodes.Add(nodeTier2B);
        allNodes.Add(nodeTier2Fishing);
        allNodes.Add(nodeTier3A);
        allNodes.Add(nodeTier3Fishing);
        allNodes.Add(nodeTier3Basin);
        allNodes.Add(nodeTier4);
        allNodes.Add(nodeTier4Fishing);
        allNodes.Add(nodeTier5A);
        allNodes.Add(nodeTier5B);
        allNodes.Add(nodeTier5Fishing);
        allNodes.Add(nodeTier6A);
        allNodes.Add(nodeTier6Fishing);
        allNodes.Add(nodeTier7);
        allNodes.Add(nodeTier7Fishing);
        allNodes.Add(nodeTier8);
        allNodes.Add(nodeTier8Princess);
        allNodes.Add(nodeTier8NecroBoss);

        Debug.Log($"[CampaignGraphSO] Built default Castle Campaign Graph: 8 Tiers, {allNodes.Count} Nodes (including 7 Fishing Ponds).");
    }

    private static CampaignNodeSO CreateNode(
        string id,
        string title,
        string subtitle,
        string description,
        string sceneName,
        CampaignNodeType nodeType,
        int tierIndex,
        Vector2 mapPosition,
        string captainName = "",
        BannerDifficultyTier difficultyTier = BannerDifficultyTier.Standard,
        BannerBountyType bountyType = BannerBountyType.None,
        string rewardsDesc = "",
        int gold = 0,
        int blood = 0,
        int metal = 0)
    {
        CampaignNodeSO node = ScriptableObject.CreateInstance<CampaignNodeSO>();
        node.name = $"Node_{id}";
        node.nodeId = id;
        node.nodeTitle = title;
        node.subtitle = subtitle;
        node.description = description;
        node.sceneName = sceneName;
        node.nodeType = nodeType;
        node.tierIndex = tierIndex;
        node.mapPosition = mapPosition;
        node.captainName = captainName;
        node.difficultyTier = difficultyTier;
        node.bountyType = bountyType;
        node.rewardsDescription = rewardsDesc;
        node.goldReward = gold;
        node.bloodReward = blood;
        node.metalReward = metal;
        node.nextNodes = new List<CampaignNodeSO>();
        return node;
    }
}
