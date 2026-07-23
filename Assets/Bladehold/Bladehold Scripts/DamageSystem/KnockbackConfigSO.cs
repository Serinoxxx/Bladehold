using UnityEngine;

/// <summary>
///     Gameplay tunables for the Knockback reactions (<see cref="KnockbackReceiver" />): the
///     resistance defaults, launch shaping, landing detection, NavMesh recovery, and sliding. 
///     The horde-safety cap on simultaneous ragdolls is a player-facing setting instead 
///     (<see cref="EnemyRagdoll.MaxActive" />, set by <see cref="GameSettingsService" />).
/// </summary>
[CreateAssetMenu(fileName = "KnockbackConfigSO", menuName = "Scriptable Objects/KnockbackConfigSO")]
public class KnockbackConfigSO : ScriptableObject
{
    [Tooltip("Knockback resistance used when the roster CSV leaves the column blank. Against resistance r: force >= r flings, force >= r-1 knocks down, else slides.")]
    public float defaultResistance = 0f;

    [Tooltip("Seconds the knockback slide lasts (when force < r-1); the push decays linearly to zero over this time.")]
    public float slideDuration = 0.18f;

    [Header("Launch")]
    [Tooltip("Launch elevation above the horizontal, in degrees. 60 sends enemies properly skyward.")]
    public float launchAngleDegrees = 60f;

    [Tooltip("Random angular velocity (rad/s) added to the pelvis so flung bodies tumble instead of gliding.")]
    public float spinTorque = 4f;

    [Tooltip("Extra velocity (m/s) kicked into one random body part on launch so every fling flails differently. 0 = off.")]
    public float randomLimbKick = 3f;

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

    [Header("Escalating Knockback Feedbacks")]
    [Tooltip("Medium visual effect played when an enemy is knocked down (force >= resistance - 1).")]
    public GameObject knockdownVfxPrefab;

    [Tooltip("Medium sound effect played when an enemy is knocked down.")]
    public AudioClip knockdownSfx;

    [Tooltip("Big visual effect played when an enemy is launched into a ragdoll fling (force >= resistance).")]
    public GameObject flyingVfxPrefab;

    [Tooltip("Big sound effect played when an enemy is launched into a ragdoll fling.")]
    public AudioClip flyingSfx;

    [Header("Flying Light Flash")]
    [Tooltip("Whether to spawn a bright flash light when an enemy is flung into a ragdoll.")]
    public bool enableFlyingLightFlash = true;

    [Tooltip("Initial color of the bright light flash on flying ragdoll launch.")]
    public Color flyingLightColor = new Color(1f, 0.95f, 0.8f, 1f);

    [Tooltip("Peak intensity of the bright light flash.")]
    public float flyingLightIntensity = 20f;

    [Tooltip("Range (radius) of the bright point light flash.")]
    public float flyingLightRange = 8f;

    [Tooltip("Duration in seconds over which the light flash rapidly dims to zero intensity.")]
    public float flyingLightDuration = 0.2f;
}

