using MoreMountains.Tools;
using TMPro;
using UnityEngine;

/// <summary>
///     The warhorse's health bar. Polls <see cref="PlayerMount.MountHealthFraction" /> — the live horse
///     while riding it, the banked value between summons — and pushes changes into an
///     <see cref="MMProgressBar" /> with <c>UpdateBar</c> only when the value actually moves, so hits
///     still get the bump and trailing delayed bar. Visibility belongs to <see cref="HorseBarGroupUI" />.
/// </summary>
public class HorseHealthBarUI : MonoBehaviour
{
    [Tooltip("The player's mount. Resolved from Player.Instance's root if left empty.")]
    [SerializeField] private PlayerMount mount;

    [Tooltip("The MMProgressBar that visualises the horse's health.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Optional: percentage label over the bar.")]
    [SerializeField] private TMP_Text valueLabel;

    private float shownFraction = -1f;
    private bool anyError;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true);
        }
        if (mount == null)
        {
            Debug.LogError("[HorseHealthBarUI] PlayerMount not found — assign it or ensure Player.Instance has one.", this);
            anyError = true;
        }
        if (progressBar == null)
        {
            Debug.LogError("[HorseHealthBarUI] MMProgressBar not assigned.", this);
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError) return;

        float fraction = mount.MountHealthFraction;
        if (shownFraction < 0f)
        {
            progressBar.SetBar01(fraction);
        }
        else if (Mathf.Abs(fraction - shownFraction) > 0.001f)
        {
            progressBar.UpdateBar01(fraction);
        }
        else
        {
            return;
        }

        shownFraction = fraction;
        if (valueLabel != null)
        {
            valueLabel.text = Mathf.CeilToInt(fraction * 100f) + "%";
        }
    }
}
