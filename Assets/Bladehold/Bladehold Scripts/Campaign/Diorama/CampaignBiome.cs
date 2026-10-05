/// <summary>
///     The landscape a campaign node's patch of the map diorama is dressed as. <see cref="Auto" /> picks
///     one from the node's scene (<see cref="Resolve" />), so new nodes look right without setting it.
/// </summary>
public enum CampaignBiome
{
    Auto,
    Grassland,
    Desert,
    Alpine,
    Graveyard,
    Lake,
    Forest,
    CastleCourt,
    Crypt,
    Sanctuary,
    Snowfield // appended: keeps serialized values stable
}

public static class CampaignBiomes
{
    /// <summary>The node's biome: its explicit <see cref="CampaignNodeSO.mapBiome" />, else one guessed from its scene and type.</summary>
    public static CampaignBiome Resolve(CampaignNodeSO node)
    {
        if (node == null) return CampaignBiome.Grassland;
        if (node.mapBiome != CampaignBiome.Auto) return node.mapBiome;

        switch (node.nodeType)
        {
            case CampaignNodeType.FishingPond: return CampaignBiome.Lake;
            case CampaignNodeType.RestArea: return CampaignBiome.Forest;
            case CampaignNodeType.NecromancerEncounter:
            case CampaignNodeType.NecromancerBoss: return CampaignBiome.Crypt;
            case CampaignNodeType.PrincessBoss: return CampaignBiome.Sanctuary;
        }

        string scene = (node.sceneName ?? string.Empty).ToLowerInvariant();
        if (scene.Contains("desert")) return CampaignBiome.Desert;
        if (scene.Contains("outer gate")) return CampaignBiome.Snowfield;
        if (scene.Contains("frozen") || scene.Contains("alpine") || scene.Contains("snow")) return CampaignBiome.Alpine;
        if (scene.Contains("graveyard")) return CampaignBiome.Graveyard;
        if (scene.Contains("garden") || scene.Contains("conservatory")) return CampaignBiome.Forest;
        if (scene.Contains("crypt")) return CampaignBiome.Crypt;
        if (scene.Contains("sanctuary")) return CampaignBiome.Sanctuary;
        if (scene.Contains("hall") || scene.Contains("throne") || scene.Contains("dungeon") || scene.Contains("castle")) return CampaignBiome.CastleCourt;
        return CampaignBiome.Grassland;
    }
}
