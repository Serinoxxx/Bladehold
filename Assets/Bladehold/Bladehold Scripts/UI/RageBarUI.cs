using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD bar for the Berserker's rage meter. Polls <see cref="RageBuff.RageFraction" /> every frame
///     (rage decays continuously — the <see cref="SwordChargeFeedback" /> polling pattern) and drives
///     a filled <see cref="Image" /> plus an optional TMP label. Class-conditional by design: when the
///     player has no enabled <see cref="RageBuff" /> (the Swordsman), the whole object hides itself —
///     that's the feature working, not an error. Lives under the HUD canvas; the fill Image should be
///     set to type Filled in the inspector (the <see cref="BowReloadUI" /> convention).
/// </summary>
public class RageBarUI : MonoBehaviour
{
    [Tooltip("The player's RageBuff. Defaults to the one on Player.Instance.")]
    [SerializeField] private RageBuff rage;
    [Tooltip("Filled Image whose fillAmount tracks the meter.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Optional numeric label, e.g. \"62\".")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Seconds the fill takes to settle on the current value (0 = snap).")]
    [SerializeField] private float smoothSeconds = 0.1f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        // The buff lives on the player prefab, the bar on the HUD canvas — reach it through the
        // Player singleton rather than a scene lookup.
        if (rage == null && Player.Instance != null)
        {
            rage = Player.Instance.GetComponent<RageBuff>();
        }

        // No enabled RageBuff = a class without rage; the bar simply isn't part of this run's HUD.
        if (rage == null || !rage.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }

        if (fillImage == null)
        {
            Debug.LogError("RageBarUI: Fill Image is not assigned or found on the GameObject.");
            anyError = true;
            return;
        }

        if (fillImage.type != Image.Type.Filled)
        {
            Debug.LogWarning("RageBarUI: the fill Image is not set to type Filled — fillAmount will have no visible effect until it is.");
        }

        fillImage.fillAmount = 0f;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        float step = smoothSeconds > 0f ? Time.deltaTime / smoothSeconds : 1f;
        fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, rage.RageFraction, step);

        if (label != null)
        {
            label.text = Mathf.RoundToInt(rage.CurrentRage).ToString();
        }
    }
}
