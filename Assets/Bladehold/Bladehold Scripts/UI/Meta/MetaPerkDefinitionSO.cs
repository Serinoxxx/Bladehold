using UnityEngine;

/// <summary>
///     Definition of a permanent meta-progression perk purchased with Goblin Blood.
///     A perk can have several ranks: <see cref="rankCosts" /> holds the Goblin Blood price of each rank
///     (its length is the max rank) and <see cref="rankValues" /> the total effect at each rank, which game
///     code reads through <see cref="RunSession.GetMetaPerkValue" />. Leave both empty for a one-off perk
///     bought for <see cref="goblinBloodCost" />.
/// </summary>
[CreateAssetMenu(fileName = "MetaPerkDefinitionSO", menuName = "Scriptable Objects/MetaPerkDefinitionSO")]
public class MetaPerkDefinitionSO : ScriptableObject
{
    public string id = "backstab";
    public string displayName = "Backstab";
    [Tooltip("{0} is replaced by the perk's value at the rank being described (a ranked perk's rankValues).")]
    [TextArea(2, 4)] public string description = "Deal +{0}% bonus damage when striking enemies from behind.";
    public int tier = 1;
    [Tooltip("Price of a one-off perk (no rankCosts).")]
    public int goblinBloodCost = 10;
    [Tooltip("Goblin Blood price of each rank, in order. Its length is the max rank. Empty = one rank at goblinBloodCost.")]
    public int[] rankCosts;
    [Tooltip("Total effect at each rank (rank 1 first), e.g. 20/40/60/80 supply. Must match rankCosts' length.")]
    public float[] rankValues;
    public Sprite icon;
    public MetaPerkDefinitionSO[] prerequisites;

    public int MaxRank => rankCosts != null && rankCosts.Length > 0 ? rankCosts.Length : 1;

    /// <summary>Goblin Blood price of buying <paramref name="rank" /> (1-based).</summary>
    public int CostForRank(int rank)
    {
        if (rankCosts == null || rankCosts.Length == 0) return goblinBloodCost;
        return rankCosts[Mathf.Clamp(rank, 1, rankCosts.Length) - 1];
    }

    /// <summary>Total effect at <paramref name="rank" /> (1-based); 0 at rank 0 or when the perk has no values.</summary>
    public float ValueAtRank(int rank)
    {
        if (rank <= 0 || rankValues == null || rankValues.Length == 0) return 0f;
        return rankValues[Mathf.Min(rank, rankValues.Length) - 1];
    }

    /// <summary>The description with {0} filled in for <paramref name="rank" /> (rank 1 when unowned).</summary>
    public string DescriptionForRank(int rank)
    {
        if (string.IsNullOrEmpty(description) || !description.Contains("{0}")) return description;
        return description.Replace("{0}", ValueAtRank(Mathf.Max(1, rank)).ToString("0.##"));
    }
}
