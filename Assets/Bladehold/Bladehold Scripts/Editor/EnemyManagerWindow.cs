using UnityEditor;
using UnityEngine;

/// <summary>
///     One-stop enemy tuning window (Bladehold > Enemy Manager): a roster list on the left and
///     per-enemy tabs on the right — Stats (edit the Enemies.csv row, save back explicitly), Model
///     (swap the Sidekick model on the prefab variant), Animation (preview clips / assign overrides),
///     Bake (record clips or ragdoll falls to .anim), and Zoo (drive the Enemy Zoo play-mode
///     gallery). Edits live in <see cref="EnemyManagerSession" /> until "Save to CSV", so they
///     survive play-mode round-trips; the CSV write path mirrors <see cref="SkillTreeCsvIO" />.
/// </summary>
public class EnemyManagerWindow : EditorWindow
{
    private const string DefaultRosterPath = "Assets/Bladehold/Bladehold Scripts/Enemies/EnemyRosterSO.asset";
    private static readonly string[] TabNames = { "Stats", "Model", "Animation", "Zoo" };

    private TextAsset csvAsset;
    private int tab;
    private Vector2 listScroll;

    private EnemyStatsTab statsTab;
    private EnemyZooTab zooTab;
    private EnemyModelTab modelTab;
    private EnemyAnimationTab animationTab;

    private EnemyManagerSession Session => EnemyManagerSession.instance;

    [MenuItem("Bladehold/Enemy Manager")]
    private static void Open()
    {
        GetWindow<EnemyManagerWindow>("Enemy Manager");
    }

    private void OnEnable()
    {
        statsTab = new EnemyStatsTab();
        zooTab = new EnemyZooTab();
        modelTab = new EnemyModelTab();
        animationTab = new EnemyAnimationTab();
        // Pending edits go straight to live zoo instances (play mode only) — the CSV stays untouched
        // until the explicit Save.
        statsTab.onRowEdited = row => EnemyZooTab.FindZoo()?.ApplyLiveDefinition(row.ToDefinition());

        // Resume the persisted session; first open (or a deleted roster) falls back to the shared
        // roster asset the WaveSpawner uses.
        if (Session.Roster == null)
        {
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(DefaultRosterPath);
            if (roster != null)
            {
                LoadRoster(roster, force: true);
            }
        }
        else if (Session.rows.Count == 0)
        {
            LoadRoster(Session.Roster, force: true);
        }
        else
        {
            EnemyCsvIO.Load(Session.Roster, out csvAsset, out _);
        }
    }

    private void LoadRoster(EnemyRosterSO roster, bool force = false)
    {
        if (!force && Session.dirty && Session.Roster != null &&
            !EditorUtility.DisplayDialog("Unsaved changes", "The enemy roster has unsaved CSV changes. Discard them?", "Discard", "Cancel"))
        {
            return;
        }
        var rows = EnemyCsvIO.Load(roster, out csvAsset, out string header);
        Session.Reset(roster, header, rows);
    }

    private void OnGUI()
    {
        DrawRosterPicker();

        if (Session.Roster == null || csvAsset == null)
        {
            EditorGUILayout.HelpBox("Assign an EnemyRosterSO asset (with a CSV assigned) to manage enemy types.", MessageType.Info);
            return;
        }

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawEnemyList();
        DrawTabs();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRosterPicker()
    {
        EditorGUI.BeginChangeCheck();
        var newRoster = (EnemyRosterSO)EditorGUILayout.ObjectField("Enemy Roster", Session.Roster, typeof(EnemyRosterSO), false);
        if (EditorGUI.EndChangeCheck() && newRoster != Session.Roster && newRoster != null)
        {
            LoadRoster(newRoster);
        }

        if (Session.Roster != null && csvAsset == null)
        {
            EditorGUILayout.HelpBox($"'{Session.Roster.name}' has no CSV TextAsset assigned.", MessageType.Warning);
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Add Type", EditorStyles.toolbarButton))
        {
            var row = new EnemyRow();
            row.Set(EnemyRow.ColId, UniqueId("new_enemy"));
            row.Set(EnemyRow.ColDisplayName, "New Enemy");
            Undo.RecordObject(Session, "Add Enemy Type");
            Session.rows.Add(row);
            Session.selectedId = row.Id;
            Session.MarkDirty();
        }

        int selected = SelectedIndex();
        using (new EditorGUI.DisabledScope(selected < 0))
        {
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton))
            {
                EnemyRow copy = Session.rows[selected].Clone();
                copy.Set(EnemyRow.ColId, UniqueId(copy.Id));
                Undo.RecordObject(Session, "Duplicate Enemy Type");
                Session.rows.Insert(selected + 1, copy);
                Session.selectedId = copy.Id;
                Session.MarkDirty();
            }

            // Row 0 is the fallback type (WaveSpawner's unlimited default) — deleting or displacing
            // it would silently re-point the fallback at whatever row came next.
            using (new EditorGUI.DisabledScope(selected == 0))
            {
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton) &&
                    EditorUtility.DisplayDialog("Delete enemy type", $"Delete '{Session.selectedId}' from the roster CSV? The prefab and its SO assets are not touched.", "Delete", "Cancel"))
                {
                    Undo.RecordObject(Session, "Delete Enemy Type");
                    Session.rows.RemoveAt(selected);
                    Session.selectedId = Session.rows.Count > 0 ? Session.rows[Mathf.Min(selected, Session.rows.Count - 1)].Id : "";
                    Session.MarkDirty();
                }
            }
        }

        GUILayout.FlexibleSpace();

        if (Session.dirty)
        {
            GUILayout.Label("unsaved changes", EditorStyles.miniLabel);
        }

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
        {
            if (!Session.dirty || EditorUtility.DisplayDialog("Unsaved changes", "Discard unsaved CSV changes and reload from disk?", "Discard", "Cancel"))
            {
                LoadRoster(Session.Roster, force: true);
            }
        }

        using (new EditorGUI.DisabledScope(!Session.dirty))
        {
            if (GUILayout.Button("Save to CSV", EditorStyles.toolbarButton))
            {
                if (EnemyCsvIO.Save(Session.Roster, csvAsset, Session.headerLine, Session.rows))
                {
                    Session.ClearDirty();
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEnemyList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(180f));
        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        foreach (EnemyRow row in Session.rows)
        {
            bool isSelected = row.Id == Session.selectedId;
            string label = string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName;
            if (GUILayout.Toggle(isSelected, label, "Button") && !isSelected)
            {
                Session.selectedId = row.Id;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void OnDisable()
    {
        // The animation tab owns an edit-mode PlayableGraph and an EditorApplication.update hook.
        animationTab?.Dispose();
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginVertical();
        tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
        tab = GUILayout.Toolbar(tab, TabNames);
        EditorGUILayout.Space();

        switch (tab)
        {
            case 0:
                statsTab.Draw(Session, SelectedIndex() == 0);
                break;
            case 1:
                modelTab.Draw(Session);
                break;
            case 2:
                animationTab.Draw(Session);
                break;
            case 3:
                zooTab.Draw(Session);
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private int SelectedIndex()
    {
        for (int i = 0; i < Session.rows.Count; i++)
        {
            if (Session.rows[i].Id == Session.selectedId)
            {
                return i;
            }
        }
        return -1;
    }

    private string UniqueId(string baseId)
    {
        string candidate = baseId;
        int suffix = 1;
        while (Session.GetRow(candidate) != null)
        {
            candidate = $"{baseId}_{++suffix}";
        }
        return candidate;
    }
}
