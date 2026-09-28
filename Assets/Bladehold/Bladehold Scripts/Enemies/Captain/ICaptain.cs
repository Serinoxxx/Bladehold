using System;

/// <summary>
///     What every Clan Captain controller exposes to the spawn flow: <see cref="GameLoopManager" /> calls
///     <see cref="Initialize" /> after spawning one, and objectives/UI read its name and tier.
/// </summary>
public interface ICaptain
{
    string CaptainName { get; }
    BannerDifficultyTier DifficultyTier { get; }

    /// <summary>Sets the tier (HP/damage scaling, outline colour) and optionally a display name.</summary>
    void Initialize(BannerDifficultyTier tier, string customName = null);
}

/// <summary>
///     Captain display name → how to spawn it. Replaces the old Kombusta/Fraglob substring branches in
///     <see cref="GameLoopManager.SpawnCaptainForWave" />. Campaign nodes and wave cards name a captain by
///     display name; <see cref="Find" /> matches on each entry's <see cref="CaptainEntry.matchKey" />.
/// </summary>
public static class CaptainRegistry
{
    public sealed class CaptainEntry
    {
        public string displayName;
        /// <summary>Substring a node/card captain name must contain (case-insensitive).</summary>
        public string matchKey;
        /// <summary>Enemies.csv row spawned through the spawner; null = the captain has no roster row.</summary>
        public string rosterId;
    }

    public const string KombustaName = "Captain Kombusta";
    public const string FraglobName = "Captain Fraglob";
    public const string MograName = "Captain Mogra Hexfang";

    public static readonly CaptainEntry Kombusta = new CaptainEntry { displayName = KombustaName, matchKey = "Kombusta", rosterId = "captain_kombusta" };
    // Fraglob has no roster row or generated prefab; GameLoopManager.captainPrefab is its only spawn path.
    public static readonly CaptainEntry Fraglob = new CaptainEntry { displayName = FraglobName, matchKey = "Fraglob", rosterId = null };
    public static readonly CaptainEntry Mogra = new CaptainEntry { displayName = MograName, matchKey = "Mogra", rosterId = "captain_mogra" };

    public static readonly CaptainEntry[] All = { Kombusta, Fraglob, Mogra };

    /// <summary>The entry whose match key the name contains, or null (blank or unknown name).</summary>
    public static CaptainEntry Find(string captainName)
    {
        if (string.IsNullOrEmpty(captainName)) return null;
        foreach (CaptainEntry entry in All)
        {
            if (captainName.IndexOf(entry.matchKey, StringComparison.OrdinalIgnoreCase) >= 0) return entry;
        }
        return null;
    }
}
