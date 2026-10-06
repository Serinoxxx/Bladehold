/// <summary>
///     Core resource-yielding fish variants in the Fishing Minigame.
/// </summary>
public enum ResourceFishType
{
    Gold,
    OrcMetal,
    GoblinBlood,
    Diamond,
    /// <summary>Plan 21 phase 5: one per pond visit, high HP, pays an Arcane Core (banked on leaving).</summary>
    ArcaneFish
}

/// <summary>
///     Special buff fish variants providing permanent in-run character enhancements.
/// </summary>
public enum BuffFishType
{
    Speedy,
    Armored,
    Fire,
    Frost,
    Spark,
    Savage
}
