using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Centre-screen tell for directional melee. The left, right, and overhead
///     indicators are authored in the HUD prefab and assigned through the Inspector.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SwingDirectionIndicatorUI : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("PlayerAttack to watch. Auto-bound from Player.Instance if left empty.")]
    [SerializeField] private PlayerAttack playerAttack;

    [Tooltip("SwingDirectionSelector to watch. Auto-bound from the PlayerAttack's GameObject if left empty.")]
    [SerializeField] private SwingDirectionSelector selector;

    [Header("UI References")]
    [Tooltip("CanvasGroup controlling the visibility of the complete indicator.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Indicator displayed for a swing from the right.")]
    [SerializeField] private Image rightIndicator;

    [Tooltip("Indicator displayed for a swing from the left.")]
    [SerializeField] private Image leftIndicator;

    [Tooltip("Indicator displayed for an overhead swing.")]
    [SerializeField] private Image overheadIndicator;

    [Header("Timing (unscaled seconds)")]
    [Tooltip("How long the indicator stays fully visible after the swing is released.")]
    [SerializeField] private float lingerAfterRelease = 0.3f;

    [Tooltip("Alpha per second when fading in.")]
    [SerializeField] private float fadeInSpeed = 14f;

    [Tooltip("Alpha per second when fading out.")]
    [SerializeField] private float fadeOutSpeed = 5f;

    [Header("Feedback")]
    [Tooltip("Pop feedback played when the right swing direction is latched.")]
    [SerializeField] private MMF_Player rightPopFeedback;

    [Tooltip("Pop feedback played when the left swing direction is latched.")]
    [SerializeField] private MMF_Player leftPopFeedback;

    [Tooltip("Pop feedback played when the overhead swing direction is latched.")]
    [SerializeField] private MMF_Player overheadPopFeedback;

    [Header("Look")]
    [Tooltip("Alpha of the two unlit indicators.")]
    [Range(0f, 1f)]
    [SerializeField] private float unlitAlpha = 0.3f;

    private SwingDirectionSelector subscribedSelector;
    private float visibleUntil = -1f;
    private bool anyError;

    private void Start()
    {
        ValidateReferences();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        TryBind();
    }

    private void ValidateReferences()
    {
        if (canvasGroup == null)
        {
            Debug.LogError(
                "SwingDirectionIndicatorUI: CanvasGroup is not assigned.",
                this);
            anyError = true;
        }

        if (rightIndicator == null ||
            leftIndicator == null ||
            overheadIndicator == null)
        {
            Debug.LogError(
                "SwingDirectionIndicatorUI: Right, left, and overhead indicators must be assigned.",
                this);
            anyError = true;
        }

        if (rightPopFeedback == null)
        {
            Debug.LogError(
                "SwingDirectionIndicatorUI: Right pop feedback is not assigned.",
                this);
        }

        if (leftPopFeedback == null)
        {
            Debug.LogError(
                "SwingDirectionIndicatorUI: Left pop feedback is not assigned.",
                this);
        }

        if (overheadPopFeedback == null)
        {
            Debug.LogError(
                "SwingDirectionIndicatorUI: Overhead pop feedback is not assigned.",
                this);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribedSelector == null)
        {
            return;
        }

        subscribedSelector.Latched -= HandleLatched;
        subscribedSelector = null;
    }

    private void TryBind()
    {
        if (playerAttack == null && Player.Instance != null)
        {
            playerAttack = Player.Instance.GetComponent<PlayerAttack>();
        }

        if (selector == null && playerAttack != null)
        {
            selector = playerAttack.GetComponent<SwingDirectionSelector>();
        }

        if (selector == subscribedSelector)
        {
            return;
        }

        Unsubscribe();

        if (selector != null)
        {
            selector.Latched += HandleLatched;
            subscribedSelector = selector;
        }
    }

    private void HandleLatched(SwingDirection direction)
    {
        visibleUntil = Time.unscaledTime + lingerAfterRelease;

        MMF_Player popFeedback = direction switch
        {
            SwingDirection.Right => rightPopFeedback,
            SwingDirection.Left => leftPopFeedback,
            SwingDirection.Overhead => overheadPopFeedback,
            _ => null,
        };

        if (popFeedback != null)
        {
            popFeedback.PlayFeedbacks();
        }
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        if (playerAttack == null || selector == null)
        {
            TryBind();
        }

        float now = Time.unscaledTime;
        bool charging = playerAttack != null && playerAttack.IsCharging;

        if (charging)
        {
            visibleUntil = now + lingerAfterRelease;
        }

        bool show = selector != null && now <= visibleUntil;
        float targetAlpha = show ? 1f : 0f;
        float fadeSpeed = show ? fadeInSpeed : fadeOutSpeed;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime);

        if (canvasGroup.alpha <= 0f || selector == null)
        {
            return;
        }

        UIThemeSO theme = UITheme.For(this);
        SwingDirection current = selector.Current;
        float charge = charging ? playerAttack.ChargeProgress : 1f;

        Color litColor = selector.CurrentIsIntentional
            ? Color.Lerp(
                theme.Get(UIColorRole.AccentMuted),
                theme.Get(UIColorRole.Accent),
                0.5f + 0.5f * charge)
            : theme.Get(UIColorRole.TextDim);

        Color unlitColor = theme.Get(UIColorRole.Text, unlitAlpha);

        UpdateIndicator(
            rightIndicator,
            current == SwingDirection.Right,
            litColor,
            unlitColor);

        UpdateIndicator(
            leftIndicator,
            current == SwingDirection.Left,
            litColor,
            unlitColor);

        UpdateIndicator(
            overheadIndicator,
            current == SwingDirection.Overhead,
            litColor,
            unlitColor);
    }

    private static void UpdateIndicator(
        Image indicator,
        bool isLit,
        Color litColor,
        Color unlitColor)
    {
        indicator.color = isLit ? litColor : unlitColor;
    }
}
