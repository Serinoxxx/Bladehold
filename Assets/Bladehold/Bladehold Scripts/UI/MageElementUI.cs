using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD widget for the Mage's elemental imbuement. Polls <see cref="MageImbuement" /> every frame
///     (the timer decays continuously — the <see cref="RageBarUI" /> polling pattern): an element
///     icon tinted per element, a charge-count label, and a remaining-time fill. Class-conditional
///     by design: when the player has no enabled <see cref="MageImbuement" /> (any non-Mage class),
///     the whole object hides itself — that's the feature working, not an error. Lives under the HUD
///     canvas; the fill Image should be set to type Filled in the inspector (the
///     <see cref="BowReloadUI" /> convention).
/// </summary>
public class MageElementUI : MonoBehaviour
{
    [Tooltip("The player's MageImbuement. Defaults to the one on Player.Instance.")]
    [SerializeField] private MageImbuement imbuement;
    [Tooltip("Image showing the active element's icon (from MageImbuement's element styles).")]
    [SerializeField] private Image elementIcon;
    [Tooltip("Filled Image whose fillAmount tracks the remaining imbuement time.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Optional charge-count label, e.g. \"x3\".")]
    [SerializeField] private TMP_Text chargeLabel;
    [Tooltip("Widget contents shown only while an imbuement is active (icon, fill, label parent). Optional — defaults to this object staying visible with an empty readout.")]
    [SerializeField] private GameObject activeGroup;

    private bool anyError = false;

    private void Start()
    {
        // The buff lives on the player prefab, the widget on the HUD canvas — reach it through the
        // Player singleton rather than a scene lookup.
        if (imbuement == null && Player.Instance != null)
        {
            imbuement = Player.Instance.GetComponentInChildren<MageImbuement>();
        }

        // No enabled MageImbuement = a class without imbuement; the widget simply isn't part of
        // this run's HUD.
        if (imbuement == null || !imbuement.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }

        if (fillImage == null)
        {
            Debug.LogError("MageElementUI: Fill Image is not assigned.");
            anyError = true;
            return;
        }

        if (fillImage.type != Image.Type.Filled)
        {
            Debug.LogWarning("MageElementUI: the fill Image is not set to type Filled — fillAmount will have no visible effect until it is.");
        }

        fillImage.fillAmount = 0f;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        bool active = imbuement.IsActive;
        if (activeGroup != null)
        {
            activeGroup.SetActive(active);
        }

        fillImage.fillAmount = imbuement.DurationFraction;

        MageImbuement.ElementStyle style = imbuement.CurrentStyle;
        if (elementIcon != null)
        {
            elementIcon.enabled = active && style != null && style.icon != null;
            if (style != null)
            {
                if (style.icon != null)
                {
                    elementIcon.sprite = style.icon;
                }
                elementIcon.color = style.tint;
            }
        }
        if (fillImage != null && style != null)
        {
            fillImage.color = style.tint;
        }

        if (chargeLabel != null)
        {
            chargeLabel.text = active ? "x" + imbuement.ChargeCount : string.Empty;
        }
    }
}
