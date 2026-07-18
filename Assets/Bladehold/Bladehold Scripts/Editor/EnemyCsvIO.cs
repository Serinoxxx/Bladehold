using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
///     One editable enemy-roster CSV row for the Enemy Manager (see <see cref="EnemyManagerWindow" />).
///     Cells are stored as the <b>raw text</b> read from the file — including the hand-aligned space
///     padding in the id/displayName columns and the blank-vs-0 distinction of the optional override
///     columns — so untouched cells round-trip byte-for-byte through a save. Serializable so
///     <see cref="EnemyManagerSession" /> can persist a List of these across domain reloads.
/// </summary>
[Serializable]
public class EnemyRow
{
    public const int ColumnCount = 13;

    public const int ColId = 0;
    public const int ColDisplayName = 1;
    public const int ColHealth = 2;
    public const int ColDamage = 3;
    public const int ColMinGold = 4;
    public const int ColMaxGold = 5;
    public const int ColSpeed = 6;
    public const int ColScale = 7;
    public const int ColUnlockWave = 8;
    public const int ColSpawnChance = 9;
    public const int ColMinSpawn = 10;
    public const int ColMaxConcurrent = 11;
    public const int ColImpulseResistance = 12;

    public string[] cells = new string[ColumnCount];

    public EnemyRow()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = "";
        }
    }

    public string Id => Get(ColId);
    public string DisplayName => Get(ColDisplayName);

    /// <summary>The trimmed value of a column (what the runtime parser sees).</summary>
    public string Get(int column)
    {
        return cells[column].Trim();
    }

    /// <summary>
    ///     Sets a column's value. Only rewrites the cell when the trimmed value actually changed, so
    ///     untouched cells keep their original raw text (padding included).
    /// </summary>
    public void Set(int column, string value)
    {
        value = value ?? "";
        if (Get(column) == value.Trim())
        {
            return;
        }
        cells[column] = value.Trim();
    }

    public EnemyRow Clone()
    {
        var copy = new EnemyRow();
        Array.Copy(cells, copy.cells, ColumnCount);
        return copy;
    }

    /// <summary>
    ///     Parses this row into a runtime <see cref="EnemyDefinition" /> with the same rules as
    ///     <see cref="EnemyRosterSO" />'s parser (InvariantCulture, blank = null override, spawnChance
    ///     authored as a percent, gold-column mirroring). Used by the live-apply path so what the zoo
    ///     shows is exactly what a save + reload would produce.
    /// </summary>
    public EnemyDefinition ToDefinition()
    {
        var def = new EnemyDefinition
        {
            id = Get(ColId),
            displayName = Get(ColDisplayName),
            health = OptionalFloat(Get(ColHealth)),
            damage = OptionalFloat(Get(ColDamage)),
            minGold = OptionalInt(Get(ColMinGold)),
            maxGold = OptionalInt(Get(ColMaxGold)),
            speed = OptionalFloat(Get(ColSpeed)),
            scale = OptionalFloat(Get(ColScale)) ?? 1f,
            unlockWave = OptionalInt(Get(ColUnlockWave)) ?? 1,
            spawnChance = OptionalFloat(Get(ColSpawnChance)) ?? 0f,
            minSpawn = OptionalInt(Get(ColMinSpawn)) ?? 0,
            maxConcurrent = OptionalInt(Get(ColMaxConcurrent)) ?? 0,
            impulseResistance = OptionalFloat(Get(ColImpulseResistance)),
        };

        if (def.minGold.HasValue != def.maxGold.HasValue)
        {
            def.minGold = def.maxGold = def.minGold ?? def.maxGold;
        }
        if (def.minGold.HasValue && def.maxGold.Value < def.minGold.Value)
        {
            def.maxGold = def.minGold;
        }

        def.spawnChance = Mathf.Clamp01(def.spawnChance / 100f);
        def.unlockWave = Mathf.Max(1, def.unlockWave);
        def.minSpawn = Mathf.Max(0, def.minSpawn);
        def.maxConcurrent = Mathf.Max(0, def.maxConcurrent);
        if (def.scale <= 0f)
        {
            def.scale = 1f;
        }

        return def;
    }

    private static float? OptionalFloat(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : (float?)null;
    }

    private static int? OptionalInt(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : (int?)null;
    }
}

/// <summary>
///     CSV read/write for the Enemy Manager, mirroring <see cref="SkillTreeCsvIO" />: reads the
///     roster's private csv/hasHeaderRow via SerializedObject, and saves by rewriting the file,
///     reimporting it, and calling <see cref="EnemyRosterSO.Reload" /> so parse errors surface in the
///     console immediately. The original header line is preserved verbatim (it is hand-aligned).
/// </summary>
public static class EnemyCsvIO
{
    public const string DefaultHeader = "id,displayName,health,damage,minGold,maxGold,speed,scale,unlockWave,spawnChance,minSpawn,maxConcurrent,impulseResistance";

    /// <summary>
    ///     Parses the roster's CSV into editable raw rows. Returns an empty list (and a null csvAsset)
    ///     when the roster is null or has no CSV assigned. <paramref name="headerLine" /> is the file's
    ///     original header row (or the canonical header when the file declares none).
    /// </summary>
    public static List<EnemyRow> Load(EnemyRosterSO roster, out TextAsset csvAsset, out string headerLine)
    {
        var rows = new List<EnemyRow>();
        csvAsset = null;
        headerLine = DefaultHeader;

        if (roster == null)
        {
            return rows;
        }

        var so = new SerializedObject(roster);
        csvAsset = so.FindProperty("csv").objectReferenceValue as TextAsset;
        bool hasHeader = so.FindProperty("hasHeaderRow").boolValue;
        if (csvAsset == null)
        {
            return rows;
        }

        string[] lines = csvAsset.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (i == 0 && hasHeader)
            {
                headerLine = line;
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> f = CsvUtil.SplitLine(line);
            if (f.Count < EnemyRow.ColumnCount)
            {
                Debug.LogWarning($"Enemy Manager: skipping line {i + 1} of '{csvAsset.name}' ({f.Count} columns, expected {EnemyRow.ColumnCount}).");
                continue;
            }

            var row = new EnemyRow();
            for (int c = 0; c < EnemyRow.ColumnCount; c++)
            {
                row.cells[c] = f[c];
            }
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    ///     Writes the rows back to the CSV file, reimports it, and reloads the roster. Untouched cells
    ///     are written exactly as loaded, and the file's existing newline style (CRLF vs LF) is kept,
    ///     so a save with no edits is byte-identical. Returns false when the asset path can't be
    ///     resolved.
    /// </summary>
    public static bool Save(EnemyRosterSO roster, TextAsset csvAsset, string headerLine, IReadOnlyList<EnemyRow> rows)
    {
        string path = AssetDatabase.GetAssetPath(csvAsset);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Enemy Manager: could not resolve the CSV asset's path.");
            return false;
        }

        string newline = csvAsset.text.Contains("\r\n") ? "\r\n" : "\n";
        var sb = new StringBuilder();
        sb.Append(headerLine).Append(newline);
        foreach (EnemyRow row in rows)
        {
            for (int c = 0; c < EnemyRow.ColumnCount; c++)
            {
                if (c > 0)
                {
                    sb.Append(',');
                }
                sb.Append(SkillTreeCsvIO.Escape(row.cells[c]));
            }
            sb.Append(newline);
        }

        File.WriteAllText(path, sb.ToString());
        AssetDatabase.ImportAsset(path);
        roster.Reload();
        return true;
    }
}
