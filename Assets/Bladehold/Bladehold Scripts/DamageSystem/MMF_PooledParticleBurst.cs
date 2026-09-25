using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Bladehold MMF feedback: takes a particle system from <see cref="ParticlePool" /> at the played
///     position and either plays it or emits a burst into it. The feedback intensity (0-1, passed to
///     <c>PlayFeedbacks(position, intensity)</c>) picks a point along each min/max range, so callers can
///     scale blood with damage or impact speed. With <see cref="UseOwnerRotation" /> the burst takes the
///     MMF_Player's rotation; callers aim it by rotating the player's GameObject just before playing.
/// </summary>
[AddComponentMenu("")]
[System.Serializable]
[FeedbackPath("Bladehold/Pooled Particle Burst")]
[FeedbackHelp("Gets the particle system from Bladehold's ParticlePool at the played position and plays it, or emits a burst into it. Intensity (0-1) picks a point along the emit count, speed and scale ranges.")]
public class MMF_PooledParticleBurst : MMF_Feedback
{
    public static bool FeedbackTypeAuthorized = true;

    public enum Modes { Play, Emit }

#if UNITY_EDITOR
    public override Color FeedbackColor => MMFeedbacksInspectorColors.ParticlesColor;
    public override bool EvaluateRequiresSetup() => ParticlePrefab == null;
    public override string RequiredTargetText => ParticlePrefab != null ? ParticlePrefab.name : "";
    public override string RequiresSetupText => "This feedback requires a ParticlePrefab.";
#endif

    [MMFInspectorGroup("Burst", true, 12, true)]
    [Tooltip("The particle system prefab to take from the pool.")]
    public ParticleSystem ParticlePrefab;
    [Tooltip("Play runs the system as authored; Emit fires Emit Count particles into it right away.")]
    public Modes Mode = Modes.Emit;
    [Tooltip("Particles emitted at intensity 0 (x) and 1 (y). Emit mode only.")]
    public Vector2Int EmitCount = new Vector2Int(3, 40);
    [Tooltip("Multiplier on the prefab's own start speed at intensity 0 (x) and 1 (y).")]
    public Vector2 SpeedMultiplier = Vector2.one;
    [Tooltip("Uniform scale of the instance at intensity 0 (x) and 1 (y). Only affects systems whose scaling mode follows the transform.")]
    public Vector2 Scale = Vector2.one;
    [Tooltip("If true, the instance takes the MMF_Player GameObject's rotation; otherwise it spawns unrotated.")]
    public bool UseOwnerRotation = false;
    [Tooltip("Seconds before the instance goes back to the pool.")]
    public float ReleaseDelay = 3f;

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
    {
        if (!Active || !FeedbackTypeAuthorized || ParticlePrefab == null)
        {
            return;
        }

        float t = Mathf.Clamp01(ComputeIntensity(feedbacksIntensity, position));
        Quaternion rotation = UseOwnerRotation ? Owner.transform.rotation : Quaternion.identity;
        ParticleSystem instance = ParticlePool.Get(ParticlePrefab, position, rotation);
        if (instance == null)
        {
            return;
        }

        instance.transform.localScale = Vector3.one * Mathf.Lerp(Scale.x, Scale.y, t);
        // Read the prefab's speed, not the instance's: pooled instances keep whatever the last caller set.
        ParticleSystem.MainModule main = instance.main;
        main.startSpeedMultiplier = ParticlePrefab.main.startSpeedMultiplier * Mathf.Lerp(SpeedMultiplier.x, SpeedMultiplier.y, t);

        if (Mode == Modes.Emit)
        {
            instance.Emit(Mathf.RoundToInt(Mathf.Lerp(EmitCount.x, EmitCount.y, t)));
        }
        else
        {
            instance.Play(true);
        }

        ParticlePool.Release(ParticlePrefab, instance, ReleaseDelay);
    }
}
