using UnityEngine;

/// <summary>
///     Tunables for <see cref="GateFallCinematic" />: the slow-motion cut to the gate exploding that plays
///     before the "gate destroyed" failure screen.
/// </summary>
[CreateAssetMenu(fileName = "GateFallCinematicConfig", menuName = "Scriptable Objects/GateFallCinematicConfigSO")]
public class GateFallCinematicConfigSO : ScriptableObject
{
    [Header("Timing")]
    [Tooltip("Real (unscaled) seconds the cinematic holds on the gate before the failure screen.")]
    [Min(0.5f)] public float duration = 3.5f;
    [Tooltip("Time.timeScale during the cinematic. The gate's explosion runs on scaled time, so this is how slow it plays.")]
    [Range(0.05f, 1f)] public float slowMotionScale = 0.25f;

    [Header("Framing")]
    [Tooltip("Height above the gate's attack point (the doors at ground level) the camera looks at, metres.")]
    public float lookHeight = 4f;
    [Tooltip("Camera distance out in front of the gate (on the attackers' side) at the start of the shot, metres.")]
    [Min(2f)] public float startDistance = 22f;
    [Tooltip("Camera distance at the end of the shot: the slow push-in towards the gate, metres.")]
    [Min(2f)] public float endDistance = 17f;
    [Tooltip("Camera height above the attack point, metres.")]
    public float cameraHeight = 7f;
    [Tooltip("Sideways offset along the wall for a three-quarter angle, metres (0 = dead centre).")]
    public float sideOffset = 6f;
    [Tooltip("Vertical field of view of the cinematic camera, degrees.")]
    [Range(15f, 90f)] public float fieldOfView = 45f;
}
