using UnityEngine;

/// <summary>Tunables for the Ultimate Wheel (<see cref="UltimateWheelUI" />).</summary>
[CreateAssetMenu(fileName = "UltimateWheelConfig", menuName = "Scriptable Objects/UI/Ultimate Wheel Config")]
public class UltimateWheelConfigSO : ScriptableObject
{
    [Tooltip("Time.timeScale while the wheel is open.")]
    [Range(0.01f, 1f)] public float slowTimeScale = 0.1f;

    [Tooltip("Left-stick deflection (0..1) needed to pick a slice on gamepad. Releasing inside it cancels.")]
    [Range(0.1f, 0.95f)] public float stickDeadZone = 0.5f;
}
