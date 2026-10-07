using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
///     HUD indicator for one ultimate slot (melee or ranged) since the Arcane Core redesign (plan 21 phase 5):
///     there's no charge any more. The bar sits full and glows with the wheel's key glyph while the run holds an
///     Arcane Core ("ready"), sits empty without one, and counts down the running ultimate's time. It hides
///     (CanvasGroup alpha 0) while the held weapon in that slot has no ultimate.
/// </summary>
public class UltimateBarUI : MonoBehaviour
{
    [Tooltip("Which ultimate this indicator tracks: the held melee or ranged weapon's.")]
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
    private bool wasOwned;

    private void OnValidate()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        TryBindController();
    }

    // The bar runs 0..1: full = ready (or the whole of a running ultimate's time).
    private const float MaxFill = 1f;

    private bool IsThisSlotActive => ultimateController != null && ultimateController.IsUltimateActive && ultimateController.ActiveSlot == slot;

    private void RefreshVisibility()
    {
        if (canvasGroup == null) return;
        // Scenes whose SceneAbilityRules block the ultimate (the Fishing Pond, the tutorial) hide it too. Polled,
        // because a tutorial step can lift the block mid-scene (T2's Ultimate Trial).
        bool owned = SceneAbilityRules.UltimateAllowed && ultimateController != null && ultimateController.HasUltimate(slot);
        if (owned != wasOwned)
        {
            wasOwned = owned;
            RefreshReady();
        }
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

    private void OnDestroy()
    {
        RunSession.OnArcaneCoresChanged -= HandleCoresChanged;
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
            RunSession.OnArcaneCoresChanged -= HandleCoresChanged;
            RunSession.OnArcaneCoresChanged += HandleCoresChanged;
            ultimateController.OnUltimateActivated -= HandleActivated;
            ultimateController.OnUltimateActivated += HandleActivated;
            ultimateController.OnUltimateDeactivated -= HandleDeactivated;
            ultimateController.OnUltimateDeactivated += HandleDeactivated;
            isSubscribed = true;
            RefreshReady();
        }
    }

    private void UnbindController()
    {
        if (ultimateController != null && isSubscribed)
        {
            RunSession.OnArcaneCoresChanged -= HandleCoresChanged;
            ultimateController.OnUltimateActivated -= HandleActivated;
            ultimateController.OnUltimateDeactivated -= HandleDeactivated;
            isSubscribed = false;
        }
    }

    private void HandleCoresChanged(int cores) => RefreshReady();

    /// <summary>Full and glowing with the key while a core is banked; empty and dim without one.</summary>
    private void RefreshReady()
    {
        if (IsThisSlotActive) return;

        bool ready = RunSession.ArcaneCores > 0 && ultimateController != null && ultimateController.HasUltimate(slot);
        float fill = ready ? MaxFill : 0f;

        if (progressBar != null)
        {
            progressBar.UpdateBar(fill, 0f, MaxFill);
        }

        if (chargeText != null)
        {
            chargeText.text = ready ? Loc.Get("hud.ultimate.ready", "Ready") : Loc.Get("hud.ultimate.no_core", "No core");
        }

        bool wasFull = isFull;
        isFull = ready;

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
            progressBar.SetBar(MaxFill, 0f, MaxFill);
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
        RefreshReady();
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
                progressBar.SetBar(fraction * MaxFill, 0f, MaxFill);
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

        // The wheel opens on the Ultimate action's binding for the device in use (Q / LB by default).
        var player = Player.Instance;
        var map = player != null && player.InputSettings != null ? player.InputSettings.GetRebindableActionMap() : null;
        var action = map != null ? map.FindAction("Ultimate") : null;
        string key = null;
        if (action != null)
        {
            int bindIndex = action.GetBindingIndexForControl(action.controls.Count > 0 ? action.controls[0] : null);
            if (bindIndex >= 0) key = action.GetBindingDisplayString(bindIndex);
        }
        if (string.IsNullOrEmpty(key)) key = "Q";
        // Just the glyph: the label is sized for one key (the wheel and tutorial explain the hold).
        inputKeyText.text = key;
    }
}
