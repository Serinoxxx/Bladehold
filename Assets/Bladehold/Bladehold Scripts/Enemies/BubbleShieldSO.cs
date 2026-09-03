using UnityEngine;

[CreateAssetMenu(fileName = "BubbleShieldSO", menuName = "Scriptable Objects/BubbleShieldSO")]
public class BubbleShieldSO : ScriptableObject
{
    [Tooltip("Radius of the protective bubble sphere in metres.")]
    public float radius = 2.0f;

    [Tooltip("Material used for the transparent bubble sphere.")]
    public Material bubbleMaterial;

    [Tooltip("Maximum health pool of the bubble shield before it is destroyed.")]
    public float shieldHealth = 40.0f;

    [Tooltip("Cooldown in seconds before a Bubbler can re-apply a shield to an enemy whose shield was broken.")]
    public float reShieldCooldown = 10.0f;

    [Tooltip("Optional audio clip played when the bubble absorbs/blocks an incoming attack.")]
    public AudioClip blockSfx;

    [Tooltip("Optional audio clip played when the bubble shield pops/breaks.")]
    public AudioClip shieldBreakSfx;

    [Tooltip("Optional prefab spawned when the bubble shield breaks.")]
    public GameObject shieldBreakVfxPrefab;

    [Tooltip("Optional volume for block SFX.")]
    [Range(0f, 1f)] public float blockSfxVolume = 0.8f;
}
