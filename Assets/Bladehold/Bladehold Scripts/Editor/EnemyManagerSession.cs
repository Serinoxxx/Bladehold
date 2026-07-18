using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Serialized state of an Enemy Manager editing session (see <see cref="EnemyManagerWindow" />):
///     the roster being edited, its working copy of the CSV rows, the current selection, and the
///     dirty flag. A <see cref="ScriptableSingleton{T}" /> (the <see cref="SkillTreeEditSession" />
///     pattern) so unsaved edits survive domain reloads AND play-mode enter/exit — the designer can
///     tweak values, hop into the Enemy Zoo, and come back without losing anything. A real Object, so
///     mutations can be captured with Undo.RecordObject.
/// </summary>
[FilePath("Library/EnemyManagerSession.asset", FilePathAttribute.Location.ProjectFolder)]
public class EnemyManagerSession : ScriptableSingleton<EnemyManagerSession>
{
    public string rosterGuid = "";
    public string headerLine = "";
    public List<EnemyRow> rows = new List<EnemyRow>();
    public string selectedId = "";
    public bool dirty;

    private EnemyRosterSO cachedRoster;
    private string cachedRosterGuid;

    /// <summary>The roster being edited, resolved from <see cref="rosterGuid" /> (null when unset/missing).</summary>
    public EnemyRosterSO Roster
    {
        get
        {
            if (string.IsNullOrEmpty(rosterGuid))
            {
                return null;
            }
            if (cachedRoster == null || cachedRosterGuid != rosterGuid)
            {
                string path = AssetDatabase.GUIDToAssetPath(rosterGuid);
                cachedRoster = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(path);
                cachedRosterGuid = rosterGuid;
            }
            return cachedRoster;
        }
    }

    public EnemyRow GetRow(string id)
    {
        foreach (EnemyRow row in rows)
        {
            if (row.Id == id)
            {
                return row;
            }
        }
        return null;
    }

    public EnemyRow SelectedRow => GetRow(selectedId);

    public void Reset(EnemyRosterSO roster, string header, List<EnemyRow> loadedRows)
    {
        rosterGuid = roster != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(roster)) : "";
        headerLine = header;
        rows = loadedRows;
        dirty = false;
        cachedRoster = roster;
        cachedRosterGuid = rosterGuid;
        if (GetRow(selectedId) == null)
        {
            selectedId = rows.Count > 0 ? rows[0].Id : "";
        }
        Save(true);
    }

    /// <summary>Marks the session dirty and persists it, so even an editor crash loses nothing.</summary>
    public void MarkDirty()
    {
        dirty = true;
        Save(true);
    }

    /// <summary>Clears the dirty flag after a successful CSV save and persists the session.</summary>
    public void ClearDirty()
    {
        dirty = false;
        Save(true);
    }
}
