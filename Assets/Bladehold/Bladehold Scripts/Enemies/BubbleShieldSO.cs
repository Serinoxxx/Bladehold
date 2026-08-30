using UnityEngine;

[CreateAssetMenu(fileName = "BubbleShieldSO", menuName = "Scriptable Objects/BubbleShieldSO")]
public class BubbleShieldSO : ScriptableObject
{
    [Tooltip("Radius of the protective bubble sphere in metres.")]
    public float radius = 2.0f;

    [Tooltip("Material used for the transparent bubble sphere.")]
    public Material bubbleMaterial;

    [Tooltip("Optional audio clip played when the bubble absorbs/blocks an incoming attack.")]
    public AudioClip blockSfx;

    [Tooltip("Optional volume for block SFX.")]
    [Range(0f, 1f)] public float blockSfxVolume = 0.8f;
}
