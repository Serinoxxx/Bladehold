/// <summary>
///     Defines the tactical encounter type of a node in the Castle Campaign graph.
/// </summary>
public enum CampaignNodeType
{
    Combat,
    RestArea,
    SupplyRoom, // Unused: the Supply Room was deleted in plan 08. Kept so serialized node types don't shift.
    FishingPond,
    PreBoss,
    NecromancerEncounter,
    PrincessBoss,
    NecromancerBoss
}
