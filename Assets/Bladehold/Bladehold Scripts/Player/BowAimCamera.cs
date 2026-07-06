using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;

/// <summary>
///     Over-the-shoulder aim camera for the bow. While <see cref="PlayerBow.IsAiming" /> (polled, the
///     <see cref="SwordChargeFeedback" /> pattern) the vendored <see cref="SampleCameraController" />'s
///     boom is blended in close and off to the side, and the camera's field of view narrows — all
///     tuned on <see cref="BowSO" />. The controller re-applies its private <c>_cameraDistance</c> /
///     <c>_cameraHorizontalOffset</c> fields to the boom every frame, so we capture the authored
///     values once and write blended values back by cached reflection (the
///     <see cref="PlayerMoveSpeedBinder" /> precedent — no vendored source is edited, and the binder
///     degrades gracefully if Synty ever renames the fields). Releasing aim blends everything back to
///     the authored framing.
/// </summary>
public class BowAimCamera : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private PlayerBow bow;
    [SerializeField] private BowSO config;
    [Tooltip("The vendored Synty camera rig, a child of the player prefab. Auto-found in children.")]
    [SerializeField] private SampleCameraController cameraController;
    [Tooltip("Camera whose field of view narrows while aiming. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;

    private FieldInfo distanceField;
    private FieldInfo horizontalOffsetField;
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
        if (cameraController == null)
        {
            cameraController = GetComponentInChildren<SampleCameraController>();
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
        if (cameraController == null)
        {
            Debug.LogError("SampleCameraController is not assigned or found in children; the aim camera can't move.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
        if (aimCamera == null)
        {
            Debug.LogError("No aim Camera assigned and no Camera.main found; the aim camera can't zoom.");
            anyError = true;
            return;
        }

        System.Type type = cameraController.GetType();
        distanceField = type.GetField("_cameraDistance", FieldFlags);
        horizontalOffsetField = type.GetField("_cameraHorizontalOffset", FieldFlags);

        if (distanceField == null || horizontalOffsetField == null)
        {
            Debug.LogError("BowAimCamera could not find the camera controller's boom fields (_cameraDistance/_cameraHorizontalOffset). Aim zoom disabled.");
            anyError = true;
            return;
        }

        // Capture the authored framing the aim blend returns to.
        baseDistance = (float)distanceField.GetValue(cameraController);
        baseHorizontalOffset = (float)horizontalOffsetField.GetValue(cameraController);
        baseFieldOfView = aimCamera.fieldOfView;
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
        distanceField.SetValue(cameraController, Mathf.Lerp(baseDistance, config.aimCameraDistance, eased));
        horizontalOffsetField.SetValue(cameraController, Mathf.Lerp(baseHorizontalOffset, config.aimCameraHorizontalOffset, eased));
        aimCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, config.aimFieldOfView, eased);
    }

    private void OnDisable()
    {
        // E.g. PlayerDeath disabling controls mid-aim: snap the framing back so the death camera
        // isn't stuck zoomed over a shoulder.
        if (anyError || distanceField == null)
        {
            return;
        }

        blend = 0f;
        distanceField.SetValue(cameraController, baseDistance);
        horizontalOffsetField.SetValue(cameraController, baseHorizontalOffset);
        if (aimCamera != null)
        {
            aimCamera.fieldOfView = baseFieldOfView;
        }
    }
}
