using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
///     HUD bar for one ultimate slot (melee or ranged). The HUD carries one bar per slot; a bar stays hidden
///     (CanvasGroup alpha 0) until the run owns that slot's ultimate.
/// </summary>
public class UltimateBarUI : MonoBehaviour
{
    [Tooltip("Which ultimate this bar tracks. The ranged one fires while aiming the ranged weapon.")]
    [SerializeField] private UltimateSlot slot = UltimateSlot.Melee;
    [Tooltip("Hides the bar while this slot has no ultimate. Auto-wired from this GameObject.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private PlayerUltimateController ultimateController;
    [SerializeField] private MoreMountains.Tools.MMProgressBar progressBar;
    [SerializeField] private Image glowImage;
    [SerializeField] private TextMeshProUGUI inputKeyText;
    [SerializeField] private TextMeshProUGUI chargeText;
    
    [Header("Feedbacks")]
    [SerializeField] private MoreMountains.Feedbacks.MMF_Player fullFeedback;
    [SerializeField] private MoreMountains.Feedbacks.MMF_Player activatedFeedback;
    
    [Header("Colors & Animation")]
    [SerializeField] private Color fullColor = Color.yellow;
    [SerializeField] private float glowSpeed = 2f;
    [SerializeField] private float glowAlphaMin = 0.2f;
    [SerializeField] private float glowAlphaMax = 0.8f;

    private bool isFull;
    private bool isSubscribed;

    private void OnValidate()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        TryBindController();
    }

    private bool IsThisSlotActive => ultimateController != null && ultimateController.IsUltimateActive && ultimateController.ActiveSlot == slot;

    private void RefreshVisibility()
    {
        if (canvasGroup == null) return;
        bool owned = !string.IsNullOrEmpty(RunSession.GetUltimateId(slot));
        canvasGroup.alpha = owned ? 1f : 0f;
        canvasGroup.blocksRaycasts = owned;
    }

    private void OnEnable()
    {
        TryBindController();
        UpdateInputText();
    }

    private void Start()
    {
        // Scenes whose SceneAbilityRules block the ultimate (the Fishing Pond) hide the bar; the
        // charge itself is untouched and shows again next scene.
        if (!SceneAbilityRules.UltimateAllowed)
        {
            gameObject.SetActive(false);
            return;
        }

        if (canvasGroup == null)
        {
            Debug.LogError($"[UltimateBarUI] '{name}' has no CanvasGroup, so it can't hide while the {slot} ultimate isn't owned.");
        }

        TryBindController();
        UpdateInputText();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        UnbindController();
    }

    private void TryBindController()
    {
        if (isSubscribed) return;

        if (ultimateController == null)
        {
            if (Player.Instance != null)
            {
                ultimateController = Player.Instance.GetComponent<PlayerUltimateController>();
            }
            if (ultimateController == null)
            {
                ultimateController = FindFirstObjectByType<PlayerUltimateController>();
            }
        }

        if (ultimateController != null)
        {
            ultimateController.OnChargeChanged -= HandleChargeChanged;
            ultimateController.OnChargeChanged += HandleChargeChanged;
            ultimateController.OnUltimateActivated -= HandleActivated;
            ultimateController.OnUltimateActivated += HandleActivated;
            ultimateController.OnUltimateDeactivated -= HandleDeactivated;
            ultimateController.OnUltimateDeactivated += HandleDeactivated;
            isSubscribed = true;
            UpdateBar(ultimateController.GetCharge(slot));
        }
    }

    private void UnbindController()
    {
        if (ultimateController != null && isSubscribed)
        {
            ultimateController.OnChargeChanged -= HandleChargeChanged;
            ultimateController.OnUltimateActivated -= HandleActivated;
            ultimateController.OnUltimateDeactivated -= HandleDeactivated;
            isSubscribed = false;
        }
    }

    private void HandleChargeChanged(UltimateSlot changedSlot, float charge)
    {
        if (changedSlot == slot) UpdateBar(charge);
    }

    private void UpdateBar(float charge)
    {
        RefreshVisibility();
        if (IsThisSlotActive) return;

        float fraction = charge / PlayerUltimateController.MaxCharge;

        if (progressBar != null)
        {
            progressBar.UpdateBar(charge, 0f, PlayerUltimateController.MaxCharge);
        }

        if (chargeText != null)
        {
            chargeText.text = $"{Mathf.FloorToInt(fraction * 100f)}%";
        }

        bool wasFull = isFull;
        isFull = fraction >= 1f;

        if (isFull && !wasFull)
        {
            if (glowImage != null) glowImage.gameObject.SetActive(true);
            if (inputKeyText != null) inputKeyText.gameObject.SetActive(true);
            if (fullFeedback != null) fullFeedback.PlayFeedbacks();
        }
        else if (!isFull && wasFull)
        {
            if (glowImage != null) glowImage.gameObject.SetActive(false);
            if (inputKeyText != null) inputKeyText.gameObject.SetActive(false);
        }
    }

    private void HandleActivated()
    {
        if (!IsThisSlotActive) return;

        if (progressBar != null)
        {
            progressBar.SetBar(PlayerUltimateController.MaxCharge, 0f, PlayerUltimateController.MaxCharge);
        }
        if (inputKeyText != null) inputKeyText.gameObject.SetActive(false);
        if (glowImage != null) glowImage.gameObject.SetActive(true);
        if (activatedFeedback != null) activatedFeedback.PlayFeedbacks();
    }

    private void HandleDeactivated()
    {
        isFull = false;
        if (glowImage != null) glowImage.gameObject.SetActive(false);
        if (inputKeyText != null) inputKeyText.gameObject.SetActive(false);
        UpdateBar(ultimateController != null ? ultimateController.GetCharge(slot) : 0f);
    }

    private void Update()
    {
        if (!isSubscribed)
        {
            TryBindController();
        }

        RefreshVisibility();

        if (IsThisSlotActive)
        {
            float remaining = ultimateController.ActiveUltimateRemainingTime;
            float total = ultimateController.ActiveUltimateDuration;
            float fraction = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

            if (progressBar != null)
            {
                // Use SetBar per mm-progress-bars skill so per-frame updates do not freeze lerp coroutine
                progressBar.SetBar(fraction * PlayerUltimateController.MaxCharge, 0f, PlayerUltimateController.MaxCharge);
            }

            if (chargeText != null)
            {
                chargeText.text = $"{remaining:F1}s";
            }

            if (inputKeyText != null && inputKeyText.gameObject.activeSelf)
            {
                inputKeyText.gameObject.SetActive(false);
            }

            if (glowImage != null && glowImage.gameObject.activeSelf)
            {
                float alpha = Mathf.Lerp(glowAlphaMin, glowAlphaMax, (Mathf.Sin(Time.time * glowSpeed * 2f) + 1f) / 2f);
                Color c = glowImage.color;
                c.a = alpha;
                glowImage.color = c;
            }

            return;
        }

        if (isFull && glowImage != null && glowImage.gameObject.activeSelf)
        {
            float alpha = Mathf.Lerp(glowAlphaMin, glowAlphaMax, (Mathf.Sin(Time.time * glowSpeed) + 1f) / 2f);
            Color c = glowImage.color;
            c.a = alpha;
            glowImage.color = c;
        }
    }

    private void UpdateInputText()
    {
        if (inputKeyText == null) return;
        
        // This is a naive way to get the primary binding for the Ultimate action.
        var player = Player.Instance;
        if (player != null && player.InputSettings != null)
        {
            var map = player.InputSettings.GetRebindableActionMap();
            if (map != null)
            {
                var action = map.FindAction("Ultimate");
                if (action != null)
                {
                    int bindIndex = action.GetBindingIndexForControl(action.controls.Count > 0 ? action.controls[0] : null);
                    string key = bindIndex >= 0 ? action.GetBindingDisplayString(bindIndex) : "Q/Y";
                    inputKeyText.text = slot == UltimateSlot.Ranged ? $"Aim + {key}" : key;
                    return;
                }
            }
        }
        
        inputKeyText.text = slot == UltimateSlot.Ranged ? "Aim + Q" : "Q";
    }
}
