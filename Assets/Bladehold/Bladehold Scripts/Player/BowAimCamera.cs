using Unity.Cinemachine;
using UnityEngine;

/// <summary>
///     Over-the-shoulder aim camera for the bow. While <see cref="PlayerBow.IsAiming" /> (polled, the
///     <see cref="SwordChargeFeedback" /> pattern) the gameplay <see cref="CinemachineCamera" />'s
///     Third Person Follow boom is blended in close and off to the side, and its lens field of view
///     scales by <see cref="BowSO.aimFieldOfViewPercent" /> (1 = unchanged) — all tuned on
///     <see cref="BowSO" />. Everything goes through Cinemachine's public fields, so the reflection
///     the old Synty-controller version needed is gone. The resting framing is captured once in
///     <c>Start</c>; releasing aim blends everything back to it. This also owns the gameplay
///     camera's resting field of view for <see cref="GameSettingsService" />'s FOV setting (see
///     <see cref="SetRestingFieldOfView" />), since it's already the only component holding this
///     camera's lens reference.
/// </summary>
public class BowAimCamera : MonoBehaviour
{
    [SerializeField] private PlayerBow bow;
    [SerializeField] private BowSO config;
    [Tooltip("The gameplay CinemachineCamera whose Third Person Follow boom and lens the aim blend drives. Auto-found in children.")]
    [SerializeField] private CinemachineCamera aimCamera;

    private CinemachineThirdPersonFollow follow;
    private float baseDistance;
    private float baseHorizontalOffset;
    private float baseFieldOfView;

    /// <summary>0 = normal framing, 1 = full aim framing.</summary>
    private float blend;

    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
        if (aimCamera == null)
        {
            aimCamera = GetComponentInChildren<CinemachineCamera>();
        }
    }

    private void Start()
    {
        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("BowSO is not assigned in the inspector.");
            anyError = true;
        }
        if (aimCamera == null)
        {
            Debug.LogError("CinemachineCamera is not assigned or found in children; the aim camera can't move.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        follow = aimCamera.GetComponent<CinemachineThirdPersonFollow>();
        if (follow == null)
        {
            Debug.LogError("The gameplay CinemachineCamera has no CinemachineThirdPersonFollow; the aim camera can't move.");
            anyError = true;
            return;
        }

        // Capture the authored framing the aim blend returns to.
        baseDistance = follow.CameraDistance;
        baseHorizontalOffset = follow.ShoulderOffset.x;
        baseFieldOfView = aimCamera.Lens.FieldOfView;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        float target = bow.IsAiming ? 1f : 0f;
        float speed = config.aimBlendSeconds > 0f ? Time.deltaTime / config.aimBlendSeconds : 1f;
        float newBlend = Mathf.MoveTowards(blend, target, speed);
        if (Mathf.Approximately(newBlend, blend) && Mathf.Approximately(blend, target))
        {
            return;
        }
        blend = newBlend;

        // Ease the linear blend so the zoom settles softly at both ends.
        float eased = Mathf.SmoothStep(0f, 1f, blend);
        follow.CameraDistance = Mathf.Lerp(baseDistance, config.aimCameraDistance, eased);
        Vector3 shoulder = follow.ShoulderOffset;
        shoulder.x = Mathf.Lerp(baseHorizontalOffset, config.aimCameraHorizontalOffset, eased);
        follow.ShoulderOffset = shoulder;
        aimCamera.Lens.FieldOfView = Mathf.Lerp(baseFieldOfView, baseFieldOfView * config.aimFieldOfViewPercent, eased);
    }

    /// <summary>
    ///     Sets the resting (non-aim) field of view the aim blend returns to, applying it immediately
    ///     if not currently aiming. The intended caller is <see cref="GameSettingsService" />; safe to
    ///     call before <see cref="Start" /> has run, since <c>Start</c> re-reads the lens value it sets
    ///     here as its own captured baseline.
    /// </summary>
    public void SetRestingFieldOfView(float fov)
    {
        if (aimCamera == null)
        {
            return;
        }

        baseFieldOfView = fov;
        if (Mathf.Approximately(blend, 0f))
        {
            aimCamera.Lens.FieldOfView = fov;
        }
    }

    private void OnDisable()
    {
        // E.g. PlayerDeath disabling controls mid-aim: snap the framing back so the death camera
        // isn't stuck zoomed over a shoulder.
        if (anyError || follow == null)
        {
            return;
        }

        blend = 0f;
        follow.CameraDistance = baseDistance;
        Vector3 shoulder = follow.ShoulderOffset;
        shoulder.x = baseHorizontalOffset;
        follow.ShoulderOffset = shoulder;
        aimCamera.Lens.FieldOfView = baseFieldOfView;
    }
}
