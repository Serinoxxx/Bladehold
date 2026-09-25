using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
///     One-shot cleanup for plan 08 (legacy code removal). Run it once from
///     <b>Bladehold/Maintenance/Plan 08 Legacy Cleanup</b>, check the report, commit, then delete this file.
///
///     1. Every prefab under Assets/Bladehold: removes nested instances of the legacy prefabs below and
///        every component whose script was deleted (missing script).
///     2. Every scene in Build Settings: the same, and adds a <see cref="TowerPlotManager" /> to scenes
///        that have tower plots but no manager. Each scene is re-saved, which also rewrites the old
///        binary castle scenes as text (the project is Force Text).
///     3. Deletes the legacy prefab assets once nothing instances them.
///     MainMenu also loses its dead character-select, level-select and upgrades screens.
/// </summary>
public static class Plan08LegacyCleanup
{
    private static readonly string[] LegacyPrefabs =
    {
        "Assets/Bladehold/Bladehold Prefabs/Fort/FortDefenseSockets.prefab",
        "Assets/Bladehold/Bladehold Prefabs/Fort/Fort_ArrowSlit.prefab",
        "Assets/Bladehold/Bladehold Prefabs/Fort/Fort_BoilingOil.prefab",
        "Assets/Bladehold/Bladehold Prefabs/Fort/Fort_Spikes.prefab",
        "Assets/Bladehold/Bladehold Prefabs/Managers/FortDefenseManager.prefab",
        "Assets/Bladehold/Bladehold Prefabs/UI/SkillNode.prefab",
        "Assets/Bladehold/Bladehold Prefabs/UI/SkillNode Reincarnate.prefab",
        "Assets/Bladehold/Bladehold Prefabs/UI/SkillNodeConnector.prefab",
        "Assets/Bladehold/Bladehold Prefabs/UI/MetaSkillCard.prefab",
    };

    /// <summary>MainMenu objects for the deleted character select, level select and gold-tree upgrades screens.</summary>
    private static readonly string[] DeadMainMenuObjects =
    {
        "CharacterSelectScreen", "CharacterSelectScreen_OLD", "LevelSelectScreen", "Screen_Upgrades", "Button_Upgrades",
    };

    [MenuItem("Bladehold/Maintenance/Plan 08 Legacy Cleanup")]
    private static void Run()
    {
        if (!EditorUtility.DisplayDialog("Plan 08 Legacy Cleanup",
                "Strips legacy prefab instances and missing scripts from every Bladehold prefab and build scene, re-saves those scenes, then deletes the legacy prefabs.\n\nSave your open scenes first. Continue?",
                "Run", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var legacy = new HashSet<string>(LegacyPrefabs.Where(p => File.Exists(p)));
        var report = new StringBuilder("[Plan08LegacyCleanup]\n");
        int problems = 0;

        // 1. Prefabs (before scenes, so scene instances inherit the fixed assets).
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Bladehold" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !legacy.Contains(p))
            .ToArray();
        for (int i = 0; i < prefabPaths.Length; i++)
        {
            string path = prefabPaths[i];
            EditorUtility.DisplayProgressBar("Plan 08 cleanup: prefabs", path, (float)i / prefabPaths.Length);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int removedInstances = RemoveLegacyInstances(root, legacy, report, ref problems);
                int removedScripts = RemoveMissingScripts(root, report, ref problems);
                if (removedInstances + removedScripts > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine($"  prefab {path}: -{removedInstances} legacy instances, -{removedScripts} missing scripts");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 2. Build scenes.
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = scenes[i].path;
            if (!File.Exists(path)) continue;
            EditorUtility.DisplayProgressBar("Plan 08 cleanup: scenes", path, (float)i / scenes.Length);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int removedInstances = 0, removedScripts = 0;
            if (Path.GetFileNameWithoutExtension(path) == "MainMenu")
            {
                removedInstances += RemoveDeadMainMenuObjects(scene, report, ref problems);
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                removedInstances += RemoveLegacyInstances(root, legacy, report, ref problems);
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                removedScripts += RemoveMissingScripts(root, report, ref problems);
            }

            bool addedManager = false;
            if (Object.FindObjectsByType<TowerPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0
                && Object.FindObjectsByType<TowerPlotManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                new GameObject("TowerPlotManager").AddComponent<TowerPlotManager>();
                addedManager = true;
            }

            // Always save: this is also what rewrites the old binary scenes as text.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine($"  scene {Path.GetFileNameWithoutExtension(path)}: -{removedInstances} legacy instances, -{removedScripts} missing scripts{(addedManager ? ", +TowerPlotManager" : "")}");
        }
        EditorUtility.ClearProgressBar();

        // 3. Legacy prefab assets.
        foreach (string path in legacy)
        {
            report.AppendLine(AssetDatabase.DeleteAsset(path) ? $"  deleted {path}" : $"  !! could not delete {path}");
        }
        AssetDatabase.SaveAssets();

        report.AppendLine(problems == 0 ? "Done, no problems." : $"Done with {problems} problem(s); see the !! lines.");
        if (problems == 0) Debug.Log(report.ToString());
        else Debug.LogError(report.ToString());
    }

    private static int RemoveDeadMainMenuObjects(UnityEngine.SceneManagement.Scene scene, StringBuilder report, ref int problems)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => DeadMainMenuObjects.Contains(t.name)).ToArray())
            {
                if (t == null) continue; // already gone with a destroyed parent
                try
                {
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
                catch (System.Exception e)
                {
                    problems++;
                    report.AppendLine($"  !! couldn't remove MainMenu object '{t.name}': {e.Message}");
                }
            }
        }
        return removed;
    }

    /// <summary>Destroys every outermost instance of a legacy prefab under <paramref name="root" />.</summary>
    private static int RemoveLegacyInstances(GameObject root, HashSet<string> legacy, StringBuilder report, ref int problems)
    {
        var doomed = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (legacy.Contains(source) && !doomed.Any(d => go.transform.IsChildOf(d.transform)))
            {
                doomed.Add(go);
            }
        }

        int removed = 0;
        foreach (GameObject go in doomed)
        {
            try
            {
                Object.DestroyImmediate(go);
                removed++;
            }
            catch (System.Exception e)
            {
                problems++;
                report.AppendLine($"  !! couldn't remove legacy instance '{go.name}': {e.Message}");
            }
        }
        return removed;
    }

    private static int RemoveMissingScripts(GameObject root, StringBuilder report, ref int problems)
    {
        int removed = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0) continue;
            try
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            catch (System.Exception e)
            {
                problems++;
                report.AppendLine($"  !! couldn't strip missing scripts on '{t.name}': {e.Message}");
            }
        }
        return removed;
    }
}
