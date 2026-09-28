using UnityEngine;

/// <summary>
///     Reads an objective's row from <c>Config/WaveObjectives.csv</c> (via <see cref="WaveChoiceConfigSO" />)
///     so the wave card and the running objective use the same timer and fail numbers. Every call
///     falls back to the objective's own serialized value when there's no config asset or no row.
/// </summary>
public static class ObjectiveCsv
{
    /// <summary>Preview waypoints are drawn at this alpha so they read as "will happen here", not live targets.</summary>
    public const float PreviewAlpha = 0.6f;

    /// <summary>The CSV row whose <c>id</c> matches <paramref name="objectiveId" />, or null.</summary>
    public static WaveObjectiveDefinition Row(string objectiveId)
    {
        WaveChoiceConfigSO config = WaveChoiceConfigSO.Load();
        return config != null ? config.Catalog?.Get(objectiveId) : null;
    }

    /// <summary>
    ///     The row's <c>timerSeconds</c>, or <paramref name="fallback" /> with no row. A row with 0 means
    ///     "no timer", which the timed-fail objectives honour (their &lt;= 0 already means no limit).
    /// </summary>
    public static float TimerSeconds(string objectiveId, float fallback)
    {
        WaveObjectiveDefinition row = Row(objectiveId);
        return row != null ? row.timerSeconds : fallback;
    }

    /// <summary>
    ///     Like <see cref="TimerSeconds" />, for objectives that can't run without a duration (Goblin
    ///     Rush, Golden Goblin): a missing row or a non-positive value keeps <paramref name="fallback" />.
    /// </summary>
    public static float RequiredDuration(string objectiveId, float fallback)
    {
        WaveObjectiveDefinition row = Row(objectiveId);
        return row != null && row.timerSeconds > 0f ? row.timerSeconds : fallback;
    }

    /// <summary>The row's <c>failParam</c>, or <paramref name="fallback" /> with no row.</summary>
    public static float FailParam(string objectiveId, float fallback)
    {
        WaveObjectiveDefinition row = Row(objectiveId);
        return row != null ? row.failParam : fallback;
    }

    /// <summary>The row's localized title (<c>wave.obj.&lt;locKey&gt;.title</c>), or <paramref name="fallback" /> with no row.</summary>
    public static string Title(string objectiveId, string fallback)
    {
        WaveObjectiveDefinition row = Row(objectiveId);
        return row != null ? row.TitleText : fallback;
    }

    /// <summary>The row's localized rule line, or <paramref name="fallback" /> with no row.</summary>
    public static string Rule(string objectiveId, string fallback)
    {
        WaveObjectiveDefinition row = Row(objectiveId);
        return row != null && !string.IsNullOrEmpty(row.rule) ? row.RuleText : fallback;
    }

    /// <summary><paramref name="color" /> at <see cref="PreviewAlpha" />.</summary>
    public static Color PreviewTint(Color color) => new Color(color.r, color.g, color.b, PreviewAlpha);

    /// <summary>"m:ss" for HUD timers.</summary>
    public static string FormatClock(float seconds)
    {
        int totalSec = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{totalSec / 60}:{totalSec % 60:D2}";
    }
}
