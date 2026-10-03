using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Visibility and status line for the warhorse HUD group (health + charge stamina bars), stacked
///     above the player's health bar. The group shows whenever the run has a mount to summon, not just
///     while riding, because the horse's wounds and banked charge now carry between summons:
///     <list type="bullet">
///         <item>Riding: full alpha.</item>
///         <item>On foot with a living horse: <see cref="idleAlpha" />, so the banked charge filling
///         from kills is still readable.</item>
///         <item>Horse dead (<see cref="PlayerMount.IsMountLost" />): dimmed, with the status label
///         telling the player to replace it at the shop.</item>
///         <item>Mount unavailable in this scene (<see cref="PlayerSummonMount.IsAbilityUnlocked" />): hidden.</item>
///     </list>
///     Alpha eases with unscaled time so it reads on paused screens too.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HorseBarGroupUI : MonoBehaviour
{
    [Tooltip("The player's mount. Resolved from Player.Instance's root if left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("Optional: status line under the bars (\"Fallen\" when the horse is dead). Hidden otherwise.")]
    [SerializeField] private TMP_Text statusLabel;

    [Tooltip("Played when the player mounts (punch / glow). Must not animate this group's alpha, which is driven here.")]
    [SerializeField] private MMF_Player mountShowFeedback;

    [Tooltip("Played when the horse dies for good.")]
    [SerializeField] private MMF_Player mountLostFeedback;

    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.7f;
    [SerializeField, Range(0f, 1f)] private float lostAlpha = 0.85f;
    [SerializeField] private float fadeSpeed = 6f;

    private CanvasGroup canvasGroup;
    private PlayerSummonMount summon;
    private bool anyError;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Start()
    {
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true);
        }
        if (mount == null)
        {
            Debug.LogError("[HorseBarGroupUI] PlayerMount not found — assign it or ensure Player.Instance has one.", this);
            anyError = true;
            return;
        }

        summon = mount.transform.root.GetComponentInChildren<PlayerSummonMount>(true);
        mount.OnMountedChanged += HandleMountedChanged;
        mount.OnMountLost += HandleMountLost;

        canvasGroup.alpha = TargetAlpha();
        RefreshStatus();
    }

    private void OnDestroy()
    {
        if (mount != null)
        {
            mount.OnMountedChanged -= HandleMountedChanged;
            mount.OnMountLost -= HandleMountLost;
        }
    }

    private void Update()
    {
        if (anyError) return;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, TargetAlpha(), fadeSpeed * Time.unscaledDeltaTime);
        RefreshStatus();
    }

    private float TargetAlpha()
    {
        bool available = summon == null || summon.IsAbilityUnlocked;
        if (!available && !mount.IsMounted) return 0f;
        if (mount.IsMounted) return 1f;
        return mount.IsMountLost ? lostAlpha : idleAlpha;
    }

    private void RefreshStatus()
    {
        if (statusLabel == null) return;

        bool lost = mount.IsMountLost && !mount.IsMounted;
        if (statusLabel.gameObject.activeSelf != lost)
        {
            statusLabel.gameObject.SetActive(lost);
        }
        if (lost)
        {
            statusLabel.text = Loc.Get("hud.mount.fallen", "Fallen: buy a new warhorse at the shop");
        }
    }

    private void HandleMountedChanged(bool mounted)
    {
        if (mounted && mountShowFeedback != null)
        {
            mountShowFeedback.PlayFeedbacks();
        }
    }

    private void HandleMountLost()
    {
        if (mountLostFeedback != null)
        {
            mountLostFeedback.PlayFeedbacks();
        }
    }
}
