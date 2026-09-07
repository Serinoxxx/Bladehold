using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Manages the player's equipped armour set.
///     Reads the equipped armour set from SaveData on Awake, swaps the visual character model,
///     and applies the armour's stat modifiers. Also supports runtime equipping, swapping
///     character skins and modifiers dynamically, and playing equip feedback effects.
/// </summary>
public class PlayerArmourManager : MonoBehaviour
{
    [Tooltip("The player rig's Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerStats stats;

    [Header("Available Armour Sets")]
    [Tooltip("List of all possible armour sets the player can equip.")]
    public ArmourSetSO[] availableArmourSets;

    [Header("Equip Feedback")]
    [Tooltip("Visual effect spawned at the player's feet when armour is equipped.")]
    [SerializeField] private GameObject equipVfxPrefab;

    [Tooltip("Audio clip played when armour is equipped.")]
    [SerializeField] private AudioClip equipSfx;

    private ArmourSetSO activeArmourSet;
    private readonly List<ArmourSetSO.ArmourStatModifier> appliedModifiers = new List<ArmourSetSO.ArmourStatModifier>();
    private readonly List<GameObject> spawnedRendererObjects = new List<GameObject>();
    private readonly List<Transform> graftedBoneRoots = new List<Transform>();
    private readonly List<SkinnedMeshRenderer> authoredRenderers = new List<SkinnedMeshRenderer>();
    private bool authoredRenderersCaptured = false;

    public ArmourSetSO ActiveArmourSet => activeArmourSet;

    public void ResolveDependencies()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (stats == null)
        {
            stats = GetComponentInChildren<PlayerStats>();
        }
    }

    private void OnValidate()
    {
        ResolveDependencies();
    }

    private void Awake()
    {
        ResolveDependencies();
        CaptureAuthoredRenderers();

        if (availableArmourSets == null || availableArmourSets.Length == 0)
        {
            return;
        }

        SaveData data = SaveSystem.Load();
        string savedArmourId = data != null ? data.equippedArmourSet : "default_armour";
        activeArmourSet = FindArmourSet(savedArmourId);

        // Fallback to first if not found
        if (activeArmourSet == null)
        {
            activeArmourSet = availableArmourSets[0];
            Debug.LogWarning($"PlayerArmourManager: saved armour set '{savedArmourId}' not found; fell back to '{activeArmourSet.id}'.");
        }

        if (activeArmourSet != null && activeArmourSet.characterModelPrefab != null && animator != null)
        {
            SwapCharacterModel(activeArmourSet.characterModelPrefab);
        }
    }

    private void Start()
    {
        ResolveDependencies();

        if (activeArmourSet != null && stats != null)
        {
            ApplyModifiers(activeArmourSet);
        }
    }

    public ArmourSetSO FindArmourSet(string id)
    {
        if (string.IsNullOrEmpty(id) || availableArmourSets == null) return null;
        foreach (var set in availableArmourSets)
        {
            if (set != null && string.Equals(set.id, id, StringComparison.OrdinalIgnoreCase))
                return set;
        }
        return null;
    }

    /// <summary>
    ///     Equips a new armour set at runtime:
    ///     - Reverts previous stat modifiers
    ///     - Destroys prior swapped character meshes
    ///     - Binds the new character mesh onto the shared rig
    ///     - Applies new stat modifiers
    ///     - Saves to SaveData
    ///     - Triggers equip feedback (VFX/audio)
    /// </summary>
    public void EquipArmour(ArmourSetSO newArmourSet, bool playEffect = true)
    {
        if (newArmourSet == null) return;
        ResolveDependencies();
        CaptureAuthoredRenderers();

        // 1. Swap stat modifiers
        RemoveCurrentModifiers();
        ApplyModifiers(newArmourSet);

        // 2. Clean up previously spawned character meshes and grafted bones
        CleanupSpawnedMeshes();

        // 3. Swap visible mesh onto player rig
        if (newArmourSet.characterModelPrefab != null && animator != null)
        {
            SwapCharacterModel(newArmourSet.characterModelPrefab);
        }
        else
        {
            RestoreAuthoredRenderers();
        }

        activeArmourSet = newArmourSet;

        // 4. Persist to SaveData
        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            data.equippedArmourSet = newArmourSet.id;
            SaveSystem.Save(data);
        }

        // 5. Play equip feedback
        if (playEffect)
        {
            PlayEquipFeedback();
        }

        Debug.Log($"[PlayerArmourManager] Equipped armour: {newArmourSet.displayName}!");
    }

    private void ApplyModifiers(ArmourSetSO set)
    {
        if (stats == null || set == null || set.statModifiers == null) return;
        foreach (var mod in set.statModifiers)
        {
            stats.AddModifier(mod.stat, mod.kind, mod.amount);
            appliedModifiers.Add(mod);
        }
    }

    private void RemoveCurrentModifiers()
    {
        if (stats == null || appliedModifiers.Count == 0) return;
        foreach (var mod in appliedModifiers)
        {
            stats.RemoveModifier(mod.stat, mod.kind, mod.amount);
        }
        appliedModifiers.Clear();
    }

    private void CaptureAuthoredRenderers()
    {
        if (authoredRenderersCaptured || animator == null) return;

        Transform rigRoot = animator.transform;
        SkinnedMeshRenderer[] allSmrs = rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in allSmrs)
        {
            // Do not capture weapon models attached to rig
            if (smr.name.StartsWith("Wep_", StringComparison.OrdinalIgnoreCase)) continue;
            authoredRenderers.Add(smr);
        }
        authoredRenderersCaptured = true;
    }

    private void RestoreAuthoredRenderers()
    {
        foreach (var smr in authoredRenderers)
        {
            if (smr != null) smr.enabled = true;
        }
    }

    private void CleanupSpawnedMeshes()
    {
        for (int i = 0; i < spawnedRendererObjects.Count; i++)
        {
            if (spawnedRendererObjects[i] != null)
            {
                SafeDestroy(spawnedRendererObjects[i]);
            }
        }
        spawnedRendererObjects.Clear();

        for (int i = 0; i < graftedBoneRoots.Count; i++)
        {
            if (graftedBoneRoots[i] != null)
            {
                SafeDestroy(graftedBoneRoots[i].gameObject);
            }
        }
        graftedBoneRoots.Clear();
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    /// <summary>
    ///     Swaps the visible character onto the shared rig: every SkinnedMeshRenderer in the armour's
    ///     model prefab is re-bound onto the existing skeleton by bone name and parented under the Animator,
    ///     then the authored character model's renderers are disabled.
    /// </summary>
    private void SwapCharacterModel(GameObject modelPrefab)
    {
        if (animator == null || modelPrefab == null) return;
        Transform rigRoot = animator.transform;

        var bonesByName = new Dictionary<string, Transform>();
        foreach (Transform bone in rigRoot.GetComponentsInChildren<Transform>(true))
        {
            bonesByName[bone.name] = bone;
        }

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
                    allBonesFound = false;
                    break;
                }
                if (!bonesByName.TryGetValue(sourceBones[i].name, out mappedBones[i]))
                {
                    Transform graftedRoot = null;
                    if (!TryGraftBone(sourceBones[i], bonesByName, out graftedRoot))
                    {
                        allBonesFound = false;
                        break;
                    }
                    if (graftedRoot != null && !graftedBoneRoots.Contains(graftedRoot))
                    {
                        graftedBoneRoots.Add(graftedRoot);
                    }
                    mappedBones[i] = bonesByName[sourceBones[i].name];
                }
            }
            if (!allBonesFound)
            {
                Debug.LogWarning($"PlayerArmourManager: renderer '{renderer.name}' on armour model '{modelPrefab.name}' references bones the player rig doesn't have — skipped.");
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
            spawnedRendererObjects.Add(renderer.gameObject);
            swapped++;
        }

        SafeDestroy(instance);

        if (swapped == 0)
        {
            Debug.LogError($"PlayerArmourManager: no SkinnedMeshRenderer in armour model '{modelPrefab.name}' could bind to the player rig.");
            RestoreAuthoredRenderers();
            return;
        }

        // Hide authored character renderers
        foreach (SkinnedMeshRenderer renderer in authoredRenderers)
        {
            if (renderer != null) renderer.enabled = false;
        }
    }

    private static bool TryGraftBone(Transform missing, Dictionary<string, Transform> bonesByName, out Transform topGrafted)
    {
        topGrafted = null;
        Transform top = missing;
        while (top.parent != null && !bonesByName.ContainsKey(top.parent.name))
        {
            top = top.parent;
        }
        if (top.parent == null)
        {
            return false;
        }

        topGrafted = top;
        top.SetParent(bonesByName[top.parent.name], false);
        foreach (Transform grafted in top.GetComponentsInChildren<Transform>(true))
        {
            if (!bonesByName.ContainsKey(grafted.name))
            {
                bonesByName[grafted.name] = grafted;
            }
        }
        return true;
    }

    public void PlayEquipFeedback()
    {
        // 1. VFX
        if (equipVfxPrefab != null)
        {
            GameObject vfx = Instantiate(equipVfxPrefab, transform.position + Vector3.up * 0.1f, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 2. Audio
        if (equipSfx != null)
        {
            AudioSource.PlayClipAtPoint(equipSfx, transform.position, 1.0f);
        }
    }
}
