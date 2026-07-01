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
///     CSV columns (one node per row): <c>id, displayName, description, cost, stat, kind, amount, prereqs, x, y</c>
///     <list type="bullet">
///         <item><c>stat</c>/<c>kind</c>/<c>amount</c> blank → a connector/unlock-only node (no stat effect).</item>
///         <item>
///             <c>stat</c>/<c>kind</c>/<c>amount</c> may each hold ';'-separated lists of equal length to apply
///             several effects atomically (e.g. a node bumping both a chance and a bonus-% stat at once).
///         </item>
///         <item><c>prereqs</c> is semicolon-separated; blank → a root node visible from the start.</item>
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

    [NonSerialized] private List<SkillNode> nodes;
    [NonSerialized] private Dictionary<string, SkillNode> byId;

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

    /// <summary>Forces a re-parse (e.g. after editing the CSV at runtime). Normally not needed.</summary>
    public void Reload()
    {
        nodes = null;
        byId = null;
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
    }

    private SkillNode ParseRow(string line, int lineNumber)
    {
        List<string> f = SplitCsvLine(line);
        if (f.Count < 10)
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has {f.Count} columns, expected 10. Skipping.");
            return null;
        }

        string id = f[0].Trim();
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError($"SkillTreeSO '{name}': line {lineNumber} has an empty id. Skipping.");
            return null;
        }

        var node = new SkillNode
        {
            id = id,
            displayName = f[1].Trim(),
            description = f[2].Trim(),
            cost = ParseInt(f[3], 0, lineNumber, "cost"),
            x = ParseFloat(f[8], 0f, lineNumber, "x"),
            y = ParseFloat(f[9], 0f, lineNumber, "y"),
        };

        string statRaw = f[4].Trim();
        if (!string.IsNullOrEmpty(statRaw))
        {
            string[] statParts = statRaw.Split(';');
            string[] kindParts = f[5].Split(';');
            string[] amountParts = f[6].Split(';');

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

                    float effectAmount = ParseFloat(amountParts[e], 0f, lineNumber, "amount");
                    node.effects.Add(new SkillEffect { stat = effectStat, kind = effectKind, amount = effectAmount });
                }
            }
        }

        string prereqRaw = f[7].Trim();
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

        return node;
    }

    // Minimal RFC-4180-ish splitter: supports double-quoted fields with embedded commas and "" escapes.
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
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
