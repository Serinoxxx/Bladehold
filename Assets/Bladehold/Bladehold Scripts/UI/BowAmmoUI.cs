using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Contextual HUD element displayed while aiming ranged weapons (Bow, Thrown Axe, Wand).
///     Shows the current and maximum ammunition under the crosshairs (e.g. '10/20 [arrow icon]')
///     and displays a bold red 'OUT OF AMMO' warning center-screen when ammunition is depleted.
///     Fades in/out with weapon aim states matching BowCrosshairUI.
/// </summary>
public class BowAmmoUI : MonoBehaviour
{
    [Tooltip("The player's bow or ranged weapon. Resolved automatically if unassigned.")]
    [SerializeField] private PlayerBow bow;

    [Tooltip("CanvasGroup controlling visibility fade during aiming. Usually on this object or root container.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Ammo Counter")]
    [Tooltip("Text element positioned directly beneath the crosshair showing '10/20'.")]
    [SerializeField] private TMP_Text ammoCountText;

    [Tooltip("Icon image displayed next to or after the ammo text (e.g. arrow icon).")]
    [SerializeField] private Image arrowIcon;

    [Header("Out of Ammo Warning")]
    [Tooltip("Center-screen text element in bold red displayed when aiming with 0 ammunition.")]
    [SerializeField] private TMP_Text outOfAmmoText;

    [Header("Tuning & Colors")]
    [Tooltip("Seconds to fade the UI in and out when aiming starts or ends.")]
    [SerializeField] private float fadeSeconds = 0.15f;

    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private Color outOfAmmoColor = new Color(1f, 0.25f, 0.25f, 1f);

    private IChargedAimWeapon weapon;
    private PlayerAmmo playerAmmo;

    private bool anyError;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        weapon = AimWeaponResolver.Resolve(bow);

        if (canvasGroup == null)
        {
            Debug.LogError("[BowAmmoUI] canvasGroup is not assigned.", this);
            anyError = true;
        }
        if (ammoCountText == null)
        {
            Debug.LogError("[BowAmmoUI] ammoCountText is not assigned.", this);
            anyError = true;
        }
        if (arrowIcon == null)
        {
            Debug.LogError("[BowAmmoUI] arrowIcon is not assigned.", this);
            anyError = true;
        }
        if (outOfAmmoText == null)
        {
            Debug.LogError("[BowAmmoUI] outOfAmmoText is not assigned.", this);
            anyError = true;
        }
        if (anyError)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        ResolveAmmoComponent();
        PlayerAmmo.OnAnyAmmoChanged += HandleAmmoChanged;

        RefreshDisplay(GetEffectiveCurrentAmmo(), GetEffectiveMaxAmmo());
    }

    private void OnDestroy()
    {
        PlayerAmmo.OnAnyAmmoChanged -= HandleAmmoChanged;
    }

    private void ResolveAmmoComponent()
    {
        if (playerAmmo == null && Player.Instance != null)
        {
            playerAmmo = Player.Instance.Ammo;
        }
        if (playerAmmo == null)
        {
            playerAmmo = PlayerAmmo.Instance;
        }
    }

    private int GetEffectiveCurrentAmmo()
    {
        return RunSession.CurrentAmmo;
    }

    private int GetEffectiveMaxAmmo()
    {
        if (playerAmmo != null)
        {
            return playerAmmo.MaxAmmo;
        }
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float val = Player.Instance.Stats.GetValue(StatType.MaxAmmo);
            if (val > 0f) return Mathf.RoundToInt(val);
        }
        return RunSession.HasMetaPerk("deep_quiver") ? 25 : 20;
    }

    private void HandleAmmoChanged(int current, int max)
    {
        RefreshDisplay(current, max);
    }

    public void RefreshDisplay(int current, int max)
    {
        if (ammoCountText != null)
        {
            ammoCountText.text = $"{current}/{max}";
            ammoCountText.color = current <= 0 ? outOfAmmoColor : normalAmmoColor;
        }

        if (outOfAmmoText != null)
        {
            outOfAmmoText.gameObject.SetActive(current <= 0);
        }
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        if (weapon == null)
        {
            weapon = AimWeaponResolver.Resolve(bow);
            if (weapon == null) return;
        }

        bool isAiming = weapon.IsAiming;
        float targetAlpha = isAiming ? 1f : 0f;

        if (canvasGroup != null)
        {
            float fadeStep = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeStep);
        }

        int curr = GetEffectiveCurrentAmmo();
        int max = GetEffectiveMaxAmmo();

        if (outOfAmmoText != null)
        {
            bool showWarning = isAiming && (curr <= 0);
            if (outOfAmmoText.gameObject.activeSelf != showWarning)
            {
                outOfAmmoText.gameObject.SetActive(showWarning);
            }
        }
    }
}
