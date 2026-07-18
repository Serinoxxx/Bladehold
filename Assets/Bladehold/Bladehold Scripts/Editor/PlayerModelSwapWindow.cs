using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
///     Edit-time twin of <see cref="PlayerClassController" />'s SwapCharacterModel: rebinds a character
///     model prefab's SkinnedMeshRenderers onto an existing rig's skeleton by bone name and bakes the
///     result into the open prefab (or scene instance), so the authored default model can be replaced
///     without re-wiring anything hanging off the skeleton — weapons under hand bones, the Animator,
///     animation events, and DamageTrigger blade points all stay exactly as wired. The model prefab
///     must share the rig's skeleton (Synty Sidekicks do; bone names must match 1:1).
///     Open via <c>Bladehold &gt; Player Model Swap</c>.
/// </summary>
public class PlayerModelSwapWindow : EditorWindow
{
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private bool deleteOldRenderers;

    [MenuItem("Bladehold/Player Model Swap")]
    private static void Open()
    {
        GetWindow<PlayerModelSwapWindow>("Player Model Swap");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Open the Player prefab in Prefab Mode (or select its scene instance), assign the new character model prefab, and click Swap. " +
            "The new meshes are bound onto the existing skeleton by bone name (the model must share the Synty Sidekick rig), the old " +
            "renderers are disabled (or deleted), and nothing parented to the bones moves. Save the prefab afterwards (Ctrl+S).",
            MessageType.Info);

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (playerRoot == null && stage != null)
        {
            playerRoot = stage.prefabContentsRoot;
        }

        playerRoot = (GameObject)EditorGUILayout.ObjectField("Player root", playerRoot, typeof(GameObject), true);
        modelPrefab = (GameObject)EditorGUILayout.ObjectField("New model prefab", modelPrefab, typeof(GameObject), false);
        deleteOldRenderers = EditorGUILayout.Toggle(
            new GUIContent("Delete old renderers", "Off = the authored model's SkinnedMeshRenderer components are disabled (reversible by hand). On = their GameObjects are deleted outright when they hold nothing but the renderer."),
            deleteOldRenderers);

        Animator animator = playerRoot != null ? playerRoot.GetComponentInChildren<Animator>(true) : null;
        if (playerRoot != null && animator == null)
        {
            EditorGUILayout.HelpBox("No Animator found under the player root — the rig can't be located.", MessageType.Error);
        }

        using (new EditorGUI.DisabledScope(playerRoot == null || modelPrefab == null || animator == null))
        {
            if (GUILayout.Button("Swap Model"))
            {
                Swap(animator, modelPrefab, deleteOldRenderers);
            }
        }
    }

    // The rebind itself lives in ModelSwapUtility (shared with the Enemy Manager's Model tab).
    private static void Swap(Animator animator, GameObject modelPrefab, bool deleteOldRenderers)
    {
        int swapped = ModelSwapUtility.Swap(animator, modelPrefab, deleteOldRenderers, "Swap Player Model");
        if (swapped == 0)
        {
            EditorUtility.DisplayDialog("Player Model Swap",
                $"No SkinnedMeshRenderer in '{modelPrefab.name}' could bind to the player rig — nothing was changed. See the console for which bones were missing.",
                "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        Debug.Log($"PlayerModelSwapWindow: bound {swapped} renderer(s) from '{modelPrefab.name}' onto '{animator.transform.name}'. Save the prefab to keep the change.");
    }
}
