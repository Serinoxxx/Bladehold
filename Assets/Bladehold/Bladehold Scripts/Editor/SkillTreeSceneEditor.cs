using System;
using System.Collections.Generic;
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

    /// <summary>The Scene view's '+' button: a new node in a free cell near the source, two-way linked to it.</summary>
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
        Session.rows.Add(row);
        AddLinkEntry(row, source.id);
        AddLinkEntry(source, row.id);
        Session.dirty = true;

        builder.AddNode(row);
        builder.SyncConnectors(row);
        builder.SyncConnectors(source);
        SetSelection(new[] { row.id });
        RaiseSessionChanged();
        return row;
    }

    /// <summary>
    ///     Links two nodes. A link is symmetric and stored on <b>both</b> nodes' lists, so direction is
    ///     irrelevant (there are no dependent/prereq roles). No-ops on self-links or an existing link.
    /// </summary>
    public static bool LinkSkill(string aId, string bId)
    {
        SkillTreeRow a = Session.GetRow(aId);
        SkillTreeRow b = Session.GetRow(bId);
        if (a == null || b == null || aId == bId)
        {
            return false;
        }
        if (a.PrereqList().Contains(bId) && b.PrereqList().Contains(aId))
        {
            return false;
        }

        RecordUndo("Link Skill");
        AddLinkEntry(a, bId);
        AddLinkEntry(b, aId);
        Session.dirty = true;
        builder.SyncConnectors(a);
        builder.SyncConnectors(b);
        RaiseSessionChanged();
        return true;
    }

    /// <summary>Removes the link between two nodes from <b>both</b> ends (the overlay's × button).</summary>
    public static void UnlinkSkill(string aId, string bId)
    {
        SkillTreeRow a = Session.GetRow(aId);
        SkillTreeRow b = Session.GetRow(bId);
        bool removed = false;
        if (a != null && a.PrereqList().Contains(bId)) removed = true;
        if (b != null && b.PrereqList().Contains(aId)) removed = true;
        if (!removed)
        {
            return;
        }

        RecordUndo("Unlink Skill");
        if (a != null) { RemoveLinkEntry(a, bId); builder.SyncConnectors(a); }
        if (b != null) { RemoveLinkEntry(b, aId); builder.SyncConnectors(b); }
        Session.dirty = true;
        RaiseSessionChanged();
    }

    /// <summary>Sets whether a node is a start-unlocked root (the CSV's 'root' column).</summary>
    public static void SetRoot(string id, bool isRoot)
    {
        SkillTreeRow row = Session.GetRow(id);
        if (row == null || row.isRoot == isRoot)
        {
            return;
        }
        RecordUndo(isRoot ? "Mark Root" : "Unmark Root");
        row.isRoot = isRoot;
        Session.dirty = true;
        RaiseSessionChanged();
    }

    /// <summary>Adds an id to a row's link list if not already present (no undo/dirty of its own).</summary>
    private static void AddLinkEntry(SkillTreeRow row, string linkedId)
    {
        List<string> links = row.PrereqList();
        if (!links.Contains(linkedId))
        {
            links.Add(linkedId);
            row.SetPrereqList(links);
        }
    }

    /// <summary>Removes an id from a row's link list (no undo/dirty of its own).</summary>
    private static void RemoveLinkEntry(SkillTreeRow row, string linkedId)
    {
        List<string> links = row.PrereqList();
        if (links.Remove(linkedId))
        {
            row.SetPrereqList(links);
        }
    }

    /// <summary>True when a node has any prereqs of its own or is a prereq of some other node.</summary>
    public static bool HasAnyLinks(string id)
    {
        SkillTreeRow row = Session.GetRow(id);
        if (row == null)
        {
            return false;
        }
        if (row.PrereqList().Count > 0)
        {
            return true;
        }
        foreach (SkillTreeRow other in Session.rows)
        {
            if (other != row && other.PrereqList().Contains(id))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    ///     Severs every link touching a node in one step (the overlay's "Clear All Links" button): its own
    ///     links, and this node's id removed from any other node's link list. Rootness is unaffected (it's
    ///     the explicit 'root' flag now, not "has no links").
    /// </summary>
    public static void ClearAllLinks(string id)
    {
        SkillTreeRow row = Session.GetRow(id);
        if (row == null)
        {
            return;
        }

        bool hadOwnPrereqs = row.PrereqList().Count > 0;
        var dependents = new List<SkillTreeRow>();
        foreach (SkillTreeRow other in Session.rows)
        {
            if (other != row && other.PrereqList().Contains(id))
            {
                dependents.Add(other);
            }
        }
        if (!hadOwnPrereqs && dependents.Count == 0)
        {
            return;
        }

        RecordUndo("Clear All Links");
        if (hadOwnPrereqs)
        {
            row.SetPrereqList(Array.Empty<string>());
            Session.dirty = true;
            builder.SyncConnectors(row);
        }
        foreach (SkillTreeRow dependent in dependents)
        {
            List<string> prereqs = dependent.PrereqList();
            prereqs.Remove(id);
            dependent.SetPrereqList(prereqs);
            builder.SyncConnectors(dependent);
        }
        Session.dirty = true;
        RaiseSessionChanged();
    }

    /// <summary>
    ///     Batch form of <see cref="ClearAllLinks" /> for the current selection (the multi-select panel's
    ///     "Clear All Links" button), in one undo step: clears every selected node's own prereqs, and
    ///     removes any selected node's id from every other node's prereqs — including links between two
    ///     selected nodes, which only need clearing once.
    /// </summary>
    public static void ClearAllLinksForSelection()
    {
        var ids = new HashSet<string>(Session.selectedIds);
        if (ids.Count == 0)
        {
            return;
        }

        bool anyLinks = false;
        foreach (string id in ids)
        {
            if (HasAnyLinks(id))
            {
                anyLinks = true;
                break;
            }
        }
        if (!anyLinks)
        {
            return;
        }

        RecordUndo("Clear All Links");
        var touched = new HashSet<SkillTreeRow>();
        foreach (string id in ids)
        {
            SkillTreeRow row = Session.GetRow(id);
            if (row != null && row.PrereqList().Count > 0)
            {
                row.SetPrereqList(Array.Empty<string>());
                touched.Add(row);
            }
        }
        foreach (SkillTreeRow other in Session.rows)
        {
            List<string> prereqs = other.PrereqList();
            if (prereqs.RemoveAll(ids.Contains) > 0)
            {
                other.SetPrereqList(prereqs);
                touched.Add(other);
            }
        }

        Session.dirty = true;
        foreach (SkillTreeRow row in touched)
        {
            builder.SyncConnectors(row);
        }
        RaiseSessionChanged();
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
                $"Delete {ids.Count} node(s) ({preview})?\n\nLinks to them from other nodes will be removed. Any node this leaves stranded (no links, not a root) will warn on Save.",
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
        var roots = new List<string>();
        // Undirected adjacency, so reachability holds even if a link was hand-authored on only one end.
        var adjacency = new Dictionary<string, HashSet<string>>();
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
            if (row.isRoot)
            {
                roots.Add(row.id);
            }
            foreach (string prereq in row.PrereqList())
            {
                if (!all.Contains(prereq))
                {
                    problems.Add($"'{row.id}' links to unknown node '{prereq}'");
                    continue;
                }
                AddAdjacency(adjacency, row.id, prereq);
                AddAdjacency(adjacency, prereq, row.id);
            }
        }

        if (roots.Count == 0)
        {
            problems.Add("no root nodes — nothing is unlocked at the start. Mark an entry node with the 'Root' toggle.");
        }
        else
        {
            // Nodes not reachable from any root can never be unlocked in-game.
            var reachable = new HashSet<string>();
            var stack = new Stack<string>(roots);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (!reachable.Add(id)) continue;
                if (adjacency.TryGetValue(id, out HashSet<string> neighbours))
                {
                    foreach (string n in neighbours) stack.Push(n);
                }
            }
            foreach (SkillTreeRow row in Session.rows)
            {
                if (!string.IsNullOrEmpty(row.id) && !reachable.Contains(row.id))
                {
                    problems.Add($"'{row.id}' is not reachable from any root node — it can never be unlocked.");
                }
            }
        }

        return problems;
    }

    private static void AddAdjacency(Dictionary<string, HashSet<string>> adjacency, string from, string to)
    {
        if (!adjacency.TryGetValue(from, out HashSet<string> set))
        {
            set = new HashSet<string>();
            adjacency[from] = set;
        }
        set.Add(to);
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
