using UnityEngine;

/// <summary>
///     Best-guess, code-driven footstep timing: the vendored Synty locomotion clips carry no baked
///     footstep animation events, and hand-editing vendored clip assets under <c>Assets/Third
///     Party/</c> is out of this project's convention. Instead of an animation event, this reads the
///     locomotion state's own <see cref="AnimatorStateInfo.normalizedTime" /> each frame and fires
///     <see cref="AnimationEvents.Footstep" /> whenever it crosses one of <see cref="footstepPhases" />
///     — inspector-tunable fractions (0-1) of the gait cycle, one per foot contact. A rough guess to
///     start; nudge the phases in the inspector rather than touching the clips.
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationEvents animationEvents;
    [Tooltip("Animator layer the locomotion state plays on.")]
    [SerializeField] private int locomotionLayer = 0;
    [Tooltip("Normalized-time fractions (0-1) for walk/crouch gait cycle contact (right foot ~0.39, left foot ~0.85).")]
    [SerializeField] private float[] walkPhases = { 0.39f, 0.85f };
    [Tooltip("Normalized-time fractions (0-1) for run gait cycle contact (left foot 0.00, right foot ~0.48).")]
    [SerializeField] private float[] runPhases = { 0.00f, 0.48f };
    [Tooltip("Speed threshold above which runPhases are used instead of walkPhases.")]
    [SerializeField] private float runSpeedThreshold = 3.0f;
    [Tooltip("Animator float parameter read to decide whether the player is moving enough to step.")]
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private float minMoveSpeed = 0.1f;
    [SerializeField] private string groundedParam = "IsGrounded";
    [SerializeField] private string mountedParam = "IsMounted";

    private int moveSpeedHash;
    private int groundedHash;
    private int mountedHash;
    private float previousNormalizedTime;
    private bool hasPrevious = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (animationEvents == null)
        {
            animationEvents = GetComponent<AnimationEvents>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (animationEvents == null)
        {
            Debug.LogError("AnimationEvents component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        moveSpeedHash = Animator.StringToHash(moveSpeedParam);
        groundedHash = Animator.StringToHash(groundedParam);
        mountedHash = Animator.StringToHash(mountedParam);
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        if (animator.GetBool(mountedHash) || !animator.GetBool(groundedHash) || animator.GetFloat(moveSpeedHash) < minMoveSpeed)
        {
            hasPrevious = false;
            return;
        }

        float normalizedTime = animator.GetCurrentAnimatorStateInfo(locomotionLayer).normalizedTime % 1f;

        float currentSpeed = animator.GetFloat(moveSpeedHash);
        float[] activePhases = (currentSpeed >= runSpeedThreshold) ? runPhases : walkPhases;

        if (hasPrevious)
        {
            foreach (float phase in activePhases)
            {
                if (CrossedPhase(previousNormalizedTime, normalizedTime, phase))
                {
                    animationEvents.Footstep();
                }
            }
        }

        previousNormalizedTime = normalizedTime;
        hasPrevious = true;
    }

    /// <summary>True if the cycle passed over <paramref name="phase" /> going from <paramref name="previous" /> to <paramref name="current" />, including the wrap from 1 back to 0.</summary>
    private static bool CrossedPhase(float previous, float current, float phase)
    {
        if (current >= previous)
        {
            return previous < phase && current >= phase;
        }
        return previous < phase || current >= phase;
    }
}
