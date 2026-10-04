using System;
using System.Globalization;
using UnityEngine;

/// <summary>
///     Shared tunables and banner content for one biome world event (Eruption, Blizzard, Thunderstorm,
///     Stampede, Blood Moon, Goblin Caravan). Each event type subclasses this for its hazard numbers; the
///     <see cref="WorldEvent" /> component on the scene's event prefab reads it. The banner text lives here
///     (with <see cref="Loc" /> keys) so wording is tuned on the asset, not in code.
/// </summary>
public abstract class WorldEventSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id (telemetry, DevConsole, localization key prefix), e.g. 'eruption'.")]
    public string id = "event";

    [Tooltip("Loc key + English for the banner's big title, e.g. ERUPTION.")]
    public string titleKey = "world_event.x.title";
    public string title = "EVENT";

    [Tooltip("Loc key + English for the one-line flavour under the title.")]
    public string taglineKey = "world_event.x.tagline";
    public string tagline = "Something stirs.";

    [Tooltip("The event's emblem on the banner and the timer chip.")]
    public Sprite icon;

    [Tooltip("Banner accent: title glow, icon disc, timer fill.")]
    public Color accentColor = Color.white;

    [Header("Banner effect lines (pros first, then cons)")]
    public WorldEventEffectLine[] effects = Array.Empty<WorldEventEffectLine>();

    [Header("Timing")]
    [Tooltip("Seconds the event lasts once it starts (it also ends early when the wave ends).")]
    [Min(5f)] public float duration = 45f;

    [Tooltip("Seconds of calm after the banner before the first hazard, so the player can read it.")]
    [Min(0f)] public float hazardGraceSeconds = 3f;

    [Tooltip("Seconds the post-processing and weather fade in/out.")]
    [Min(0.1f)] public float fadeSeconds = 2.5f;

    [Header("Selection")]
    [Tooltip("Relative pick weight when a scene allows several events.")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("The earliest wave (1-based) this event may roll on.")]
    [Min(1)] public int minWave = 2;

    [Header("Player buff while active (optional)")]
    [Tooltip("Off = no stat change.")]
    public bool hasBuff;
    public StatType buffStat = StatType.FireDamageBonus;
    public ModifierKind buffKind = ModifierKind.Flat;
    [Tooltip("Added while the event runs, removed exactly when it ends. Bonus stats with base 0 need Flat (0.5 = +50%).")]
    public float buffAmount = 0.5f;

    public string Title => Loc.Get(titleKey, title);
    public string Tagline => Loc.Get(taglineKey, tagline);

    /// <summary>The buff as a whole-number percent for "{0}" in effect lines (0.5 → "50").</summary>
    public string BuffPercentText => Mathf.RoundToInt(Mathf.Abs(buffAmount) * 100f).ToString(CultureInfo.InvariantCulture);

    /// <summary>An effect line's display text, with {0} = the buff percent and {1} = the event duration in seconds.</summary>
    public string FormatLine(WorldEventEffectLine line)
    {
        string text = Loc.Get(line.textKey, line.text);
        return text.Replace("{0}", BuffPercentText)
            .Replace("{1}", Mathf.RoundToInt(duration).ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>One pro or con row on the world-event banner.</summary>
[Serializable]
public struct WorldEventEffectLine
{
    [Tooltip("On = a boon (green chip). Off = a hazard (red chip).")]
    public bool positive;
    public Sprite icon;
    public string textKey;
    [Tooltip("English. {0} = the buff percent, {1} = the event duration in seconds. Keep it under ~28 characters: it's read mid-fight.")]
    public string text;
}
