using UnityEngine;

[CreateAssetMenu(fileName = "MedusaGazeAuraSO", menuName = "Scriptable Objects/MedusaGazeAuraSO")]
public class MedusaGazeAuraSO : ScriptableObject
{
    [Tooltip("Max distance at which the gaze can catch the player.")]
    public float range = 10f;

    [Tooltip("Half-angle of the gaze cone in degrees, measured from the medusa's forward.")]
    public float halfAngleDegrees = 35f;

    [Tooltip("Fraction of MoveSpeed removed while caught in the gaze (0.5 = half speed). Applied as a Percent modifier on StatType.MoveSpeed.")]
    [Range(0f, 0.95f)] public float slowFraction = 0.5f;

    [Tooltip("Seconds between gaze cone re-tests (~5 Hz).")]
    public float tickInterval = 0.2f;
}
