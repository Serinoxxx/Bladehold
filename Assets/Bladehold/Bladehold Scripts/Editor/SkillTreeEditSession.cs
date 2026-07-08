using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Serialized state of a Scene-view skill tree editing session (see
///     <see cref="SkillTreeSceneEditor" />): the tree being edited, its working copy of the CSV rows,
///     the current selection, and the scene active-states to restore on close. A
///     <see cref="ScriptableSingleton{T}" /> so it survives domain reloads (the session resumes after a
///     script recompile), and a real Object so every mutation can be captured with
///     Undo.RecordObject — Ctrl+Z/Ctrl+Y over adds/moves/edits/deletes just works.
/// </summary>
[FilePath("Library/SkillTreeEditSession.asset", FilePathAttribute.Location.ProjectFolder)]
public class SkillTreeEditSession : ScriptableSingleton<SkillTreeEditSession>
{
    public bool active;
    public string treeGuid = "";
    public List<SkillTreeRow> rows = new List<SkillTreeRow>();
    public List<string> selectedIds = new List<string>();
    public bool dirty;

    // GameObject active-states of the two in-scene tree roots before the session swapped them,
    // restored on EndSession.
    public bool restoreStatesValid;
    public bool goldTreeWasActive;
    public bool reincarnateTreeWasActive;

    private SkillTreeSO cachedTree;
    private string cachedTreeGuid;

    /// <summary>The SkillTreeSO being edited, resolved from <see cref="treeGuid" /> (null when unset/missing).</summary>
    public SkillTreeSO Tree
    {
        get
        {
            if (string.IsNullOrEmpty(treeGuid))
            {
                return null;
            }
            if (cachedTree == null || cachedTreeGuid != treeGuid)
            {
                string path = AssetDatabase.GUIDToAssetPath(treeGuid);
                cachedTree = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SkillTreeSO>(path);
                cachedTreeGuid = treeGuid;
            }
            return cachedTree;
        }
    }

    public SkillTreeRow GetRow(string id)
    {
        foreach (SkillTreeRow row in rows)
        {
            if (row.id == id)
            {
                return row;
            }
        }
        return null;
    }

    public IEnumerable<SkillTreeRow> SelectedRows()
    {
        foreach (string id in selectedIds)
        {
            SkillTreeRow row = GetRow(id);
            if (row != null)
            {
                yield return row;
            }
        }
    }

    public void Reset(SkillTreeSO tree, List<SkillTreeRow> loadedRows)
    {
        active = true;
        treeGuid = tree != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tree)) : "";
        rows = loadedRows;
        selectedIds.Clear();
        dirty = false;
        restoreStatesValid = false;
        cachedTree = tree;
        cachedTreeGuid = treeGuid;
    }
}
