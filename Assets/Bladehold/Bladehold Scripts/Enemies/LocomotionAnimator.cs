using UnityEngine;

/// <summary>
///     Reusable locomotion-animation driver extracted from Synty's
///     <c>SamplePlayerAnimationController</c>. It computes the goblin locomotion
///     blend-tree parameters (gait, strafe/shuffle blend, start/stop detection and
///     lean) from a per-frame movement snapshot and writes them to an
///     <see cref="Animator" />.
///
///     It is deliberately input-agnostic: the original sample tangled this maths up
///     with the <c>InputReader</c>, the camera controller and a
///     <c>CharacterController</c>. Here, the only thing fed in is the
///     <see cref="LocomotionInput" /> snapshot, so it can be driven from player
///     input, a <c>NavMeshAgent</c> for AI, or anything else. It never moves or
///     rotates the transform itself — it only reads movement and sets animator
///     parameters.
/// </summary>
public class LocomotionAnimator
{
    /// <summary>Locomotion gait, matching the <c>CurrentGait</c> integer in the animator.</summary>
    public enum Gait
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Sprint = 3
    }

    /// <summary>Per-frame movement snapshot the animator parameters are derived from.</summary>
    public struct LocomotionInput
    {
        /// <summary>Horizontal world-space velocity of the character this frame.</summary>
        public Vector3 Velocity;

        /// <summary>The direction the character intends to move (world space, need not be normalised). Zero when idle.</summary>
        public Vector3 MoveDirection;

        /// <summary>Current world-space forward of the character (usually <c>transform.forward</c>).</summary>
        public Vector3 Forward;

        /// <summary>Whether the character is standing on the ground.</summary>
        public bool IsGrounded;

        /// <summary>Whether the character is in a deliberate walk gait (vs. running).</summary>
        public bool IsWalking;

        /// <summary>Whether the character is crouching.</summary>
        public bool IsCrouching;

        /// <summary>
        ///     Whether the character keeps a fixed facing and strafes (driving the strafe blend), rather than
        ///     turning to face its movement direction.
        /// </summary>
        public bool IsStrafing;
    }

    #region Animation Variable Hashes

    // These names must match the Synty goblin locomotion controller
    // (e.g. AC_Polygon / AC_Sidekick).
    private static readonly int _moveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int _currentGaitHash = Animator.StringToHash("CurrentGait");
    private static readonly int _isStoppedHash = Animator.StringToHash("IsStopped");
    private static readonly int _isStartingHash = Animator.StringToHash("IsStarting");
    private static readonly int _isStrafingHash = Animator.StringToHash("IsStrafing");
    private static readonly int _isWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int _isCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _isTurningInPlaceHash = Animator.StringToHash("IsTurningInPlace");
    private static readonly int _strafeDirectionXHash = Animator.StringToHash("StrafeDirectionX");
    private static readonly int _strafeDirectionZHash = Animator.StringToHash("StrafeDirectionZ");
    private static readonly int _shuffleDirectionXHash = Animator.StringToHash("ShuffleDirectionX");
    private static readonly int _shuffleDirectionZHash = Animator.StringToHash("ShuffleDirectionZ");
    private static readonly int _forwardStrafeHash = Animator.StringToHash("ForwardStrafe");
    private static readonly int _leanValueHash = Animator.StringToHash("LeanValue");
    private static readonly int _locomotionStartDirectionHash = Animator.StringToHash("LocomotionStartDirection");
    private static readonly int _inclineAngleHash = Animator.StringToHash("InclineAngle");

    #endregion

    #region Tuning Constants

    private const float _ANIMATION_DAMP_TIME = 5f;
    private const float _STRAFE_DIRECTION_DAMP_TIME = 20f;
    private const float _FORWARD_STRAFE_MIN_THRESHOLD = -55.0f;
    private const float _FORWARD_STRAFE_MAX_THRESHOLD = 125.0f;
    private const float _MAX_LEAN_ROTATION_RATE = 275.0f;
    private const float _LEAN_SMOOTHNESS = 5f;

    #endregion

    #region Config

    private readonly Animator _animator;
    private readonly float _walkSpeed;
    private readonly float _runSpeed;
    private readonly float _sprintSpeed;
    private readonly AnimationCurve _leanCurve;
    private readonly bool _enableLean;

    #endregion

    #region Runtime State

    private Gait _currentGait;
    private float _speed2D;
    private float _strafeDirectionX;
    private float _strafeDirectionZ;
    private float _shuffleDirectionX;
    private float _shuffleDirectionZ;
    private float _forwardStrafe = 1f;
    private float _leanValue;
    private float _newDirectionDifferenceAngle;
    private float _locomotionStartDirection;
    private float _locomotionStartTimer;
    private float _leanDelay;
    private bool _isStopped = true;
    private bool _isStarting;
    private Vector3 _previousRotation;

    // Per-tick scratch values, flattened onto the XZ plane.
    private Vector3 _forwardFlat;
    private Vector3 _moveDirectionFlat;

    #endregion

    /// <summary>The smoothed 2D (horizontal) speed last written to the animator. Useful for gameplay decisions.</summary>
    public float Speed2D => _speed2D;

    /// <summary>The current locomotion gait last written to the animator.</summary>
    public Gait CurrentGait => _currentGait;

    /// <param name="animator">The animator to drive. Must use the Synty goblin locomotion parameter set.</param>
    /// <param name="walkSpeed">Top speed of the walk gait (gait thresholds are derived from these three speeds).</param>
    /// <param name="runSpeed">Default running speed.</param>
    /// <param name="sprintSpeed">Top sprint speed.</param>
    /// <param name="leanCurve">Optional curve mapping normalised speed to lean amount. Pass <c>null</c>/empty to disable lean.</param>
    public LocomotionAnimator(Animator animator, float walkSpeed, float runSpeed, float sprintSpeed, AnimationCurve leanCurve = null)
    {
        _animator = animator;
        _walkSpeed = walkSpeed;
        _runSpeed = runSpeed;
        _sprintSpeed = sprintSpeed;
        _leanCurve = leanCurve;
        _enableLean = leanCurve != null && leanCurve.length > 0;
        _previousRotation = animator != null ? animator.transform.forward : Vector3.forward;
    }

    /// <summary>
    ///     Recomputes all locomotion animation parameters from the given snapshot and writes them to the animator.
    ///     Call once per frame (e.g. from <c>Update</c>).
    /// </summary>
    /// <param name="input">This frame's movement snapshot.</param>
    /// <param name="deltaTime">Frame delta time used for smoothing.</param>
    public void Tick(in LocomotionInput input, float deltaTime)
    {
        if (_animator == null || deltaTime <= 0f)
        {
            return;
        }

        _forwardFlat = new Vector3(input.Forward.x, 0f, input.Forward.z).normalized;
        _moveDirectionFlat = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.z);

        CalculateMoveValues(input);
        CheckIfStarting(input, deltaTime);
        CheckIfStopped();
        CalculateStrafeDirection(input, deltaTime);
        CalculateLean(input, deltaTime);
        WriteToAnimator(input);
    }

    /// <summary>Computes the smoothed 2D speed, the turn-difference angle and the current gait.</summary>
    private void CalculateMoveValues(in LocomotionInput input)
    {
        Vector3 horizontalVelocity = new Vector3(input.Velocity.x, 0f, input.Velocity.z);
        _speed2D = Mathf.Round(horizontalVelocity.magnitude * 1000f) / 1000f;

        _newDirectionDifferenceAngle = _forwardFlat != _moveDirectionFlat
            ? Vector3.SignedAngle(_forwardFlat, _moveDirectionFlat, Vector3.up)
            : 0f;

        CalculateGait();
    }

    /// <summary>
    ///     Determines the locomotion gait (Idle / Walk / Run / Sprint) from the current 2D speed,
    ///     using thresholds halfway between the configured gait speeds.
    /// </summary>
    private void CalculateGait()
    {
        float runThreshold = (_walkSpeed + _runSpeed) / 2f;
        float sprintThreshold = (_runSpeed + _sprintSpeed) / 2f;

        if (_speed2D < 0.01f)
        {
            _currentGait = Gait.Idle;
        }
        else if (_speed2D < runThreshold)
        {
            _currentGait = Gait.Walk;
        }
        else if (_speed2D < sprintThreshold)
        {
            _currentGait = Gait.Run;
        }
        else
        {
            _currentGait = Gait.Sprint;
        }
    }

    /// <summary>Detects the brief "starting to move" window so the animator can play start animations.</summary>
    private void CheckIfStarting(in LocomotionInput input, float deltaTime)
    {
        _locomotionStartTimer = VariableOverrideDelayTimer(_locomotionStartTimer, deltaTime);

        bool isStartingCheck = false;

        if (_locomotionStartTimer <= 0.0f)
        {
            if (_moveDirectionFlat.magnitude > 0.01f && _speed2D < 1f && !input.IsStrafing)
            {
                isStartingCheck = true;
            }

            if (isStartingCheck)
            {
                if (!_isStarting)
                {
                    _locomotionStartDirection = _newDirectionDifferenceAngle;
                    _animator.SetFloat(_locomotionStartDirectionHash, _locomotionStartDirection);
                }

                const float delayTime = 0.2f;
                _leanDelay = delayTime;
                _locomotionStartTimer = delayTime;
            }
        }
        else
        {
            isStartingCheck = true;
        }

        _isStarting = isStartingCheck;
    }

    /// <summary>Detects whether the character has come to rest.</summary>
    private void CheckIfStopped()
    {
        _isStopped = _moveDirectionFlat.magnitude == 0f && _speed2D < 0.5f;
    }

    /// <summary>
    ///     Computes the strafe/shuffle blend values. When not strafing the character is assumed to face its
    ///     movement direction (handled externally), so the blend collapses to "forward".
    /// </summary>
    private void CalculateStrafeDirection(in LocomotionInput input, float deltaTime)
    {
        if (input.IsStrafing)
        {
            Vector3 characterForward = _forwardFlat;
            Vector3 characterRight = Vector3.Cross(Vector3.up, characterForward);
            Vector3 directionForward = _moveDirectionFlat.normalized;

            float strafeAngle = characterForward != directionForward
                ? Vector3.SignedAngle(characterForward, directionForward, Vector3.up)
                : 0f;

            if (_moveDirectionFlat.magnitude > 0.01f)
            {
                // Shuffle values are taken immediately (not lerped) so the blend tree knows the shuffle
                // direction even after movement input stops.
                _shuffleDirectionZ = Vector3.Dot(characterForward, directionForward);
                _shuffleDirectionX = Vector3.Dot(characterRight, directionForward);

                UpdateStrafeDirection(
                    Vector3.Dot(characterForward, directionForward),
                    Vector3.Dot(characterRight, directionForward),
                    deltaTime
                );

                float targetValue =
                    strafeAngle > _FORWARD_STRAFE_MIN_THRESHOLD && strafeAngle < _FORWARD_STRAFE_MAX_THRESHOLD ? 1f : 0f;

                if (Mathf.Abs(_forwardStrafe - targetValue) <= 0.001f)
                {
                    _forwardStrafe = targetValue;
                }
                else
                {
                    float t = Mathf.Clamp01(_STRAFE_DIRECTION_DAMP_TIME * deltaTime);
                    _forwardStrafe = Mathf.SmoothStep(_forwardStrafe, targetValue, t);
                }
            }
            else
            {
                UpdateStrafeDirection(1f, 0f, deltaTime);
            }
        }
        else
        {
            UpdateStrafeDirection(1f, 0f, deltaTime);
            _shuffleDirectionZ = 1f;
            _shuffleDirectionX = 0f;
        }
    }

    /// <summary>Smoothly lerps the strafe blend values toward the given targets.</summary>
    private void UpdateStrafeDirection(float targetZ, float targetX, float deltaTime)
    {
        _strafeDirectionZ = Mathf.Lerp(_strafeDirectionZ, targetZ, _ANIMATION_DAMP_TIME * deltaTime);
        _strafeDirectionX = Mathf.Lerp(_strafeDirectionX, targetX, _ANIMATION_DAMP_TIME * deltaTime);
        _strafeDirectionZ = Mathf.Round(_strafeDirectionZ * 1000f) / 1000f;
        _strafeDirectionX = Mathf.Round(_strafeDirectionX * 1000f) / 1000f;
    }

    /// <summary>
    ///     Computes the lean additive from how fast the character is turning, scaled by the lean curve.
    ///     The sample also drove head/body look from the camera; those are camera-only and intentionally
    ///     left at their defaults here.
    /// </summary>
    private void CalculateLean(in LocomotionInput input, float deltaTime)
    {
        if (!_enableLean)
        {
            _leanValue = 0f;
            _previousRotation = _forwardFlat;
            return;
        }

        _leanDelay = VariableOverrideDelayTimer(_leanDelay, deltaTime);
        bool leanEnabled = _leanDelay == 0.0f && !_isStarting;

        Vector3 currentRotation = _forwardFlat;
        float rotationRate = 0f;

        if (leanEnabled)
        {
            rotationRate = currentRotation != _previousRotation
                ? Vector3.SignedAngle(currentRotation, _previousRotation, Vector3.up) / deltaTime * -1f
                : 0f;
        }

        float initialLeanValue = leanEnabled ? rotationRate : 0f;
        float referenceValue = _sprintSpeed > 0f ? _speed2D / _sprintSpeed : 0f;

        _leanValue = CalculateSmoothedValue(
            _leanValue,
            initialLeanValue,
            _MAX_LEAN_ROTATION_RATE,
            _LEAN_SMOOTHNESS,
            _leanCurve,
            referenceValue,
            deltaTime
        );

        _previousRotation = currentRotation;
    }

    /// <summary>
    ///     Smooths <paramref name="mainVariable" /> toward a curve-scaled target. Mirrors the sample's
    ///     multiplier-style smoothing used for the lean value.
    /// </summary>
    private float CalculateSmoothedValue(
        float mainVariable,
        float newValue,
        float maxRateChange,
        float smoothness,
        AnimationCurve referenceCurve,
        float referenceValue,
        float deltaTime
    )
    {
        float changeVariable = Mathf.Clamp(newValue / maxRateChange, -1.0f, 1.0f);
        changeVariable *= referenceCurve.Evaluate(referenceValue);

        if (!changeVariable.Equals(mainVariable))
        {
            changeVariable = Mathf.Lerp(mainVariable, changeVariable, smoothness * deltaTime);
        }

        return changeVariable;
    }

    /// <summary>Provides a clamped countdown timer, used to delay lean re-activation after a start.</summary>
    private float VariableOverrideDelayTimer(float timeVariable, float deltaTime)
    {
        if (timeVariable > 0.0f)
        {
            timeVariable -= deltaTime;
            timeVariable = Mathf.Clamp(timeVariable, 0.0f, 1.0f);
        }
        else
        {
            timeVariable = 0.0f;
        }

        return timeVariable;
    }

    /// <summary>Pushes the freshly computed values onto the animator.</summary>
    private void WriteToAnimator(in LocomotionInput input)
    {
        _animator.SetFloat(_moveSpeedHash, _speed2D);
        _animator.SetInteger(_currentGaitHash, (int) _currentGait);

        _animator.SetBool(_isStoppedHash, _isStopped);
        _animator.SetBool(_isStartingHash, _isStarting);
        _animator.SetBool(_isStrafingHash, input.IsStrafing);
        _animator.SetBool(_isWalkingHash, input.IsWalking);
        _animator.SetBool(_isCrouchingHash, input.IsCrouching);
        _animator.SetBool(_isGroundedHash, input.IsGrounded);
        _animator.SetBool(_isTurningInPlaceHash, false);

        _animator.SetFloat(_strafeDirectionXHash, _strafeDirectionX);
        _animator.SetFloat(_strafeDirectionZHash, _strafeDirectionZ);
        _animator.SetFloat(_shuffleDirectionXHash, _shuffleDirectionX);
        _animator.SetFloat(_shuffleDirectionZHash, _shuffleDirectionZ);
        _animator.SetFloat(_forwardStrafeHash, _forwardStrafe);

        _animator.SetFloat(_leanValueHash, _leanValue);
        _animator.SetFloat(_locomotionStartDirectionHash, _locomotionStartDirection);

        // Grounded AI on a flat NavMesh has no incline information to feed the additive.
        _animator.SetFloat(_inclineAngleHash, 0f);
    }
}
