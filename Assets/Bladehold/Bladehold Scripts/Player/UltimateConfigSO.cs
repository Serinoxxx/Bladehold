using UnityEngine;

/// <summary>
///     Configurable tunables for player class & weapon ultimates.
/// </summary>
[CreateAssetMenu(fileName = "UltimateConfigSO", menuName = "Scriptable Objects/UltimateConfigSO")]
public class UltimateConfigSO : ScriptableObject
{
    [Tooltip("Unique ID matching the ultimate (e.g. axe_bladestorm_ult, bow_stream_ult, etc.).")]
    public string ultimateId;

    [Tooltip("Display name of the ultimate.")]
    public string displayName;

    [Tooltip("Base duration in seconds of this ultimate before skill tree upgrades.")]
    public float baseDuration = 5f;

    [Tooltip("Optional icon shown for the ultimate.")]
    public Sprite icon;
}
