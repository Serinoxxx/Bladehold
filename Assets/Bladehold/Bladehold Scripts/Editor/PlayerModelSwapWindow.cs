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

    /// <summary>
    ///     Moves a bone subtree the rig is missing onto the rig, under its same-named parent bone,
    ///     preserving local transforms — the skeleton proportions are identical, so the grafted
    ///     bones land in exactly their authored pose. Grafts the topmost missing ancestor so a
    ///     whole dangler chain moves as one piece. Registers every grafted transform in the map.
    /// </summary>
    private static bool TryGraftBone(Transform missing, Dictionary<string, Transform> bonesByName)
    {
        Transform top = missing;
        while (top.parent != null && !bonesByName.ContainsKey(top.parent.name))
        {
            top = top.parent;
        }
        if (top.parent == null)
        {
            return false;
        }

        top.SetParent(bonesByName[top.parent.name], false);
        foreach (Transform grafted in top.GetComponentsInChildren<Transform>(true))
        {
            if (!bonesByName.ContainsKey(grafted.name))
            {
                bonesByName[grafted.name] = grafted;
            }
        }
        Undo.RegisterCreatedObjectUndo(top.gameObject, "Swap Player Model");
        return true;
    }

    private static void Swap(Animator animator, GameObject modelPrefab, bool deleteOldRenderers)
    {
        Transform rigRoot = animator.transform;

        var bonesByName = new Dictionary<string, Transform>();
        foreach (Transform bone in rigRoot.GetComponentsInChildren<Transform>(true))
        {
            bonesByName[bone.name] = bone;
        }

        // Captured before the new renderers arrive so only the authored model gets hidden.
        SkinnedMeshRenderer[] authoredRenderers = rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Swap Player Model");
        int undoGroup = Undo.GetCurrentGroup();

        // Plain Instantiate (not PrefabUtility) so the clone has no prefab link and its children can be re-parented freely.
        GameObject instance = Instantiate(modelPrefab);
        int swapped = 0;
        foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] sourceBones = renderer.bones;
            Transform[] mappedBones = new Transform[sourceBones.Length];
            bool allBonesFound = true;
            for (int i = 0; i < sourceBones.Length; i++)
            {
                if (sourceBones[i] == null)
                {
                    Debug.LogWarning($"PlayerModelSwapWindow: renderer '{renderer.name}' on model '{modelPrefab.name}' has a null bone reference — skipped.");
                    allBonesFound = false;
                    break;
                }
                if (!bonesByName.TryGetValue(sourceBones[i].name, out mappedBones[i]))
                {
                    // Outfit-specific bones (cape/armour danglers like abac_dyn_*) don't exist on
                    // the base Sidekick rig — graft the missing subtree onto its same-named parent
                    // bone. Ungrafted, they just ride along with that parent (no Animator input).
                    if (!TryGraftBone(sourceBones[i], bonesByName))
                    {
                        Debug.LogWarning($"PlayerModelSwapWindow: renderer '{renderer.name}' on model '{modelPrefab.name}' references bone '{sourceBones[i].name}', which has no same-named ancestor on the player rig to graft onto — skipped. The model must share the rig's base skeleton (Synty Sidekicks do).");
                        allBonesFound = false;
                        break;
                    }
                    mappedBones[i] = bonesByName[sourceBones[i].name];
                }
            }
            if (!allBonesFound)
            {
                continue;
            }

            renderer.bones = mappedBones;
            if (renderer.rootBone != null && bonesByName.TryGetValue(renderer.rootBone.name, out Transform mappedRoot))
            {
                renderer.rootBone = mappedRoot;
            }

            Transform rendererTransform = renderer.transform;
            rendererTransform.SetParent(rigRoot, false);
            rendererTransform.localPosition = Vector3.zero;
            rendererTransform.localRotation = Quaternion.identity;
            rendererTransform.localScale = Vector3.one;
            renderer.gameObject.SetActive(true);
            Undo.RegisterCreatedObjectUndo(renderer.gameObject, "Swap Player Model");
            swapped++;
        }
        // Whatever's left of the instantiated prefab is just its now-meshless skeleton.
        DestroyImmediate(instance);

        if (swapped == 0)
        {
            EditorUtility.DisplayDialog("Player Model Swap",
                $"No SkinnedMeshRenderer in '{modelPrefab.name}' could bind to the player rig — nothing was changed. See the console for which bones were missing.",
                "OK");
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        foreach (SkinnedMeshRenderer renderer in authoredRenderers)
        {
            // Only delete pure mesh holders — anything carrying other components just gets its renderer disabled.
            if (deleteOldRenderers && renderer.GetComponents<Component>().Length == 2 && renderer.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(renderer.gameObject);
            }
            else
            {
                Undo.RecordObject(renderer, "Swap Player Model");
                renderer.enabled = false;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(rigRoot.gameObject.scene);
        Debug.Log($"PlayerModelSwapWindow: bound {swapped} renderer(s) from '{modelPrefab.name}' onto '{rigRoot.name}'. Save the prefab to keep the change.");
    }
}
