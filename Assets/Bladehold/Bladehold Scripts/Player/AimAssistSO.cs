using UnityEngine;

/// <summary>
///     Tunables for <see cref="ControllerAimAssist" />. The player-facing strength (0–1) and window
///     (degrees) come from settings and scale these maxima.
/// </summary>
[CreateAssetMenu(fileName = "AimAssist", menuName = "Scriptable Objects/Player/Aim Assist")]
public class AimAssistSO : ScriptableObject
{
    [Tooltip("Degrees per second the view is pulled toward the head at full strength, dead centre of the window.")]
    public float maxPullDegreesPerSecond = 40f;
    [Tooltip("Extra pull fraction while the look stick is held (tracking a moving target), on top of the idle pull.")]
    public float stickHeldPullBonus = 0.5f;
    [Tooltip("Fraction the look-stick speed is cut when the crosshair sits on a head at full strength (0 = no slowdown).")]
    [Range(0f, 0.9f)] public float maxSlowdown = 0.5f;
    [Tooltip("Furthest an enemy can be and still be assisted, in metres.")]
    public float range = 45f;
    [Tooltip("Seconds between target-list refreshes (a physics overlap). The best target is re-picked every frame.")]
    public float scanInterval = 0.2f;
    [Tooltip("Layers searched for enemies.")]
    public LayerMask enemyLayers;
    [Tooltip("Layers that block line of sight to a head (environment, walls). Leave Enemy and Player out.")]
    public LayerMask occluderLayers = ~0;
    [Tooltip("Head height above the root, as a fraction of the collider's height, when an enemy has no head marker or head bone.")]
    [Range(0f, 1f)] public float fallbackHeadHeight = 0.9f;
}
