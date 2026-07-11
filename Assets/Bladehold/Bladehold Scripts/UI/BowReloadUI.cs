using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Radial reload indicator for the active class's hold-aim weapon (bow / thrown axe). While a
///     shot's fire cooldown is running, fades a <see cref="CanvasGroup" /> in and drives a
///     radial-filled <see cref="Image" /> from empty to full as
///     <see cref="IChargedAimWeapon.CooldownFraction" /> recovers (polled, the
///     <see cref="BowCrosshairUI" /> pattern), then fades back out once the weapon is ready. Only
///     shown while aiming — the cooldown only gates shots, and the crosshair is the aim-mode anchor
///     it sits next to. Lives on the reload UI object under the HUD canvas; the fill Image must be
///     set to Filled / Radial 360 in the inspector (the fill origin/direction are authored there too).
/// </summary>
public class BowReloadUI : MonoBehaviour
{
    [Tooltip("The player's bow, used while it's the active class's aim weapon. When benched (disabled), the class controller's ActiveAimWeapon takes over. Defaults to the one on Player.Instance.")]
    [SerializeField] private PlayerBow bow;
    [Tooltip("Faded in while the fire cooldown is running. Usually on this object.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Radial-filled Image (type Filled / Radial 360) whose fillAmount tracks the cooldown recovery.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Seconds the indicator takes to fade in and out.")]
    [SerializeField] private float fadeSeconds = 0.1f;

    private IChargedAimWeapon weapon;
    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        // The weapon lives on the player prefab, the indicator on the HUD canvas — reach it through
        // the Player singleton rather than a scene lookup.
        weapon = AimWeaponResolver.Resolve(bow);

        if (weapon == null)
        {
            Debug.LogError("No aim weapon found: assign the bow, or ensure the class controller's active class carries an IChargedAimWeapon.");
            anyError = true;
        }
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (fillImage == null)
        {
            Debug.LogError("Fill Image is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (fillImage.type != Image.Type.Filled)
        {
            Debug.LogWarning("BowReloadUI: the fill Image is not set to type Filled — fillAmount will have no visible effect until it is (Filled / Radial 360).");
        }

        canvasGroup.alpha = 0f;
        // Display-only; never let it swallow clicks meant for the game.
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        bool reloading = weapon.IsAiming && weapon.IsCoolingDown;
        if (reloading)
        {
            fillImage.fillAmount = weapon.CooldownFraction;
        }

        float fadeStep = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, reloading ? 1f : 0f, fadeStep);
    }
}
