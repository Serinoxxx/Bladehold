using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     The Scene-view detail panel for the skill tree editor: shown (docked) while a
///     <see cref="SkillTreeSceneEditor" /> session is active. Header = save/revert/close; then either an
///     interaction cheatsheet (nothing selected), the full node editor (one selected: id, name,
///     description/upgrade text, cost/growth/maxLevel, effects, prereqs, position, icon), or the multi-edit
///     panel (several selected: shared icon swap + delete). All edits funnel through SkillTreeSceneEditor so they're
///     undoable and the Scene-view preview refreshes live.
/// </summary>
[Overlay(typeof(SceneView), "Skill Tree Editor", defaultDisplay = false)]
public class SkillTreeOverlay : Overlay
{
    private IMGUIContainer container;
    private Vector2 scroll;

    public override VisualElement CreatePanelContent()
    {
        container = new IMGUIContainer(DrawPanel);
        container.style.minWidth = 320;
        container.style.maxHeight = 600;
        return container;
    }

    public override void OnCreated()
    {
        base.OnCreated();
        SkillTreeSceneEditor.RegisterOverlay(this);
        SkillTreeSceneEditor.SessionChanged += Repaint;
        SkillTreeSceneEditor.SelectionChanged += Repaint;
    }

    public override void OnWillBeDestroyed()
    {
        SkillTreeSceneEditor.SessionChanged -= Repaint;
        SkillTreeSceneEditor.SelectionChanged -= Repaint;
        SkillTreeSceneEditor.UnregisterOverlay(this);
        base.OnWillBeDestroyed();
    }

    private void Repaint()
    {
        container?.MarkDirtyRepaint();
    }

    private void DrawPanel()
    {
        SkillTreeEditSession session = SkillTreeSceneEditor.Session;
        if (!session.active)
        {
            EditorGUILayout.HelpBox("No editing session. Start one via Bladehold > Skill Tree Scene Editor.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawHeader(session);
        EditorGUILayout.Space(4f);
        DrawModeButtons(session);
        EditorGUILayout.Space(4f);

        var selected = new List<SkillTreeRow>(session.SelectedRows());
        if (selected.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Click a node to edit it.\n" +
                "Drag to move (snaps to 0.5 grid steps).\n" +
                "Drag empty space to box-select; Shift-click to add.\n" +
                "Hover a node and click its + to add a linked child.\n" +
                "Alt-click a node to link it with the selected node (either unlocks the other).\n" +
                "Delete key removes the selection.",
                MessageType.Info);
        }
        else if (selected.Count == 1)
        {
            DrawSingleNode(session, selected[0]);
        }
        else
        {
            DrawMultiSelect(session, selected);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawHeader(SkillTreeEditSession session)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{SkillTreeSceneEditor.TreeName()}{(session.dirty ? " •" : "")}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(!session.dirty))
        {
            if (GUILayout.Button("Save CSV"))
            {
                SkillTreeSceneEditor.SaveCsv();
            }
            if (GUILayout.Button("Revert"))
            {
                SkillTreeSceneEditor.RevertFromDisk();
            }
        }
        if (GUILayout.Button("Close"))
        {
            SkillTreeSceneEditor.EndSession();
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawModeButtons(SkillTreeEditSession session)
    {
        EditorGUILayout.BeginHorizontal();

        bool oneSelected = session.selectedIds.Count == 1;
        using (new EditorGUI.DisabledScope(!oneSelected))
        {
            bool linking = SkillTreeSceneGuiHandler.IsLinking;
            bool wantLinking = GUILayout.Toggle(linking, new GUIContent("Link Skill",
                "Then click another node in the Scene view — the two nodes become linked; purchasing either one unlocks the other."), "Button");
            if (wantLinking != linking)
            {
                if (wantLinking)
                {
                    SkillTreeSceneGuiHandler.EnterLinkMode(session.selectedIds[0]);
                }
                else
                {
                    SkillTreeSceneGuiHandler.ExitLinkMode();
                }
            }

            if (GUILayout.Button(new GUIContent("Add Node", "Add a new node linked to the selected one (same as the Scene-view + button).")))
            {
                SkillTreeSceneEditor.AddChildNode(session.selectedIds[0]);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawSingleNode(SkillTreeEditSession session, SkillTreeRow row)
    {
        string id = row.id;
        SkillTreeSO tree = session.Tree;

        // Id commits on Enter/focus-loss (a live rename per keystroke would thrash prereq rewrites).
        string newId = EditorGUILayout.DelayedTextField("Id", row.id);
        if (newId != row.id)
        {
            SkillTreeSceneEditor.RenameNode(row.id, newId);
            id = row.id; // row instance was edited in place; keep using its (possibly unchanged) id
        }
        if (SkillTreeCsvIO.CountId(session.rows, row.id) > 1)
        {
            EditorGUILayout.HelpBox("Duplicate id — the tree will ignore the second occurrence.", MessageType.Error);
        }

        EditorGUI.BeginChangeCheck();
        string displayName = EditorGUILayout.TextField("Display Name", row.displayName);
        EditorGUILayout.LabelField(new GUIContent("Description (unlock text)", "Shown before purchase, and the only text for single-level nodes."));
        string description = EditorGUILayout.TextArea(row.description, GUILayout.MinHeight(40f));
        EditorGUILayout.LabelField(new GUIContent("Upgrade Text", "Shown once owned and still upgradeable. Blank reuses the description."));
        string upgradeText = EditorGUILayout.TextArea(row.upgradeText, GUILayout.MinHeight(30f));
        int cost = EditorGUILayout.IntField(new GUIContent("Cost", "Cost of level 1."), row.cost);
        float growth = EditorGUILayout.FloatField(new GUIContent("Growth",
            "Per-level cost multiplier: each level costs round(prev × growth). ≤1 = flat cost every level."), row.growth);
        int maxLevel = EditorGUILayout.IntField(new GUIContent("Max Level",
            "How many times the node can be purchased. 1 = single-level."), row.maxLevel);
        if (EditorGUI.EndChangeCheck())
        {
            SkillTreeSceneEditor.EditRow(id, r =>
            {
                r.displayName = displayName;
                r.description = description;
                r.upgradeText = upgradeText;
                r.cost = cost;
                r.growth = growth;
                r.maxLevel = maxLevel;
            });
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Effects (';'-separated per stat; blank = connector node)", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        string stat = EditorGUILayout.TextField(new GUIContent("Stat", "StatType name(s), e.g. SwordDamage or GoldenGoblinChance;GoldenGoblinGoldBonusPercent"), row.stat);
        string kind = EditorGUILayout.TextField(new GUIContent("Kind", "Flat or Percent, one per stat"), row.kind);
        string amount = EditorGUILayout.TextField(new GUIContent("Amount",
            "Per stat, ';'-separated. Within one stat, a '|'-separated list gives per-level increments (length = Max Level); a single value repeats every level. e.g. 0.05;0.5|0.5|1|2"), row.amount);
        if (EditorGUI.EndChangeCheck())
        {
            SkillTreeSceneEditor.EditRow(id, r =>
            {
                r.stat = stat;
                r.kind = kind;
                r.amount = amount;
            });
        }

        EditorGUILayout.Space(2f);
        bool wantRoot = EditorGUILayout.Toggle(new GUIContent("Root (unlocked at start)",
            "A start-unlocked entry node. Every tree needs at least one root; the rest unlock by buying a linked node. This flag — not an empty link list — is what makes a node a root."), row.isRoot);
        if (wantRoot != row.isRoot)
        {
            SkillTreeSceneEditor.SetRoot(id, wantRoot);
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Links (use Link Skill or Alt-click to add)", EditorStyles.miniBoldLabel);
        foreach (string prereq in row.PrereqList())
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(prereq);
            if (GUILayout.Button("×", GUILayout.Width(22f)))
            {
                SkillTreeSceneEditor.UnlinkSkill(prereq, id);
                EditorGUILayout.EndHorizontal();
                return; // the list changed under this loop — redraw next frame
            }
            EditorGUILayout.EndHorizontal();
        }
        if (row.PrereqList().Count == 0)
        {
            EditorGUILayout.LabelField(
                row.isRoot ? "(no links)" : "(no links — unreachable unless marked Root)",
                EditorStyles.miniLabel);
        }
        if (SkillTreeSceneEditor.HasAnyLinks(id) && GUILayout.Button(new GUIContent("Clear All Links",
                "Removes this node's own prereqs, and removes it from any other node's prereqs.")))
        {
            SkillTreeSceneEditor.ClearAllLinks(id);
            return; // links changed under this draw — redraw next frame
        }

        EditorGUILayout.Space(2f);
        EditorGUI.BeginChangeCheck();
        float x = EditorGUILayout.DelayedFloatField("X", row.x);
        float y = EditorGUILayout.DelayedFloatField("Y", row.y);
        if (EditorGUI.EndChangeCheck())
        {
            float snappedX = SkillTreeSceneEditor.Snap(x);
            float snappedY = SkillTreeSceneEditor.Snap(y);
            SkillTreeSceneEditor.EditRow(id, r =>
            {
                r.x = snappedX;
                r.y = snappedY;
            });
        }

        EditorGUILayout.Space(2f);
        Sprite current = tree != null ? tree.GetIcon(row.icon) : null;
        var picked = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Icon",
            "Drag a Sprite here; it is added to the tree's icons list and referenced by name."), current, typeof(Sprite), false);
        if (picked != current)
        {
            if (picked != null && tree != null)
            {
                SkillTreeCsvIO.EnsureIconInTree(tree, picked);
            }
            string iconName = picked != null ? picked.name : "";
            SkillTreeSceneEditor.EditRow(id, r => r.icon = iconName);
        }
        if (!string.IsNullOrEmpty(row.icon) && tree != null && tree.GetIcon(row.icon) == null)
        {
            EditorGUILayout.HelpBox($"No sprite named '{row.icon}' in this tree's icons list.", MessageType.Warning);
        }
    }

    private static void DrawMultiSelect(SkillTreeEditSession session, List<SkillTreeRow> selected)
    {
        EditorGUILayout.LabelField($"{selected.Count} nodes selected", EditorStyles.boldLabel);

        SkillTreeSO tree = session.Tree;

        // Show the shared sprite when every selected node uses the same icon, else a mixed (null) field.
        string sharedIcon = selected[0].icon;
        for (int i = 1; i < selected.Count; i++)
        {
            if (selected[i].icon != sharedIcon)
            {
                sharedIcon = null;
                break;
            }
        }
        Sprite current = sharedIcon != null && tree != null ? tree.GetIcon(sharedIcon) : null;

        EditorGUI.showMixedValue = sharedIcon == null;
        var picked = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Icon (apply to all)",
            "Assigns this sprite to every selected node."), current, typeof(Sprite), false);
        EditorGUI.showMixedValue = false;
        if (picked != current && picked != null)
        {
            SkillTreeSceneEditor.SetIconForSelection(picked);
        }

        EditorGUILayout.Space(4f);
        bool anySelectedHasLinks = false;
        foreach (SkillTreeRow r in selected)
        {
            if (SkillTreeSceneEditor.HasAnyLinks(r.id))
            {
                anySelectedHasLinks = true;
                break;
            }
        }
        using (new EditorGUI.DisabledScope(!anySelectedHasLinks))
        {
            if (GUILayout.Button(new GUIContent("Clear All Links",
                    "Removes every link touching any selected node — its own links and any other node's link to it.")))
            {
                SkillTreeSceneEditor.ClearAllLinksForSelection();
            }
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Delete Selected"))
        {
            SkillTreeSceneEditor.DeleteSelection();
        }
    }
}
