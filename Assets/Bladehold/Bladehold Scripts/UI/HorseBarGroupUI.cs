using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Controls the show/hide animation for the horse HUD group (health + stamina bars).
///     Listens to <see cref="PlayerMount.OnMountedChanged" /> and plays a <see cref="MMF_Player" />
///     when the player mounts or dismounts, giving the bars a polished Feel-powered entrance/exit.
///
///     Attach this to the root GameObject of the horse bar group (the object containing both the
///     horse health bar and the horse stamina bar). Pair with a <see cref="CanvasGroup" /> whose
///     alpha the mount feedback can animate (use an MMF_CanvasGroup feedback).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HorseBarGroupUI : MonoBehaviour
{
    [Tooltip("The player's mount. Auto-wired from Player.Instance if left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("Played when the player mounts a horse (slide in / fade in).")]
    [SerializeField] private MMF_Player mountShowFeedback;

    [Tooltip("Played when the player dismounts or the horse dies (fade out).")]
    [SerializeField] private MMF_Player mountHideFeedback;

    private CanvasGroup _canvasGroup;
    private bool _anyError;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        // Start hidden — the group only becomes visible on mount.
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    private void Start()
    {
        if (mount == null && Player.Instance != null)
            mount = Player.Instance.GetComponent<PlayerMount>();

        if (mount == null)
        {
            Debug.LogError("[HorseBarGroupUI] PlayerMount not found — assign it or ensure Player.Instance has one.");
            _anyError = true;
            return;
        }

        mount.OnMountedChanged += HandleMountedChanged;

        // If already mounted at Start (e.g. StartMountedSpawner), show immediately without animation.
        if (mount.IsMounted)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }
    }

    private void OnDestroy()
    {
        if (mount != null)
            mount.OnMountedChanged -= HandleMountedChanged;
    }

    private void HandleMountedChanged(bool mounted)
    {
        if (_anyError) return;

        if (mounted)
        {
            _canvasGroup.blocksRaycasts = true;
            if (mountShowFeedback != null)
                mountShowFeedback.PlayFeedbacks();
            else
                _canvasGroup.alpha = 1f; // Fallback if no feedback assigned.
        }
        else
        {
            if (mountHideFeedback != null)
            {
                mountHideFeedback.PlayFeedbacks();
                // Disable raycasts after the hide animation finishes.
                _canvasGroup.blocksRaycasts = false;
            }
            else
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
