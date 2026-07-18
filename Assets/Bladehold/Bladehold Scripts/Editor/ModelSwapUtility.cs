using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     The bone-name model rebind extracted from <see cref="PlayerModelSwapWindow" /> so the Enemy
///     Manager can reuse it on enemy prefab variants: binds a character model prefab's
///     SkinnedMeshRenderers onto an existing rig's skeleton by bone name (grafting outfit-specific
///     dangler bones onto their same-named parents), disables/deletes the authored renderers, and
///     leaves everything hanging off the skeleton — weapons, Animator, DamageTriggers — untouched.
///     The model must share the rig's base skeleton (Synty Sidekicks do). Pure hierarchy surgery:
///     callers own persistence (save the scene/prefab) and any user-facing dialogs.
/// </summary>
public static class ModelSwapUtility
{
    /// <summary>
    ///     Rebinds <paramref name="modelPrefab" />'s renderers onto <paramref name="animator" />'s rig.
    ///     Returns how many renderers were bound (0 = nothing changed). Undo-registered under
    ///     <paramref name="undoName" /> (a no-op for LoadPrefabContents roots, which is fine — the
    ///     caller saves or discards those wholesale). <paramref name="swappedNames" />, when given,
    ///     receives the added renderer objects' names (for a <see cref="ModelSwapRecord" />).
    /// </summary>
    public static int Swap(Animator animator, GameObject modelPrefab, bool deleteOldRenderers, string undoName, List<string> swappedNames = null)
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
        Undo.SetCurrentGroupName(undoName);
        int undoGroup = Undo.GetCurrentGroup();

        // Plain Instantiate (not PrefabUtility) so the clone has no prefab link and its children can be re-parented freely.
        GameObject instance = Object.Instantiate(modelPrefab);
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
                    Debug.LogWarning($"ModelSwapUtility: renderer '{renderer.name}' on model '{modelPrefab.name}' has a null bone reference — skipped.");
                    allBonesFound = false;
                    break;
                }
                if (!bonesByName.TryGetValue(sourceBones[i].name, out mappedBones[i]))
                {
                    // Outfit-specific bones (cape/armour danglers like abac_dyn_*) don't exist on
                    // the base Sidekick rig — graft the missing subtree onto its same-named parent
                    // bone. Ungrafted, they just ride along with that parent (no Animator input).
                    if (!TryGraftBone(sourceBones[i], bonesByName, undoName))
                    {
                        Debug.LogWarning($"ModelSwapUtility: renderer '{renderer.name}' on model '{modelPrefab.name}' references bone '{sourceBones[i].name}', which has no same-named ancestor on the rig to graft onto — skipped. The model must share the rig's base skeleton (Synty Sidekicks do).");
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
            Undo.RegisterCreatedObjectUndo(renderer.gameObject, undoName);
            swappedNames?.Add(renderer.gameObject.name);
            swapped++;
        }
        // Whatever's left of the instantiated prefab is just its now-meshless skeleton.
        Object.DestroyImmediate(instance);

        if (swapped == 0)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return 0;
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
                Undo.RecordObject(renderer, undoName);
                renderer.enabled = false;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        return swapped;
    }

    /// <summary>
    ///     Moves a bone subtree the rig is missing onto the rig, under its same-named parent bone,
    ///     preserving local transforms — the skeleton proportions are identical, so the grafted
    ///     bones land in exactly their authored pose. Grafts the topmost missing ancestor so a
    ///     whole dangler chain moves as one piece. Registers every grafted transform in the map.
    /// </summary>
    private static bool TryGraftBone(Transform missing, Dictionary<string, Transform> bonesByName, string undoName)
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
        Undo.RegisterCreatedObjectUndo(top.gameObject, undoName);
        return true;
    }
}
