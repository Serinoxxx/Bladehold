using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Where a wave objective is fought: at the gate under the towers, or out in the field away from them.</summary>
public enum WaveStance
{
    None,
    Defence,
    Offence
}

/// <summary>
///     How an objective can be lost. Defence rows are <see cref="None" />: the gate falling is their
///     only risk, and that ends the whole run.
/// </summary>
public enum WaveFailRule
{
    None,
    Timer,
    Escapes
}

/// <summary>One row of <c>Config/WaveObjectives.csv</c>. <see cref="id" /> matches <see cref="ISurvivorsObjective.ObjectiveId" />.</summary>
[Serializable]
public class WaveObjectiveDefinition
{
    public string id;
    public WaveStance stance;
    public string locKey;
    public string icon;
    public float timerSeconds;
    public WaveFailRule failRule;
    public float failParam;
    public float weight;
    public int minWave;
    public int minThreat;
    public bool draftable;
    public bool demoEnabled;

    // English source text; Loc.Get(key, english) lets Strings.csv override it per language.
    public string title;
    public string rule;
    public string failText;
    public string threatText;

    // Plan 22: objectives are optional bonuses; completing one pays this on top of the card. Failing pays nothing.
    public int bonusGold;
    public int bonusSupply;

    public bool HasTimer => timerSeconds > 0f;
    public bool HasBonus => bonusGold > 0 || bonusSupply > 0;

    public string TitleText => Loc.Get($"wave.obj.{locKey}.title", title);
    public string RuleText => Loc.Get($"wave.obj.{locKey}.rule", rule);
    public string FailLineText => string.IsNullOrEmpty(failText) ? "" : Loc.Get($"wave.obj.{locKey}.fail", failText);
    public string ThreatLineText => string.IsNullOrEmpty(threatText) ? "" : Loc.Get($"wave.obj.{locKey}.threat", threatText);
}

/// <summary>
///     Parses <c>WaveObjectives.csv</c> (the TextAsset on <see cref="WaveChoiceConfigSO" />) into
///     <see cref="WaveObjectiveDefinition" /> rows. Pure data, no scene state, so the benchmark and the
///     card generator read exactly what the game does.
/// </summary>
public class WaveObjectiveCatalog
{
    // Columns: id,stance,locKey,icon,timerSeconds,failRule,failParam,weight,minWave,minThreat,draftable,demoEnabled,title,rule,failText,threatText,bonusGold,bonusSupply
    private const int RequiredColumns = 12;

    private readonly List<WaveObjectiveDefinition> rows = new List<WaveObjectiveDefinition>();
    private readonly Dictionary<string, WaveObjectiveDefinition> byId = new Dictionary<string, WaveObjectiveDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<WaveObjectiveDefinition> All => rows;

    public WaveObjectiveDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return byId.TryGetValue(id, out WaveObjectiveDefinition def) ? def : null;
    }

    public static WaveObjectiveCatalog Parse(string csvText, List<string> errors = null)
    {
        WaveObjectiveCatalog catalog = new WaveObjectiveCatalog();
        if (string.IsNullOrEmpty(csvText))
        {
            Report(errors, "WaveObjectives.csv is missing or empty.");
            return catalog;
        }

        using (StringReader reader = new StringReader(csvText))
        {
            reader.ReadLine(); // header
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                WaveObjectiveDefinition def = ParseRow(line, lineNumber, errors);
                if (def == null) continue;
                if (catalog.byId.ContainsKey(def.id))
                {
                    Report(errors, $"Line {lineNumber}: duplicate objective id '{def.id}'.");
                    continue;
                }
                catalog.rows.Add(def);
                catalog.byId[def.id] = def;
            }
        }
        return catalog;
    }

    private static WaveObjectiveDefinition ParseRow(string line, int lineNumber, List<string> errors)
    {
        List<string> cols = DraftUpgradeService.ParseCsvRow(line);
        if (cols.Count < RequiredColumns)
        {
            Report(errors, $"Line {lineNumber}: expected at least {RequiredColumns} columns, got {cols.Count}.");
            return null;
        }

        string Col(int i) => i < cols.Count ? cols[i].Trim() : "";

        WaveObjectiveDefinition def = new WaveObjectiveDefinition
        {
            id = Col(0),
            locKey = Col(2),
            icon = Col(3),
            timerSeconds = ParseFloat(Col(4)),
            failParam = ParseFloat(Col(6)),
            weight = ParseFloat(Col(7)),
            minWave = ParseInt(Col(8)),
            minThreat = ParseInt(Col(9)),
            draftable = ParseBool(Col(10)),
            demoEnabled = ParseBool(Col(11)),
            title = Col(12),
            rule = Col(13),
            failText = Col(14),
            threatText = Col(15),
            bonusGold = Mathf.Max(0, ParseInt(Col(16))),
            bonusSupply = Mathf.Max(0, ParseInt(Col(17)))
        };

        if (string.IsNullOrEmpty(def.id))
        {
            Report(errors, $"Line {lineNumber}: empty objective id.");
            return null;
        }
        if (!Enum.TryParse(Col(1), true, out def.stance))
        {
            Report(errors, $"Objective '{def.id}': unknown stance '{Col(1)}' (Defence, Offence or None).");
            return null;
        }
        if (!Enum.TryParse(Col(5), true, out def.failRule))
        {
            Report(errors, $"Objective '{def.id}': unknown failRule '{Col(5)}' (Timer, Escapes or None).");
            return null;
        }
        if (def.draftable && def.stance == WaveStance.None)
        {
            Report(errors, $"Objective '{def.id}' is draftable but has no stance; it can't be offered.");
            def.draftable = false;
        }
        if (def.failRule == WaveFailRule.Timer && !def.HasTimer)
        {
            Report(errors, $"Objective '{def.id}' fails on a timer but has no timerSeconds.");
        }
        if (string.IsNullOrEmpty(def.locKey)) def.locKey = def.id;
        if (string.IsNullOrEmpty(def.title)) def.title = def.id;
        return def;
    }

    private static void Report(List<string> errors, string message)
    {
        if (errors != null) errors.Add(message);
        else Debug.LogError($"[WaveObjectiveCatalog] {message}");
    }

    private static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    private static int ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static bool ParseBool(string s) =>
        string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
}
