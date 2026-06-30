using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The player's central stat aggregation layer, reached through <see cref="Player.Instance" />.<c>.Stats</c>.
///     Each <see cref="StatType" /> holds a <em>base</em> value plus accumulated flat and percent modifiers;
///     the effective value is <c>(base + Σflat) × (1 + Σpercent)</c>.
///
///     Bases are <b>not</b> stored here as authored data — they are registered at runtime by whichever
///     system owns the value (the sword's <see cref="DamageTrigger" /> registers its damage/range from its
///     <see cref="DamageSO" />, the <see cref="PlayerMoveSpeedBinder" /> registers move speed from the
///     controller, etc.). That keeps the underlying ScriptableObjects/controllers as the source of base
///     truth and means this component never mutates them.
///
///     Upgrades call <see cref="AddModifier" />; consumers call <see cref="GetValue" />. <see cref="OnStatChanged" />
///     fires whenever a base or modifier changes so consumers (e.g. the move-speed binder) can refresh.
///     Modifiers can be added before or after a base is registered — the final value is recomputed on read,
///     so initialization order between systems does not matter.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    private class Entry
    {
        public bool hasBase;
        public float baseValue;
        public float flat;
        public float percent;

        public float Value => (baseValue + flat) * (1f + percent);
    }

    private readonly Dictionary<StatType, Entry> entries = new Dictionary<StatType, Entry>();

    /// <summary>Raised whenever the base or a modifier of the given stat changes.</summary>
    public event Action<StatType> OnStatChanged;

    /// <summary>
    ///     Registers (or replaces) the base value of a stat. Called once in <c>Start</c> by the system that
    ///     owns the value. Safe to call after modifiers have already been added.
    /// </summary>
    public void SetBase(StatType stat, float value)
    {
        Entry entry = GetOrCreate(stat);
        entry.hasBase = true;
        entry.baseValue = value;
        OnStatChanged?.Invoke(stat);
    }

    /// <summary>Adds a modifier of the given kind. Duplicate modifiers stack additively.</summary>
    public void AddModifier(StatType stat, ModifierKind kind, float amount)
    {
        Entry entry = GetOrCreate(stat);
        if (kind == ModifierKind.Flat)
        {
            entry.flat += amount;
        }
        else
        {
            entry.percent += amount;
        }
        OnStatChanged?.Invoke(stat);
    }

    /// <summary>The effective value of a stat, or 0 if nothing has registered it yet.</summary>
    public float GetValue(StatType stat)
    {
        return entries.TryGetValue(stat, out Entry entry) ? entry.Value : 0f;
    }

    /// <summary>The registered base value of a stat (before modifiers), or 0 if unregistered.</summary>
    public float GetBase(StatType stat)
    {
        return entries.TryGetValue(stat, out Entry entry) ? entry.baseValue : 0f;
    }

    private Entry GetOrCreate(StatType stat)
    {
        if (!entries.TryGetValue(stat, out Entry entry))
        {
            entry = new Entry();
            entries[stat] = entry;
        }
        return entry;
    }
}
