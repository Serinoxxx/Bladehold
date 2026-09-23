using System;

/// <summary>
///     Identifies minigame-exclusive draft upgrade cards offered during the 60-second Fishing Frenzy.
/// </summary>
public enum FishingUpgradeType
{
    BounceShot,
    Fishsploshion,
    IceyWater,
    FishSkewer,
    Bleed,
    FatFish
}

[Serializable]
public class FishingUpgradeCardInfo
{
    public FishingUpgradeType type;
    public string title;
    public string description;
    public int currentLevel;
    public int maxLevel = 4;
}
