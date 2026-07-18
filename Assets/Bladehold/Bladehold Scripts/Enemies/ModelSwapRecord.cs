using UnityEngine;

/// <summary>
///     Data-only marker left on a prefab whose character model was swapped by the Enemy Manager's
///     Model tab: which model prefab was bound onto the rig and which renderer objects it added.
///     Zero runtime logic — it exists so (a) <c>EnemyPrefabGenerator</c> re-runs know not to re-apply
///     a manifest material over the swapped model, and (b) the Model tab can revert the swap
///     (delete the added renderers, re-enable the authored ones, remove this record).
/// </summary>
public class ModelSwapRecord : MonoBehaviour
{
    [Tooltip("The character model prefab whose renderers were bound onto this rig.")]
    public GameObject sourceModelPrefab;

    [Tooltip("Names of the SkinnedMeshRenderer objects the swap added under the rig root.")]
    public string[] swappedRendererNames;
}
