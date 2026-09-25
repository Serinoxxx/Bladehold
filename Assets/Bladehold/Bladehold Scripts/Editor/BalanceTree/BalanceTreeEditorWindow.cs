using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>
///     Bladehold > Balance Tree Editor (F1). One view of every draft card and what it depends on: weapons,
///     elements, towers, the StatTypes each card writes (and which scripts read them), armour, mounts and
///     meta perks. Filter by weapon/element/category/ultimate/demo/search, edit cards inline with a
///     per-level preview, and save back to DraftUpgrades.csv losslessly (see <see cref="DraftCsvDocument" />).
///     Side panels: Validation and Pacing. Edit-mode tool: in Play mode, edits reach the game on the next
///     Play session (the draft catalog is read once per session).
/// </summary>
public class BalanceTreeEditorWindow : EditorWindow
{
    private enum Tab
    {
        Inspector,
        Validation,
        Pacing
    }

    // Columns whose edits change edges or lanes, so the graph rebuilds.
    private static readonly HashSet<string> StructuralColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "id", "category", "weapon", "element", "isUltimate", "stat", "targetSlot", "isDuo", "prerequisiteElements"
    };

    private static readonly HashSet<string> TextAreaColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "description", "upgradeText" };

    [SerializeField] private BalanceTreeFilter filter = new BalanceTreeFilter();
    [SerializeField] private string selectedKey;
    [SerializeField] private Tab tab;
    [SerializeField] private bool showErrors = true, showWarnings = true, showInfo = true;

    private DraftCsvDocument csv;
    private BalanceTreeModel model;
    private BalanceTreeLayoutStore layout;
    private List<BalanceIssue> issues = new List<BalanceIssue>();
    private HashSet<BalanceNode> visible = new HashSet<BalanceNode>();
    private SkillTreeIconsSO icons;

    private BalanceGraphView graphView;
    private IMGUIContainer sidePanel;
    private ToolbarButton saveButton;
    private Label countLabel;
    private ToolbarMenu weaponMenu, elementMenu, categoryMenu, kindsMenu;
    private Vector2 sideScroll;
    private Editor embeddedEditor;
    private bool rebuildQueued;

    [MenuItem("Bladehold/Balance Tree Editor _F1", false, 0)]
    public static void ShowWindow() => GetWindow<BalanceTreeEditorWindow>("Balance Tree").Show();

    private void OnEnable()
    {
        saveChangesMessage = "DraftUpgrades.csv has unsaved Balance Tree edits.";
        layout = BalanceTreeLayoutStore.Load();
        icons = Resources.Load<SkillTreeIconsSO>("SkillTreeIcons");
        BuildUi();
        Reload();
    }

    private void OnDisable()
    {
        if (embeddedEditor != null) DestroyImmediate(embeddedEditor);
    }

    public override void SaveChanges()
    {
        Save();
        base.SaveChanges();
    }

    // --- UI ----------------------------------------------------------------------------------------------

    private void BuildUi()
    {
        rootVisualElement.Clear();

        var actions = new Toolbar();
        actions.Add(new ToolbarButton(ConfirmReload) { text = "Reload", tooltip = "Re-read the CSV and assets from disk." });
        saveButton = new ToolbarButton(Save) { text = "Save", tooltip = "Write DraftUpgrades.csv (edited rows only) and save edited assets." };
        actions.Add(saveButton);
        actions.Add(new ToolbarButton(() => graphView.FrameAll()) { text = "Frame All" });
        actions.Add(new ToolbarButton(ResetLayout) { text = "Reset Layout", tooltip = "Forget dragged node positions and auto-layout again." });
        actions.Add(new ToolbarSpacer { flex = true });
        countLabel = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, marginRight = 6 } };
        actions.Add(countLabel);
        rootVisualElement.Add(actions);

        var filters = new Toolbar();
        weaponMenu = new ToolbarMenu();
        elementMenu = new ToolbarMenu();
        categoryMenu = new ToolbarMenu();
        kindsMenu = new ToolbarMenu { text = "Show" };
        filters.Add(weaponMenu);
        filters.Add(elementMenu);
        filters.Add(categoryMenu);

        var ultimates = new ToolbarToggle { text = "Ultimates", value = filter.ultimatesOnly, tooltip = "Only ultimate cards." };
        ultimates.RegisterValueChangedCallback(e => { filter.ultimatesOnly = e.newValue; ApplyFilter(); });
        filters.Add(ultimates);
        var demo = new ToolbarToggle
        {
            text = "Demo only", value = filter.demoOnly,
            tooltip = "Hide demo-locked weapons (and their cards) and tier 2+ meta perks. Plan 07 will own the real demo gating."
        };
        demo.RegisterValueChangedCallback(e => { filter.demoOnly = e.newValue; ApplyFilter(); });
        filters.Add(demo);
        filters.Add(kindsMenu);

        var search = new ToolbarSearchField { value = filter.search, style = { flexGrow = 1, maxWidth = 320 } };
        search.RegisterValueChangedCallback(e => { filter.search = e.newValue ?? ""; ApplyFilter(); });
        filters.Add(search);
        filters.Add(new ToolbarButton(ClearFilters) { text = "Clear" });
        rootVisualElement.Add(filters);

        var split = new TwoPaneSplitView(1, 380, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
        graphView = new BalanceGraphView { style = { flexGrow = 1 } };
        graphView.OnNodeSelected = n => Select(n.Key, reveal: false);
        split.Add(graphView);

        sidePanel = new IMGUIContainer(DrawSidePanel) { style = { flexGrow = 1 } };
        split.Add(sidePanel);
        rootVisualElement.Add(split);
    }

    private void RefreshFilterMenus()
    {
        weaponMenu.text = filter.weapon.Length > 0 ? $"Weapon: {filter.weapon}" : "Weapon: any";
        elementMenu.text = filter.element.Length > 0 ? $"Element: {filter.element}" : "Element: any";
        categoryMenu.text = filter.category.Length > 0 ? $"Category: {filter.category}" : "Category: any";

        FillMenu(weaponMenu, model.Weapons.Select(w => w.id), () => filter.weapon, v => filter.weapon = v);
        FillMenu(elementMenu, model.Nodes.Where(n => n.Kind == BalanceNodeKind.Element).Select(n => n.Title), () => filter.element, v => filter.element = v);
        FillMenu(categoryMenu, Enum.GetNames(typeof(DraftCategory)), () => filter.category, v => filter.category = v);

        kindsMenu.menu.MenuItems().Clear();
        foreach (BalanceNodeKind kind in Enum.GetValues(typeof(BalanceNodeKind)))
        {
            kindsMenu.menu.AppendAction(kind.ToString(), _ =>
            {
                if (!filter.hiddenKinds.Remove(kind)) filter.hiddenKinds.Add(kind);
                ApplyFilter();
            }, _ => filter.hiddenKinds.Contains(kind) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Checked);
        }
    }

    private void FillMenu(ToolbarMenu menu, IEnumerable<string> values, Func<string> get, Action<string> set)
    {
        menu.menu.MenuItems().Clear();
        foreach (string v in new[] { "" }.Concat(values))
        {
            string value = v;
            menu.menu.AppendAction(value.Length == 0 ? "(any)" : value, _ => { set(value); ApplyFilter(); },
                _ => string.Equals(get(), value, StringComparison.OrdinalIgnoreCase) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }
    }

    private void ClearFilters()
    {
        filter = new BalanceTreeFilter();
        BuildUi();
        RebuildGraph();
    }

    // --- Data --------------------------------------------------------------------------------------------

    private void ConfirmReload()
    {
        if (csv != null && csv.IsDirty && !EditorUtility.DisplayDialog("Reload Balance Tree", "Discard unsaved CSV edits and reload from disk?", "Discard", "Cancel")) return;
        Reload();
    }

    private void Reload()
    {
        csv = DraftCsvDocument.Load();
        model = BalanceTreeModel.Load(csv);
        RebuildGraph();
    }

    private void Save()
    {
        bool wroteCsv = csv.Save();
        AssetDatabase.SaveAssets();
        UpdateDirtyState();
        if (wroteCsv)
        {
            Debug.Log(EditorApplication.isPlaying
                ? "[Balance Tree] Saved DraftUpgrades.csv. The running game read the catalog at startup; changes apply from the next Play session."
                : "[Balance Tree] Saved DraftUpgrades.csv.");
        }
    }

    private void ResetLayout()
    {
        layout.Clear();
        RebuildGraph();
        graphView.FrameAll();
    }

    private void RebuildGraph()
    {
        issues = BalanceTreeValidator.Run(model, icons);
        RefreshFilterMenus();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        RefreshFilterMenus();
        visible = filter.Apply(model);
        graphView.Build(model, visible, layout, BalanceTreeValidator.WorstByNode(issues));
        countLabel.text = $"{visible.Count(n => n.Kind == BalanceNodeKind.Card)} / {model.Cards.Count()} cards · {visible.Count} nodes";
        if (selectedKey != null) graphView.Reveal(selectedKey, frame: false);
        UpdateDirtyState();
        sidePanel.MarkDirtyRepaint();
    }

    private void UpdateDirtyState()
    {
        hasUnsavedChanges = csv != null && csv.IsDirty;
        if (saveButton != null) saveButton.text = hasUnsavedChanges ? "Save *" : "Save";
    }

    private void Select(string key, bool reveal)
    {
        if (selectedKey != key && embeddedEditor != null)
        {
            DestroyImmediate(embeddedEditor);
            embeddedEditor = null;
        }
        selectedKey = key;
        tab = Tab.Inspector;
        if (reveal && !graphView.Reveal(key, frame: true))
        {
            ShowNotification(new GUIContent("Hidden by the current filter"));
        }
        sidePanel.MarkDirtyRepaint();
        Repaint();
    }

    private void OnCardEdited(BalanceNode card, bool structural)
    {
        model.RefreshCard(card);
        issues = BalanceTreeValidator.Run(model, icons);
        UpdateDirtyState();
        if (structural)
        {
            // Rebuild after this IMGUI pass, not in the middle of it.
            if (!rebuildQueued)
            {
                rebuildQueued = true;
                EditorApplication.delayCall += () =>
                {
                    rebuildQueued = false;
                    if (this != null) ApplyFilter();
                };
            }
        }
        else
        {
            BalanceTreeValidator.WorstByNode(issues).TryGetValue(card.Key, out BalanceIssueSeverity worst);
            graphView.UpdateNode(card, worst);
        }
    }

    // --- Side panel --------------------------------------------------------------------------------------

    private void DrawSidePanel()
    {
        if (model == null) return;

        int errors = issues.Count(i => i.Severity == BalanceIssueSeverity.Error);
        int warnings = issues.Count(i => i.Severity == BalanceIssueSeverity.Warning);
        string validationLabel = errors + warnings > 0 ? $"Validation ({errors} / {warnings})" : "Validation";
        tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "Inspector", validationLabel, "Pacing" });

        sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
        switch (tab)
        {
            case Tab.Inspector: DrawInspector(); break;
            case Tab.Validation: DrawValidation(); break;
            case Tab.Pacing: DrawPacing(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawInspector()
    {
        BalanceNode node = model.Find(selectedKey);
        if (node == null)
        {
            EditorGUILayout.HelpBox("Select a node. Filter with the toolbar; drag nodes to arrange them (positions are kept per machine).", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(node.Title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(node.Subtitle ?? "", EditorStyles.miniLabel);

        foreach (BalanceIssue issue in issues.Where(i => i.NodeKey == node.Key))
        {
            EditorGUILayout.HelpBox(issue.Message, issue.Severity == BalanceIssueSeverity.Error ? MessageType.Error
                : issue.Severity == BalanceIssueSeverity.Warning ? MessageType.Warning : MessageType.Info);
        }

        switch (node.Kind)
        {
            case BalanceNodeKind.Card:
                DrawCardFields(node);
                DrawCardPreview(node);
                break;
            case BalanceNodeKind.Stat:
                DrawStat(node);
                break;
            case BalanceNodeKind.Tower:
                DrawTower(node);
                break;
            case BalanceNodeKind.Element:
                break;
            default:
                DrawAsset(node);
                break;
        }

        DrawRelated(node);
    }

    private void DrawCardFields(BalanceNode card)
    {
        DraftCsvRow row = card.Row;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"DraftUpgrades.csv, line {row.LineNumber}{(row.IsDirty ? " (edited)" : "")}", EditorStyles.miniBoldLabel);

        foreach (string column in csv.Header)
        {
            string before = row.Get(column);
            string after;

            if (column.Equals("category", StringComparison.OrdinalIgnoreCase))
                after = Popup(column, before, Enum.GetNames(typeof(DraftCategory)));
            else if (column.Equals("weapon", StringComparison.OrdinalIgnoreCase))
                after = Popup(column, before, model.Weapons.Select(w => w.id));
            else if (column.Equals("targetSlot", StringComparison.OrdinalIgnoreCase))
                after = Popup(column, before, RunSession.KnownElementalSlots.OrderBy(s => s));
            else if (TextAreaColumns.Contains(column))
            {
                EditorGUILayout.LabelField(column);
                after = EditorGUILayout.TextArea(before, EditorStyles.textArea, GUILayout.MinHeight(36));
            }
            else
                after = EditorGUILayout.DelayedTextField(column, before);

            if (after == before) continue;
            row.Set(column, after);
            OnCardEdited(card, StructuralColumns.Contains(column));
        }

        EditorGUILayout.HelpBox("Lists: ';' separates stats (with matching kind/amount), '|' separates per-level amounts and prerequisite elements. Amounts are absolute per level.", MessageType.None);
    }

    private static string Popup(string label, string current, IEnumerable<string> options)
    {
        var list = new List<string> { "" };
        list.AddRange(options);
        if (!list.Contains(current, StringComparer.OrdinalIgnoreCase)) list.Add(current);
        int index = list.FindIndex(o => o.Equals(current, StringComparison.OrdinalIgnoreCase));
        int picked = EditorGUILayout.Popup(label, index, list.Select(o => o.Length == 0 ? "(none)" : o).ToArray());
        // Keep the cell's original casing unless the user actually picked something else.
        return picked == index ? current : list[picked];
    }

    private void DrawCardPreview(BalanceNode card)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Card preview", EditorStyles.boldLabel);

        DraftUpgradeDefinition def = card.Definition;
        if (def == null)
        {
            EditorGUILayout.HelpBox("The runtime skips this row, so it never appears in a draft. See the errors above.", MessageType.Error);
            return;
        }

        Sprite sprite = icons != null ? icons.GetIcon(def.iconName) : null;
        using (new EditorGUILayout.HorizontalScope())
        {
            Texture2D tex = sprite != null ? AssetPreview.GetAssetPreview(sprite) : null;
            if (tex != null) GUILayout.Label(tex, GUILayout.Width(48), GUILayout.Height(48));
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(def.displayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{def.category}{(def.isUltimate ? " · ultimate" : "")}{(string.IsNullOrEmpty(def.targetSlot) ? "" : " · imbues " + def.targetSlot)}", EditorStyles.miniLabel);
            }
        }

        var body = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
        for (int level = 1; level <= def.maxLevel; level++)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int current = level - 1;
                EditorGUILayout.LabelField(current > 0 ? $"Level {current} -> {level} (Max {def.maxLevel})" : $"Unlock Level 1 (Max {def.maxLevel})", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(def.GetDescriptionForLevel(current), body);
                if (level == 1 && !string.IsNullOrEmpty(def.targetSlot))
                {
                    EditorGUILayout.LabelField($"<color=#FFA500>If {def.targetSlot} holds another element: [Overwrite] (+{DraftUpgradeService.ElementOverwriteGold} gold per card)</color>", body);
                }

                foreach (SkillEffect effect in def.effects)
                {
                    float now = effect.AmountForLevel(level);
                    float delta = now - (level > 1 ? effect.AmountForLevel(level - 1) : 0f);
                    EditorGUILayout.LabelField($"  {effect.stat}: {FormatAmount(now, effect.kind)}{(level > 1 ? $"  ({(delta >= 0 ? "+" : "")}{FormatAmount(delta, effect.kind, signed: false)} vs L{level - 1})" : "")}", EditorStyles.miniLabel);
                }
            }
        }
    }

    private static string FormatAmount(float amount, ModifierKind kind, bool signed = true)
    {
        string sign = signed && amount >= 0 ? "+" : "";
        return kind == ModifierKind.Percent
            ? $"{sign}{(amount * 100f).ToString("0.##", CultureInfo.InvariantCulture)}%"
            : $"{sign}{amount.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private void DrawStat(BalanceNode stat)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Referenced by (gameplay scripts)", EditorStyles.boldLabel);
        if (!model.StatConsumers.TryGetValue((string)stat.Data, out List<string> files))
        {
            EditorGUILayout.LabelField("Nothing. This stat has no effect in the game.", EditorStyles.wordWrappedMiniLabel);
            return;
        }
        foreach (string path in files) ScriptButton(path);
    }

    private void DrawTower(BalanceNode tower)
    {
        var go = (GameObject)tower.Data;
        EditorGUILayout.Space();
        if (GUILayout.Button("Open prefab")) AssetDatabase.OpenAsset(go);
        EditorGUILayout.LabelField("Fort scripts on the prefab", EditorStyles.boldLabel);
        foreach (string path in model.TowerScripts(go)) ScriptButton(path);
    }

    private static void ScriptButton(string path)
    {
        if (GUILayout.Button(path.Replace("Assets/Bladehold/Bladehold Scripts/", ""), EditorStyles.linkLabel))
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(path));
        }
    }

    private void DrawAsset(BalanceNode node)
    {
        var asset = node.Data as Object;
        if (asset == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.ObjectField("Asset", asset, typeof(Object), false);
        if (embeddedEditor == null || embeddedEditor.target != asset)
        {
            if (embeddedEditor != null) DestroyImmediate(embeddedEditor);
            embeddedEditor = Editor.CreateEditor(asset);
        }
        embeddedEditor.OnInspectorGUI();
    }

    private void DrawRelated(BalanceNode node)
    {
        List<BalanceNode> related = model.Neighbours(node).Distinct().OrderBy(n => n.Kind).ThenBy(n => n.Title).ToList();
        if (related.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Connected ({related.Count})", EditorStyles.boldLabel);
        foreach (BalanceNode n in related)
        {
            string hidden = visible.Contains(n) ? "" : "  (filtered out)";
            if (GUILayout.Button($"{n.Kind}: {n.Title}{hidden}", EditorStyles.linkLabel)) Select(n.Key, reveal: true);
        }
    }

    private void DrawValidation()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            showErrors = GUILayout.Toggle(showErrors, $"Errors ({issues.Count(i => i.Severity == BalanceIssueSeverity.Error)})", EditorStyles.miniButtonLeft);
            showWarnings = GUILayout.Toggle(showWarnings, $"Warnings ({issues.Count(i => i.Severity == BalanceIssueSeverity.Warning)})", EditorStyles.miniButtonMid);
            showInfo = GUILayout.Toggle(showInfo, $"Info ({issues.Count(i => i.Severity == BalanceIssueSeverity.Info)})", EditorStyles.miniButtonRight);
        }

        foreach (BalanceIssue issue in issues)
        {
            if (issue.Severity == BalanceIssueSeverity.Error && !showErrors) continue;
            if (issue.Severity == BalanceIssueSeverity.Warning && !showWarnings) continue;
            if (issue.Severity == BalanceIssueSeverity.Info && !showInfo) continue;

            string icon = issue.Severity == BalanceIssueSeverity.Error ? "console.erroricon.sml"
                : issue.Severity == BalanceIssueSeverity.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
            var content = new GUIContent(issue.Message, EditorGUIUtility.IconContent(icon).image);
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { imagePosition = ImagePosition.ImageLeft };
            if (GUILayout.Button(content, style) && issue.NodeKey != null) Select(issue.NodeKey, reveal: true);
        }
    }

    private void DrawPacing()
    {
        EditorGUILayout.LabelField("Weapon cards", EditorStyles.boldLabel);
        TableRow(true, "Weapon", "Cards", "Ults", "Picks");
        foreach (BalanceTreePacing.WeaponRow r in BalanceTreePacing.ByWeapon(model))
        {
            TableRow(false, r.Weapon + (r.DemoLocked ? " (demo-locked)" : ""), Count(r.Cards), Count(r.Ultimates), Count(r.Picks));
        }
        EditorGUILayout.LabelField("Picks = sum of max levels: how many drafts that pool can absorb.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Elemental cards by slot", EditorStyles.boldLabel);
        TableRow(true, "Element", "Melee", "Ranged", "Dash", "Ult", "Fort", "Passive");
        foreach (KeyValuePair<string, int[]> kv in BalanceTreePacing.ByElementAndSlot(model, elementalOnly: true))
        {
            TableRow(false, new[] { kv.Key }.Concat(kv.Value.Select(Count)).ToArray());
        }
        EditorGUILayout.LabelField("Orange 0 = that element can't imbue that slot. Duos have no slot, so they show as passive.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("By category", EditorStyles.boldLabel);
        TableRow(true, "Category", "Cards", "Picks");
        foreach (var (category, cards, picks) in BalanceTreePacing.ByCategory(model))
        {
            TableRow(false, category, Count(cards), Count(picks));
        }
    }

    private static string Count(int n) => n == 0 ? "<color=#FFA500>0</color>" : n.ToString();

    private static void TableRow(bool header, params string[] cells)
    {
        var style = new GUIStyle(header ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel) { richText = true };
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(cells[0], style, GUILayout.Width(150));
            for (int i = 1; i < cells.Length; i++) GUILayout.Label(cells[i], style, GUILayout.Width(44));
        }
    }
}
