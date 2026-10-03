using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The warhorse's charge-stamina bar. Polls <see cref="PlayerMount.MountStaminaFraction" /> every
///     frame (<c>SetBar01</c>, the per-frame idiom), so it fills from kills even while the player is on
///     foot. The fill tint reads the charge state at a glance:
///     <list type="bullet">
///         <item>building (below the recovery threshold, can't charge yet): <see cref="buildingColor" /></item>
///         <item>ready to charge: <see cref="readyColor" />, with <see cref="readyFeedback" /> on the edge</item>
///         <item>charging: <see cref="chargingColor" /></item>
///         <item>exhausted (emptied mid-charge): <see cref="exhaustedColor" /></item>
///     </list>
///     Visibility (show/hide) belongs to <see cref="HorseBarGroupUI" /> on the parent.
/// </summary>
public class HorseStaminaUI : MonoBehaviour
{
    [Tooltip("The player's mount. Resolved from Player.Instance's root if left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("The MMProgressBar that displays the horse's stamina.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Optional: the foreground fill Image inside the MMProgressBar, tinted by charge state.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Optional: label that reads CHARGE READY when a charge can start.")]
    [SerializeField] private TMP_Text readyLabel;

    [Tooltip("Optional: played when the charge becomes ready (glint, pop).")]
    [SerializeField] private MMF_Player readyFeedback;

    [SerializeField] private Color buildingColor = new Color(0.55f, 0.47f, 0.30f);
    [SerializeField] private Color readyColor = new Color(1f, 0.82f, 0.44f);
    [SerializeField] private Color chargingColor = new Color(1f, 0.95f, 0.75f);

    [Tooltip("Fill tint while the horse is exhausted (charging locked until stamina recovers).")]
    [SerializeField] private Color exhaustedColor = new Color(0.85f, 0.30f, 0.15f);

    private bool wasReady;
    private bool anyError;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true);
        }
        if (mount == null)
        {
            Debug.LogError("[HorseStaminaUI] PlayerMount is not assigned and could not be found on the Player.", this);
            anyError = true;
        }
        if (progressBar == null)
        {
            Debug.LogError("[HorseStaminaUI] MMProgressBar is not assigned in the inspector.", this);
            anyError = true;
        }
        if (anyError) return;

        wasReady = mount.IsChargeReady;
        if (readyLabel != null)
        {
            readyLabel.text = Loc.Get("hud.mount.charge_ready", "Charge ready");
        }
    }

    private void Update()
    {
        if (anyError) return;

        progressBar.SetBar01(mount.MountStaminaFraction);

        bool ready = mount.IsChargeReady && !mount.IsMountLost;
        if (ready && !wasReady && readyFeedback != null)
        {
            readyFeedback.PlayFeedbacks();
        }
        wasReady = ready;

        if (fillImage != null)
        {
            if (mount.IsMountExhausted) fillImage.color = exhaustedColor;
            else if (mount.IsCharging) fillImage.color = chargingColor;
            else fillImage.color = ready ? readyColor : buildingColor;
        }

        if (readyLabel != null)
        {
            bool showReady = ready && !mount.IsCharging;
            if (readyLabel.gameObject.activeSelf != showReady)
            {
                readyLabel.gameObject.SetActive(showReady);
            }
        }
    }
}
