using System;
using System.Collections;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
///     Generic Scene Singleton controlling cinematic introductions for bosses and special enemies.
///     When triggered:
///     1) Freezes game timescale (Time.timeScale = 0).
///     2) Locks player controls and camera pivot.
///     3) Sets enemy's animator to UnscaledTime and plays the taunt trigger.
///     4) Blends Cinemachine to an Enemy Intro Camera focused on the enemy.
///     5) Displays cinematic letterbox bars and enemy name banner via EnemyIntroUI.
///     6) Holds for 3s in unscaled time, then restores player controls, resets camera priority, and unpauses.
/// </summary>
public class EnemyIntroController : MonoBehaviour
{
    public static EnemyIntroController Instance { get; private set; }

    [Header("Cinemachine & Camera")]
    [Tooltip("The CinemachineCamera used to frame special enemy intros. Raised to high priority during intro.")]
    [SerializeField] private CinemachineCamera introCamera;

    [Tooltip("Optional reference to CinemachineBrain (auto-located if empty).")]
    [SerializeField] private CinemachineBrain cinemachineBrain;

    [Tooltip("Priority value assigned to intro camera during sequence.")]
    [SerializeField] private int introPriority = 30;

    [Header("UI References")]
    [Tooltip("The cinematic letterbox and enemy name overlay UI.")]
    [SerializeField] private EnemyIntroUI introUI;

    [Tooltip("Optional top-center Boss Health Bar to automatically show after intro completes.")]
    [SerializeField] private BossHealthBarUI bossHealthBar;

    [Header("Intro Tuning")]
    [Tooltip("Total duration of the intro sequence in seconds (unscaled time).")]
    [SerializeField] private float defaultIntroDuration = 3.0f;

    [Header("Intro Audio")]
    [Tooltip("Optional fallback roar / intro sound effect played if the special enemy has no specific roar sound configured.")]
    [SerializeField] private AudioClip defaultRoarSound;

    [Tooltip("Volume scale for the fallback roar sound (0 to 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultRoarVolume = 1.0f;

    [Tooltip("Delay in unscaled seconds before playing the fallback roar sound.")]
    [SerializeField] private float defaultRoarDelay = 0f;

    [Tooltip("Optional extra components to disable on the player during intro.")]
    [SerializeField] private MonoBehaviour[] extraComponentsToDisable;

    private readonly List<MonoBehaviour> disabledPlayerComponents = new List<MonoBehaviour>();
    private bool isIntroActive = false;
    private Coroutine activeIntroRoutine;

    public bool IsIntroActive => isIntroActive;

    public event Action<SpecialEnemyIntro> OnIntroStarted;
    public event Action<SpecialEnemyIntro> OnIntroCompleted;

    [SerializeField] GameObject[] objectsToHideDuringIntro;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (cinemachineBrain == null)
        {
            cinemachineBrain = FindFirstObjectByType<CinemachineBrain>();
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.IgnoreTimeScale = true;
        }

        if (introUI == null)
        {
            introUI = EnemyIntroUI.Instance ?? FindFirstObjectByType<EnemyIntroUI>();
        }

        if (introCamera == null)
        {
            CinemachineCamera[] allCams = Resources.FindObjectsOfTypeAll<CinemachineCamera>();
            foreach (CinemachineCamera cam in allCams)
            {
                if (cam.gameObject.name == "Enemy Intro Camera" && cam.gameObject.scene.isLoaded)
                {
                    introCamera = cam;
                    break;
                }
            }
        }

        if (introCamera != null)
        {
            introCamera.Priority.Value = 0;
            introCamera.enabled = false;
            introCamera.gameObject.SetActive(false);
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
    ///     Executes a cinematic introduction sequence for the given special enemy.
    /// </summary>
    public void PlayIntro(SpecialEnemyIntro enemy, Action onComplete = null)
    {
        if (enemy == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (activeIntroRoutine != null)
        {
            StopCoroutine(activeIntroRoutine);
        }

        activeIntroRoutine = StartCoroutine(IntroSequenceRoutine(enemy, onComplete));
    }

    private IEnumerator IntroSequenceRoutine(SpecialEnemyIntro enemy, Action onComplete)
    {
        isIntroActive = true;
        OnIntroStarted?.Invoke(enemy);

        // 1. Pause gameplay
        Time.timeScale = 0f;

        if (introUI == null)
        {
            introUI = EnemyIntroUI.Instance ?? FindFirstObjectByType<EnemyIntroUI>(FindObjectsInactive.Include);
        }

        // 2. Hide HUD elements during intro cutscene while preserving EnemyIntroUI
        //List<GameObject> hiddenObjects = new List<GameObject>();
        //List<Canvas> hiddenCanvases = new List<Canvas>();
        //Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        //foreach (Canvas c in allCanvases)
        //{
        //    if (c == null || !c.enabled) continue;

        //    if (introUI != null && introUI.transform.IsChildOf(c.transform))
        //    {
        //        // This Canvas hosts our introUI. Hide sibling gameplay HUD branches so they disappear,
        //        // while keeping the path to EnemyIntroUI active.
        //        HideSiblingsAlongPath(c.transform, introUI.transform, hiddenObjects);
        //    }
        //    else if (c.gameObject.name.Contains("HUD"))
        //    {
        //        c.enabled = false;
        //        hiddenCanvases.Add(c);
        //    }
        //}
        foreach (GameObject go in objectsToHideDuringIntro)
        {
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }
        }

        // 3. Lock player controls & disable pause toggle
        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.SetToggleEnabled(false);
        }
        //LockPlayerControls();

        // 4. Ensure CinemachineBrain ignores timescale
        if (cinemachineBrain != null)
        {
            cinemachineBrain.IgnoreTimeScale = true;
        }

        // 5. Set enemy animator to UnscaledTime and trigger taunt
        Animator enemyAnimator = enemy.Animator;
        AnimatorUpdateMode previousUpdateMode = AnimatorUpdateMode.Normal;
        if (enemyAnimator != null)
        {
            previousUpdateMode = enemyAnimator.updateMode;
            enemyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (!string.IsNullOrEmpty(enemy.TauntTriggerName))
            {
                enemyAnimator.SetTrigger(enemy.TauntTriggerName);
            }
        }

        // 5b. Play intro roar sound (from enemy or fallback)
        if (enemy != null && enemy.RoarSound != null)
        {
            enemy.PlayRoarSound();
        }
        else if (defaultRoarSound != null && enemy != null)
        {
            StartCoroutine(PlayDefaultRoarRoutine(enemy.transform.position));
        }

        // 6. Focus intro camera, reset tracking state, and raise priority
        if (introCamera != null)
        {
            introCamera.gameObject.SetActive(true);
            introCamera.enabled = true;
            introCamera.Follow = enemy.transform;
            introCamera.LookAt = enemy.CameraFocusTransform;
            introCamera.PreviousStateIsValid = false;
            introCamera.Priority.Value = introPriority;
        }

        // 7. Trigger Letterbox & Enemy Name UI
        if (introUI != null)
        {
            introUI.ShowIntro(enemy.EnemyDisplayName, defaultIntroDuration);
        }

        // 8. Hold for duration in unscaled time
        float elapsed = 0f;
        while (elapsed < defaultIntroDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 9. Transition back & restore state
        if (introCamera != null)
        {
            introCamera.Priority.Value = 0;
            introCamera.Follow = null;
            introCamera.LookAt = null;
            introCamera.PreviousStateIsValid = false;
            introCamera.enabled = false;
            introCamera.gameObject.SetActive(false);
        }

        if (enemyAnimator != null)
        {
            enemyAnimator.updateMode = previousUpdateMode;
        }

        UnlockPlayerControls();

        // Restore HUD Canvases & Objects
        foreach (GameObject go in objectsToHideDuringIntro)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        //foreach (Canvas c in hiddenCanvases)
        //{
        //    if (c != null)
        //    {
        //        c.enabled = true;
        //    }
        //}

        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.SetToggleEnabled(true);
        }

        // Resume game timescale
        Time.timeScale = GameSettingsService.TargetTimeScale;

        // Show top health bar if configured
        if (bossHealthBar != null && enemy.Health != null)
        {
            bossHealthBar.Show(enemy.Health, enemy.EnemyDisplayName);
        }

        isIntroActive = false;
        activeIntroRoutine = null;

        OnIntroCompleted?.Invoke(enemy);
        onComplete?.Invoke();
    }

    private void HideSiblingsAlongPath(Transform root, Transform target, List<GameObject> hiddenObjects)
    {
        Transform current = target;
        while (current != null && current != root)
        {
            Transform parent = current.parent;
            if (parent == null) break;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling != current && sibling.gameObject.activeSelf)
                {
                    sibling.gameObject.SetActive(false);
                    hiddenObjects.Add(sibling.gameObject);
                }
            }

            current = parent;
        }
    }

    private void LockPlayerControls()
    {
        disabledPlayerComponents.Clear();

        Player player = Player.Instance;
        if (player != null)
        {
            MonoBehaviour[] allPlayerComponents = player.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour comp in allPlayerComponents)
            {
                if (comp == null || !comp.enabled) continue;

                // Disable player locomotion, weapon actions, input reader, camera pivot
                if (comp is InputReader ||
                    comp is PlayerCameraPivot ||
                    comp is PlayerAttack ||
                    comp is PlayerBow ||
                    comp is PlayerDodge ||
                    comp is PlayerThrownAxe ||
                    comp is PlayerWand ||
                    comp is PlayerMount ||
                    comp.GetType().Name.Contains("SamplePlayerAnimationController"))
                {
                    comp.enabled = false;
                    disabledPlayerComponents.Add(comp);
                }
            }
        }

        if (extraComponentsToDisable != null)
        {
            foreach (MonoBehaviour extra in extraComponentsToDisable)
            {
                if (extra != null && extra.enabled)
                {
                    extra.enabled = false;
                    disabledPlayerComponents.Add(extra);
                }
            }
        }
    }

    private void UnlockPlayerControls()
    {
        foreach (MonoBehaviour comp in disabledPlayerComponents)
        {
            if (comp != null)
            {
                comp.enabled = true;
            }
        }
        disabledPlayerComponents.Clear();
    }

    private IEnumerator PlayDefaultRoarRoutine(Vector3 position)
    {
        if (defaultRoarDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(defaultRoarDelay);
        }

        if (defaultRoarSound != null)
        {
            AudioSource.PlayClipAtPoint(defaultRoarSound, position, defaultRoarVolume);
        }
    }
}
