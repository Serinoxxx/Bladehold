using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD stamina bar for the mounted horse: shows while riding (via
///     <see cref="PlayerMount.OnMountedChanged" />), polls the horse's
///     <see cref="HorseMotor.NormalizedStamina" /> each frame into an Image fill (the
///     <see cref="SwordChargeFeedback" /> polling idiom), and tints it while the horse is
///     exhausted so the charge lockout reads at a glance. A pure display — it never touches the
///     horse.
/// </summary>
public class HorseStaminaUI : MonoBehaviour
{
    [Tooltip("The player's mount. Auto-wired from Player.Instance in Start when left empty.")]
    [SerializeField] private PlayerMount mount;
    [Tooltip("Root object toggled with mounting — the whole bar (background + fill), so nothing lingers on foot.")]
    [SerializeField] private GameObject container;
    [Tooltip("Filled-type Image whose fillAmount tracks stamina 0..1.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Color normalColor = new Color(0.35f, 0.75f, 0.25f);
    [Tooltip("Fill tint while the horse is exhausted (charging locked until stamina recovers).")]
    [SerializeField] private Color exhaustedColor = new Color(0.8f, 0.3f, 0.15f);

    private bool anyError = false;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.GetComponent<PlayerMount>();
        }
        if (mount == null)
        {
            Debug.LogError("PlayerMount is not assigned and could not be found on the Player.");
            anyError = true;
        }
        if (container == null)
        {
            Debug.LogError("Container GameObject is not assigned in the inspector.");
            anyError = true;
        }
        if (fillImage == null)
        {
            Debug.LogError("Fill Image is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        mount.OnMountedChanged += HandleMountedChanged;
        HandleMountedChanged(mount.IsMounted);
    }

    private void OnDestroy()
    {
        if (mount != null)
        {
            mount.OnMountedChanged -= HandleMountedChanged;
        }
    }

    private void HandleMountedChanged(bool mounted)
    {
        container.SetActive(mounted);
    }

    private void Update()
    {
        if (anyError || !mount.IsMounted) return;

        HorseMotor horse = mount.CurrentHorse;
        if (horse == null) return;

        fillImage.fillAmount = horse.NormalizedStamina;
        fillImage.color = horse.IsExhausted ? exhaustedColor : normalColor;
    }
}
