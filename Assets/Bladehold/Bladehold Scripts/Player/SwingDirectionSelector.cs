using System.Collections.Generic;
using UnityEngine;

/// <summary>Which side a melee swing comes from. Values match the Animator's SwingDirection int.</summary>
public enum SwingDirection
{
    /// <summary>Forehand: the weapon travels right to left.</summary>
    Right = 0,
    /// <summary>Backhand: the weapon travels left to right.</summary>
    Left = 1,
    /// <summary>Vertical chop: narrow, so it hits harder (OverheadSwingDamageMultiplier).</summary>
    Overhead = 2,
}

/// <summary>
///     Mount &amp; Blade style directional swings: the way the camera turned in the moment before an
///     attack press picks the swing side (look left → swing from the left, look up → overhead chop).
///     With no clear look, swings alternate left/right so the attack never repeats the same animation.
///     Reads the camera pivot's rotation rather than raw input, so mouse, gamepad, sensitivity and
///     invert settings all work the same. <see cref="PlayerAttack" /> calls <see cref="Latch" /> on
///     each accepted press; the Animator picks the matching windup through the SwingDirection int.
/// </summary>
public class SwingDirectionSelector : MonoBehaviour
{
    [SerializeField] private SwingDirectionSO config;
    [SerializeField] private PlayerStats stats;
    [Tooltip("The player's humanoid Animator (on the Synty rig child), which owns the SwingDirection int.")]
    [SerializeField] private Animator animator;
    [Tooltip("The camera pivot whose yaw/pitch is read. Auto-wired from children.")]
    [SerializeField] private PlayerCameraPivot cameraPivot;

    private static readonly int swingDirectionHash = Animator.StringToHash("SwingDirection");

    private struct LookSample
    {
        public float time;
        public float yaw;
        public float pitch;
    }

    private readonly Queue<LookSample> samples = new Queue<LookSample>();
    private SwingDirection lastSideSwing = SwingDirection.Left;
    private bool anyError;

    /// <summary>Direction of the swing in progress (or the last one).</summary>
    public SwingDirection Current { get; private set; } = SwingDirection.Right;

    /// <summary>
    ///     True when <see cref="Current" /> came from a deliberate camera look; false when no clear look was
    ///     made and the swing just alternated sides. Read-only, for HUD feedback.
    /// </summary>
    public bool CurrentIsIntentional { get; private set; }

    /// <summary>Raised after each <see cref="Latch" /> with the chosen direction (HUD feedback only).</summary>
    public event System.Action<SwingDirection> Latched;

    private void OnValidate()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (cameraPivot == null) cameraPivot = GetComponentInChildren<PlayerCameraPivot>(true);
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("SwingDirectionSelector: config (SwingDirectionSO) is not assigned.", this);
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("SwingDirectionSelector: PlayerStats is not assigned.", this);
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("SwingDirectionSelector: animator is not assigned.", this);
            anyError = true;
        }
        if (cameraPivot == null)
        {
            Debug.LogError("SwingDirectionSelector: cameraPivot is not assigned or found.", this);
            anyError = true;
        }
        if (anyError) return;

        stats.SetBase(StatType.OverheadSwingDamageMultiplier, config.overheadDamageMultiplier);
    }

    private void LateUpdate()
    {
        if (anyError) return;

        Vector3 euler = cameraPivot.transform.eulerAngles;
        samples.Enqueue(new LookSample { time = Time.time, yaw = euler.y, pitch = euler.x });
        // Keep one sample older than the window so the delta always spans the full window.
        while (samples.Count > 2 && Time.time - PeekSecond().time > config.lookWindow)
        {
            samples.Dequeue();
        }
    }

    private LookSample PeekSecond()
    {
        using (var e = samples.GetEnumerator())
        {
            e.MoveNext();
            e.MoveNext();
            return e.Current;
        }
    }

    /// <summary>
    ///     Picks the direction for the swing that is starting now and hands it to the Animator.
    ///     Call once per accepted attack press, before the StartAttack trigger is consumed.
    /// </summary>
    public SwingDirection Latch()
    {
        if (anyError) return Current;

        float yawDelta = 0f;
        float pitchDelta = 0f;
        if (samples.Count > 0)
        {
            LookSample oldest = samples.Peek();
            Vector3 euler = cameraPivot.transform.eulerAngles;
            yawDelta = Mathf.DeltaAngle(oldest.yaw, euler.y);
            pitchDelta = Mathf.DeltaAngle(oldest.pitch, euler.x);
        }

        // Pivot convention: looking up moves pitch negative.
        float lookUp = -pitchDelta;
        if (lookUp >= config.pitchThreshold && lookUp > Mathf.Abs(yawDelta))
        {
            Current = SwingDirection.Overhead;
        }
        else if (yawDelta <= -config.yawThreshold)
        {
            Current = SwingDirection.Left;
        }
        else if (yawDelta >= config.yawThreshold)
        {
            Current = SwingDirection.Right;
        }
        else
        {
            Current = lastSideSwing == SwingDirection.Left ? SwingDirection.Right : SwingDirection.Left;
        }
        CurrentIsIntentional = (lookUp >= config.pitchThreshold && lookUp > Mathf.Abs(yawDelta))
                               || Mathf.Abs(yawDelta) >= config.yawThreshold;

        if (Current != SwingDirection.Overhead)
        {
            lastSideSwing = Current;
        }

        animator.SetInteger(swingDirectionHash, (int)Current);
        Latched?.Invoke(Current);
        return Current;
    }
}
