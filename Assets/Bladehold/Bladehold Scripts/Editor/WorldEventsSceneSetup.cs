using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
///     Puts the WorldEvents prefab into the open scene and switches on only the biome's events, by
///     <see cref="WorldEventSO.id" />. <see cref="DefenseSceneGenerator" /> calls it with the palette's
///     <c>worldEventIds</c>; hand-built battle scenes (Frozen Pass, Ancient Garden, Survivors Scene) are set up
///     the same way through <see cref="Place" /> from a one-off call. Re-running replaces the old instance.
/// </summary>
public static class WorldEventsSceneSetup
{
    public const string PrefabPath = "Assets/Bladehold/Bladehold Prefabs/WorldEvents/WorldEvents.prefab";
    private const string Tag = "[WorldEventsSceneSetup]";

    /// <summary>Places (or replaces) the WorldEvents prefab with only <paramref name="eventIds" /> enabled. Null/empty removes it.</summary>
    public static GameObject Place(string[] eventIds)
    {
        foreach (WorldEventDirector old in Object.FindObjectsByType<WorldEventDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject);
            Object.DestroyImmediate(instanceRoot != null ? instanceRoot : old.gameObject);
        }
        if (eventIds == null || eventIds.Length == 0) return null;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"{Tag} No WorldEvents prefab at {PrefabPath}; no world events placed.");
            return null;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        root.name = "WorldEvents";
        var wanted = new HashSet<string>(eventIds);
        var enabled = new List<WorldEvent>();
        foreach (WorldEvent e in root.GetComponentsInChildren<WorldEvent>(true))
        {
            bool on = e.Config != null && wanted.Contains(e.Config.id);
            e.gameObject.SetActive(on);
            if (on)
            {
                enabled.Add(e);
                wanted.Remove(e.Config.id);
            }
        }
        foreach (string missing in wanted) Debug.LogError($"{Tag} No event with id '{missing}' in the WorldEvents prefab.");

        var director = root.GetComponent<WorldEventDirector>();
        var so = new SerializedObject(director);
        SerializedProperty list = so.FindProperty("events");
        list.arraySize = enabled.Count;
        for (int i = 0; i < enabled.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enabled[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }
}
