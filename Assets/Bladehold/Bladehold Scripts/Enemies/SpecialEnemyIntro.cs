using System.Collections;
using UnityEngine;

/// <summary>
///     Identifies a boss or special enemy that triggers a cinematic introduction when spawning.
///     Configures the display name, animation trigger, optional camera framing point, and roar/intro sound.
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
    [Tooltip("Optional roar or audio clip played during the intro cinematic.")]
    [SerializeField] private AudioClip roarSound;

    [Tooltip("Volume scale for the roar audio clip (0 to 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float roarVolume = 1f;

    [Tooltip("Delay in unscaled seconds after the intro starts before playing the roar sound.")]
    [SerializeField] private float roarDelay = 0f;

    [Tooltip("Optional AudioSource component to play the roar sound through. If not assigned, uses one on this GameObject or plays at position.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Component References")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;

    public string EnemyDisplayName => enemyDisplayName;
    public string TauntTriggerName => tauntTriggerName;
    public Transform CameraFocusTransform => cameraFocusTransform != null ? cameraFocusTransform : transform;
    public AudioClip RoarSound => roarSound;
    public float RoarVolume => roarVolume;
    public float RoarDelay => roarDelay;
    public AudioSource AudioSource => audioSource;
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
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        AutoFindFocusTransform();
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
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
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
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
    ///     Plays the configured roar sound for this special enemy intro.
    /// </summary>
    public void PlayRoarSound()
    {
        if (roarSound == null) return;

        if (roarDelay > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(PlayRoarDelayedRoutine());
        }
        else
        {
            ExecutePlayRoar();
        }
    }

    private IEnumerator PlayRoarDelayedRoutine()
    {
        yield return new WaitForSecondsRealtime(roarDelay);
        ExecutePlayRoar();
    }

    private void ExecutePlayRoar()
    {
        if (roarSound == null) return;

        AudioSource source = audioSource != null ? audioSource : GetComponent<AudioSource>();
        if (source != null)
        {
            source.PlayOneShot(roarSound, roarVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(roarSound, transform.position, roarVolume);
        }
    }
}

