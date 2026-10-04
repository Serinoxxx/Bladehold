using UnityEngine;

/// <summary>Tunables for <see cref="SpearmanStance" />, the Spearman's spears-high / spears-forward upper-body pose.</summary>
[CreateAssetMenu(fileName = "SpearmanStanceSO", menuName = "Scriptable Objects/Enemies/Spearman Stance")]
public class SpearmanStanceSO : ScriptableObject
{
    [Tooltip("Lower the spears to brace when the MOUNTED player is within this distance (m) — far enough out that the levelled spears read before a charge arrives.")]
    [Min(0f)]
    public float braceRangeMounted = 14f;

    [Tooltip("Lower the spears when the player ON FOOT is within this distance (m).")]
    [Min(0f)]
    public float braceRangeOnFoot = 4f;

    [Tooltip("Animator layer holding the static upper-body poses (built by Bladehold > Spearman > Build Pose Animations).")]
    public string poseLayerName = "SpearPose";

    [Tooltip("Animator bool: true = spears forward (braced), false = spears high.")]
    public string spearsForwardParam = "SpearsForward";

    [Tooltip("How fast (weight per second) the pose layer fades out on death/knockdown and back in after.")]
    [Min(0.01f)]
    public float layerFadeSpeed = 6f;
}
