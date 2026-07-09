using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
///     One editable skill-tree CSV row (mirrors the columns of
///     id,displayName,description,upgradeText,cost,growth,maxLevel,stat,kind,amount,prereqs,x,y,icon).
///     Serializable so an editing session (e.g. <see cref="SkillTreeEditSession" />) can persist a List of
///     these through domain reloads and register them with the Undo system.
/// </summary>
[Serializable]
public class SkillTreeRow
{
    public string id = "";
    public string displayName = "";
    public string description = "";
    public string upgradeText = "";
    public int cost;
    public float growth = 1f;
    public int maxLevel = 1;
    public string stat = "";
    public string kind = "";
    public string amount = "";
    public string prereqs = "";
    public float x;
    public float y;
    public string icon = "";

    /// <summary>Whether this node is unlocked from the start (the CSV's 'root' column).</summary>
    public bool isRoot;

    /// <summary>The prereqs column split into trimmed, non-empty ids.</summary>
    public List<string> PrereqList()
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(prereqs))
        {
            return result;
        }
        foreach (string part in prereqs.Split(';'))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }
        return result;
    }

    public void SetPrereqList(IEnumerable<string> ids)
    {
        prereqs = string.Join(";", ids);
    }

    public SkillTreeRow Clone()
    {
        return (SkillTreeRow)MemberwiseClone();
    }
}

/// <summary>
///     Shared CSV read/write for the skill-tree editors (the list-and-detail
///     <see cref="SkillTreeCsvEditorWindow" /> and the Scene-view editor). Reads a
///     <see cref="SkillTreeSO" />'s private csv/hasHeaderRow via SerializedObject, and writes all 15
///     columns back with the same quoting the runtime parser (CsvUtil.SplitLine) understands. All float
///     parsing/writing is InvariantCulture to match SkillTreeSO's parser — a locale with ',' decimals
///     must never corrupt the file.
/// </summary>
public static class SkillTreeCsvIO
{
    public const string Header = "id,displayName,description,upgradeText,cost,growth,maxLevel,stat,kind,amount,prereqs,x,y,icon,root";

    /// <summary>
    ///     Parses the tree's CSV into editable rows. Returns an empty list (and null csvAsset) when the
    ///     tree is null or has no CSV assigned.
    /// </summary>
    public static List<SkillTreeRow> Load(SkillTreeSO tree, out TextAsset csvAsset)
    {
        var rows = new List<SkillTreeRow>();
        csvAsset = null;

        if (tree == null)
        {
            return rows;
        }

        var so = new SerializedObject(tree);
        csvAsset = so.FindProperty("csv").objectReferenceValue as TextAsset;
        bool hasHeader = so.FindProperty("hasHeaderRow").boolValue;
        if (csvAsset == null)
        {
            return rows;
        }

        string[] lines = csvAsset.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0 && hasHeader)
            {
                continue;
            }

            string line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> f = CsvUtil.SplitLine(line);
            if (f.Count < 13)
            {
                Debug.LogWarning($"Skill Tree Editor: skipping line {i + 1} of '{csvAsset.name}' ({f.Count} columns, expected at least 13).");
                continue;
            }

            var row = new SkillTreeRow
            {
                id = f[0].Trim(),
                displayName = f[1].Trim(),
                description = f[2].Trim(),
                upgradeText = f[3].Trim(),
                stat = f[7].Trim(),
                kind = f[8].Trim(),
                amount = f[9].Trim(),
                prereqs = f[10].Trim(),
                icon = f.Count > 13 ? f[13].Trim() : "",
                isRoot = f.Count > 14 && IsTruthy(f[14]),
            };
            int.TryParse(f[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out row.cost);
            if (!float.TryParse(f[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out row.growth)) row.growth = 1f;
            if (!int.TryParse(f[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out row.maxLevel)) row.maxLevel = 1;
            float.TryParse(f[11].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out row.x);
            float.TryParse(f[12].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out row.y);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    ///     Writes the rows back to the CSV file the asset lives at (always all 14 columns), reimports it,
    ///     and reloads the tree so parse errors surface immediately. Returns false when the asset path
    ///     can't be resolved.
    /// </summary>
    public static bool Save(SkillTreeSO tree, TextAsset csvAsset, IReadOnlyList<SkillTreeRow> rows)
    {
        string path = AssetDatabase.GetAssetPath(csvAsset);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Skill Tree Editor: could not resolve the CSV asset's path.");
            return false;
        }

        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (SkillTreeRow row in rows)
        {
            sb.Append(Escape(row.id)).Append(',');
            sb.Append(Escape(row.displayName)).Append(',');
            sb.Append(Escape(row.description)).Append(',');
            sb.Append(Escape(row.upgradeText)).Append(',');
            sb.Append(row.cost.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.growth.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.maxLevel.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(row.stat)).Append(',');
            sb.Append(Escape(row.kind)).Append(',');
            sb.Append(Escape(row.amount)).Append(',');
            sb.Append(Escape(row.prereqs)).Append(',');
            sb.Append(row.x.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.y.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(row.icon)).Append(',');
            sb.Append(row.isRoot ? "1" : "").Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
        AssetDatabase.ImportAsset(path);
        tree.Reload();
        return true;
    }

    /// <summary>A CSV cell counts as true when it is "1", "true", "yes", or "root" (case-insensitive).</summary>
    private static bool IsTruthy(string s)
    {
        s = s.Trim();
        return s == "1"
            || s.Equals("true", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("root", StringComparison.OrdinalIgnoreCase);
    }

    public static string Escape(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }
        if (field.IndexOf(',') < 0 && field.IndexOf('"') < 0 && field.IndexOf('\n') < 0)
        {
            return field;
        }
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Adds the sprite to the tree asset's icons list (if not already there) and refreshes the tree's icon cache.</summary>
    public static void EnsureIconInTree(SkillTreeSO tree, Sprite sprite)
    {
        var so = new SerializedObject(tree);
        SerializedProperty icons = so.FindProperty("icons");

        for (int i = 0; i < icons.arraySize; i++)
        {
            if (icons.GetArrayElementAtIndex(i).objectReferenceValue == sprite)
            {
                return;
            }
        }

        icons.InsertArrayElementAtIndex(icons.arraySize);
        icons.GetArrayElementAtIndex(icons.arraySize - 1).objectReferenceValue = sprite;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tree);
        tree.Reload();
    }

    /// <summary>First id not already used by any row: baseId, then baseId_2, baseId_3, …</summary>
    public static string UniqueId(IReadOnlyList<SkillTreeRow> rows, string baseId)
    {
        string candidate = baseId;
        int suffix = 1;
        while (CountId(rows, candidate) > 0)
        {
            candidate = $"{baseId}_{++suffix}";
        }
        return candidate;
    }

    public static int CountId(IReadOnlyList<SkillTreeRow> rows, string id)
    {
        int count = 0;
        foreach (SkillTreeRow row in rows)
        {
            if (row.id == id)
            {
                count++;
            }
        }
        return count;
    }
}
