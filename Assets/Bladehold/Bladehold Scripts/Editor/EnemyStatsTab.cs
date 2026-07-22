using System;
using UnityEditor;
using UnityEngine;

/// <summary>
///     The Enemy Manager's Stats tab: edits the selected roster row's CSV columns. Combat/economy
///     columns are optional overrides (blank = keep the prefab SO value, the roster convention), so
///     every field is a text field rather than a float field — blank and 0 are different values.
///     Edits go through <see cref="EnemyManagerSession" /> (dirty until "Save to CSV" in the window
///     toolbar) and are reported via <see cref="onRowEdited" /> so live zoo instances can be updated.
/// </summary>
public class EnemyStatsTab
{
    /// <summary>Raised after any cell of a row changes. The window routes this to the Enemy Zoo (live re-apply).</summary>
    public Action<EnemyRow> onRowEdited;

    private Vector2 scroll;

    private static readonly (int column, string label, string tooltip)[] OverrideFields =
    {
        (EnemyRow.ColHealth, "Health", "Overrides HealthSO.maxHealth. Blank = prefab default."),
        (EnemyRow.ColDamage, "Damage", "Overrides the attack SO's damage (all attack components). Blank = prefab default."),
        (EnemyRow.ColMinGold, "Min Gold", "Overrides EnemySO.minCoinDrop. Blank = prefab default; filling only one gold column uses it for both."),
        (EnemyRow.ColMaxGold, "Max Gold", "Overrides EnemySO.maxCoinDrop. Blank = prefab default."),
        (EnemyRow.ColSpeed, "Speed", "Overrides AIMovementSO.speed (NavMeshAgent speed). Blank = prefab default."),
        (EnemyRow.ColScale, "Scale", "Multiplier on the prefab's authored transform scale (and NavMeshAgent size). Blank = 1."),
        (EnemyRow.ColKnockbackResistance, "Knockback Resistance", "Overrides the KnockbackReceiver's resistance. Blank = prefab default."),
    };

    private static readonly (int column, string label, string tooltip)[] SchedulingFields =
    {
        (EnemyRow.ColUnlockWave, "Unlock Wave", "First wave (1-based) this type can appear on. Blank = 1."),
        (EnemyRow.ColSpawnChance, "Spawn Chance %", "Per-spawn roll once unlocked, authored as a percent (10 = 10%). Ignored for the fallback first row."),
        (EnemyRow.ColMinSpawn, "Min Spawn", "Per-wave spawn budget at the unlock wave (guarantee + per-wave cap, ramping +1 per wave). Blank/0 = chance-only."),
        (EnemyRow.ColMaxConcurrent, "Max Concurrent", "Max of this type alive at once. Blank/0 = unlimited."),
    };

    public void Draw(EnemyManagerSession session, bool isFallbackRow)
    {
        EnemyRow row = session.SelectedRow;
        if (row == null)
        {
            EditorGUILayout.HelpBox("Select an enemy type on the left.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // The id keys the prefab map, the manifest, and the wave spawner — renames belong in the
        // add-enemy-type flow, not a stats tweak session.
        EditorGUILayout.LabelField("Id", row.Id);
        DrawCell(session, row, EnemyRow.ColDisplayName, "Display Name", "Shown in the Enemy Zoo and debug UIs.");

        if (isFallbackRow)
        {
            EditorGUILayout.HelpBox("This is the fallback type (first CSV row): it spawns whenever no other type wins its roll, so spawn chance and scheduling are ignored.", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stat Overrides (blank = prefab SO default)", EditorStyles.boldLabel);
        foreach ((int column, string label, string tooltip) in OverrideFields)
        {
            DrawCell(session, row, column, label, tooltip);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(isFallbackRow))
        {
            EditorGUILayout.LabelField("Wave Scheduling (saved to CSV; no live effect in the zoo)", EditorStyles.boldLabel);
            foreach ((int column, string label, string tooltip) in SchedulingFields)
            {
                DrawCell(session, row, column, label, tooltip);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(EnemyManagerSession session, EnemyRow row, int column, string label, string tooltip)
    {
        EditorGUI.BeginChangeCheck();
        string value = EditorGUILayout.DelayedTextField(new GUIContent(label, tooltip), row.Get(column));
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Undo.RecordObject(session, $"Edit Enemy {label}");
        row.Set(column, value);
        session.MarkDirty();
        onRowEdited?.Invoke(row);
    }
}
