using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     The Scene-view skill tree editor's controller (Bladehold > Skill Tree Scene Editor). Opens the
///     SkillTreePreview scene, spawns the real node/connector prefabs in edit mode via
///     <see cref="SkillTreePreviewBuilder" /> (true WYSIWYG — it is the runtime UI), and owns every
///     mutation of the working CSV rows: add/link/move/duplicate/delete/edit/save. Input arrives from
///     <see cref="SkillTreeSceneGuiHandler" /> (drag, marquee, '+' button, link mode, delete key) and
///     from <see cref="SkillTreeOverlay" /> (the docked detail panel); both only ever talk to this class.
///     Every mutation is captured with Undo.RecordObject on the <see cref="SkillTreeEditSession" />
///     singleton, so Ctrl+Z works; the session survives domain reloads and play mode, resuming with its
///     unsaved rows intact.
/// </summary>
[InitializeOnLoad]
public static class SkillTreeSceneEditor
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/SkillTreePreview.unity";
    private const string GoldTreePath = "Assets/Bladehold/Bladehold Scripts/Upgrades/SkillTreeSO.asset";
    private const string ReincarnateTreePath = "Assets/Bladehold/Bladehold Scripts/Upgrades/ReincarnateSkillTreeSO.asset";

    /// <summary>Grid snap step for dragging and coordinate fields (1 grid unit = 160 px at runtime).</summary>
    public const float SnapStep = 0.5f;

    private static readonly SkillTreePreviewBuilder builder = new SkillTreePreviewBuilder();
    private static SkillTreeView currentView;
    private static bool hooked;
    private static readonly List<SkillTreeOverlay> overlays = new List<SkillTreeOverlay>();

    public static SkillTreeEditSession Session => SkillTreeEditSession.instance;
    public static SkillTreePreviewBuilder Builder => builder;
    public static bool IsActive => Session.active && builder.IsValid;

    /// <summary>Raised after any row/dirty change (overlay repaint hook).</summary>
    public static event Action SessionChanged;
    /// <summary>Raised after the selection changes.</summary>
    public static event Action SelectionChanged;

    static SkillTreeSceneEditor()
    {
        // After a domain reload (script recompile / play mode exit) the DontSave preview objects are
        // gone but the session rows survive in the ScriptableSingleton — rebuild and carry on.
        EditorApplication.delayCall += TryResume;
    }

    // ---------------------------------------------------------------- session lifecycle

    [MenuItem("Bladehold/Skill Tree Scene Editor/Edit Gold Tree")]
    private static void EditGoldTree() => StartSession(AssetDatabase.LoadAssetAtPath<SkillTreeSO>(GoldTreePath));

    [MenuItem("Bladehold/Skill Tree Scene Editor/Edit Reincarnate Tree")]
    private static void EditReincarnateTree() => StartSession(AssetDatabase.LoadAssetAtPath<SkillTreeSO>(ReincarnateTreePath));

    public static void StartSession(SkillTreeSO tree)
    {
        if (tree == null)
        {
            EditorUtility.DisplayDialog("Skill Tree Scene Editor", "Could not load the skill tree asset.", "OK");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Skill Tree Scene Editor", "Exit Play mode first.", "OK");
            return;
        }
        if (Session.active && !EndSession())
        {
            return; // user cancelled the dirty prompt of the previous session
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        SkillTreeView view = FindViewFor(tree);
        if (view == null)
        {
            EditorUtility.DisplayDialog("Skill Tree Scene Editor",
                $"No SkillTreeView in '{ScenePath}' points at '{tree.name}'.", "OK");
            return;
        }

        List<SkillTreeRow> rows = SkillTreeCsvIO.Load(tree, out TextAsset csvAsset);
        if (csvAsset == null)
        {
            EditorUtility.DisplayDialog("Skill Tree Scene Editor", $"'{tree.name}' has no CSV TextAsset assigned.", "OK");
            return;
        }

        Session.Reset(tree, rows);
        SwapViewActiveStates(view);

        currentView = view;
        builder.BuildAll(Session, view);
        Hook();
        FocusSceneView();
        SetOverlaysVisible(true);
        RaiseSessionChanged();
        RaiseSelectionChanged();
    }

    /// <summary>Ends the session (Save/Discard/Cancel prompt when dirty). Returns false if the user cancelled.</summary>
    public static bool EndSession(bool promptIfDirty = true)
    {
        if (Session.active && Session.dirty && promptIfDirty)
        {
            int choice = EditorUtility.DisplayDialogComplex("Unsaved skill tree changes",
                $"The skill tree '{TreeName()}' has unsaved CSV changes.", "Save", "Cancel", "Discard");
            if (choice == 1)
            {
                return false;
            }
            if (choice == 0)
            {
                SaveCsv();
            }
        }

        builder.TearDown();
        RestoreViewActiveStates();
        Unhook();
        Session.active = false;
        Session.dirty = false;
        Session.selectedIds.Clear();
        currentView = null;
        SetOverlaysVisible(false);
        SceneView.RepaintAll();
        return true;
    }

    private static void TryResume()
    {
        if (!Session.active || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        if (SceneManager.GetActiveScene().path != ScenePath || Session.Tree == null)
        {
            Debug.LogWarning("Skill Tree Scene Editor: could not resume the previous session (scene or tree missing). Session closed; unsaved changes were discarded.");
            Session.active = false;
            Session.dirty = false;
            return;
        }

        SkillTreeView view = FindViewFor(Session.Tree);
        if (view == null)
        {
            Session.active = false;
            return;
        }
        currentView = view;
        builder.BuildAll(Session, view);
        Hook();
        SetOverlaysVisible(true);
        SceneView.RepaintAll();
    }

    private static void Hook()
    {
        if (hooked)
        {
            return;
        }
        hooked = true;
        SceneView.duringSceneGui += SkillTreeSceneGuiHandler.OnSceneGUI;
        Undo.undoRedoPerformed += HandleUndoRedo;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
        EditorSceneManager.sceneClosing += HandleSceneClosing;
    }

    private static void Unhook()
    {
        if (!hooked)
        {
            return;
        }
        hooked = false;
        SceneView.duringSceneGui -= SkillTreeSceneGuiHandler.OnSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        EditorSceneManager.sceneClosing -= HandleSceneClosing;
    }

    private static void HandleUndoRedo()
    {
        if (!Session.active)
        {
            return;
        }
        RebuildPreview();
        RaiseSessionChanged();
        RaiseSelectionChanged();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            // DontSave objects must not enter play mode; the session rows survive in the singleton and
            // TryResume rebuilds after play ends (via the domain reload's delayCall, or here).
            builder.TearDown();
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            TryResume();
        }
    }

    private static void HandleBeforeAssemblyReload()
    {
        // Orphaned DontSave objects would linger in the hierarchy through a domain reload.
        builder.TearDown();
    }

    private static void HandleSceneClosing(Scene scene, bool removing)
    {
        if (Session.active && scene.path == ScenePath)
        {
            EndSession();
        }
    }

    private static SkillTreeView FindViewFor(SkillTreeSO tree)
    {
        SkillTreeView[] views = UnityEngine.Object.FindObjectsByType<SkillTreeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SkillTreeView view in views)
        {
            var viewSo = new SerializedObject(view);
            var service = viewSo.FindProperty("serviceBehaviour").objectReferenceValue as MonoBehaviour;
            if (service == null)
            {
                continue;
            }
            var serviceSo = new SerializedObject(service);
            SerializedProperty treeProp = serviceSo.FindProperty("tree");
            if (treeProp != null && treeProp.objectReferenceValue == tree)
            {
                return view;
            }
        }

        // Fallback: match by the scene's known GameObject names.
        string wantedName = tree.name.Contains("Reincarnate") ? "ReincarnateSkillTree" : "GoldSkillTree";
        foreach (SkillTreeView view in views)
        {
            if (view.gameObject.name == wantedName)
            {
                return view;
            }
        }
        return null;
    }

    /// <summary>
    ///     Activates the edited tree's view GameObject and deactivates the other (the Reincarnate view is
    ///     inactive in the saved scene), remembering both states for restore on close. The scene gets
    ///     dirty from this — it is restored on EndSession, so don't save the scene mid-session.
    /// </summary>
    private static void SwapViewActiveStates(SkillTreeView chosen)
    {
        SkillTreeView[] views = UnityEngine.Object.FindObjectsByType<SkillTreeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SkillTreeView view in views)
        {
            bool isChosen = view == chosen;
            if (view.gameObject.name == "GoldSkillTree")
            {
                Session.goldTreeWasActive = view.gameObject.activeSelf;
            }
            else if (view.gameObject.name == "ReincarnateSkillTree")
            {
                Session.reincarnateTreeWasActive = view.gameObject.activeSelf;
            }
            view.gameObject.SetActive(isChosen);
        }
        Session.restoreStatesValid = true;
    }

    private static void RestoreViewActiveStates()
    {
        if (!Session.restoreStatesValid)
        {
            return;
        }
        SkillTreeView[] views = UnityEngine.Object.FindObjectsByType<SkillTreeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SkillTreeView view in views)
        {
            if (view.gameObject.name == "GoldSkillTree")
            {
                view.gameObject.SetActive(Session.goldTreeWasActive);
            }
            else if (view.gameObject.name == "ReincarnateSkillTree")
            {
                view.gameObject.SetActive(Session.reincarnateTreeWasActive);
            }
        }
        Session.restoreStatesValid = false;
    }

    private static void FocusSceneView()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            return;
        }
        sv.in2DMode = true;
        sv.Frame(builder.ContentWorldBounds(), false);
        sv.Focus();
    }

    private static void RebuildPreview()
    {
        if (currentView == null)
        {
            currentView = Session.Tree != null ? FindViewFor(Session.Tree) : null;
        }
        if (currentView != null)
        {
            builder.BuildAll(Session, currentView);
        }
        SceneView.RepaintAll();
    }

    public static string TreeName() => Session.Tree != null ? Session.Tree.name : "(none)";

    // ---------------------------------------------------------------- overlay registration

    public static void RegisterOverlay(SkillTreeOverlay overlay)
    {
        if (!overlays.Contains(overlay))
        {
            overlays.Add(overlay);
        }
        overlay.displayed = Session.active;
    }

    public static void UnregisterOverlay(SkillTreeOverlay overlay)
    {
        overlays.Remove(overlay);
    }

    private static void SetOverlaysVisible(bool visible)
    {
        overlays.RemoveAll(o => o == null);
        foreach (SkillTreeOverlay overlay in overlays)
        {
            overlay.displayed = visible;
        }
    }

    // ---------------------------------------------------------------- selection

    public static void SetSelection(IEnumerable<string> ids)
    {
        Session.selectedIds.Clear();
        Session.selectedIds.AddRange(ids);
        RaiseSelectionChanged();
    }

    public static void ToggleSelected(string id)
    {
        if (!Session.selectedIds.Remove(id))
        {
            Session.selectedIds.Add(id);
        }
        RaiseSelectionChanged();
    }

    public static void ClearSelection() => SetSelection(Array.Empty<string>());

    // ---------------------------------------------------------------- mutations

    /// <summary>The Scene view's '+' button: a new node in a free cell near the source, prereq pre-linked.</summary>
    public static SkillTreeRow AddChildNode(string sourceId)
    {
        SkillTreeRow source = Session.GetRow(sourceId);
        if (source == null)
        {
            return null;
        }

        RecordUndo("Add Skill Node");
        var row = new SkillTreeRow { id = SkillTreeCsvIO.UniqueId(Session.rows, "new_node") };
        (row.x, row.y) = FindFreeCell(source.x, source.y);
        row.prereqs = source.id;
        Session.rows.Add(row);
        Session.dirty = true;

        builder.AddNode(row);
        SetSelection(new[] { row.id });
        RaiseSessionChanged();
        return row;
    }

    /// <summary>
    ///     "New Skill Level": a copy of the node one tier up — trailing number in the id incremented
    ///     (sword_2 → sword_3, else _2 appended), same name/effects/family/icon, prereq = the source.
    /// </summary>
    public static SkillTreeRow DuplicateAsHigherTier(string sourceId)
    {
        SkillTreeRow source = Session.GetRow(sourceId);
        if (source == null)
        {
            return null;
        }

        RecordUndo("New Skill Level");
        SkillTreeRow row = source.Clone();
        Match match = Regex.Match(source.id, @"^(.*?)(\d+)$");
        string candidate = match.Success
            ? match.Groups[1].Value + (int.Parse(match.Groups[2].Value) + 1)
            : source.id + "_2";
        row.id = SkillTreeCsvIO.UniqueId(Session.rows, candidate);
        row.prereqs = source.id;
        (row.x, row.y) = FindFreeCell(source.x, source.y);
        Session.rows.Add(row);
        Session.dirty = true;

        builder.AddNode(row);
        SetSelection(new[] { row.id });
        RaiseSessionChanged();
        return row;
    }

    /// <summary>Adds prereqId to dependentId's prereqs. No-ops on self/duplicate links; refuses cycles.</summary>
    public static bool LinkSkill(string prereqId, string dependentId)
    {
        SkillTreeRow dependent = Session.GetRow(dependentId);
        if (dependent == null || Session.GetRow(prereqId) == null || prereqId == dependentId)
        {
            return false;
        }
        List<string> prereqs = dependent.PrereqList();
        if (prereqs.Contains(prereqId))
        {
            return false;
        }
        if (IsReachable(dependentId, prereqId))
        {
            EditorUtility.DisplayDialog("Skill Tree Scene Editor",
                $"'{dependentId}' is already a prerequisite (directly or indirectly) of '{prereqId}' — linking them the other way would create a cycle, permanently locking both nodes.", "OK");
            return false;
        }

        RecordUndo("Link Skill");
        prereqs.Add(prereqId);
        dependent.SetPrereqList(prereqs);
        Session.dirty = true;
        builder.SyncConnectors(dependent);
        RaiseSessionChanged();
        return true;
    }

    /// <summary>Removes one prereq id from a node (the overlay's × button).</summary>
    public static void UnlinkSkill(string prereqId, string dependentId)
    {
        SkillTreeRow dependent = Session.GetRow(dependentId);
        if (dependent == null)
        {
            return;
        }
        List<string> prereqs = dependent.PrereqList();
        if (!prereqs.Remove(prereqId))
        {
            return;
        }
        RecordUndo("Unlink Skill");
        dependent.SetPrereqList(prereqs);
        Session.dirty = true;
        builder.SyncConnectors(dependent);
        RaiseSessionChanged();
    }

    /// <summary>True when 'targetId' is reachable from 'startId' by walking prereq edges upward.</summary>
    private static bool IsReachable(string startId, string targetId)
    {
        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(startId);
        while (stack.Count > 0)
        {
            string id = stack.Pop();
            if (id == targetId)
            {
                return true;
            }
            if (!visited.Add(id))
            {
                continue;
            }
            SkillTreeRow row = Session.GetRow(id);
            if (row == null)
            {
                continue;
            }
            foreach (string prereq in row.PrereqList())
            {
                stack.Push(prereq);
            }
        }
        return false;
    }

    /// <summary>Records one undo step for the drag about to start (per-frame moves are then silent).</summary>
    public static void BeginDrag()
    {
        RecordUndo("Move Skill Nodes");
    }

    /// <summary>Moves a node to (snapped) grid coordinates. Undo is recorded by <see cref="BeginDrag" />.</summary>
    public static void SetNodeGrid(string id, float x, float y)
    {
        SkillTreeRow row = Session.GetRow(id);
        if (row == null || (Mathf.Approximately(row.x, x) && Mathf.Approximately(row.y, y)))
        {
            return;
        }
        row.x = x;
        row.y = y;
        Session.dirty = true;
        builder.UpdateNodePosition(row);
        RaiseSessionChanged();
    }

    public static float Snap(float value) => Mathf.Round(value / SnapStep) * SnapStep;

    /// <summary>Deletes the selected nodes after an "are you sure?" dialog, cleaning up prereq references.</summary>
    public static void DeleteSelection()
    {
        var ids = new List<string>();
        foreach (SkillTreeRow row in Session.SelectedRows())
        {
            ids.Add(row.id);
        }
        if (ids.Count == 0)
        {
            return;
        }

        string preview = string.Join(", ", ids.GetRange(0, Mathf.Min(ids.Count, 5)));
        if (ids.Count > 5)
        {
            preview += ", …";
        }
        if (!EditorUtility.DisplayDialog("Delete skill nodes",
                $"Delete {ids.Count} node(s) ({preview})?\n\nPrereq references to them in other nodes will be removed; nodes left with no prereqs become root nodes.",
                "Delete", "Cancel"))
        {
            return;
        }

        RecordUndo("Delete Skill Nodes");
        var deleted = new HashSet<string>(ids);
        Session.rows.RemoveAll(row => deleted.Contains(row.id));

        foreach (string id in ids)
        {
            builder.RemoveNode(id);
        }
        foreach (SkillTreeRow row in Session.rows)
        {
            List<string> prereqs = row.PrereqList();
            if (prereqs.RemoveAll(deleted.Contains) > 0)
            {
                row.SetPrereqList(prereqs);
                builder.SyncConnectors(row);
            }
        }

        Session.dirty = true;
        ClearSelection();
        RaiseSessionChanged();
    }

    /// <summary>Multi-edit: assigns one sprite (added to the tree's icons list) to every selected node.</summary>
    public static void SetIconForSelection(Sprite sprite)
    {
        if (Session.selectedIds.Count == 0)
        {
            return;
        }
        if (sprite != null && Session.Tree != null)
        {
            SkillTreeCsvIO.EnsureIconInTree(Session.Tree, sprite);
        }

        RecordUndo("Set Skill Icons");
        foreach (SkillTreeRow row in Session.SelectedRows())
        {
            row.icon = sprite != null ? sprite.name : "";
            builder.UpdateNodeVisual(row);
        }
        Session.dirty = true;
        RaiseSessionChanged();
    }

    /// <summary>Overlay field edits funnel through here: one undo step, preview refreshed live.</summary>
    public static void EditRow(string id, Action<SkillTreeRow> edit)
    {
        SkillTreeRow row = Session.GetRow(id);
        if (row == null)
        {
            return;
        }
        RecordUndo("Edit Skill Node");
        edit(row);
        Session.dirty = true;
        builder.UpdateNodeVisual(row);
        builder.UpdateNodePosition(row);
        builder.SyncConnectors(row);
        RaiseSessionChanged();
    }

    /// <summary>Renames a node id, rewriting every other node's prereq references in the same undo step.</summary>
    public static void RenameNode(string oldId, string newId)
    {
        newId = newId?.Trim() ?? "";
        SkillTreeRow row = Session.GetRow(oldId);
        if (row == null || newId.Length == 0 || newId == oldId)
        {
            return;
        }

        RecordUndo("Rename Skill Node");
        row.id = newId;
        foreach (SkillTreeRow other in Session.rows)
        {
            if (other == row)
            {
                continue;
            }
            List<string> prereqs = other.PrereqList();
            bool changed = false;
            for (int i = 0; i < prereqs.Count; i++)
            {
                if (prereqs[i] == oldId)
                {
                    prereqs[i] = newId;
                    changed = true;
                }
            }
            if (changed)
            {
                other.SetPrereqList(prereqs);
            }
        }

        int index = Session.selectedIds.IndexOf(oldId);
        if (index >= 0)
        {
            Session.selectedIds[index] = newId;
        }
        Session.dirty = true;
        builder.RenameNode(oldId, newId);
        RaiseSessionChanged();
        RaiseSelectionChanged();
    }

    // ---------------------------------------------------------------- save / revert

    public static void SaveCsv()
    {
        if (Session.Tree == null)
        {
            return;
        }

        List<string> problems = Validate();
        if (problems.Count > 0 &&
            !EditorUtility.DisplayDialog("Skill tree validation",
                "Problems found:\n\n- " + string.Join("\n- ", problems) + "\n\nSave anyway?", "Save anyway", "Cancel"))
        {
            return;
        }

        var so = new SerializedObject(Session.Tree);
        var csvAsset = so.FindProperty("csv").objectReferenceValue as TextAsset;
        if (csvAsset == null || !SkillTreeCsvIO.Save(Session.Tree, csvAsset, Session.rows))
        {
            return;
        }
        Session.dirty = false;
        RebuildPreview(); // re-fits treeOffset now that positions are final
        RaiseSessionChanged();
    }

    public static void RevertFromDisk()
    {
        if (Session.dirty &&
            !EditorUtility.DisplayDialog("Revert skill tree", "Discard unsaved CSV changes and reload from disk?", "Discard", "Cancel"))
        {
            return;
        }
        RecordUndo("Revert Skill Tree");
        Session.rows = SkillTreeCsvIO.Load(Session.Tree, out TextAsset _);
        Session.dirty = false;
        ClearSelection();
        RebuildPreview();
        RaiseSessionChanged();
    }

    private static List<string> Validate()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>();
        var all = new HashSet<string>();
        foreach (SkillTreeRow row in Session.rows)
        {
            all.Add(row.id);
        }
        foreach (SkillTreeRow row in Session.rows)
        {
            if (string.IsNullOrEmpty(row.id))
            {
                problems.Add("a node has an empty id");
            }
            else if (!seen.Add(row.id))
            {
                problems.Add($"duplicate id '{row.id}' (the tree will ignore the second occurrence)");
            }
            foreach (string prereq in row.PrereqList())
            {
                if (!all.Contains(prereq))
                {
                    problems.Add($"'{row.id}' requires unknown node '{prereq}'");
                }
            }
        }
        return problems;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    ///     A free 0.5-grid cell near (nearX, nearY): straight below first (the "child" convention), then
    ///     expanding rings, preferring cells below the source. A cell is occupied when any node sits
    ///     within 0.45 units on both axes (100 px nodes on a 160 px grid overlap inside ~0.5 units).
    /// </summary>
    public static (float x, float y) FindFreeCell(float nearX, float nearY)
    {
        if (IsCellFree(nearX, nearY + 1f))
        {
            return (nearX, nearY + 1f);
        }

        for (float radius = 0.5f; radius <= 4f; radius += 0.5f)
        {
            // Ring cells at Chebyshev distance == radius, below-the-source cells first.
            var ring = new List<Vector2>();
            for (float dx = -radius; dx <= radius; dx += 0.5f)
            {
                for (float dy = -radius; dy <= radius; dy += 0.5f)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) >= radius - 0.01f)
                    {
                        ring.Add(new Vector2(dx, dy));
                    }
                }
            }
            ring.Sort((a, b) => b.y.CompareTo(a.y)); // larger dy = further down the tree = preferred
            foreach (Vector2 offset in ring)
            {
                if (IsCellFree(nearX + offset.x, nearY + offset.y))
                {
                    return (nearX + offset.x, nearY + offset.y);
                }
            }
        }

        float maxY = float.MinValue;
        foreach (SkillTreeRow row in Session.rows)
        {
            maxY = Mathf.Max(maxY, row.y);
        }
        return (nearX, (maxY == float.MinValue ? nearY : maxY) + 1f);
    }

    private static bool IsCellFree(float x, float y)
    {
        foreach (SkillTreeRow row in Session.rows)
        {
            if (Mathf.Abs(row.x - x) < 0.45f && Mathf.Abs(row.y - y) < 0.45f)
            {
                return false;
            }
        }
        return true;
    }

    private static void RecordUndo(string label)
    {
        Undo.RecordObject(Session, label);
    }

    private static void RaiseSessionChanged() => SessionChanged?.Invoke();
    private static void RaiseSelectionChanged() => SelectionChanged?.Invoke();
}
