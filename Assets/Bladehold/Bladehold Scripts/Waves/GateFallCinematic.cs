using System;
using System.Collections;
using MoreMountains.Feedbacks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
///     The gate-destroyed cinematic: when a <see cref="Gate" /> falls, <see cref="DeathScreen" /> hands the
///     run-over to <see cref="Play" /> instead of freezing time straight away. It hard-cuts to
///     <see cref="gateCamera" /> framing the gate from the attackers' side, hides the HUD behind the
///     letterbox bars (<see cref="EnemyIntroUI.ShowLetterbox" />), drops time to
///     <see cref="GateFallCinematicConfigSO.slowMotionScale" /> so the gate's explosion plays in slow
///     motion, slowly pushes in, and after <see cref="GateFallCinematicConfigSO.duration" /> real seconds
///     calls back so the death screen freezes time and shows the failure.
///     Lives on the CameraRig prefab (every battle scene has one) beside the Enemy Intro Camera.
/// </summary>
public class GateFallCinematic : MonoBehaviour
{
    public static GateFallCinematic Instance { get; private set; }

    [SerializeField] private GateFallCinematicConfigSO config;

    [Tooltip("The camera that frames the falling gate. Kept disabled at priority 0 until the cinematic.")]
    [SerializeField] private CinemachineCamera gateCamera;

    [Tooltip("Priority given to the gate camera during the cinematic (above the gameplay and intro cameras).")]
    [SerializeField] private int cinematicPriority = 40;

    [Header("Feedback")]
    [Tooltip("Optional: played at the gate as the cinematic starts (slow-motion whoosh, low boom, impulse). Must run on unscaled time. Leave empty for just the gate's own explosion.")]
    [SerializeField] private MMF_Player slowMotionFeedback;

    private bool anyError;
    private bool isPlaying;

    /// <summary>True from the cut to the gate until the failure screen takes over.</summary>
    public bool IsPlaying => isPlaying;

    private void OnValidate()
    {
        if (gateCamera == null)
        {
            gateCamera = GetComponentInChildren<CinemachineCamera>(true);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError($"[GateFallCinematic] {name}: config is not assigned.", this);
            anyError = true;
        }
        if (gateCamera == null)
        {
            Debug.LogError($"[GateFallCinematic] {name}: gateCamera is not assigned.", this);
            anyError = true;
        }

        if (gateCamera != null)
        {
            gateCamera.Priority.Value = 0;
            gateCamera.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    ///     Plays the cinematic on <paramref name="gate" />, then invokes <paramref name="onComplete" /> with time
    ///     still slowed (the caller freezes it). Falls straight through to the callback when unwired.
    /// </summary>
    public void Play(Gate gate, Action onComplete)
    {
        if (anyError || gate == null || isPlaying)
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(PlayRoutine(gate, onComplete));
    }

    private IEnumerator PlayRoutine(Gate gate, Action onComplete)
    {
        isPlaying = true;

        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.SetToggleEnabled(false);
        }

        HideHud();
        if (EnemyIntroUI.Instance != null)
        {
            EnemyIntroUI.Instance.ShowLetterbox(config.duration + 1f);
        }

        // Hard cut: no blend from the gameplay camera, and the brain keeps running while time is slowed.
        CinemachineBrain brain = CinemachineCore.FindPotentialTargetBrain(gateCamera);
        if (brain != null)
        {
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        Vector3 outward = gate.OutwardDirection;
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        Vector3 focus = gate.TargetPosition + Vector3.up * config.lookHeight;

        gateCamera.Lens.FieldOfView = config.fieldOfView;
        PlaceCamera(gate, outward, side, focus, 0f);
        gateCamera.gameObject.SetActive(true);
        gateCamera.PreviousStateIsValid = false;
        gateCamera.Priority.Value = cinematicPriority;

        if (slowMotionFeedback != null)
        {
            slowMotionFeedback.PlayFeedbacks(focus);
        }

        float elapsed = 0f;
        while (elapsed < config.duration)
        {
            // Re-asserted every frame: a hitstop feedback (MMTimeManager) restores normal speed when it ends.
            Time.timeScale = config.slowMotionScale;
            elapsed += Time.unscaledDeltaTime;
            PlaceCamera(gate, outward, side, focus, Mathf.Clamp01(elapsed / config.duration));
            yield return null;
        }

        // The run is over: the camera stays on the ruined gate behind the failure screen.
        onComplete?.Invoke();
    }

    /// <summary>Places the camera on the push-in path (<paramref name="t" /> 0..1, eased out) aimed at the focus.</summary>
    private void PlaceCamera(Gate gate, Vector3 outward, Vector3 side, Vector3 focus, float t)
    {
        float eased = 1f - (1f - t) * (1f - t);
        float distance = Mathf.Lerp(config.startDistance, config.endDistance, eased);
        Vector3 position = gate.TargetPosition + outward * distance + side * config.sideOffset + Vector3.up * config.cameraHeight;
        gateCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
    }

    /// <summary>Hides everything on the HUD canvas except the branch holding the letterbox bars.</summary>
    private static void HideHud()
    {
        EnemyIntroUI intro = EnemyIntroUI.Instance;
        if (intro == null)
        {
            return;
        }
        Canvas canvas = intro.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.rootCanvas.transform : null;
        if (root == null)
        {
            return;
        }

        Transform keep = intro.transform;
        while (keep.parent != null && keep.parent != root)
        {
            keep = keep.parent;
        }
        foreach (Transform child in root)
        {
            if (child != keep)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
