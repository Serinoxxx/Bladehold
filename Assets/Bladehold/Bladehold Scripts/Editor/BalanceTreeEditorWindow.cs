using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Edge = UnityEditor.Experimental.GraphView.Edge;

[Serializable]
public class TodoEntry
{
    public string id;
    public string type;
    public string name;
    public string dependencies;
    public string description;
    public bool isTodo;
}

[Serializable]
public class TodoData
{
    public List<TodoEntry> entries = new List<TodoEntry>();
}

public class BalanceTreeEditorWindow : EditorWindow
{
    private BalanceGraphView graphView;
    private IMGUIContainer inspectorContainer;
    private Vector2 inspectorScrollPosition;

    private object selectedData;
    private Editor currentSOEditor;

    private List<DraftUpgradeCsvEntry> draftEntries = new List<DraftUpgradeCsvEntry>();
    private string draftCsvPath = "Assets/Bladehold/Resources/DraftUpgrades.csv";

    private TodoData todoData = new TodoData();
    private string todoJsonPath = "UPGRADE_TODOS.json";
    private string todoMdPath = "UPGRADE_TODOS.md";

    [MenuItem("Bladehold/Balance Tree Editor _F1", false, 0)]
    public static void ShowWindow()
    {
        var window = GetWindow<BalanceTreeEditorWindow>("Balance Tree");
        window.Show();
    }

    private void OnEnable()
    {
        LoadTodos();
        ConstructWindow();
    }

    private void OnDisable()
    {
        if (currentSOEditor != null) DestroyImmediate(currentSOEditor);
    }

    private void ConstructWindow()
    {
        rootVisualElement.Clear();

        var toolbar = new UnityEditor.UIElements.Toolbar();
        toolbar.Add(new UnityEditor.UIElements.ToolbarButton(RefreshGraph) { text = "Refresh" });
        toolbar.Add(new UnityEditor.UIElements.ToolbarButton(SaveAll) { text = "Save All" });
        
        var addMenu = new UnityEditor.UIElements.ToolbarMenu { text = "Add Node" };
        addMenu.menu.AppendAction("Weapon", a => AddTodoNode("Weapon"));
        addMenu.menu.AppendAction("Armour Set", a => AddTodoNode("Armour Set"));
        addMenu.menu.AppendAction("Mount", a => AddTodoNode("Mount"));
        addMenu.menu.AppendAction("Tower", a => AddTodoNode("Tower"));
        addMenu.menu.AppendAction("Draft Upgrade", a => AddTodoNode("Draft Upgrade"));
        addMenu.menu.AppendAction("Meta Perk", a => AddTodoNode("Meta Perk"));
        toolbar.Add(addMenu);
        
        toolbar.Add(new UnityEditor.UIElements.ToolbarSpacer() { flex = true });
        toolbar.Add(new Label("Select a node to inspect and edit."));

        var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

        graphView = new BalanceGraphView { style = { flexGrow = 3 } };
        graphView.OnNodeSelected = (data) =>
        {
            selectedData = data;
            if (currentSOEditor != null)
            {
                DestroyImmediate(currentSOEditor);
                currentSOEditor = null;
            }
            if (data is ScriptableObject so)
            {
                currentSOEditor = Editor.CreateEditor(so);
            }
            else if (data is GameObject go)
            {
                currentSOEditor = Editor.CreateEditor(go);
            }
            inspectorContainer?.MarkDirtyRepaint();
        };

        var inspectorWrapper = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                minWidth = 300,
                borderLeftColor = Color.gray,
                borderLeftWidth = 1
            }
        };

        inspectorContainer = new IMGUIContainer(DrawInspector);
        inspectorContainer.StretchToParentSize();
        inspectorWrapper.Add(inspectorContainer);

        container.Add(graphView);
        container.Add(inspectorWrapper);

        rootVisualElement.Add(toolbar);
        rootVisualElement.Add(container);

        RefreshGraph();
    }

    private void AddTodoNode(string type)
    {
        string newId = $"new_{type.Replace(" ", "").ToLower()}_{Guid.NewGuid().ToString().Substring(0, 4)}";
        var entry = new TodoEntry
        {
            id = newId,
            type = type,
            name = $"New {type}",
            isTodo = true,
            description = "Description here..."
        };
        todoData.entries.Add(entry);
        SaveTodos();
        RefreshGraph();
    }

    private void RefreshGraph()
    {
        graphView.ClearGraph();
        LoadDraftCSV();

        var weapons = FindAssets<WeaponDefinitionSO>();
        var armours = FindAssets<ArmourSetSO>();
        var mounts = FindAssets<MountDefinitionSO>();
        var metaPerks = FindAssets<MetaPerkDefinitionSO>();
        var towers = LoadTowers();

        float xOffset = 0;
        
        // Weapons
        float yOffset = 0;
        var weaponNodes = new Dictionary<string, Node>();
        foreach (var w in weapons)
        {
            var node = CreateGraphNode(w.displayName, w, new Vector2(xOffset, yOffset), GetId(w));
            weaponNodes[w.id] = node;
            yOffset += 100;
        }

        // Drafts
        xOffset += 300;
        yOffset = 0;
        var draftNodes = new Dictionary<string, Node>();
        foreach (var draft in draftEntries)
        {
            var node = CreateGraphNode(draft.displayName, draft, new Vector2(xOffset, yOffset), draft.id);
            draftNodes[draft.id] = node;

            if (!string.IsNullOrEmpty(draft.weapon) && weaponNodes.TryGetValue(draft.weapon, out var wNode))
            {
                graphView.ConnectNodes(wNode, node);
            }
            yOffset += 100;
        }

        // Armours & Mounts & Meta Perks (Scatter them)
        xOffset += 300;
        yOffset = 0;
        foreach (var a in armours)
        {
            CreateGraphNode(a.displayName, a, new Vector2(xOffset, yOffset), GetId(a));
            yOffset += 100;
        }
        foreach (var m in mounts)
        {
            CreateGraphNode(m.displayName, m, new Vector2(xOffset, yOffset), GetId(m));
            yOffset += 100;
        }

        xOffset += 300;
        yOffset = 0;
        var perkNodes = new Dictionary<string, Node>();
        foreach (var p in metaPerks)
        {
            var node = CreateGraphNode(p.displayName, p, new Vector2(xOffset, yOffset), GetId(p));
            perkNodes[p.id] = node;
            yOffset += 100;
        }

        // Connect Perks
        foreach (var p in metaPerks)
        {
            if (p.prerequisites != null && perkNodes.TryGetValue(p.id, out var childNode))
            {
                foreach (var prereq in p.prerequisites)
                {
                    if (prereq != null && perkNodes.TryGetValue(prereq.id, out var parentNode))
                    {
                        graphView.ConnectNodes(parentNode, childNode);
                    }
                }
            }
        }

        // Towers
        xOffset += 300;
        yOffset = 0;
        foreach (var t in towers)
        {
            CreateGraphNode(t.name, t, new Vector2(xOffset, yOffset), t.name);
            yOffset += 100;
        }

        // Planned Todo Nodes (Not yet matched to an asset)
        xOffset += 300;
        yOffset = 0;
        foreach (var todo in todoData.entries)
        {
            if (todo.isTodo && !AssetExists(todo.id, weapons, armours, mounts, metaPerks, towers, draftEntries))
            {
                CreateGraphNode($"[TODO] {todo.name}", todo, new Vector2(xOffset, yOffset), todo.id);
                yOffset += 100;
            }
        }
    }

    private Node CreateGraphNode(string title, object data, Vector2 position, string id)
    {
        var node = graphView.CreateNode(title, data, position);
        
        var todo = todoData.entries.FirstOrDefault(e => e.id == id);
        if (todo != null && todo.isTodo)
        {
            node.titleContainer.style.backgroundColor = new Color(0.8f, 0.4f, 0.1f, 0.8f);
            if (!node.title.StartsWith("[TODO]"))
            {
                node.title = "[TODO] " + node.title;
            }
        }
        return node;
    }

    private bool AssetExists(string id, WeaponDefinitionSO[] w, ArmourSetSO[] a, MountDefinitionSO[] m, MetaPerkDefinitionSO[] p, List<GameObject> t, List<DraftUpgradeCsvEntry> d)
    {
        if (w.Any(x => x.id == id)) return true;
        if (a.Any(x => x.id == id)) return true;
        if (m.Any(x => x.id == id)) return true;
        if (p.Any(x => x.id == id)) return true;
        if (t.Any(x => x.name == id)) return true;
        if (d.Any(x => x.id == id)) return true;
        return false;
    }

    private string GetId(object obj)
    {
        if (obj is WeaponDefinitionSO w) return w.id;
        if (obj is ArmourSetSO a) return a.id;
        if (obj is MountDefinitionSO m) return m.id;
        if (obj is MetaPerkDefinitionSO p) return p.id;
        if (obj is GameObject g) return g.name;
        if (obj is DraftUpgradeCsvEntry d) return d.id;
        if (obj is TodoEntry t) return t.id;
        return "";
    }

    private string GetName(object obj)
    {
        if (obj is WeaponDefinitionSO w) return w.displayName;
        if (obj is ArmourSetSO a) return a.displayName;
        if (obj is MountDefinitionSO m) return m.displayName;
        if (obj is MetaPerkDefinitionSO p) return p.displayName;
        if (obj is GameObject g) return g.name;
        if (obj is DraftUpgradeCsvEntry d) return d.displayName;
        if (obj is TodoEntry t) return t.name;
        return "";
    }

    private string GetNodeType(object obj)
    {
        if (obj is WeaponDefinitionSO) return "Weapon";
        if (obj is ArmourSetSO) return "Armour Set";
        if (obj is MountDefinitionSO) return "Mount";
        if (obj is MetaPerkDefinitionSO) return "Meta Perk";
        if (obj is GameObject) return "Tower";
        if (obj is DraftUpgradeCsvEntry) return "Draft Upgrade";
        if (obj is TodoEntry t) return t.type;
        return "Unknown";
    }

    private T[] FindAssets<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        return guids.Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
    }

    private List<GameObject> LoadTowers()
    {
        var list = new List<GameObject>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Bladehold/Bladehold Prefabs/Defenses" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }
        return list;
    }

    private void DrawInspector()
    {
        if (selectedData == null)
        {
            GUILayout.Label("No node selected.");
            return;
        }

        inspectorScrollPosition = GUILayout.BeginScrollView(inspectorScrollPosition);

        // --- TODO TRACKING SECTION ---
        string id = GetId(selectedData);
        var todo = todoData.entries.FirstOrDefault(e => e.id == id);
        
        GUILayout.Label("TODO Tracking", EditorStyles.boldLabel);
        bool isTodo = todo != null && todo.isTodo;
        
        EditorGUI.BeginChangeCheck();
        bool newIsTodo = EditorGUILayout.Toggle("Is TODO", isTodo);
        string newDesc = todo != null ? todo.description : "";
        string newDeps = todo != null ? todo.dependencies : "";

        if (newIsTodo)
        {
            GUILayout.Label("TODO Description / Specification");
            newDesc = EditorGUILayout.TextArea(newDesc, GUILayout.MinHeight(60));
            newDeps = EditorGUILayout.TextField("Dependencies", newDeps);
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (todo == null)
            {
                todo = new TodoEntry { id = id, type = GetNodeType(selectedData), name = GetName(selectedData) };
                todoData.entries.Add(todo);
            }
            
            bool isTodoChanged = todo.isTodo != newIsTodo;
            todo.isTodo = newIsTodo;
            todo.description = newDesc;
            todo.dependencies = newDeps;
            
            if (isTodoChanged)
            {
                var node = graphView.GetNodeByData(selectedData);
                if (node != null)
                {
                    if (newIsTodo)
                    {
                        node.titleContainer.style.backgroundColor = new Color(0.8f, 0.4f, 0.1f, 0.8f);
                        if (!node.title.StartsWith("[TODO]"))
                            node.title = "[TODO] " + node.title;
                    }
                    else
                    {
                        node.titleContainer.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                        if (node.title.StartsWith("[TODO] "))
                            node.title = node.title.Substring(7);
                        else if (node.title.StartsWith("[TODO]"))
                            node.title = node.title.Substring(6);
                    }
                }
            }
            SaveTodos();
        }

        EditorGUILayout.Space(10);
        
        // --- NODE DATA SECTION ---
        EditorGUI.BeginChangeCheck();
        
        if (selectedData is TodoEntry onlyTodo)
        {
            GUILayout.Label("Planned Node (No asset yet)", EditorStyles.boldLabel);
            onlyTodo.id = EditorGUILayout.TextField("ID", onlyTodo.id);
            onlyTodo.name = EditorGUILayout.TextField("Name", onlyTodo.name);
            onlyTodo.type = EditorGUILayout.TextField("Type", onlyTodo.type);
            
            var node = graphView.GetNodeByData(onlyTodo);
            if (node != null)
            {
                string expectedTitle = (onlyTodo.isTodo ? "[TODO] " : "") + onlyTodo.name;
                if (node.title != expectedTitle)
                {
                    node.title = expectedTitle;
                }
            }
        }
        else if (currentSOEditor != null)
        {
            currentSOEditor.OnInspectorGUI();
        }
        else if (selectedData is DraftUpgradeCsvEntry draft)
        {
            DrawDraftEditor(draft);
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplyChanges();
            if (selectedData is TodoEntry)
            {
                SaveTodos();
            }
        }

        GUILayout.EndScrollView();
    }

    private void DrawDraftEditor(DraftUpgradeCsvEntry draft)
    {
        GUILayout.Label("Draft Upgrade CSV Entry", EditorStyles.boldLabel);
        
        draft.id = EditorGUILayout.TextField("ID", draft.id);
        draft.displayName = EditorGUILayout.TextField("Display Name", draft.displayName);
        draft.category = EditorGUILayout.TextField("Category", draft.category);
        draft.weapon = EditorGUILayout.TextField("Weapon ID", draft.weapon);
        draft.element = EditorGUILayout.TextField("Element", draft.element);
        draft.isUltimate = EditorGUILayout.TextField("Is Ultimate", draft.isUltimate);
        draft.maxLevel = EditorGUILayout.TextField("Max Level", draft.maxLevel);
        
        GUILayout.Label("Description");
        draft.description = EditorGUILayout.TextArea(draft.description, GUILayout.MinHeight(40));
        
        draft.upgradeText = EditorGUILayout.TextField("Upgrade Text", draft.upgradeText);
        draft.stat = EditorGUILayout.TextField("Stat", draft.stat);
        draft.kind = EditorGUILayout.TextField("Kind", draft.kind);
        draft.amount = EditorGUILayout.TextField("Amount", draft.amount);
        draft.icon = EditorGUILayout.TextField("Icon", draft.icon);
        draft.targetSlot = EditorGUILayout.TextField("Target Slot", draft.targetSlot);
        draft.isDuo = EditorGUILayout.TextField("Is Duo", draft.isDuo);
        draft.prerequisiteElements = EditorGUILayout.TextField("Prereq Elements", draft.prerequisiteElements);
    }

    private void ApplyChanges()
    {
        if (selectedData is ScriptableObject so)
        {
            EditorUtility.SetDirty(so);
        }
        else if (selectedData is GameObject go)
        {
            EditorUtility.SetDirty(go);
        }

        if (EditorApplication.isPlaying)
        {
            TriggerLiveReload();
        }
    }

    private void SaveAll()
    {
        AssetDatabase.SaveAssets();
        SaveDraftCSV();
        SaveTodos();
        if (EditorApplication.isPlaying) TriggerLiveReload();
        Debug.Log("Balance Tree: All saved!");
    }

    private void TriggerLiveReload()
    {
        var draftType = Type.GetType("DraftUpgradeService, Assembly-CSharp");
        if (draftType != null)
        {
            var instProp = draftType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instProp != null)
            {
                var inst = instProp.GetValue(null);
                if (inst != null)
                {
                    var reloadMethod = draftType.GetMethod("ReloadCatalog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (reloadMethod != null) reloadMethod.Invoke(inst, null);
                }
            }
        }
    }

    private void LoadDraftCSV()
    {
        draftEntries.Clear();
        if (!File.Exists(draftCsvPath)) return;
        
        var lines = File.ReadAllLines(draftCsvPath);
        if (lines.Length <= 1) return;
        
        // Skip header
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line);
            if (parts.Length < 16) continue;
            
            draftEntries.Add(new DraftUpgradeCsvEntry
            {
                id = parts[0],
                displayName = parts[1],
                category = parts[2],
                weapon = parts[3],
                element = parts[4],
                isUltimate = parts[5],
                maxLevel = parts[6],
                description = parts[7],
                upgradeText = parts[8],
                stat = parts[9],
                kind = parts[10],
                amount = parts[11],
                icon = parts[12],
                targetSlot = parts[13],
                isDuo = parts[14],
                prerequisiteElements = parts[15]
            });
        }
    }

    private void SaveDraftCSV()
    {
        if (draftEntries.Count == 0) return;
        
        var lines = new List<string>
        {
            "id,displayName,category,weapon,element,isUltimate,maxLevel,description,upgradeText,stat,kind,amount,icon,targetSlot,isDuo,prerequisiteElements"
        };
        
        foreach (var d in draftEntries)
        {
            lines.Add($"{EscapeCsv(d.id)},{EscapeCsv(d.displayName)},{EscapeCsv(d.category)},{EscapeCsv(d.weapon)},{EscapeCsv(d.element)},{EscapeCsv(d.isUltimate)},{EscapeCsv(d.maxLevel)},{EscapeCsv(d.description)},{EscapeCsv(d.upgradeText)},{EscapeCsv(d.stat)},{EscapeCsv(d.kind)},{EscapeCsv(d.amount)},{EscapeCsv(d.icon)},{EscapeCsv(d.targetSlot)},{EscapeCsv(d.isDuo)},{EscapeCsv(d.prerequisiteElements)}");
        }
        
        File.WriteAllLines(draftCsvPath, lines);
    }

    private void LoadTodos()
    {
        if (File.Exists(todoJsonPath))
        {
            string json = File.ReadAllText(todoJsonPath);
            todoData = JsonUtility.FromJson<TodoData>(json) ?? new TodoData();
        }
    }

    private void SaveTodos()
    {
        todoData.entries.RemoveAll(e => !e.isTodo);
        string json = JsonUtility.ToJson(todoData, true);
        File.WriteAllText(todoJsonPath, json);
        
        var mdLines = new List<string>
        {
            "# Upgrade TODOs",
            "Auto-generated by Balance Tree Editor.",
            ""
        };
        
        foreach (var t in todoData.entries)
        {
            if (t.isTodo)
            {
                mdLines.Add($"## [TODO] {t.name} ({t.type})");
                mdLines.Add($"**ID**: {t.id}");
                mdLines.Add($"**Dependencies**: {t.dependencies}");
                mdLines.Add($"**Description**:");
                mdLines.Add(t.description);
                mdLines.Add("");
                mdLines.Add("---");
                mdLines.Add("");
            }
        }
        
        File.WriteAllLines(todoMdPath, mdLines);
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var currentPart = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentPart.ToString());
                currentPart.Clear();
            }
            else
            {
                currentPart.Append(c);
            }
        }
        result.Add(currentPart.ToString());
        return result.ToArray();
    }

    private string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}

public class BalanceGraphView : GraphView
{
    public Action<object> OnNodeSelected;
    private Dictionary<Node, object> nodeDataMap = new Dictionary<Node, object>();

    public BalanceGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        
        var grid = new GridBackground();
        grid.StretchToParentSize();
        Insert(0, grid);
    }

    public Node GetNodeByData(object data)
    {
        return nodeDataMap.FirstOrDefault(kvp => kvp.Value == data).Key;
    }

    public void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        nodeDataMap.Clear();
    }

    public Node CreateNode(string title, object data, Vector2 position)
    {
        var node = new Node
        {
            title = title,
        };

        var inPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inPort.portName = "In";
        node.inputContainer.Add(inPort);

        var outPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        outPort.portName = "Out";
        node.outputContainer.Add(outPort);

        node.SetPosition(new Rect(position, Vector2.zero));
        node.RefreshExpandedState();
        node.RefreshPorts();

        // Detect click for inspector
        node.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button == 0)
            {
                OnNodeSelected?.Invoke(data);
            }
        });

        AddElement(node);
        nodeDataMap[node] = data;

        return node;
    }

    public void ConnectNodes(Node parent, Node child)
    {
        var outPort = parent.outputContainer.Q<Port>();
        var inPort = child.inputContainer.Q<Port>();
        if (outPort != null && inPort != null)
        {
            var edge = outPort.ConnectTo(inPort);
            AddElement(edge);
        }
    }
}

public class DraftUpgradeCsvEntry
{
    public string id;
    public string displayName;
    public string category;
    public string weapon;
    public string element;
    public string isUltimate;
    public string maxLevel;
    public string description;
    public string upgradeText;
    public string stat;
    public string kind;
    public string amount;
    public string icon;
    public string targetSlot;
    public string isDuo;
    public string prerequisiteElements;
}
