using UnityEngine;

/// <summary>
///     Tunables for <see cref="SwingDirectionSelector" />: how much the camera has to turn just before an
///     attack press to pick the swing side, and the overhead chop's damage reward.
/// </summary>
[CreateAssetMenu(fileName = "SwingDirectionSO", menuName = "Scriptable Objects/Player/Swing Direction")]
public class SwingDirectionSO : ScriptableObject
{
    [Tooltip("Seconds of camera movement before the press that count towards the swing direction.")]
    public float lookWindow = 0.2f;

    [Tooltip("Degrees of yaw within the look window needed to swing from that side (look left = swing from the left).")]
    public float yawThreshold = 3f;

    [Tooltip("Degrees of upward pitch within the look window needed for an overhead chop. Checked before yaw, but only wins when the look is mostly vertical.")]
    public float pitchThreshold = 3f;

    [Tooltip("Base damage multiplier for overhead chops (registered as the OverheadSwingDamageMultiplier stat base). The chop is narrow and hard to aim, so it hits harder.")]
    public float overheadDamageMultiplier = 1.5f;
}
