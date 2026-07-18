using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
///     <b>Bladehold &gt; Localization &gt; Sync Keys</b> — keeps <c>Strings.csv</c> in step with the
///     game's generated-key sources: every <see cref="SkillTreeSO" /> with a loc key prefix
///     (<c>skill.&lt;tree&gt;.&lt;id&gt;.name/.desc/.upgrade</c>), every <see cref="EnemyRosterSO" />
///     (<c>enemy.&lt;id&gt;.name</c>), every <see cref="ClassDefinitionSO" />
///     (<c>class.&lt;id&gt;.name/.desc</c>), and the <see cref="StatType" /> enum
///     (<c>stat.&lt;StatType&gt;</c>).
///
///     Missing keys are <b>appended</b> with the <c>en</c> cell auto-filled from the source (and
///     refreshed on every sync so translators always see current English); existing translations are
///     never touched, and orphaned keys (source id deleted/renamed) are only <b>reported</b>, never
///     deleted — at runtime the two-arg <see cref="Loc.Get(string, string)" /> fallback prefers the
///     live gameplay-CSV English anyway, so a stale row can't show outdated text.
/// </summary>
public class LocalizationSyncWindow : EditorWindow
{
    private const string StringsPath = "Assets/Bladehold/Resources/Localization/Strings.csv";

    private Vector2 scroll;
    private string report = "";

    [MenuItem("Bladehold/Localization/Sync Keys")]
    public static void Open()
    {
        GetWindow<LocalizationSyncWindow>("Localization Sync");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Scans skill trees, the enemy roster, class definitions, and StatType for generated loc keys, " +
            "appends missing rows to Strings.csv (en auto-filled), refreshes stale en cells, and reports orphans. " +
            "Translations are never modified or deleted.",
            MessageType.Info);

        if (GUILayout.Button("Sync Keys", GUILayout.Height(28)))
        {
            report = Sync();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static string Sync()
    {
        TextAsset stringsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(StringsPath);
        if (stringsAsset == null)
        {
            return $"ERROR: {StringsPath} not found.";
        }

        // Expected keys: key -> current English source text.
        Dictionary<string, string> expected = CollectExpectedKeys(out List<string> sourceNotes);

        // Parse the existing CSV, preserving raw lines so translations round-trip untouched.
        string[] lines = stringsAsset.text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        List<string> header = CsvUtil.SplitLine(lines[0]);
        int columnCount = header.Count;
        int enColumn = header.FindIndex(h => h.Trim().TrimStart('﻿').ToLowerInvariant() == "en");
        if (enColumn < 0)
        {
            return "ERROR: Strings.csv header has no 'en' column.";
        }

        var existingKeys = new Dictionary<string, int>(); // key -> line index
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }
            string key = CsvUtil.SplitLine(lines[i])[0].Trim();
            if (key.Length > 0 && !key.StartsWith("#") && !existingKeys.ContainsKey(key))
            {
                existingKeys[key] = i;
            }
        }

        var output = new List<string>(lines);
        int added = 0, refreshed = 0;
        var sb = new StringBuilder();

        foreach (KeyValuePair<string, string> pair in expected.OrderBy(p => p.Key))
        {
            if (existingKeys.TryGetValue(pair.Key, out int lineIndex))
            {
                List<string> cells = CsvUtil.SplitLine(output[lineIndex]);
                while (cells.Count < columnCount) cells.Add("");
                if (cells[enColumn] != pair.Value)
                {
                    cells[enColumn] = pair.Value;
                    output[lineIndex] = string.Join(",", cells.Select(EscapeCell));
                    refreshed++;
                    sb.AppendLine($"refreshed en: {pair.Key}");
                }
            }
            else
            {
                var cells = new string[columnCount];
                for (int c = 0; c < columnCount; c++) cells[c] = "";
                cells[0] = pair.Key;
                if (columnCount > 1) cells[1] = "(generated)";
                cells[enColumn] = pair.Value;
                output.Add(string.Join(",", cells.Select(EscapeCell)));
                added++;
                sb.AppendLine($"added: {pair.Key}");
            }
        }

        // Orphans: generated-namespace keys in the CSV that no source produces anymore.
        string[] generatedPrefixes = { "skill.gold.", "skill.berserker.", "skill.mage.", "skill.reinc.", "enemy.", "class.", "stat." };
        var orphans = existingKeys.Keys
            .Where(k => generatedPrefixes.Any(k.StartsWith) && !expected.ContainsKey(k) && k != "stat.suffix.seconds")
            .OrderBy(k => k)
            .ToList();

        System.IO.File.WriteAllText(
            System.IO.Path.GetFullPath(StringsPath),
            string.Join("\n", output) + "\n",
            new UTF8Encoding(true));
        AssetDatabase.ImportAsset(StringsPath);

        sb.Insert(0, $"Sync complete: {added} added, {refreshed} en cells refreshed, {orphans.Count} orphans.\n" +
                     string.Join("\n", sourceNotes) + "\n\n");
        if (orphans.Count > 0)
        {
            sb.AppendLine("\nOrphaned keys (source id gone — left in place, delete manually if intended):");
            foreach (string orphan in orphans) sb.AppendLine($"  {orphan}");
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> CollectExpectedKeys(out List<string> notes)
    {
        var expected = new Dictionary<string, string>();
        notes = new List<string>();

        foreach (SkillTreeSO tree in LoadAllAssets<SkillTreeSO>())
        {
            if (string.IsNullOrEmpty(tree.LocKeyPrefix))
            {
                notes.Add($"skipped tree '{tree.name}' (no locKeyPrefix)");
                continue;
            }
            int count = 0;
            foreach (SkillNode node in tree.Nodes)
            {
                string prefix = tree.LocKeyPrefix + "." + node.id;
                AddKey(expected, prefix + ".name", node.displayName);
                AddKey(expected, prefix + ".desc", node.description);
                if (!string.IsNullOrEmpty(node.upgradeText))
                {
                    AddKey(expected, prefix + ".upgrade", node.upgradeText);
                }
                count++;
            }
            notes.Add($"tree '{tree.name}' ({tree.LocKeyPrefix}): {count} nodes");
        }

        foreach (EnemyRosterSO roster in LoadAllAssets<EnemyRosterSO>())
        {
            int count = 0;
            foreach (EnemyDefinition def in roster.Enemies)
            {
                AddKey(expected, "enemy." + def.id + ".name", def.displayName);
                count++;
            }
            notes.Add($"roster '{roster.name}': {count} enemies");
        }

        foreach (ClassDefinitionSO cls in LoadAllAssets<ClassDefinitionSO>())
        {
            AddKey(expected, "class." + cls.id + ".name", cls.displayName);
            AddKey(expected, "class." + cls.id + ".desc", cls.description);
        }

        foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
        {
            AddKey(expected, "stat." + stat, StatDisplay.EnglishLabel(stat));
        }

        return expected;
    }

    private static void AddKey(Dictionary<string, string> expected, string key, string english)
    {
        if (!string.IsNullOrEmpty(english) && !expected.ContainsKey(key))
        {
            // Loc's parser reads literal \n as a line break; keep authored newlines round-trippable.
            expected[key] = english.Replace("\r\n", "\n").Replace("\n", "\\n");
        }
    }

    private static IEnumerable<T> LoadAllAssets<T>() where T : Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null);
    }

    private static string EscapeCell(string cell)
    {
        if (cell.Contains(",") || cell.Contains("\""))
        {
            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }
        return cell;
    }
}
