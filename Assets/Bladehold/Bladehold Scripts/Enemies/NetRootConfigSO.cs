using UnityEngine;

/// <summary>
///     Configurable visual and scaling parameters for the <see cref="NetRootStatus" /> effect.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable Objects/Enemies/Net Root Config", fileName = "NetRootConfigSO")]
public class NetRootConfigSO : ScriptableObject
{
    [Header("Visual Prefab")]
    [Tooltip("Visual prefab instantiated on rooted enemies (e.g. draped net dome and rope ring).")]
    public GameObject captureVisualPrefab;

    [Header("Scaling")]
    [Tooltip("Whether to adapt the visual's scale to match the enemy's collider bounds.")]
    public bool scaleWithTargetCollider = true;

    [Tooltip("Multiplier applied to the calculated collider scale.")]
    public float scaleMultiplier = 1.0f;

    [Tooltip("Minimum overall scale for the capture visual.")]
    public float minScale = 0.8f;
}
