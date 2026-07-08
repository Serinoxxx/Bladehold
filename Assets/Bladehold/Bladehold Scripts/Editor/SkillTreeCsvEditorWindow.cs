using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Editor window for authoring the CSV-driven skill trees without leaving Unity (Bladehold > Skill
///     Tree Editor). Pick a <see cref="SkillTreeSO" />, edit its nodes in a list + detail layout, and
///     drag a Sprite onto a node's Icon field — the sprite is added to the tree asset's icons list
///     automatically and the node's icon column is set to the sprite's name. Save writes the CSV back to
///     the same file the SO points at (id,displayName,description,cost,stat,kind,amount,prereqs,x,y,icon,family)
///     and reloads the tree, so parse errors surface in the console immediately.
///     CSV parsing/serialization is shared with the Scene-view editor via <see cref="SkillTreeCsvIO" />.
/// </summary>
public class SkillTreeCsvEditorWindow : EditorWindow
{
    private SkillTreeSO tree;
    private TextAsset csvAsset;
    private List<SkillTreeRow> rows = new List<SkillTreeRow>();
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
            var row = new SkillTreeRow { id = SkillTreeCsvIO.UniqueId(rows, "new_node") };
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
                SkillTreeRow copy = rows[selected].Clone();
                copy.id = SkillTreeCsvIO.UniqueId(rows, copy.id);
                copy.y += 1f;
                rows.Insert(selected + 1, copy);
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
                if (SkillTreeCsvIO.Save(tree, csvAsset, rows))
                {
                    dirty = false;
                }
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

        SkillTreeRow row = rows[selected];

        EditorGUI.BeginChangeCheck();

        row.id = EditorGUILayout.TextField("Id", row.id);
        if (SkillTreeCsvIO.CountId(rows, row.id) > 1)
        {
            EditorGUILayout.HelpBox("Duplicate id — the tree will ignore the second occurrence.", MessageType.Error);
        }

        row.displayName = EditorGUILayout.TextField("Display Name", row.displayName);
        EditorGUILayout.LabelField("Description");
        row.description = EditorGUILayout.TextArea(row.description, GUILayout.MinHeight(40f));
        row.cost = EditorGUILayout.IntField("Cost", row.cost);
        row.family = EditorGUILayout.TextField(new GUIContent("Family", "Optional price pool: family nodes buy in any order but share one escalating price ladder (the Nth family purchase costs the Nth-cheapest cost in the family). Blank = priced individually."), row.family);

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
                SkillTreeCsvIO.EnsureIconInTree(tree, picked);
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
                    SkillTreeCsvIO.EnsureIconInTree(tree, sprite);
                }
                else if (dragged is Texture2D texture)
                {
                    // A texture dragged from the Project window: add its (first) sprite representation.
                    string path = AssetDatabase.GetAssetPath(texture);
                    foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    {
                        if (sub is Sprite texSprite)
                        {
                            SkillTreeCsvIO.EnsureIconInTree(tree, texSprite);
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
        selected = -1;
        dirty = false;
        rows = SkillTreeCsvIO.Load(tree, out csvAsset);
        if (rows.Count > 0)
        {
            selected = 0;
        }
    }
}
