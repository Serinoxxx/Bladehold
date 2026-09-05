using UnityEngine;

/// <summary>
///     ScriptableObject defining an interchangeable reward offered by a War Banner.
///     Enables designers to easily modify reward bounties, icons, quantities, and pool assignments in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "BannerReward_", menuName = "Scriptable Objects/War Banner/Banner Reward")]
public class WarBannerRewardSO : ScriptableObject
{
    [Header("Reward Identity")]
    [Tooltip("Display title of the reward (e.g. 'Gold Cache', 'Weapon Upgrade Draft').")]
    public string rewardName = "Reward";

    [Tooltip("Underlying bounty category claimed by the player upon tearing down the banner.")]
    public BannerBountyType bountyType = BannerBountyType.None;

    [Tooltip("Icon representing the reward (e.g. gold ingot, sword, iron bar, potion).")]
    public Sprite rewardIcon;

    [Header("Display Quantity")]
    [Tooltip("Formatted quantity string shown on the banner quick-facts card (e.g. 'x 100', 'x 3', 'x 1 Draft').")]
    public string quantityText = "x 1";

    [Header("Reward Amount Range")]
    [Tooltip("Minimum amount awarded if numeric (e.g. 75 gold).")]
    public int minAmount = 1;

    [Tooltip("Maximum amount awarded if numeric (e.g. 125 gold).")]
    public int maxAmount = 1;

    [Header("Designer Notes")]
    [TextArea(2, 4)]
    [Tooltip("Designer notes or reward description.")]
    public string rewardDescription = "";

    /// <summary>
    ///     Gets the formatted quantity string for display, falling back to min/max range if empty.
    /// </summary>
    public string GetFormattedQuantity()
    {
        if (!string.IsNullOrEmpty(quantityText))
            return quantityText;

        if (minAmount == maxAmount)
            return $"x {minAmount}";

        return $"x {minAmount}–{maxAmount}";
    }
}
