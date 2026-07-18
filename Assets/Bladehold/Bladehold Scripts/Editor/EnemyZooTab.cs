using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
///     The Enemy Manager's Zoo tab: drives the play-mode <see cref="EnemyZoo" /> harness from the
///     window — open the zoo scene, enter play, spawn batches of the selected type, toggle battle
///     mode. Stat edits made in the Stats tab are pushed to live zoo instances automatically (see
///     <see cref="EnemyManagerWindow" />); this tab is the spawn/test surface.
/// </summary>
public class EnemyZooTab
{
    public const string ZooScenePath = "Assets/Bladehold/Bladehold Scenes/Enemy Zoo.unity";

    private int batchCount = 5;

    /// <summary>The live zoo, re-resolved on demand (domain reloads and play transitions invalidate it).</summary>
    public static EnemyZoo FindZoo()
    {
        return Application.isPlaying ? Object.FindFirstObjectByType<EnemyZoo>() : null;
    }

    public void Draw(EnemyManagerSession session)
    {
        EnemyRow row = session.SelectedRow;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("The Enemy Zoo runs in play mode. Open its scene and press Play; live stat edits from the Stats tab apply to spawned enemies immediately.", MessageType.Info);
            if (GUILayout.Button("Open Enemy Zoo Scene & Play", GUILayout.Height(28f)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ZooScenePath);
                    EditorApplication.isPlaying = true;
                }
            }
            return;
        }

        EnemyZoo zoo = FindZoo();
        if (zoo == null || !zoo.IsReady)
        {
            EditorGUILayout.HelpBox(zoo == null
                ? "Play mode is running but no EnemyZoo is in the scene. Open the Enemy Zoo scene and play it."
                : "The EnemyZoo failed to boot (missing roster/prefab map — see the console).", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Enemy Zoo (live)", EditorStyles.boldLabel);

        bool battle = EditorGUILayout.ToggleLeft("Battle Mode (gallery fights the player)", zoo.BattleMode);
        if (battle != zoo.BattleMode)
        {
            zoo.SetBattleMode(battle);
        }

        if (GUILayout.Button("Respawn Gallery", GUILayout.Height(24f)))
        {
            zoo.RespawnGallery();
        }

        EditorGUILayout.Space();
        if (row == null)
        {
            EditorGUILayout.HelpBox("Select an enemy type on the left to spawn it.", MessageType.Info);
            return;
        }

        batchCount = EditorGUILayout.IntSlider("Batch Size", batchCount, 1, 100);
        if (GUILayout.Button($"Spawn {batchCount}× {row.DisplayName}", GUILayout.Height(28f)))
        {
            // Push any pending (unsaved) edits first so the batch spawns with what the window shows.
            zoo.ApplyLiveDefinition(row.ToDefinition());
            if (!zoo.SpawnBatchOf(row.Id, batchCount))
            {
                Debug.LogWarning($"Enemy Manager: '{row.Id}' has no prefab mapping in the zoo; nothing spawned.");
            }
        }
    }
}
