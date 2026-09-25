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
        Completed
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

    [SerializeField] private Color combatNodeColor = new Color(0.55f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color restNodeColor = new Color(0.18f, 0.45f, 0.32f, 1f);
    [SerializeField] private Color supplyNodeColor = new Color(0.18f, 0.35f, 0.55f, 1f);
    [SerializeField] private Color fishingNodeColor = new Color(0.12f, 0.52f, 0.58f, 1f);
    [SerializeField] private Color bossNodeColor = new Color(0.45f, 0.15f, 0.55f, 1f);
    [SerializeField] private Sprite fishingIcon;

    private CampaignNodeSO nodeData;
    private NodeVisualStatus currentStatus = NodeVisualStatus.Locked;
    private Action<CampaignNodeSO> onNodeSelectedCallback;
    private Action<CampaignNodeSO, NodeVisualStatus, Vector2> onHoveredCallback;
    private Action onHoverExitedCallback;

    private Vector3 originalScale = Vector3.one;
    private bool isHovered = false;

    public CampaignNodeSO NodeData => nodeData;
    public NodeVisualStatus CurrentStatus => currentStatus;

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
                string skulls = BannerDifficultyHelper.GetSkullString(node.difficultyTier);
                captainBadgeText.text = $"{skulls} {node.captainName}";
                captainBadgeText.color = BannerDifficultyHelper.GetTierColor(node.difficultyTier);
            }
            else
            {
                captainBadgeText.gameObject.SetActive(false);
            }
        }

        // Apply node category background tint
        if (backgroundImage != null)
        {
            backgroundImage.color = GetNodeBgColor(node.nodeType);
        }

        // Icon (e.g. fishing pond)
        if (nodeIconImage != null)
        {
            if (node.nodeType == CampaignNodeType.FishingPond && fishingIcon != null)
            {
                nodeIconImage.gameObject.SetActive(true);
                nodeIconImage.sprite = fishingIcon;
            }
            else
            {
                nodeIconImage.gameObject.SetActive(false);
            }
        }

        // Status Visuals
        ApplyStatusVisuals(status);
    }

    private void ApplyStatusVisuals(NodeVisualStatus status)
    {
        if (lockOverlay != null) lockOverlay.SetActive(status == NodeVisualStatus.Locked);
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
                default:
                    borderImage.color = lockedBorderColor;
                    break;
            }
        }

        if (button != null)
        {
            button.interactable = (status == NodeVisualStatus.Available);
        }
    }

    private Color GetNodeBgColor(CampaignNodeType type)
    {
        switch (type)
        {
            case CampaignNodeType.Combat: return combatNodeColor;
            case CampaignNodeType.RestArea: return restNodeColor;
            case CampaignNodeType.SupplyRoom: return supplyNodeColor;
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
        // Smooth scale lerp on hover
        Vector3 targetScale = (isHovered && currentStatus == NodeVisualStatus.Available)
            ? originalScale * 1.08f
            : originalScale;

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 14f);
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
