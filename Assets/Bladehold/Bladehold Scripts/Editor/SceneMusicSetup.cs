using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
///     Puts a <see cref="SceneMusic" /> into the open scene. <see cref="DefenseSceneGenerator" /> calls it with the
///     palette's cues; hand-built scenes can call it from a one-off script. Re-running replaces the old one,
///     and a null scene cue just removes it.
/// </summary>
public static class SceneMusicSetup
{
    public static SceneMusic Place(MusicCueSO sceneCue, MusicCueSO prepOverride, MusicCueSO battleOverride)
    {
        foreach (SceneMusic old in Object.FindObjectsByType<SceneMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(old.gameObject);
        }
        if (sceneCue == null)
        {
            return null;
        }

        var go = new GameObject("SceneMusic");
        var music = go.AddComponent<SceneMusic>();
        var so = new SerializedObject(music);
        so.FindProperty("sceneCue").objectReferenceValue = sceneCue;
        so.FindProperty("prepOverride").objectReferenceValue = prepOverride;
        so.FindProperty("battleOverride").objectReferenceValue = battleOverride;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        return music;
    }
}
