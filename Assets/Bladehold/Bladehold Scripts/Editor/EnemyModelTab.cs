using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     The Enemy Manager's Model tab: swaps a Synty Sidekick character model onto the selected
///     enemy's prefab variant using the shared <see cref="ModelSwapUtility" /> bone-name rebind.
///     The swap is baked into the variant via LoadPrefabContents/SaveAsPrefabAsset (the
///     <c>EnemyPrefabGenerator</c> idiom, so it lands as variant overrides, never base-prefab
///     edits), recorded with a <see cref="ModelSwapRecord" /> for generator idempotence, and fully
///     revertible (authored renderers are disabled, not deleted).
/// </summary>
public class EnemyModelTab
{
    private const string MapAssetPath = "Assets/Bladehold/Bladehold Scripts/Enemies/EnemyPrefabMap.asset";

    private GameObject modelPrefab;

    public void Draw(EnemyManagerSession session)
    {
        EnemyRow row = session.SelectedRow;
        if (row == null)
        {
            EditorGUILayout.HelpBox("Select an enemy type on the left.", MessageType.Info);
            return;
        }

        string prefabPath = FindVariantPath(row.Id, out string mapError);
        if (prefabPath == null)
        {
            EditorGUILayout.HelpBox(mapError, MessageType.Warning);
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUILayout.ObjectField("Enemy Prefab", prefab, typeof(GameObject), false);

        ModelSwapRecord record = prefab != null ? prefab.GetComponentInChildren<ModelSwapRecord>(true) : null;
        if (record != null)
        {
            string source = record.sourceModelPrefab != null ? record.sourceModelPrefab.name : "(missing model prefab)";
            EditorGUILayout.HelpBox($"Current model: swapped to '{source}' ({(record.swappedRendererNames?.Length ?? 0)} renderer(s)). The authored renderers are disabled underneath.", MessageType.None);
            if (GUILayout.Button("Revert to Authored Model", GUILayout.Height(24f)))
            {
                Revert(prefabPath);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.Space();
        }

        EditorGUILayout.HelpBox("Assign a character model prefab that shares the Synty Sidekick skeleton — its meshes are bound onto the enemy rig by bone name, so all animations, weapons, and hitboxes keep working.", MessageType.Info);
        modelPrefab = (GameObject)EditorGUILayout.ObjectField("New Model Prefab", modelPrefab, typeof(GameObject), false);

        using (new EditorGUI.DisabledScope(modelPrefab == null))
        {
            if (GUILayout.Button($"Swap Model on '{row.DisplayName}'", GUILayout.Height(28f)))
            {
                SwapIntoVariant(prefabPath, modelPrefab);
                GUIUtility.ExitGUI();
            }
        }
    }

    /// <summary>The selected enemy's prefab asset path from the shared id → prefab map.</summary>
    public static string FindVariantPath(string id, out string error)
    {
        error = null;
        var map = AssetDatabase.LoadAssetAtPath<EnemyPrefabMapSO>(MapAssetPath);
        if (map == null)
        {
            error = $"Enemy prefab map not found at '{MapAssetPath}'.";
            return null;
        }
        GameObject prefab = map.FindPrefab(id);
        if (prefab == null)
        {
            error = $"'{id}' has no prefab mapping. Generate its prefab first (Bladehold > Generate Enemy Prefabs).";
            return null;
        }
        return AssetDatabase.GetAssetPath(prefab);
    }

    /// <summary>Bakes a model swap into the prefab variant and records it in a <see cref="ModelSwapRecord" />.</summary>
    private static void SwapIntoVariant(string prefabPath, GameObject model)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError($"Enemy Manager: no Animator under '{prefabPath}' — the rig can't be located.");
                return;
            }

            // Reverting an existing swap first keeps the record's renderer list truthful — swapping
            // model B over model A would otherwise disable A's renderers into limbo.
            ModelSwapRecord existing = root.GetComponentInChildren<ModelSwapRecord>(true);
            if (existing != null)
            {
                RevertInContents(root, existing);
            }

            var swappedNames = new List<string>();
            int swapped = ModelSwapUtility.Swap(animator, model, deleteOldRenderers: false, "Swap Enemy Model", swappedNames);
            if (swapped == 0)
            {
                EditorUtility.DisplayDialog("Enemy Model Swap",
                    $"No SkinnedMeshRenderer in '{model.name}' could bind to the enemy rig — nothing was changed. See the console for which bones were missing.",
                    "OK");
                return;
            }

            var record = root.GetComponent<ModelSwapRecord>();
            if (record == null)
            {
                record = root.AddComponent<ModelSwapRecord>();
            }
            record.sourceModelPrefab = model;
            record.swappedRendererNames = swappedNames.ToArray();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"Enemy Manager: bound {swapped} renderer(s) from '{model.name}' onto '{prefabPath}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Undoes a recorded swap on the prefab variant: added renderers deleted, authored renderers re-enabled.</summary>
    private static void Revert(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ModelSwapRecord record = root.GetComponentInChildren<ModelSwapRecord>(true);
            if (record == null)
            {
                return;
            }
            RevertInContents(root, record);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"Enemy Manager: reverted '{prefabPath}' to its authored model.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RevertInContents(GameObject root, ModelSwapRecord record)
    {
        var addedNames = new HashSet<string>(record.swappedRendererNames ?? new string[0]);
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (addedNames.Contains(renderer.gameObject.name))
            {
                Object.DestroyImmediate(renderer.gameObject);
            }
            else
            {
                renderer.enabled = true;
            }
        }
        Object.DestroyImmediate(record);
    }
}
