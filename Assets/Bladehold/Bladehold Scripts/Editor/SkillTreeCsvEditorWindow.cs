using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Editor window for authoring the CSV-driven skill trees without leaving Unity (Bladehold > Skill
///     Tree Editor). Pick a <see cref="SkillTreeSO" />, edit its nodes in a list + detail layout, and
///     drag a Sprite onto a node's Icon field — the sprite is added to the tree asset's icons list
///     automatically and the node's icon column is set to the sprite's name. Save writes the CSV back to
///     the same file the SO points at (id,displayName,description,cost,stat,kind,amount,prereqs,x,y,icon)
///     and reloads the tree, so parse errors surface in the console immediately.
/// </summary>
public class SkillTreeCsvEditorWindow : EditorWindow
{
    private class Row
    {
        public string id = "";
        public string displayName = "";
        public string description = "";
        public int cost;
        public string stat = "";
        public string kind = "";
        public string amount = "";
        public string prereqs = "";
        public float x;
        public float y;
        public string icon = "";
    }

    private const string Header = "id,displayName,description,cost,stat,kind,amount,prereqs,x,y,icon";

    private SkillTreeSO tree;
    private TextAsset csvAsset;
    private readonly List<Row> rows = new List<Row>();
    private int selected = -1;
    private Vector2 listScroll;
    private Vector2 detailScroll;
    private bool dirty;

    [MenuItem("Bladehold/Skill Tree Editor")]
    private static void Open()
    {
        GetWindow<SkillTreeCsvEditorWindow>("Skill Tree Editor");
    }

    private void OnGUI()
    {
        DrawTreePicker();

        if (tree == null || csvAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a SkillTreeSO asset (with a CSV assigned) to edit its nodes.", MessageType.Info);
            return;
        }

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawNodeList();
        DrawDetail();
        EditorGUILayout.EndHorizontal();

        DrawIconDropArea();
    }

    private void DrawTreePicker()
    {
        EditorGUI.BeginChangeCheck();
        var newTree = (SkillTreeSO)EditorGUILayout.ObjectField("Skill Tree", tree, typeof(SkillTreeSO), false);
        if (EditorGUI.EndChangeCheck() && newTree != tree)
        {
            if (dirty && tree != null &&
                !EditorUtility.DisplayDialog("Unsaved changes", $"'{tree.name}' has unsaved CSV changes. Discard them?", "Discard", "Cancel"))
            {
                return;
            }
            tree = newTree;
            Load();
        }

        if (tree != null && csvAsset == null)
        {
            EditorGUILayout.HelpBox($"'{tree.name}' has no CSV TextAsset assigned.", MessageType.Warning);
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Add Node", EditorStyles.toolbarButton))
        {
            var row = new Row { id = UniqueId("new_node") };
            if (selected >= 0 && selected < rows.Count)
            {
                row.x = rows[selected].x;
                row.y = rows[selected].y + 1f;
                row.prereqs = rows[selected].id;
            }
            rows.Add(row);
            selected = rows.Count - 1;
            dirty = true;
        }

        using (new EditorGUI.DisabledScope(selected < 0 || selected >= rows.Count))
        {
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton))
            {
                Row src = rows[selected];
                rows.Insert(selected + 1, new Row
                {
                    id = UniqueId(src.id),
                    displayName = src.displayName,
                    description = src.description,
                    cost = src.cost,
                    stat = src.stat,
                    kind = src.kind,
                    amount = src.amount,
                    prereqs = src.prereqs,
                    x = src.x,
                    y = src.y + 1f,
                    icon = src.icon,
                });
                selected++;
                dirty = true;
            }

            if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
            {
                rows.RemoveAt(selected);
                selected = Mathf.Min(selected, rows.Count - 1);
                dirty = true;
            }
        }

        GUILayout.FlexibleSpace();

        if (dirty)
        {
            GUILayout.Label("unsaved changes", EditorStyles.miniLabel);
        }

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
        {
            if (!dirty || EditorUtility.DisplayDialog("Unsaved changes", "Discard unsaved CSV changes and reload from disk?", "Discard", "Cancel"))
            {
                Load();
            }
        }

        using (new EditorGUI.DisabledScope(!dirty))
        {
            if (GUILayout.Button("Save CSV", EditorStyles.toolbarButton))
            {
                Save();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawNodeList()
    {
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(200f));
        for (int i = 0; i < rows.Count; i++)
        {
            bool isSelected = i == selected;
            bool nowSelected = GUILayout.Toggle(isSelected, $"{rows[i].id}", isSelected ? "Button" : EditorStyles.miniButton);
            if (nowSelected && !isSelected)
            {
                selected = i;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawDetail()
    {
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        if (selected < 0 || selected >= rows.Count)
        {
            EditorGUILayout.HelpBox("Select a node on the left, or Add Node.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        Row row = rows[selected];

        EditorGUI.BeginChangeCheck();

        row.id = EditorGUILayout.TextField("Id", row.id);
        if (CountId(row.id) > 1)
        {
            EditorGUILayout.HelpBox("Duplicate id — the tree will ignore the second occurrence.", MessageType.Error);
        }

        row.displayName = EditorGUILayout.TextField("Display Name", row.displayName);
        EditorGUILayout.LabelField("Description");
        row.description = EditorGUILayout.TextArea(row.description, GUILayout.MinHeight(40f));
        row.cost = EditorGUILayout.IntField("Cost", row.cost);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effects (';'-separated lists of equal length; blank = connector node)", EditorStyles.miniBoldLabel);
        row.stat = EditorGUILayout.TextField(new GUIContent("Stat", "StatType name(s), e.g. SwordDamage or GoldenGoblinChance;GoldenGoblinGoldBonusPercent"), row.stat);
        row.kind = EditorGUILayout.TextField(new GUIContent("Kind", "Flat or Percent, one per stat"), row.kind);
        row.amount = EditorGUILayout.TextField(new GUIContent("Amount", "one number per stat"), row.amount);

        EditorGUILayout.Space();
        row.prereqs = EditorGUILayout.TextField(new GUIContent("Prereqs", "';'-separated node ids; blank = root node"), row.prereqs);
        row.x = EditorGUILayout.FloatField("X", row.x);
        row.y = EditorGUILayout.FloatField("Y", row.y);

        EditorGUILayout.Space();
        Sprite current = tree.GetIcon(row.icon);
        var picked = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Icon", "Drag a Sprite here; it is added to the tree's icons list and referenced by name."), current, typeof(Sprite), false);
        if (picked != current)
        {
            row.icon = picked != null ? picked.name : "";
            if (picked != null)
            {
                EnsureIconInTree(picked);
            }
        }
        row.icon = EditorGUILayout.TextField("Icon Name", row.icon);
        if (!string.IsNullOrEmpty(row.icon) && tree.GetIcon(row.icon) == null)
        {
            EditorGUILayout.HelpBox($"No sprite named '{row.icon}' in this tree's icons list.", MessageType.Warning);
        }

        if (EditorGUI.EndChangeCheck())
        {
            dirty = true;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawIconDropArea()
    {
        Rect drop = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drop sprites here to add them to this tree's icons list (without assigning to a node)", EditorStyles.helpBox);

        Event evt = Event.current;
        if (!drop.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                if (dragged is Sprite sprite)
                {
                    EnsureIconInTree(sprite);
                }
                else if (dragged is Texture2D texture)
                {
                    // A texture dragged from the Project window: add its (first) sprite representation.
                    string path = AssetDatabase.GetAssetPath(texture);
                    foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    {
                        if (sub is Sprite texSprite)
                        {
                            EnsureIconInTree(texSprite);
                            break;
                        }
                    }
                }
            }
            evt.Use();
        }
    }

    private void Load()
    {
        rows.Clear();
        selected = -1;
        dirty = false;
        csvAsset = null;

        if (tree == null)
        {
            return;
        }

        var so = new SerializedObject(tree);
        csvAsset = so.FindProperty("csv").objectReferenceValue as TextAsset;
        bool hasHeader = so.FindProperty("hasHeaderRow").boolValue;
        if (csvAsset == null)
        {
            return;
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
            if (f.Count < 10)
            {
                Debug.LogWarning($"Skill Tree Editor: skipping line {i + 1} of '{csvAsset.name}' ({f.Count} columns, expected at least 10).");
                continue;
            }

            var row = new Row
            {
                id = f[0].Trim(),
                displayName = f[1].Trim(),
                description = f[2].Trim(),
                stat = f[4].Trim(),
                kind = f[5].Trim(),
                amount = f[6].Trim(),
                prereqs = f[7].Trim(),
                icon = f.Count > 10 ? f[10].Trim() : "",
            };
            int.TryParse(f[3].Trim(), out row.cost);
            float.TryParse(f[8].Trim(), out row.x);
            float.TryParse(f[9].Trim(), out row.y);
            rows.Add(row);
        }

        if (rows.Count > 0)
        {
            selected = 0;
        }
    }

    private void Save()
    {
        string path = AssetDatabase.GetAssetPath(csvAsset);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Skill Tree Editor: could not resolve the CSV asset's path.");
            return;
        }

        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (Row row in rows)
        {
            sb.Append(Escape(row.id)).Append(',');
            sb.Append(Escape(row.displayName)).Append(',');
            sb.Append(Escape(row.description)).Append(',');
            sb.Append(row.cost).Append(',');
            sb.Append(Escape(row.stat)).Append(',');
            sb.Append(Escape(row.kind)).Append(',');
            sb.Append(Escape(row.amount)).Append(',');
            sb.Append(Escape(row.prereqs)).Append(',');
            sb.Append(row.x).Append(',');
            sb.Append(row.y).Append(',');
            sb.Append(Escape(row.icon)).Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
        AssetDatabase.ImportAsset(path);
        tree.Reload();
        dirty = false;
    }

    private static string Escape(string field)
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
    private void EnsureIconInTree(Sprite sprite)
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

    private string UniqueId(string baseId)
    {
        string candidate = baseId;
        int suffix = 1;
        while (CountId(candidate) > 0)
        {
            candidate = $"{baseId}_{++suffix}";
        }
        return candidate;
    }

    private int CountId(string id)
    {
        int count = 0;
        foreach (Row row in rows)
        {
            if (row.id == id)
            {
                count++;
            }
        }
        return count;
    }
}
