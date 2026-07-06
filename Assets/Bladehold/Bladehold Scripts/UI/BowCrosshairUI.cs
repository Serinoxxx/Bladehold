using UnityEngine;

/// <summary>
///     Screen-centre crosshair for the bow. Fades a <see cref="CanvasGroup" /> in while
///     <see cref="PlayerBow.IsAiming" /> (polled, the <see cref="SwordChargeFeedback" /> pattern) and
///     back out on release, and tightens the reticle as the draw gains charge levels so full draw
///     reads at a glance. Lives on the crosshair UI object under the HUD canvas; the visuals (Image
///     sprite, size, colour) are authored on the object itself.
/// </summary>
public class BowCrosshairUI : MonoBehaviour
{
    [Tooltip("The player's bow. Defaults to the one on Player.Instance.")]
    [SerializeField] private PlayerBow bow;
    [Tooltip("Faded in while aiming. Usually on this object.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Scaled down as the draw charges. Usually this object's own RectTransform.")]
    [SerializeField] private RectTransform reticle;

    [Tooltip("Seconds the crosshair takes to fade in and out.")]
    [SerializeField] private float fadeSeconds = 0.15f;
    [Tooltip("Reticle scale at full charge (1 = no tightening).")]
    [SerializeField] private float fullChargeScale = 0.6f;
    [Tooltip("Seconds the reticle takes to settle on each new charge step.")]
    [SerializeField] private float tightenSeconds = 0.1f;

    private float currentScale = 1f;
    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (reticle == null)
        {
            reticle = transform as RectTransform;
        }
    }

    private void Start()
    {
        // The bow lives on the player prefab, the crosshair on the HUD canvas — reach it through the
        // Player singleton rather than a scene lookup.
        if (bow == null && Player.Instance != null)
        {
            bow = Player.Instance.GetComponentInChildren<PlayerBow>();
        }

        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned and none was found on Player.Instance.");
            anyError = true;
        }
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (reticle == null)
        {
            Debug.LogError("Reticle RectTransform is not assigned and this object has none.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        // The crosshair is display-only; never let it swallow clicks meant for the game.
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        float fadeStep = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, bow.IsAiming ? 1f : 0f, fadeStep);

        // Tighten toward full-charge scale as the draw levels up; snap open again on release.
        float targetScale = 1f;
        if (bow.IsAiming && bow.MaxChargeLevels > 0)
        {
            float chargeFraction = Mathf.Clamp01((float)bow.ChargeLevel / bow.MaxChargeLevels);
            targetScale = Mathf.Lerp(1f, fullChargeScale, chargeFraction);
        }
        float tightenStep = tightenSeconds > 0f ? Time.deltaTime / tightenSeconds : 1f;
        currentScale = Mathf.MoveTowards(currentScale, targetScale, tightenStep);
        reticle.localScale = Vector3.one * currentScale;
    }
}
