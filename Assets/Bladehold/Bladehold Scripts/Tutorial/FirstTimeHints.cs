using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     One-time contextual hints (plan 16, phase I): the first time the gate takes damage, a tower runs
///     low, the gate needs repair, the wave cards open, the player returns to the Meta Area with
///     Goblin Blood, comes back with the first-run gift (<see cref="FirstRunGift" />), or reaches the Fishing Pond. Each id shows once per save (<see cref="SaveData.seenHints" />), on
///     <see cref="TutorialHintUI" /> as a timed overlay, with an optional HUD waypoint while it's up.
///     Triggers live in <see cref="FirstTimeHintsWatcher" />.
/// </summary>
public static class FirstTimeHints
{
    public const string GateDamaged = "gate_damaged";
    public const string GateRepair = "gate_repair";
    public const string TowerRestock = "tower_restock";
    public const string WaveCards = "wave_cards";
    public const string MetaSpirit = "meta_spirit";
    public const string MetaGift = "meta_gift";
    public const string FishingPond = "fishing_pond";

    public static bool HasSeen(string id)
    {
        List<string> seen = SaveSystem.Load().seenHints;
        return seen != null && seen.Contains(id);
    }

    /// <summary>Shows the hint if it hasn't been seen, marks it seen, and returns whether it showed.</summary>
    public static bool TryShow(string id, TutorialHint hint, float seconds, Transform waypoint = null)
    {
        if (TutorialHintUI.Instance == null || hint == null || HasSeen(id)) return false;

        SaveData data = SaveSystem.Load();
        if (data.seenHints == null) data.seenHints = new List<string>();
        data.seenHints.Add(id);
        SaveSystem.Save(data);

        TutorialHintUI.Instance.ShowTimed(hint, seconds);
        if (waypoint != null) FirstTimeHintsWatcher.PointAt(waypoint, seconds);
        return true;
    }
}
