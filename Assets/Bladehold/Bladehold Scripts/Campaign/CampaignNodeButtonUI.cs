using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Interactive tactical node on the Campaign Map UI.
///     Displays state visuals (Locked, Available, Completed), difficulty indicators,
///     handles hover tooltips, and initiates sector deployment.
/// </summary>
public class CampaignNodeButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum NodeVisualStatus
    {
        Locked,
        Available,
        Completed,
        DemoLocked, // past the demo cutoff: shown, never playable
        Bypassed // behind the player's position, or on a branch they can no longer reach: faded out
    }

    [Header("Core References")]
    [SerializeField] private Button button;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image nodeIconImage;

    [Header("Labels")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text captainBadgeText;

    [Header("Status Overlays")]
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject completedCheckmark;
    [SerializeField] private GameObject activePulseGlow;

    [Header("Color Palettes")]
    [SerializeField] private Color availableBorderColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color completedBorderColor = new Color(0.3f, 0.7f, 0.4f, 1f);
    [SerializeField] private Color lockedBorderColor = new Color(0.35f, 0.35f, 0.4f, 0.8f);
    [Tooltip("Bypassed nodes: background blended this far towards grey.")]
    [Range(0f, 1f)] [SerializeField] private float bypassedDesaturate = 0.7f;
    [Tooltip("Bypassed nodes: alpha of the background, border and labels.")]
    [Range(0f, 1f)] [SerializeField] private float bypassedAlpha = 0.35f;
    [Tooltip("Reachable-later (Locked) nodes: alpha of the background, border and labels.")]
    [Range(0f, 1f)] [SerializeField] private float upcomingAlpha = 0.8f;

    [SerializeField] private Color combatNodeColor = new Color(0.55f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color restNodeColor = new Color(0.18f, 0.45f, 0.32f, 1f);
    [SerializeField] private Color fishingNodeColor = new Color(0.12f, 0.52f, 0.58f, 1f);
    [SerializeField] private Color bossNodeColor = new Color(0.45f, 0.15f, 0.55f, 1f);
    [Header("Type Icons (top-left badge; empty = no badge for that type)")]
    [SerializeField] private Sprite fishingIcon;
    [SerializeField] private Sprite combatIcon;
    [SerializeField] private Sprite restIcon;
    [SerializeField] private Sprite preBossIcon;
    [SerializeField] private Sprite bossIcon;
    [SerializeField] private Sprite cryptIcon;

    private CampaignNodeSO nodeData;
    private NodeVisualStatus currentStatus = NodeVisualStatus.Locked;
    private Action<CampaignNodeSO> onNodeSelectedCallback;
    private Action<CampaignNodeSO, NodeVisualStatus, Vector2> onHoveredCallback;
    private Action onHoverExitedCallback;

    private Vector3 originalScale = Vector3.one;
    private bool isHovered = false;

    public CampaignNodeSO NodeData => nodeData;
    public NodeVisualStatus CurrentStatus => currentStatus;
    public RectTransform Rect => rectTransform;

    private void OnValidate()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (button == null) button = GetComponent<Button>();
    }

    private void Awake()
    {
        originalScale = transform.localScale;

        if (button == null)
        {
            Debug.LogError($"[CampaignNodeButtonUI] '{name}' has no Button assigned; the node can't be clicked.", this);
            return;
        }

        // Clicks come only through the Button (it also handles keyboard/gamepad submit). Don't add an
        // IPointerClickHandler here too, or a mouse click deploys twice.
        button.onClick.AddListener(HandleButtonClick);
    }

    /// <summary>
    ///     Configures this button for a specific node in the campaign graph.
    /// </summary>
    public void Setup(
        CampaignNodeSO node,
        NodeVisualStatus status,
        Action<CampaignNodeSO> onClicked,
        Action<CampaignNodeSO, NodeVisualStatus, Vector2> onHovered,
        Action onHoverExited)
    {
        nodeData = node;
        currentStatus = status;
        onNodeSelectedCallback = onClicked;
        onHoveredCallback = onHovered;
        onHoverExitedCallback = onHoverExited;

        if (node == null) return;

        // Text labels
        if (titleText != null) titleText.text = node.nodeTitle;
        if (tierText != null) tierText.text = $"Tier {node.tierIndex}";

        // Captain badge
        if (captainBadgeText != null)
        {
            if (node.HasCaptain)
            {
                captainBadgeText.gameObject.SetActive(true);
                captainBadgeText.text = node.captainName; // tier shows as colour; the emoji skulls had no glyph in the font
                captainBadgeText.color = BannerDifficultyHelper.GetTierColor(node.difficultyTier);
            }
            else
            {
                captainBadgeText.gameObject.SetActive(false);
            }
        }

        // Apply node category background tint (faded for nodes the route has left behind)
        if (backgroundImage != null)
        {
            Color bg = GetNodeBgColor(node.nodeType);
            if (status == NodeVisualStatus.Bypassed)
            {
                float grey = bg.grayscale;
                bg = Color.Lerp(bg, new Color(grey, grey, grey, 1f), bypassedDesaturate);
            }
            bg.a *= StatusAlpha(status);
            backgroundImage.color = bg;
        }
        SetLabelAlpha(StatusAlpha(status));

        // Type badge (swords, campfire, fish, skull...)
        if (nodeIconImage != null)
        {
            Sprite icon = GetTypeIcon(node.nodeType);
            nodeIconImage.gameObject.SetActive(icon != null);
            if (icon != null) nodeIconImage.sprite = icon;
        }

        // Status Visuals
        ApplyStatusVisuals(status);
    }

    private void ApplyStatusVisuals(NodeVisualStatus status)
    {
        // Padlocks only where the demo stops; nodes further along the route just wait their turn.
        if (lockOverlay != null) lockOverlay.SetActive(status == NodeVisualStatus.DemoLocked);
        if (completedCheckmark != null) completedCheckmark.SetActive(status == NodeVisualStatus.Completed);
        if (activePulseGlow != null) activePulseGlow.SetActive(status == NodeVisualStatus.Available);

        if (borderImage != null)
        {
            switch (status)
            {
                case NodeVisualStatus.Available:
                    borderImage.color = availableBorderColor;
                    break;
                case NodeVisualStatus.Completed:
                    borderImage.color = completedBorderColor;
                    break;
                case NodeVisualStatus.Locked:
                case NodeVisualStatus.DemoLocked:
                case NodeVisualStatus.Bypassed:
                default:
                    Color border = lockedBorderColor;
                    border.a *= StatusAlpha(status);
                    borderImage.color = border;
                    break;
            }
        }

        if (button != null)
        {
            button.interactable = (status == NodeVisualStatus.Available);
        }
    }

    private float StatusAlpha(NodeVisualStatus status)
    {
        switch (status)
        {
            case NodeVisualStatus.Bypassed: return bypassedAlpha;
            case NodeVisualStatus.Locked: return upcomingAlpha;
            default: return 1f;
        }
    }

    private void SetLabelAlpha(float alpha)
    {
        foreach (TMP_Text label in new[] { titleText, tierText, captainBadgeText })
        {
            if (label != null) label.alpha = alpha;
        }
        if (nodeIconImage != null)
        {
            Color c = nodeIconImage.color;
            c.a = alpha;
            nodeIconImage.color = c;
        }
    }

    private Sprite GetTypeIcon(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return combatIcon;
            case CampaignNodeType.RestArea: return restIcon;
            case CampaignNodeType.FishingPond: return fishingIcon;
            case CampaignNodeType.PreBoss: return preBossIcon;
            case CampaignNodeType.NecromancerEncounter: return cryptIcon;
            case CampaignNodeType.PrincessBoss:
            case CampaignNodeType.NecromancerBoss:
                return bossIcon;
            default: return null;
        }
    }

    private Color GetNodeBgColor(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return combatNodeColor;
            case CampaignNodeType.RestArea: return restNodeColor;
            case CampaignNodeType.FishingPond: return fishingNodeColor;
            case CampaignNodeType.PreBoss:
            case CampaignNodeType.NecromancerEncounter:
            case CampaignNodeType.PrincessBoss:
            case CampaignNodeType.NecromancerBoss:
                return bossNodeColor;
            default: return combatNodeColor;
        }
    }

    private void Update()
    {
        // Smooth scale lerp on hover / keyboard-gamepad focus. Unplayable nodes grow less so focus still reads.
        Vector3 targetScale = originalScale;
        if (isHovered)
        {
            targetScale = originalScale * (currentStatus == NodeVisualStatus.Available ? 1.08f : 1.04f);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 14f);
    }

    /// <summary>
    ///     Keyboard/gamepad focus from <see cref="CampaignMapUI" />: same visuals and tooltip as a mouse hover.
    /// </summary>
    public void SetFocused(bool focused)
    {
        if (focused)
        {
            OnPointerEnter(null);
        }
        else
        {
            OnPointerExit(null);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        Vector2 screenPos = rectTransform != null ? (Vector2)rectTransform.position : (Vector2)transform.position;
        onHoveredCallback?.Invoke(nodeData, currentStatus, screenPos);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        onHoverExitedCallback?.Invoke();
    }

    private void HandleButtonClick()
    {
        if (currentStatus != NodeVisualStatus.Available) return;
        if (nodeData == null) return;

        onNodeSelectedCallback?.Invoke(nodeData);
    }
}
