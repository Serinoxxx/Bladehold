using UnityEngine;

/// <summary>
///     Gameplay tunables for the Impulse fling reaction (<see cref="ImpulseReceiver" />): the
///     resistance defaults, launch shaping, landing detection, NavMesh recovery, and the horde-safety
///     cap on simultaneous ragdolls. The player-side numbers (orb duration, power, force bases) live
///     on <see cref="ImpulseSO" /> instead — this asset is the enemy/reaction side.
/// </summary>
[CreateAssetMenu(fileName = "ImpulseConfigSO", menuName = "Scriptable Objects/ImpulseConfigSO")]
public class ImpulseConfigSO : ScriptableObject
{
    [Tooltip("Impulse resistance used when the roster CSV leaves the column blank. Against resistance r: power >= r flings, power >= r-1 knocks down, else nothing extra. 0 = base goblins always fling.")]
    public float defaultResistance = 0f;

    [Header("Launch")]
    [Tooltip("Launch elevation above the horizontal, in degrees. 60 sends enemies properly skyward.")]
    public float launchAngleDegrees = 60f;

    [Tooltip("Random angular velocity (rad/s) added to the pelvis so flung bodies tumble instead of gliding.")]
    public float spinTorque = 4f;

    [Header("Landing detection")]
    [Tooltip("Seconds after launch before landing checks begin (lets the impulse actually lift the body).")]
    public float minAirTime = 0.3f;

    [Tooltip("The body counts as settled once the pelvis stays below this speed (m/s)...")]
    public float settleSpeed = 0.5f;

    [Tooltip("...continuously for this many seconds.")]
    public float settleTime = 0.3f;

    [Tooltip("Hard cap on airborne time; after this the body is treated as settled wherever it is.")]
    public float airborneTimeout = 6f;

    [Header("Recovery")]
    [Tooltip("Max distance from the landed pelvis to search for the NavMesh when re-seating the agent.")]
    public float recoverSampleDistance = 2f;

    [Tooltip("Seconds between NavMesh re-samples while the landing spot is off-mesh (the body may still slide somewhere valid).")]
    public float recoverRetryInterval = 0.5f;

    [Tooltip("Total seconds to keep retrying before giving up and force-killing the stranded enemy.")]
    public float recoverRetryWindow = 3f;

    [Tooltip("Seconds the stand-up animation is given before the AI resumes (match the get-up clip length).")]
    public float getUpSeconds = 2.2f;

    [Tooltip("Seconds a knockdown (the animation-only reaction below the fling threshold) lasts before the AI resumes.")]
    public float knockdownSeconds = 2.5f;

    [Header("Horde safety")]
    [Tooltip("Max ragdolls simulating at once; further flings degrade to knockdowns so the buff never stops working but physics cost stays bounded.")]
    public int maxSimultaneousRagdolls = 12;
}
