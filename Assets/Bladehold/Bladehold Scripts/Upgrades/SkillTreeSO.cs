using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
///     The skill-tree definition, authored as a CSV and parsed into <see cref="SkillNode" />s. Designers
///     edit the CSV in any spreadsheet tool; Unity auto-reimports the <see cref="TextAsset" />. Following the
///     codebase convention, the config itself lives on a ScriptableObject (created via
///     <c>Scriptable Objects/SkillTreeSO</c>) that points at the CSV.
///
///     CSV columns (one node per row):
///     <c>id, displayName, description, upgradeText, cost, growth, maxLevel, stat, kind, amount, prereqs, x, y, icon, root</c>
///     <list type="bullet">
///         <item><c>description</c> = the unlock text; <c>upgradeText</c> = text shown once owned and still upgradeable (blank reuses <c>description</c>).</item>
///         <item><c>stat</c>/<c>kind</c>/<c>amount</c> blank → a connector/unlock-only node (no stat effect).</item>
///         <item>
///             <c>cost</c> is the level-1 cost; each subsequent level costs <c>round(prev × growth)</c>
///             (blank/≤1 growth → flat cost every level). <c>maxLevel</c> is how many times the node can be
///             purchased (blank/≤1 → single-level).
///         </item>
///         <item>
///             <c>icon</c> is optional (the column may be absent entirely, or blank per row): the name of a
///             sprite in this asset's <see cref="icons" /> list, shown on the node by <see cref="SkillNodeView" />.
///         </item>
///         <item>
///             <c>stat</c>/<c>kind</c>/<c>amount</c> may each hold ';'-separated lists of equal length to apply
///             several effects atomically (e.g. a node bumping both a chance and a bonus-% stat at once).
///             Within one stat's <c>amount</c>, a '|'-separated list gives per-level increments (length
///             <c>maxLevel</c>); a single value is the same increment every level.
///         </item>
///         <item>
///             <c>prereqs</c> is a semicolon-separated list of linked node ids. Links are symmetric and
///             stored on both ends — buying either end's first level reveals the other (see
///             <see cref="SkillNode.prereqs" />). It does <b>not</b> control rootness.
///         </item>
///         <item>
///             <c>root</c> (optional trailing column) marks a start-unlocked entry node (truthy = "1"/
///             "true"/"yes"/"root"; blank = normal). This is the only thing that makes a node a root — an
///             empty <c>prereqs</c> list does not. A tree may have several roots.
///         </item>
///         <item>Fields may be wrapped in double quotes to contain commas; "" is an escaped quote.</item>
///     </list>
/// </summary>
[CreateAssetMenu(fileName = "SkillTreeSO", menuName = "Scriptable Objects/SkillTreeSO")]
public class SkillTreeSO : ScriptableObject
{
    [Tooltip("CSV defining the skill tree. Edit in a spreadsheet; Unity reimports automatically.")]
    [SerializeField] private TextAsset csv;

    [Tooltip("Skip the first CSV row as a header.")]
    [SerializeField] private bool hasHeaderRow = true;

    [Tooltip("Loc key prefix for this tree's node text, e.g. 'skill.gold' -> keys like 'skill.gold.sword_dmg.name' in Strings.csv. Blank = tree text is never localized (CSV English only).")]
    [SerializeField] private string locKeyPrefix = "";

    /// <summary>Loc key prefix for this tree's nodes (see <see cref="SkillNode.locKey" />); read by the Localization Sync window.</summary>
    public string LocKeyPrefix => locKeyPrefix;

    [Tooltip("Sprites the CSV's 'icon' column can reference by sprite asset name.")]
    [SerializeField] private Sprite[] icons;

    [NonSerialized] private List<SkillNode> nodes;
    [NonSerialized] private Dictionary<string, SkillNode> byId;
    [NonSerialized] private Dictionary<string, Sprite> iconsByName;
    [NonSerialized] private Dictionary<string, List<string>> dependentsById;

    /// <summary>All nodes, parsed lazily from the CSV.</summary>
    public IReadOnlyList<SkillNode> Nodes
    {
        get
        {
            EnsureParsed();
            return nodes;
        }
    }

    /// <summary>The node with the given id, or null.</summary>
    public SkillNode GetById(string id)
    {
        EnsureParsed();
        return byId.TryGetValue(id, out SkillNode node) ? node : null;
    }

    /// <summary>The sprite for the given icon name from <see cref="icons" />, or null (blank/unknown name).</summary>
    public Sprite GetIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return null;
        }

        if (iconsByName == null)
        {
            iconsByName = new Dictionary<string, Sprite>();
            if (icons != null)
            {
                foreach (Sprite sprite in icons)
                {
                    if (sprite != null)
                    {
                        iconsByName[sprite.name] = sprite;
                    }
                }
            }
        }

        return iconsByName.TryGetValue(iconName, out Sprite found) ? found : null;
    }

    /// <summary>
    ///     Ids of nodes that list <paramref name="id" /> in their own <see cref="SkillNode.prereqs" /> — the
    ///     reverse direction of that link. A prereq link is symmetric (see <see cref="SkillNode.prereqs" />),
    ///     so services check both this and the node's own list when deciding whether it's revealed.
    /// </summary>
    public IReadOnlyList<string> GetDependents(string id)
    {
        EnsureParsed();
        return dependentsById.TryGetValue(id, out List<string> list) ? list : Array.Empty<string>();
    }

    /// <summary>Forces a re-parse (e.g. after editing the CSV at runtime). Normally not needed.</summary>
    public void Reload()
    {
        nodes = null;
        byId = null;
        iconsByName = null;
        dependentsById = null;
        EnsureParsed();
    }

    private void EnsureParsed()
    {
        if (nodes != null)
        {
            return;
        }

        nodes = new List<SkillNode>();
        byId = new Dictionary<string, SkillNode>();
        dependentsById = new Dictionary<string, List<string>>();

        if (csv == null)
        {
            Debug.LogError($"SkillTreeSO '{name}' has no CSV assigned.");
            return;
        }

        string[] lines = csv.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0 && hasHeaderRow)
            {
                continue;
            }

            string line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            SkillNode node = ParseRow(line, i + 1);
            if (node == null)
            {
                continue;
            }

            if (byId.ContainsKey(node.id))
            {
                Debug.LogError($"SkillTreeSO '{name}': duplicate node id '{node.id}' on line {i + 1}; ignoring the duplicate.");
                continue;
            }

            nodes.Add(node);
            byId[node.id] = node;
        }

        foreach (SkillNode node in nodes)
        {
            foreach (string prereq in node.prereqs)
            {
                if (!dependentsById.TryGetValue(prereq, out List<string> list))
                {
                    list = new List<string>();
                    dependentsById[prereq] = list;
                }
                list.Add(node.id);
            }
        }
    }

    private SkillNode ParseRow(string line, int lineNumber)
    {
        List<string> f = CsvUtil.SplitLine(line);
        if (f.Count < 13)
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has {f.Count} columns, expected at least 13. Skipping.");
            return null;
        }

        string id = f[0].Trim();
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has an empty id. Skipping.");
            return null;
        }

        int maxLevel = Mathf.Max(1, ParseInt(f[6], 1, lineNumber, "maxLevel"));

        var node = new SkillNode
        {
            id = id,
            locKey = string.IsNullOrEmpty(locKeyPrefix) ? "" : locKeyPrefix + "." + id,
            displayName = f[1].Trim(),
            description = f[2].Trim(),
            upgradeText = f[3].Trim(),
            maxLevel = maxLevel,
            costPerLevel = BuildCostLadder(
                ParseInt(f[4], 0, lineNumber, "cost"),
                ParseFloat(f[5], 1f, lineNumber, "growth"),
                maxLevel),
            x = ParseFloat(f[11], 0f, lineNumber, "x"),
            y = ParseFloat(f[12], 0f, lineNumber, "y"),
        };

        string statRaw = f[7].Trim();
        if (!string.IsNullOrEmpty(statRaw))
        {
            string[] statParts = statRaw.Split(';');
            string[] kindParts = f[8].Split(';');
            string[] amountParts = f[9].Split(';');

            if (kindParts.Length != statParts.Length || amountParts.Length != statParts.Length)
            {
                Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has mismatched stat/kind/amount effect counts. Treating node as effect-less.");
            }
            else
            {
                for (int e = 0; e < statParts.Length; e++)
                {
                    if (!Enum.TryParse(statParts[e].Trim(), true, out StatType effectStat))
                    {
                        Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has unknown stat '{statParts[e]}'. Skipping that effect.");
                        continue;
                    }

                    if (!Enum.TryParse(kindParts[e].Trim(), true, out ModifierKind effectKind))
                    {
                        Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has unknown modifier kind '{kindParts[e]}'. Defaulting to Flat.");
                        effectKind = ModifierKind.Flat;
                    }

                    float[] amounts = ParsePerLevelAmounts(amountParts[e], maxLevel, lineNumber, statParts[e].Trim());
                    node.effects.Add(new SkillEffect { stat = effectStat, kind = effectKind, amounts = amounts });
                }
            }
        }

        string prereqRaw = f[10].Trim();
        if (!string.IsNullOrEmpty(prereqRaw))
        {
            foreach (string p in prereqRaw.Split(';'))
            {
                string trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    node.prereqs.Add(trimmed);
                }
            }
        }

        // Optional trailing icon column — rows without it parse as icon-less.
        node.iconName = f.Count > 13 ? f[13].Trim() : "";
        if (!string.IsNullOrEmpty(node.iconName) && GetIcon(node.iconName) == null)
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} names icon '{node.iconName}', which is not in this asset's icons list.");
        }

        // Optional trailing 'root' column — a truthy value marks a start-unlocked entry node. Absent/blank
        // (older files) parse as non-root, which is why the CSVs carry an explicit root per entry branch.
        node.isRoot = f.Count > 14 && ParseBool(f[14]);

        return node;
    }

    /// <summary>Level-1 cost, then <c>round(prev × growth)</c> per level (growth ≤ 1 ⇒ flat cost).</summary>
    private static int[] BuildCostLadder(int baseCost, float growth, int maxLevel)
    {
        var ladder = new int[maxLevel];
        ladder[0] = baseCost;
        for (int i = 1; i < maxLevel; i++)
        {
            ladder[i] = growth > 1f ? Mathf.RoundToInt(ladder[i - 1] * growth) : baseCost;
        }
        return ladder;
    }

    /// <summary>
    ///     Parses one stat's amount cell into a per-level array. A single value = same increment every
    ///     level; a '|'-separated list gives one increment per level (must be length <paramref name="maxLevel" />).
    /// </summary>
    private float[] ParsePerLevelAmounts(string raw, int maxLevel, int lineNumber, string statName)
    {
        string[] parts = raw.Split('|');
        if (parts.Length == 1)
        {
            return new[] { ParseFloat(parts[0], 0f, lineNumber, "amount") };
        }

        if (parts.Length != maxLevel)
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} stat '{statName}' has {parts.Length} per-level amounts but maxLevel is {maxLevel}. Extra/missing levels reuse the nearest value.");
        }

        var amounts = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            amounts[i] = ParseFloat(parts[i], 0f, lineNumber, "amount");
        }
        return amounts;
    }

    /// <summary>A CSV cell counts as true when it is "1", "true", "yes", or "root" (case-insensitive).</summary>
    private static bool ParseBool(string s)
    {
        s = s.Trim();
        return s == "1"
            || s.Equals("true", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("root", StringComparison.OrdinalIgnoreCase);
    }

    private int ParseInt(string s, int fallback, int lineNumber, string field)
    {
        if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            return v;
        }
        Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has invalid {field} '{s}'. Using {fallback}.");
        return fallback;
    }

    private float ParseFloat(string s, float fallback, int lineNumber, string field)
    {
        if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            return v;
        }
        Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has invalid {field} '{s}'. Using {fallback}.");
        return fallback;
    }
}
