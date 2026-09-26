using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Identifies a boss or special enemy that triggers a cinematic introduction when spawning.
///     Configures the display name, animation trigger, optional camera framing point, and roar feedback.
/// </summary>
public class SpecialEnemyIntro : MonoBehaviour
{
    [Header("Identity & Intro Setup")]
    [Tooltip("Display name shown on the cinematic intro banner and boss health bar (e.g. 'SLAYER').")]
    [SerializeField] private string enemyDisplayName = "SLAYER";

    [Tooltip("Animator trigger parameter played during the unscaled intro sequence.")]
    [SerializeField] private string tauntTriggerName = "Taunt";

    [Tooltip("Optional specific focal point for the intro camera (e.g. head/chest bone). Defaults to this transform.")]
    [SerializeField] private Transform cameraFocusTransform;

    [Tooltip("Whether to automatically trigger the intro sequence in Start(). Objective spawners can also trigger it explicitly.")]
    [SerializeField] private bool autoTriggerOnStart = false;

    [Header("Intro Audio")]
    [Tooltip("Optional: the roar played when the intro starts. The intro freezes time, so any delay on it must use unscaled time. Leave empty to use EnemyIntroController's default roar.")]
    [SerializeField] private MMF_Player roarFeedback;

    [Header("Component References")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;

    public string EnemyDisplayName => enemyDisplayName;
    public string TauntTriggerName => tauntTriggerName;
    public Transform CameraFocusTransform => cameraFocusTransform != null ? cameraFocusTransform : transform;
    public bool HasRoar => roarFeedback != null;
    public Health Health => health;
    public Animator Animator => animator;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        AutoFindFocusTransform();
    }

    private void Awake()
    {
        AutoFindFocusTransform();
    }

    private void Start()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        AutoFindFocusTransform();

        if (autoTriggerOnStart && EnemyIntroController.Instance != null)
        {
            EnemyIntroController.Instance.PlayIntro(this);
        }
    }

    private void AutoFindFocusTransform()
    {
        if (cameraFocusTransform == null)
        {
            if (animator != null && animator.isHuman)
            {
                cameraFocusTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (cameraFocusTransform == null)
            {
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    if (t.name.Equals("Head", System.StringComparison.OrdinalIgnoreCase))
                    {
                        cameraFocusTransform = t;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Triggers the cinematic introduction sequence through the EnemyIntroController singleton.
    /// </summary>
    public void TriggerIntro(System.Action onComplete = null)
    {
        if (EnemyIntroController.Instance != null)
        {
            EnemyIntroController.Instance.PlayIntro(this, onComplete);
        }
        else
        {
            Debug.LogWarning("[SpecialEnemyIntro] EnemyIntroController.Instance not found in scene. Skipping intro cinematic.");
            onComplete?.Invoke();
        }
    }

    /// <summary>
    ///     Plays this special enemy's intro roar, if it has one.
    /// </summary>
    public void PlayRoar()
    {
        if (roarFeedback != null)
        {
            roarFeedback.PlayFeedbacks(transform.position);
        }
    }
}
