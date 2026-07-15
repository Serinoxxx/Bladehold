using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD stamina bar for the mounted horse: polls <see cref="HorseMotor.NormalizedStamina" />
///     each frame into an <see cref="MMProgressBar" /> while mounted, and tints the bar's foreground
///     <see cref="Image" /> while the horse is exhausted so the charge lockout reads at a glance.
///
///     Visibility (show/hide on mount) is handled by <see cref="HorseBarGroupUI" /> on the parent
///     container — this script only drives the bar value and tint.
/// </summary>
public class HorseStaminaUI : MonoBehaviour
{
    [Tooltip("The player's mount. Auto-wired from Player.Instance in Start when left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("The MMProgressBar that displays the horse's stamina.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Optional: the foreground fill Image inside the MMProgressBar, used for exhaustion tinting.")]
    [SerializeField] private Image fillImage;

    [SerializeField] private Color normalColor = new Color(0.25f, 0.80f, 0.35f);

    [Tooltip("Fill tint while the horse is exhausted (charging locked until stamina recovers).")]
    [SerializeField] private Color exhaustedColor = new Color(0.85f, 0.30f, 0.15f);

    private bool _anyError;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
            mount = Player.Instance.GetComponent<PlayerMount>();

        if (mount == null)
            mount = FindObjectOfType<PlayerMount>();

        if (mount == null)
        {
            Debug.LogError("[HorseStaminaUI] PlayerMount is not assigned and could not be found on the Player.");
            _anyError = true;
        }

        if (progressBar == null)
        {
            Debug.LogError("[HorseStaminaUI] MMProgressBar is not assigned in the inspector.");
            _anyError = true;
        }
    }

    private void Update()
    {
        if (_anyError || !mount.IsMounted) return;

        HorseMotor horse = mount.CurrentHorse;
        if (horse == null) return;

        progressBar.UpdateBar(horse.NormalizedStamina, 0f, 1f);

        // Tint the foreground fill to signal exhaustion (the SwordChargeFeedback polling idiom).
        if (fillImage != null)
            fillImage.color = horse.IsExhausted ? exhaustedColor : normalColor;
    }
}
